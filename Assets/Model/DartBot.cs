using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine;

[System.Serializable]
public class DartBot : BasePlayer
{
    [JsonProperty] private DartBotDifficulty difficulty;
    [JsonProperty] private readonly int number;

    private int streak = 0;

    private float confidence = 0f;

    private int dartsThrown = 0;


    private static readonly int[] BoardCircle = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

    private readonly struct BotSkillProfile
    {
        public readonly float BaseAccuracy;
        public readonly float SingleWrongRingChance;
        public readonly float SingleWrongRingTripleShare;
        public readonly float SingleMissChance;
        public readonly float DoubleSuccessChance;
        public readonly float DoubleWrongRingChance;
        public readonly float DoubleOutsideMissChance;
        public readonly float DoubleWrongRingTripleShare;
        public readonly float TripleSuccessChance;
        public readonly float TripleWrongRingChance;
        public readonly float TripleWrongRingDoubleShare;
        public readonly float TripleOutsideMissChance;
        // neighbor-related probabilities separated:
        public readonly float NeighborNumberChance;   // chance a miss lands on neighbor number vs random board
        public readonly float NeighborTripleChance;   // when neighbor, chance the neighbor ring is Triple (for triple-target misses)
        public readonly float NeighborDoubleChance;   // when neighbor, chance the neighbor ring is Double (for double-target misses)
        public readonly float SingleBullSingleChance;
        public readonly float SingleBullDoubleChance;
        public readonly float DoubleBullSingleChance;
        public readonly float DoubleBullDoubleChance;

        public BotSkillProfile(
            float baseAccuracy,
            float singleWrongRingChance,
            float singleWrongRingTripleShare,
            float singleMissChance,
            float doubleSuccessChance,
            float doubleWrongRingChance,
            float doubleWrongRingTripleShare,
            float doubleOutsideMissChance,
            float tripleSuccessChance,
            float tripleWrongRingChance,
            float tripleWrongRingDoubleShare,
            float tripleOutsideMissChance,
            float neighborNumberChance,
            float neighborTripleChance,
            float neighborDoubleChance,
            float singleBullSingleChance,
            float singleBullDoubleChance,
            float doubleBullSingleChance,
            float doubleBullDoubleChance)
        {
            BaseAccuracy = baseAccuracy;
            SingleWrongRingChance = singleWrongRingChance;
            SingleWrongRingTripleShare = singleWrongRingTripleShare;
            SingleMissChance = singleMissChance;
            DoubleSuccessChance = doubleSuccessChance;
            DoubleWrongRingChance = doubleWrongRingChance;
            DoubleWrongRingTripleShare = doubleWrongRingTripleShare;
            DoubleOutsideMissChance = doubleOutsideMissChance;
            TripleSuccessChance = tripleSuccessChance;
            TripleWrongRingChance = tripleWrongRingChance;
            TripleWrongRingDoubleShare = tripleWrongRingDoubleShare;
            TripleOutsideMissChance = tripleOutsideMissChance;
            NeighborNumberChance = neighborNumberChance;
            NeighborTripleChance = neighborTripleChance;
            NeighborDoubleChance = neighborDoubleChance;
            SingleBullSingleChance = singleBullSingleChance;
            SingleBullDoubleChance = singleBullDoubleChance;
            DoubleBullSingleChance = doubleBullSingleChance;
            DoubleBullDoubleChance = doubleBullDoubleChance;
        }
    }

