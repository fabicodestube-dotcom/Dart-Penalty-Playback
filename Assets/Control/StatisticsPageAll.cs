using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatisticsPageAll : MonoBehaviour
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

    private static readonly string AllGamesPenaltiesKey = "AllGames_Penalties";
    private static readonly string AllGamesParticipationKey = "AllGames_Participation";
    private static readonly string AllGamesLegsKey = "AllGames_Legs";


    public IEnumerator ShowStatsAsync(List<GameStats> stats, List<string> playerNames)
    {
        yield return BuildAllGamesStatsCoroutineRoutine(stats, playerNames);
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


    public IEnumerator BuildAllGamesStatsCoroutineRoutine(List<GameStats> gameStats, List<string> playerNames)
    {
        bool originalParentState = parent.gameObject.activeSelf;
        parent.gameObject.SetActive(false);
        var pageLayout = StatisticsPageBuildHelper.SuspendPageLayout(parent);

        ClearPageChildrenExceptCachedTables(parent);

        // =====================================================
        // PARTICIPATION
        // =====================================================
        var participationTable = GetOrCreateCachedTable(
            AllGamesParticipationKey,
            parent,
            "Games",
            new TableCellData[] { "Played", "Won", "Win %" });

        participationTable.gameObject.SetActive(true);
        var activeParticipationKeys = new HashSet<string>();

        for (int i = 0; i < playerNames.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStats[i];

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
            AllGamesPenaltiesKey,
            parent,
            "Penalties",
            penaltyHeader);

        penaltyTable.gameObject.SetActive(true);
        var activePenaltyKeys = new HashSet<string>();

        for (int i = 0; i < playerNames.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStats[i];

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
            AllGamesLegsKey,
            parent,
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" });

        legsTable.gameObject.SetActive(true);
        var activeLegKeys = new HashSet<string>();

        for (int i = 0; i < playerNames.Count; i++)
        {
            string key = playerNames[i];
            var stats = gameStats[i];

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

        StatisticsPageBuildHelper.ApplyTablesOnParent(parent, cachedTables.Values);
        StatisticsPageBuildHelper.FinalizePage(parent, pageLayout, originalParentState);

        yield return null;
    }

}