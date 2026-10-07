using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class AppHandler : MonoBehaviour
{
    public ThemeManager themeManager;

    public Action<Game> OnAddGame;
    public Action<GameMode, Guid> OnDeleteGame;
    public Action<GameMode> OnDeleteGamesOfMode;
    public Action OnAllGamesDeleted;
    public Action<Guid> OnPlayerAdded;

    private Database database;
    private Game selectedGame;
    private Game activeRuntimeGame;
    private Guid lastClickedGameId;

    private bool isShuttingDown = false;

    private void Awake()
    {
        DisableDebugLogs();
        Debug.Log("Application Folder: " + Application.persistentDataPath);
        LoadDatabase();
        MaximizeFPS();
    }

    private void Start()
    {
        themeManager.ForceApplyAll();
    }

    private void OnEnable()
    {
        isShuttingDown = false;
        #if UNITY_EDITOR
        EditorApplication.wantsToQuit += OnEditorWantsToQuit;
        #endif
    }

    private void OnDisable()
    {
        #if UNITY_EDITOR
        EditorApplication.wantsToQuit -= OnEditorWantsToQuit;
        #endif
    }

    #if UNITY_EDITOR
    private bool OnEditorWantsToQuit()
    {
        isShuttingDown = true;
        return true;
    }
    #endif

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SafeSave();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SafeSave();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
        else if (selectedGame != null && !selectedGame.IsFinished())
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }

    private void OnApplicationQuit()
    {
        SafeSave();
    }

    public List<BasePlayer> GetPlayers()
    {
        return database.GetPlayers();
    }

    public List<BasePlayer> GetActivePlayers()
    {
        return database.GetActivePlayers();
    }

    public List<BasePlayer> GetReservePlayers()
    {
        return database.GetReservePlayers();
    }

    public BasePlayer GetPlayerByID(Guid id)
    {
        return database.GetPlayerByID(id);
    }

    public string GetPlayerNameByID(Guid playerID)
    {
        return database.GetPlayerNameByID(playerID);
    }

    public List<GameSummary> GetGameSummaries()
    {
        return database.GetGameSummaries();
    }

    public GameSummary GetGameSummary(Guid id)
    {
        return database.GetGameSummary(id);
    }

    public Game GetSelectedGame()
    {
        return selectedGame;
    }

    public GameMode GetLastGameMode()
    {
        return database.GetLastGameMode();
    }

    public X01GameSettings GetX01Settings()
    {
        return database.GetX01Settings();
    }

    public CricketGameSettings GetCricketSettings()
    {
        return database.GetCricketSettings();
    }

    public ATCGameSettings GetATCSettings()
    {
        return database.GetATCSettings();
    }

    public SetsAndLegs GetSetsAndLegsMode()
    {
        return database.GetSetsAndLegsMode();
    }

    public int GetSetCount()
    {
        return database.GetSetCount();
    }

    public int GetLegCount()
    {
        return database.GetLegCount();
    }

    public void SaveDatabase()
    {
        SaveSystem.SaveDatabase(database);
    }

    public void SetPlayers(List<Guid> active, List<Guid> reserve)
    {
        database.SetPlayers(active, reserve);
        SaveSystem.SaveDatabase(database);
    }

    public void ApplyPlayerSetupStartOrderFromFlags()
    {
        database.ApplySetupPlayerOrderFromFlags();
        SaveSystem.SaveDatabase(database);
    }

    public void AddPlayer(string playerName)
    {
        Guid newId = database.AddPlayer(playerName);
        SaveSystem.SaveDatabase(database);
        OnPlayerAdded?.Invoke(newId);
    }

    public void AddDartBot(DartBotDifficulty difficulty)
    {
        Guid newId = database.AddDartBot(difficulty);
        SaveSystem.SaveDatabase(database);
        OnPlayerAdded?.Invoke(newId);
    }

    public void RenamePlayer(Guid playerID, string newName)
    {
        database.RenamePlayer(playerID, newName);
        SaveSystem.SaveDatabase(database);
    }

    public void DeletePlayer(Guid playerID)
    {
        database.DeletePlayer(playerID);
        SaveSystem.SaveDatabase(database);
    }

    public void SetPlayerVisibleInStatistics(Guid id, bool isVisible)
    {
        database.SetPlayerVisibleInStatistics(id, isVisible);
    }

    public Game LoadGameById(Guid id)
    {
        return SaveSystem.LoadGameById(id);
    }

    public void SaveGame(Game g)
    {
        if (g == null)
            return;

        SetSelectedGame(g);

        if (g.IsFinished())
        {
            ApplyGameStatsToPlayers(g);
            SaveSystem.SaveGame(g);
            SaveSystem.SaveAggregatedPlayerStats(g);
        }
        else
        {
            g.CalculatePlayerStatsOnSave();
            SaveSystem.SaveGame(g);
        }

        g.MarkSavedNow();

        var summary = CreateGameSummaryFromGame(g);
        database.AddGameSummary(summary);

        OnAddGame?.Invoke(g);
    }

    public void DeleteGame(Guid id)
    {
        GameSummary summary = database.GetGameSummary(id);
        GameMode? mode = database.DeleteGame(id);

        if (mode == null)
        {
            Debug.LogWarning($"DeleteGame: Kein Game mit ID {id} gefunden.");
            return;
        }

        SaveSystem.DeleteGame(id);
        SaveSystem.RebuildAggregatedPlayerStats();

        if (summary != null)
        {
            var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();
            if (allStats.TryGetValue(id, out var playerStats))
            {
                foreach (var playerStat in playerStats)
                {
                    var player = GetPlayerByID(playerStat.Key);
                    player?.RemoveGameStatsFromSummary(summary, playerStat.Value);
                }
            }
        }

        OnDeleteGame?.Invoke(mode.Value, id);
    }

    public void DeleteGamesOfMode(GameMode mode)
    {
        var summaries = database.GetGameSummaries()
            .Where(s => s.GameMode == mode)
            .ToList();

        database.DeleteGames(mode);
        SaveSystem.DeleteGamesByMode(mode);
        SaveSystem.RebuildAggregatedPlayerStats();

        foreach (var summary in summaries)
        {
            var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();
            if (allStats.TryGetValue(summary.Id, out var playerStats))
            {
                foreach (var playerStat in playerStats)
                {
                    var player = GetPlayerByID(playerStat.Key);
                    player?.RemoveGameStatsFromSummary(summary, playerStat.Value);
                }
            }
        }

        foreach (var player in database.GetPlayers())
        {
            if (player != null)
                player.ResetStatsOfMode(mode);
        }

        OnDeleteGamesOfMode?.Invoke(mode);
    }

    public void DeleteAllGames()
    {
        var summaries = database.GetGameSummaries().ToList();

        database.DeleteGames(null);
        SaveSystem.DeleteAllGames();
        SaveSystem.RebuildAggregatedPlayerStats();

        foreach (var summary in summaries)
        {
            var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();
            if (allStats.TryGetValue(summary.Id, out var playerStats))
            {
                foreach (var playerStat in playerStats)
                {
                    var player = GetPlayerByID(playerStat.Key);
                    player?.RemoveGameStatsFromSummary(summary, playerStat.Value);
                }
            }
        }

        foreach (var player in database.GetPlayers())
        {
            if (player != null)
                player.ResetAllStats();
        }

        OnAllGamesDeleted?.Invoke();
    }

    public void SetSelectedGame(Game game)
    {
        selectedGame = game;
        if (game != null)
            activeRuntimeGame = game;
    }

    public void SetLastClickedGameId(Guid id)
    {
        lastClickedGameId = id;
    }

    public Guid GetLastClickedGameId()
    {
        return lastClickedGameId;
    }

    public void SetActiveRuntimeGame(Game game)
    {
        activeRuntimeGame = game;
        if (game != null)
            selectedGame = game;
    }

    public X01Game PrepareX01Game(X01GameSettings data)
    {
        var game = database.PrepareX01Game(data);
        SetActiveRuntimeGame(game);
        return game;
    }

    public CricketGame PrepareCricketGame(CricketGameSettings data)
    {
        var game = database.PrepareCricketGame(data);
        SetActiveRuntimeGame(game);
        return game;
    }

    public ATCGame PrepareATCGame(ATCGameSettings data)
    {
        var game = database.PrepareATCGame(data);
        SetActiveRuntimeGame(game);
        return game;
    }

    public void SetActivePlayerOrder(List<Guid> newOrder)
    {
        database.SetActivePlayerOrder(newOrder);
        SaveSystem.SaveDatabase(database);
    }

    public void SaveSettings()
    {
        SaveSystem.SaveDatabase(database);
    }

    public void SetLastGameMode(GameMode mode)
    {
        database.SetLastGameMode(mode);
        SaveSystem.SaveDatabase(database);
    }

    public void SetX01Settings(X01GameSettings settings)
    {
        database.SetX01Settings(settings);
        SaveSystem.SaveDatabase(database);
    }

    public void SetCricketSettings(CricketGameSettings settings)
    {
        database.SetCricketSettings(settings);
        SaveSystem.SaveDatabase(database);
    }

    public void SetATCSettings(ATCGameSettings settings)
    {
        database.SetATCSettings(settings);
        SaveSystem.SaveDatabase(database);
    }

    public void SetSetupState(GameMode mode, SetsAndLegs setsAndLegs, int setCount, int legCount)
    {
        database.SetSetupState(mode, setsAndLegs, setCount, legCount);
        SaveSystem.SaveDatabase(database);
    }

    private void HandleDomainUnload(object sender, EventArgs e)
    {
        isShuttingDown = true;
    }

    private void SafeSave()
    {
        if (isShuttingDown)
        {
            Debug.Log("<color=orange>Save blockiert: Domain wird entladen.</color>");
            return;
        }

        #if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            return;
        }
        #endif

        if (database == null || database.GetGameSummaries() == null)
        {
            Debug.LogWarning("Save blockiert: Datenbank-Referenz ist bereits null.");
            return;
        }

        PersistOpenRuntimeGames();

        foreach (var summary in database.GetGameSummaries())
        {
            if (summary != null)
            {
            }
        }

        SaveSystem.SaveDatabase(database);
    }

    private void PersistOpenRuntimeGames()
    {
        if (database == null)
            return;

        var runtimeGames = new List<Game>();
        if (selectedGame != null)
            runtimeGames.Add(selectedGame);
        if (activeRuntimeGame != null)
            runtimeGames.Add(activeRuntimeGame);

        var seenGameIds = new HashSet<Guid>();
        foreach (var game in runtimeGames)
        {
            if (game == null || seenGameIds.Contains(game.GetID()))
                continue;

            seenGameIds.Add(game.GetID());
            if (!game.IsFinished())
                game.CalculatePlayerStatsOnSave();

            SaveSystem.SaveGame(game);
        }
    }

    private void LoadDatabase()
    {
        database = SaveSystem.LoadDatabase();
        database.RebuildPlayerStatistics();
    }

    private void ApplyGameStatsToPlayers(Game game)
    {
        if (game == null)
            return;

        foreach (Guid pid in game.GetPlayerIDs())
        {
            BasePlayer player = GetPlayerByID(pid);
            player?.ApplyGameStats(game);
        }
    }

    private GameSummary CreateGameSummaryFromGame(Game game)
    {
        var summary = new GameSummary
        {
            Id = game.GetID(),
            GameMode = game.GetGameMode(),
            CreatedAt = game.GetCreatedAt(),
            LastActivityAt = game.GetLastActivityAt(),
            FinishedAt = game.GetFinishedAt(),
            CurrentPlayerIndex = game.GetPlayerIDs().IndexOf(game.GetCurrentPlayerId()),
            StartingPlayerIndex = 0,
            WinnerPlayerId = game.GetMatchWinnerId(),
            PlayerIds = new List<Guid>(game.GetPlayerIDs()),
            SettingsJson = JsonConvert.SerializeObject(game.GetSettings())
        };

        var stats = game.GetPlayerStats();
        foreach (var pid in game.GetPlayerIDs())
        {
            stats.TryGetValue(pid, out var ps);
            var entry = new PlayerSummaryEntry
            {
                PlayerId = pid,
                Score = GetScoreForPlayer(game, pid),
                SetsWon = game.GetWonSets(pid),
                LegsWon = game.IsFinished() ? 0 : game.GetWonLegs(pid),
                TargetsHit = game is ATCGame atc ? atc.GetTargetsHit(pid) : 0,
                TotalTargets = game is ATCGame atc2 ? atc2.GetTotalTargets() : 0,
                WallCount = ps?.wallCount ?? 0,
                CeilingCount = ps?.ceilingCount ?? 0,
                AllMissCount = ps?.allMissCount ?? 0,
                ThreeOnesCount = ps?.tripleOnesCount ?? 0,
                TripleDigitCount = ps?.tripleDigitCount ?? 0,
                LostGameCount = ps?.lostGame ?? 0
            };
            summary.PlayerEntries.Add(entry);
        }

        return summary;
    }

    private int GetScoreForPlayer(Game game, Guid pid)
    {
        return game switch
        {
            X01Game x01 => x01.GetScore(pid),
            CricketGame cricket => cricket.GetScore(pid),
            ATCGame atc => atc.GetTargetsHit(pid),
            _ => 0
        };
    }

    private void DisableDebugLogs()
    {
    #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.logEnabled = false;
    #endif
    }

    private void MaximizeFPS()
    {
        QualitySettings.vSyncCount = 0;

        double screenRefreshRate = Screen.currentResolution.refreshRateRatio.value;

        int targetFPS = Mathf.CeilToInt((float)screenRefreshRate);

        if (targetFPS <= 0)
        {
            targetFPS = 60;
        }

        Application.targetFrameRate = targetFPS;

        Debug.Log($"[FPS Manager] Target Frame Rate automatisch auf Maximum gesetzt: {targetFPS} FPS");
    }

    internal List<Player> GetLastSetOfPlayers()
    {
        throw new NotImplementedException();
    }
}
