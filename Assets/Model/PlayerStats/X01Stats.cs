using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;

public class X01Stats
{
    [JsonProperty] public int turnCount;
    [JsonProperty] public int turnSum;
    [JsonProperty] public float averagePointsPerTurn;
    [JsonProperty] public int bestTurnPoints;

    [JsonProperty] public int first9Points;
    [JsonProperty] public float first9Average;

    [JsonProperty] public int count60Plus;
    [JsonProperty] public int count100Plus;
    [JsonProperty] public int count140Plus;
    [JsonProperty] public int count180;

    [JsonProperty] public List<Turn> checkoutTurns = new List<Turn>();
    [JsonProperty] public int highestCheckout;
    [JsonProperty] public float checkoutAttemptCount;

    [JsonProperty] public Dictionary<string, int> hitSectorCounts = new Dictionary<string, int>();
    [JsonProperty] public Dictionary<Guid, float> gameAveragePoints = new Dictionary<Guid, float>();
    [JsonProperty] public Dictionary<Guid, int> gameBestTurnPoints = new Dictionary<Guid, int>();
    [JsonProperty] public Dictionary<Guid, int> gameHighestCheckouts = new Dictionary<Guid, int>();
    [JsonProperty] public Dictionary<Guid, List<Turn>> gameCheckoutTurns = new Dictionary<Guid, List<Turn>>();
    [JsonProperty] public Dictionary<Guid, Dictionary<string, int>> gameHitSectorCounts = new Dictionary<Guid, Dictionary<string, int>>();
    [JsonProperty] public Dictionary<Guid, float> gameFirst9Average = new Dictionary<Guid, float>();
    [JsonProperty] public Dictionary<Guid, float> gameCheckoutAttemptCount = new Dictionary<Guid, float>();

    public X01Stats()
    {
        InitHitSectors();
        EnsureInternalStructures();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        EnsureInternalStructures();
        InitHitSectorsIfNeeded();
        checkoutTurns ??= new List<Turn>();
    }

    public void EnsureInternalStructures()
    {
        gameAveragePoints ??= new Dictionary<Guid, float>();
        gameBestTurnPoints ??= new Dictionary<Guid, int>();
        gameHighestCheckouts ??= new Dictionary<Guid, int>();
        gameCheckoutTurns ??= new Dictionary<Guid, List<Turn>>();
        gameHitSectorCounts ??= new Dictionary<Guid, Dictionary<string, int>>();
        gameFirst9Average ??= new Dictionary<Guid, float>();
        gameCheckoutAttemptCount ??= new Dictionary<Guid, float>();
        hitSectorCounts ??= new Dictionary<string, int>();
    }

    public void AddGameStat(X01Stats x01, Guid gameId)
    {
        if (x01 == null || gameId == Guid.Empty || gameBestTurnPoints.ContainsKey(gameId))
            return;

        turnCount += x01.turnCount;
        turnSum += x01.turnSum;
        count60Plus += x01.count60Plus;
        count100Plus += x01.count100Plus;
        count140Plus += x01.count140Plus;
        count180 += x01.count180;

        gameAveragePoints[gameId] = x01.averagePointsPerTurn;
        gameBestTurnPoints[gameId] = x01.bestTurnPoints;
        gameHighestCheckouts[gameId] = x01.highestCheckout;
        gameCheckoutTurns[gameId] = new List<Turn>(x01.checkoutTurns ?? new List<Turn>());
        gameHitSectorCounts[gameId] = new Dictionary<string, int>(x01.hitSectorCounts ?? new Dictionary<string, int>());
        gameFirst9Average[gameId] = x01.first9Average;
        gameCheckoutAttemptCount[gameId] = x01.checkoutAttemptCount;

        RecalculateAggregatedValues();
    }

