using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class DartBot : BasePlayer
{
    [JsonProperty] private float skillRating;
    [JsonProperty] private readonly int number;

    [JsonProperty] private int streak = 0;
    [JsonProperty] private float confidence = 0f;
    [JsonProperty] private int dartsThrown = 0;

    private const int IntentionalBustMaxScore = 60;
    private const float BustFinalWeightMargin = 0.10f;

    public DartBot(Guid id, string playerName, float skillRating, int number) : base(id, playerName)
    {
        this.skillRating = BotSkillScale.Normalize(skillRating);
        this.number = number;
    }

    public string GetNameWithSkill() => $"{GetName()} ({GetSkillRating():0.0})";

    public int GetNumber()
    {
        return number;
    }

    public float GetSkillRating() => BotSkillScale.Normalize(skillRating);

    public void SetSkillRating(float value)
    {
        skillRating = BotSkillScale.Normalize(value);
    }

    public Throw GetNextX01Throw(int currentScore, CheckoutType checkoutType)
    {
        return GetNextX01Throw(currentScore, checkoutType, 3, true, CheckinType.StraightIn);
    }

    public Throw GetNextX01Throw(
        int currentScore,
        CheckoutType checkoutType,
        int dartsLeft,
        bool isCheckedIn,
        CheckinType checkinType,
        int turnStartScore = -1)
    {
        dartsLeft = Mathf.Clamp(dartsLeft, 1, 3);

        if (!isCheckedIn)
        {
            if (checkinType == CheckinType.DoubleIn)
                return Simulate(20, DartMultiplier.Double);

            if (checkinType == CheckinType.MasterIn)
                return Simulate(20, DartMultiplier.Triple);
        }

        if (currentScore <= 0)
            return Simulate(20, DartMultiplier.Single);

        var checkoutPlan = CheckoutDatabase.GetCheckoutPlan(checkoutType, currentScore, 3);

        if (ShouldIntentionalBust(checkoutType, currentScore, dartsLeft, turnStartScore, checkoutPlan))
            return Simulate(20, DartMultiplier.Triple);

        if (checkoutPlan.IsValid)
        {
            var first = ParseDart(checkoutPlan.Route.Split(' ')[0]);
            return Simulate(first.val, first.mult);
        }

        var scoringTarget = ChooseScoringTarget(currentScore);
        return Simulate(scoringTarget.val, scoringTarget.mult);
    }

    private static bool ShouldIntentionalBust(
        CheckoutType checkoutType,
        int currentScore,
        int dartsLeft,
        int turnStartScore,
        CheckoutDatabase.CheckoutPlan checkoutPlan)
    {
        if (dartsLeft != 1
            || turnStartScore <= currentScore
            || currentScore > IntentionalBustMaxScore
            || checkoutType == CheckoutType.Single
            || !checkoutPlan.IsValid)
        {
            return false;
        }

        if (CheckoutDatabase.GetCheckoutPlan(checkoutType, currentScore, 1).IsValid)
            return false;

        var first = ParseDart(checkoutPlan.Route.Split(' ')[0]);
        int normalLeave = currentScore - DartScore(first.val, first.mult);

        if (normalLeave <= 0)
            return false;

        var startPlan = CheckoutDatabase.GetCheckoutPlan(checkoutType, turnStartScore, 3);
        if (!startPlan.IsValid)
            return false;

        var leavePlan = CheckoutDatabase.GetCheckoutPlan(checkoutType, normalLeave, 3);
        if (!leavePlan.IsValid)
            return true;

        if (startPlan.DartCount != leavePlan.DartCount)
            return startPlan.DartCount < leavePlan.DartCount;

        return startPlan.FinalWeight > leavePlan.FinalWeight + BustFinalWeightMargin;
    }

    private static int DartScore(int value, DartMultiplier multiplier)
    {
        return multiplier switch
        {
            DartMultiplier.Double => value * 2,
            DartMultiplier.Triple => value * 3,
            _ => value
        };
    }

    private (int val, DartMultiplier mult) ChooseScoringTarget(int score)
    {
        if (score > 170 && IsBogeyLeave(score - 60) && !IsBogeyLeave(score - 57))
            return (19, DartMultiplier.Triple);

        if (score > 60)
            return (20, DartMultiplier.Triple);

        if (score % 2 == 0 && score <= 40)
            return (score / 2, DartMultiplier.Double);

        int leave = score - 40;
        if (leave >= 1 && leave <= 20)
            return (leave, DartMultiplier.Single);

        return (20, DartMultiplier.Single);
    }

    private static bool IsBogeyLeave(int score)
    {
        return score == 169
            || score == 168
            || score == 166
            || score == 163
            || score == 162
            || score == 159;
    }

    private static (int val, DartMultiplier mult) ParseDart(string dart)
    {
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

        return (1, DartMultiplier.Single);
    }

    public Throw GetNextCricketThrow(CricketBotContext ctx)
    {
        Guid myId = ctx.PlayerId;

        var openNumbers = ctx.Numbers
            .Where(n => ctx.PlayerHits[myId][n] < 3)
            .OrderByDescending(CricketTargetValue)
            .ToList();

        if (openNumbers.Count > 0)
        {
            int number = openNumbers[0];
            int myHits = ctx.PlayerHits[myId][number];
            bool needOne = 3 - myHits == 1;

            DartMultiplier multiplier;
            if (number == 25)
                multiplier = needOne && GetSkillRating() < 6f
                    ? DartMultiplier.Single
                    : DartMultiplier.Double;
            else if (needOne && GetSkillRating() < 5f)
                multiplier = DartMultiplier.Single;
            else
                multiplier = DartMultiplier.Triple;

            return Simulate(number, multiplier);
        }

        if (ctx.PointsEnabled)
        {
            var scoringNumbers = ctx.Numbers
                .Where(n => ctx.PlayerIds.Any(id => id != myId && ctx.PlayerHits[id][n] < 3))
                .OrderByDescending(n => ctx.PlayerIds.Count(id => id != myId && ctx.PlayerHits[id][n] < 3))
                .ThenByDescending(CricketTargetValue)
                .ToList();

            if (scoringNumbers.Count > 0)
            {
                int number = scoringNumbers[0];
                return Simulate(number, number == 25 ? DartMultiplier.Double : DartMultiplier.Triple);
            }
        }

        return Simulate(20, DartMultiplier.Triple);
    }

    private static float CricketTargetValue(int number)
    {
        return number == 25 ? 17.5f : number;
    }

    public Throw GetNextATCThrow(int currentTarget, DartMultiplier requiredMultiplier)
    {
        if (currentTarget <= 0)
            return new Throw(DartMultiplier.Single, 0, HitType.Wall, false, -1);

        Throw result = Simulate(currentTarget, requiredMultiplier);
        bool isActuallyHit = IsAtcHit(result, currentTarget, requiredMultiplier);

        return new Throw(
            result.Multiplier,
            result.Value,
            result.HitType,
            isActuallyHit,
            currentTarget,
            result.CustomHitId,
            requiredMultiplier,
            result.LandingX,
            result.LandingY,
            result.TargetX,
            result.TargetY,
            result.HasLandingPoint,
            result.HasTargetPoint);
    }

    private static bool IsAtcHit(Throw result, int target, DartMultiplier required)
    {
        if (result.Value != target)
            return false;

        if (result.HitType != HitType.Board && result.HitType != HitType.Custom)
            return false;

        if (required == DartMultiplier.Single)
            return !(target == 25 && result.Multiplier == DartMultiplier.Triple);

        return result.Multiplier == required;
    }

    private Throw Simulate(int value, DartMultiplier multiplier)
    {
        Throw throwData = DartThrowSimulator.Simulate(
            GetSkillRating(),
            value,
            multiplier,
            GetCurrentFormFactor());

        RegisterThrow(throwData);
        return throwData;
    }

    private float GetCurrentFormFactor()
    {
        float factor = 1f;
        factor -= Mathf.Clamp(streak, -5, 5) * 0.012f;
        factor -= Mathf.Clamp(confidence, -5f, 5f) * 0.008f;
        factor += GetFatiguePenalty();

        return Mathf.Clamp(factor, 0.80f, 1.35f);
    }

    private float GetFatiguePenalty()
    {
        if (dartsThrown < 150)
            return 0f;

        if (dartsThrown < 300)
            return 0.01f;

        return 0.025f;
    }

    private void RegisterThrow(Throw dart)
    {
        dartsThrown++;

        if (dart.GetScore() > 0)
        {
            streak = Math.Min(5, streak + 1);
            confidence = Math.Min(5f, confidence + 0.2f);
        }
        else
        {
            streak = Math.Max(-5, streak - 1);
            confidence = Math.Max(-5f, confidence - 0.3f);
        }
    }
}
