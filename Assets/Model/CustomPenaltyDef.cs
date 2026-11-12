using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Conditional type of a (custom) penalty. HitTypeMiss / RepdigitScore are
/// legacy (old data + evaluation); the editor offers TurnPattern (2.1),
/// ExactScore (2.2) and Leg/Set/GameLost (2.3)
/// </summary>
public enum PenaltyConditionKind
{
    /// <summary>Single miss throw (wall/ceiling) Instantly checked at AddThrow. (Legacy)</summary>
    HitTypeMiss,
    /// <summary>Exact turn point count (points event).</summary>
    ExactScore,
    /// <summary>
    /// Exact rest score after the turn (X01 rest, cricket total).
    /// With transition rule: fire only when the total is newly reached.
    /// Replaces the Legacy repdigit (Seeds 111–999).
    /// </summary>
    RemainingExactScore,
    /// <summary>Bnaps number: Turn score is Repdigit (11, 22,..., 111, 222,...). (Legacy)</summary>
    RepdigitScore,
    /// <summary>Certain rolls within a turn (e.g. 3x1).</summary>
    TurnPattern,
    /// <summary>Leg lost (costs are borne by all but Leg winners).</summary>
    LegLost,
    /// <summary>Set lost (costs are borne by all but Set winners).</summary>
    SetLost,
    /// <summary>Game lost (costs are all but match winners).</summary>
    GameLost
}

/// <summary>
/// A slot of a turn pattern. anyValue / anyMultiplier / anyHitType = Wildcard.
/// </summary>
[System.Serializable]
public class TurnPatternEntry
{
    [JsonProperty] public bool anyValue = true;
    [JsonProperty] public int value;
    [JsonProperty] public bool anyMultiplier = true;
    [JsonProperty] public DartMultiplier multiplier = DartMultiplier.Single;
    [JsonProperty] public bool anyHitType = true;
    [JsonProperty] public HitType hitType = HitType.Board;

    /// <summary>
    /// Sector ID at hitType == Custom (Tab 1 “Areas”). Empty = everyone
    /// Custom sector. Only evaluated if anyHitType is false.
    /// </summary>
    [JsonProperty] public string customHitId;

    /// <summary>
    /// True = Wall-or-Ceiling (for AllMiss slots).
    /// </summary>
    [JsonProperty] public bool anyMissHitType;
}

/// <summary>
/// Evaluable condition of a penalty.
/// </summary>
[System.Serializable]
public class PenaltyCondition
{
    [JsonProperty] public PenaltyConditionKind kind = PenaltyConditionKind.HitTypeMiss;

    // HitTypeMiss
    [JsonProperty] public HitType hitType = HitType.Wall;

    /// <summary>
    /// Sector ID at hitType == Custom (Tab 1 “Areas”). Empty = everyone
    /// Custom sector. Wall/Ceiling use their fixed HitTypes.
    /// </summary>
    [JsonProperty] public string customHitId;

    // ExactScore
    [JsonProperty] public int score;

    // RepdigitScore (minimum score, default 11)
    [JsonProperty] public int minScore = 11;

    // TurnPattern
    [JsonProperty] public List<TurnPatternEntry> pattern = new List<TurnPatternEntry>();

    /// <summary>
    /// Single order toggle (2.1): true = ordered (slots in this
    /// Order in the turn), false = disordered (pattern as multi-set in the
    /// Turn. Additional throws are allowed in both cases. Replace that
    /// old matchExact (old data without matchOrder behave like false).
    /// </summary>
    [JsonProperty] public bool matchOrder = false;
}

/// <summary>
/// Custom penalty (stable string ID, so audio clips are used for
/// stay linked when editing). Built-ins remain the PenaltyType enum.
/// </summary>
[System.Serializable]
public class CustomPenaltyDef
{
    [JsonProperty] public string id;
    [JsonProperty] public string name = "New penalty";

    /// <summary>
    /// Optional short name for narrow columns of tables (e.g. "Floor")
    /// instead of "ExtraLangerPenaltyName..."). Empty = display name (if applicable).
    /// </summary>
    [JsonProperty] public string shortName;

    /// <summary>
    /// Generated English description of the condition (e.g. "Reach exact
    /// score: 420"). Set on seed/save and persisted for the row info text.
    /// </summary>
    [JsonProperty] public string info;

    [JsonProperty] public bool enabled = true;

    /// <summary>
    /// True = Seed (not erasable, but normally evaluated/editable).
    /// Penalty seeds are erasable (false); event/sector seeds use the
    /// Flag for extinguishing protection.
    /// </summary>
    [JsonProperty] public bool builtIn;

    /// <summary>
    /// Cost. Edited/stored here; the billing logic
    /// (Application in the game) follows separately.
    /// </summary>
    [JsonProperty] public int cost = 10;

    [JsonProperty] public List<PenaltyCondition> conditions = new List<PenaltyCondition>();

    /// <summary>"builtin:wall" → PenaltyType.Wall (for PenaltySettings -Sync and Enum-Clip-Buttons).</summary>
    public static bool TryParseBuiltInType(string id, out PenaltyType type)
    {
        type = default;

        if (string.IsNullOrEmpty(id) || !id.StartsWith("builtin:"))
            return false;

        foreach (PenaltyType candidate in System.Enum.GetValues(typeof(PenaltyType)))
        {
            if (string.Equals(candidate.ToString(), id.Substring(8),
                    System.StringComparison.OrdinalIgnoreCase))
            {
                type = candidate;
                return true;
            }
        }

        return false;
    }

    public static CustomPenaltyDef CreateNew()
    {
        return new CustomPenaltyDef
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = "New penalty",
            enabled = true,
            cost = 10,
            conditions = new List<PenaltyCondition>
            {
                new PenaltyCondition
                {
                    kind = PenaltyConditionKind.TurnPattern,
                    matchOrder = false,
                    pattern = new List<TurnPatternEntry>
                    {
                        new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true },
                        new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true },
                        new TurnPatternEntry { anyValue = false, value = 1, anyMultiplier = true }
                    }
                }
            }
        };
    }

    /// <summary>True for LegLost / SetLost / GameLost (evaluation at leg/set/game ending).</summary>
    public static bool IsGameEventKind(PenaltyConditionKind kind)
    {
        return kind == PenaltyConditionKind.LegLost
            || kind == PenaltyConditionKind.SetLost
            || kind == PenaltyConditionKind.GameLost;
    }
}