    public void RemoveGameStat(X01Stats x01, Guid gameId)
    {
        if (x01 == null || !gameBestTurnPoints.ContainsKey(gameId))
            return;

        turnCount = Math.Max(0, turnCount - x01.turnCount);
        turnSum = Math.Max(0, turnSum - x01.turnSum);
        count60Plus = Math.Max(0, count60Plus - x01.count60Plus);
        count100Plus = Math.Max(0, count100Plus - x01.count100Plus);
        count140Plus = Math.Max(0, count140Plus - x01.count140Plus);
        count180 = Math.Max(0, count180 - x01.count180);

        gameAveragePoints.Remove(gameId);
        gameBestTurnPoints.Remove(gameId);
        gameHighestCheckouts.Remove(gameId);
        gameCheckoutTurns.Remove(gameId);
        gameHitSectorCounts.Remove(gameId);
        gameFirst9Average.Remove(gameId);
        gameCheckoutAttemptCount.Remove(gameId);

        RecalculateAggregatedValues();
    }

    public void ResetForRebuild()
    {
        turnCount = 0;
        turnSum = 0;
        averagePointsPerTurn = 0f;
        bestTurnPoints = 0;
        first9Points = 0;
        first9Average = 0f;
        count60Plus = 0;
        count100Plus = 0;
        count140Plus = 0;
        count180 = 0;
        checkoutTurns = new List<Turn>();
        highestCheckout = 0;
        checkoutAttemptCount = 0f;

        gameAveragePoints.Clear();
        gameBestTurnPoints.Clear();
        gameHighestCheckouts.Clear();
        gameCheckoutTurns.Clear();
        gameHitSectorCounts.Clear();
        gameFirst9Average.Clear();
        gameCheckoutAttemptCount.Clear();

        InitHitSectors();
    }

    private void RecalculateAggregatedValues()
    {
        averagePointsPerTurn = turnCount == 0
            ? 0f
            : (float)Math.Round((float)turnSum / turnCount, 2);

        bestTurnPoints = gameBestTurnPoints.Count == 0
            ? 0
            : gameBestTurnPoints.Values.Max();

        highestCheckout = gameHighestCheckouts.Count == 0
            ? 0
            : gameHighestCheckouts.Values.Max();

        checkoutTurns = gameCheckoutTurns.Values.SelectMany(list => list).ToList();

        first9Average = gameFirst9Average.Count == 0
            ? 0f
            : gameFirst9Average.Values.Average();

        checkoutAttemptCount = gameCheckoutAttemptCount.Count == 0
            ? 0f
            : (float)Math.Round(gameCheckoutAttemptCount.Values.Average(), 2);

        RebuildHitSectorCounts();
    }

    private void RebuildHitSectorCounts()
    {
        hitSectorCounts = new Dictionary<string, int>();

        foreach (var kv in gameHitSectorCounts.Values)
        {
            foreach (var inner in kv)
            {
                hitSectorCounts[inner.Key] = hitSectorCounts.TryGetValue(inner.Key, out var existing)
                    ? existing + inner.Value
                    : inner.Value;
            }
        }

        if (hitSectorCounts.Count == 0)
            InitHitSectors();
    }

    private void InitHitSectorsIfNeeded()
    {
        hitSectorCounts ??= new Dictionary<string, int>();

        if (hitSectorCounts.Count > 0)
            return;

        InitHitSectors();
    }

    public void RegisterTurn(Turn t)
    {
        if (t == null)
        {
            Debug.LogWarning("[PlayerStatsX01] RegisterTurn NULL");
            return;
        }

        turnCount += 1;

        int score = t.GetTurnScore();

        if (turnCount <= 3)
        {
            first9Points += score;
            UpdateFirst9Average();
        }

        turnSum += score;

        if (score > bestTurnPoints)
            bestTurnPoints = score;

        foreach (var throwItem in t.GetThrows())
            ApplyThrowToStats(throwItem, +1);

        if (score == 180)
            count180 += 1;
        else if (score > 139)
            count140Plus += 1;
        else if (score > 99)
            count100Plus += 1;
        else if (score > 59)
            count60Plus += 1;

        CalculateAveragePointsPerTurn();
    }

    public void UnregisterTurn(Turn t)
    {
        if (t == null || turnCount <= 0)
            return;

        turnCount -= 1;

        int score = t.GetTurnScore();
        turnSum -= score;

        foreach (var dart in t.GetThrows())
            ApplyThrowToStats(dart, -1);

        if (score == 180)
            count180 -= 1;
        else if (score > 139)
            count140Plus -= 1;
        else if (score > 99)
            count100Plus -= 1;
        else if (score > 59)
            count60Plus -= 1;

        CalculateAveragePointsPerTurn();
    }

