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

    // Maximum 3 darts per turn (classic X01 behavior)
    [JsonProperty] private List<Throw> throws = new List<Throw>(3);

    [JsonProperty] public bool IsBust { get; private set; } = false;

    // Snapshot of the score before the turn (e.g. for UI / Undo / Validation)
    [JsonProperty] public int ScoreBeforeTurn { get; set; }

    // Snapshot of ALL Player Scores at Turn Start (Cricket: Cutthroat Checks)
    // Opponent totals; only changed totals can have been newly reached.
    // null on old saves -> Fallback on legacy behavior.
    [JsonProperty] public Dictionary<Guid, int> TurnStartScores { get; set; }

    // Exactly the turn penalties that were booked at the finalize of this turn.
    // And then, they will return the boots to the boots.
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
        // Hard Limit: maximum 3 throws per turn
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
    /// Calculates the total score of the turn.
    /// Optionally, a bust can be ignored (e.g. for UI display of other players).
    /// </summary>
    public int GetTurnScore(bool includeBustDarts = true)
    {
        if (!includeBustDarts && IsBust)
            return 0; // At Bust, the turn is counted as 0 (only visually/logically separated)

        return throws.Sum(t => t.GetScore());
    }


    // =========================================================
    // BUST HANDLING
    // =========================================================

    public void MarkAsBust() => IsBust = true;

    public void ClearBust() => IsBust = false;
}

/// <summary>
/// A turn penalty booked at the turn finalize (ThreeOnes / AllMiss /repdigit).
/// It is exactly taken back at Undo.
/// </summary>
[System.Serializable]
public struct TurnPenaltyRecord
{
    [JsonProperty] public Guid PlayerId;
    [JsonProperty] public PenaltyType Type;

    /// <summary>
    /// Custom penalty (Tab 2): def ID + booked costs. null/0 = built-in.
    /// Instant=true: booked at the throw (Legacy HitTypeMiss ), otherwise at the finalize.
    /// </summary>
    [JsonProperty] public string CustomPenaltyId;
    [JsonProperty] public float CustomCost;
    [JsonProperty] public bool CustomInstant;
}