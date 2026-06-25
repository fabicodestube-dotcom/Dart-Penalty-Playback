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

         // Daten-Initialisierung
        settings = ((X01Game)game).GetSettingsAsX01();
        
        checkoutHandler.Show(null);

        playerList.Init(GetUIStats());

        // Logik & Events
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
        game.OnPenaltyTriggered += HandlePenalty;
        game.OnTurnCompleted += HandleTurnCompleted;
        ((X01Game) game).OnCheckoutAvailable += HandleCheckoutAvailable;
        ((X01Game) game).OnFourTwenty += HandleFourTwenty;
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
        playerList.RefreshUI(GetUIStats());
    }

    public override void Undo()
    {
        if (game == null)
            return;

        ((X01Game) game).Undo();
        StopBotTurn();
        audioManager.PlaySpecialClip(SpecialAudioType.Undo);
        playerList.RefreshUI(GetUIStats());
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
        isPaused = true;
        windowHandler.ShowPopup(popupAbortGame);
    }

    public void OnClickAbortCancle()
    {
        isPaused = false;
        windowHandler.HidePopup();
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
            audioManager.PlaySpecialClip(SpecialAudioType.Bust);
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
        // Input sperren
        inputHandler.SetInputEnabled(false);

        yield return new WaitForSeconds(2f);

        for (int i = 0; i < 3; i++)
        {
            // yield return new WaitUntil(() => !isPaused && (!botPausePending || pauseConfirmed));
            yield return new WaitUntil(() => !isPaused);
            // Falls Turn schon beendet (z.B. durch Bust oder Win)
            var currentTurn = game.GetCurrentTurnOfPlayer(bot.GetID());
            
            if (!currentTurn.HasSpace() || currentTurn.IsBust || ((X01Game) game).GetScore(bot.GetID()) == 0)
            {
                Debug.Log("Bust oder Turnende erkannt, keine weiteren Bot-Würfe");
                yield break;
            }

            Throw t = bot.GetNextX01Throw(((X01Game) game).GetScore(bot.GetID()), ((X01Game) game).GetSettingsAsX01().checkoutType);
            AddThrow(t);

            yield return new WaitForSeconds(2f);
        }
    }

    private void HandlePenalty(Guid playerId, PenaltyType type, Turn turn)
    {
        playerList.RefreshUI(GetUIStats());

        audioManager.PlayPenalty(type);
    }

    private void HandleLegWon(Guid playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Leg gewonnen");

        StopBotTurn();

        playerList.RefreshUI(GetUIStats());
    }

    private void HandleSetWon(Guid? playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Set gewonnen");

        StopBotTurn();

        playerList.RefreshUI(GetUIStats());
    }

    private void HandleCheckoutAvailable(Guid playerId, string option)
    {
        if (playerId != GetCurrentPlayerId())
            return;

        // UI bekommt Checkout-Vorschlag
        checkoutHandler.Show(option);
    }

    private void HandleFourTwenty(Guid playerId)
    {
        Debug.Log($"[ENGINE] 🎉 Spieler {playerId} hat genau 420 Punkte!");
        
        audioManager.PlaySpecialClip(SpecialAudioType.FourTwenty);
    }


    // =========================================================
    // CHECKOUT LOGIC
    // =========================================================

    public int GetRemainingDarts(Guid playerId)
    {
        var turn = game.GetCurrentTurnOfPlayer(playerId);

        if (turn == null)
            return 3; // noch kein Turn → volle Darts

        return 3 - turn.GetThrows().Count;
    }

    public List<X01UIStats> GetUIStats()
    {
        var result = new List<X01UIStats>();

        foreach (Guid playerId in game.GetPlayerIDs())
        {
            BasePlayer player = appHandler.GetPlayerByID(playerId);

            var stats = new X01UIStats(((X01Game) game), player);

            result.Add(stats);
        }

        return result;
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
        game.OnPenaltyTriggered -= HandlePenalty;
        game.OnTurnCompleted -= HandleTurnCompleted;
        ((X01Game) game).OnCheckoutAvailable -= HandleCheckoutAvailable;
        ((X01Game) game).OnFourTwenty -= HandleFourTwenty;
        game.OnBotCheck -= HandleBotCheck;
        ((X01Game) game).OnSuddenDeath -= HandleSuddenDeath;
    }



    private void HandleSuddenDeath()
    {
        audioManager.PlaySpecialClip(SpecialAudioType.SuddenDeath);
    }
}