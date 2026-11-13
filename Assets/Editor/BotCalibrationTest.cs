using System;
using UnityEditor;
using UnityEngine;

public static class BotCalibrationTest
{
    [MenuItem("Tools/Bot/Run Throw Calibration")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        CheckGeometry();
        CheckCheckoutRoutes();
        CheckIntentionalBust();
        CheckBoardProjection();
        CheckSkillTenRates();
        CheckAverageCurve();

        Debug.Log("[BOT-CAL] PASS");
    }

    private static void CheckGeometry()
    {
        Check(DartboardGeometry.ResolvePoint(0f, 171f).kind == DartboardGeometry.PointZoneKind.BoardMiss, "171 mm must be a board miss.");
        Check(DartboardGeometry.ResolvePoint(0f, 213f).kind == DartboardGeometry.PointZoneKind.BoardMiss, "213 mm must be a board miss.");
        Check(DartboardGeometry.ResolvePoint(0f, 215f).kind == DartboardGeometry.PointZoneKind.OffBoard, "215 mm must be off board.");
        Check(DartboardGeometry.ResolvePoint(0f, 220f).kind == DartboardGeometry.PointZoneKind.OffBoard, "220 mm must be off board.");
        Check(DartboardGeometry.ResolvePoint(0f, 166f).kind == DartboardGeometry.PointZoneKind.Double, "166 mm must be a double.");
        Check(DartboardGeometry.ResolvePoint(0f, 166f).number == 20, "166 mm must be D20.");
        Check(DartboardGeometry.ResolvePoint(0f, 103f).kind == DartboardGeometry.PointZoneKind.Triple, "103 mm must be a triple.");
        Check(DartboardGeometry.ResolvePoint(0f, 103f).number == 20, "103 mm must be T20.");
        Check(DartboardGeometry.ResolvePoint(0f, 134.5f).kind == DartboardGeometry.PointZoneKind.Single, "134.5 mm must be a single.");
        Check(DartboardGeometry.ResolvePoint(0f, 134.5f).number == 20, "134.5 mm must be S20.");
        Check(DartboardGeometry.ResolvePoint(0f, 0f).kind == DartboardGeometry.PointZoneKind.Bull, "Centre must be bull.");
        Check(DartboardGeometry.ResolvePoint(0f, 11f).kind == DartboardGeometry.PointZoneKind.OuterBull, "11 mm must be outer bull.");

        Debug.Log("[BOT-CAL] Geometry OK");
    }

