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

    // Snapshot ALLER Spieler-Scores bei Turn-Start (Cricket: Cutthroat prüft
    // Gegner-Totals; nur veränderte Totals können neu erreicht worden sein).
    // Null bei alten Saves -> Fallback auf Legacy-Verhalten.
    [JsonProperty] public Dictionary<Guid, int> TurnStartScores { get; set; }

    // Exakt die Turn-Penalties, die beim Finalize dieses Turns gebucht wurden.
    // Undo nimmt nur diese zurück (kein Doppelzählen bei Nachwurf).
    [JsonProperty] public List<TurnPenaltyRecord> RecordedPenalties { get; set; }


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

/// <summary>
/// Ein beim Turn-Finalize gebuchtes Turn-Penalty (ThreeOnes/AllMiss/Schnapszahl).
/// Wird beim Undo exakt zurückgenommen.
/// </summary>
[System.Serializable]
public struct TurnPenaltyRecord
{
    [JsonProperty] public Guid PlayerId;
    [JsonProperty] public PenaltyType Type;

    /// <summary>
    /// Custom-Penalty (Tab 2): Def-ID + gebuchte Kosten. Null/0 = Built-in.
    /// Instant=true: beim Wurf gebucht (Legacy HitTypeMiss), sonst beim Finalize.
    /// </summary>
    [JsonProperty] public string CustomPenaltyId;
    [JsonProperty] public float CustomCost;
    [JsonProperty] public bool CustomInstant;
}