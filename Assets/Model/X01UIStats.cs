using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class X01UIStats
{
    [JsonProperty] public bool isActive;
    [JsonProperty] public Guid playerID;
    [JsonProperty] public string playerName;
    [JsonProperty] public int currentScore;
    [JsonProperty] public List<Throw> lastThreeThrows;
    [JsonProperty] public bool isBust;
    [JsonProperty] public int turnScore;
    [JsonProperty] public int wonSets;
    [JsonProperty] public int wonLegs;
    [JsonProperty] public int thrownDartsCount;
    [JsonProperty] public float averageScorePerTurn;
    

    public X01UIStats(X01Game game, BasePlayer player)
    {
        this.playerID = player.GetID();
        this.playerName = player.GetName();
        isActive = IsActive(game, playerID);
        
        currentScore = game.GetScore(playerID);

        lastThreeThrows = GetDisplayThrows(game, game.GetCurrentPlayerId() == playerID);

        isBust = IsBust(game);

        turnScore = CalculateTurnScore(lastThreeThrows);

        wonSets = game.GetWonSets(playerID);

        wonLegs = game.GetWonLegs(playerID);

        thrownDartsCount = game.GetDartsInCurrentLeg(playerID);

        averageScorePerTurn = AvgPointsPerTurn(game);
    }

    private bool IsActive(X01Game game, Guid playerID)
    {
        return game.GetCurrentPlayerId() == playerID;
    }

    private List<Throw> GetDisplayThrows(X01Game game, bool isActivePlayer)
    {
        Turn turn = isActivePlayer
            ? game.GetCurrentTurnOfPlayer(playerID)
            : game.GetLastTurnInCurrentLeg(playerID);

        return turn?.GetThrows() ?? new List<Throw>();
    }

    private int CalculateTurnScore(List<Throw> throws)
    {
        int sum = 0;

        if (throws != null)
        {
            foreach (Throw t in throws)
            {
                if (t != null)
                {
                    sum += t.GetScore();
                }
            }
        }
        return sum;
    }

    private float AvgPointsPerTurn(X01Game game)
    {
        var finishedTurns = game.GetAllTurns(playerID).Where(t => t.IsCompleted()).ToList();
        if (finishedTurns.Count == 0) 
            return 0f;
        return (float)Math.Round(finishedTurns.Sum(t => t.GetTurnScore()) / (float)finishedTurns.Count, 2);
    }

    private bool IsBust(X01Game game)
    {
        Turn turn = isActive 
        ? game.GetCurrentTurnOfPlayer(playerID) 
        : game.GetLastTurnForPlayer(playerID);

        return turn != null && turn.IsBust;
    }
}