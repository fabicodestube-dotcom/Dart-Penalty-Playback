using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using System.Runtime.Serialization;

public class GameStatsATC : GameStats
{
    // =========================================================
    // CORE ATC STATS
    // =========================================================

    [JsonProperty] public int targetsHit = 0;
    [JsonProperty] public int totalTargets = 0;
    [JsonProperty] public float hitPercentage = 0f;

    // =========================================================
    // FIRST DART STATS
    // =========================================================

    [JsonProperty] public int firstDartHits = 0;
    [JsonProperty] public int firstDartAttempts = 0;
    [JsonProperty] public float firstDartHitPercentage = 0f;

    // =========================================================
    // STREAKS
    // =========================================================

    [JsonProperty] public bool hasStreak = false;
    [JsonProperty] public int currentHitStreak = 0;
    [JsonProperty] public int longestHitStreak = 0;
    [JsonProperty] public List<int> completedStreaks = new();

    [JsonProperty] public Dictionary<Guid, float> gameHitPercentage = new Dictionary<Guid, float>();
    [JsonProperty] public Dictionary<Guid, float> gameFirstDartPercentage = new Dictionary<Guid, float>();
    [JsonProperty] public Dictionary<Guid, int> gameLongestHitStreak = new Dictionary<Guid, int>();

    // =========================================================
    // FAIL STATES
    // =========================================================

    [JsonProperty] public Dictionary<int, int> throwsPerTarget;
    [JsonProperty] public Dictionary<int, int> hitsPerTarget;

    // =========================================================
    // TABLE HIGHSCORES
    // =========================================================
    [JsonProperty] public int streak3Plus = 0;
    [JsonProperty] public int streak6Plus = 0;
    [JsonProperty] public int streak9Plus = 0;
    [JsonProperty] public int streak12Plus = 0;


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public GameStatsATC(Guid playerID) : base(playerID)
    {
        EnsureTargetDictionaries();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        // Bei Newtonsoft wird der Konstruktor nicht garantiert aufgerufen.
        // Falls alte Savegames diese Dictionaries nicht enthalten, müssen wir sie nachziehen.
        EnsureTargetDictionaries();
        EnsureInternalStructures();
    }

    private void EnsureTargetDictionaries()
    {
        throwsPerTarget ??=
            Enumerable.Range(1, 20)
                .Append(25)
                .ToDictionary(x => x, x => 0);
        hitsPerTarget ??=
            Enumerable.Range(1, 20)
                .Append(25)
                .ToDictionary(x => x, x => 0);
    }

    private void EnsureInternalStructures()
    {
        gameHitPercentage ??= new Dictionary<Guid, float>();
        gameFirstDartPercentage ??= new Dictionary<Guid, float>();
        gameLongestHitStreak ??= new Dictionary<Guid, int>();
    }

    private void RegisterCompletedStreak(int streakLength)
    {
        if (streakLength >= 12)
            streak12Plus ++;
        else if (streakLength >= 9)
            streak9Plus++;
        else if (streakLength >= 6)
            streak6Plus++;
        else if (streakLength >= 3)
            streak3Plus++;
    }


    public override void AddGameStat(GameStats stats, Guid gameId, float penaltyCost = 0f)
    {
        if (stats is not GameStatsATC atc)
            return;

        if (gameHitPercentage.ContainsKey(gameId))
            return;

        base.AddGameStat(stats, gameId, penaltyCost);

        // Hits and Targets
        int games = gameHitPercentage.Count;
        if (games == 0)
        {
            targetsHit = atc.targetsHit;
            totalTargets = atc.totalTargets;
        }
        else
        {
            targetsHit = (int)Math.Round(
                ((float)targetsHit * games + atc.targetsHit)
                / (games + 1));

            totalTargets = (int)Math.Round(
                ((float)totalTargets * games + atc.totalTargets)
                / (games + 1));
        }

        firstDartHits += atc.firstDartHits;
        firstDartAttempts += atc.firstDartAttempts;
        currentHitStreak = Math.Max(currentHitStreak, atc.currentHitStreak);
        longestHitStreak = Math.Max(longestHitStreak, atc.longestHitStreak);

        gameHitPercentage[gameId] = atc.hitPercentage;
        gameFirstDartPercentage[gameId] = atc.firstDartHitPercentage;
        gameLongestHitStreak[gameId] = atc.longestHitStreak;

        UpdateAggregatedPercentages();
    }

