using System;
using System.Collections.Generic;
using System.IO;
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

    private bool isShuttingDown = false;

    

// ================= UNITY LIFECYCLE =================

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
        return true; // Erlaubt das Beenden, setzt aber vorher unseren Riegel
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

// ================= PUBLIC GETTERS =================

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

    public List<Game> GetGames()
    {
        return database.GetGames();
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
        return database.GetX01Settings();;
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

// ================= PUBLIC METHODS =================

    public void SaveDatabase()
    {
        SaveSystem.SaveDatabase(database);
    }

    public void SetPlayers(List<Guid> active, List<Guid> reserve)
    {
        database.SetPlayers(active, reserve);
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

    public Game LoadGame(Guid id)
    {
        return database.LoadGame(id);
    }

    public void SaveGame(Game g)
    {
        if (g == null)
            return;

        if (g.IsFinished())
        {
            ApplyGameStatsToPlayers(g);
        }

        OnAddGame?.Invoke(g);
    }

    public void DeleteGame(Guid id)
    {
        Game game = database.LoadGame(id);
        GameMode? mode = database.DeleteGame(id);

        if (mode == null)
        {
            Debug.LogWarning($"DeleteGame: Kein Game mit ID {id} gefunden.");
            return;
        }

        if (game != null)
        {
            if (game.IsFinished())
                RemoveGameStatsFromPlayers(game);
            OnDeleteGame?.Invoke(mode.Value, id);
        }
    }

    public void DeleteGamesOfMode(GameMode mode)
    {
        database.DeleteGames(mode);
        SaveSystem.SaveDatabase(database);

        foreach (var player in database.GetPlayers())
        {
            if (player != null)
                player.ResetStatsOfMode(mode);
        }

        OnDeleteGamesOfMode?.Invoke(mode);
    }

    public void DeleteAllGames()
    {
        database.DeleteGames(null);
        SaveSystem.SaveDatabase(database);

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
    }

    public X01Game PrepareX01Game(X01GameSettings data)
    {
        var game = database.PrepareX01Game(data);
        return game;
    }

    public CricketGame PrepareCricketGame(CricketGameSettings data)
    {
        var game = database.PrepareCricketGame(data);
        return game;
    }

    public ATCGame PrepareATCGame(ATCGameSettings data)
    {
        var game = database.PrepareATCGame(data);
        return game;
    }

    public void SetActivePlayerOrder(List<Guid> newOrder)
    {
        database.SetActivePlayerOrder(newOrder);
        SaveSystem.SaveDatabase(database);
    }

    // {
    //     return database.GetAppSettings();
    // }

    public void SaveSettings()
    {
        SaveSystem.SaveDatabase(database);
    }

    public void SetLastGameMode(GameMode mode)
    {
        database.SetLastGameMode(mode);
        SaveSystem.SaveDatabase(database);
    }

// ================= PRIVATE HELPERS =================

    private void HandleDomainUnload(object sender, EventArgs e)
    {
        isShuttingDown = true;
    }

    // Eine zentrale Methode für alle Speicheraufrufe
    private void SafeSave()
    {
        // 1. Warte-Check: Wenn die AppDomain gerade entladen wird (Rekomplierung/Stop), brich sofort ab!
        if (isShuttingDown) 
        {
            Debug.Log("<color=orange>Save blockiert: Domain wird entladen.</color>");
            return;
        }

        #if UNITY_EDITOR
        // 2. Editor-Check: Falls wir gerade mitten im Re-Load/Kompilieren sind
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            return;
        }
        #endif

        // 3. Daten-Validierung: Wenn das Objekt im Speicher schon leer ist, nicht die Datei überschreiben!
        if (database == null || database.GetGames() == null) 
        {
            Debug.LogWarning("Save blockiert: Datenbank-Referenz ist bereits null.");
            return;
        }

        SaveSystem.SaveDatabase(database);
    }

    private void LoadDatabase()
    {
        // Load with fallback (main -> backups). If nothing valid exists, a new DB is returned.
        database = SaveSystem.LoadDatabase();
        database.RebuildPlayerStatistics();

        // Ensure there is always a main file on disk.
        SaveSystem.SaveDatabase(database);
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

    private void RemoveGameStatsFromPlayers(Game game)
    {
        if (game == null)
            return;

        foreach (Guid pid in game.GetPlayerIDs())
        {
            BasePlayer player = GetPlayerByID(pid);
            player?.RemoveGameStats(game);
        }
    }

    private void DisableDebugLogs()
    {
    // Deaktiviert alle Debug.Log Ausgaben im finalen Build
    #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.logEnabled = false;
    #endif
    }

    private void MaximizeFPS()
    {
        // 1. VSync ausschalten, damit das Unity-Limit greift
        QualitySettings.vSyncCount = 0;

        // 2. Die maximale Hertz-Zahl des aktuellen Displays auslesen
        double screenRefreshRate = Screen.currentResolution.refreshRateRatio.value;

        // 3. Framerate auf das Maximum des Displays setzen (aufgerundet als Int)
        int targetFPS = Mathf.CeilToInt((float)screenRefreshRate);

        // Falls die Abfrage fehlschlägt (Wert <= 0), Sicherheitsnetz auf 60 FPS
        if (targetFPS <= 0)
        {
            targetFPS = 60;
        }

        Application.targetFrameRate = targetFPS;

        // Optionale Ausgabe für dich im Log
        Debug.Log($"[FPS Manager] Target Frame Rate automatisch auf Maximum gesetzt: {targetFPS} FPS");
    }






    




































































































}