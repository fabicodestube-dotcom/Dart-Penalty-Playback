using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatisticsPageATC : MonoBehaviour
{
    [Header("Swipe / Pages")]
    public Transform parent;

    [Header("Prefabs")]
    public GameObject prefabTable;
    public GameObject prefabHeadline;
    public GameObject prefabHitSectorChart;

    [Header("Icons")]
    public Sprite iconWall;
    public Sprite iconCeiling;
    public Sprite iconMiss;
    public Sprite iconOnes;
    public Sprite iconSchnaps;
    public Sprite iconLost;


    private readonly Dictionary<string, TableView> cachedTables = new Dictionary<string, TableView>();
    private readonly Dictionary<string, HitSectorChart> cachedCharts = new Dictionary<string, HitSectorChart>();
    private GameObject attemptsHeadline;


    private static readonly string ATCPenaltiesKey = "ATC_Penalties";
    private static readonly string ATCParticipationKey = "ATC_Participation";
    private static readonly string ATCLegsKey = "ATC_Legs";
    private static readonly string ATCScoringKey = "ATC_Scoring";
    private static readonly string ATCStreaksKey = "ATC_Streaks";
    private static readonly string ATCHighscoresKey = "ATC_Highscores";

    [Header("Penalty-Spalten-Detail")]
    [Tooltip("Detail-Popup für Header-Taps (optional, sonst Auto-Find).")]
    public PenaltyDetailPopup columnDetailPopup;

    private PenaltyDetailPopup ColumnPopup
    {
        get
        {
            if (columnDetailPopup == null)
                columnDetailPopup = PenaltyDetailPopup.FindVisibleInstance();
            return columnDetailPopup;
        }
    }

    [Header("Penalty-Resolver (TEMP-DIAG)")]
    [Tooltip("Löst Penalty-Stats zum Tap-Zeitpunkt frisch auf (statt Build-Zeit-Closure). Wird vom StatisticsHandler gesetzt.")]
    public Func<string, GameStats> penaltyStatsResolver;

    /// <summary>
    /// Tap-Handler mit frischem Lookup (Stale-Closure-Fix): Stats werden erst
    /// beim Antippen aus BasePlayer/currentRange geholt, Closure nur Fallback.
    /// </summary>
    private void ShowPenaltyDetails(PenaltyDetailPopup popup, string playerName, GameStats fallbackStats)
    {
        GameStats resolved = null;
        try { resolved = penaltyStatsResolver?.Invoke(playerName); }
        catch (Exception e) { Debug.LogWarning($"[PenaltyDiag] Resolver fehlgeschlagen ({playerName}): {e.Message}"); }

        var used = resolved ?? fallbackStats;
        Debug.Log($"[PenaltyDiag] Tap {playerName}: fallback(hash={(fallbackStats != null ? fallbackStats.GetHashCode().ToString() : "null")}," +
            $"anzahlFallback={(fallbackStats != null ? PenaltyDisplay.TotalPenaltyCount(fallbackStats).ToString() : "-")}," +
            $"kostenFallback={(fallbackStats != null ? fallbackStats.totalPenaltyCost.ToString("0.##") : "-")} " +
            $"resolved(hash={(resolved != null ? resolved.GetHashCode().ToString() : "null")}," +
            $"anzahl={(used != null ? PenaltyDisplay.TotalPenaltyCount(used).ToString() : "-")}," +
            $"kosten={(used != null ? used.totalPenaltyCost.ToString("0.##") : "-")})");

        if (popup != null)
            popup.ShowPlayerAggregates(playerName, used);
    }


    public IEnumerator ShowStatsAsync(List<GameStatsATC> stats, List<string> playerNames)
    {
        yield return BuildATCStatsCoroutine(stats, playerNames);
    }

    /// <summary>
    /// Gezieltes Refresh EINER Spielerzeile (alle Tabellen + Chart dieser Page).
    /// Kein Neuaufbau, kein Clear — nutzt gecachte Tabellen/Charts.
    /// </summary>
    public void UpsertPlayerRow(GameStatsATC stats, string playerName)
    {
        if (stats == null || string.IsNullOrEmpty(playerName) || parent == null)
            return;

        var participationTable = GetOrCreateCachedTable(
            ATCParticipationKey,
            parent,
            "Games",
            new TableCellData[] { "Played", "Won", "Win %" });

        if (participationTable != null)
        {
            int participated = stats.gameCount;
            int won = stats.gamesWon;
            float pct = participated == 0 ? 0f : (won / (float)participated) * 100f;

            participationTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                participated.ToString(),
                won.ToString(),
                $"{pct:0.00}%"
            });
            participationTable.SetRowActive(playerName, true);
        }

        var penaltyTable = GetOrCreateCachedTable(
            ATCPenaltiesKey,
            parent,
            "Penalties",
            new TableCellData[] { "Count", "Costs", "Details" });

        if (penaltyTable != null)
        {
            penaltyTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                PenaltyDisplay.TotalPenaltyCount(stats).ToString(),
                stats.totalPenaltyCost.ToString("0"),
                "› Details"
            });
            penaltyTable.SetRowActive(playerName, true);

            var popup = ColumnPopup;
            if (popup != null && penaltyTable.TryGetRow(playerName, out var rowGo) && rowGo != null)
            {
                var row = rowGo.GetComponent<DynamicTableRow>();
                row?.WireCellTaps(_ => ShowPenaltyDetails(popup, playerName, stats));
            }
        }

        var legsTable = GetOrCreateCachedTable(
            ATCLegsKey,
            parent,
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" });

        if (legsTable != null)
        {
            int played = stats.totalLegsCount;
            int wonLegs = stats.totalLegsWon;
            float legsPct = played == 0 ? 0f : (wonLegs / (float)played) * 100f;

            legsTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                played.ToString(),
                wonLegs.ToString(),
                $"{legsPct:0.00}%"
            });
            legsTable.SetRowActive(playerName, true);
        }

        var scoringTable = GetOrCreateCachedTable(
            ATCScoringKey,
            parent,
            "Scoring",
            new TableCellData[] { "Ø Throws", "Ø Hits", "Ø Hit %" });

        if (scoringTable != null)
        {
            scoringTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.totalTargets.ToString(),
                stats.targetsHit.ToString(),
                $"{(stats.hitPercentage * 100f):0.0}%"
            });
            scoringTable.SetRowActive(playerName, true);
        }

        var streaksTable = GetOrCreateCachedTable(
            ATCStreaksKey,
            parent,
            "Streaks",
            new TableCellData[] { "1st Dart Hit%", "Longest Streak", "Biggest Choke" });

        if (streaksTable != null)
        {
            var (target, attempts) = stats.GetChoke();
            string chokeDisplay = target != -1 ? $"{target} ({attempts})" : "-";

            streaksTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                $"{(stats.firstDartHitPercentage * 100f):0.00}%",
                stats.longestHitStreak.ToString(),
                chokeDisplay
            });
            streaksTable.SetRowActive(playerName, true);
        }

        var highscoreTable = GetOrCreateCachedTable(
            ATCHighscoresKey,
            parent,
            "Highscores",
            new TableCellData[] { "3 Hits +", "6 Hits +", "9 Hits +", "12 Hits +" });

        if (highscoreTable != null)
        {
            highscoreTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.streak3Plus.ToString(),
                stats.streak6Plus.ToString(),
                stats.streak9Plus.ToString(),
                stats.streak12Plus.ToString(),
            });
            highscoreTable.SetRowActive(playerName, true);
        }

        if (prefabHitSectorChart != null)
        {
            if (attemptsHeadline == null)
                attemptsHeadline = CreateHeadline(parent, "Attempts per Field");
            else
                attemptsHeadline.SetActive(true);

            var chart = GetOrCreateChart(playerName);
            if (chart != null)
            {
                chart.gameObject.SetActive(true);
                ATCAttemptsChart.SetupChart(chart, playerName, stats.throwSectorCounts);
            }
        }

        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
    }

    /// <summary>
    /// Blendet alle Zeilen + Chart eines Spielers auf dieser Page um.
    /// Legt nichts an — nur Bestehendes.
    /// Gibt true zurück, wenn mindestens eine Zeile / der Chart WIRKLICH
    /// umgeschaltet hat (nur dann muss das Layout via RefreshLayout neu
    /// berechnet werden).
    /// </summary>
    public bool SetPlayerRowVisible(string playerName, bool visible)
    {
        if (string.IsNullOrEmpty(playerName))
            return false;

        bool changed = false;
        foreach (var table in cachedTables.Values)
        {
            if (table != null && table.HasRow(playerName))
                changed |= table.SetRowActive(playerName, visible);
        }

        if (cachedCharts.TryGetValue(playerName, out var chart) && chart != null)
        {
            if (chart.gameObject.activeSelf != visible)
            {
                chart.gameObject.SetActive(visible);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Rechnet alle Tabellenhöhen dieser Page aus den SICHTBAREN Zeilen neu
    /// (Divider + LayoutElement.preferredHeight). Nur aufrufen, wenn sich die
    /// sichtbare Zeilenzahl wirklich geändert hat. Charts kollabieren von
    /// selbst übers Layout (inaktiv = ignoriert).
    /// </summary>
    public void RefreshLayout()
    {
        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
    }

    /// <summary>
    /// True, wenn irgendeine Tabelle weniger als `desired` Reserve-Rows hat.
    /// </summary>
    public bool SpareShortage(int desired)
    {
        foreach (var table in cachedTables.Values)
        {
            if (table != null && table.PooledSpareRows < desired)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Füllt Reserve-Rows aller Tabellen auf (gechunkt, eine Tabelle pro Frame).
    /// </summary>
    public IEnumerator PreallocateSpareRowsAsync(int desired)
    {
        foreach (var table in cachedTables.Values)
        {
            if (table != null)
                table.EnsureSpareRows(desired);
            yield return null;
        }
    }


    private TableView GetOrCreateCachedTable(string key, Transform parent, string title, TableCellData[] header)
    {
        if (string.IsNullOrEmpty(key) || parent == null)
            return null;

        if (cachedTables.TryGetValue(key, out var existingTable) && existingTable != null)
        {
            existingTable.SetTitle(title);
            existingTable.SetHeader(header);
            return existingTable;
        }

        if (prefabTable == null)
            return null;

        var table = Instantiate(prefabTable, parent).GetComponent<TableView>();
        if (table == null)
            return null;

        table.Build(title, header);
        cachedTables[key] = table;
        return table;
    }

    private void ClearPageChildrenExceptCachedTables(Transform parent)
    {
        if (parent == null)
            return;

        var preserved = new HashSet<GameObject>(cachedTables.Values
            .Where(table => table != null && table.transform.parent == parent)
            .Select(table => table.gameObject));

        foreach (var chart in cachedCharts.Values)
        {
            if (chart != null && chart.transform.parent == parent)
                preserved.Add(chart.gameObject);
        }

        if (attemptsHeadline != null && attemptsHeadline.transform.parent == parent)
            preserved.Add(attemptsHeadline);

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (!preserved.Contains(child))
                Destroy(child);
        }
    }

    public void RefreshLocalization()
    {
        foreach (var table in cachedTables.Values)
            table?.RefreshLocalization();
        if (attemptsHeadline != null)
        {
            var tmp = attemptsHeadline.GetComponent<TMP_Text>();
            if (tmp != null)
                tmp.text = LocalizationText.GetByEnglish("Attempts per Field", "Attempts per Field");
        }
    }

    private GameObject CreateHeadline(Transform parentTransform, string text)
    {
        if (prefabHeadline == null || parentTransform == null) return null;
        var go = Instantiate(prefabHeadline, parentTransform);
        var tmp = go.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = LocalizationText.GetByEnglish(text, text);
            // Perf (M1): Headline ist nie interaktiv.
            if (tmp.raycastTarget)
                tmp.raycastTarget = false;
        }
        return go;
    }

    private HitSectorChart GetOrCreateChart(string playerKey)
    {
        if (cachedCharts.TryGetValue(playerKey, out var existingChart) && existingChart != null)
            return existingChart;

        if (prefabHitSectorChart == null)
            return null;

        var chart = Instantiate(prefabHitSectorChart, parent).GetComponent<HitSectorChart>();
        if (chart == null)
        {
            Debug.LogError("prefabHitSectorChart has no HitSectorChart component");
            return null;
        }

        cachedCharts[playerKey] = chart;
        return chart;
    }


    private IEnumerator BuildATCStatsCoroutine(List<GameStatsATC> gameStatsATCs, List<string> playerNames)
    {
        bool originalParentState = parent.gameObject.activeSelf;
        parent.gameObject.SetActive(false);
        var pageLayout = StatisticsPageBuildHelper.SuspendPageLayout(parent);

        ClearPageChildrenExceptCachedTables(parent);

        // =====================================================
        // 1. PARTICIPATION
        // =====================================================
        var participationTable = GetOrCreateCachedTable(
            ATCParticipationKey,
            parent,
            "Games",
            new TableCellData[] { "Played", "Won", "Win %" });

        // participationTable.gameObject.SetActive(true);

        var activeParticipationKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsATCs.Count; i++)
        {
            var stats = gameStatsATCs[i];
            string key = playerNames[i];

            int participated = stats?.gameCount ?? 0;
            int won = stats?.gamesWon ?? 0;
            float pct = participated == 0 ? 0f : (won / (float)participated) * 100f;

            participationTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                participated.ToString(),
                won.ToString(),
                $"{pct:0.00}%"
            }, false);

            participationTable.SetRowActive(key, true);
            activeParticipationKeys.Add(key);
        }

        foreach (var k in participationTable.GetRowKeys())
            if (!activeParticipationKeys.Contains(k))
                participationTable.SetRowActive(k, false);

        // Frame abgeben (wie X01: kein N-Spieler-Burst pro Frame).
        yield return null;

        // =====================================================
        // 2. PENALTIES (einfach: Anzahl | Kosten | Details → Popup)
        // =====================================================
        var penaltyTable = GetOrCreateCachedTable(
            ATCPenaltiesKey,
            parent,
            "Penalties",
            new TableCellData[] { "Count", "Costs", "Details" });

        penaltyTable.gameObject.SetActive(true);

        var activePenaltyKeys = new HashSet<string>();
        var popup = ColumnPopup;

        for (int i = 0; i < playerNames.Count; i++)
        {
            var stats = i < gameStatsATCs.Count ? gameStatsATCs[i] : null;
            string key = playerNames[i];
            string playerName = playerNames[i];

            penaltyTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                PenaltyDisplay.TotalPenaltyCount(stats).ToString(),
                (stats?.totalPenaltyCost ?? 0f).ToString("0"),
                "› Details"
            }, false);

            penaltyTable.SetRowActive(key, true);
            activePenaltyKeys.Add(key);

            if (popup != null && penaltyTable.TryGetRow(key, out var rowGo) && rowGo != null)
            {
                var row = rowGo.GetComponent<DynamicTableRow>();
                row?.WireCellTaps(_ => ShowPenaltyDetails(popup, playerName, stats));
            }
        }

        foreach (var k in penaltyTable.GetRowKeys())
            if (!activePenaltyKeys.Contains(k))
                penaltyTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // 3. LEGS
        // =====================================================
        var legsTable = GetOrCreateCachedTable(
            ATCLegsKey,
            parent,
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" });

        legsTable.gameObject.SetActive(true);

        var activeLegKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsATCs.Count; i++)
        {
            var stats = gameStatsATCs[i];
            string key = playerNames[i];

            int played = stats?.totalLegsCount ?? 0;
            int won = stats?.totalLegsWon ?? 0;
            float pct = played == 0 ? 0f : (won / (float)played) * 100f;

            legsTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                played.ToString(),
                won.ToString(),
                $"{pct:0.00}%"
            }, false);

            legsTable.SetRowActive(key, true);
            activeLegKeys.Add(key);
        }

        foreach (var k in legsTable.GetRowKeys())
            if (!activeLegKeys.Contains(k))
                legsTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // 4. SCORING
        // =====================================================
        var scoringTable = GetOrCreateCachedTable(
            ATCScoringKey,
            parent,
            "Scoring",
            new TableCellData[] { "Ø Throws", "Ø Hits", "Ø Hit %" });

        scoringTable.gameObject.SetActive(true);

        var activeScoringKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsATCs.Count; i++)
        {
            var stats = gameStatsATCs[i];
            string key = playerNames[i];

            scoringTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats != null ? stats.totalTargets.ToString() : "0",
                stats != null ? stats.targetsHit.ToString() : "0",
                stats != null ? $"{(stats.hitPercentage * 100f):0.0}%" : "0.0%"
            }, false);

            scoringTable.SetRowActive(key, true);
            activeScoringKeys.Add(key);
        }

        foreach (var k in scoringTable.GetRowKeys())
            if (!activeScoringKeys.Contains(k))
                scoringTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // 5. STREAKS
        // =====================================================
        var streaksTable = GetOrCreateCachedTable(
            ATCStreaksKey,
            parent,
            "Streaks",
            new TableCellData[] { "1st Dart Hit%", "Longest Streak", "Biggest Choke" });

        streaksTable.gameObject.SetActive(true);

        var activeStreakKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsATCs.Count; i++)
        {
            var stats = gameStatsATCs[i];
            string key = playerNames[i];

            string chokeDisplay = "-";

            if (stats != null)
            {
                var (target, attempts) = stats.GetChoke();
                chokeDisplay = target != -1 ? $"{target} ({attempts})" : "-";
            }

            streaksTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats != null ? $"{(stats.firstDartHitPercentage * 100f):0.00}%" : "0.00%",
                stats != null ? stats.longestHitStreak.ToString() : "0",
                chokeDisplay
            }, false);

            streaksTable.SetRowActive(key, true);
            activeStreakKeys.Add(key);
        }

        foreach (var k in streaksTable.GetRowKeys())
            if (!activeStreakKeys.Contains(k))
                streaksTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // 6. HIGHSCORES
        // =====================================================
        var highscoreTable = GetOrCreateCachedTable(
            ATCHighscoresKey,
            parent,
            "Highscores",
            new TableCellData[] { "3 Hits +", "6 Hits +", "9 Hits +", "12 Hits +" });

        highscoreTable.gameObject.SetActive(true);

        var activeHighKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsATCs.Count; i++)
        {
            var stats = gameStatsATCs[i];
            string key = playerNames[i];

            highscoreTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats != null ? stats.streak3Plus.ToString() : "0",
                stats != null ? stats.streak6Plus.ToString() : "0",
                stats != null ? stats.streak9Plus.ToString() : "0",
                stats != null ? stats.streak12Plus.ToString() : "0",
            }, false);

            highscoreTable.SetRowActive(key, true);
            activeHighKeys.Add(key);
        }

        foreach (var k in highscoreTable.GetRowKeys())
            if (!activeHighKeys.Contains(k))
                highscoreTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // ATTEMPTS PER FIELD (X01-Prefab wiederverwendet)
        // =====================================================
        if (prefabHitSectorChart != null)
        {
            if (attemptsHeadline == null)
                attemptsHeadline = CreateHeadline(parent, "Attempts per Field");
            else
                attemptsHeadline.SetActive(true);

            var activeChartKeys = new HashSet<string>();

            for (int i = 0; i < gameStatsATCs.Count; i++)
            {
                var stats = gameStatsATCs[i];
                string key = playerNames[i];

                var chart = GetOrCreateChart(key);
                if (chart == null)
                    continue;

                ATCAttemptsChart.SetupChart(chart, key, stats?.throwSectorCounts);
                activeChartKeys.Add(key);

                // Perf (M6): Chart-Instantiate + Bar-Setup pro Spieler abgeben.
                if ((i & 1) == 1)
                    yield return null;
            }

            foreach (var chartKey in cachedCharts.Keys.ToList())
            {
                if (!activeChartKeys.Contains(chartKey) && cachedCharts[chartKey] != null)
                    cachedCharts[chartKey].gameObject.SetActive(false);
            }
        }

        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
        StatisticsPageBuildHelper.FinalizePage(parent, pageLayout, originalParentState);

        yield return null;
    }
}