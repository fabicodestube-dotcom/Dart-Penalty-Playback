using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;

public class AtcStats
{
    [JsonProperty] public int hits = 0;
    [JsonProperty] public int attempts = 0;
    [JsonProperty] public float hitPercentage = 0f;

    [JsonProperty] public int firstDartHits = 0;
    [JsonProperty] public int firstDartAttempts = 0;
    [JsonProperty] public float firstDartHitPercentage = 0f;

    [JsonProperty] public bool hasStreak = false;
    [JsonProperty] public int currentHitStreak = 0;
    [JsonProperty] public int longestHitStreak = 0;
    [JsonProperty] public List<int> completedStreaks = new();

    [JsonProperty] public Dictionary<Guid, int> gameLongestHitStreak = new Dictionary<Guid, int>();

    [JsonProperty] public Dictionary<int, int> throwsPerTarget;
    [JsonProperty] public Dictionary<int, int> hitsPerTarget;
    [JsonProperty] public Dictionary<string, int> throwSectorCounts = new Dictionary<string, int>();

    [JsonProperty] public int streak3Plus = 0;
    [JsonProperty] public int streak6Plus = 0;
    [JsonProperty] public int streak9Plus = 0;
    [JsonProperty] public int streak12Plus = 0;

    public AtcStats()
    {
        EnsureTargetDictionaries();
        EnsureInternalStructures();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        EnsureTargetDictionaries();
        EnsureInternalStructures();
    }

    public void EnsureInternalStructures()
    {
        gameLongestHitStreak ??= new Dictionary<Guid, int>();
        throwSectorCounts ??= new Dictionary<string, int>();
        completedStreaks ??= new List<int>();
        EnsureTargetDictionaries();
    }

    public static string GetAttemptSectorKey(Throw t, int target)
    {
        if (t == null || (t.HitType != HitType.Board && !(t.HitType == HitType.Custom && t.Value > 0)) || t.GetScore() == 0)
            return "0";
        if (t.Multiplier == DartMultiplier.Double)
            return "D" + target;
        if (t.Multiplier == DartMultiplier.Triple)
            return target == 25 ? "25" : "T" + target;
        return target.ToString();
    }

    private void EnsureTargetDictionaries()
    {
        throwsPerTarget ??= Enumerable.Range(1, 20).Append(25).ToDictionary(x => x, x => 0);
        hitsPerTarget ??= Enumerable.Range(1, 20).Append(25).ToDictionary(x => x, x => 0);
    }

    public void FlushCurrentStreak()
    {
        if (currentHitStreak > 0)
        {
            completedStreaks.Add(currentHitStreak);
            RegisterCompletedStreak(currentHitStreak);
        }

        hasStreak = false;
        currentHitStreak = 0;
        RecalculateLongestStreak();
    }

    private void RegisterCompletedStreak(int streakLength)
    {
        if (streakLength >= 12)
            streak12Plus++;
        else if (streakLength >= 9)
            streak9Plus++;
        else if (streakLength >= 6)
            streak6Plus++;
        else if (streakLength >= 3)
            streak3Plus++;
    }

    public void AddGameStat(AtcStats atc, Guid gameId)
    {
        if (atc == null || gameId == Guid.Empty || gameLongestHitStreak.ContainsKey(gameId))
            return;

        hits += atc.hits;
        attempts += atc.attempts;
        firstDartHits += atc.firstDartHits;
        firstDartAttempts += atc.firstDartAttempts;

        MergeTargetCounts(throwsPerTarget, atc.throwsPerTarget);
        MergeTargetCounts(hitsPerTarget, atc.hitsPerTarget);

        if (atc.throwSectorCounts != null)
        {
            foreach (var kv in atc.throwSectorCounts)
                throwSectorCounts[kv.Key] = throwSectorCounts.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
        }

        currentHitStreak = Math.Max(currentHitStreak, atc.currentHitStreak);
        longestHitStreak = Math.Max(longestHitStreak, atc.longestHitStreak);

        streak3Plus += atc.streak3Plus;
        streak6Plus += atc.streak6Plus;
        streak9Plus += atc.streak9Plus;
        streak12Plus += atc.streak12Plus;

        gameLongestHitStreak[gameId] = atc.longestHitStreak;

        UpdatePercentages();
    }

    public void RemoveGameStat(AtcStats atc, Guid gameId)
    {
        if (atc == null || !gameLongestHitStreak.ContainsKey(gameId))
            return;

        hits = Math.Max(0, hits - atc.hits);
        attempts = Math.Max(0, attempts - atc.attempts);
        firstDartHits = Math.Max(0, firstDartHits - atc.firstDartHits);
        firstDartAttempts = Math.Max(0, firstDartAttempts - atc.firstDartAttempts);

        SubtractTargetCounts(throwsPerTarget, atc.throwsPerTarget);
        SubtractTargetCounts(hitsPerTarget, atc.hitsPerTarget);

        if (atc.throwSectorCounts != null)
        {
            foreach (var kv in atc.throwSectorCounts)
            {
                if (throwSectorCounts.TryGetValue(kv.Key, out var v))
                    throwSectorCounts[kv.Key] = Math.Max(0, v - kv.Value);
            }
        }

        streak3Plus = Math.Max(0, streak3Plus - atc.streak3Plus);
        streak6Plus = Math.Max(0, streak6Plus - atc.streak6Plus);
        streak9Plus = Math.Max(0, streak9Plus - atc.streak9Plus);
        streak12Plus = Math.Max(0, streak12Plus - atc.streak12Plus);

        gameLongestHitStreak.Remove(gameId);

        longestHitStreak = gameLongestHitStreak.Count == 0
            ? 0
            : gameLongestHitStreak.Values.Max();

        UpdatePercentages();
    }

