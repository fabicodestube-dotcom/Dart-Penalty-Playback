using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

public class ATCGameEngine : GameEngine
{
    [Header("Dependencies")]

    [Header("UI")]
    public ATCPlayerList playerList;

    // =========================
    // GAME START / LOAD
    // =========================

    protected override void InitializeGame(Game game)
    {
        base.InitializeGame(game);

        settings = ((ATCGame)game).GetSettingsAsATC();

        InitPlayerList();


        HandleBotCheck(game.GetCurrentPlayerId());

        inputHandler?.SetUndoEnabled(false);
    }

    // =========================
    // PLAYER LIST
    // =========================

    private void InitPlayerList()
    {
        if (playerList == null)
            return;

        var players = game.GetPlayerIDs()
            .Select(id => appHandler.GetPlayerByID(id))
            .ToList();
        playerList.Init(players);
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        if (playerList == null)
            return;

        playerList.Refresh((ATCGame) game);
        ((ATCInputHandler) inputHandler).UpdateHitButtonLabel();
    }

    // =========================
    // EVENT SUBSCRIPTION
    // =========================

    protected override void SubscribeToGameEvents()
    {
        if (game == null)
            return;

        game.OnLegWon += HandleLegWon;
        game.OnSetWon += HandleSetWon;
        game.OnMatchWon += HandleMatchWon;
        game.OnPenaltyTriggered += HandlePenalty;
        ((ATCGame) game).OnATCTurnCompleted += HandleTurnCompleted;
        ((ATCGame) game).OnStreakStarted += HandleATCStreak;
        game.OnBotCheck += HandleBotCheck;
    }

    // =========================
    // EVENT HANDLERS
    // =========================

    private void HandleATCStreak(Guid playerId)
    {
        if (audioManager != null)
            audioManager.PlaySpecialClip(SpecialAudioType.ATCStreak);
    }

    private void HandleLegWon(Guid playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Leg gewonnen");

        StopBotTurn();

        playerList.Refresh((ATCGame) game);

        HandleBotCheck(game.GetCurrentPlayerId());
    }

    private void HandleSetWon(Guid? playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Set gewonnen");

        StopBotTurn();

        playerList.Refresh((ATCGame) game);

        HandleBotCheck(game.GetCurrentPlayerId());
    }

    private void HandlePenalty(Guid playerId, PenaltyType type, Turn turn)
    {
        if (audioManager != null)
            audioManager.PlayPenalty(type);

        RefreshPlayerList();
    }

    private void HandleTurnCompleted(Guid playerId, int validThrows)
    {
        audioManager.PlayScore(validThrows);
        audioManager.PlaySpecialClip(SpecialAudioType.Hits);
    }

    public void OnClickContinueFromPause()
    {
        isPaused = false;
        // pauseConfirmed = true;
        // botPausePending = false;
        inputHandler?.SetUndoEnabled(false);
        HidePausePanel();

        if (game != null && !game.IsFinished() && !IsCurrentPlayerBot())
        {
            HandleBotCheck(game.GetCurrentPlayerId());
        }
    }

    protected override IEnumerator HandleBotTurn(DartBot bot)
    {
        inputHandler.SetInputEnabled(false);
        yield return new WaitForSeconds(1.5f);

        // Endlose Schleife, die nur abbricht, wenn der Bot nicht mehr dran ist
        while (!isPaused && game.GetCurrentPlayerId() == bot.GetID() && !game.IsFinished())
        {
            //yield return new WaitUntil(() => !isPaused && (!botPausePending || pauseConfirmed));
            yield return new WaitUntil(() => !isPaused);
            Guid playerId = bot.GetID();
            
            // Ziel für diesen spezifischen Wurf holen
            Throw t = bot.GetNextATCThrow(
                ((ATCGame) game).GetCurrentTarget(playerId),
                ((ATCGame) game).GetSettingsAsATC().targetType,
                ((ATCGame) game).HasPlayerStarted(playerId)
            );

            AddThrow(t);

            // Nach AddThrow hat die Engine evtl. NextPlayer() gerufen (bei Miss oder Turn-Ende ohne Carry)
            // Wir prüfen also sofort, ob wir noch dran sind
            if (game.GetCurrentPlayerId() != bot.GetID() || game.IsFinished())
            {
                break; 
            }

            yield return new WaitForSeconds(1.5f);
        }

        Debug.Log($"Bot {bot.GetName()} beendet seinen Turn-Zyklus.");
    }


    // =========================
    // CORE GAME ACTIONS
    // =========================

    public override void AddThrow(Throw t)
    {
        if (game == null)
            return;

        ((ATCGame) game).AddThrow(t);
        RefreshPlayerList();
    }

    public override void Undo()
    {
        if (game == null)
            return;

        ((ATCGame) game).Undo();
        audioManager?.PlaySpecialClip(SpecialAudioType.Undo);
        RefreshPlayerList();
        HandleUndoBotState();
    }

    public ATCGame GetGame() => ((ATCGame) game);
    public ATCGameSettings GetSettings() => ((ATCGameSettings) settings);

    // =========================
    // UI NAVIGATION
    // =========================

    public void OnClickReturnFromATC()
    {
        isPaused = true;

        if (windowHandler == null)
            windowHandler = FindFirstObjectByType<WindowHandler>();

        if (windowHandler != null && popupAbortGame != null){

            windowHandler.ShowPopup(popupAbortGame);
        }
        else
            windowHandler.GoTo(ScreenId.Zoggen);
    }

    protected override void UnsubscribeFromGameEvents()
    {
        if (game == null) return;

        game.OnLegWon -= HandleLegWon;
        game.OnSetWon -= HandleSetWon;
        game.OnMatchWon -= HandleMatchWon;
        game.OnPenaltyTriggered -= HandlePenalty;
        ((ATCGame) game).OnATCTurnCompleted -= HandleTurnCompleted;
        game.OnBotCheck -= HandleBotCheck;
        ((ATCGame) game).OnStreakStarted -= HandleATCStreak;
    }

    private Throw CreateHitThrow()
    {
        if (game == null)
            return new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);

        Guid playerId = game.GetCurrentPlayerId();
        int target = ((ATCGame) game).GetCurrentTarget(playerId);
        var settings = ((ATCGame) game).GetSettingsAsATC();
        bool hasStarted = ((ATCGame) game).HasPlayerStarted(playerId);

        if (target == -1)
            return new Throw(DartMultiplier.Single, 0, HitType.Board, false, -1);

        switch (settings.targetType)
        {
            case ATCTargetType.Singles:
                return new Throw(DartMultiplier.Single, target, HitType.Board, true, target);
            case ATCTargetType.Doubles:
                return new Throw(DartMultiplier.Double, target, HitType.Board, true, target);
            case ATCTargetType.Triples:
                return new Throw(DartMultiplier.Triple, target, HitType.Board, true, target);
            default:
                return new Throw(DartMultiplier.Single, target, HitType.Board, true, target);
        }
    }
}