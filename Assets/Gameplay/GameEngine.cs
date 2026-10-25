using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Newtonsoft.Json;

public abstract class GameEngine : MonoBehaviour
{
    [Header("Dependencies")]
    public AppHandler appHandler;
    public WindowHandler windowHandler;
    public AudioManager audioManager;
    public Summary summary;
    public InputHandler inputHandler;

    [Header("UI")]
    public TMP_Text settingsHeadlineText;
    public UIScreen popupAbortGame;
    public GameObject pausePanel;

    protected Game game;
    protected GameSettings settings;

    protected bool isPaused;
    protected Coroutine botCoroutine;
    private bool botUndoPending;

    public virtual void StartGame(Game game)
    {
        InitializeGame(game);
    }

    public virtual void LoadGame(Game game)
    {
        Debug.Log("[GameEngine] LoadGame: " + game.GetID());
        if (game == null)
            return;

        game.InitializeAfterLoad();

        InitializeGame(game);

        Debug.Log("[GameEngine] Settings: " + game.GetSettings().GetString());
    }

    protected virtual void InitializeGame(Game game)
    {
        // Absicherung
        if (game == null)
        {
            Debug.LogError("X01GameEngine: Game Objekt ist null");
            OnClickAbortQuitGame();
            return;
        }

        if (appHandler == null)
            appHandler = FindAnyObjectByType<AppHandler>();


        Debug.Log("GameEngine: Initialisiere");
        this.game = game;

        isPaused = false;

        // Erst abonnieren, damit Game Start (manuelles Event) über den
        // Spiel-Snapshot gefiltert und abgespielt werden kann.
        SubscribeToGameEvents();
        game.OnSectorHit += HandleSectorHit;

        PlayManualAudioEvent(SeededAudioEvents.GameStartId);

        SetHeadline();

        HidePausePanel();
    }

    public virtual void OnClickAbortSaveGame()
    {
        game.CalculatePlayerStatsOnSave();

        appHandler.SaveGame(game);

        CleanupGame();

        windowHandler.GoTo(ScreenId.Zoggen);
        windowHandler.HidePopup();
    }

    public virtual void OnClickAbortQuitGame()
    {
        appHandler.DeleteGame(game.GetID());

        CleanupGame();

        windowHandler.GoTo(ScreenId.Zoggen);
        windowHandler.HidePopup();
    }

    public virtual void OnClickAbortCancel()
    {
        isPaused = false;
        windowHandler.HidePopup();
    }

    public void ResumeBotTurn()
    {
        botUndoPending = false;
        isPaused = false;

        HandleBotCheck(game.GetCurrentPlayerId());
    }

    protected virtual void CleanupGame()
    {
        StopBotTurn();

        if (game != null)
            game.OnSectorHit -= HandleSectorHit;

        UnsubscribeFromGameEvents();

        game = null;
        settings = default;
    }

    protected virtual void BeforeSave()
    {
        // z.B. X01 überschreibt das
    }

    protected void StartBotTurn(DartBot bot)
    {
        StopBotTurn(); // defensive: nie doppelt laufen lassen
        botCoroutine = StartCoroutine(HandleBotTurn(bot));
    }

    protected void StopBotTurn()
    {
        if (botCoroutine == null)
            return;

        StopCoroutine(botCoroutine);
        botCoroutine = null;
    }

    protected abstract IEnumerator HandleBotTurn(DartBot bot);

    protected virtual void HandleMatchWon(Guid playerId)
    {
        StopBotTurn();

        PlayManualAudioEvent(SeededAudioEvents.GameOverId);

        game.CalculatePlayerStatsOnSave();

        var summaryObj = CreateGameSummaryFromGame(game);
        appHandler.SaveGame(game);
        summary.ShowSummary(summaryObj);
        windowHandler.GoTo(ScreenId.Summary);
    }

