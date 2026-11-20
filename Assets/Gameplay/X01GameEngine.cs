using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class X01GameEngine : GameEngine
{
    [Header("Dependencies")]
    public CheckoutHandler checkoutHandler;
    public X01PlayerList playerList;



    // =========================================================
    // GAME START / LOAD
    // =========================================================


    protected override void InitializeGame(Game game)
    {
        Debug.Log("X01GameEngine: Initialisiere");
        base.InitializeGame(game);

        this.game = (X01Game) game;

         // Data initialization
        settings = ((X01Game)game).GetSettingsAsX01();

        checkoutHandler.Show(null);

        playerList.Init(GetUIStats());
        RefreshBotOverlayStats();

        // Logic & Events
        TriggerInitialCheckout();
        HandleBotCheck(game.GetCurrentPlayerId());
        inputHandler?.SetUndoEnabled(false);
    }


    // =========================================================
    // EVENT SUBSCRIPTION
    // =========================================================

    protected override void SubscribeToGameEvents()
    {
        game.OnLegWon += HandleLegWon;
        game.OnSetWon += HandleSetWon;
        game.OnMatchWon += HandleMatchWon;
        game.OnCustomPenaltyTriggered += HandleCustomPenaltyAudio;
        game.OnCustomAudioEvent += HandleCustomAudioEvent;
        game.OnTurnCompleted += HandleTurnCompleted;
        ((X01Game) game).OnCheckoutAvailable += HandleCheckoutAvailable;
        game.OnBotCheck += HandleBotCheck;
        ((X01Game) game).OnSuddenDeath += HandleSuddenDeath;
    }


    // =========================================================
    // CORE GAME ACTIONS
    // =========================================================

    public override void AddThrow(Throw t)
    {
        if (game == null)
            return;
        ((X01Game) game).AddThrow(t);
        RefreshPlayerUI();
    }

    public override void Undo()
    {
        if (game == null)
            return;

        ((X01Game) game).Undo();
        StopBotTurn();
        PlayManualAudioEvent(SeededAudioEvents.UndoId);
        RefreshPlayerUI();
        HandleUndoBotState();
    }


    // =========================================================
    // PLAYER / GAME STATE ACCESS
    // =========================================================

    public Guid GetCurrentPlayerId() => game.GetCurrentPlayerId();



    // =========================================================
    // UI NAVIGATION (ABORT FLOW)
    // =========================================================

    public void OnClickReturnFromX01()
    {
        PauseForAbortPopup();
        windowHandler.ShowPopup(popupAbortGame);
    }

    public override void OnClickAbortCancel()
    {
        base.OnClickAbortCancel();
    }

    // =========================================================
    // TURN / GAMEPLAY EVENTS
    // =========================================================

    private void HandleTurnCompleted(Guid playerId, Turn turn)
    {
        if (game.IsFinished())
            return;

        if (turn.IsBust)
        {
            PlayManualAudioEvent(SeededAudioEvents.BustId);
        }
        else
        {
            if (((X01Game)game).IsCheckedIn(playerId)){
                audioManager.PlayScore(turn.GetTurnScore());
            }
            else
            {
                audioManager.PlayScore(0);
            }
        }
    }

    protected override IEnumerator HandleBotTurn(DartBot bot)
    {
        int version = CurrentBotTurnVersion;

        // Lock input
        inputHandler.SetInputEnabled(false);

        for (int i = 0; i < 3 && version == CurrentBotTurnVersion; i++)
        {
            yield return new WaitUntil(() => !isPaused);

            if (version != CurrentBotTurnVersion)
                yield break;

            // If Turn has already ended (e.g. by Bust or Win)
            var currentTurn = game.GetCurrentTurnOfPlayer(bot.GetID());

            if (currentTurn == null || !currentTurn.HasSpace() || currentTurn.IsBust || ((X01Game) game).GetScore(bot.GetID()) == 0)
            {
                Debug.Log("Bust oder Turnende erkannt, keine weiteren Bot-Würfe");
                yield break;
            }

            Throw t = GetNextX01BotThrow(bot);
            yield return inputHandler.PlayBotThrow(t, () => AddThrow(t));

            if (version != CurrentBotTurnVersion)
                yield break;

            var updatedTurn = game.GetCurrentTurnOfPlayer(bot.GetID());
            bool canThrowAgain = i < 2
                && updatedTurn != null
                && updatedTurn.HasSpace()
                && !updatedTurn.IsBust
                && !game.IsFinished()
                && ((X01Game) game).GetScore(bot.GetID()) != 0;

            if (!canThrowAgain)
                yield break;
        }
    }

    private Throw GetNextX01BotThrow(DartBot bot)
    {
        var x01 = (X01Game) game;
        var settings = x01.GetSettingsAsX01();
        var turn = game.GetCurrentTurnOfPlayer(bot.GetID());
        int turnStartScore = turn != null && turn.GetThrows().Count > 0
            ? turn.ScoreBeforeTurn
            : x01.GetScore(bot.GetID());

        return bot.GetNextX01Throw(
            x01.GetScore(bot.GetID()),
            settings.checkoutType,
            GetRemainingDarts(bot.GetID()),
            x01.IsCheckedIn(bot.GetID()),
            settings.checkinType,
            turnStartScore);
    }

    private void HandleLegWon(Guid playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Leg gewonnen");

        StopBotTurn();

        RefreshPlayerUI();

        NotifyWin(WinLevel.Leg, playerId);
    }

    private void HandleSetWon(Guid? playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Set gewonnen");

        StopBotTurn();

        RefreshPlayerUI();

        if (playerId.HasValue && playerId.Value != Guid.Empty)
            NotifyWin(WinLevel.Set, playerId.Value);
    }

    private void HandleCheckoutAvailable(Guid playerId, string option)
    {
        if (playerId != GetCurrentPlayerId())
            return;

        // UI receives checkout suggestion
        checkoutHandler.Show(option);
    }

    // Note: Restscore 420 fires as global custom event
    // (ScoreReached0@@) about the turn evaluation — consciously NOT
    // more explicit OnFourTwenty hook more, who would play double.


    // =========================================================
    // CHECKOUT LOGIC
    // =========================================================

    public int GetRemainingDarts(Guid playerId)
    {
        var turn = game.GetCurrentTurnOfPlayer(playerId);

        if (turn == null)
            return 3; // No turning yet, full darts

        return 3 - turn.GetThrows().Count;
    }

    public List<PlayerLiveStats> GetUIStats()
    {
        var result = new List<PlayerLiveStats>();

        foreach (Guid playerId in game.GetPlayerIDs())
        {
            BasePlayer player = appHandler.GetPlayerByID(playerId);

            var stats = PlayerLiveStats.Create(game, player);

            result.Add(stats);
        }

        return result;
    }

    private void RefreshPlayerUI()
    {
        playerList.RefreshUI(GetUIStats());
        RefreshBotOverlayStats();
    }

    private void RefreshBotOverlayStats()
    {
        if (inputHandler == null || game == null || appHandler == null)
            return;

        BasePlayer player = appHandler.GetPlayerByID(game.GetCurrentPlayerId());
        if (player != null)
            inputHandler.RefreshBotPlayerStats(PlayerLiveStats.Create(game, player));
    }

    private void TriggerInitialCheckout()
    {
        Guid playerId = game.GetCurrentPlayerId();

        int score = ((X01Game) game).GetScore(playerId);
        int dartsLeft = GetRemainingDarts(playerId);

        var option = CheckoutDatabase.GetCheckout(
            ((X01GameSettings) settings).checkoutType,
            score,
            dartsLeft
        );

        if (option != null)
        {
            HandleCheckoutAvailable(playerId, option);
        }
    }

    protected override void UnsubscribeFromGameEvents()
    {
        if (game == null) return;

        game.OnLegWon -= HandleLegWon;
        game.OnSetWon -= HandleSetWon;
        game.OnMatchWon -= HandleMatchWon;
        game.OnCustomPenaltyTriggered -= HandleCustomPenaltyAudio;
        game.OnCustomAudioEvent -= HandleCustomAudioEvent;
        game.OnTurnCompleted -= HandleTurnCompleted;
        ((X01Game) game).OnCheckoutAvailable -= HandleCheckoutAvailable;
        game.OnBotCheck -= HandleBotCheck;
        ((X01Game) game).OnSuddenDeath -= HandleSuddenDeath;
    }



    private void HandleSuddenDeath()
    {
        // Explicit moment (uniqueness + undo-consistency controls this)
        // suddenDeathTriggered -Flag in the X01Game: plays the custom event,
        // Fallback is the Sudden Death sound.
        PlayManualAudioEvent(SeededAudioEvents.SuddenDeathId);
    }
}