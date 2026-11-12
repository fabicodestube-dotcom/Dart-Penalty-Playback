using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class CricketGameEngine : GameEngine
{
    [Header("Dependencies")]
    public CricketPlayerLayoutController playerList;

    
    // =========================
    // GAME START / LOAD
    // =========================


    protected override void InitializeGame(Game game)
    {
        base.InitializeGame(game);
        settings = ((CricketGame) game).GetSettingsAsCricket();

        InitPlayerList();

        HandleBotCheck(game.GetCurrentPlayerId());

        inputHandler?.SetUndoEnabled(false);
    }

    protected override void SubscribeToGameEvents()
    {
        ((CricketGame) game).OnTurnCompleted += HandleTurnCompleted;
        game.OnLegWon += HandleLegWon;
        game.OnSetWon += HandleSetWon;
        game.OnCustomPenaltyTriggered += HandleCustomPenaltyAudio;
        game.OnCustomAudioEvent += HandleCustomAudioEvent;
        game.OnMatchWon += HandleMatchWon;
        game.OnBotCheck += HandleBotCheck;
    }

    // =========================
    // CORE GAME ACTIONS
    // =========================

    public override void AddThrow(Throw t)
    {
        if (game == null) return;
        ((CricketGame) game).AddThrow(t);
        RefreshPlayerList();
    }

    public override void Undo()
    {
        if (game == null)
            return;
        ((CricketGame) game).Undo();
        PlayManualAudioEvent(SeededAudioEvents.UndoId);
        RefreshPlayerList();
        HandleUndoBotState();
    }

    // =========================
    // UI QUERY API
    // =========================
    public IReadOnlyCollection<int> GetNumbers() => game != null ? ((CricketGame) game).GetNumbers() : new List<int>();

    public CricketGame GetGame() => ((CricketGame) game);


    // =========================
    // UI NAVIGATION (like X01)
    // =========================

    public void OnClickReturnFromCricket()
    {
        if (windowHandler == null)
            return;

        if (popupAbortGame != null){
            isPaused = true;
            windowHandler.ShowPopup(popupAbortGame);
        }
        else
            windowHandler.ResetTo(ScreenId.Zoggen);
    }

    public void OnClickOptions()
    {
        if (windowHandler == null)
            return;
        isPaused = true;
        windowHandler.GoTo(ScreenId.Optionen);
    }

    public void OnClickAbortCancel()
    {
        if (windowHandler != null){
            isPaused = false;
            windowHandler.HidePopup();
            if (game != null)
                HandleBotCheck(game.GetCurrentPlayerId());
        }
    }

    private void InitPlayerList()
    {
        if (game == null)
            return;

        if (appHandler == null)
            appHandler = FindAnyObjectByType<AppHandler>();

        if (playerList == null)
            playerList = FindAnyObjectByType<CricketPlayerLayoutController>();

        if (playerList == null || appHandler == null)
        {
            Debug.LogError("CricketGameEngine.InitPlayerList: Kein CricketPlayerLayoutController oder AppHandler gefunden.");
            return;
        }

        var players = game.GetPlayerIDs()
            .Select(id => appHandler.GetPlayerByID(id))
            .Where(p => p != null)
            .ToList();

        playerList.Init(players);
    }

    private void HandleTurnCompleted(CricketTurnResult result)
    {
        // (Legacy) penalties are decommissioned; Customs fire over
        // OnCustomPenaltyTriggered (HandleCustomPenaltyAudio), Specials
        // (e.g. White Horse) via OnCustomAudioEvent — both before this
        // Hook. Order here: Marks number, points, then Marks event.
        if (result.Hits.Count > 0)
        {
            // Marks: announce number, then points, then Marks event (Custom, Seed cricket_marks).
            int totalMarks = result.Hits.Sum(h => h.EffectiveMarks);
            audioManager.PlayScore(totalMarks);

            if (result.TurnScore > 0)
                audioManager.PlayScore(result.TurnScore);

            PlayManualAudioEvent(SeededAudioEvents.MarksId);
        }

        // Nothing at all
        else
        {
            audioManager.PlayScore(0);
        }
            



        RefreshPlayerList();
    }

    private void HandleLegWon(Guid playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Leg gewonnen");

        StopBotTurn();

        playerList.RefreshUI();

        NotifyWin(WinLevel.Leg, playerId);

        HandleBotCheck(game.GetCurrentPlayerId());
    }

    private void HandleSetWon(Guid? playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Set gewonnen");

        StopBotTurn();

        playerList.RefreshUI();

        if (playerId.HasValue && playerId.Value != Guid.Empty)
            NotifyWin(WinLevel.Set, playerId.Value);

        HandleBotCheck(game.GetCurrentPlayerId());
    }


    protected override IEnumerator HandleBotTurn(DartBot bot)
    {
        // Lock input
        inputHandler.SetInputEnabled(false);

        for (int i = 0; i < 3; i++)
        {
            yield return new WaitUntil(() => !isPaused);

            // If Turn has already ended (e.g. by Bust or Win)
            var currentTurn = game.GetCurrentTurnOfPlayer(bot.GetID());
            if (currentTurn == null || !currentTurn.HasSpace() || game.IsFinished())
            {
                Debug.Log("Bust oder Turnende erkannt, keine weiteren Bot-Würfe");
                yield break;
            }

            Throw t = GetNextCricketBotThrow(bot);
            yield return inputHandler.PlayBotThrow(t, () => AddThrow(t));

            var updatedTurn = game.GetCurrentTurnOfPlayer(bot.GetID());
            bool canThrowAgain = i < 2
                && updatedTurn != null
                && updatedTurn.HasSpace()
                && !game.IsFinished();

            if (!canThrowAgain)
                yield break;
        }
    }

    private Throw GetNextCricketBotThrow(DartBot bot)
    {
        return bot.GetNextCricketThrow(CreateBotContext(bot.GetID()));
    }

    private CricketBotContext CreateBotContext(Guid playerId)
    {
        var settings = ((CricketGame) game).GetSettingsAsCricket();

        return new CricketBotContext
        {
            PlayerId = playerId,
            Numbers = GetNumbers().ToList(),
            PlayerScores = ((CricketGame) game).GetPlayerScores(),
            PlayerHits = ((CricketGame) game).GetPlayerHits(),
            PlayerIds = game.GetPlayerIDs(),

            PointsEnabled = settings == null || settings.pointsEnabled,
            CutThroat = settings != null && settings.cutThroatEnabled
        };
    }

    private void RefreshPlayerList()
    {
        if (playerList != null)
            playerList.RefreshUI();
    }

    protected override void UnsubscribeFromGameEvents()
    {
        if (game == null) return;

        game.OnLegWon -= HandleLegWon;
        game.OnSetWon -= HandleSetWon;
        game.OnMatchWon -= HandleMatchWon;
        game.OnCustomPenaltyTriggered -= HandleCustomPenaltyAudio;
        game.OnCustomAudioEvent -= HandleCustomAudioEvent;
        ((CricketGame) game).OnTurnCompleted -= HandleTurnCompleted;
        game.OnBotCheck -= HandleBotCheck;
    }
}



public class CricketBotContext
{
    public Guid PlayerId;

    public List<int> Numbers;

    public Dictionary<Guid, int> PlayerScores;
    public Dictionary<Guid, Dictionary<int, int>> PlayerHits;

    public bool PointsEnabled;
    public bool CutThroat;

    public List<Guid> PlayerIds;
}
