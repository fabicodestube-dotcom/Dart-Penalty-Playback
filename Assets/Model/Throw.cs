using Newtonsoft.Json;

[System.Serializable]
public class Throw
{
    // =========================================================
    // Immutable state (Throw is deliberately unchangeable by construction)
    // =========================================================

    [JsonProperty] public DartMultiplier Multiplier { get; }
    [JsonProperty] public int Value { get; }
    [JsonProperty] public HitType HitType { get; }
    [JsonProperty] public bool IsTargetHit { get; }
    [JsonProperty] public int TargetValue { get; }
    [JsonProperty] public DartMultiplier TargetMultiplier { get; }

    [JsonProperty] public float LandingX { get; }
    [JsonProperty] public float LandingY { get; }
    [JsonProperty] public float TargetX { get; }
    [JsonProperty] public float TargetY { get; }
    [JsonProperty] public bool HasLandingPoint { get; }
    [JsonProperty] public bool HasTargetPoint { get; }

    /// <summary>
    /// sector ID of a custom hit area (only set at HitType.Custom),
    /// Value/multiplier determines the score (0 = miss).
    /// </summary>
    [JsonProperty] public string CustomHitId { get; }


    // =========================================================
    // CONSTRUCTION
    // =========================================================

    public Throw(
        DartMultiplier multiplier,
        int value,
        HitType hitType = HitType.Board,
        bool isTargetHit = false,
        int targetValue = -1,
        string customHitId = null,
        DartMultiplier targetMultiplier = DartMultiplier.Single,
        float landingX = 0f,
        float landingY = 0f,
        float targetX = 0f,
        float targetY = 0f,
        bool hasLandingPoint = false,
        bool hasTargetPoint = false)
    {
        Multiplier = multiplier;
        Value = value;
        HitType = hitType;
        IsTargetHit = isTargetHit;
        TargetValue = targetValue;
        TargetMultiplier = targetMultiplier;
        CustomHitId = hitType == HitType.Custom ? customHitId : null;
        LandingX = landingX;
        LandingY = landingY;
        TargetX = targetX;
        TargetY = targetY;
        HasLandingPoint = hasLandingPoint;
        HasTargetPoint = hasTargetPoint;
    }


    // =========================================================
    // SCORING LOGIC
    // =========================================================

    /// <summary>
    /// Calculate the score of this throw.
    /// Important: Only valid board hits count, everything else = 0.
    /// </summary>
    public int GetScore() => 
        (HitType != HitType.Board && HitType != HitType.Custom)
            ? 0 
            : Multiplier switch
            {
                DartMultiplier.Single => Value,
                DartMultiplier.Double => Value * 2,
                DartMultiplier.Triple => Value * 3,
                _ => 0
            };


    // =========================================================
    // CONVENIENCE CHECKS
    // =========================================================

    /// <summary>
    /// Check if the litter could be a double finish.
    /// </summary>
    public bool IsDouble() => 
        (HitType == HitType.Board || HitType == HitType.Custom) && Multiplier == DartMultiplier.Double;

    /// <summary>
    /// Check if the litter could be a triple finish.
    /// </summary>
    public bool IsTriple() => 
        (HitType == HitType.Board || HitType == HitType.Custom) && Multiplier == DartMultiplier.Triple;
}