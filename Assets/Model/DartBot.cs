using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using System;

[System.Serializable]
public class DartBot : BasePlayer
{
    [JsonProperty] private DartBotDifficulty difficulty;
    [JsonProperty] private int number;


    private static readonly int[] BoardCircle = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

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

    private float GetNumberAccuracy()
    {
        return difficulty switch
        {
            DartBotDifficulty.Easy => 0.45f,
            DartBotDifficulty.Medium => 0.72f,
            DartBotDifficulty.Hard => 0.87f,
            DartBotDifficulty.Pro => 0.95f,
            _ => 0.6f
        };
    }

    private float GetTripleChance()
    {
        return difficulty switch
        {
            DartBotDifficulty.Easy => 0.05f,
            DartBotDifficulty.Medium => 0.13f,
            DartBotDifficulty.Hard => 0.25f,
            DartBotDifficulty.Pro => 0.45f,
            _ => 0.1f
        };
    }

    private float GetDoubleChance()
    {
        return difficulty switch
        {
            DartBotDifficulty.Easy => 0.08f,
            DartBotDifficulty.Medium => 0.15f,
            DartBotDifficulty.Hard => 0.25f,
            DartBotDifficulty.Pro => 0.4f,
            _ => 0.1f
        };
    }

    private float GetNeighborChance()
    {
        return difficulty switch
        {
            DartBotDifficulty.Easy => 0.60f,
            DartBotDifficulty.Medium => 0.75f,
            DartBotDifficulty.Hard => 0.85f,
            DartBotDifficulty.Pro => 0.93f,
            _ => 0.7f
        };
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
        if (dart.StartsWith("T"))
            return (int.Parse(dart.Substring(1)), DartMultiplier.Triple);

        if (dart.StartsWith("D"))
            return (int.Parse(dart.Substring(1)), DartMultiplier.Double);

        if (dart.StartsWith("S"))
            return (int.Parse(dart.Substring(1)), DartMultiplier.Single);

        if (dart == "50")
            return (25, DartMultiplier.Double);

        if (dart == "25" || dart == "Bull")
            return (25, DartMultiplier.Single);

        return (20, DartMultiplier.Single);
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
    // 🎯 REALISTISCHE PHYSIK
    // =========================================================

    private Throw SimulatePhysicalThrow((int val, DartMultiplier mult) target)
    {
        float numberAcc = GetNumberAccuracy();
        bool hitNumber = UnityEngine.Random.value < numberAcc;

        int actualValue;
        DartMultiplier actualMultiplier = DartMultiplier.Single;

        if (hitNumber)
        {
            actualValue = target.val;

            float roll = UnityEngine.Random.value;

            if (target.mult == DartMultiplier.Triple)
            {
                if (roll < GetTripleChance())
                {
                    actualMultiplier = DartMultiplier.Triple;
                }
                else
                {
                    // 👉 Triple verfehlt → oft Single gleiche Zahl
                    actualMultiplier = DartMultiplier.Single;
                }
            }
            else if (target.mult == DartMultiplier.Double)
            {
                if (roll < GetDoubleChance())
                {
                    actualMultiplier = DartMultiplier.Double;
                }
                else
                {
                    // 👉 Double verfehlt → meist Single gleiche Zahl
                    actualMultiplier = DartMultiplier.Single;
                }
            }
        }
        else
        {
            // 👉 Zahl verfehlt
            float neighborChance = GetNeighborChance();

            if (target.val == 25)
            {
                actualValue = BoardCircle[UnityEngine.Random.Range(0, 20)];
            }
            else if (UnityEngine.Random.value < neighborChance)
            {
                var (left, right) = GetNeighbors(target.val);
                actualValue = UnityEngine.Random.value > 0.5f ? left : right;
            }
            else
            {
                actualValue = BoardCircle[UnityEngine.Random.Range(0, 20)];
            }

            actualMultiplier = DartMultiplier.Single;
        }

        bool isTargetHit = actualValue == target.val && actualMultiplier == target.mult;

        return new Throw(actualMultiplier, actualValue, HitType.Board, isTargetHit, target.val);
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