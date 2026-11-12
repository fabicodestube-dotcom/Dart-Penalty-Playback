using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;

[System.Serializable]
public class Set
{
    // =========================================================
    // STATE
    // =========================================================

    [JsonProperty] private List<Leg> legs = new List<Leg>();
    [JsonProperty] public Guid? WinnerPlayerId { get; private set; } = null;


    // =========================================================
    // LEG MANAGEMENT
    // =========================================================

    public void AddLeg(Leg leg)
    {
        legs.Add(leg);
    }

    /// <summary>
    /// Returns the current leg.
    /// If there is no leg yet, one is automatically created.
    /// </summary>
    public Leg GetCurrentLeg()
    {
        // Lazy initialization: first leg is generated when needed
        if (legs.Count == 0)
        {
            var leg = new Leg();
            legs.Add(leg);
        }

        // Return of the last element (current leg)
        return legs[^1];
    }

    public List<Leg> GetLegs() => legs;

    public void SetWinner(Guid playerId)
    {
        WinnerPlayerId = playerId;
    }
    public void ClearWinner()
    {
        WinnerPlayerId = null;
    }
}