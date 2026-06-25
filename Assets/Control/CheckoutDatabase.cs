using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CheckoutDatabase
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

    // =========================================
    // PUBLIC API
    // =========================================
    public static string GetCheckout(CheckoutType type, int score, int dartsLeft)
    {
        if (score <= 1 || dartsLeft <= 0)
            return null;

        var route =
            GenerateRoutes(score, 1, type).FirstOrDefault() ??
            (dartsLeft >= 2 ? GenerateRoutes(score, 2, type).FirstOrDefault() : null) ??
            (dartsLeft >= 3 ? GenerateRoutes(score, 3, type).FirstOrDefault() : null);

        return route != null
            ? string.Join(" ", route)
            : null;
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

        // Weniger komplex = besser
        score -= route.Count * 0.05f;

        // Zu viele Triples bestrafen
        int triples = route.Count(d => d.StartsWith("T"));
        score -= triples * 0.15f;

        return score;
    }

    // =========================================
    // CHECKOUT DART SCORE
    // =========================================
    private static float ScoreCheckoutDart(string dart)
    {
        if (dart.StartsWith("D"))
        {
            int val = int.Parse(dart.Substring(1)) * 2;

            if (DoubleWeights.TryGetValue(val, out float weight))
                return weight * 10f;

            return 5f;
        }

        if (dart == "D25")
            return 9.5f;

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

        // Kleine Zahlen lieber als Single spielen
        if (remainingScore <= 20)
        {
            if (IsSingle(dart))
                value += 1.2f;

            if (dart.StartsWith("D"))
                value -= 0.5f;

            if (dart.StartsWith("T"))
                value -= 2.0f;
        }

        // T1-T6 vermeiden
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

        // Lieblingszahlen
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