    private static readonly BotSkillProfile[] SkillProfiles =
    {
        // Easy (Anfänger / Kneipenspieler)
        new BotSkillProfile(
            baseAccuracy: 0.35f,
            singleWrongRingChance: 0.15f,
            singleWrongRingTripleShare: 0.20f,
            singleMissChance: 0.20f,

            doubleSuccessChance: 0.15f,
            doubleWrongRingChance: 0.70f,
            doubleWrongRingTripleShare: 0.05f,
            doubleOutsideMissChance: 0.35f,

            tripleSuccessChance: 0.10f,
            tripleWrongRingChance: 0.80f,
            tripleWrongRingDoubleShare: 0.05f,
            tripleOutsideMissChance: 0.20f,

            neighborNumberChance: 0.40f,
            neighborTripleChance: 0.02f,
            neighborDoubleChance: 0.05f,

            singleBullSingleChance: 0.15f,
            singleBullDoubleChance: 0.02f,
            doubleBullSingleChance: 0.10f,
            doubleBullDoubleChance: 0.05f),

        // Medium (Fortgeschrittener Hobbyspieler)
        new BotSkillProfile(
            baseAccuracy: 0.55f,
            singleWrongRingChance: 0.08f,
            singleWrongRingTripleShare: 0.30f,
            singleMissChance: 0.08f,

            doubleSuccessChance: 0.30f,
            doubleWrongRingChance: 0.55f,
            doubleWrongRingTripleShare: 0.10f,
            doubleOutsideMissChance: 0.20f,

            tripleSuccessChance: 0.25f,
            tripleWrongRingChance: 0.65f,
            tripleWrongRingDoubleShare: 0.10f,
            tripleOutsideMissChance: 0.10f,

            neighborNumberChance: 0.70f,
            neighborTripleChance: 0.08f,
            neighborDoubleChance: 0.12f,

            singleBullSingleChance: 0.35f,
            singleBullDoubleChance: 0.10f,
            doubleBullSingleChance: 0.30f,
            doubleBullDoubleChance: 0.20f),

        // Hard (Lokaler Ligaspieler)
        new BotSkillProfile(
            baseAccuracy: 0.75f,
            singleWrongRingChance: 0.04f,
            singleWrongRingTripleShare: 0.40f,
            singleMissChance: 0.02f,

            doubleSuccessChance: 0.45f,
            doubleWrongRingChance: 0.45f,
            doubleWrongRingTripleShare: 0.15f,
            doubleOutsideMissChance: 0.10f,

            tripleSuccessChance: 0.42f,
            tripleWrongRingChance: 0.53f,
            tripleWrongRingDoubleShare: 0.15f,
            tripleOutsideMissChance: 0.05f,

            neighborNumberChance: 0.90f,
            neighborTripleChance: 0.20f,
            neighborDoubleChance: 0.25f,

            singleBullSingleChance: 0.60f,
            singleBullDoubleChance: 0.15f,
            doubleBullSingleChance: 0.45f,
            doubleBullDoubleChance: 0.40f),

        // Pro (PDC Profi-Niveau)
        new BotSkillProfile(
            baseAccuracy: 0.92f,
            singleWrongRingChance: 0.015f,
            singleWrongRingTripleShare: 0.50f,
            singleMissChance: 0.002f,

            doubleSuccessChance: 0.60f,
            doubleWrongRingChance: 0.38f,
            doubleWrongRingTripleShare: 0.20f,
            doubleOutsideMissChance: 0.02f,

            tripleSuccessChance: 0.55f,
            tripleWrongRingChance: 0.43f,
            tripleWrongRingDoubleShare: 0.20f,
            tripleOutsideMissChance: 0.02f,

            neighborNumberChance: 0.98f,
            neighborTripleChance: 0.35f,
            neighborDoubleChance: 0.40f,

            singleBullSingleChance: 0.75f,
            singleBullDoubleChance: 0.20f,
            doubleBullSingleChance: 0.40f,
            doubleBullDoubleChance: 0.55f)
    };

    public DartBot(Guid id, string playerName, DartBotDifficulty difficulty, int number) : base(id, playerName)
    {
        this.difficulty = difficulty;
        this.number = number;
    }

    public string GetNameWithDifficulty() => $"{GetName()} ({difficulty})";

    public int GetNumber()
    {
        return number;
    }

    public DartBotDifficulty GetDifficulty() => difficulty;

    // =========================================================
    // WAHRSCHEINLICHKEITEN
    // =========================================================

    private static BotSkillProfile GetSkillProfile(DartBotDifficulty difficulty)
    {
        return SkillProfiles[(int)difficulty];
    }

    private BotSkillProfile GetSkillProfile()
    {
        return GetSkillProfile(difficulty);
    }

    private float GetNumberAccuracy()
    {
        return GetSkillProfile().BaseAccuracy;
    }

    private float GetCurrentAccuracy()
    {
        float accuracy = GetNumberAccuracy();

        accuracy += streak * 0.01f;
        accuracy += confidence * 0.005f;
        accuracy -= GetFatiguePenalty();

        return Mathf.Clamp01(accuracy);
    }

    private void UpdateForm(Throw dart)
    {
        if (dart.HitType == HitType.Board)
        {
            streak++;
            confidence += 0.2f;
        }
        else
        {
            streak--;
            confidence -= 0.3f;
        }

        streak = Mathf.Clamp(streak, -5, 5);
        confidence = Mathf.Clamp(confidence, -5f, 5f);
    }

