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


    private readonly Dictionary<string, TableView> cachedTables = new Dictionary<string, TableView>();
    private readonly Dictionary<string, HitSectorChart> cachedCharts = new Dictionary<string, HitSectorChart>();
    private GameObject hitsHeadline;

    private static readonly PenaltyType[] penaltyColumns =
    {
        PenaltyType.Wall,
        PenaltyType.Ceiling,
        PenaltyType.AllMiss,
        PenaltyType.ThreeOnes,
        PenaltyType.Schnapszahl,
        PenaltyType.LostGame
    };


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

    private GameObject CreateHeadline(Transform parentTransform, string text)
    {
        if (prefabHeadline == null || parentTransform == null) return null;
        var go = Instantiate(prefabHeadline, parentTransform);
        var tmp = go.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = text;
        return go;
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

    private TableCellData GetPenaltyHeaderData(PenaltyType type)
    {
        Sprite icon = null;
        switch (type)
        {
            case PenaltyType.Wall:        icon = iconWall; break;
            case PenaltyType.Ceiling:     icon = iconCeiling; break;
            case PenaltyType.AllMiss:     icon = iconMiss; break;
            case PenaltyType.ThreeOnes:   icon = iconOnes; break;
            case PenaltyType.Schnapszahl: icon = iconSchnaps; break;
            case PenaltyType.LostGame:    icon = iconLost; break;
        }

        return new TableCellData 
        { 
            text = type.ToString(), 
            icon = icon, 
            isIcon = icon != null // Wenn kein Icon da ist, nimm Text
        };
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

            int participated = stats?.gameCount == 0 ? 0 : stats.gameCount;
            int won = stats?.gamesWon == 0? 0 : stats.gamesWon;

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

        // =====================================================
        // PENALTIES
        // =====================================================
        var penaltyHeader = penaltyColumns
            .Select(type => GetPenaltyHeaderData(type))
            .Concat(new TableCellData[] { "Sum" })
            .ToArray();

        var penaltyTable = GetOrCreateCachedTable(
            X01PenaltiesKey,
            parent,
            "Penalties",
            penaltyHeader);

        var activePenaltyKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            var stats = gameStatsX01s[i];
            string key = playerNames[i];

            penaltyTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                stats?.wallCount.ToString() ?? "0",
                stats?.ceilingCount.ToString() ?? "0",
                stats?.allMissCount.ToString() ?? "0",
                stats?.tripleOnesCount.ToString() ?? "0",
                stats?.tripleDigitCount.ToString() ?? "0",
                stats?.lostGame.ToString() ?? "0",
                stats?.totalPenaltyCost.ToString("0") ?? "0"
            }, false);

            penaltyTable.SetRowActive(key, true);
            activePenaltyKeys.Add(key);
        }

        foreach (var existingKey in penaltyTable.GetRowKeys())
            if (!activePenaltyKeys.Contains(existingKey))
                penaltyTable.SetRowActive(existingKey, false);

        penaltyTable.gameObject.SetActive(true);

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
                $"{stats.first9Average : 0.00}",
                stats?.bestTurnPoints.ToString() ?? "0"
            }, false);

            scoringTable.SetRowActive(key, true);
            activeScoreKeys.Add(key);
        }

        foreach (var k in scoringTable.GetRowKeys())
            if (!activeScoreKeys.Contains(k))
                scoringTable.SetRowActive(k, false);

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