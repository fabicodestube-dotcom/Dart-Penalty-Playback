using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatisticsPageCricket : MonoBehaviour
{
    [Header("Swipe / Pages")]
    public Transform parent;

    [Header("Prefabs")]
    public GameObject prefabTable;
    public GameObject prefabHeadline;

    [Header("Icons")]
    public Sprite iconWall;
    public Sprite iconCeiling;
    public Sprite iconMiss;
    public Sprite iconOnes;
    public Sprite iconSchnaps;
    public Sprite iconLost;


    private readonly Dictionary<string, TableView> cachedTables = new Dictionary<string, TableView>();


    private static readonly string CricketPenaltiesKey = "Cricket_Penalties";
    private static readonly string CricketParticipationKey = "Cricket_Participation";
    private static readonly string CricketLegsKey = "Cricket_Legs";
    private static readonly string CricketThrowsKey = "Cricket_Throws";
    private static readonly string CricketScoresKey = "Cricket_Scores";
    private static readonly string CricketMprKey = "Cricket_MPR";
    private static readonly string CricketHighscoresKey = "Cricket_Highscores";
    private static readonly string CricketPreferencesKey = "Cricket_Preferences";

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


    public IEnumerator ShowStatsAsync(List<GameStatsCricket> stats, List<string> playerNames)
    {
        yield return BuildCricketStatsCoroutine(stats, playerNames);
    }

    /// <summary>
    /// Gezieltes Refresh EINER Spielerzeile (alle Tabellen dieser Page).
    /// Kein Neuaufbau, kein Clear — nutzt die gecachten Tabellen.
    /// </summary>
    public void UpsertPlayerRow(GameStatsCricket stats, string playerName)
    {
        if (stats == null || string.IsNullOrEmpty(playerName) || parent == null)
            return;

        var participationTable = GetOrCreateCachedTable(
            CricketParticipationKey,
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
            CricketPenaltiesKey,
            parent,
            "Penalties",
            new TableCellData[] { "Anzahl", "Kosten", "Details" });

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
            CricketLegsKey,
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

        var throwsTable = GetOrCreateCachedTable(
            CricketThrowsKey,
            parent,
            "Throws",
            new TableCellData[] { "Ø Throws", "Double %", "Triple %" });

        if (throwsTable != null)
        {
            throwsTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.AverageThrowsPerGame.ToString("0.00"),
                $"{stats.doublePercentage * 100f:0.00}%",
                $"{stats.triplePercentage * 100f:0.00}%"
            });
            throwsTable.SetRowActive(playerName, true);
        }

        var mprTable = GetOrCreateCachedTable(
            CricketMprKey,
            parent,
            "Marks",
            new TableCellData[] { "Marks", "Max MPR", "MPR" });

        if (mprTable != null)
        {
            mprTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.marksPerRound.ToString("0.00"),
                stats.markCount.ToString(),
                stats.maxMPR.ToString("0.00")
            });
            mprTable.SetRowActive(playerName, true);
        }

        var scoresTable = GetOrCreateCachedTable(
            CricketScoresKey,
            parent,
            "Points",
            new TableCellData[] { "Ø Points", "Max Points", "Overkill Points" });

        if (scoresTable != null)
        {
            scoresTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.AverageScorePerTurn.ToString("0.00"),
                stats.MaxScoreInTurn.ToString(),
                stats.TotalOverkillPoints.ToString()
            });
            scoresTable.SetRowActive(playerName, true);
        }

        var highscoresTable = GetOrCreateCachedTable(
            CricketHighscoresKey,
            parent,
            "Highscores",
            new TableCellData[] { "9 Marks", "White Horse", "Same Triple" });

        if (highscoresTable != null)
        {
            highscoresTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.nineMarkTurns.ToString(),
                stats.whiteHorseTurns.ToString(),
                stats.sameTripleTurns.ToString()
            });
            highscoresTable.SetRowActive(playerName, true);
        }

        var preferencesTable = GetOrCreateCachedTable(
            CricketPreferencesKey,
            parent,
            "Preferences",
            new TableCellData[] { "First Closed", "Last Closed", "Most Points" });

        if (preferencesTable != null)
        {
            preferencesTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.firstClosedField == -1 ? "-" : stats.firstClosedField.ToString(),
                stats.lastClosedField == -1 ? "-" : stats.lastClosedField.ToString(),
                stats.maxPointsField == -1 ? "-" : stats.maxPointsField.ToString()
            });
            preferencesTable.SetRowActive(playerName, true);
        }

        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
    }

    /// <summary>
    /// Blendet alle Zeilen eines Spielers auf dieser Page um.
    /// Legt nichts an — nur Bestehendes.
    /// Gibt true zurück, wenn mindestens eine Zeile WIRKLICH umgeschaltet hat
    /// (nur dann muss die Tabellenhöhe via RefreshLayout neu berechnet werden).
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

        return changed;
    }

    /// <summary>
    /// Rechnet alle Tabellenhöhen dieser Page aus den SICHTBAREN Zeilen neu
    /// (Divider + LayoutElement.preferredHeight). Nur aufrufen, wenn sich die
    /// sichtbare Zeilenzahl wirklich geändert hat.
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

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (!preserved.Contains(child))
                Destroy(child);
        }
    }


    private IEnumerator BuildCricketStatsCoroutine(List<GameStatsCricket> gameStatsX01s, List<string> playerNames)
    {
         bool originalParentState = parent.gameObject.activeSelf;
        parent.gameObject.SetActive(false);
        var pageLayout = StatisticsPageBuildHelper.SuspendPageLayout(parent);

        ClearPageChildrenExceptCachedTables(parent);

        // =====================================================
        // GAMES / PARTICIPATION
        // =====================================================
        var participationTable = GetOrCreateCachedTable(
            CricketParticipationKey,
            parent,
            "Games",
            new TableCellData[] { "Played", "Won", "Win %" });

        participationTable.gameObject.SetActive(true);

        var activeParticipationKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

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
        // PENALTIES (einfach: Anzahl | Kosten | Details → Popup)
        // =====================================================
        var penaltyTable = GetOrCreateCachedTable(
            CricketPenaltiesKey,
            parent,
            "Penalties",
            new TableCellData[] { "Anzahl", "Kosten", "Details" });

        penaltyTable.gameObject.SetActive(true);

        var activePenaltyKeys = new HashSet<string>();
        var popup = ColumnPopup;

        for (int i = 0; i < playerNames.Count; i++)
        {
            string key = playerNames[i];
            string playerName = playerNames[i];
            var stats = i < gameStatsX01s.Count ? gameStatsX01s[i] : null;

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
        // LEGS
        // =====================================================
        var legsTable = GetOrCreateCachedTable(
            CricketLegsKey,
            parent,
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" });

        legsTable.gameObject.SetActive(true);

        var activeLegKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

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
        // THROWS
        // =====================================================
        var throwsTable = GetOrCreateCachedTable(
            CricketThrowsKey,
            parent,
            "Throws",
            new TableCellData[] { "Ø Throws", "Double %", "Triple %" });

        throwsTable.gameObject.SetActive(true);

        var activeThrowKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

            throwsTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats != null ? stats.AverageThrowsPerGame.ToString("0.00") : "0.00",
                $"{(stats?.doublePercentage ?? 0f) * 100f:0.00}%",
                $"{(stats?.triplePercentage ?? 0f) * 100f:0.00}%"
            }, false);

            throwsTable.SetRowActive(key, true);
            activeThrowKeys.Add(key);
        }

        foreach (var k in throwsTable.GetRowKeys())
            if (!activeThrowKeys.Contains(k))
                throwsTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // MPR / MARKS
        // =====================================================
        var mprTable = GetOrCreateCachedTable(
            CricketMprKey,
            parent,
            "Marks",
            new TableCellData[] { "Marks", "Max MPR", "MPR" });

        mprTable.gameObject.SetActive(true);

        var activeMprKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

            mprTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats != null ? stats.marksPerRound.ToString("0.00") : "0.00",
                stats != null ? stats.markCount.ToString() : "0",
                stats != null ? stats.maxMPR.ToString("0.00") : "0.00"
            }, false);

            mprTable.SetRowActive(key, true);
            activeMprKeys.Add(key);
        }

        foreach (var k in mprTable.GetRowKeys())
            if (!activeMprKeys.Contains(k))
                mprTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // SCORES
        // =====================================================
        var scoresTable = GetOrCreateCachedTable(
            CricketScoresKey,
            parent,
            "Points",
            new TableCellData[] { "Ø Points", "Max Points", "Overkill Points" });

        scoresTable.gameObject.SetActive(true);

        var activeScoreKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

            scoresTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats != null ? stats.AverageScorePerTurn.ToString("0.00") : "0.00",
                stats != null ? stats.MaxScoreInTurn.ToString() : "0",
                stats != null ? stats.TotalOverkillPoints.ToString() : "0"
            }, false);

            scoresTable.SetRowActive(key, true);
            activeScoreKeys.Add(key);
        }

        foreach (var k in scoresTable.GetRowKeys())
            if (!activeScoreKeys.Contains(k))
                scoresTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // HIGHSCORES
        // =====================================================
        var highscoresTable = GetOrCreateCachedTable(
            CricketHighscoresKey,
            parent,
            "Highscores",
            new TableCellData[] { "9 Marks", "White Horse", "Same Triple" });

        highscoresTable.gameObject.SetActive(true);

        var activeHighKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

            highscoresTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats != null ? stats.nineMarkTurns.ToString() : "0",
                stats != null ? stats.whiteHorseTurns.ToString() : "0",
                stats != null ? stats.sameTripleTurns.ToString() : "0"
            }, false);

            highscoresTable.SetRowActive(key, true);
            activeHighKeys.Add(key);
        }

        foreach (var k in highscoresTable.GetRowKeys())
            if (!activeHighKeys.Contains(k))
                highscoresTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // PREFERENCES
        // =====================================================
        var preferencesTable = GetOrCreateCachedTable(
            CricketPreferencesKey,
            parent,
            "Preferences",
            new TableCellData[] { "First Closed", "Last Closed", "Most Points" });

        preferencesTable.gameObject.SetActive(true);

        var activePrefKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

            preferencesTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats == null ? "-" : (stats.firstClosedField == -1 ? "-" : stats.firstClosedField.ToString()),
                stats == null ? "-" : (stats.lastClosedField == -1 ? "-" : stats.lastClosedField.ToString()),
                stats == null ? "-" : (stats.maxPointsField == -1 ? "-" : stats.maxPointsField.ToString())
            }, false);

            preferencesTable.SetRowActive(key, true);
            activePrefKeys.Add(key);
        }

        foreach (var k in preferencesTable.GetRowKeys())
            if (!activePrefKeys.Contains(k))
                preferencesTable.SetRowActive(k, false);

        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
        StatisticsPageBuildHelper.FinalizePage(parent, pageLayout, originalParentState);

        yield return null;
    }
}