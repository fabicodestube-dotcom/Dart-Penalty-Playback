using System;
using UnityEngine;

public static class DartThrowSimulator
{
    private const float SingleSigmaMm = 13f;
    private const float SingleFocus = 0.95f;
    private const float TripleFocus = 0.95f;
    private const float DoubleFocus = 0.87f;
    private const float BullInnerSigmaMm = 7.5f;
    private const float BullOuterSigmaMm = 9f;

    private const float TripleTargetRadiusMm = 103f;
    private const float DoubleTargetRadiusMm = 165.5f;
    private const float SingleTargetRadiusMm = 134.5f;
    private const float OuterBullTargetRadiusMm = 11.125f;

    private const float DoubleCovXx = 49.6025f;
    private const float DoubleCovXy = -0.4631f;
    private const float DoubleCovYy = 69.1665f;

    private const float TripleCovXx = 77.1141f;
    private const float TripleCovXy = 19.2138f;
    private const float TripleCovYy = 36.7390f;

    private static readonly System.Random SharedRandom = new System.Random();

    public static Throw Simulate(float skill, int targetValue, DartMultiplier targetMultiplier, float formFactor = 1f, System.Random random = null)
    {
        if (random == null)
            random = SharedRandom;

        if (targetValue <= 0)
            return new Throw(DartMultiplier.Single, 0, HitType.Wall, false, targetValue, targetMultiplier: targetMultiplier);

        GetTargetPoint(targetValue, targetMultiplier, out float targetX, out float targetY);
        GetCovariance(targetValue, targetMultiplier, out float covXx, out float covXy, out float covYy, out float focus);

        float spread = GetSkillSpread(BotSkillScale.Normalize(skill))
            * Mathf.Clamp(formFactor, 0.75f, 1.4f)
            * focus;

        float scaledXx = covXx * spread * spread;
        float scaledXy = covXy * spread * spread;
        float scaledYy = covYy * spread * spread;

        float l11 = Mathf.Sqrt(Mathf.Max(0.0001f, scaledXx));
        float l21 = scaledXy / l11;
        float l22 = Mathf.Sqrt(Mathf.Max(0.0001f, scaledYy - l21 * l21));

        float z1 = NextGaussian(random);
        float z2 = NextGaussian(random);

        float landingX = targetX + l11 * z1;
        float landingY = targetY + l21 * z1 + l22 * z2;

        DartboardGeometry.PointZone zone = DartboardGeometry.ResolvePoint(landingX, landingY);
        MapZone(zone, out int value, out DartMultiplier multiplier, out HitType hitType);

        bool isTargetHit = hitType == HitType.Board
            && value == targetValue
            && multiplier == targetMultiplier;

        float inverseBoardRadius = 1f / DartboardGeometry.BoardRadiusMm;

        return new Throw(
            multiplier,
            value,
            hitType,
            isTargetHit,
            targetValue,
            targetMultiplier: targetMultiplier,
            landingX: landingX * inverseBoardRadius,
            landingY: landingY * inverseBoardRadius,
            targetX: targetX * inverseBoardRadius,
            targetY: targetY * inverseBoardRadius,
            hasLandingPoint: true,
            hasTargetPoint: true);
    }

    public static void GetTargetPoint(int value, DartMultiplier multiplier, out float x, out float y)
    {
        if (value == 25)
        {
            x = 0f;
            y = multiplier == DartMultiplier.Double ? 0f : OuterBullTargetRadiusMm;
            return;
        }

        float radius = multiplier switch
        {
            DartMultiplier.Triple => TripleTargetRadiusMm,
            DartMultiplier.Double => DoubleTargetRadiusMm,
            _ => SingleTargetRadiusMm
        };

        float angle = DartboardGeometry.GetSegmentCenterAngle(value) * Mathf.Deg2Rad;
        x = Mathf.Sin(angle) * radius;
        y = Mathf.Cos(angle) * radius;
    }

    public static float GetSkillSpread(float skill)
    {
        float t = Mathf.Clamp01((BotSkillScale.Max - skill) / (BotSkillScale.Max - BotSkillScale.Min));
        return 1f + 1.84815f * t - 3.16163f * t * t + 4.60f * t * t * t;
    }

    private static void GetCovariance(int value, DartMultiplier multiplier, out float xx, out float xy, out float yy, out float focus)
    {
        xy = 0f;

        if (value == 25)
        {
            float sigma = multiplier == DartMultiplier.Double ? BullInnerSigmaMm : BullOuterSigmaMm;
            xx = sigma * sigma;
            yy = xx;
            focus = 1f;
            return;
        }

        if (multiplier == DartMultiplier.Triple)
        {
            xx = TripleCovXx;
            xy = TripleCovXy;
            yy = TripleCovYy;
            focus = TripleFocus;
            return;
        }

        if (multiplier == DartMultiplier.Double)
        {
            xx = DoubleCovXx;
            xy = DoubleCovXy;
            yy = DoubleCovYy;
            focus = DoubleFocus;
            return;
        }

        xx = SingleSigmaMm * SingleSigmaMm;
        yy = xx;
        focus = SingleFocus;
    }

    private static void MapZone(DartboardGeometry.PointZone zone, out int value, out DartMultiplier multiplier, out HitType hitType)
    {
        hitType = HitType.Board;

        switch (zone.kind)
        {
            case DartboardGeometry.PointZoneKind.OffBoard:
                value = 0;
                multiplier = DartMultiplier.Single;
                hitType = HitType.Wall;
                break;

            case DartboardGeometry.PointZoneKind.BoardMiss:
                value = 0;
                multiplier = DartMultiplier.Single;
                break;

            case DartboardGeometry.PointZoneKind.Bull:
                value = 25;
                multiplier = DartMultiplier.Double;
                break;

            case DartboardGeometry.PointZoneKind.OuterBull:
                value = 25;
                multiplier = DartMultiplier.Single;
                break;

            case DartboardGeometry.PointZoneKind.Double:
                value = zone.number;
                multiplier = DartMultiplier.Double;
                break;

            case DartboardGeometry.PointZoneKind.Triple:
                value = zone.number;
                multiplier = DartMultiplier.Triple;
                break;

            default:
                value = zone.number;
                multiplier = DartMultiplier.Single;
                break;
        }
    }

    private static float NextGaussian(System.Random random)
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = random.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}