    protected GameSummary CreateGameSummaryFromGame(Game game)
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
            SettingsJson = Newtonsoft.Json.JsonConvert.SerializeObject(game.GetSettings())
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

        // Snapshot für transiente Anzeige (Summary-Screen): Spielstart-Stand
        // aus dem Game (nicht Live-Store), damit Namen + enabled-Stand zum
        // Spielzeitpunkt stehen.
        try
        {
            summary.CustomPenaltySnapshot = game.GetPenaltySnapshot();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[GameEngine] Custom-Penalty-Snapshot fehlgeschlagen: " + e.Message);
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

    protected void ShowPausePanel()
    {
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    protected void HidePausePanel()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    protected void SetHeadline()
    {
        settingsHeadlineText.text = game.GetSettings().GetString();
    }

    protected virtual void OnDestroy()
    {
        if (game != null)
            game.OnSectorHit -= HandleSectorHit;

        UnsubscribeFromGameEvents();
    }

    protected void HandleBotCheck(Guid playerId)
    {
        BasePlayer player =
            appHandler.GetPlayerByID(playerId);


        if (player is DartBot bot)
        {
            // Immer Bot-UI anzeigen
            inputHandler.ShowBotPlayingState();


            // Bot wurde durch Undo oder Pause angehalten
            if (botUndoPending || isPaused)
            {
                inputHandler.ShowBotPausedState();
                return;
            }


            StartBotTurn(bot);
        }
        else
        {
            botUndoPending = false;

            inputHandler.HideBotState();

            inputHandler.SetInputEnabled(true);
        }
    }

    /// <summary>
    /// Custom-Penalty ausgelöst: nur Audio (Zähler/Kosten folgen separat).
    /// Turn-Parameter für spätere Counter-Anbindung.
    /// </summary>
    protected void HandleCustomPenaltyAudio(string penaltyId, Guid playerId, Turn turn)
    {
        if (audioManager != null)
            audioManager.PlayCustomPenaltyClip(penaltyId);
    }

    /// <summary>Custom-Audio-Event ausgelöst: nur Abspielen.</summary>
    protected void HandleCustomAudioEvent(string eventId)
    {
        if (audioManager != null)
            audioManager.PlayCustomEvent(eventId);
    }

    /// <summary>
    /// Sektor-Eingabe (Wall/Ceiling/Custom): spielt den Sektor-eigenen Clip
    /// (Key = Sektor-ID). Der Penalty-Sound folgt separat über
    /// OnCustomPenaltyTriggered, die Queue hält die Reihenfolge ein.
    /// </summary>
    protected virtual void HandleSectorHit(Guid playerId, string sectorId)
    {
        if (audioManager != null)
            audioManager.PlaySectorClip(sectorId);
    }

    /// <summary>
    /// Manuelles Audio-Event nur abspielen, wenn es im Spiel-Snapshot aktiv
    /// und für den Modus freigegeben ist (disabled = stumm).
    /// </summary>
    protected void PlayManualAudioEvent(string eventId)
    {
        game?.TryTriggerManualAudioEvent(eventId);
    }

    public void PauseBotTurn()
    {
        isPaused = true;

        StopBotTurn();

        inputHandler.ShowBotPausedState();
    }

    protected void HandleUndoBotState()
    {
        StopBotTurn();


        if (IsCurrentPlayerBot())
        {
            botUndoPending = true;

            inputHandler.ShowBotPausedState();
        }
        else
        {
            botUndoPending = false;

            inputHandler.HideBotState();
        }
    }

    protected bool IsCurrentPlayerBot()
    {
        if (game == null)
            return false;

        BasePlayer player =
            appHandler.GetPlayerByID(game.GetCurrentPlayerId());

        return player is DartBot;
    }

    public abstract void Undo();
    public abstract void AddThrow(Throw t);
    protected abstract void SubscribeToGameEvents();
    protected abstract void UnsubscribeFromGameEvents();
}