    private static void MergeTargetCounts(Dictionary<int, int> target, Dictionary<int, int> source)
    {
        if (target == null || source == null)
            return;

        foreach (var kv in source)
            target[kv.Key] = target.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
    }

    private static void SubtractTargetCounts(Dictionary<int, int> target, Dictionary<int, int> source)
    {
        if (target == null || source == null)
            return;

        foreach (var kv in source)
        {
            if (target.TryGetValue(kv.Key, out var v))
                target[kv.Key] = Math.Max(0, v - kv.Value);
        }
    }

    public void ResetForRebuild()
    {
        hits = 0;
        attempts = 0;
        hitPercentage = 0f;
        firstDartHits = 0;
        firstDartAttempts = 0;
        firstDartHitPercentage = 0f;
        hasStreak = false;
        currentHitStreak = 0;
        longestHitStreak = 0;
        completedStreaks.Clear();
        gameLongestHitStreak.Clear();
        throwSectorCounts.Clear();
        streak3Plus = 0;
        streak6Plus = 0;
        streak9Plus = 0;
        streak12Plus = 0;

        throwsPerTarget = Enumerable.Range(1, 20).Append(25).ToDictionary(x => x, x => 0);
        hitsPerTarget = Enumerable.Range(1, 20).Append(25).ToDictionary(x => x, x => 0);
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

    public void AddThrow(Throw t, int target, bool wasCorrectTarget, bool wasFirstDart)
    {
        if (t == null)
            return;

        attempts++;
        throwsPerTarget[target] = throwsPerTarget.TryGetValue(target, out int throwCount) ? throwCount + 1 : 1;

        string sectorKey = GetAttemptSectorKey(t, target);
        throwSectorCounts[sectorKey] = throwSectorCounts.TryGetValue(sectorKey, out var sectorVal) ? sectorVal + 1 : 1;

        if (wasFirstDart)
            firstDartAttempts++;

        if (wasCorrectTarget)
        {
            hits++;
            hitsPerTarget[target] = hitsPerTarget.TryGetValue(target, out int hitCount) ? hitCount + 1 : 1;

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
    }

    public void RemoveThrow(Throw t, int target, bool wasCorrectTarget, bool wasFirstDart, int previousStreakLength)
    {
        if (t == null)
            return;

        attempts = Math.Max(0, attempts - 1);
        if (throwsPerTarget.TryGetValue(target, out int throwCount))
            throwsPerTarget[target] = Math.Max(0, throwCount - 1);

        string sectorKey = GetAttemptSectorKey(t, target);
        if (throwSectorCounts.TryGetValue(sectorKey, out var sectorVal))
            throwSectorCounts[sectorKey] = Math.Max(0, sectorVal - 1);

        if (wasFirstDart)
        {
            firstDartAttempts = Math.Max(0, firstDartAttempts - 1);

            if (wasCorrectTarget)
                firstDartHits = Math.Max(0, firstDartHits - 1);
        }

        if (wasCorrectTarget)
        {
            hits = Math.Max(0, hits - 1);
            if (hitsPerTarget.TryGetValue(target, out int hitCount))
                hitsPerTarget[target] = Math.Max(0, hitCount - 1);
        }
        else if (completedStreaks.Count > 0)
        {
            int restoredStreak = completedStreaks[completedStreaks.Count - 1];
            completedStreaks.RemoveAt(completedStreaks.Count - 1);
            UnregisterCompletedStreak(restoredStreak);
        }

        currentHitStreak = previousStreakLength;
        hasStreak = previousStreakLength > 0;

        RecalculateLongestStreak();
        UpdatePercentages();
    }

    public (int target, int attempts) GetChoke()
    {
        if (throwsPerTarget == null || throwsPerTarget.Count == 0)
            return (0, 0);

        var result = throwsPerTarget.OrderByDescending(x => x.Value).First();
        return (result.Key, result.Value);
    }

    private void UpdatePercentages()
    {
        hitPercentage = attempts == 0
            ? 0f
            : (float)Math.Round((float)hits / attempts, 2);

        firstDartHitPercentage = firstDartAttempts == 0
            ? 0f
            : (float)Math.Round((float)firstDartHits / firstDartAttempts, 2);
    }

    private void RecalculateLongestStreak()
    {
        int completedMax = completedStreaks.Count > 0
            ? completedStreaks.Max()
            : 0;

        longestHitStreak = Math.Max(completedMax, currentHitStreak);
    }
}
