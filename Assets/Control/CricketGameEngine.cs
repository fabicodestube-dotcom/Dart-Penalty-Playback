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
        game.OnPenaltyTriggered += HandlePenalty;
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
        audioManager.PlaySpecialClip(SpecialAudioType.Undo);
        RefreshPlayerList();
        HandleUndoBotState();
    }

    private void HandlePenalty(Guid playerId, PenaltyType type, Turn turn)
    {
        if (playerList != null)
            playerList.RefreshUI();

        if (audioManager == null || game == null)
            return;

        audioManager.PlayPenalty(type);
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
            windowHandler.GoTo(ScreenId.Zoggen);
    }

    public void OnClickOptions()
    {
        if (windowHandler == null)
            return;
        isPaused = true;
        windowHandler.GoTo(ScreenId.Optionen);
    }

    public void OnClickAbortCancle()
    {
        if (windowHandler != null){
            isPaused = false;
            windowHandler.HidePopup();
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
        // Penalties
        foreach (var p in result.Penalties)
        {
            audioManager.PlayPenalty(p);
        }

        // Marks
        if (result.Hits.Count > 0)
        {
            int totalMarks = result.Hits.Sum(h => h.EffectiveMarks);
            audioManager.PlayScore(totalMarks);
            audioManager.PlaySpecialClip(SpecialAudioType.Marks);

            // Score
            if (result.TurnScore > 0)
                audioManager.PlayScore(result.TurnScore);
        }

        // Gar nix
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

        HandleBotCheck(game.GetCurrentPlayerId());
    }

    private void HandleSetWon(Guid? playerId)
    {
        Debug.Log($"[ENGINE] Spieler {playerId} hat ein Set gewonnen");

        StopBotTurn();

        playerList.RefreshUI();

        HandleBotCheck(game.GetCurrentPlayerId());
    }


    protected override IEnumerator HandleBotTurn(DartBot bot)
    {
        // Input sperren
        inputHandler.SetInputEnabled(false);

        yield return new WaitForSeconds(2f);

        for (int i = 0; i < 3; i++)
        {
            //yield return new WaitUntil(() => !isPaused && (!botPausePending || pauseConfirmed));
            yield return new WaitUntil(() => !isPaused);

            // Falls Turn schon beendet (z.B. durch Bust oder Win)
            var currentTurn = game.GetCurrentTurnOfPlayer(bot.GetID());
            if (!currentTurn.HasSpace() || game.IsFinished())
            {
                Debug.Log("Bust oder Turnende erkannt, keine weiteren Bot-Würfe");
                yield break;
            }

            Throw t = bot.GetNextCricketThrow(CreateBotContext(bot.GetID()));
            AddThrow(t);

            yield return new WaitForSeconds(2f);
        }
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
        game.OnPenaltyTriggered -= HandlePenalty;
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
