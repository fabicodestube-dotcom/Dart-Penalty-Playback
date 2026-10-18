using System.Collections.Generic;

/// <summary>
/// Offizielle Dartscheiben-Geometrie + Mapping von HitSector-Keys auf Board-Zonen.
/// Alles normiert: Board-Radius (Double außen) = 1.
/// Reale Maße (mm, Durchmesser): Bull 12.7 / Außen-Bull 31.8 / Triple innen 198 /
/// Triple außen 214 / Double innen 324 / Double außen 340.
/// </summary>
public static class DartboardGeometry
{
    /// <summary>Zahlenreihenfolge im Uhrzeigersinn, 20 steht oben (12 Uhr).</summary>
    public static readonly int[] NumberOrder =
    {
        20, 1, 18, 4, 13, 6, 10, 15, 2, 17,
        3, 19, 7, 16, 8, 11, 14, 9, 12, 5
    };

    public const float BullInnerRadius = 0.037f;   // 12.7 / 340
    public const float BullOuterRadius = 0.094f;   // 31.8 / 340
    public const float TripleInnerRadius = 0.582f; // 198 / 340
    public const float TripleOuterRadius = 0.629f; // 214 / 340
    public const float DoubleInnerRadius = 0.953f; // 324 / 340
    public const float DoubleOuterRadius = 1.0f;

    public const float SegmentAngle = 18f;

    /// <summary>Mitte des Segments für eine Zahl in Grad (0 = oben, Uhrzeigersinn).</summary>
    public static float GetSegmentCenterAngle(int number)
    {
        for (int i = 0; i < NumberOrder.Length; i++)
        {
            if (NumberOrder[i] == number)
                return i * SegmentAngle;
        }
        return 0f;
    }

    public enum ZoneKind
    {
        Bull,        // D25
        OuterBull,   // 25
        InnerSingle, // "n"
        Triple,      // "Tn"
        OuterSingle, // "n" (gleicher Key wie InnerSingle)
        Double       // "Dn"
    }

    public struct Zone
    {
        public string sectorKey; // HitSector-Key ("20", "D20", "T20", "25", "D25")
        public int number;       // 1..20, 25 für Bull
        public ZoneKind kind;
    }

    /// <summary>
    /// Alle 82 Zonen: Bull + Außen-Bull + 20 Zahlen x 4 Ringe.
    /// Inneres + äußeres Single teilen sich bewusst einen Key
    /// (Datenmodell fasst beide in "n" zusammen).
    /// </summary>
    public static List<Zone> BuildZones()
    {
        var zones = new List<Zone>(82)
        {
            new Zone { sectorKey = "D25", number = 25, kind = ZoneKind.Bull },
            new Zone { sectorKey = "25", number = 25, kind = ZoneKind.OuterBull }
        };

        for (int n = 1; n <= 20; n++)
        {
            string s = n.ToString();
            zones.Add(new Zone { sectorKey = s, number = n, kind = ZoneKind.InnerSingle });
            zones.Add(new Zone { sectorKey = "T" + n, number = n, kind = ZoneKind.Triple });
            zones.Add(new Zone { sectorKey = s, number = n, kind = ZoneKind.OuterSingle });
            zones.Add(new Zone { sectorKey = "D" + n, number = n, kind = ZoneKind.Double });
        }

        return zones;
    }

    /// <summary>Basisfarbe einer Zone im klassischen Board-Farbschema.</summary>
    public static UnityEngine.Color GetBaseColor(Zone zone, int segmentIndex)
    {
        bool isEven = segmentIndex % 2 == 0;

        switch (zone.kind)
        {
            case ZoneKind.Bull:
                return new UnityEngine.Color(0.78f, 0.12f, 0.12f); // rot
            case ZoneKind.OuterBull:
                return new UnityEngine.Color(0.10f, 0.45f, 0.18f); // grün
            case ZoneKind.InnerSingle:
            case ZoneKind.OuterSingle:
                return isEven
                    ? new UnityEngine.Color(0.08f, 0.08f, 0.08f)    // schwarz
                    : new UnityEngine.Color(0.92f, 0.85f, 0.70f);   // creme
            case ZoneKind.Triple:
            case ZoneKind.Double:
                return isEven
                    ? new UnityEngine.Color(0.78f, 0.12f, 0.12f)    // rot
                    : new UnityEngine.Color(0.10f, 0.45f, 0.18f);   // grün
            default:
                return UnityEngine.Color.gray;
        }
    }

    /// <summary>Segment-Index (0..19) einer Zahl für das Schachbrett-Farbschema.</summary>
    public static int GetSegmentIndex(int number)
    {
        for (int i = 0; i < NumberOrder.Length; i++)
        {
            if (NumberOrder[i] == number)
                return i;
        }
        return 0;
    }
}
