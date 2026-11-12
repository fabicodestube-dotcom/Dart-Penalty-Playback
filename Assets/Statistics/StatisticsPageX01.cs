using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatisticsPageX01 : MonoBehaviour
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
    [Tooltip("Löst Penalty-Stats zum Tap-Zeitpunkt frisch auf (statt Build-Zeit-Closure). Wird vom StatisticsScreen gesetzt.")]
    public Func<string, GameStats> penaltyStatsResolver;

    /// <summary>
    /// Tap handlers with fresh lookup (Stale-Closure-Fix): stats are only now
    /// When tapping brought in BasePlayer / currentRange, Closure only Fallback.
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


    private readonly Dictionary<string, TableView> cachedTables = new Dictionary<string, TableView>();
    private readonly Dictionary<string, HitSectorChart> cachedCharts = new Dictionary<string, HitSectorChart>();
    private GameObject hitsHeadline;


    private static readonly string X01PenaltiesKey = "X01_Penalties";
    private static readonly string X01PlayerSummaryKey = "X01_PlayerSummary";
    private static readonly string X01LegsKey = "X01_Legs";
    private static readonly string X01ThrowsKey = "X01_Throws";
    private static readonly string X01ScoringKey = "X01_Scoring";
    private static readonly string X01HighScoresKey = "X01_HighScores";
    private static readonly string X01CheckoutsKey = "X01_Checkouts";


    public IEnumerator ShowStatsAsync(List<GameStatsX01> stats, List<string> playerNames)
    {
        yield return BuildX01StatsCoroutine(stats, playerNames);
    }

    /// <summary>
    /// Targeted refresh of ONE player line (all tables + chart of this page).
    /// No rebuild, no clear — use cached tables/charts.
    /// </summary>
    public void UpsertPlayerRow(GameStatsX01 stats, string playerName)
    {
        if (stats == null || string.IsNullOrEmpty(playerName) || parent == null)
            return;

        var playerTable = GetOrCreateCachedTable(X01PlayerSummaryKey, parent, "Games", new TableCellData[] { "Played", "Won", "Win %" });
        if (playerTable != null)
        {
            int participated = stats.gameCount;
            int won = stats.gamesWon;
            float pct = participated == 0 ? 0f : (won / (float)participated) * 100f;

            playerTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                participated.ToString(),
                won.ToString(),
                pct.ToString("0.00") + "%"
            });
            playerTable.SetRowActive(playerName, true);
        }

        var penaltyTable = GetOrCreateCachedTable(
            X01PenaltiesKey,
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

        var legsTable = GetOrCreateCachedTable(X01LegsKey, parent, "Legs", new TableCellData[] { "Played", "Won", "Win %" });
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

        var throwsTable = GetOrCreateCachedTable(X01ThrowsKey, parent, "Throws", new TableCellData[] { "Ø Throws", "Double %", "Triple %" });
        if (throwsTable != null)
        {
            throwsTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.totalThrowsCount.ToString("0.00"),
                $"{stats.doublePercentage * 100f:0.00}%",
                $"{stats.triplePercentage * 100f:0.00}%"
            });
            throwsTable.SetRowActive(playerName, true);
        }

        var scoringTable = GetOrCreateCachedTable(X01ScoringKey, parent, "Scoring", new TableCellData[] { "Ø Points", "Ø First-9", "Max Score" });
        if (scoringTable != null)
        {
            scoringTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.averagePointsPerTurn.ToString("0.00"),
                $"{stats.first9Average:0.00}",
                stats.bestTurnPoints.ToString()
            });
            scoringTable.SetRowActive(playerName, true);
        }

        var highScoreTable = GetOrCreateCachedTable(X01HighScoresKey, parent, "Highscores", new TableCellData[] { "60+", "100+", "140+", "180" });
        if (highScoreTable != null)
        {
            highScoreTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.count60Plus.ToString(),
                stats.count100Plus.ToString(),
                stats.count140Plus.ToString(),
                stats.count180.ToString()
            });
            highScoreTable.SetRowActive(playerName, true);
        }

        var checkoutTable = GetOrCreateCachedTable(X01CheckoutsKey, parent, "Checkouts", new TableCellData[] { "Max Checkout", "Attempts", "Favorite" });
        if (checkoutTable != null)
        {
            checkoutTable.AddOrUpdateRow(playerName, playerName, new TableCellData[]
            {
                stats.highestCheckout.ToString(),
                stats.checkoutAttemptCount.ToString("0.00"),
                stats.GetMostFrequentClosingSector() ?? "n/a"
            });
            checkoutTable.SetRowActive(playerName, true);
        }

        if (prefabHitSectorChart != null)
        {
            if (hitsHeadline == null)
                hitsHeadline = CreateHeadline(parent, "Hits In Sector");
            else
                hitsHeadline.SetActive(true);

            var aggregated = new Dictionary<string, int>();
            foreach (var kv in stats.hitSectorCounts)
                aggregated[kv.Key] = kv.Value;

            var chart = GetOrCreateChart(playerName);
            if (chart != null)
            {
                chart.gameObject.SetActive(true);
                chart.SetData(playerName, aggregated);
            }
        }

        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
    }

    /// <summary>
    /// Re-fades all rows + chart of a player on this page.
    /// Don't put anything on — just existing.
    /// Returns true if at least one row/chart REALLY
    /// switched (only then the layout must be changed via RefreshLayout)
    /// are calculated.
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
    /// Recalculates all table heights of this page from the visible lines
    /// (Divider + LayoutElement.preferredHeight).
    /// The visible number of lines has really changed.
    /// even over the layout (inactive = ignored).
    /// </summary>
    public void RefreshLayout()
    {
        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
    }

    /// <summary>
    /// True if any table has less than `desired` reserve rows.
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
    /// Fills up reserve rows of all tables (chunked, one table per frame).
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

    public void RefreshLocalization()
    {
        foreach (var table in cachedTables.Values)
            table?.RefreshLocalization();
        if (hitsHeadline != null)
        {
            var tmp = hitsHeadline.GetComponent<TMP_Text>();
            if (tmp != null)
                tmp.text = LocalizationText.GetByEnglish("Hits In Sector", "Hits In Sector");
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
            // Perf (M1): Headline is never interactive.
            if (tmp.raycastTarget)
                tmp.raycastTarget = false;
        }
        return go;
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

    private void ClearPageChildrenExceptCached(Transform parentTransform)
    {
        if (parentTransform == null)
            return;

        var preserved = new HashSet<GameObject>();

        foreach (var table in cachedTables.Values)
        {
            if (table != null && table.transform.parent == parentTransform)
                preserved.Add(table.gameObject);
        }

        foreach (var chart in cachedCharts.Values)
        {
            if (chart != null && chart.transform.parent == parentTransform)
                preserved.Add(chart.gameObject);
        }

        if (hitsHeadline != null && hitsHeadline.transform.parent == parentTransform)
            preserved.Add(hitsHeadline);

        for (int i = parentTransform.childCount - 1; i >= 0; i--)
        {
            var child = parentTransform.GetChild(i).gameObject;
            if (!preserved.Contains(child))
                Destroy(child);
        }
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


    private IEnumerator BuildX01StatsCoroutine(List<GameStatsX01> gameStatsX01s, List<string> playerNames)
    {
        bool originalParentState = parent.gameObject.activeSelf;
        parent.gameObject.SetActive(false);
        var pageLayout = StatisticsPageBuildHelper.SuspendPageLayout(parent);

        ClearPageChildrenExceptCached(parent);

        // =====================================================
        // PLAYER SUMMARY
        // =====================================================
        var playerTable = GetOrCreateCachedTable(X01PlayerSummaryKey, parent, "Games", new TableCellData[] { "Played", "Won", "Win %" });
        playerTable.gameObject.SetActive(true);

        var activePlayerKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            var stats = gameStatsX01s[i];
            string key = playerNames[i];

            int participated = stats?.gameCount ?? 0;
            int won = stats?.gamesWon ?? 0;

            float pct = participated == 0 ? 0f : (won / (float)participated) * 100f;

            playerTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                participated.ToString() ?? "0",
                won.ToString() ?? "0",
                pct.ToString("0.00") + "%"
            }, false);

            playerTable.SetRowActive(key, true);
            activePlayerKeys.Add(key);
        }

        foreach (var existingKey in playerTable.GetRowKeys())
            if (!activePlayerKeys.Contains(existingKey))
                playerTable.SetRowActive(existingKey, false);

        // Make a frame: N player setups at a time would otherwise
        // create visible Hitch (applies to all table sections).
        yield return null;

        // =====================================================
        // PENALTIES (simple: count | cost | details → popup)
        // =====================================================
        var penaltyTable = GetOrCreateCachedTable(
            X01PenaltiesKey,
            parent,
            "Penalties",
            new TableCellData[] { "Count", "Costs", "Details" });

        penaltyTable.gameObject.SetActive(true);
        var activePenaltyKeys = new HashSet<string>();
        var popup = ColumnPopup;

        for (int i = 0; i < playerNames.Count; i++)
        {
            var stats = i < gameStatsX01s.Count ? gameStatsX01s[i] : null;
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

        foreach (var existingKey in penaltyTable.GetRowKeys())
            if (!activePenaltyKeys.Contains(existingKey))
                penaltyTable.SetRowActive(existingKey, false);

        yield return null;

        // =====================================================
        // LEGS
        // =====================================================
        var legsTable = GetOrCreateCachedTable(X01LegsKey, parent, "Legs", new TableCellData[] { "Played", "Won", "Win %" });
        legsTable.gameObject.SetActive(true);

        var activeLegKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            var stats = gameStatsX01s[i];
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
        // THROWS
        // =====================================================
        var throwsTable = GetOrCreateCachedTable(X01ThrowsKey, parent, "Throws", new TableCellData[] { "Ø Throws", "Double %", "Triple %" });
        throwsTable.gameObject.SetActive(true);

        var activeScoreKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            var stats = gameStatsX01s[i];
            string key = playerNames[i];

            throwsTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats?.totalThrowsCount.ToString("0.00") ?? "0.00",
                $"{(stats?.doublePercentage ?? 0f) * 100f:0.00}%",
                $"{(stats?.triplePercentage ?? 0f) * 100f:0.00}%"
            }, false);

            throwsTable.SetRowActive(key, true);
            activeScoreKeys.Add(key);
        }

        foreach (var k in throwsTable.GetRowKeys())
            if (!activeScoreKeys.Contains(k))
                throwsTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // SCORING
        // =====================================================
        var scoringTable = GetOrCreateCachedTable(X01ScoringKey, parent, "Scoring", new TableCellData[] { "Ø Points", "Ø First-9", "Max Score" });
        scoringTable.gameObject.SetActive(true);

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            var stats = gameStatsX01s[i];
            string key = playerNames[i];

            scoringTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats?.averagePointsPerTurn.ToString("0.00") ?? "0.00",
                $"{stats?.first9Average ?? 0 : 0.00}",
                stats?.bestTurnPoints.ToString() ?? "0"
            }, false);

            scoringTable.SetRowActive(key, true);
            activeScoreKeys.Add(key);
        }

        foreach (var k in scoringTable.GetRowKeys())
            if (!activeScoreKeys.Contains(k))
                scoringTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // HIGH SCORES
        // =====================================================
        var highScoreTable = GetOrCreateCachedTable(X01HighScoresKey, parent, "Highscores", new TableCellData[] { "60+", "100+", "140+", "180" });
        highScoreTable.gameObject.SetActive(true);

        var activeHighKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            var stats = gameStatsX01s[i];
            string key = playerNames[i];

            highScoreTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats?.count60Plus.ToString() ?? "0",
                stats?.count100Plus.ToString() ?? "0",
                stats?.count140Plus.ToString() ?? "0",
                stats?.count180.ToString() ?? "0"
            }, false);

            highScoreTable.SetRowActive(key, true);
            activeHighKeys.Add(key);
        }

        foreach (var k in highScoreTable.GetRowKeys())
            if (!activeHighKeys.Contains(k))
                highScoreTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // CHECKOUTS
        // =====================================================
        var checkoutTable = GetOrCreateCachedTable(X01CheckoutsKey, parent, "Checkouts", new TableCellData[] { "Max Checkout", "Attempts", "Favorite" });
        checkoutTable.gameObject.SetActive(true);

        var activeCheckoutKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            var stats = gameStatsX01s[i];
            string key = playerNames[i];

            checkoutTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats?.highestCheckout.ToString() ?? "0",
                stats?.checkoutAttemptCount.ToString("0.00") ?? "0.00",
                stats?.GetMostFrequentClosingSector() ?? "n/a"
            }, false);

            checkoutTable.SetRowActive(key, true);
            activeCheckoutKeys.Add(key);
        }

        foreach (var k in checkoutTable.GetRowKeys())
            if (!activeCheckoutKeys.Contains(k))
                checkoutTable.SetRowActive(k, false);

        yield return null;

        // =====================================================
        // HIT SECTOR CHARTS
        // =====================================================
        if (prefabHitSectorChart != null)
        {
            if (hitsHeadline == null)
                hitsHeadline = CreateHeadline(parent, "Hits In Sector");
            else
                hitsHeadline.SetActive(true);

            var activeChartKeys = new HashSet<string>();

            for (int i = 0; i < gameStatsX01s.Count; i++)
            {
                var stats = gameStatsX01s[i];
                string key = playerNames[i];
                var aggregated = new Dictionary<string, int>();

                if (stats != null)
                {
                    foreach (var kv in stats.hitSectorCounts)
                        aggregated[kv.Key] = kv.Value;
                }

                var chart = GetOrCreateChart(key);
                if (chart == null)
                    continue;

                chart.gameObject.SetActive(true);
                chart.SetData(key, aggregated);
                activeChartKeys.Add(key);

                // Perf (M6): hand off chart instantiate + 63 bar setup per player.
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