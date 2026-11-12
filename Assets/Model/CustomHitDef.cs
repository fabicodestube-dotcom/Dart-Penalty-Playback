using Newtonsoft.Json;

/// <summary>
/// Hit area from Tab 1 ("Areas"): miss throw variant next to Wall/Ceiling.
/// Board hits are hardwired and do not appear here. Custom sectors
/// can have a dart value and multiplier (count then like board hits).
/// Value 0 / Multiplier Single = Miss (0 points).
/// Pattern references and audio clips remain linked when editing.
/// Seeds: "builtin:wall" (wall), "builtin:ceiling" (ceiling).
/// </summary>
[System.Serializable]
public class CustomHitDef
{
    public const string WallId = "builtin:wall";
    public const string CeilingId = "builtin:ceiling";

    [JsonProperty] public string id;
    [JsonProperty] public string name = "New area";
    [JsonProperty] public bool enabled = true;

    /// <summary>darts value of the sector (0-20, 25). 0 = miss (no points).</summary>
    [JsonProperty] public int value = 0;

    /// <summary>Multiplier of the sector (Single/Double/Triple).</summary>
    [JsonProperty] public DartMultiplier multiplier = DartMultiplier.Single;

    /// <summary>
    /// Generated English description of the setting (function for built-in
    /// seeds, settings summary for custom entries). Set on seed/save and
    /// persisted so the row can show it without recomputation.
    /// </summary>
    [JsonProperty] public string info;

    /// <summary>
    /// True = Seed (not erasable, but normally editable).
    /// Wall/Ceiling seeds are built-in and therefore not erasable.
    /// </summary>
    [JsonProperty] public bool builtIn;

    public static CustomHitDef CreateNew()
    {
        return new CustomHitDef
        {
            id = System.Guid.NewGuid().ToString("N"),
            name = "New area",
            enabled = true,
            builtIn = false
        };
    }

    /// <summary>True for the two seed areas wall/ceiling.</summary>
    public static bool IsBuiltInSector(string id)
    {
        return string.Equals(id, WallId, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, CeilingId, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Deliver the fixed HitType of a seed (Wall/Ceiling), otherwise custom.</summary>
    public static HitType HitTypeForSector(string id)
    {
        if (string.Equals(id, WallId, System.StringComparison.OrdinalIgnoreCase))
            return HitType.Wall;
        if (string.Equals(id, CeilingId, System.StringComparison.OrdinalIgnoreCase))
            return HitType.Ceiling;
        return HitType.Custom;
    }

    /// <summary>
    /// Stable audio/sector ID of a roll. Board rolls deliver null.
    /// Wall/Ceiling are mapped to their seed IDs, free sectors
    /// keep their own ID.
    /// </summary>
    public static string GetSectorId(Throw throwItem)
    {
        if (throwItem == null || throwItem.HitType == HitType.Board)
            return null;

        return throwItem.HitType switch
        {
            HitType.Wall => WallId,
            HitType.Ceiling => CeilingId,
            HitType.Custom => throwItem.CustomHitId,
            _ => null
        };
    }
}
