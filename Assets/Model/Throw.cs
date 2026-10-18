using Newtonsoft.Json;

[System.Serializable]
public class Throw
{
    // =========================================================
    // IMMUTABLE STATE (Throw ist bewusst unveränderlich nach Konstruktion)
    // =========================================================

    [JsonProperty] public DartMultiplier Multiplier { get; }
    [JsonProperty] public int Value { get; }
    [JsonProperty] public HitType HitType { get; }
    [JsonProperty] public bool IsTargetHit { get; }
    [JsonProperty] public int TargetValue { get; }

    /// <summary>
    /// Sektor-ID eines Custom-Treffer-Bereichs (nur bei HitType.Custom gesetzt,
    /// sonst null). Custom-Sektoren haben immer Value 0 / Multiplier Single.
    /// </summary>
    [JsonProperty] public string CustomHitId { get; }


    // =========================================================
    // CONSTRUCTION
    // =========================================================

    public Throw(DartMultiplier multiplier, int value, HitType hitType = HitType.Board, bool isTargetHit = false, int targetValue = -1, string customHitId = null)
    {
        Multiplier = multiplier;
        Value = value;
        HitType = hitType;
        IsTargetHit = isTargetHit;
        TargetValue = targetValue;
        CustomHitId = hitType == HitType.Custom ? customHitId : null;
    }


    // =========================================================
    // SCORING LOGIC
    // =========================================================

    /// <summary>
    /// Berechnet den Score dieses Wurfs.
    /// Wichtig: Nur gültige Board-Treffer zählen, alles andere = 0.
    /// </summary>
    public int GetScore() => 
        HitType != HitType.Board 
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
    /// Prüft, ob der Wurf ein Double-Finish sein könnte.
    /// </summary>
    public bool IsDouble() => 
        HitType == HitType.Board && Multiplier == DartMultiplier.Double;

    /// <summary>
    /// Prüft, ob der Wurf ein Triple-Finish sein könnte.
    /// </summary>
    public bool IsTriple() => 
        HitType == HitType.Board && Multiplier == DartMultiplier.Triple;
}