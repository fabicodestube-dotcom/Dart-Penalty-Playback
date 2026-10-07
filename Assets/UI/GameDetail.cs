using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

public class GameDetail : UIScreen, IUIScreen
{
    [Header("Control")]
    public AppHandler appHandler;
    public WindowHandler windowHandler;
    public X01GameEngine x01GameEngine;
    public ATCGameEngine atcGameEngine;
    public CricketGameEngine cricketGameEngine;
    public Transform contentParent;

    [Header("Popup")]
    public UIScreen popup;

    [Header("Scroll View")]
    public ScrollRect scrollRect;

    [Header("Tables")]
    public HistoryItemHeadline historyItemHeadline;
    public GameObject prefabTable;
    public GameObject prefabHeadline;
    public GameObject prefabHitSectorChart;

    public Sprite iconWall;
    public Sprite iconCeiling;
    public Sprite iconAllMiss;
    public Sprite iconTripleOnes;
    public Sprite iconTripleDigit;
    public Sprite iconLostGame;

    [Header("Actions")]
    public GameObject buttonContinue;
    public GameObject buttonDelete;

    private GameSummary gameSummary;
    private Dictionary<Guid, GameStats> playerStats;
    private readonly Dictionary<string, TableView> cachedTables = new Dictionary<string, TableView>();
    private readonly List<HitSectorChart> x01HitSectorCharts = new List<HitSectorChart>();
    private GameObject x01HitSectorHeadline;

    private void Awake()
    {
        InitializeCachedTables();
    }

    public void OnShow()
    {
        var selectedGame = appHandler.GetSelectedGame();
        if (selectedGame != null)
        {
            gameSummary = appHandler.GetGameSummary(selectedGame.GetID());
        }
        else
        {
            gameSummary = appHandler.GetGameSummary(appHandler.GetLastClickedGameId());
        }

        if (gameSummary == null)
        {
            Debug.LogError("GameDetail: no game summary found");
            return;
        }

        playerStats = LoadPlayerStatsForGame(gameSummary.Id);

        if (buttonDelete != null)
            buttonDelete.SetActive(true);

        if (buttonContinue != null)
            buttonContinue.SetActive(gameSummary != null && !gameSummary.IsFinished);

        if (historyItemHeadline != null)
            historyItemHeadline.Initialize(gameSummary);

        HideAllCachedSections();

        if (gameSummary.GameMode == GameMode.X01)
            PopulateX01Details();
        else if (gameSummary.GameMode == GameMode.Cricket)
            PopulateCricketDetails();
        else if (gameSummary.GameMode == GameMode.ATC)
            PopulateATCDetails();

        if (buttonContinue != null)
            buttonContinue.transform.SetAsLastSibling();
        if (buttonDelete != null)
            buttonDelete.transform.SetAsLastSibling();
    }

    private Guid GetLastClickedGameId()
    {
        return Guid.Empty;
    }

    private Dictionary<Guid, GameStats> LoadPlayerStatsForGame(Guid gameId)
    {
        var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();
        if (allStats.TryGetValue(gameId, out var stats))
            return stats;
        return new Dictionary<Guid, GameStats>();
    }

    private void ClearContent()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            var child = contentParent.GetChild(i).gameObject;

            if (historyItemHeadline != null && child == historyItemHeadline.gameObject)
                continue;

            if (buttonContinue != null && child == buttonContinue) continue;
            if (buttonDelete != null && child == buttonDelete) continue;