    private static void CheckCheckoutRoutes()
    {
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Single, 1, 1) == "1", "Single 1 checkout must be S1.");
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Double, 1, 1) == null, "Double 1 must not have a checkout.");
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Triple, 60, 1) == "T20", "Triple 60 checkout must be T20.");
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Master, 60, 1) == "T20", "Master 60 checkout must be T20.");
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Master, 50, 1) == "D25", "Master 50 checkout must be D25.");
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Double, 50, 1) == "D25", "Double 50 checkout must be D25.");
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Triple, 3, 1) == "T1", "Triple 3 checkout must be T1.");
        Check(CheckoutDatabase.GetCheckout(CheckoutType.Master, 2, 1) == "D1", "Master 2 checkout must be D1.");
        Check(!string.IsNullOrEmpty(CheckoutDatabase.GetCheckout(CheckoutType.Master, 110, 2)), "Master 110 two-dart checkout is missing.");
        Check(!string.IsNullOrEmpty(CheckoutDatabase.GetCheckout(CheckoutType.Double, 170, 3)), "Double 170 three-dart checkout is missing.");

        Debug.Log("[BOT-CAL] Checkout routes OK");
    }

    private static void CheckIntentionalBust()
    {
        CheckBotTarget(3, 16, 1, 20, DartMultiplier.Triple, "Last-dart bust from 3 to a better double");
        CheckBotTarget(2, 16, 1, 1, DartMultiplier.Double, "Direct double finish must not be busted");
        CheckBotTarget(10, 16, 1, 5, DartMultiplier.Double, "Direct D5 finish must not be busted");
        CheckBotTarget(51, 16, 1, 11, DartMultiplier.Single, "D20 leave must beat a D8 reset");
        CheckBotTarget(39, 16, 1, 7, DartMultiplier.Single, "D16 leave must beat a D8 reset");
        CheckBotTarget(51, 51, 1, 11, DartMultiplier.Single, "One dart left must still use the three-dart checkout");
        CheckBotTarget(3, 16, 2, 1, DartMultiplier.Single, "Intentional bust only on the last dart");

        Debug.Log("[BOT-CAL] Intentional bust OK");
    }

    private static void CheckBotTarget(
        int currentScore,
        int turnStartScore,
        int dartsLeft,
        int expectedValue,
        DartMultiplier expectedMultiplier,
        string message)
    {
        var bot = new DartBot(Guid.NewGuid(), "Decision", 10f, 1);
        Throw throwData = bot.GetNextX01Throw(
            currentScore,
            CheckoutType.Double,
            dartsLeft,
            true,
            CheckinType.StraightIn,
            turnStartScore);

        Check(throwData.TargetValue == expectedValue
            && throwData.TargetMultiplier == expectedMultiplier,
            $"{message}: expected {expectedValue}/{expectedMultiplier}, got {throwData.TargetValue}/{throwData.TargetMultiplier}");
    }

    private static void CheckBoardProjection()
    {
        CheckProjection(20, DartMultiplier.Triple, 103f, 0.4335f);
        CheckProjection(20, DartMultiplier.Double, 165.5f, 0.7875f);
        CheckProjection(20, DartMultiplier.Single, 134.5f, 0.618f);
        CheckProjection(25, DartMultiplier.Single, 11.125f, 0.07f);
        CheckProjection(25, DartMultiplier.Double, 0f, 0f);

        CheckProjectedRadius(99f, 0.409f);
        CheckProjectedRadius(107f, 0.456f);
        CheckProjectedRadius(162f, 0.749f);
        CheckProjectedRadius(170f, 0.796f);

        Debug.Log("[BOT-CAL] Board projection OK");
    }

    private static void CheckProjection(int value, DartMultiplier multiplier, float radiusMm, float expected)
    {
        float projected = BotTurnPanel.PhysicalRadiusToVisualFraction(radiusMm);
        float ring = BotTurnPanel.GetVisualRingFraction(value, multiplier);

        Check(Mathf.Abs(projected - expected) < 0.0005f,
            $"{value}/{multiplier} projection mismatch: {projected:0.0000} != {expected:0.0000}");
        Check(Mathf.Abs(ring - expected) < 0.0005f,
            $"{value}/{multiplier} ring fraction mismatch: {ring:0.0000} != {expected:0.0000}");
    }

    private static void CheckProjectedRadius(float radiusMm, float expected)
    {
        float projected = BotTurnPanel.PhysicalRadiusToVisualFraction(radiusMm);

        Check(Mathf.Abs(projected - expected) < 0.0005f,
            $"Physical {radiusMm} mm projection mismatch: {projected:0.0000} != {expected:0.0000}");
    }

    private static void CheckSkillTenRates()
    {
        var random = new System.Random(12345);

        var d20 = SimulateCounts(20, DartMultiplier.Double, 10f, 200000, random);
        Check(d20.hit > 0.38f && d20.hit < 0.46f, $"D20 hit rate out of range: {d20.hit:0.000}");
        Check(d20.sameSingle > 0.26f && d20.sameSingle < 0.35f, $"D20 single rate out of range: {d20.sameSingle:0.000}");
        Check(d20.boardMiss > 0.23f && d20.boardMiss < 0.32f, $"D20 board-miss rate out of range: {d20.boardMiss:0.000}");
        Check(d20.wall < 0.01f, $"D20 wall rate out of range: {d20.wall:0.000}");

        var t20 = SimulateCounts(20, DartMultiplier.Triple, 10f, 200000, random);
        Check(t20.hit > 0.45f && t20.hit < 0.53f, $"T20 hit rate out of range: {t20.hit:0.000}");
        Check(t20.boardMiss < 0.01f, $"T20 board-miss rate out of range: {t20.boardMiss:0.000}");
        Check(t20.wall < 0.01f, $"T20 wall rate out of range: {t20.wall:0.000}");

        var s20 = SimulateCounts(20, DartMultiplier.Single, 10f, 200000, random);
        Check(s20.hit > 0.85f && s20.hit < 0.93f, $"S20 hit rate out of range: {s20.hit:0.000}");
        Check(s20.wall < 0.01f, $"S20 wall rate out of range: {s20.wall:0.000}");

        var doubles = SimulateAllDoubles(10f, 20000, random);
        Check(doubles.hit > 0.38f && doubles.hit < 0.47f, $"All-doubles hit rate out of range: {doubles.hit:0.000}");
        Check(doubles.boardMiss > 0.22f && doubles.boardMiss < 0.33f, $"All-doubles board-miss rate out of range: {doubles.boardMiss:0.000}");
        Check(doubles.wall < 0.01f, $"All-doubles wall rate out of range: {doubles.wall:0.000}");

        Debug.Log($"[BOT-CAL] Skill 10: D20 {d20.hit:0.000}/{d20.sameSingle:0.000}/{d20.boardMiss:0.000}/{d20.wall:0.000}, T20 {t20.hit:0.000}/{t20.boardMiss:0.000}/{t20.wall:0.000}, S20 {s20.hit:0.000}/{s20.wall:0.000}, Doubles {doubles.hit:0.000}/{doubles.boardMiss:0.000}/{doubles.wall:0.000}");
    }

    private static void CheckAverageCurve()
    {
        float average1 = SimulateLegAverage(1f, 120, new System.Random(101));
        float average5 = SimulateLegAverage(5f, 120, new System.Random(505));
        float average10 = SimulateLegAverage(10f, 120, new System.Random(1010));

        Check(average1 > 22f && average1 < 40f, $"Skill 1 average out of range: {average1:0.0}");
        Check(average5 > 50f && average5 < 75f, $"Skill 5 average out of range: {average5:0.0}");
        Check(average10 > 90f && average10 < 112f, $"Skill 10 average out of range: {average10:0.0}");
        Check(average1 < average5 && average5 < average10, "Average must increase with skill.");

        Debug.Log($"[BOT-CAL] Leg averages: skill 1 = {average1:0.0}, skill 5 = {average5:0.0}, skill 10 = {average10:0.0}");
    }

    private static Counts SimulateCounts(int value, DartMultiplier multiplier, float skill, int iterations, System.Random random)
    {
        int hit = 0;
        int sameSingle = 0;
        int boardMiss = 0;
        int wall = 0;

        for (int i = 0; i < iterations; i++)
        {
            Throw throwData = DartThrowSimulator.Simulate(skill, value, multiplier, 1f, random);

            if (throwData.IsTargetHit)
                hit++;

            if (throwData.Value == value && throwData.Multiplier == DartMultiplier.Single)
                sameSingle++;

            if (throwData.HitType == HitType.Board && throwData.Value == 0)
                boardMiss++;

            if (throwData.HitType == HitType.Wall)
                wall++;
        }

        return new Counts
        {
            hit = hit / (float)iterations,
            sameSingle = sameSingle / (float)iterations,
            boardMiss = boardMiss / (float)iterations,
            wall = wall / (float)iterations
        };
    }

    private static Counts SimulateAllDoubles(float skill, int iterationsPerTarget, System.Random random)
    {
        long hit = 0;
        long boardMiss = 0;
        long wall = 0;
        int total = 0;

        for (int value = 1; value <= 20; value++)
        {
            for (int i = 0; i < iterationsPerTarget; i++)
            {
                Throw throwData = DartThrowSimulator.Simulate(skill, value, DartMultiplier.Double, 1f, random);

                if (throwData.IsTargetHit)
                    hit++;

                if (throwData.HitType == HitType.Board && throwData.Value == 0)
                    boardMiss++;

                if (throwData.HitType == HitType.Wall)
                    wall++;

                total++;
            }
        }

        return new Counts
        {
            hit = hit / (float)total,
            boardMiss = boardMiss / (float)total,
            wall = wall / (float)total
        };
    }

    private static float SimulateLegAverage(float skill, int legs, System.Random random)
    {
        int totalDarts = 0;

        for (int leg = 0; leg < legs; leg++)
        {
            var bot = new DartBot(Guid.NewGuid(), "Calibration", skill, 1);
            int score = 501;
            int dartsInLeg = 0;
            int turnStart = score;
            int throwsThisTurn = 0;

            while (score > 0 && dartsInLeg < 300)
            {
                if (throwsThisTurn == 0)
                    turnStart = score;

                Throw throwData = bot.GetNextX01Throw(
                    score,
                    CheckoutType.Double,
                    3 - throwsThisTurn,
                    true,
                    CheckinType.StraightIn,
                    turnStart);

                dartsInLeg++;
                throwsThisTurn++;

                int newScore = score - throwData.GetScore();
                bool bust = newScore < 0 || newScore == 1 || (newScore == 0 && !throwData.IsDouble());

                if (bust)
                {
                    score = turnStart;
                    throwsThisTurn = 0;
                    continue;
                }

                score = newScore;

                if (score == 0)
                    break;

                if (throwsThisTurn == 3)
                    throwsThisTurn = 0;
            }

            totalDarts += dartsInLeg;
        }

        return 501f * 3f * legs / totalDarts;
    }

    private struct Counts
    {
        public float hit;
        public float sameSingle;
        public float boardMiss;
        public float wall;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("[BOT-CAL] " + message);
    }
}
