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

    [Header("Icons")]
    public Sprite iconWall;
    public Sprite iconCeiling;
    public Sprite iconMiss;
    public Sprite iconOnes;
    public Sprite iconSchnaps;
    public Sprite iconLost;


    private readonly Dictionary<string, TableView> cachedTables = new Dictionary<string, TableView>();


    private static readonly PenaltyType[] penaltyColumns =
    {
        PenaltyType.Wall,
        PenaltyType.Ceiling,
        PenaltyType.AllMiss,
        PenaltyType.ThreeOnes,
        PenaltyType.Schnapszahl,
        PenaltyType.LostGame
    };

    private static readonly string ATCPenaltiesKey = "ATC_Penalties";
    private static readonly string ATCParticipationKey = "ATC_Participation";
    private static readonly string ATCLegsKey = "ATC_Legs";
    private static readonly string ATCScoringKey = "ATC_Scoring";
    private static readonly string ATCStreaksKey = "ATC_Streaks";
    private static readonly string ATCHighscoresKey = "ATC_Highscores";

    public IEnumerator ShowStatsAsync(List<GameStatsATC> stats, List<string> playerNames)
    {
        yield return BuildATCStatsCoroutine(stats, playerNames);
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

        // =====================================================
        // 2. PENALTIES
        // =====================================================
        var penaltyHeader = penaltyColumns
            .Select(type => GetPenaltyHeaderData(type))
            .Concat(new TableCellData[] { "Sum" })
            .ToArray();

        var penaltyTable = GetOrCreateCachedTable(
            ATCPenaltiesKey,
            parent,
            "Penalties",
            penaltyHeader);

        penaltyTable.gameObject.SetActive(true);

        var activePenaltyKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsATCs.Count; i++)
        {
            var stats = gameStatsATCs[i];
            string key = playerNames[i];

            penaltyTable.AddOrUpdateRow(key, key, new TableCellData[]
            {
                (stats?.wallCount ?? 0).ToString(),
                (stats?.ceilingCount ?? 0).ToString(),
                (stats?.allMissCount ?? 0).ToString(),
                (stats?.tripleOnesCount ?? 0).ToString(),
                (stats?.tripleDigitCount ?? 0).ToString(),
                (stats?.lostGame ?? 0).ToString(),
                (stats?.totalPenaltyCost ?? 0f).ToString("0")
            }, false);

            penaltyTable.SetRowActive(key, true);
            activePenaltyKeys.Add(key);
        }

        foreach (var k in penaltyTable.GetRowKeys())
            if (!activePenaltyKeys.Contains(k))
                penaltyTable.SetRowActive(k, false);

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

        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
        StatisticsPageBuildHelper.FinalizePage(parent, pageLayout, originalParentState);

        yield return null;
    }
}