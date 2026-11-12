using System;
using System.Collections;
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
    /// <summary>
    /// After last player/setup change, wait until the DB
    /// Bundles click series (setup stepper etc.) into a write.
    /// </summary>
    private const float DatabaseIdleSaveDelaySeconds = 2f;

    public ThemeManager themeManager;

    public Action<Game> OnAddGame;
    /// <summary>
    /// Parameters: mode, game ID, affected player IDs (read from the summary BEFORE the
    /// delete). The in-memory aggregates (BasePlayer) + SQLite aggregates
    /// are already updated when firing — listeners read them directly and
    /// avoid any reloading.
    /// </summary>
    public Action<GameMode, Guid, List<Guid>> OnDeleteGame;
    /// <summary>
    /// Parameters: mode, affected player ids (distinct from the deleted
    /// Aggregates are already updated when firing.
    /// </summary>
    public Action<GameMode, List<Guid>> OnDeleteGamesOfMode;
    public Action OnAllGamesDeleted;
    public Action<Guid> OnPlayerAdded;
    public Action<Guid, string> OnPlayerDeleted;
    public Action<Guid, string, string> OnPlayerRenamed;

    private Database database;
    private Game selectedGame;
    private Game activeRuntimeGame;
    private Guid lastClickedGameId;

    private bool isShuttingDown = false;

    /// <summary>
    /// True if player/setup changes are not yet in SQLite.
    /// All small setters only mark dirty; writes are batched
    /// (idle debounce, game start, lifecycle save).
    /// </summary>
    private bool databaseDirty = false;
    private Coroutine dirtySaveRoutine = null;

    /// <summary>
    /// Collapse break/focus/quit double events into a save.
    /// fire OnApplicationPause (true) + OnApplicationFocus (false) when
    /// Put away almost always directly one after the other (if necessary + quit afterwards).
    /// </summary>
    private const float LifecycleSaveCooldownSeconds = 2f;
    private float lastLifecycleSaveTime = -1000f;

    private void Awake()
    {
        DisableDebugLogs();
        Debug.Log("Application Folder: " + Application.persistentDataPath);

        try
        {
            DatabaseBootstrapper.EnsureReady();
            LoadDatabase();
        }
        catch (Exception e)
        {
            database = new Database();
            SaveSystem.MarkLoadFailed("AppHandler konnte die Datenbank nicht laden: " + e.Message);
        }

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

    public bool SaveDatabase()
    {
        return SaveSystem.SaveDatabase(database);
    }

    public void SetPlayers(List<Guid> active, List<Guid> reserve)
    {
        database.SetPlayers(active, reserve);
        MarkDatabaseDirty();
    }

    public void ApplyPlayerSetupStartOrderFromFlags()
    {
        database.ApplySetupPlayerOrderFromFlags();
        MarkDatabaseDirty();
    }

    public void AddPlayer(string playerName)
    {
        Guid newId = database.AddPlayer(playerName);
        MarkDatabaseDirty();
        OnPlayerAdded?.Invoke(newId);
    }

    public void AddDartBot(float skillRating)
    {
        Guid newId = database.AddDartBot(skillRating);
        MarkDatabaseDirty();
        OnPlayerAdded?.Invoke(newId);
    }

    public void RenamePlayer(Guid playerID, string newName)
    {
        string oldName = GetPlayerByID(playerID)?.GetName();
        database.RenamePlayer(playerID, newName);
        MarkDatabaseDirty();
        OnPlayerRenamed?.Invoke(playerID, oldName, newName);
    }

    public void DeletePlayer(Guid playerID)
    {
        string oldName = GetPlayerByID(playerID)?.GetName();
        database.DeletePlayer(playerID);
        MarkDatabaseDirty();
        OnPlayerDeleted?.Invoke(playerID, oldName);
    }

    public void SetPlayerVisibleInStatistics(Guid id, bool isVisible)
    {
        database.SetPlayerVisibleInStatistics(id, isVisible);
        // Has never been persistent before (selected after restart) —
        // Now it's on the bundled dirty save.
        MarkDatabaseDirty();
    }

    public Game LoadGameById(Guid id)
    {
        return SaveSystem.LoadGameById(id);
    }

    public bool SaveGame(Game g)
    {
        if (g == null)
            return false;

        SetSelectedGame(g);

        // Pending player/setup changes with backup (crash security),
        // No-op if nothing has been changed since last write.
        FlushDatabase();

        // Per-game states always recompute from the structure (idempotent thanks to resets),
        // so that Reload+Save (e.g. Summary-Screen) also writes correct legs/sets/games.
        // Cricket/ATC have their own overrides for mode-specific fields.
        g.CalculatePlayerStatsOnSave();

        if (!SaveSystem.SaveGame(g))
        {
            Debug.LogError("Spiel konnte nicht gespeichert werden: " + g.GetID());
            return false;
        }

        if (g.IsFinished())
        {
            ApplyGameStatsToPlayers(g);
            if (!SaveSystem.SaveAggregatedPlayerStats(g))
            {
                Debug.LogError("Spiel gespeichert, aber Aggregate konnten nicht aktualisiert werden: " + g.GetID());
            }
        }

        g.MarkSavedNow();

        var summary = CreateGameSummaryFromGame(g);
        database.AddGameSummary(summary);

        OnAddGame?.Invoke(g);
        return true;
    }

    public bool DeleteGame(Guid id)
    {
        GameSummary summary = database.GetGameSummary(id);

        // Load stats BEFORE the SQLite delete (after which the rows are gone).
        // A targeted single-game read instead of full-table read.
        var playerStats = summary != null && summary.IsFinished
            ? SqliteDatabaseRepository.LoadPlayerGameStatsForGame(id)
            : null;

        if (!SaveSystem.DeleteGame(id))
        {
            Debug.LogError("Spiel konnte nicht gelöscht werden: " + id);
            return false;
        }

        database.DeleteGame(id);

        if (summary != null && summary.IsFinished)
        {
            // Subtract aggregates incrementally instead of all tables from all
            // rebuild (O(1) instead of O(history)).
            SubtractAggregatedPlayerStats(summary, playerStats);

            if (playerStats != null)
            {
                foreach (var playerStat in playerStats)
                {
                    var player = GetPlayerByID(playerStat.Key);
                    player?.RemoveGameStatsFromSummary(summary, playerStat.Value);
                }
            }
        }

        if (selectedGame != null && selectedGame.GetID() == id)
            selectedGame = null;
        if (activeRuntimeGame != null && activeRuntimeGame.GetID() == id)
            activeRuntimeGame = null;
        if (lastClickedGameId == id)
            lastClickedGameId = Guid.Empty;

        if (summary != null)
        {
            var affectedPlayerIds = playerStats != null
                ? new List<Guid>(playerStats.Keys)
                : (summary.PlayerIds != null ? new List<Guid>(summary.PlayerIds) : new List<Guid>());
            OnDeleteGame?.Invoke(summary.GameMode, id, affectedPlayerIds);
        }
        else
        {
            Debug.Log($"DeleteGame: Kein Summary für {id}; nur persistente Reste entfernt.");
        }

        return true;
    }

    /// <summary>
    /// Subtracts a deleted game from the persistent aggregates.
    /// Mirror of SaveAggregatedPlayerStats; harmless for unfinished
    /// Games (never aggregated) and missing lines (RemoveGameStatguardet).
    /// </summary>
    private void SubtractAggregatedPlayerStats(GameSummary summary, Dictionary<Guid, GameStats> playerStats)
    {
        if (summary == null || !summary.IsFinished)
            return;
        if (playerStats == null || playerStats.Count == 0)
            return;

        foreach (var kvp in playerStats)
        {
            var allStats = SqliteDatabaseRepository.LoadPlayerStatsAll(kvp.Key);
            if (allStats != null)
            {
                allStats.RemoveGameStat(kvp.Value, summary.Id);
                SqliteDatabaseRepository.SavePlayerStatsAll(kvp.Key, allStats);
            }

            var modeStats = SqliteDatabaseRepository.LoadPlayerStatsForMode(kvp.Key, summary.GameMode);
            if (modeStats != null)
            {
                modeStats.RemoveGameStat(kvp.Value, summary.Id);
                SqliteDatabaseRepository.SavePlayerStatsForMode(kvp.Key, modeStats, summary.GameMode);
            }
        }
    }

    public bool DeleteGamesOfMode(GameMode mode)
    {
        var summaries = database.GetGameSummaries()
            .Where(s => s.GameMode == mode)
            .ToList();

        // Load stats ONCE before the delete (a full-read instead of N).
        var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();

        if (!SaveSystem.DeleteGamesByMode(mode))
        {
            Debug.LogError("Spiele des Modus konnten nicht gelöscht werden: " + mode);
            return false;
        }

        database.DeleteGames(mode);

        foreach (var summary in summaries)
        {
            allStats.TryGetValue(summary.Id, out var playerStats);
            SubtractAggregatedPlayerStats(summary, playerStats);

            if (playerStats == null)
                continue;
            foreach (var playerStat in playerStats)
            {
                var player = GetPlayerByID(playerStat.Key);
                player?.RemoveGameStatsFromSummary(summary, playerStat.Value);
            }
        }

        foreach (var player in database.GetPlayers())
        {
            if (player != null)
                player.ResetStatsOfMode(mode);
        }

        var affectedPlayerIds = new List<Guid>();
        foreach (var summary in summaries)
        {
            if (summary?.PlayerIds == null)
                continue;
            foreach (var pid in summary.PlayerIds)
            {
                if (!affectedPlayerIds.Contains(pid))
                    affectedPlayerIds.Add(pid);
            }
        }

        if (selectedGame != null && selectedGame.GetGameMode() == mode)
            selectedGame = null;
        if (activeRuntimeGame != null && activeRuntimeGame.GetGameMode() == mode)
            activeRuntimeGame = null;
        if (lastClickedGameId != Guid.Empty && database.GetGameSummary(lastClickedGameId) == null)
            lastClickedGameId = Guid.Empty;

        OnDeleteGamesOfMode?.Invoke(mode, affectedPlayerIds);
        return true;
    }

    public bool DeleteAllGames()
    {
        if (!SaveSystem.DeleteAllGames())
        {
            Debug.LogError("Spiele konnten nicht gelöscht werden.");
            return false;
        }

        database.DeleteGames(null);
        // Aggregates are empty: Full-Rebuild will not find any games anymore
        // and only cleans the aggregate tables (cheap, no dead loops).
        if (!SaveSystem.RebuildAggregatedPlayerStats())
            Debug.LogError("Spieler-Aggregate konnten nach dem Löschen nicht neu aufgebaut werden.");

        foreach (var player in database.GetPlayers())
        {
            if (player != null)
                player.ResetAllStats();
        }

        selectedGame = null;
        activeRuntimeGame = null;
        lastClickedGameId = Guid.Empty;

        OnAllGamesDeleted?.Invoke();
        return true;
    }

    public void SetSelectedGame(Game game)
    {
        selectedGame = game;
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
        MarkDatabaseDirty();
    }

    public void SetPlayerOrder(List<Guid> newOrder)
    {
        database.SetPlayerOrder(newOrder);
        MarkDatabaseDirty();
    }

    public bool SaveSettings()
    {
        if (!SaveSystem.SaveDatabase(database))
        {
            Debug.LogError("Einstellungen konnten nicht gespeichert werden.");
            return false;
        }

        return true;
    }

    public void SetLastGameMode(GameMode mode)
    {
        database.SetLastGameMode(mode);
        MarkDatabaseDirty();
    }

    public void SetX01Settings(X01GameSettings settings)
    {
        database.SetX01Settings(settings);
        MarkDatabaseDirty();
    }

    public void SetCricketSettings(CricketGameSettings settings)
    {
        database.SetCricketSettings(settings);
        MarkDatabaseDirty();
    }

    public void SetATCSettings(ATCGameSettings settings)
    {
        database.SetATCSettings(settings);
        MarkDatabaseDirty();
    }

    public void SetSetupState(GameMode mode, SetsAndLegs setsAndLegs, int setCount, int legCount)
    {
        database.SetSetupState(mode, setsAndLegs, setCount, legCount);
        MarkDatabaseDirty();
    }

    /// <summary>
    /// Marks player/setup changes as unsaved.
    /// Write runs bundled to idle timeout (bundles click series)
    /// One write instead of one per click.
    /// </summary>
    private void MarkDatabaseDirty()
    {
        databaseDirty = true;

        if (dirtySaveRoutine != null)
            StopCoroutine(dirtySaveRoutine);
        dirtySaveRoutine = StartCoroutine(FlushDatabaseAfterIdle());
    }

    private IEnumerator FlushDatabaseAfterIdle()
    {
        // Realtime: fires reliably even when paused TimeScale.
        yield return new WaitForSecondsRealtime(DatabaseIdleSaveDelaySeconds);
        dirtySaveRoutine = null;
        FlushDatabase();
    }

    /// <summary>
    /// Writes pending player/setup changes. No-op if clean.
    /// Only places with an immediate write: game start, lifecycle, explicit.
    /// </summary>
    private bool FlushDatabase()
    {
        if (!databaseDirty || database == null)
            return database != null;

        if (isShuttingDown)
        {
            Debug.Log("<color=orange>Save blockiert: Domain wird entladen.</color>");
            return false;
        }

        #if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            return false;
        }
        #endif

        if (!SaveSystem.SaveDatabase(database))
        {
            Debug.LogError("Ausstehende Datenbankänderungen konnten nicht gespeichert werden.");
            return false;
        }

        databaseDirty = false;
        return true;
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

        if (database == null)
        {
            Debug.LogWarning("Save blockiert: Datenbank-Referenz ist bereits null.");
            return;
        }

        // Pause/Focus/Quit come bundled: only the first saves, the rest
        // would be an identical full save (game rewrite + DB).
        // Nothing changes because the app is in the background.
        // before the Summaries-Check, so that double events do not do any work.
        if (Time.realtimeSinceStartup - lastLifecycleSaveTime < LifecycleSaveCooldownSeconds)
            return;
        lastLifecycleSaveTime = Time.realtimeSinceStartup;

        if (database.GetGameSummaries() == null)
        {
            Debug.LogWarning("Save blockiert: Datenbank-Referenz ist bereits null.");
            return;
        }

        PersistOpenRuntimeGames();

        FlushDatabase();
    }

    public bool SaveBeforeExit()
    {
        if (isShuttingDown)
        {
            Debug.Log("<color=orange>Save blockiert: Domain wird entladen.</color>");
            return false;
        }

        #if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
            return false;
        #endif

        if (database == null || database.GetGameSummaries() == null)
        {
            Debug.LogWarning("Save blockiert: Datenbank-Referenz ist bereits null.");
            return false;
        }

        if (dirtySaveRoutine != null)
        {
            StopCoroutine(dirtySaveRoutine);
            dirtySaveRoutine = null;
        }

        bool gamesSaved = PersistOpenRuntimeGames();
        bool databaseSaved = FlushDatabase();
        bool saved = gamesSaved && databaseSaved;

        if (saved)
            lastLifecycleSaveTime = Time.realtimeSinceStartup;
        else
            lastLifecycleSaveTime = -1000f;

        return saved;
    }

    private bool PersistOpenRuntimeGames()
    {
        if (database == null || database.GetGameSummaries() == null)
            return false;

        var runtimeGames = new List<Game>();
        if (selectedGame != null)
            runtimeGames.Add(selectedGame);
        if (activeRuntimeGame != null)
            runtimeGames.Add(activeRuntimeGame);

        var seenGameIds = new HashSet<Guid>();
        var gamesToSave = new List<Game>();

        foreach (var game in runtimeGames)
        {
            if (game == null || game.GetID() == Guid.Empty || seenGameIds.Contains(game.GetID()))
                continue;

            seenGameIds.Add(game.GetID());
            if (!game.IsFinished())
                game.CalculatePlayerStatsOnSave();

            gamesToSave.Add(game);
        }

        if (gamesToSave.Count == 0)
            return true;

        if (!SaveSystem.SaveGames(gamesToSave))
        {
            Debug.LogError($"Lifecycle-Save für {gamesToSave.Count} offene Spiele fehlgeschlagen.");
            return false;
        }

        return true;
    }

    private void LoadDatabase()
    {
        database = SaveSystem.LoadDatabase();
        // Note: NO RebuildPlayerStatistics() more here. SaveSystem has
        // already RebuildAllPlayerStatistics() running (all-time AND)
        // Time ranges from a single full table read.
        // Run would be a complete re-deserialize of all games.
        // Time-range refresh at date change runs over
        // RebuildTimeBasedStatistics().
    }

    /// <summary>
    /// Build only the time-range snapshots (Today/Week/Month/Year) of all players
    /// new from the stored per-game stats.
    /// The snapshots from the save time are otherwise stale.
    /// Once a day, not per event.
    /// </summary>
    public void RebuildTimeBasedStatistics()
    {
        database?.RebuildPlayerStatistics();
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
                LegsWon = game.GetWonLegsTotal(pid),
                TargetsHit = game is ATCGame atc ? atc.GetTargetsHit(pid) : 0,
                TotalTargets = game is ATCGame atc2 ? atc2.GetTotalTargets() : 0,
                WallCount = ps?.wallCount ?? 0,
                CeilingCount = ps?.ceilingCount ?? 0,
                AllMissCount = ps?.allMissCount ?? 0,
                ThreeOnesCount = ps?.tripleOnesCount ?? 0,
                TripleDigitCount = ps?.tripleDigitCount ?? 0,
                LostGameCount = ps?.lostGame ?? 0,
                CustomPenaltyCounts = ps?.customPenaltyCounts != null
                    ? new Dictionary<string, int>(ps.customPenaltyCounts)
                    : new Dictionary<string, int>()
            };
            summary.PlayerEntries.Add(entry);
        }

        // Snapshot from the game (start-status, incl. enabled) — same
        // Source like the persistent path so that names are instead of IDs.
        try
        {
            summary.CustomPenaltySnapshot = game.GetPenaltySnapshot();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AppHandler] Custom-Penalty-Snapshot fehlgeschlagen: " + e.Message);
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

    /// <summary>
    /// Capped frame rate instead of maximum FPS: VSync at +60 FPS give the GPU
    /// Sleep phases (no thermal throttling) and stable 16.6ms frames.
    /// The old maximum mode (vSync from + up to 120 FPS continuous load on
    /// Static UI) was the main cause of sporadic jerks on the phone.
    /// </summary>
    private void MaximizeFPS()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;

        Debug.Log("[FPS Manager] VSync an, Target Frame Rate auf stabile 60 FPS gesetzt.");
    }

    internal List<Player> GetLastSetOfPlayers()
    {
        throw new NotImplementedException();
    }
}