            Destroy(child);
        }
    }

    private void InitializeCachedTables()
    {
        if (contentParent == null || prefabTable == null)
        {
            Debug.LogError("GameDetail: contentParent or prefabTable is not assigned.");
            return;
        }

        ClearContent();

        CreateCachedTable("X01_PlayerSummary", "Player", new TableCellData[] { "Sets", "Legs", "Score" });

        CreateCachedTable("X01_Penalties", "Strafen", new TableCellData[]
        {
            new TableCellData { text = "Wall", icon = iconWall, isIcon = true },
            new TableCellData { text = "Decke", icon = iconCeiling, isIcon = true },
            new TableCellData { text = "AllMiss", icon = iconAllMiss, isIcon = true },
            new TableCellData { text = "3x1", icon = iconTripleOnes, isIcon = true },
            new TableCellData { text = "3xX", icon = iconTripleDigit, isIcon = true },
            new TableCellData { text = "lost", icon = iconLostGame, isIcon = true },
            "Summe"
        });

        CreateCachedTable("X01_Legs", "Legs",
            new TableCellData[] { "Played", "Won", "Win %" });

        CreateCachedTable("X01_Throws", "Throws",
            new TableCellData[] { "Throws", "Double %", "Triple %" });

        CreateCachedTable("X01_Scoring", "Scoring",
            new TableCellData[] { "ø Points", "ø First-9", "Max Score" });

        CreateCachedTable("X01_HighScores", "Highscores",
            new TableCellData[] { "60+", "100+", "140+", "180" });

        CreateCachedTable("X01_Checkouts", "Checkouts",
            new TableCellData[] { "Max Checkout", "Attempts", "Favorite" });

        CreateCachedTable("Cricket_PlayerSummary", "Players", new TableCellData[] { "Sets", "Legs", "Score" });
        CreateCachedTable("Cricket_Penalties", "Penalties", new TableCellData[]
        {
            new TableCellData { text = "Wall", icon = iconWall, isIcon = true },
            new TableCellData { text = "Decke", icon = iconCeiling, isIcon = true },
            new TableCellData { text = "AllMiss", icon = iconAllMiss, isIcon = true },
            new TableCellData { text = "3x1", icon = iconTripleOnes, isIcon = true },
            new TableCellData { text = "3xX", icon = iconTripleDigit, isIcon = true },
            new TableCellData { text = "lost", icon = iconLostGame, isIcon = true },
            "Summe"
        });
        CreateCachedTable("Cricket_Legs", "Legs", new TableCellData[] { "Played", "Won", "Win %" });
        CreateCachedTable("Cricket_Throws", "Throws", new TableCellData[] { "Throws", "Double %", "Triple %" });
        CreateCachedTable("Cricket_Scores", "Points", new TableCellData[] { "Ø Points/Turn", "Max Points", "Overkill Points" });
        CreateCachedTable("Cricket_MPR", "Marks per Round", new TableCellData[] { "MPR", "Marks", "Max MPR" });
        CreateCachedTable("Cricket_Highscores", "Highscores", new TableCellData[] { "9 Marks", "White Horse", "Same Triple" });
        CreateCachedTable("Cricket_Preferences", "Preferences", new TableCellData[] { "First closed", "Last closed", "Most Points" });

        CreateCachedTable("ATC_PlayerSummary", "Player", new TableCellData[] { "Sets", "Legs", "Targets" });
        CreateCachedTable("ATC_Penalties", "Strafen", new TableCellData[]
        {
            new TableCellData { text = "Wall", icon = iconWall, isIcon = true },
            new TableCellData { text = "Decke", icon = iconCeiling, isIcon = true },
            new TableCellData { text = "AllMiss", icon = iconAllMiss, isIcon = true },
            new TableCellData { text = "3x1", icon = iconTripleOnes, isIcon = true },
            new TableCellData { text = "3xX", icon = iconTripleDigit, isIcon = true },
            new TableCellData { text = "lost", icon = iconLostGame, isIcon = true },
            "Summe"
        });
        CreateCachedTable("ATC_Legs", "Legs", new TableCellData[] { "Played", "Won", "Win %" });
        CreateCachedTable("ATC_Scoring", "Scoring", new TableCellData[] { "Throws", "Hits", "Hit %" });
        CreateCachedTable("ATC_Streaks", "Streaks & Chokes", new TableCellData[] { "First Dart Hit %", "Longest Streak", "Biggest Choke" });

        HideAllCachedSections();
    }

    private void CreateCachedTable(string key, string title, TableCellData[] header)
    {
        var table = Instantiate(prefabTable, contentParent).GetComponent<TableView>();
        if (table == null)
        {
            Debug.LogError("GameDetail: prefabTable does not contain a TableView component.");
            return;
        }

        table.Build(title, header);
        table.ClearRows();
        table.gameObject.SetActive(false);
        cachedTables[key] = table;
    }

    private void PopulateX01Details()
    {
        var playerIDs = gameSummary.PlayerIds;
        var statsDict = playerStats;

        var playerTable = GetCachedTable("X01_PlayerSummary");
        playerTable.gameObject.SetActive(true);

        var activeX01PlayerKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsX01 : null;
            string key = pid.ToString();

            playerTable.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats?.totalSetsWon.ToString() ?? "0",
                stats?.currentLegCount.ToString() ?? "0",
                GetScoreForPlayer(pid).ToString()
            }, false);

            playerTable.SetRowActive(key, true);
            activeX01PlayerKeys.Add(key);
        }

        foreach (var existingKey in playerTable.GetRowKeys())
        {
            if (!activeX01PlayerKeys.Contains(existingKey))
                playerTable.SetRowActive(existingKey, false);
        }

        playerTable.RefreshLayout();

        var table = GetCachedTable("X01_Penalties");

        table.gameObject.SetActive(true);

        var activeX01PenaltyKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict.TryGetValue(pid, out var s) ? s : null;
            string key = pid.ToString();

            table.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats?.wallCount.ToString() ?? "0",
                stats?.ceilingCount.ToString() ?? "0",
                stats?.allMissCount.ToString() ?? "0",
                stats?.tripleOnesCount.ToString() ?? "0",
                stats?.tripleDigitCount.ToString() ?? "0",
                stats?.lostGame.ToString() ?? "0",
                stats?.GetTotalPenaltyCosts().ToString() ?? "0"
            }, false);

            table.SetRowActive(key, true);
            activeX01PenaltyKeys.Add(key);
        }

        foreach (var existingKey in table.GetRowKeys())
        {
            if (!activeX01PenaltyKeys.Contains(existingKey))
                table.SetRowActive(existingKey, false);
        }

        table.RefreshLayout();

        CreateTable(
            GameMode.X01,
            "X01_Legs",
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" },
            pid =>
            {
                if (!statsDict.TryGetValue(pid, out var s)) return null;
                var stats = s as GameStatsX01;
                if (stats == null) return null;

                return new TableCellData[]
                {
                    stats.totalLegsCount.ToString(),
                    stats.totalLegsWon.ToString(),
                    $"{stats.totalLegWinRate * 100:0.00}%"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.X01,
            "X01_Throws",
            "Throws",
            new TableCellData[] { "Throws", "Double %", "Triple %"},
            pid =>
            {
                if (!statsDict.TryGetValue(pid, out var baseStats))
                    return null;

                var stats = baseStats as GameStatsX01;

                return new TableCellData[]
                {
                    $"{stats?.totalThrowsCount ?? 0:0}",
                    $"{baseStats.doublePercentage * 100:0.00}%",
                    $"{baseStats.triplePercentage * 100:0.00}%"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.X01,
            "X01_Scoring",
            "Scoring",
            new TableCellData[] { "Ø Points", "Ø First-9", "Max Score" },
            pid =>
            {
                if (!statsDict.TryGetValue(pid, out var baseStats))
                    return null;

                var stats = baseStats as GameStatsX01;
                if (stats == null) return null;

                return new TableCellData[]
                {
                    $"{stats.averagePointsPerTurn : 0.00}",
                    $"{stats.first9Average : 0.00}",
                    $"{stats.bestTurnPoints}"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.X01,
            "X01_HighScores",
            "Highscores",
            new TableCellData[] { "60+", "100+", "140+", "180" },
            pid =>
            {
                if (!statsDict.TryGetValue(pid, out var baseStats))
                    return null;

                var stats = baseStats as GameStatsX01;

                return new TableCellData[]
                {
                    $"{stats?.count60Plus ?? 0}",
                    $"{stats?.count100Plus ?? 0}",
                    $"{stats?.count140Plus ?? 0}",
                    $"{stats?.count180 ?? 0}"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.X01,
            "X01_Checkouts",
            "Checkouts",
            new TableCellData[] { "Max Checkout", "Attempts", "Favorite" },
            pid =>
            {
                if (!statsDict.TryGetValue(pid, out var baseStats))
                    return null;

                var stats = baseStats as GameStatsX01;
                if (stats == null) return null;

                return new TableCellData[]
                {
                    $"{stats.highestCheckout}",
                    $"{stats.checkoutAttemptCount}",
                    $"{stats.GetMostFrequentClosingSector()}"
                };
            },
            playerIDs
        );

        if (x01HitSectorHeadline == null)
        {
            x01HitSectorHeadline = Instantiate(prefabHeadline, contentParent);
            var headlineText = x01HitSectorHeadline.GetComponentInChildren<TMP_Text>();
            if (headlineText != null)
            {
                headlineText.text = "Hit Sectors";
            }
        }
        x01HitSectorHeadline.SetActive(true);

        if (prefabHitSectorChart != null)
        {
            var labelOrder = BuildHitSectorLabelOrder();

            EnsureX01HitSectorCharts(playerIDs.Count, labelOrder);

            for (int i = 0; i < playerIDs.Count; i++)
            {
                var pid = playerIDs[i];
                var chart = x01HitSectorCharts[i];
                chart.gameObject.SetActive(true);

                GameStatsX01 stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsX01 : null;
                string playerName = appHandler.GetPlayerNameByID(pid);

                chart.SetData(playerName, stats?.GetHitSector);
            }
        }
    }

    private void PopulateCricketDetails()
    {
        var playerIDs = gameSummary.PlayerIds;
        var statsDict = playerStats;

        var playerTable = GetCachedTable("Cricket_PlayerSummary");
        playerTable.gameObject.SetActive(true);

        var activePlayerKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsCricket : null;
            string key = pid.ToString();

            playerTable.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats?.totalSetsWon.ToString() ?? "0",
                stats?.currentLegCount.ToString() ?? "0",
                GetScoreForPlayer(pid).ToString()
            }, false);

            playerTable.SetRowActive(key, true);
            activePlayerKeys.Add(key);
        }

        foreach (var existingKey in playerTable.GetRowKeys())
        {
            if (!activePlayerKeys.Contains(existingKey))
                playerTable.SetRowActive(existingKey, false);
        }

        playerTable.RefreshLayout();

        var table = GetCachedTable("Cricket_Penalties");

        table.gameObject.SetActive(true);

        var activePenaltyKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict.TryGetValue(pid, out var s) ? s : null;
            string key = pid.ToString();

            table.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats?.wallCount.ToString() ?? "0",
                stats?.ceilingCount.ToString() ?? "0",
                stats?.allMissCount.ToString() ?? "0",
                stats?.tripleOnesCount.ToString() ?? "0",
                stats?.tripleDigitCount.ToString() ?? "0",
                stats?.lostGame.ToString() ?? "0",
                stats?.GetTotalPenaltyCosts().ToString() ?? "0"
            }, false);

            table.SetRowActive(key, true);
            activePenaltyKeys.Add(key);
        }

        foreach (var existingKey in table.GetRowKeys())
        {
            if (!activePenaltyKeys.Contains(existingKey))
                table.SetRowActive(existingKey, false);
        }

        table.RefreshLayout();

        CreateTable(
            GameMode.Cricket,
            "Cricket_Legs",
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s : null;
                if (stats == null) return null;
                int played = stats.totalLegsCount;

                return new TableCellData[]
                {
                    played.ToString(),
                    stats.totalLegsWon.ToString(),
                    $"{stats.totalLegWinRate * 100:0.00}%"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.Cricket,
            "Cricket_Throws",
            "Throws",
            new TableCellData[] { "Throws", "Double %", "Triple %" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s : null;

                return new TableCellData[]
                {
                    stats?.totalThrowsCount.ToString() ?? "0",
                    $"{stats?.doublePercentage * 100 ?? 0:0.00}%",
                    $"{stats?.triplePercentage * 100 ?? 0:0.00}%"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.Cricket,
            "Cricket_Scores",
            "Punkte",
            new TableCellData[] { "Ø Points/Turn", "Max Points", "Overkill Points" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsCricket : null;
                if (stats == null) return null;

                return new TableCellData[]
                {
                    $"{stats.AverageScorePerTurn:0.00}",
                    stats.MaxScoreInTurn.ToString(),
                    stats.TotalOverkillPoints.ToString()
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.Cricket,
            "Cricket_MPR",
            "Marks per Round",
            new TableCellData[] { "MPR", "Marks", "Max MPR" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsCricket : null;
                if (stats == null) return null;

                return new TableCellData[]
                {
                    $"{stats.marksPerRound:0.00}",
                    stats.markCount.ToString(),
                    $"{stats.maxMPR:0.00}"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.Cricket,
            "Cricket_Highscores",
            "Highscores",
            new TableCellData[] { "9 Marks", "White Horse", "Same Triple" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsCricket : null;
                if (stats == null) return null;

                return new TableCellData[]
                {
                    stats.nineMarkTurns.ToString(),
                    stats.whiteHorseTurns.ToString(),
                    stats.sameTripleTurns.ToString()
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.Cricket,
            "Cricket_Preferences",
            "Präferenzen",
            new TableCellData[] { "First Closed", "Last Closed", "Most Points" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsCricket : null;
                if (stats == null) return null;

                return new TableCellData[]
                {
                    stats.firstClosedField == -1 ? "-" : stats.firstClosedField.ToString(),
                    stats.lastClosedField == -1 ? "-" : stats.lastClosedField.ToString(),
                    stats.maxPointsField == -1 ? "-" : stats.maxPointsField.ToString()
                };
            },
            playerIDs
        );
    }

    private void PopulateATCDetails()
    {
        var playerIDs = gameSummary.PlayerIds;
        var statsDict = playerStats;

        var playerTable = GetCachedTable("ATC_PlayerSummary");
        playerTable.gameObject.SetActive(true);

        var activeATCPlayerKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsATC : null;
            string key = pid.ToString();

            playerTable.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats?.totalSetsWon.ToString() ?? "0",
                stats?.currentLegCount.ToString() ?? "0",
                (stats?.targetsHit ?? 0) + "/" + (stats?.totalTargets ?? 0)
            }, false);

            playerTable.SetRowActive(key, true);
            activeATCPlayerKeys.Add(key);
        }

        foreach (var existingKey in playerTable.GetRowKeys())
        {
            if (!activeATCPlayerKeys.Contains(existingKey))
                playerTable.SetRowActive(existingKey, false);
        }

        playerTable.RefreshLayout();

        var table = GetCachedTable("ATC_Penalties");

        table.gameObject.SetActive(true);

        var activeATCPenaltyKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict.TryGetValue(pid, out var s) ? s : null;
            string key = pid.ToString();

            table.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats?.wallCount.ToString() ?? "0",
                stats?.ceilingCount.ToString() ?? "0",
                stats?.allMissCount.ToString() ?? "0",
                stats?.tripleOnesCount.ToString() ?? "0",
                stats?.tripleDigitCount.ToString() ?? "0",
                stats?.lostGame.ToString() ?? "0",
                stats?.GetTotalPenaltyCosts().ToString() ?? "0"
            }, false);

            table.SetRowActive(key, true);
            activeATCPenaltyKeys.Add(key);
        }

        foreach (var existingKey in table.GetRowKeys())
        {
            if (!activeATCPenaltyKeys.Contains(existingKey))
                table.SetRowActive(existingKey, false);
        }

        table.RefreshLayout();

        CreateTable(
            GameMode.ATC,
            "ATC_Legs",
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s : null;
                if (stats == null) return null;
                int played = stats.totalLegsCount;

                return new TableCellData[]
                {
                    played.ToString(),
                    stats.totalLegsWon.ToString(),
                    $"{stats.totalLegWinRate * 100:0.00}%"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.ATC,
            "ATC_Scoring",
            "Scoring",
            new TableCellData[] { "Throws", "Hits", "Hit %" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsATC : null;
                return new TableCellData[]
                {
                    stats?.totalThrowsCount.ToString() ?? "0",
                    stats?.targetsHit.ToString() ?? "0",
                    $"{stats?.hitPercentage * 100 ?? 0:0.00}%"
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.ATC,
            "ATC_Streaks",
            "Streaks & Chokes",
            new TableCellData[] { "1st Dart Hit %", "Longest Streak", "Biggest Choke" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsATC : null;
                if (stats == null) return new TableCellData[] { "0%", "0", "-" };

                var (target, attempts) = stats.GetChoke();

                string chokeDisplay = target != -1 ? $"{target} ({attempts})" : "-";

                return new TableCellData[]
                {
                    $"{stats.firstDartHitPercentage * 100f:0.00}%",
                    stats.longestHitStreak.ToString(),
                    chokeDisplay
                };
            },
            playerIDs
        );

        CreateTable(
            GameMode.ATC,
            "ATC_Highscores",
            "Highscores",
            new TableCellData[] { "3 Hits +", "6 Hits +", "9 Hits +", "12 Hits +" },
            pid =>
            {
                var stats = statsDict.TryGetValue(pid, out var s) ? s as GameStatsATC : null;
                if (stats == null) return new TableCellData[] { "0", "0", "0", "0" };

                return new TableCellData[]
                {
                    $"{stats.streak3Plus.ToString():0}",
                    $"{stats.streak6Plus.ToString():0}",
                    $"{stats.streak9Plus.ToString():0}",
                    $"{stats.streak12Plus.ToString():0}"
                };
            },
            playerIDs
        );
    }

    private int GetScoreForPlayer(Guid pid)
    {
        var entry = gameSummary.PlayerEntries.FirstOrDefault(e => e.PlayerId == pid);
        return entry?.Score ?? 0;
    }

    private void EnsureX01HitSectorCharts(int requestedCount, List<string> labelOrder)
    {
        while (x01HitSectorCharts.Count < requestedCount)
        {
            var chart = Instantiate(prefabHitSectorChart, contentParent).GetComponent<HitSectorChart>();
            if (chart == null)
            {
                Debug.LogError("prefabHitSectorChart has no HitSectorChart component");
                return;
            }

            chart.gameObject.SetActive(false);
            x01HitSectorCharts.Add(chart);
        }

        for (int i = 0; i < x01HitSectorCharts.Count; i++)
        {
            x01HitSectorCharts[i].gameObject.SetActive(i < requestedCount);
        }
    }

    private List<string> BuildHitSectorLabelOrder()
    {
        var list = new List<string> { "0" };

        for (int i = 1; i <= 20; i++)
            list.Add(i.ToString());
        list.Add("25");

        for (int i = 1; i <= 20; i++)
            list.Add("D" + i);
        list.Add("D25");

        for (int i = 1; i <= 20; i++)
            list.Add("T" + i);
        return list;
    }

    private void CreateTable(
        GameMode mode,
        string cacheKey,
        string title,
        TableCellData[] header,
        System.Func<Guid, TableCellData[]> rowBuilder,
        List<Guid> playerIDs)
    {
        var table = GetCachedTable(cacheKey);
        if (table == null)
        {
            table = Instantiate(prefabTable, contentParent).GetComponent<TableView>();
            table.Build(title, header);
            cachedTables[cacheKey] = table;
        }

        table.gameObject.SetActive(true);

        var activeKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            string key = pid.ToString();
            table.AddOrUpdateRow(key, GetPlayerName(pid), rowBuilder(pid), false);
            table.SetRowActive(key, true);
            activeKeys.Add(key);
        }

        foreach (var existingKey in table.GetRowKeys())
        {
            if (!activeKeys.Contains(existingKey))
                table.SetRowActive(existingKey, false);
        }

        table.RefreshLayout();
    }

    private TableView GetCachedTable(string key)
    {
        return cachedTables.TryGetValue(key, out var result) ? result : null;
    }

    private void HideAllCachedSections()
    {
        foreach (var pair in cachedTables)
        {
            if (pair.Value != null)
                pair.Value.gameObject.SetActive(false);
        }

        if (x01HitSectorHeadline != null)
            x01HitSectorHeadline.SetActive(false);

        foreach (var chart in x01HitSectorCharts)
        {
            if (chart != null)
                chart.gameObject.SetActive(false);
        }
    }

    private string GetPlayerName(Guid pid)
    {
        var name = appHandler != null ? appHandler.GetPlayerNameByID(pid) : null;
        return string.IsNullOrWhiteSpace(name) ? $"Player {pid}" : name;
    }

    public void OnClickDeleteGame()
    {
        windowHandler.ShowPopup(popup);
    }

    public void ConfirmDeleteGame()
    {
        if (gameSummary == null)
        {
            Debug.LogError("GameDetail: no game summary for delete");
            return;
        }

        appHandler.DeleteGame(gameSummary.Id);

        windowHandler.HidePopup();
        windowHandler.GoBack();
    }

    public void CancleDeleteGame()
    {
        windowHandler.HidePopup();
    }

    public void OnClickContinueGame()
    {
        if (gameSummary == null)
        {
            Debug.LogError("GameDetail: no game summary for continue");
            return;
        }

        if (gameSummary.IsFinished)
        {
            Debug.LogWarning("GameDetail: game is finished, cannot continue");
            return;
        }

        var game = appHandler.LoadGameById(gameSummary.Id);
        if (game == null)
        {
            Debug.LogError("GameDetail: failed to load game for continue");
            return;
        }

        switch (gameSummary.GameMode)
        {
            case GameMode.X01:
                if (x01GameEngine == null || windowHandler == null)
                {
                    Debug.LogError("GameDetail: x01GameEngine/windowHandler not wired");
                    return;
                }
                windowHandler.GoTo(ScreenId.X01Game);
                x01GameEngine.LoadGame((X01Game)game);
                break;

            case GameMode.ATC:
                if (atcGameEngine == null || windowHandler == null)
                {
                    Debug.LogError("GameDetail: atcGameEngine/windowHandler not wired");
                    return;
                }
                windowHandler.GoTo(ScreenId.ATCGame);
                atcGameEngine.LoadGame((ATCGame)game);
                break;

            case GameMode.Cricket:
                if (cricketGameEngine == null || windowHandler == null)
                {
                    Debug.LogError("GameDetail: cricketGameEngine/windowHandler not wired");
                    return;
                }
                windowHandler.GoTo(ScreenId.CricketGame);
                cricketGameEngine.LoadGame((CricketGame)game);
                break;
        }
    }

    public void OnHide()
    {
        ResetScroll();
    }

    private void ResetScroll()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>();

        if (scrollRect == null)
        {
            Debug.LogError("GameDetail: No ScrollRect found for resetting scroll position");
            return;
        }

        scrollRect.verticalNormalizedPosition = 1f;
    }
}
