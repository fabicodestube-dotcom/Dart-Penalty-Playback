using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static partial class CheckoutDatabase
{
    private static readonly int Bull = 25;

    // =========================================
    // DOUBLE PRIORITY
    // =========================================
    private static readonly Dictionary<int, float> DoubleWeights = new Dictionary<int, float>
    {
        {40, 1.00f},
        {32, 0.95f},
        {16, 0.90f},
        {20, 0.85f},
        {24, 0.80f},
        {36, 0.78f},
        {12, 0.75f},
        {8,  0.72f},
        {4,  0.70f},
        {28, 0.65f},
        {22, 0.60f},
        {18, 0.58f},
        {30, 0.55f},
        {26, 0.50f},
        {34, 0.45f},
        {38, 0.40f},
        {14, 0.35f},
        {10, 0.30f},
        {6,  0.25f},
        {2,  0.20f},
    };

    private static readonly Dictionary<int, float> TripleWeights = new Dictionary<int, float>
    {
        {60, 1.00f},
        {57, 0.95f},
        {54, 0.90f},
        {51, 0.85f},
        {48, 0.80f},
        {45, 0.75f},
        {42, 0.70f},
        {39, 0.65f},
        {36, 0.60f},
        {33, 0.55f},
        {30, 0.50f},
        {27, 0.45f},
        {24, 0.40f},
        {21, 0.35f},
        {18, 0.30f},
        {15, 0.25f},
        {12, 0.20f},
        {9,  0.15f},
        {6,  0.10f},
        {3,  0.05f},
    };

    private static readonly Dictionary<int, string[]> DoubleOutTable = new Dictionary<int, string[]>
    {
        { 170, new[] { "T20", "T20", "DB" } },
        { 167, new[] { "T20", "T19", "DB" } },
        { 164, new[] { "T20", "T18", "DB" } },
        { 161, new[] { "T20", "T17", "DB" } },
        { 160, new[] { "T20", "T20", "D20" } },
        { 158, new[] { "T20", "T20", "D19" } },
        { 157, new[] { "T20", "T19", "D20" } },
        { 156, new[] { "T20", "T20", "D18" } },
        { 155, new[] { "T20", "T19", "D19" } },
        { 154, new[] { "T20", "T18", "D20" } },
        { 153, new[] { "T20", "T19", "D18" } },
        { 152, new[] { "T20", "T20", "D16" } },
        { 151, new[] { "T20", "T17", "D20" } },
        { 150, new[] { "T20", "T18", "D18" } },
        { 149, new[] { "T20", "T19", "D16" } },
        { 148, new[] { "T20", "T20", "D14" } },
        { 147, new[] { "T20", "T17", "D18" } },
        { 146, new[] { "T20", "T18", "D16" } },
        { 145, new[] { "T20", "T19", "D14" } },
        { 144, new[] { "T20", "T20", "D12" } },
        { 143, new[] { "T20", "T17", "D16" } },
        { 142, new[] { "T20", "T14", "D20" } },
        { 141, new[] { "T20", "T19", "D12" } },
        { 140, new[] { "T20", "T20", "D10" } },
        { 139, new[] { "T20", "T13", "D20" } },
        { 138, new[] { "T20", "T18", "D12" } },
        { 137, new[] { "T20", "T19", "D10" } },
        { 136, new[] { "T20", "T20", "D8" } },
        { 135, new[] { "T20", "T17", "D12" } },
        { 134, new[] { "T20", "T16", "D13" } },
        { 133, new[] { "T20", "T19", "D8" } },
        { 132, new[] { "T20", "T16", "D12" } },
        { 131, new[] { "T19", "T14", "D16" } },
        { 130, new[] { "T20", "T20", "D5" } },
        { 129, new[] { "T19", "T16", "D12" } },
        { 128, new[] { "T18", "T14", "D16" } },
        { 127, new[] { "T20", "T17", "D8" } },
        { 126, new[] { "T19", "T19", "D6" } },
        { 125, new[] { "T18", "T19", "D7" } },
        { 124, new[] { "T20", "T14", "D11" } },
        { 123, new[] { "T19", "T16", "D9" } },
        { 122, new[] { "T18", "T18", "D7" } },
        { 121, new[] { "T20", "T11", "D14" } },
        { 120, new[] { "T20", "20", "D20" } },
        { 119, new[] { "T19", "T12", "D13" } },
        { 118, new[] { "T20", "18", "D20" } },
        { 117, new[] { "T19", "20", "D20" } },
        { 116, new[] { "T19", "19", "D20" } },
        { 115, new[] { "T20", "15", "D20" } },
        { 114, new[] { "T19", "17", "D20" } },
        { 113, new[] { "T19", "16", "D20" } },
        { 112, new[] { "T20", "T12", "D8" } },
        { 111, new[] { "T19", "14", "D20" } },
        { 110, new[] { "T20", "T10", "D10" } },
        { 109, new[] { "T20", "9", "D20" } },
        { 108, new[] { "T20", "16/8", "D16/D20" } },
        { 107, new[] { "T19", "T10", "D10" } },
        { 106, new[] { "T20", "T10", "D8" } },
        { 105, new[] { "T20", "13", "D16" } },
        { 104, new[] { "T19", "15", "D16" } },
        { 103, new[] { "T19", "6/10", "D20/D18" } },
        { 102, new[] { "T20", "10/6", "D16/D18" } },
        { 101, new[] { "T20", "9", "D16" } },
        { 100, new[] { "T20", "D20", "-" } },
        { 99, new[] { "T19", "10/6", "D16/D18" } },
        { 98, new[] { "T20", "D19", "-" } },
        { 97, new[] { "T19", "D20", "-" } },
        { 96, new[] { "T20", "D18", "-" } },
        { 95, new[] { "T19", "D19", "-" } },
        { 94, new[] { "T18", "D20", "-" } },
        { 93, new[] { "T19", "D18", "-" } },
        { 92, new[] { "T20", "D16", "-" } },
        { 91, new[] { "T17", "D20", "-" } },
        { 90, new[] { "T20", "D15", "-" } },
        { 89, new[] { "T19", "D16", "-" } },
        { 88, new[] { "T20", "D14", "-" } },
        { 87, new[] { "T17", "D18", "-" } },
        { 86, new[] { "T18", "D16", "-" } },
        { 85, new[] { "T15", "D20", "-" } },
        { 84, new[] { "T20", "D12", "-" } },
        { 83, new[] { "T17", "D16", "-" } },
        { 82, new[] { "DB", "D16", "-" } },
        { 81, new[] { "T19", "D12", "-" } },
        { 80, new[] { "T20", "D10", "-" } },
        { 79, new[] { "T19", "D11", "-" } },
        { 78, new[] { "T18", "D12", "-" } },
        { 77, new[] { "T19", "D10", "-" } },
        { 76, new[] { "T20", "D8", "-" } },
        { 75, new[] { "T17", "D12", "-" } },
        { 74, new[] { "T14", "D16", "-" } },
        { 73, new[] { "T19", "D8", "-" } },
        { 72, new[] { "T16", "D12", "-" } },
        { 71, new[] { "T13", "D16", "-" } },
        { 70, new[] { "T18", "D8", "-" } },
        { 69, new[] { "T19", "D6", "-" } },
        { 68, new[] { "T20", "D4", "-" } },
        { 67, new[] { "T9", "D20", "-" } },
        { 66, new[] { "T10", "D18", "-" } },
        { 65, new[] { "T11", "D16", "-" } },
        { 64, new[] { "T16", "D8", "-" } },
        { 63, new[] { "T13", "D12", "-" } },
        { 62, new[] { "T10", "D16", "-" } },
        { 61, new[] { "T15", "D8", "-" } },
        { 60, new[] { "20", "D20", "-" } },
        { 59, new[] { "19", "D20", "-" } },
        { 58, new[] { "18", "D20", "-" } },
        { 57, new[] { "17", "D20", "-" } },
        { 56, new[] { "T16", "D4", "-" } },
        { 55, new[] { "15", "D20", "-" } },
        { 54, new[] { "14", "D20", "-" } },
        { 53, new[] { "13", "D20", "-" } },
        { 52, new[] { "12", "D20", "-" } },
        { 51, new[] { "11", "D20", "-" } },
        { 50, new[] { "10", "D20", "-" } },
        { 49, new[] { "9", "D20", "-" } },
        { 48, new[] { "16/8", "D16/D20", "-" } },
        { 47, new[] { "7", "D20", "-" } },
        { 46, new[] { "6/10", "D20/D18", "-" } },
        { 45, new[] { "13", "D16", "-" } },
        { 44, new[] { "12", "D16", "-" } },
        { 43, new[] { "3", "D20", "-" } },
        { 42, new[] { "10/6", "D16/D18", "-" } },
        { 41, new[] { "9", "D16", "-" } },
        { 40, new[] { "D20", "-", "-" } },
        { 39, new[] { "7", "D16", "-" } },
        { 38, new[] { "D19", "-", "-" } },
        { 37, new[] { "5", "D16", "-" } },
        { 36, new[] { "D18", "-", "-" } },
        { 35, new[] { "3", "D16", "-" } },
        { 34, new[] { "D17", "-", "-" } },
        { 33, new[] { "1", "D16", "-" } },
        { 32, new[] { "D16", "-", "-" } },
        { 31, new[] { "15", "D8", "-" } },
        { 30, new[] { "D15", "-", "-" } },
        { 29, new[] { "13", "D8", "-" } },
        { 28, new[] { "D14", "-", "-" } },
        { 27, new[] { "11", "D8", "-" } },
        { 26, new[] { "D13", "-", "-" } },
        { 25, new[] { "9", "D8", "-" } },
        { 24, new[] { "D12", "-", "-" } },
        { 23, new[] { "7", "D8", "-" } },
        { 22, new[] { "D11", "-", "-" } },
        { 21, new[] { "5", "D8", "-" } },
        { 20, new[] { "D10", "-", "-" } },
        { 19, new[] { "3", "D8", "-" } },
        { 18, new[] { "D9", "-", "-" } },
        { 17, new[] { "1", "D8", "-" } },
        { 16, new[] { "D8", "-", "-" } },
        { 15, new[] { "7", "D4", "-" } },
        { 14, new[] { "D7", "-", "-" } },
        { 13, new[] { "5", "D4", "-" } },
        { 12, new[] { "D6", "-", "-" } },
        { 11, new[] { "3", "D4", "-" } },
        { 10, new[] { "D5", "-", "-" } },
        { 9,  new[] { "1", "D4", "-" } },
        { 8,  new[] { "D4", "-", "-" } },
        { 7,  new[] { "3", "D2", "-" } },
        { 6,  new[] { "D3", "-", "-" } },
        { 5,  new[] { "1", "D2", "-" } },
        { 4,  new[] { "D2", "-", "-" } },
        { 3,  new[] { "1", "D1", "-" } },
        { 2,  new[] { "D1", "-", "-" } }
    };

    private static readonly Dictionary<int, string[]> DoubleOutTwoDartTable = new Dictionary<int, string[]>
    {
        { 101, new[] { "T17", "D25" } },
        { 104, new[] { "T18", "D25" } },
        { 107, new[] { "T19", "D25" } },
        { 110, new[] { "T20", "D25" } }
    };

    private static bool TryGetDoubleOutTableRoute(int score, int maxDarts, out List<string> route)
    {
        route = null;

        if (!DoubleOutTable.TryGetValue(score, out string[] slots))
            return false;

        var resolved = new List<string>(slots.Length);

        foreach (string slot in slots)
        {
            if (slot == "-")
                continue;

            string dart = slot;
            int separator = dart.IndexOf('/');

            if (separator >= 0)
                dart = dart.Substring(0, separator);

            if (dart == "DB")
                dart = "D25";

            resolved.Add(dart);
        }

        if (resolved.Count == 0 || resolved.Count > maxDarts)
            return false;

        if (GetRouteScore(resolved) != score || GetFinalWeight(resolved[^1]) <= 0f)
            return false;

        route = resolved;
        return true;
    }

    private static bool TryGetDoubleOutTwoDartRoute(int score, out List<string> route)
    {
        route = null;

        if (!DoubleOutTwoDartTable.TryGetValue(score, out string[] slots))
            return false;

        var resolved = new List<string>(slots);

        if (resolved.Count != 2 || GetRouteScore(resolved) != score || GetFinalWeight(resolved[^1]) <= 0f)
            return false;

        route = resolved;
        return true;
    }

    private static List<string> GetDoubleOutRoute(int score, int maxDarts)
    {
        if (TryGetDoubleOutTableRoute(score, maxDarts, out List<string> tableRoute))
            return tableRoute;

        if (maxDarts >= 2 && TryGetDoubleOutTwoDartRoute(score, out List<string> twoDartRoute))
            return twoDartRoute;

        if (maxDarts >= 1 && TryGetDirectDoubleFinish(score, out string directDart))
            return new List<string> { directDart };

        return null;
    }

    // =========================================
    // PUBLIC API
    // =========================================
    public struct CheckoutPlan
    {
        public string Route;
        public int DartCount;
        public float FinalWeight;
        public bool IsValid => !string.IsNullOrEmpty(Route);
    }

    public static CheckoutPlan GetDoubleOutTurnPlan(int score)
    {
        var plan = new CheckoutPlan();

        if (score <= 0 || !TryGetDoubleOutTableRoute(score, 3, out List<string> route))
            return plan;

        plan.Route = string.Join(" ", route);
        plan.DartCount = route.Count;
        plan.FinalWeight = GetFinalWeight(route[^1]);
        return plan;
    }

    public static bool TryGetDirectDoubleFinish(int score, out string dart)
    {
        if (score == 50)
        {
            dart = "D25";
            return true;
        }

        if (score >= 2 && score <= 40 && score % 2 == 0)
        {
            dart = "D" + (score / 2);
            return true;
        }

        dart = null;
        return false;
    }

    public static string GetCheckout(CheckoutType type, int score, int dartsLeft)
    {
        return GetCheckoutPlan(type, score, dartsLeft).Route;
    }

    public static CheckoutPlan GetCheckoutPlan(CheckoutType type, int score, int maxDarts)
    {
        var plan = new CheckoutPlan();

        if (score <= 0 || maxDarts <= 0)
            return plan;

        if (maxDarts > 3)
            maxDarts = 3;

        List<string> route = null;

        if (type == CheckoutType.Double)
        {
            if (score > 170)
                return plan;

            route = GetDoubleOutRoute(score, maxDarts);
        }
        else
        {
            route = GetBestRoute(type, score, maxDarts);
        }

        if (route == null)
            return plan;

        plan.Route = string.Join(" ", route);
        plan.DartCount = route.Count;
        plan.FinalWeight = GetFinalWeight(route[^1]);
        return plan;
    }

    // =========================================
    // ROUTE SELECTION
    // =========================================
    private static List<string> GetBestRoute(CheckoutType type, int score, int maxDarts)
    {
        for (int darts = 1; darts <= maxDarts; darts++)
        {
            var route = GenerateRoutes(score, darts, type).FirstOrDefault();

            if (route != null)
                return route;
        }

        return null;
    }

    // =========================================
    // ROUTE GENERATION
    // =========================================
    private static List<List<string>> GenerateRoutes(int score, int dartsLeft, CheckoutType type)
    {
        List<List<string>> results = new List<List<string>>();

        var lastDarts = GetDartsForCheckoutType(type);

        foreach (var last in lastDarts)
        {
            int rest = score - last.value;

            if (rest < 0)
                continue;

            if (rest == 0)
            {
                results.Add(new List<string> { last.label });
                continue;
            }

            if (dartsLeft == 1)
                continue;

            var previousRoutes = GeneratePreviousDarts(rest, dartsLeft - 1);

            foreach (var prev in previousRoutes)
            {
                var full = new List<string>(prev)
                {
                    last.label
                };

                results.Add(full);
            }
        }

        return results
            .OrderByDescending(ScoreRoute)
            .ToList();
    }

    // =========================================
    // CHECKOUT TARGETS
    // =========================================
    private static List<(int value, string label)> GetDartsForCheckoutType(CheckoutType type)
    {
        var list = new List<(int value, string label)>();

        switch (type)
        {
            case CheckoutType.Single:

                for (int i = 1; i <= 20; i++)
                    list.Add((i, i.ToString()));

                list.Add((Bull, "25"));
                break;

            case CheckoutType.Double:

                for (int i = 1; i <= 20; i++)
                    list.Add((i * 2, "D" + i));

                list.Add((50, "D25"));
                break;

            case CheckoutType.Triple:

                for (int i = 1; i <= 20; i++)
                    list.Add((i * 3, "T" + i));

                break;

            case CheckoutType.Master:

                for (int i = 1; i <= 20; i++)
                {
                    list.Add((i * 2, "D" + i));
                    list.Add((i * 3, "T" + i));
                }

                list.Add((50, "D25"));
                break;
        }

        return list
            .OrderByDescending(x => x.value)
            .ToList();
    }

    // =========================================
    // ROUTE SCORING
    // =========================================
    private static float ScoreRoute(List<string> route)
    {
        if (route == null || route.Count == 0)
            return 0f;

        float score = 0f;
        int remaining = GetRouteScore(route);

        for (int i = 0; i < route.Count; i++)
        {
            string dart = route[i];

            bool isLast = i == route.Count - 1;

            // =========================================
            // FINAL DART
            // =========================================
            if (isLast)
            {
                score += ScoreCheckoutDart(dart);
                break;
            }

            // =========================================
            // NORMAL DART
            // =========================================
            score += ScoreDart(dart, remaining);

            remaining -= GetDartValue(dart);
        }

        // =========================================
        // ROUTE CLEANNESS BONUS
        // =========================================

        // Less complex = better
        score -= route.Count * 0.05f;

        // Punishing too many triples
        int triples = route.Count(d => d.StartsWith("T"));
        score -= triples * 0.15f;

        return score;
    }

    // =========================================
    // CHECKOUT DART SCORE
    // =========================================
    private static float ScoreCheckoutDart(string dart)
    {
        return GetFinalWeight(dart) * 10f;
    }

    private static float GetFinalWeight(string dart)
    {
        if (dart == "D25")
            return 0.95f;

        if (dart.StartsWith("D"))
        {
            int val = int.Parse(dart.Substring(1)) * 2;

            if (DoubleWeights.TryGetValue(val, out float weight))
                return weight;

            return 0.5f;
        }

        if (dart.StartsWith("T"))
        {
            int val = int.Parse(dart.Substring(1)) * 3;

            if (TripleWeights.TryGetValue(val, out float weight))
                return weight;

            return 0.2f;
        }

        return 0f;
    }

    // =========================================
    // SINGLE DART SCORING
    // =========================================
    private static float ScoreDart(string dart, int remainingScore)
    {
        bool isSetup = remainingScore <= 60;
        bool isScoring = remainingScore > 100;

        float value = 0f;

        // =========================================
        // BASE TYPE VALUE
        // =========================================

        if (IsSingle(dart))
        {
            value = isSetup
                ? 1.0f
                : isScoring
                    ? 0.2f
                    : 0.5f;
        }
        else if (dart.StartsWith("D"))
        {
            value = isSetup
                ? 0.3f
                : isScoring
                    ? 0.6f
                    : 0.5f;
        }
        else if (dart.StartsWith("T"))
        {
            value = isSetup
                ? 0.1f
                : isScoring
                    ? 1.0f
                    : 0.7f;
        }

        // =========================================
        // SMALL NUMBER LOGIC
        // =========================================

        int dartValue = GetDartValue(dart);

        // Prefer playing small numbers as singles
        if (remainingScore <= 20)
        {
            if (IsSingle(dart))
                value += 1.2f;

            if (dart.StartsWith("D"))
                value -= 0.5f;

            if (dart.StartsWith("T"))
                value -= 2.0f;
        }

        // Avoid T1-T6
        if (dart.StartsWith("T"))
        {
            int triple = int.Parse(dart.Substring(1));

            if (triple <= 6)
                value -= 1.5f;
        }

        // =========================================
        // GOOD LEAVE BONUS
        // =========================================

        int after = remainingScore - dartValue;

        if (after > 1 && after % 2 == 0)
        {
            value += 0.3f;
        }

        // Favorite numbers
        if (after == 40)
            value += 1.0f;

        if (after == 32)
            value += 0.9f;

        if (after == 24)
            value += 0.8f;

        if (after == 16)
            value += 0.8f;

        // =========================================
        // BOARD PREFERENCE
        // =========================================

        value += GetBoardPreference(dart);

        return value;
    }

    // =========================================
    // BOARD PREFERENCE
    // =========================================
    private static float GetBoardPreference(string dart)
    {
        if (dart == "20")
            return 1.0f;

        if (dart == "19")
            return 0.95f;

        if (dart == "18")
            return 0.9f;

        if (dart == "17")
            return 0.85f;

        if (dart.StartsWith("T"))
            return 0.2f;

        if (dart.StartsWith("D"))
            return 0.4f;

        return 0.7f;
    }

    // =========================================
    // PREVIOUS DARTS
    // =========================================
    private static List<List<string>> GeneratePreviousDarts(int score, int dartsLeft)
    {
        List<List<string>> results = new List<List<string>>();

        if (dartsLeft <= 0)
            return results;

        var moves = new List<(int value, string label)>();

        for (int i = 1; i <= 20; i++)
        {
            // Singles
            moves.Add((i, i.ToString()));

            // Doubles
            moves.Add((i * 2, "D" + i));

            // Triples
            moves.Add((i * 3, "T" + i));
        }

        moves.Add((25, "25"));
        moves.Add((50, "D25"));

        foreach (var move in moves)
        {
            if (move.value > score)
                continue;

            int rest = score - move.value;

            if (rest == 0 && dartsLeft == 1)
            {
                results.Add(new List<string> { move.label });
                continue;
            }

            var next = GeneratePreviousDarts(rest, dartsLeft - 1);

            foreach (var n in next)
            {
                var list = new List<string> { move.label };
                list.AddRange(n);

                results.Add(list);
            }
        }

        return results;
    }

    // =========================================
    // HELPERS
    // =========================================
    private static bool IsSingle(string dart)
    {
        if (dart.StartsWith("D"))
            return false;

        if (dart.StartsWith("T"))
            return false;

        return true;
    }

    private static int GetDartValue(string dart)
    {
        if (dart.StartsWith("S"))
            return int.Parse(dart.Substring(1));

        if (dart.StartsWith("D"))
            return int.Parse(dart.Substring(1)) * 2;

        if (dart.StartsWith("T"))
            return int.Parse(dart.Substring(1)) * 3;

        if (int.TryParse(dart, out int numeric))
            return numeric;

        if (dart == "Bull")
            return 25;

        return 0;
    }

    private static int GetRouteScore(List<string> route)
    {
        int sum = 0;

        foreach (var dart in route)
            sum += GetDartValue(dart);

        return sum;
    }
}