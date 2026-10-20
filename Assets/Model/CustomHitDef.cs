using Newtonsoft.Json;

/// <summary>
/// Treffer-Bereich aus Tab 1 („Bereiche“): Fehlwurf-Variante neben Wall/Ceiling.
/// Board-Treffer sind fest verdrahtet und erscheinen hier nicht. Custom-Sektoren
/// haben immer Value 0 / Multiplier Single (0 Punkte). Stabile String-ID, damit
/// Pattern-Referenzen und Audio-Clips beim Editieren verlinkt bleiben.
/// Seeds: "builtin:wall" (Wand), "builtin:ceiling" (Decke).
/// </summary>
[System.Serializable]
public class CustomHitDef
{
    [JsonProperty] public string id;
    [JsonProperty] public string name = "New area";
    [JsonProperty] public bool enabled = true;

    /// <summary>
    /// Generated English description of the setting (function for built-in
    /// seeds, settings summary for custom entries). Set on seed/save and
    /// persisted so the row can show it without recomputation.
    /// </summary>
    [JsonProperty] public string info;

    /// <summary>
    /// True = Seed (nicht löschbar, aber normal editierbar). Die
    /// Wall-/Ceiling-Seeds sind löschbar (false).
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

    /// <summary>True für die beiden Seed-Bereiche Wand/Decke.</summary>
    public static bool IsBuiltInSector(string id)
    {
        return string.Equals(id, "builtin:wall", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "builtin:ceiling", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Liefert den festen HitType eines Seeds (Wall/Ceiling), sonst Custom.</summary>
    public static HitType HitTypeForSector(string id)
    {
        if (string.Equals(id, "builtin:wall", System.StringComparison.OrdinalIgnoreCase))
            return HitType.Wall;
        if (string.Equals(id, "builtin:ceiling", System.StringComparison.OrdinalIgnoreCase))
            return HitType.Ceiling;
        return HitType.Custom;
    }
}
