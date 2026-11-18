using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class PlayerStats
{
    [JsonProperty] public Guid playerID;

    [JsonProperty] public int gameCount = 0;
    [JsonProperty] public int gamesWon = 0;
    [JsonProperty] public int currentLegCount = 0;
    [JsonProperty] public int totalLegsCount = 0;
    [JsonProperty] public int totalLegsWon = 0;
    [JsonProperty] public float totalLegWinRate = 0f;

    [JsonProperty] public int totalSetsCount = 0;
    [JsonProperty] public int totalSetsWon = 0;
    [JsonProperty] public float totalSetWinRate = 0f;

    [JsonProperty] public int wallCount = 0;
    [JsonProperty] public int ceilingCount = 0;
    [JsonProperty] public int allMissCount = 0;
    [JsonProperty] public int tripleOnesCount = 0;
    [JsonProperty] public int tripleDigitCount = 0;
    [JsonProperty] public int lostGame = 0;

    [JsonProperty] public int totalThrowsCount = 0;
    [JsonProperty] public int totalDoubleCount = 0;
    [JsonProperty] public int totalTripleCount = 0;
    [JsonProperty] public float doublePercentage = 0f;
    [JsonProperty] public float triplePercentage = 0f;

    [JsonProperty] public List<Guid> appliedGameIds = new List<Guid>();
    [JsonProperty] public Dictionary<Guid, float> gamePenaltyCosts = new Dictionary<Guid, float>();
    [JsonProperty] public float totalPenaltyCost = 0f;

    [JsonProperty] public float customPenaltyCost = 0f;
    [JsonProperty] public Dictionary<string, int> customPenaltyCounts = new Dictionary<string, int>();

    [JsonProperty] public X01Stats x01;
    [JsonProperty] public CricketStats cricket;
    [JsonProperty] public AtcStats atc;

    [JsonIgnore]
    public float AverageThrowsPerGame => gameCount <= 0
        ? 0f
        : (float)totalThrowsCount / gameCount;

    public PlayerStats(Guid playerID)
    {
        this.playerID = playerID;
        EnsureInternalStructures();
    }

    public static PlayerStats CreateForMode(Guid playerID, GameMode mode)
    {
        var stats = new PlayerStats(playerID);

        switch (mode)
        {
            case GameMode.X01:
                stats.x01 = new X01Stats();
                break;
            case GameMode.Cricket:
                stats.cricket = new CricketStats();
                break;
            case GameMode.ATC:
                stats.atc = new AtcStats();
                break;
        }

        return stats;
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        EnsureInternalStructures();
        x01?.EnsureInternalStructures();
        cricket?.EnsureInternalStructures();
        atc?.EnsureInternalStructures();
    }

    private void EnsureInternalStructures()
    {
        appliedGameIds ??= new List<Guid>();
        gamePenaltyCosts ??= new Dictionary<Guid, float>();
        customPenaltyCounts ??= new Dictionary<string, int>();
    }

    public virtual void AddGameStat(PlayerStats stats, Guid gameId, float penaltyCost = 0f)
    {
        if (stats == null || gameId == Guid.Empty || appliedGameIds.Contains(gameId))
            return;

        appliedGameIds.Add(gameId);
        gameCount++;
        gamesWon += Math.Min(stats.gamesWon, 1);

        float resolvedCost = penaltyCost;
        try { resolvedCost = stats.GetTotalPenaltyCosts(); }
        catch { resolvedCost = penaltyCost; }

        gamePenaltyCosts[gameId] = resolvedCost;
        totalPenaltyCost = gamePenaltyCosts.Values.Sum();

        totalLegsCount += stats.totalLegsCount;
        totalLegsWon += stats.totalLegsWon;
        totalSetsCount += stats.totalSetsCount;
        totalSetsWon += stats.totalSetsWon;

        wallCount += stats.wallCount;
        ceilingCount += stats.ceilingCount;
        allMissCount += stats.allMissCount;
        tripleOnesCount += stats.tripleOnesCount;
        tripleDigitCount += stats.tripleDigitCount;
        lostGame += stats.lostGame;

        totalThrowsCount += stats.totalThrowsCount;
        totalDoubleCount += stats.totalDoubleCount;
        totalTripleCount += stats.totalTripleCount;

        MergeCustomPenaltyCounts(stats);

        x01?.AddGameStat(stats.x01, gameId);
        cricket?.AddGameStat(stats.cricket, gameId);
        atc?.AddGameStat(stats.atc, gameId);

        UpdateThrowPercentages();
        UpdateLegWinRate();
        UpdateSetWinRate();
    }

    public virtual void RemoveGameStat(PlayerStats stats, Guid gameId)
    {
        if (stats == null)
        {
            Debug.LogWarning("[Abbruch] Stats-Objekt ist null.");
            return;
        }

        if (gameId == Guid.Empty)
        {
            Debug.LogWarning("[Abbruch] Ungültige GameId.");
            return;
        }

        if (!appliedGameIds.Contains(gameId))
        {
            Debug.LogWarning($"[Abbruch] GameId {gameId} ist nicht in 'appliedGameIds' enthalten.");
            return;
        }

        appliedGameIds.Remove(gameId);
        gameCount = Math.Max(0, gameCount - 1);
        gamesWon = Math.Max(0, gamesWon - stats.gamesWon);

        gamePenaltyCosts.Remove(gameId);
        totalPenaltyCost = gamePenaltyCosts.Values.Sum();

        totalLegsCount = Math.Max(0, totalLegsCount - stats.totalLegsCount);
        totalLegsWon = Math.Max(0, totalLegsWon - stats.totalLegsWon);
        totalSetsCount = Math.Max(0, totalSetsCount - stats.totalSetsCount);
        totalSetsWon = Math.Max(0, totalSetsWon - stats.totalSetsWon);

        wallCount = Math.Max(0, wallCount - stats.wallCount);
        ceilingCount = Math.Max(0, ceilingCount - stats.ceilingCount);
        allMissCount = Math.Max(0, allMissCount - stats.allMissCount);
        tripleOnesCount = Math.Max(0, tripleOnesCount - stats.tripleOnesCount);
        tripleDigitCount = Math.Max(0, tripleDigitCount - stats.tripleDigitCount);
        lostGame = Math.Max(0, lostGame - stats.lostGame);

        totalThrowsCount = Math.Max(0, totalThrowsCount - stats.totalThrowsCount);
        totalDoubleCount = Math.Max(0, totalDoubleCount - stats.totalDoubleCount);
        totalTripleCount = Math.Max(0, totalTripleCount - stats.totalTripleCount);

        SubtractCustomPenaltyCounts(stats);

        x01?.RemoveGameStat(stats.x01, gameId);
        cricket?.RemoveGameStat(stats.cricket, gameId);
        atc?.RemoveGameStat(stats.atc, gameId);

        UpdateThrowPercentages();
        UpdateLegWinRate();
        UpdateSetWinRate();
    }

    protected void MergeCustomPenaltyCounts(PlayerStats stats)
    {
        if (stats?.customPenaltyCounts == null)
            return;

        foreach (var kvp in stats.customPenaltyCounts)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                continue;

            customPenaltyCounts.TryGetValue(kvp.Key, out int current);
            customPenaltyCounts[kvp.Key] = current + kvp.Value;
        }
    }

    protected void SubtractCustomPenaltyCounts(PlayerStats stats)
    {
        if (stats?.customPenaltyCounts == null)
            return;

        foreach (var kvp in stats.customPenaltyCounts)
        {
            if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                continue;

            if (!customPenaltyCounts.TryGetValue(kvp.Key, out int current))
                continue;

            int rest = current - kvp.Value;
            if (rest <= 0)
                customPenaltyCounts.Remove(kvp.Key);
            else
                customPenaltyCounts[kvp.Key] = rest;
        }
    }

    public virtual void AddThrow(Throw t)
    {
        if (t == null)
            return;

        totalThrowsCount++;

        if (t.HitType == HitType.Board || (t.HitType == HitType.Custom && t.Value > 0))
        {
            if (t.Multiplier == DartMultiplier.Double)
                totalDoubleCount++;

            if (t.Multiplier == DartMultiplier.Triple)
                totalTripleCount++;
        }

        UpdateThrowPercentages();
    }

    public virtual void AddPenalty(PenaltyType type)
    {
        switch (type)
        {
            case PenaltyType.Wall:
                wallCount++;
                break;
            case PenaltyType.Ceiling:
                ceilingCount++;
                break;
            case PenaltyType.AllMiss:
                allMissCount++;
                break;
            case PenaltyType.ThreeOnes:
                tripleOnesCount++;
                break;
            case PenaltyType.Schnapszahl:
                tripleDigitCount++;
                break;
            case PenaltyType.LostGame:
                lostGame++;
                break;
        }
    }

    public virtual void RemovePenalty(PenaltyType type)
    {
        switch (type)
        {
            case PenaltyType.Wall:
                wallCount = Math.Max(0, wallCount - 1);
                break;
            case PenaltyType.Ceiling:
                ceilingCount = Math.Max(0, ceilingCount - 1);
                break;
            case PenaltyType.AllMiss:
                allMissCount = Math.Max(0, allMissCount - 1);
                break;
            case PenaltyType.ThreeOnes:
                tripleOnesCount = Math.Max(0, tripleOnesCount - 1);
                break;
            case PenaltyType.Schnapszahl:
                tripleDigitCount = Math.Max(0, tripleDigitCount - 1);
                break;
            case PenaltyType.LostGame:
                lostGame = Math.Max(0, lostGame - 1);
                break;
        }
    }

    public virtual void AddCustomPenaltyCost(float cost)
    {
        if (cost <= 0f)
            return;

        customPenaltyCost += cost;
    }

    public virtual void AddCustomPenaltyBooking(string penaltyId, float cost)
    {
        AddCustomPenaltyCost(cost);

        if (string.IsNullOrEmpty(penaltyId))
            return;

        customPenaltyCounts.TryGetValue(penaltyId, out int current);
        customPenaltyCounts[penaltyId] = current + 1;
    }

    public virtual void RemoveCustomPenaltyCost(float cost)
    {
        if (cost <= 0f)
            return;

        customPenaltyCost = Mathf.Max(0f, customPenaltyCost - cost);
    }

    public void ResetForStatCalculation()
    {
        currentLegCount = 0;
        totalLegsCount = 0;
        totalLegsWon = 0;
        totalLegWinRate = 0f;

        totalSetsCount = 0;
        totalSetsWon = 0;
        totalSetWinRate = 0f;

        totalThrowsCount = 0;
        totalDoubleCount = 0;
        totalTripleCount = 0;

        doublePercentage = 0f;
        triplePercentage = 0f;
    }

    public void RegisterLegWon()
    {
        totalLegsWon++;
        currentLegCount++;
        UpdateLegWinRate();
    }

    public void RegisterLegPlayed()
    {
        totalLegsCount++;
        UpdateLegWinRate();
    }

    public void RegisterSetWon()
    {
        totalSetsWon++;
        UpdateSetWinRate();
    }

    public void RegisterSetPlayed()
    {
        totalSetsCount++;
        currentLegCount = 0;
        UpdateSetWinRate();
    }

    public void RegisterGameWon()
    {
        gamesWon += 1;
    }

    public float GetTotalPenaltyCosts()
    {
        var settings = AppSettingsManager.Instance.Settings.Penalties;

        float total = 0f;

        total += GetPenaltyCost(settings, PenaltyType.Wall, wallCount);
        total += GetPenaltyCost(settings, PenaltyType.Ceiling, ceilingCount);
        total += GetPenaltyCost(settings, PenaltyType.AllMiss, allMissCount);
        total += GetPenaltyCost(settings, PenaltyType.ThreeOnes, tripleOnesCount);
        total += GetPenaltyCost(settings, PenaltyType.LostGame, lostGame);
        total += GetPenaltyCost(settings, PenaltyType.Schnapszahl, tripleDigitCount);
        total += customPenaltyCost;

        return total;
    }

    private void UpdateLegWinRate()
    {
        if (totalLegsCount == 0)
        {
            totalLegWinRate = 0;
            return;
        }

        totalLegWinRate = (float)Math.Round((float)totalLegsWon / totalLegsCount, 2);
    }

    private void UpdateSetWinRate()
    {
        if (totalSetsCount == 0)
        {
            totalSetWinRate = 0;
            return;
        }

        totalSetWinRate = (float)Math.Round((float)totalSetsWon / totalSetsCount, 2);
    }

    private void UpdateThrowPercentages()
    {
        if (totalThrowsCount == 0)
        {
            doublePercentage = 0;
            triplePercentage = 0;
            return;
        }

        doublePercentage = (float)Math.Round((float)totalDoubleCount / totalThrowsCount, 2);
        triplePercentage = (float)Math.Round((float)totalTripleCount / totalThrowsCount, 2);
    }

    private float GetPenaltyCost(PenaltySettings settings, PenaltyType type, int count)
    {
        if (count <= 0)
            return 0f;

        if (!settings.IsEnabled(type))
            return 0f;

        return settings.GetCost(type) * count;
    }
}
