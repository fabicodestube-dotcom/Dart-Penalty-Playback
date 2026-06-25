using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private Game game;
    private readonly Dictionary<string, TableView> cachedTables = new Dictionary<string, TableView>();
    private readonly List<HitSectorChart> x01HitSectorCharts = new List<HitSectorChart>();
    private GameObject x01HitSectorHeadline;

    private void Awake()
    {
        InitializeCachedTables();
    }

    public void OnShow()
    {
        game = appHandler.GetSelectedGame();

        if (game == null)
        {
            Debug.LogError("GameDetail: no selected game");
            return;
        }

        if (buttonDelete != null)
            buttonDelete.SetActive(true);

        if (buttonContinue != null)
            buttonContinue.SetActive(game != null && !game.IsFinished());

        if (historyItemHeadline != null)
            historyItemHeadline.Initialize(game);

        HideAllCachedSections();

        if (game.GetGameMode() == GameMode.X01)
            PopulateX01Details();
        else if (game.GetGameMode() == GameMode.Cricket)
            PopulateCricketDetails();
        else if (game.GetGameMode() == GameMode.ATC)
            PopulateATCDetails();

        // Ensure buttons are at the bottom of the scroll content
        if (buttonContinue != null)
            buttonContinue.transform.SetAsLastSibling();
        if (buttonDelete != null)
            buttonDelete.transform.SetAsLastSibling();
    }

    private void ClearContent()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            var child = contentParent.GetChild(i).gameObject;

            if (historyItemHeadline != null && child == historyItemHeadline.gameObject)
                continue;
            
            // Keep action buttons (they should remain in scroll content)
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

        // Create X01 tables
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

        // Create Cricket tables
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

        // Create ATC tables
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
        var x01 = game as X01Game;
        if (x01 == null)
        {
            Debug.LogError("Game is not X01");
            return;
        }

        var playerIDs = x01.GetPlayerIDs();
        // Hol dir das Dictionary mit den Basis-Stats
        var statsDict = x01.GetPlayerStats();

        // =====================================================
        // PLAYER SUMMARY
        // =====================================================
        var playerTable = GetCachedTable("X01_PlayerSummary");
        playerTable.gameObject.SetActive(true);

        var activeX01PlayerKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict[pid] as GameStatsX01;
            string key = pid.ToString();

            playerTable.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats.totalSetsWon.ToString(),
                stats.currentLegCount.ToString(),
                x01.GetScore(pid).ToString()
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

        // =====================================================
        // PENALTIES (JETZT AUS STATS!)
        // =====================================================
        var table = GetCachedTable("X01_Penalties");

        TableCellData[] penaltyHeader =
        {
            new TableCellData { text = "Wall", icon = iconWall, isIcon = true },
            new TableCellData { text = "Decke", icon = iconCeiling, isIcon = true },
            new TableCellData { text = "AllMiss", icon = iconAllMiss, isIcon = true },
            new TableCellData { text = "3x1", icon = iconTripleOnes, isIcon = true },
            new TableCellData { text = "3xX", icon = iconTripleDigit, isIcon = true },
            new TableCellData { text = "lost", icon = iconLostGame, isIcon = true },            
            "Summe"
        };

        table.gameObject.SetActive(true);

        var activeX01PenaltyKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict[pid];
            string key = pid.ToString();

            table.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats.wallCount.ToString(),
                stats.ceilingCount.ToString(),
                stats.allMissCount.ToString(),
                stats.tripleOnesCount.ToString(),
                stats.tripleDigitCount.ToString(),
                stats.lostGame.ToString(),
                stats.GetTotalPenaltyCosts().ToString()
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


        // =====================================================
        // TABLE 1: LEGS
        // =====================================================
        CreateTable(
            GameMode.X01,
            "X01_Legs",
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" },
            pid =>
            {
                if (!statsDict.TryGetValue(pid, out var s)) return null;
                int played = s.totalLegsCount;

                return new TableCellData[]
                {
                    played.ToString(),
                    s.totalLegsWon.ToString(),
                    $"{s.totalLegWinRate * 100:0.00}%"
                };
            },
            playerIDs
        );

        // =====================================================
        // TABLE 2: THROWS
        // =====================================================
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

        // =====================================================
        // TABLE 3: SCORING
        // =====================================================
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

                // Use cached favorite double computation from stats (no LINQ overhead)
                return new TableCellData[]
                {
                    $"{stats.averagePointsPerTurn : 0.00}",
                    $"{stats.first9Average : 0.00}",
                    $"{stats.bestTurnPoints}"
                };
            },
            playerIDs
        );

        // =====================================================
        // TABLE 4: HIGH SCORES
        // =====================================================
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

        // =====================================================
        // TABLE 5: CHECKOUTS
        // =====================================================
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

                // Use cached favorite double computation from stats (no LINQ overhead)
                return new TableCellData[]
                {
                    $"{stats.highestCheckout}",
                    $"{stats.checkoutAttemptCount}",
                    $"{stats.GetMostFrequentClosingSector()}"
                };
            },
            playerIDs
        );


        // =====================================================
        // HIT SECTOR HEADLINE
        // =====================================================
        if (x01HitSectorHeadline != null)
            x01HitSectorHeadline.SetActive(true);

        // =====================================================
        // HIT SECTOR CHARTS (bleibt wie bisher, da nicht in Stats)
        // =====================================================
        if (prefabHitSectorChart != null)
        {
            var labelOrder = BuildHitSectorLabelOrder();

            EnsureX01HitSectorCharts(playerIDs.Count, labelOrder);

            for (int i = 0; i < playerIDs.Count; i++)
            {
                var pid = playerIDs[i];
                var chart = x01HitSectorCharts[i];
                chart.gameObject.SetActive(true);

                GameStatsX01 stats = (GameStatsX01)statsDict[pid];
                string playerName = appHandler.GetPlayerNameByID(pid);

                //chart.Build(playerName, labelOrder);
                chart.SetData(playerName, stats.GetHitSector);
            }
        }
    }

    

    private void PopulateCricketDetails()
    {
        var cricket = game as CricketGame;
        if (cricket == null)
        {
            Debug.LogError("Game is not Cricket");
            return;
        }

        var playerIDs = cricket.GetPlayerIDs();
        // Hol dir das Dictionary mit den Basis-Stats
        var statsDict = cricket.GetPlayerStats();

        // =====================================================
        // PLAYER SUMMARY
        // =====================================================
        var playerTable = GetCachedTable("Cricket_PlayerSummary");
        playerTable.gameObject.SetActive(true);

        var activePlayerKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict[pid] as GameStatsCricket;
            string key = pid.ToString();

            playerTable.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats.totalSetsWon.ToString(),
                stats.currentLegCount.ToString(),
                cricket.GetScore(pid).ToString() // 🔥 bleibt im Game
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

        // =====================================================
        // PENALTIES (JETZT AUS STATS!)
        // =====================================================
        var table = GetCachedTable("Cricket_Penalties");

        TableCellData[] penaltyHeader =
        {
            new TableCellData { text = "Wall", icon = iconWall, isIcon = true },
            new TableCellData { text = "Decke", icon = iconCeiling, isIcon = true },
            new TableCellData { text = "AllMiss", icon = iconAllMiss, isIcon = true },
            new TableCellData { text = "3x1", icon = iconTripleOnes, isIcon = true },
            new TableCellData { text = "3xX", icon = iconTripleDigit, isIcon = true },
            new TableCellData { text = "lost", icon = iconLostGame, isIcon = true },            
            "Summe"
        };

        table.gameObject.SetActive(true);

        var activePenaltyKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict[pid];
            string key = pid.ToString();

            table.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats.wallCount.ToString(),
                stats.ceilingCount.ToString(),
                stats.allMissCount.ToString(),
                stats.tripleOnesCount.ToString(),
                stats.tripleDigitCount.ToString(),
                stats.lostGame.ToString(),
                stats.GetTotalPenaltyCosts().ToString()
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

        // =====================================================
        // LEGS
        // =====================================================
        CreateTable(
            GameMode.Cricket,
            "Cricket_Legs",
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" },
            pid =>
            {
                var stats = statsDict[pid];
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

        // =====================================================
        // SCORING
        // =====================================================
        CreateTable(
            GameMode.Cricket,
            "Cricket_Throws",
            "Throws",
            new TableCellData[] { "Throws", "Double %", "Triple %" },
            pid =>
            {
                var stats = statsDict[pid];

                return new TableCellData[]
                {
                    stats.totalThrowsCount.ToString(),
                    $"{stats.doublePercentage * 100:0.00}%",
                    $"{stats.triplePercentage * 100:0.00}%"
                };
            },
            playerIDs
        );

        // =====================================================
        // SCORES
        // =====================================================
        CreateTable(
            GameMode.Cricket,
            "Cricket_Scores",
            "Punkte",
            new TableCellData[] { "Ø Points/Turn", "Max Points", "Overkill Points" },
            pid =>
            {
                var stats = statsDict[pid] as GameStatsCricket;

                return new TableCellData[]
                {
                    $"{stats.AverageScorePerTurn:0.00}",
                    stats.MaxScoreInTurn.ToString(),
                    stats.TotalOverkillPoints.ToString()
                };
            },
            playerIDs
        );

        // =====================================================
        // MARKS PER ROUND
        // =====================================================
        CreateTable(
            GameMode.Cricket,
            "Cricket_MPR",
            "Marks per Round",
            new TableCellData[] { "MPR", "Marks", "Max MPR" },
            pid =>
            {
                var stats = statsDict[pid] as GameStatsCricket;

                return new TableCellData[]
                {
                    $"{stats.marksPerRound:0.00}",
                    stats.markCount.ToString(),
                    $"{stats.maxMPR:0.00}"
                };
            },
            playerIDs
        );

        // =====================================================
        // HIGHSCORES
        // =====================================================
        CreateTable(
            GameMode.Cricket,
            "Cricket_Highscores",
            "Highscores",
            new TableCellData[] { "9 Marks", "White Horse", "Same Triple" },
            pid =>
            {
                var stats = statsDict[pid] as GameStatsCricket;

                return new TableCellData[]
                {
                    stats.nineMarkTurns.ToString(),
                    stats.whiteHorseTurns.ToString(),
                    stats.sameTripleTurns.ToString()
                };
            },
            playerIDs
        );

        // =====================================================
        // PREFERENCES
        // =====================================================
        CreateTable(
            GameMode.Cricket,
            "Cricket_Preferences",
            "Präferenzen",
            new TableCellData[] { "First Closed", "Last Closed", "Most Points" },
            pid =>
            {
                var stats = statsDict[pid] as GameStatsCricket;

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
        var atc = game as ATCGame;
        if (atc == null)
        {
            Debug.LogError("Game is not ATC");
            return;
        }

        var playerIDs = atc.GetPlayerIDs();
        // Hol dir das Dictionary mit den Basis-Stats
        var statsDict = atc.GetPlayerStats();

        // =====================================================
        // TABLE 1: PLAYER SUMMARY
        // =====================================================
        var playerTable = GetCachedTable("ATC_PlayerSummary");
        playerTable.gameObject.SetActive(true);

        var activeATCPlayerKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict[pid] as GameStatsATC;
            string key = pid.ToString();

            playerTable.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats.totalSetsWon.ToString(),
                stats.currentLegCount.ToString(),
                atc.GetTargetsHit(pid) + "/" + atc.GetTotalTargets() // 🔥 bleibt im Game
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

        // =====================================================
        // TABLE 2: PENALTIES (JETZT AUS STATS!)
        // =====================================================
        var table = GetCachedTable("ATC_Penalties");

        TableCellData[] penaltyHeader =
        {
            new TableCellData { text = "Wall", icon = iconWall, isIcon = true },
            new TableCellData { text = "Decke", icon = iconCeiling, isIcon = true },
            new TableCellData { text = "AllMiss", icon = iconAllMiss, isIcon = true },
            new TableCellData { text = "3x1", icon = iconTripleOnes, isIcon = true },
            new TableCellData { text = "3xX", icon = iconTripleDigit, isIcon = true },
            new TableCellData { text = "lost", icon = iconLostGame, isIcon = true },            
            "Summe"
        };

        table.gameObject.SetActive(true);

        var activeATCPenaltyKeys = new HashSet<string>();
        foreach (var pid in playerIDs)
        {
            var stats = statsDict[pid];
            string key = pid.ToString();

            table.AddOrUpdateRow(key, GetPlayerName(pid), new TableCellData[]
            {
                stats.wallCount.ToString(),
                stats.ceilingCount.ToString(),
                stats.allMissCount.ToString(),
                stats.tripleOnesCount.ToString(),
                stats.tripleDigitCount.ToString(),
                stats.lostGame.ToString(),
                stats.GetTotalPenaltyCosts().ToString()
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

        // =====================================================
        // TABLE 3: LEGS
        // =====================================================
        CreateTable(
            GameMode.ATC,
            "ATC_Legs",
            "Legs",
            new TableCellData[] { "Played", "Won", "Win %" },
            pid =>
            {
                var stats = statsDict[pid];
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


        // =====================================================
        // TABLE 4: SCORING (Nutzt Base Stats & ATC Stats)
        // =====================================================
        CreateTable(
            GameMode.ATC,
            "ATC_Scoring",
            "Scoring",
            new TableCellData[] { "Throws", "Hits", "Hit %" },
            pid => 
            {
                var stats = statsDict[pid] as GameStatsATC;
                return new TableCellData[]
                {
                    stats?.totalThrowsCount.ToString() ?? "0", // Aus Basisklasse
                    stats?.targetsHit.ToString() ?? "0",       // Aus ATC-Klasse
                    $"{stats?.hitPercentage * 100 ?? 0:0.00}%"        // Aus ATC-Klasse
                };
            },
            playerIDs
        );

        // =====================================================
        // TABLE 5: STREAKS & CHOKES
        // =====================================================
        CreateTable(
            GameMode.ATC,
            "ATC_Streaks",
            "Streaks & Chokes",
            new TableCellData[] { "1st Dart Hit %", "Longest Streak", "Biggest Choke" },
            pid =>
            {
                var stats = statsDict[pid] as GameStatsATC;
                if (stats == null) return new TableCellData[] { "0%", "0", "-" };

                // Choke-Daten abrufen
                var (target, attempts) = stats.GetChoke();
                
                // Formatierung: "Ziel (Versuche)", z.B. "20 (12)" oder "-" wenn kein Treffer
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

        // =====================================================
        // TABLE 6: HISHCORES
        // =====================================================
        CreateTable(
            GameMode.ATC,
            "ATC_Highscores",
            "Highscores",
            new TableCellData[] { "3 Hits +", "6 Hits +", "9 Hits +", "12 Hits +" },
            pid =>
            {
                var stats = statsDict[pid] as GameStatsATC;
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
        // Order: singles first, then doubles, then triples (no T25). Keep 0 first.
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

    // =========================================================
    // GENERIC TABLE BUILDER
    // =========================================================

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


    // =========================================================
    // BUTTON CALLBACKS
    // =========================================================

    public void OnClickDeleteGame()
    {
        windowHandler.ShowPopup(popup);
    }

    public void ConfirmDeleteGame()
    {
        if (game == null)
        {
            Debug.LogError("GameDetail: no selected game for delete");
            return;
        }

        appHandler.DeleteGame(game.GetID());

        windowHandler.HidePopup();
        windowHandler.GoBack();
    }

    public void CancleDeleteGame()
    {
        windowHandler.HidePopup();
    }

    public void OnClickContinueGame()
    {
        if (game == null)
        {
            Debug.LogError("GameDetail: no selected game for continue");
            return;
        }

        if (game.IsFinished())
        {
            Debug.LogWarning("GameDetail: game is finished, cannot continue");
            return;
        }

        switch (game.GetGameMode())
        {
            case GameMode.X01:
                if (x01GameEngine == null || windowHandler == null)
                {
                    Debug.LogError("GameDetail: x01GameEngine/windowHandler not wired");
                    return;
                }
                windowHandler.GoTo(ScreenId.X01Game);
                x01GameEngine.LoadGame((X01Game) game);
                break;

            case GameMode.ATC:
                if (atcGameEngine == null || windowHandler == null)
                {
                    Debug.LogError("GameDetail: atcGameEngine/windowHandler not wired");
                    return;
                }
                windowHandler.GoTo(ScreenId.ATCGame);
                atcGameEngine.LoadGame((ATCGame) game);
                break;

            case GameMode.Cricket:
                if (cricketGameEngine == null || windowHandler == null)
                {
                    Debug.LogError("GameDetail: cricketGameEngine/windowHandler not wired");
                    return;
                }
                windowHandler.GoTo(ScreenId.CricketGame);
                cricketGameEngine.LoadGame((CricketGame) game);
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

        // wichtig: erst Layout aktualisieren lassen
        //Canvas.ForceUpdateCanvases();

        scrollRect.verticalNormalizedPosition = 1f;
    }
}