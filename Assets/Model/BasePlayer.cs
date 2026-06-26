using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public abstract class BasePlayer
{
    // =========================================================
    // IDENTIFICATION
    // =========================================================
    [JsonProperty] protected Guid id;
    [JsonProperty] protected string playerName;

    // =========================================================
    // LOGIC-FLAGS
    // =========================================================
    [JsonProperty] protected bool showInStatistics;
    [JsonProperty] protected bool deletedFlag;

    // =========================================================
    // ALL-TIME STATS
    // =========================================================
    [JsonProperty] protected GameStatsX01 x01Stats;
    [JsonProperty] protected GameStatsCricket cricketStats;
    [JsonProperty] protected GameStatsATC atcStats;


    // =========================================================
    // TIME-BASED STATS (X01)
    // =========================================================
    [JsonProperty] protected GameStatsX01 x01TodayStats;
    [JsonProperty] protected GameStatsX01 x01WeekStats;
    [JsonProperty] protected GameStatsX01 x01MonthStats;
    [JsonProperty] protected GameStatsX01 x01YearStats;

    // =========================================================
    // TIME-BASED STATS (CRICKET)
    // =========================================================
    [JsonProperty] protected GameStatsCricket cricketTodayStats;
    [JsonProperty] protected GameStatsCricket cricketWeekStats;
    [JsonProperty] protected GameStatsCricket cricketMonthStats;
    [JsonProperty] protected GameStatsCricket cricketYearStats;

    // =========================================================
    // TIME-BASED STATS (AROUND THE CLOCK)
    // =========================================================
    [JsonProperty] protected GameStatsATC atcTodayStats;
    [JsonProperty] protected GameStatsATC atcWeekStats;
    [JsonProperty] protected GameStatsATC atcMonthStats;
    [JsonProperty] protected GameStatsATC atcYearStats;


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    protected BasePlayer(Guid id, string playerName)
    {
        this.id = id;
        this.playerName = playerName;

        showInStatistics = true;
        deletedFlag = false;

        ResetAllStats();
    }


    // =========================================================
    // EDITING
    // =========================================================

    public virtual void Rename(string newName)
    {
        Debug.LogWarning($"{GetName()} kann nicht umbenannt werden (Basis-Implementierung).");
    }


    // =========================================================
    // IDENTIFICATION
    // =========================================================

    public Guid GetID() => id;

    public string GetName() => playerName;

    // =========================================================
    // VISIBILITY
    // =========================================================

    public void ShowInStatistics(bool show)
    {
        showInStatistics = show;
    }

    public bool IsVisibleInStatistics()
    {
        return showInStatistics;
    }


    // =========================================================
    // DELETION
    // =========================================================

    public void Delete()
    {
        deletedFlag = true;
    }

    public bool GotDeleted()
    {
        return deletedFlag;
    }


    // =========================================================
    // STATS
    // =========================================================

    // STATS 1: GETTER
    public GameStatsX01 GetX01Stats()
    {
        return x01Stats ??= new GameStatsX01(id, 0);
    }

    public GameStatsCricket GetCricketStats()
    {
        return cricketStats ??= new GameStatsCricket(id);
    }

    public GameStatsATC GetATCStats()
    {
        return atcStats ??= new GameStatsATC(id);
    }

    public GameStats GetTimebasedStats(StatisticsRange range, GameMode mode)
    {

        return (range, mode) switch
        {
            (StatisticsRange.Today, GameMode.X01) => x01TodayStats,
            (StatisticsRange.Today, GameMode.Cricket) => cricketTodayStats,
            (StatisticsRange.Today, GameMode.ATC) => atcTodayStats,

            (StatisticsRange.ThisWeek, GameMode.X01) => x01WeekStats,
            (StatisticsRange.ThisWeek, GameMode.Cricket) => cricketWeekStats,
            (StatisticsRange.ThisWeek, GameMode.ATC) => atcWeekStats,

            (StatisticsRange.ThisMonth, GameMode.X01) => x01MonthStats,
            (StatisticsRange.ThisMonth, GameMode.Cricket) => cricketMonthStats,
            (StatisticsRange.ThisMonth, GameMode.ATC) => atcMonthStats,

            (StatisticsRange.ThisYear, GameMode.X01) => x01YearStats,
            (StatisticsRange.ThisYear, GameMode.Cricket) => cricketYearStats,
            (StatisticsRange.ThisYear, GameMode.ATC) => atcYearStats,

            _ => null
        };
    }


    // STATS 2: RESET
    public void ResetAllStats()
    {
        // All-Time
        x01Stats = new GameStatsX01(id, 0);
        cricketStats = new GameStatsCricket(id);
        atcStats = new GameStatsATC(id);

        ResetTimeStats();
    }

    public void ResetTimeStats()
    {
        // X01
        x01TodayStats = new GameStatsX01(id, 0);
        x01WeekStats = new GameStatsX01(id, 0);
        x01MonthStats = new GameStatsX01(id, 0);
        x01YearStats = new GameStatsX01(id, 0);

        // Cricket
        cricketTodayStats = new GameStatsCricket(id);
        cricketWeekStats = new GameStatsCricket(id);
        cricketMonthStats = new GameStatsCricket(id);
        cricketYearStats = new GameStatsCricket(id);

        // ATC
        atcTodayStats = new GameStatsATC(id);
        atcWeekStats = new GameStatsATC(id);
        atcMonthStats = new GameStatsATC(id);
        atcYearStats = new GameStatsATC(id);
    }

    public void ResetStatsOfMode(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.X01:
                x01Stats = new GameStatsX01(id, 0);

                x01TodayStats = new GameStatsX01(id, 0);
                x01WeekStats = new GameStatsX01(id, 0);
                x01MonthStats = new GameStatsX01(id, 0);
                x01YearStats = new GameStatsX01(id, 0);
                break;

            case GameMode.Cricket:
                cricketStats = new GameStatsCricket(id);

                cricketTodayStats = new GameStatsCricket(id);
                cricketWeekStats = new GameStatsCricket(id);
                cricketMonthStats = new GameStatsCricket(id);
                cricketYearStats = new GameStatsCricket(id);
                break;

            case GameMode.ATC:
                atcStats = new GameStatsATC(id);

                atcTodayStats = new GameStatsATC(id);
                atcWeekStats = new GameStatsATC(id);
                atcMonthStats = new GameStatsATC(id);
                atcYearStats = new GameStatsATC(id);
                break;
        }
    }

    // STATS 3: APPLY
    public void ApplyGameStats(Game game)
    {
        if (game == null)
            return;

        if (!game.GetPlayerIDs().Contains(id))
            return;

        if (!game.GetPlayerStats().TryGetValue(id, out var stats))
            return;

        float penaltyCost = game.GetTotalPenaltyCost(id);

        switch (game.GetGameMode())
        {
            case GameMode.X01:
            {
                GameStatsX01 s = stats as GameStatsX01;

                // AllTime
                GetX01Stats().AddGameStat(s, game.GetID(), penaltyCost);

                // TimeBased
                ApplyToTimeRanges(game,
                    x01TodayStats,
                    x01WeekStats,
                    x01MonthStats,
                    x01YearStats,
                    s,
                    penaltyCost);

                break;
            }

            case GameMode.Cricket:
            {
                GameStatsCricket s = stats as GameStatsCricket;

                GetCricketStats().AddGameStat(s, game.GetID(), penaltyCost);

                ApplyToTimeRanges(game,
                    cricketTodayStats,
                    cricketWeekStats,
                    cricketMonthStats,
                    cricketYearStats,
                    s,
                    penaltyCost);

                break;
            }

            case GameMode.ATC:
            {
                GameStatsATC s = stats as GameStatsATC;

                GetATCStats().AddGameStat(s, game.GetID(), penaltyCost);

                ApplyToTimeRanges(game,
                    atcTodayStats,
                    atcWeekStats,
                    atcMonthStats,
                    atcYearStats,
                    s,
                    penaltyCost);

                break;
            }
        }
    }

    public void ApplyTimebasedGameStats(Game game)
    {
        if (game == null)
            return;

        if (!game.GetPlayerIDs().Contains(id))
            return;

        if (!game.GetPlayerStats().TryGetValue(id, out var stats))
            return;

        float penaltyCost = game.GetTotalPenaltyCost(id);

        switch (game.GetGameMode())
        {
            case GameMode.X01:
            {
                GameStatsX01 s = stats as GameStatsX01;

                // TimeBased
                ApplyToTimeRanges(game,
                    x01TodayStats,
                    x01WeekStats,
                    x01MonthStats,
                    x01YearStats,
                    s,
                    penaltyCost);

                break;
            }

            case GameMode.Cricket:
            {
                GameStatsCricket s = stats as GameStatsCricket;

                ApplyToTimeRanges(game,
                    cricketTodayStats,
                    cricketWeekStats,
                    cricketMonthStats,
                    cricketYearStats,
                    s,
                    penaltyCost);

                break;
            }

            case GameMode.ATC:
            {
                GameStatsATC s = stats as GameStatsATC;

                ApplyToTimeRanges(game,
                    atcTodayStats,
                    atcWeekStats,
                    atcMonthStats,
                    atcYearStats,
                    s,
                    penaltyCost);

                break;
            }
        }
    }

    private void ApplyToTimeRanges<T>(
    Game game,
    T today,
    T week,
    T month,
    T year,
    T stats,
    float penaltyCost)
    where T : GameStats
    {
        if (IsInRange(game, StatisticsRange.Today))
            today.AddGameStat(stats, game.GetID(), penaltyCost);

        if (IsInRange(game, StatisticsRange.ThisWeek))
            week.AddGameStat(stats, game.GetID(), penaltyCost);

        if (IsInRange(game, StatisticsRange.ThisMonth))
            month.AddGameStat(stats, game.GetID(), penaltyCost);

        if (IsInRange(game, StatisticsRange.ThisYear))
            year.AddGameStat(stats, game.GetID(), penaltyCost);
    }


    // STATS 4: REMOVE
    public void RemoveGameStats(Game game)
    {
        if (game == null)
            return;

        if (!game.GetPlayerIDs().Contains(id))
            return;

        if (!game.GetPlayerStats().TryGetValue(id, out var stats))
            return;

        switch (game.GetGameMode())
        {
            case GameMode.X01:
                GameStatsX01 sx = stats as GameStatsX01;

                GetX01Stats().RemoveGameStat(sx, game.GetID());

                RemoveFromTimeRanges(
                        game,
                        x01TodayStats,
                        x01WeekStats,
                        x01MonthStats,
                        x01YearStats,
                        sx);
            break;

            case GameMode.Cricket:
                GameStatsCricket sc = stats as GameStatsCricket;

                GetCricketStats().RemoveGameStat(sc, game.GetID());

                RemoveFromTimeRanges(
                        game,
                        cricketTodayStats,
                        cricketWeekStats,
                        cricketMonthStats,
                        cricketYearStats,
                        sc);
                break;

            case GameMode.ATC:
                GameStatsATC sa = stats as GameStatsATC;

                GetATCStats().RemoveGameStat(sa, game.GetID());

                RemoveFromTimeRanges(
                        game,
                        atcTodayStats,
                        atcWeekStats,
                        atcMonthStats,
                        atcYearStats,
                        sa);
                break;
        }
    }

    private void RemoveFromTimeRanges<T>(
        Game game,
        T today,
        T week,
        T month,
        T year,
        T stats)
        where T : GameStats
    {
        if (IsInRange(game, StatisticsRange.Today))
            today.RemoveGameStat(stats, game.GetID());

        if (IsInRange(game, StatisticsRange.ThisWeek))
            week.RemoveGameStat(stats, game.GetID());

        if (IsInRange(game, StatisticsRange.ThisMonth))
            month.RemoveGameStat(stats, game.GetID());

        if (IsInRange(game, StatisticsRange.ThisYear))
            year.RemoveGameStat(stats, game.GetID());
    }

    // =========================================================
    // DATE FILTERING
    // =========================================================

    private bool IsInRange(Game game, StatisticsRange range)
    {
        DateTime? gameDateNullable = game.GetFinishedAt();

        if (!gameDateNullable.HasValue)
            return false;

        DateTime gameDate = gameDateNullable.Value;
        DateTime now = DateTime.Now;

        switch (range)
        {
            case StatisticsRange.Today:
                return gameDate.Date == now.Date;

            case StatisticsRange.ThisWeek:
            {
                DateTime monday =
                    now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));

                return gameDate >= monday;
            }

            case StatisticsRange.ThisMonth:
                return gameDate.Month == now.Month &&
                    gameDate.Year == now.Year;

            case StatisticsRange.ThisYear:
                return gameDate.Year == now.Year;

            case StatisticsRange.AllTime:
                return true;

            default:
                return false;
        }
    }
}
