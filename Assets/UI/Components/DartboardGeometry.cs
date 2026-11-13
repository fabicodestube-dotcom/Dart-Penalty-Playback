using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Official dartboard geometry + mapping of HitSector keys on board zones.
/// Everything normalized: Board radius (double outside) = 1.
/// Real dimensions (mm, diameter): Bull 12.7 / Outside Bull 31.8 / Triple inside 198 /
/// Triple outside 214 / Double inside 324 / Double outside 340.
/// </summary>
public static class DartboardGeometry
{
    /// <summary>Number order clockwise, 20 is above (12 o'clock)</summary>
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

    public const float BoardRadiusMm = 170f;
    public const float BoardSurfaceOuterMm = 214f;
    public const float BullInnerMm = 6.35f;
    public const float BullOuterMm = 15.9f;
    public const float TripleInnerMm = 99f;
    public const float TripleOuterMm = 107f;
    public const float DoubleInnerMm = 162f;
    public const float DoubleOuterMm = 170f;

    public enum PointZoneKind
    {
        OffBoard,
        BoardMiss,
        Single,
        Double,
        Triple,
        OuterBull,
        Bull
    }

    public struct PointZone
    {
        public PointZoneKind kind;
        public int number;
    }

    public static PointZone ResolvePoint(float xMm, float yMm)
    {
        float radius = Mathf.Sqrt(xMm * xMm + yMm * yMm);

        if (radius > BoardSurfaceOuterMm)
            return new PointZone { kind = PointZoneKind.OffBoard, number = 0 };

        if (radius > DoubleOuterMm)
            return new PointZone { kind = PointZoneKind.BoardMiss, number = 0 };

        if (radius <= BullInnerMm)
            return new PointZone { kind = PointZoneKind.Bull, number = 25 };

        if (radius <= BullOuterMm)
            return new PointZone { kind = PointZoneKind.OuterBull, number = 25 };

        float angle = Mathf.Atan2(xMm, yMm) * Mathf.Rad2Deg;
        angle = Mathf.Repeat(angle, 360f);

        int index = Mathf.RoundToInt(angle / SegmentAngle) % NumberOrder.Length;
        int number = NumberOrder[index];

        if (radius < TripleInnerMm)
            return new PointZone { kind = PointZoneKind.Single, number = number };

        if (radius <= TripleOuterMm)
            return new PointZone { kind = PointZoneKind.Triple, number = number };

        if (radius < DoubleInnerMm)
            return new PointZone { kind = PointZoneKind.Single, number = number };

        return new PointZone { kind = PointZoneKind.Double, number = number };
    }

    /// <summary>Center of the segment for a number in degrees (0 = above, clockwise).</summary>
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
        OuterSingle, // "n" (same key as InnerSingle)
        Double       // "Dn"
    }

    public struct Zone
    {
        public string sectorKey; // HitSector-Key ("20", "D20", "T20", "25", "D25")
        public int number;       // 1.20, 25 for Bull
        public ZoneKind kind;
    }

    /// <summary>
    /// All 82 zones: Bull + Outer Bull + 20 numbers x 4 rings.
    /// Inner + External Single Consciously Share a Key
    /// (Data model combines both in "n").
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

    /// <summary>Base color of a zone in the classic board color scheme.</summary>
    public static UnityEngine.Color GetBaseColor(Zone zone, int segmentIndex)
    {
        bool isEven = segmentIndex % 2 == 0;

        switch (zone.kind)
        {
            case ZoneKind.Bull:
                return new UnityEngine.Color(0.78f, 0.12f, 0.12f); // rot
            case ZoneKind.OuterBull:
                return new UnityEngine.Color(0.10f, 0.45f, 0.18f); // Green
            case ZoneKind.InnerSingle:
            case ZoneKind.OuterSingle:
                return isEven
                    ? new UnityEngine.Color(0.08f, 0.08f, 0.08f)    // Black
                    : new UnityEngine.Color(0.92f, 0.85f, 0.70f);   // creme
            case ZoneKind.Triple:
            case ZoneKind.Double:
                return isEven
                    ? new UnityEngine.Color(0.78f, 0.12f, 0.12f)    // rot
                    : new UnityEngine.Color(0.10f, 0.45f, 0.18f);   // Green
            default:
                return UnityEngine.Color.gray;
        }
    }

    /// <summary>Segment index (0..19) of a number for the chessboard color scheme.</summary>
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