    private float GetFatiguePenalty()
    {
        if (dartsThrown < 150)
            return 0f;

        if (dartsThrown < 300)
            return 0.01f;

        return 0.025f;
    }

    private float GetTripleChance()
    {
        return GetSkillProfile().TripleSuccessChance;
    }

    private float GetDoubleChance()
    {
        return GetSkillProfile().DoubleSuccessChance;
    }

    private float GetNeighborChance()
    {
        return GetSkillProfile().NeighborNumberChance;
    }

    private DartMultiplier GetWrongRingMultiplierForSingle()
    {
        var profile = GetSkillProfile();
        return UnityEngine.Random.value < profile.SingleWrongRingTripleShare
            ? DartMultiplier.Triple
            : DartMultiplier.Double;
    }

    private DartMultiplier GetWrongRingMultiplierForDouble()
    {
        var profile = GetSkillProfile();
        return UnityEngine.Random.value < profile.DoubleWrongRingTripleShare
            ? DartMultiplier.Triple
            : DartMultiplier.Single;
    }

    private DartMultiplier GetWrongRingMultiplierForTriple()
    {
        var profile = GetSkillProfile();
        return UnityEngine.Random.value < profile.TripleWrongRingDoubleShare
            ? DartMultiplier.Double
            : DartMultiplier.Single;
    }

    // =========================================================
    // X01
    // =========================================================

    public Throw GetNextX01Throw(int currentScore, CheckoutType checkoutType)
    {
        var target = DetermineX01Target(currentScore, checkoutType);
        return SimulatePhysicalThrow(target);
    }

    private (int value, DartMultiplier multiplier) DetermineX01Target(int score, CheckoutType checkoutType)
    {
        // 👉 Wie viele Darts sind noch im Turn?
        // (Du hast das aktuell NICHT im Bot → brauchst du!)
        int dartsLeft = 3; // TODO: vom Game übergeben wäre besser

        string checkout = CheckoutDatabase.GetCheckout(checkoutType, score, dartsLeft);

        if (!string.IsNullOrEmpty(checkout))
        {
            var parts = checkout.Split(' ');

            // 👉 Ersten Dart der Route nehmen
            var first = ParseDart(parts[0]);

            return first;
        }

        // Fallback (dein bisheriges Verhalten)
        return (difficulty == DartBotDifficulty.Easy)
            ? (20, DartMultiplier.Single)
            : (20, DartMultiplier.Triple);
    }

    // Using Checkout Database to determine the best target based on current score and checkout type
    private (int val, DartMultiplier mult) ParseDart(string dart)
    {
        // Nur Zahl -> Single
        if (int.TryParse(dart, out int value))
        {
            if (value == 50)
                return (25, DartMultiplier.Double);

            if (value == 25)
                return (25, DartMultiplier.Single);

            return (value, DartMultiplier.Single);
        }

        if (dart.StartsWith("T"))
            return (int.Parse(dart.Substring(1)), DartMultiplier.Triple);

        if (dart.StartsWith("D"))
            return (int.Parse(dart.Substring(1)), DartMultiplier.Double);

        if (dart.StartsWith("S"))
            return (int.Parse(dart.Substring(1)), DartMultiplier.Single);

        if (dart.Equals("Bull", StringComparison.OrdinalIgnoreCase))
            return (25, DartMultiplier.Single);

        // Fallback
        return (1, DartMultiplier.Single);
    }


    // =========================================================
    // CRICKET
    // =========================================================

