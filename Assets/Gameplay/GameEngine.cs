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
    public SummaryScreen summary;
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
    private Coroutine pendingBotCheckCoroutine;
    private Guid pendingBotCheckPlayerId;
    private bool hasPendingBotCheck;
    private WinAnimationController winAnimation;

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

    public List<CustomHitDef> GetHitSectorSnapshot() => game?.GetHitSectorSnapshot();

    protected virtual void InitializeGame(Game game)
    {
        // Safeguard
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

        // Subscribe first, so that Game Start (manual event) via the
        // Game snapshot can be filtered and played.
        SubscribeToGameEvents();
        game.OnSectorHit += HandleSectorHit;

        PlayManualAudioEvent(SeededAudioEvents.GameStartId);

        SetHeadline();

        HidePausePanel();

        EnsureWinAnimation();

        if (inputHandler != null)
            inputHandler.RefreshSectorUiFromGame();
    }

    public virtual void OnClickAbortSaveGame()
    {
        if (game == null)
            return;

        game.CalculatePlayerStatsOnSave();

        if (!appHandler.SaveGame(game))
        {
            Debug.LogError("Spiel konnte beim Abbruch nicht gespeichert werden.");
            return;
        }

        CleanupGame();

        windowHandler.HidePopup(0f);
        windowHandler.ResetTo(ScreenId.Zoggen);
    }

    public virtual void OnClickAbortQuitGame()
    {
        if (game == null)
            return;

        if (!appHandler.DeleteGame(game.GetID()))
        {
            Debug.LogError("Spiel konnte beim Abbruch nicht gelöscht werden.");
            return;
        }

        CleanupGame();

        windowHandler.HidePopup(0f);
        windowHandler.ResetTo(ScreenId.Zoggen);
    }

    public virtual void OnClickAbortCancel()
    {
        isPaused = false;
        windowHandler.HidePopup();

        if (game != null)
            HandleBotCheck(game.GetCurrentPlayerId());
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
        // e.g. X01 overwrites this
    }

    protected void StartBotTurn(DartBot bot)
    {
        StopBotTurn(); // defensive: never double-run
        botCoroutine = StartCoroutine(HandleBotTurn(bot));
    }

    protected void StopBotTurn()
    {
        if (pendingBotCheckCoroutine != null)
        {
            StopCoroutine(pendingBotCheckCoroutine);
            pendingBotCheckCoroutine = null;
        }

        hasPendingBotCheck = false;

        inputHandler?.CancelBotHitHold();

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
        if (!appHandler.SaveGame(game))
        {
            Debug.LogError("Beendetes Spiel konnte nicht gespeichert werden.");
            return;
        }

        NotifyWin(WinLevel.Match, playerId, () =>
        {
            summary.ShowSummary(summaryObj);
            windowHandler.Replace(ScreenId.Summary);
        });
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

        // Snapshot for transient display (summary screen): Game start state
        // from the game (not live store), so that name + enabled stand to
        // at game time.
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
        if (inputHandler != null && inputHandler.IsBotHitHoldActive)
        {
            pendingBotCheckPlayerId = playerId;
            hasPendingBotCheck = true;

            if (pendingBotCheckCoroutine == null)
                pendingBotCheckCoroutine = StartCoroutine(HandleBotCheckAfterHitHold());

            return;
        }

        HandleBotCheckNow(playerId);
    }

    private IEnumerator HandleBotCheckAfterHitHold()
    {
        yield return new WaitUntil(() => inputHandler == null || !inputHandler.IsBotHitHoldActive);

        pendingBotCheckCoroutine = null;

        if (!hasPendingBotCheck)
            yield break;

        hasPendingBotCheck = false;
        HandleBotCheckNow(pendingBotCheckPlayerId);
    }

    private void HandleBotCheckNow(Guid playerId)
    {
        BasePlayer player =
            appHandler.GetPlayerByID(playerId);

        if (player is DartBot bot)
        {
            // Always show bot UI
            inputHandler.ShowBotPlayingState(bot);

            // Bot stopped by Undo or Pause
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
    /// Custom penalty triggered: only audio (counter/cost follow separately).
    /// Turn parameters for later counter-connection.
    /// </summary>
    protected void HandleCustomPenaltyAudio(string penaltyId, Guid playerId, Turn turn)
    {
        if (audioManager != null)
            audioManager.PlayCustomPenaltyClip(penaltyId);
    }

    /// <summary>Custom audio event triggered: play only.</summary>
    protected void HandleCustomAudioEvent(string eventId)
    {
        if (audioManager != null)
            audioManager.PlayCustomEvent(eventId);
    }

    /// <summary>
    /// Sector input (Wall/Ceiling/Custom): plays the sector-owned clip
    /// (Key = sector ID) The penalty sound follows separately over
    /// OnCustomPenaltyTriggered, the queue keeps the order.
    /// </summary>
    protected virtual void HandleSectorHit(Guid playerId, string sectorId)
    {
        if (audioManager != null)
            audioManager.PlaySectorClip(sectorId);
    }

    /// <summary>
    /// Play manual audio event only when it is active in the game snapshot
    /// and is released for mode (disabled = mute).
    /// </summary>
    protected void PlayManualAudioEvent(string eventId)
    {
        game?.TryTriggerManualAudioEvent(eventId);
    }

    private void EnsureWinAnimation()
    {
        if (winAnimation != null)
            return;

        winAnimation = FindAnyObjectByType<WinAnimationController>();
        if (winAnimation == null)
            winAnimation = WinAnimationController.Create();
    }

    protected void NotifyWin(WinLevel level, Guid playerId, Action onComplete = null)
    {
        EnsureWinAnimation();

        string playerName = appHandler?.GetPlayerByID(playerId)?.GetName() ?? string.Empty;
        winAnimation.NotifyWin(level, playerName, onComplete);
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

            if (game != null)
            {
                BasePlayer current = appHandler.GetPlayerByID(game.GetCurrentPlayerId());
                if (current is DartBot bot)
                    inputHandler.ShowBotPlayingState(bot);
            }

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