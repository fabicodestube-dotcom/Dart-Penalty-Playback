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
    /// Gibt das aktuelle Leg zurück.
    /// Falls noch kein Leg existiert, wird automatisch eines erstellt.
    /// </summary>
    public Leg GetCurrentLeg()
    {
        // Lazy initialization: erstes Leg wird bei Bedarf erzeugt
        if (legs.Count == 0)
        {
            var leg = new Leg();
            legs.Add(leg);
        }

        // Rückgabe des letzten Elements (aktuelles Leg)
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