    public Throw GetNextCricketThrow(CricketBotContext ctx)
    {
        Guid myId = ctx.PlayerId;

        var openNumbers = ctx.Numbers.Where(n => ctx.PlayerHits[myId][n] < 3).ToList();

        var targets = ctx.Numbers
            .OrderByDescending(n => {
                if (openNumbers.Count == 1 && openNumbers.Contains(n)) return 100f;

                return n switch {
                    25 => 17.5f,
                    _ => (float)n
                };
            })
            .ToList();

        foreach (int number in targets)
        {
            int myHits = ctx.PlayerHits[myId][number];
            bool iHaveClosed = myHits >= 3;

            if (!iHaveClosed)
            {
                if (3 - myHits == 1 && difficulty <= DartBotDifficulty.Medium)
                    return SimulatePhysicalThrow((number, DartMultiplier.Single));

                return SimulatePhysicalThrow((number, number == 25 ? DartMultiplier.Double : DartMultiplier.Triple));
            }

            if (ctx.PointsEnabled)
            {
                bool opponentOpen = ctx.PlayerIds.Where(id => id != myId).Any(id => ctx.PlayerHits[id][number] < 3);

                if (opponentOpen)
                {
                    bool shouldScore = false;
                    int myScore = ctx.PlayerScores[myId];

                    if (ctx.CutThroat)
                    {
                        int minScore = ctx.PlayerScores.Values.Min();
                        if (myScore > minScore || difficulty == DartBotDifficulty.Pro) shouldScore = true;
                    }
                    else
                    {
                        int maxScore = ctx.PlayerScores.Values.Max();
                        if (myScore < maxScore || difficulty == DartBotDifficulty.Pro) shouldScore = true;
                    }

                    if (shouldScore)
                        return SimulatePhysicalThrow((number, number == 25 ? DartMultiplier.Double : DartMultiplier.Triple));
                }
            }
        }

        return SimulatePhysicalThrow((20, DartMultiplier.Single));
    }

    // =========================================================
    // ATC
    // =========================================================

    public Throw GetNextATCThrow(int currentTarget, ATCTargetType targetType, bool hasStarted)
    {
        (int val, DartMultiplier mult) targetParams;

        switch (targetType)
        {
            case ATCTargetType.Singles:
                targetParams = (currentTarget, DartMultiplier.Single);
                break;
            case ATCTargetType.Doubles:
                targetParams = (currentTarget, DartMultiplier.Double);
                break;
            case ATCTargetType.Triples:
                targetParams = (currentTarget, DartMultiplier.Triple);
                break;
            default:
                targetParams = (20, DartMultiplier.Single);
                break;
        }

        Throw result = SimulatePhysicalThrow(targetParams);

        bool isActuallyHit = result.Value == targetParams.val;

        if (targetParams.mult == DartMultiplier.Double && result.Multiplier != DartMultiplier.Double) isActuallyHit = false;
        if (targetParams.mult == DartMultiplier.Triple && result.Multiplier != DartMultiplier.Triple) isActuallyHit = false;

        return new Throw(result.Multiplier, result.Value, result.HitType, isActuallyHit, targetParams.val);
    }

    // =========================================================
    // 🎯 PHYSICS
    // =========================================================

    private Throw SimulatePhysicalThrow((int val, DartMultiplier mult) target)
    {
        if (target.val == 25)
            return SimulateBullThrow(target);

        return target.mult switch
        {
            DartMultiplier.Single => SimulateSingleThrow(target),
            DartMultiplier.Double => SimulateDoubleThrow(target),
            DartMultiplier.Triple => SimulateTripleThrow(target),
            _ => SimulateSingleThrow(target)
        };
    }

    private Throw SimulateSingleThrow((int val, DartMultiplier mult) target)
    {
        var profile = GetSkillProfile();
        bool hitNumber = UnityEngine.Random.value < GetCurrentAccuracy();

        if (hitNumber)
        {
            float ringRoll = UnityEngine.Random.value;
            float goodSingleChance = 1f - profile.SingleWrongRingChance - profile.SingleMissChance;

            if (ringRoll < goodSingleChance)
                return CreateThrow(target.val, DartMultiplier.Single, target);

            if (ringRoll < goodSingleChance + profile.SingleWrongRingChance)
                return CreateThrow(target.val, GetWrongRingMultiplierForSingle(), target);
        }

        return CreateThrow(GetMissedNumber(target.val), DartMultiplier.Single, target);
    }

    private Throw SimulateTripleThrow((int val, DartMultiplier mult) target)
    {
        var profile = GetSkillProfile();
        bool hitNumber = UnityEngine.Random.value < GetCurrentAccuracy();

        if (hitNumber)
        {
            float ringRoll = UnityEngine.Random.value;

            if (ringRoll < profile.TripleSuccessChance)
                return CreateThrow(target.val, DartMultiplier.Triple, target);

            if (ringRoll < profile.TripleSuccessChance + profile.TripleWrongRingChance)
                return CreateThrow(target.val, GetWrongRingMultiplierForTriple(), target);

            if (ringRoll < profile.TripleSuccessChance + profile.TripleWrongRingChance + profile.TripleOutsideMissChance)
                return new Throw(DartMultiplier.Single, 0, HitType.Board, false, target.val);
        }

        // Decide whether the miss lands on a neighbor number or somewhere else
        if (UnityEngine.Random.value < profile.NeighborNumberChance)
        {
            var (left, right) = GetNeighbors(target.val);
            int neighbor = UnityEngine.Random.value < 0.5f ? left : right;
            DartMultiplier mult = UnityEngine.Random.value < profile.NeighborTripleChance
                ? DartMultiplier.Triple
                : DartMultiplier.Single;

            return CreateThrow(neighbor, mult, target);
        }

        // random board miss -> single
        return CreateThrow(RandomBoardNumber(), DartMultiplier.Single, target);
    }

