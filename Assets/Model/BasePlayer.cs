using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public abstract class BasePlayer
{
    [JsonProperty] protected Guid id;
    [JsonProperty] protected string playerName;

    [JsonProperty] protected bool showInStatistics;
    [JsonProperty] protected bool deletedFlag;
    [JsonProperty] protected int setupStartPosition = -1;

    [JsonIgnore] protected Dictionary<StatisticsRange, Dictionary<GameMode, PlayerStats>> statsByRange;

    private static readonly StatisticsRange[] AllRanges =
    {
        StatisticsRange.AllTime,
        StatisticsRange.Today,
        StatisticsRange.ThisWeek,
        StatisticsRange.ThisMonth,
        StatisticsRange.ThisYear
    };

    private static readonly StatisticsRange[] TimeRanges =
    {
        StatisticsRange.Today,
        StatisticsRange.ThisWeek,
        StatisticsRange.ThisMonth,
        StatisticsRange.ThisYear
    };

    private static readonly GameMode[] AllModes =
    {
        GameMode.X01,
        GameMode.Cricket,
        GameMode.ATC
    };

    protected BasePlayer(Guid id, string playerName)
    {
        this.id = id;
        this.playerName = playerName;

        showInStatistics = true;
        deletedFlag = false;

        ResetAllStats();
    }

    public virtual void Rename(string newName)
    {
        Debug.LogWarning($"{GetName()} kann nicht umbenannt werden (Basis-Implementierung).");
    }

    public Guid GetID() => id;

    public string GetName() => playerName;

    public void ShowInStatistics(bool show)
    {
        showInStatistics = show;
    }

    public bool IsVisibleInStatistics()
    {
        return showInStatistics;
    }

    public void Delete()
    {
        deletedFlag = true;
    }

    public bool GotDeleted()
    {
        return deletedFlag;
    }

    public PlayerStats GetX01Stats() => GetModeStats(GameMode.X01, StatisticsRange.AllTime);

    public PlayerStats GetCricketStats() => GetModeStats(GameMode.Cricket, StatisticsRange.AllTime);

    public PlayerStats GetATCStats() => GetModeStats(GameMode.ATC, StatisticsRange.AllTime);

    public PlayerStats GetTimebasedStats(StatisticsRange range, GameMode mode)
    {
        if (range == StatisticsRange.AllTime)
            return null;

        EnsureStatsMap();
        EnsureRange(range);

        return statsByRange[range].TryGetValue(mode, out var stats) ? stats : null;
    }

    public void SetModeStats(GameMode mode, StatisticsRange range, PlayerStats stats)
    {
        EnsureStatsMap();
        EnsureRange(range);

        statsByRange[range][mode] = stats ?? PlayerStats.CreateForMode(id, mode);
    }

    public void ResetAllStats()
    {
        statsByRange = new Dictionary<StatisticsRange, Dictionary<GameMode, PlayerStats>>();

        foreach (var range in AllRanges)
        {
            var modeStats = new Dictionary<GameMode, PlayerStats>();
            foreach (var mode in AllModes)
                modeStats[mode] = PlayerStats.CreateForMode(id, mode);

            statsByRange[range] = modeStats;
        }
    }

    public void ResetTimeStats()
    {
        EnsureStatsMap();

        foreach (var range in TimeRanges)
        {
            var modeStats = new Dictionary<GameMode, PlayerStats>();
            foreach (var mode in AllModes)
                modeStats[mode] = PlayerStats.CreateForMode(id, mode);

            statsByRange[range] = modeStats;
        }
    }

    public void ResetStatsOfMode(GameMode mode)
    {
        EnsureStatsMap();

        foreach (var range in AllRanges)
            statsByRange[range][mode] = PlayerStats.CreateForMode(id, mode);
    }

    public void ApplyGameStats(Game game)
    {
        if (game == null || !game.GetPlayerIDs().Contains(id))
            return;

        if (!game.GetPlayerStats().TryGetValue(id, out var stats))
            return;

        ApplyToRanges(game, stats, game.GetTotalPenaltyCost(id), includeAllTime: true);
    }

    public void ApplyTimebasedGameStats(Game game)
    {
        if (game == null || !game.GetPlayerIDs().Contains(id))
            return;

        if (!game.GetPlayerStats().TryGetValue(id, out var stats))
            return;

        ApplyToRanges(game, stats, game.GetTotalPenaltyCost(id), includeAllTime: false);
    }

    public void ApplyTimebasedGameStatsFromSummary(GameSummary summary, PlayerStats stats, float penaltyCost)
    {
        if (summary == null || stats == null || !summary.PlayerIds.Contains(id))
            return;

        ApplyToRangesFromSummary(summary, stats, penaltyCost, includeAllTime: false);
    }

    public void ApplyGameStatsFromSummary(GameSummary summary, PlayerStats stats, float penaltyCost)
    {
        if (summary == null || stats == null || !summary.PlayerIds.Contains(id))
            return;

        ApplyToRangesFromSummary(summary, stats, penaltyCost, includeAllTime: true);
    }

    public void RemoveGameStatsFromSummary(GameSummary summary, PlayerStats stats)
    {
        if (summary == null || stats == null || !summary.PlayerIds.Contains(id))
            return;

        RemoveFromRangesFromSummary(summary, stats);
    }

    public void RemoveGameStats(Game game)
    {
        if (game == null || !game.GetPlayerIDs().Contains(id))
            return;

        if (!game.GetPlayerStats().TryGetValue(id, out var stats))
            return;

        RemoveFromRanges(game, stats);
    }

    private void ApplyToRanges(Game game, PlayerStats stats, float penaltyCost, bool includeAllTime)
    {
        GameMode mode = game.GetGameMode();

        if (includeAllTime)
            GetModeStats(mode, StatisticsRange.AllTime).AddGameStat(stats, game.GetID(), penaltyCost);

        foreach (var range in TimeRanges)
        {
            if (IsInRange(game, range))
                GetModeStats(mode, range).AddGameStat(stats, game.GetID(), penaltyCost);
        }
    }

    private void ApplyToRangesFromSummary(GameSummary summary, PlayerStats stats, float penaltyCost, bool includeAllTime)
    {
        GameMode mode = summary.GameMode;

        if (includeAllTime)
            GetModeStats(mode, StatisticsRange.AllTime).AddGameStat(stats, summary.Id, penaltyCost);

        foreach (var range in TimeRanges)
        {
            if (IsInRangeFromSummary(summary, range))
                GetModeStats(mode, range).AddGameStat(stats, summary.Id, penaltyCost);
        }
    }

    private void RemoveFromRanges(Game game, PlayerStats stats)
    {
        GameMode mode = game.GetGameMode();

        GetModeStats(mode, StatisticsRange.AllTime).RemoveGameStat(stats, game.GetID());

        foreach (var range in TimeRanges)
        {
            if (IsInRange(game, range))
                GetModeStats(mode, range).RemoveGameStat(stats, game.GetID());
        }
    }

    private void RemoveFromRangesFromSummary(GameSummary summary, PlayerStats stats)
    {
        GameMode mode = summary.GameMode;

        GetModeStats(mode, StatisticsRange.AllTime).RemoveGameStat(stats, summary.Id);

        foreach (var range in TimeRanges)
        {
            if (IsInRangeFromSummary(summary, range))
                GetModeStats(mode, range).RemoveGameStat(stats, summary.Id);
        }
    }

    private bool IsInRange(Game game, StatisticsRange range)
    {
        DateTime? gameDateNullable = game.GetFinishedAt();

        if (!gameDateNullable.HasValue)
            return false;

        return IsInRange(gameDateNullable.Value, range);
    }

    private bool IsInRangeFromSummary(GameSummary summary, StatisticsRange range)
    {
        if (!summary.FinishedAt.HasValue)
            return false;

        return IsInRange(summary.FinishedAt.Value, range);
    }

    private static bool IsInRange(DateTime gameDate, StatisticsRange range)
    {
        DateTime now = DateTime.Now;

        switch (range)
        {
            case StatisticsRange.Today:
                return gameDate.Date == now.Date;

            case StatisticsRange.ThisWeek:
            {
                DateTime monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
                return gameDate >= monday;
            }

            case StatisticsRange.ThisMonth:
                return gameDate.Month == now.Month && gameDate.Year == now.Year;

            case StatisticsRange.ThisYear:
                return gameDate.Year == now.Year;

            case StatisticsRange.AllTime:
                return true;

            default:
                return false;
        }
    }

    internal void SetSetupStartPosition(int setupStartPosition)
    {
        this.setupStartPosition = setupStartPosition;
    }

    internal int GetSetupStartPosition() => setupStartPosition;

    public PlayerStats GetModeStats(GameMode mode, StatisticsRange range)
    {
        EnsureStatsMap();
        EnsureRange(range);

        if (statsByRange[range].TryGetValue(mode, out var stats) && stats != null)
            return stats;

        stats = PlayerStats.CreateForMode(id, mode);
        statsByRange[range][mode] = stats;
        return stats;
    }

    public PlayerStats GetCombinedStats(StatisticsRange range)
    {
        var x01 = GetModeStats(GameMode.X01, range);
        var cricket = GetModeStats(GameMode.Cricket, range);
        var atc = GetModeStats(GameMode.ATC, range);

        var combined = new PlayerStats(id);

        foreach (var s in new[] { x01, cricket, atc })
        {
            if (s == null)
                continue;

            combined.gameCount += s.gameCount;
            combined.gamesWon += s.gamesWon;

            combined.totalLegsCount += s.totalLegsCount;
            combined.totalLegsWon += s.totalLegsWon;
            combined.totalSetsCount += s.totalSetsCount;
            combined.totalSetsWon += s.totalSetsWon;

            combined.wallCount += s.wallCount;
            combined.ceilingCount += s.ceilingCount;
            combined.allMissCount += s.allMissCount;
            combined.tripleOnesCount += s.tripleOnesCount;
            combined.tripleDigitCount += s.tripleDigitCount;
            combined.lostGame += s.lostGame;

            combined.totalThrowsCount += s.totalThrowsCount;
            combined.totalDoubleCount += s.totalDoubleCount;
            combined.totalTripleCount += s.totalTripleCount;

            combined.customPenaltyCost += s.customPenaltyCost;

            if (s.customPenaltyCounts != null)
            {
                foreach (var kvp in s.customPenaltyCounts)
                {
                    if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                        continue;

                    combined.customPenaltyCounts.TryGetValue(kvp.Key, out int current);
                    combined.customPenaltyCounts[kvp.Key] = current + kvp.Value;
                }
            }

            if (s.appliedGameIds != null)
            {
                foreach (var gid in s.appliedGameIds)
                {
                    if (!combined.appliedGameIds.Contains(gid))
                        combined.appliedGameIds.Add(gid);
                }
            }

            if (s.gamePenaltyCosts != null)
            {
                foreach (var kvp in s.gamePenaltyCosts)
                {
                    if (!combined.gamePenaltyCosts.ContainsKey(kvp.Key))
                        combined.gamePenaltyCosts[kvp.Key] = kvp.Value;
                }
            }
        }

        combined.totalPenaltyCost = 0f;
        foreach (var cost in combined.gamePenaltyCosts.Values)
            combined.totalPenaltyCost += cost;

        combined.totalLegWinRate = combined.totalLegsCount == 0
            ? 0f
            : (float)Math.Round((float)combined.totalLegsWon / combined.totalLegsCount, 2);

        combined.totalSetWinRate = combined.totalSetsCount == 0
            ? 0f
            : (float)Math.Round((float)combined.totalSetsWon / combined.totalSetsCount, 2);

        combined.doublePercentage = combined.totalThrowsCount == 0
            ? 0f
            : (float)Math.Round((float)combined.totalDoubleCount / combined.totalThrowsCount, 2);

        combined.triplePercentage = combined.totalThrowsCount == 0
            ? 0f
            : (float)Math.Round((float)combined.totalTripleCount / combined.totalThrowsCount, 2);

        return combined;
    }

    private void EnsureStatsMap()
    {
        statsByRange ??= new Dictionary<StatisticsRange, Dictionary<GameMode, PlayerStats>>();
    }

    private void EnsureRange(StatisticsRange range)
    {
        if (!statsByRange.ContainsKey(range))
            statsByRange[range] = new Dictionary<GameMode, PlayerStats>();
    }
}
