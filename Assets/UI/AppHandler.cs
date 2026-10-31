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
    /// Nach letzter Spieler-/Setup-Änderung so lange warten, bevor die DB
    /// geschrieben wird. Bündelt Klickserien (Setup-Stepper etc.) zu einem Write.
    /// </summary>
    private const float DatabaseIdleSaveDelaySeconds = 2f;

    public ThemeManager themeManager;

    public Action<Game> OnAddGame;
    /// <summary>
    /// Parameter: Modus, GameId, betroffene Spieler-Ids (aus der Summary VOR dem
    /// Delete gelesen). Die In-Memory-Aggregate (BasePlayer) + SQLite-Aggregate
    /// sind beim Feuern bereits aktualisiert — Listener lesen sie direkt und
    /// sparen sich jedes Nachladen.
    /// </summary>
    public Action<GameMode, Guid, List<Guid>> OnDeleteGame;
    /// <summary>
    /// Parameter: Modus, betroffene Spieler-Ids (distinct aus den gelöschten
    /// Summaries). Aggregate sind beim Feuern bereits aktualisiert.
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
    /// True, wenn Spieler-/Setup-Änderungen noch nicht in SQLite stehen.
    /// Alle kleinen Setter markieren nur dirty; geschrieben wird gebündelt
    /// (Idle-Debounce, Spielstart, Lifecycle-Save).
    /// </summary>
    private bool databaseDirty = false;
    private Coroutine dirtySaveRoutine = null;

    /// <summary>
    /// Kollabiert Pause/Fokus/Quit-Doppelevents zu einem Save. Auf Mobile
    /// feuern OnApplicationPause(true) + OnApplicationFocus(false) beim
    /// Weglegen fast immer direkt hintereinander (ggf. + Quit danach).
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

    public void SaveDatabase()
    {
        SaveSystem.SaveDatabase(database);
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

    public void AddDartBot(DartBotDifficulty difficulty)
    {
        Guid newId = database.AddDartBot(difficulty);
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
        // Wurde vorher nie persistiert (Auswahl nach Neustart weg) —
        // läuft jetzt über den gebündelten Dirty-Save mit.
        MarkDatabaseDirty();
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

        // Ausstehende Spieler-/Setup-Änderungen mit sichern (Crash-Sicherheit),
        // No-Op wenn seit letztem Write nichts geändert wurde.
        FlushDatabase();

        // Per-Game-Stats immer aus der Struktur recomputen (idempotent dank Resets),
        // damit auch Reload+Save (z.B. Summary-Screen) korrekte Legs/Sets/Games schreibt.
        // Cricket/ATC haben eigene Overrides für modus-spezifische Felder.
        g.CalculatePlayerStatsOnSave();

        if (g.IsFinished())
        {
            ApplyGameStatsToPlayers(g);
            SaveSystem.SaveGame(g);
            SaveSystem.SaveAggregatedPlayerStats(g);
        }
        else
        {
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

        // Stats VOR dem SQLite-Delete laden (danach sind die Zeilen weg).
        // Ein gezielter Single-Game-Read statt Full-Table-Read.
        var playerStats = SqliteDatabaseRepository.LoadPlayerGameStatsForGame(id);

        SaveSystem.DeleteGame(id);

        // Aggregate inkrementell abziehen statt alle Tabellen aus allen
        // Spielen neu aufzubauen (O(1) statt O(Historie)).
        SubtractAggregatedPlayerStats(summary, playerStats);

        if (summary != null && playerStats != null)
        {
            foreach (var playerStat in playerStats)
            {
                var player = GetPlayerByID(playerStat.Key);
                player?.RemoveGameStatsFromSummary(summary, playerStat.Value);
            }
        }

        var affectedPlayerIds = playerStats != null
            ? new List<Guid>(playerStats.Keys)
            : (summary?.PlayerIds != null
                ? new List<Guid>(summary.PlayerIds)
                : new List<Guid>());
        OnDeleteGame?.Invoke(mode.Value, id, affectedPlayerIds);
    }

    /// <summary>
    /// Zieht EIN gelöschtes Spiel aus den persistierten Aggregaten ab.
    /// Spiegel von SaveAggregatedPlayerStats; ungefährlich für unfertige
    /// Spiele (nie aggregiert) und fehlende Zeilen (RemoveGameStat guardet).
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

    public void DeleteGamesOfMode(GameMode mode)
    {
        var summaries = database.GetGameSummaries()
            .Where(s => s.GameMode == mode)
            .ToList();

        // Stats EINMAL vor dem Delete laden (ein Full-Read statt N).
        var allStats = SqliteDatabaseRepository.LoadAllPlayerGameStats();

        database.DeleteGames(mode);
        SaveSystem.DeleteGamesByMode(mode);

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
        OnDeleteGamesOfMode?.Invoke(mode, affectedPlayerIds);
    }

    public void DeleteAllGames()
    {
        database.DeleteGames(null);
        SaveSystem.DeleteAllGames();
        // Aggregate sind damit leer: Full-Rebuild findet keine Spiele mehr
        // und räumt nur die Aggregat-Tabellen ab (billig, keine toten Loops).
        SaveSystem.RebuildAggregatedPlayerStats();

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
        databaseDirty = true;
    }

    public void SetPlayerOrder(List<Guid> newOrder)
    {
        database.SetPlayerOrder(newOrder);
        databaseDirty = true;
    }

    public void SaveSettings()
    {
        SaveSystem.SaveDatabase(database);
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
    /// Markiert Spieler-/Setup-Änderungen als ungespeichert. Der eigentliche
    /// Write läuft gebündelt nach Idle-Timeout (bündelt Klickserien zu
    /// einem Write statt einem pro Klick).
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
        // Realtime: feuert auch bei pausiertem TimeScale zuverlässig.
        yield return new WaitForSecondsRealtime(DatabaseIdleSaveDelaySeconds);
        dirtySaveRoutine = null;
        FlushDatabase();
    }

    /// <summary>
    /// Schreibt ausstehende Spieler-/Setup-Änderungen. No-Op wenn clean.
    /// Einzige Stellen mit sofortigem Write: Spielstart, Lifecycle, explizit.
    /// </summary>
    private void FlushDatabase()
    {
        if (!databaseDirty || database == null)
            return;

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

        SaveSystem.SaveDatabase(database);
        databaseDirty = false;
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

        // Pause/Fokus/Quit kommen gebündelt: nur der erste speichert, der Rest
        // wäre ein identischer Voll-Save (Spiele-Rewrite + DB). Dazwischen kann
        // sich nichts ändern, da die App im Hintergrund liegt. Steht bewusst
        // vor dem Summaries-Check, damit Doppelevents gar keine Arbeit machen.
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
        // Hinweis: KEIN RebuildPlayerStatistics() mehr hier. SaveSystem hat
        // bereits RebuildAllPlayerStatistics() laufen lassen (All-Time UND
        // Time-Ranges aus einem einzigen Full-Table-Read). Ein zweiter
        // Durchlauf wäre ein komplettes Re-Deserialize aller Spiele.
        // Time-Range-Refresh bei Datumswechsel läuft über
        // RebuildTimeBasedStatistics().
    }

    /// <summary>
    /// Baut nur die Time-Range-Snapshots (Today/Week/Month/Year) aller Spieler
    /// aus den gespeicherten Per-Game-Stats neu. Nötig nach Datumswechsel, da
    /// die Snapshots vom Save-Zeitpunkt sonst stale sind. All-Time-Aggregate
    /// bleiben unangetastet. Einmal pro Tag, nicht pro Event.
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
                LegsWon = game.IsFinished() ? 0 : game.GetWonLegs(pid),
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

        // Snapshot aus dem Game (Spielstart-Stand, inkl. enabled) — gleiche
        // Quelle wie der persistente Pfad, damit Namen statt IDs stehen.
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
    /// Deckelte Bildrate statt Maximal-FPS: VSync an + 60 FPS geben der GPU
    /// Schlafphasen (kein Thermal Throttling) und stabile 16,6ms-Frames.
    /// Der alte Maximum-Modus (vSync aus + bis zu 120 FPS Dauerlast auf
    /// statischer UI) war die Hauptursache für sporadische Ruckler am Phone.
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
