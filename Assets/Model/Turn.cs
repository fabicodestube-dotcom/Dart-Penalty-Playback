using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

[System.Serializable]
public class Turn
{
    // =========================================================
    // CORE STATE
    // =========================================================
    

    [JsonProperty] public int CheckinThrowIndex = -1;
    [JsonProperty] public Guid PlayerId { get; }

    // Maximal 3 Darts pro Turn (klassisches X01 Verhalten)
    [JsonProperty] private List<Throw> throws = new List<Throw>(3);

    [JsonProperty] public bool IsBust { get; private set; } = false;

    // Snapshot des Scores vor dem Turn (z.B. für UI / Undo / Validation)
    [JsonProperty] public int ScoreBeforeTurn { get; set; }


    // =========================================================
    // CONSTRUCTION
    // =========================================================

    [JsonConstructor] public Turn(Guid playerId)
    {
        PlayerId = playerId;
    }

    public int GetThrowCount() => throws.Count;

    // =========================================================
    // THROW MANAGEMENT
    // =========================================================

    public void AddThrow(Throw t)
    {
        // Hard Limit: maximal 3 Würfe pro Turn
        if (throws.Count < 3)
            throws.Add(t);
    }

    public Throw RemoveLastThrow()
    {
        if (throws.Count == 0)
            return null;

        var t = throws[^1];
        throws.RemoveAt(throws.Count - 1);
        return t;
    }

    public List<Throw> GetThrows() => throws;


    // =========================================================
    // TURN STATE CHECKS
    // =========================================================

    public bool HasSpace() => throws.Count < 3;

    public bool IsCompleted() => throws.Count == 3 && !IsBust;


    // =========================================================
    // SCORING
    // =========================================================

    /// <summary>
    /// Berechnet den Gesamt-Score des Turns.
    /// Optional kann ein Bust ignoriert werden (z.B. für UI Anzeige anderer Spieler).
    /// </summary>
    public int GetTurnScore(bool includeBustDarts = true)
    {
        if (!includeBustDarts && IsBust)
            return 0; // Bei Bust wird der Turn als 0 gewertet (nur visuell/logisch getrennt)

        return throws.Sum(t => t.GetScore());
    }


    // =========================================================
    // BUST HANDLING
    // =========================================================

    public void MarkAsBust() => IsBust = true;

    public void ClearBust() => IsBust = false;
}