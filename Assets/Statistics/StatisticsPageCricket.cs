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


    private static readonly PenaltyType[] penaltyColumns =
    {
        PenaltyType.Wall,
        PenaltyType.Ceiling,
        PenaltyType.AllMiss,
        PenaltyType.ThreeOnes,
        PenaltyType.Schnapszahl,
        PenaltyType.LostGame
    };

    private static readonly string CricketPenaltiesKey = "Cricket_Penalties";
    private static readonly string CricketParticipationKey = "Cricket_Participation";
    private static readonly string CricketLegsKey = "Cricket_Legs";
    private static readonly string CricketThrowsKey = "Cricket_Throws";
    private static readonly string CricketScoresKey = "Cricket_Scores";
    private static readonly string CricketMprKey = "Cricket_MPR";
    private static readonly string CricketHighscoresKey = "Cricket_Highscores";
    private static readonly string CricketPreferencesKey = "Cricket_Preferences";

    public IEnumerator ShowStatsAsync(List<GameStatsCricket> stats, List<string> playerNames)
    {
        yield return BuildCricketStatsCoroutine(stats, playerNames);
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

        // =====================================================
        // PENALTIES
        // =====================================================
        var penaltyHeader = penaltyColumns
            .Select(type => GetPenaltyHeaderData(type))
            .Concat(new TableCellData[] { "Sum" })
            .ToArray();

        var penaltyTable = GetOrCreateCachedTable(
            CricketPenaltiesKey,
            parent,
            "Penalties",
            penaltyHeader);

        penaltyTable.gameObject.SetActive(true);

        var activePenaltyKeys = new HashSet<string>();

        for (int i = 0; i < gameStatsX01s.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStatsX01s[i];

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