    private Throw SimulateDoubleThrow((int val, DartMultiplier mult) target)
    {
        var profile = GetSkillProfile();
        bool hitNumber = UnityEngine.Random.value < GetCurrentAccuracy();

        if (hitNumber)
        {
            float ringRoll = UnityEngine.Random.value;

            if (ringRoll < profile.DoubleSuccessChance)
                return CreateThrow(target.val, DartMultiplier.Double, target);

            if (ringRoll < profile.DoubleSuccessChance + profile.DoubleWrongRingChance)
                return CreateThrow(target.val, GetWrongRingMultiplierForDouble(), target);

            if (ringRoll < profile.DoubleSuccessChance + profile.DoubleWrongRingChance + profile.DoubleOutsideMissChance)
                return new Throw(DartMultiplier.Single, 0, HitType.Board, false, target.val);
        }

        // Decide whether the miss lands on a neighbor number or somewhere else
        if (UnityEngine.Random.value < profile.NeighborNumberChance)
        {
            var (left, right) = GetNeighbors(target.val);
            int neighbor = UnityEngine.Random.value < 0.5f ? left : right;
            DartMultiplier mult = UnityEngine.Random.value < profile.NeighborDoubleChance
                ? DartMultiplier.Double
                : DartMultiplier.Single;

            return CreateThrow(neighbor, mult, target);
        }

        // random board miss -> single
        return CreateThrow(RandomBoardNumber(), DartMultiplier.Single, target);
    }

    private Throw SimulateBullThrow((int val, DartMultiplier mult) target)
    {
        var profile = GetSkillProfile();
        float roll = UnityEngine.Random.value;

        if (target.mult == DartMultiplier.Single)
        {
            if (roll < profile.SingleBullDoubleChance)
                return CreateThrow(25, DartMultiplier.Double, target);

            if (roll < profile.SingleBullDoubleChance + profile.SingleBullSingleChance)
                return CreateThrow(25, DartMultiplier.Single, target);

            return CreateThrow(RandomBoardNumber(), DartMultiplier.Single, target);
        }

        // Ziel = Bullseye
        if (roll < profile.DoubleBullDoubleChance)
            return CreateThrow(25, DartMultiplier.Double, target);

        if (roll < profile.DoubleBullDoubleChance + profile.DoubleBullSingleChance)
            return CreateThrow(25, DartMultiplier.Single, target);

        return CreateThrow(RandomBoardNumber(), DartMultiplier.Single, target);
    }

    private Throw CreateThrow(int value,
                          DartMultiplier multiplier,
                          (int val, DartMultiplier mult) target)
    {
        bool hit =
            value == target.val &&
            multiplier == target.mult;

        return new Throw(
            multiplier,
            value,
            HitType.Board,
            hit,
            target.val);
    }

    private int RandomBoardNumber()
    {
        return BoardCircle[UnityEngine.Random.Range(0, BoardCircle.Length)];
    }

    private int GetMissedNumber(int target)
    {
        if (target == 25)
            return RandomBoardNumber();

        if (UnityEngine.Random.value < GetNeighborChance())
        {
            var (left, right) = GetNeighbors(target);
            return UnityEngine.Random.value < .5f ? left : right;
        }

        return RandomBoardNumber();
    }

    // =========================================================
    // HELPER
    // =========================================================

    private (int left, int right) GetNeighbors(int value)
    {
        int index = System.Array.IndexOf(BoardCircle, value);
        if (index == -1) return (1, 5);

        int left = BoardCircle[(index - 1 + 20) % 20];
        int right = BoardCircle[(index + 1) % 20];
        return (left, right);
    }
}

public enum DartBotDifficulty { Easy, Medium, Hard, Pro }