    public override void RemoveGameStat(GameStats stats, Guid gameId)
    {
        if (stats is not GameStatsATC atc)
            return;

        if (!gameHitPercentage.ContainsKey(gameId))
            return;

        base.RemoveGameStat(stats, gameId);

        // Hits and Targets
        int games = gameHitPercentage.Count;
        if (games <= 1)
        {
            targetsHit = 0;
            totalTargets = 0;
        }
        else
        {
            targetsHit = (int)Math.Round(
                ((float)targetsHit * games - atc.targetsHit)
                / (games - 1));

            totalTargets = (int)Math.Round(
                ((float)totalTargets * games - atc.totalTargets)
                / (games - 1));
        }

        firstDartHits = Math.Max(0, firstDartHits - atc.firstDartHits);
        firstDartAttempts = Math.Max(0, firstDartAttempts - atc.firstDartAttempts);

        gameHitPercentage.Remove(gameId);
        gameFirstDartPercentage.Remove(gameId);
        gameLongestHitStreak.Remove(gameId);

        longestHitStreak = gameLongestHitStreak.Count == 0
            ? 0
            : gameLongestHitStreak.Values.Max();

        UpdateAggregatedPercentages();
    }

    private void UpdateAggregatedPercentages()
    {
        hitPercentage = gameHitPercentage.Count == 0
            ? 0f
            : gameHitPercentage.Values.Average();

        firstDartHitPercentage = gameFirstDartPercentage.Count == 0
            ? 0f
            : gameFirstDartPercentage.Values.Average();
    }

    private void UnregisterCompletedStreak(int streakLength)
    {
        if (streakLength >= 12)
            streak12Plus = Math.Max(0, streak12Plus - 1);

        else if (streakLength >= 9)
            streak9Plus = Math.Max(0, streak9Plus - 1);

        else if (streakLength >= 6)
            streak6Plus = Math.Max(0, streak6Plus - 1);

        else if (streakLength >= 3)
            streak3Plus = Math.Max(0, streak3Plus - 1);
    }


    // =========================================================
    // ROUND / THROW LOGIC
    // =========================================================

    public void AddThrow(Throw t, int target, bool wasCorrectTarget, bool wasFirstDart)
    {
        if (t == null)
            return;

        totalTargets++;
        throwsPerTarget[target]++;

        if (wasFirstDart)
            firstDartAttempts++;

        if (wasCorrectTarget)
        {
            targetsHit++;
            hitsPerTarget[target]++;

            if (wasFirstDart)
                firstDartHits++;

            currentHitStreak++;
            hasStreak = true;
        }
        else
        {
            if (currentHitStreak > 0)
            {
                completedStreaks.Add(currentHitStreak);
                RegisterCompletedStreak(currentHitStreak);
            }

            hasStreak = false;
            currentHitStreak = 0;
        }

        RecalculateLongestStreak();
        UpdatePercentages();

        Debug.Log("LongestStreak: " + longestHitStreak + " | CurrentStreak: " + currentHitStreak);
    }

    public void RemoveThrow(Throw t, int target, bool wasCorrectTarget, bool wasFirstDart, int previousStreakLength)
    {

        if (t == null)
            return;

        totalTargets = Math.Max(0, totalTargets - 1);
        throwsPerTarget[target] = Math.Max(0, throwsPerTarget[target] - 1);

        if (wasFirstDart)
        {
            firstDartAttempts = Math.Max(0, firstDartAttempts - 1);

            if (wasCorrectTarget)
                firstDartHits = Math.Max(0, firstDartHits - 1);
        }

        if (wasCorrectTarget)
        {
            targetsHit = Math.Max(0, targetsHit - 1);
            hitsPerTarget[target] = Math.Max(0, hitsPerTarget[target] - 1);
        }
        else
        {
            if (completedStreaks.Count > 0)
            {
                int restoredStreak =
                    completedStreaks[completedStreaks.Count - 1];

                completedStreaks.RemoveAt(completedStreaks.Count - 1);

                UnregisterCompletedStreak(restoredStreak);
            }
        }

        currentHitStreak = previousStreakLength;
        hasStreak = previousStreakLength > 0;

        RecalculateLongestStreak();
        UpdatePercentages();

        Debug.Log("LongestStreak: " + longestHitStreak + " | CurrentStreak: " + currentHitStreak);
    }


    public (int target, int attempts) GetChoke()
    {
        var result = throwsPerTarget
            .OrderByDescending(x => x.Value)
            .First();

        return (result.Key, result.Value);
    }


    // =========================================================
    // CALCULATIONS
    // =========================================================

    private void UpdatePercentages()
    {
        if (totalTargets == 0)
            hitPercentage = 0f;
        else
            hitPercentage = (float)Math.Round((float)targetsHit / totalTargets, 2);

        if (firstDartAttempts == 0)
            firstDartHitPercentage = 0f;
        else
            firstDartHitPercentage = (float)Math.Round((float)firstDartHits / firstDartAttempts, 2);

    }

    private void RecalculateLongestStreak()
    {
        int completedMax = completedStreaks.Count > 0
            ? completedStreaks.Max()
            : 0;

        longestHitStreak = Math.Max(completedMax, currentHitStreak);
    }
}