    public void RegisterCheckout(Turn checkoutTurn)
    {
        if (checkoutTurn == null)
            return;

        checkoutTurns.Add(checkoutTurn);
        int score = checkoutTurn.GetTurnScore();

        if (score > highestCheckout)
            highestCheckout = score;
    }

    public void UnregisterCheckout(Turn checkoutTurn)
    {
        if (checkoutTurn == null)
            return;

        if (!checkoutTurns.Remove(checkoutTurn))
            return;

        highestCheckout = checkoutTurns.Count == 0
            ? 0
            : checkoutTurns.Max(t => t.GetTurnScore());
    }

    public void RegisterCheckoutAttempt()
    {
        checkoutAttemptCount += 1;
    }

    public void UnregisterCheckoutAttempt()
    {
        if (checkoutAttemptCount > 0)
            checkoutAttemptCount -= 1;
    }

    [JsonIgnore]
    public IReadOnlyDictionary<string, int> GetHitSector
        => new Dictionary<string, int>(hitSectorCounts);

    public string GetMostFrequentClosingSector()
    {
        if (checkoutTurns == null || checkoutTurns.Count == 0)
            return LocalizationText.Get("No_checkouts", "No checkouts");

        var closingSectors = checkoutTurns
            .Select(t => t.GetThrows().LastOrDefault())
            .Where(lastThrow => lastThrow != null)
            .Select(GetSectorKey)
            .ToList();

        if (closingSectors.Count == 0)
            return LocalizationText.Get("No_data", "No data");

        return closingSectors
            .GroupBy(sector => sector)
            .OrderByDescending(group => group.Count())
            .First()
            .Key;
    }

    private void CalculateAveragePointsPerTurn()
    {
        if (turnCount == 0)
        {
            averagePointsPerTurn = 0;
            return;
        }

        averagePointsPerTurn = (float)Math.Round((float)turnSum / turnCount, 2);
    }

    private void InitHitSectors()
    {
        hitSectorCounts = new Dictionary<string, int>
        {
            ["0"] = 0
        };

        for (int i = 1; i <= 20; i++)
        {
            hitSectorCounts[i.ToString()] = 0;
            hitSectorCounts["D" + i] = 0;
            hitSectorCounts["T" + i] = 0;
        }

        hitSectorCounts["25"] = 0;
        hitSectorCounts["D25"] = 0;
    }

    private void ApplyThrowToStats(Throw t, int delta)
    {
        string key = null;

        if ((t.HitType != HitType.Board && t.HitType != HitType.Custom) || t.GetScore() == 0)
        {
            key = "0";
        }
        else if (t.Value == 25)
        {
            if (t.Multiplier == DartMultiplier.Triple)
                return;

            key = (t.Multiplier == DartMultiplier.Double) ? "D25" : "25";
        }
        else if (t.Value < 1 || t.Value > 20)
        {
            key = "0";
        }
        else
        {
            key = t.Multiplier switch
            {
                DartMultiplier.Double => "D" + t.Value,
                DartMultiplier.Triple => "T" + t.Value,
                _ => t.Value.ToString()
            };
        }

        if (key != null)
        {
            hitSectorCounts.TryGetValue(key, out int current);
            hitSectorCounts[key] = Math.Max(0, current + delta);
        }
    }

    private static string GetSectorKey(Throw t)
    {
        if ((t.HitType != HitType.Board && t.HitType != HitType.Custom) || t.GetScore() == 0)
            return "0";

        if (t.Value == 25)
            return (t.Multiplier == DartMultiplier.Double) ? "D25" : "25";

        return t.Multiplier switch
        {
            DartMultiplier.Double => "D" + t.Value,
            DartMultiplier.Triple => "T" + t.Value,
            _ => t.Value.ToString()
        };
    }

    private void UpdateFirst9Average()
    {
        if (turnCount == 0)
        {
            first9Average = 0f;
            return;
        }

        first9Average = (float)Math.Round((float)first9Points / turnCount, 2);
    }
}
