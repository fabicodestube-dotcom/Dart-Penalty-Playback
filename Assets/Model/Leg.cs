using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;

[System.Serializable]
public class Leg
{
    // =========================================================
    // STATE
    // =========================================================

    [JsonProperty] private List<Turn> turns = new List<Turn>();

    [JsonProperty] public Guid WinnerPlayerId { get; private set; }


    // =========================================================
    // WINNER HANDLING
    // =========================================================

    public void SetWinner(Guid playerId)
    {
        WinnerPlayerId = playerId;
    }

    public void ClearWinner()
    {
        WinnerPlayerId = Guid.Empty;
    }


    // =========================================================
    // TURN MANAGEMENT
    // =========================================================

    public void AddTurn(Turn t)
    {
        turns.Add(t);
    }

    /// <summary>
    /// Returns the last turn or null if no turns exist.
    /// Uses index-from-end operator (^1).
    /// </summary>
    public Turn GetLastTurn()
    {
        return turns.Count > 0 ? turns[^1] : null;
    }

    public List<Turn> GetTurns()
    {
        return turns;
    }
}