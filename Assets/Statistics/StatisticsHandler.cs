using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StatisticsHandler : MonoBehaviour, IUIScreen
{
    [Header("Inspector Components")]
    public AppHandler appHandler;
    public WindowHandler windowHandler;

    [Header("Swipe / Pages")]
    public SwipeMenu swipeMenu;
    public Transform allGamesParent;
    public Transform x01GamesParent;
    public Transform cricketGamesParent;
    public Transform atcGamesParent;

    [Header("Statistic Pages (All)")]
    public StatisticsPageAll pageAllAlltime;
    public StatisticsPageAll pageAllToday;
    public StatisticsPageAll pageAllWeek;
    public StatisticsPageAll pageAllMonth;
    public StatisticsPageAll pageAllYear;

    [Header("Statistic Pages (X01)")]
    public StatisticsPageX01 pageX01Alltime;
    public StatisticsPageX01 pageX01Today;
    public StatisticsPageX01 pageX01Week;
    public StatisticsPageX01 pageX01Month;
    public StatisticsPageX01 pageX01Year;

    [Header("Statistic Pages (Cricket)")]
    public StatisticsPageCricket pageCricketAlltime;
    public StatisticsPageCricket pageCricketToday;
    public StatisticsPageCricket pageCricketWeek;
    public StatisticsPageCricket pageCricketMonth;
    public StatisticsPageCricket pageCricketYear;

    [Header("Statistic Pages (ATC)")]
    public StatisticsPageATC pageATCAlltime;
    public StatisticsPageATC pageATCToday;
    public StatisticsPageATC pageATCWeek;
    public StatisticsPageATC pageATCMonth;
    public StatisticsPageATC pageATCYear;

    [Header("Popup Filter Players")]
    public UIScreen popup;
    public StatisticsFilterListItem prefabFilterList;
    public Transform popupParent;

    [Header("Single Selection Group Timeline")]
    public SingleSelectionGroup timelineSelectionGroup;

    // Targeting pages when switching timelines
    private Dictionary<StatisticsRange, GameObject[]> pageDictionary;

    private List<StatisticsFilterListItem> popupItems = new List<StatisticsFilterListItem>();
    private readonly HashSet<StatisticsRange> builtRanges = new HashSet<StatisticsRange>();

    private StatisticsRange currentRange = StatisticsRange.AllTime;
    private Coroutine buildRoutine;
    private Coroutine preloadRoutine;

    private static readonly StatisticsRange[] PreloadOrder =
    {
        StatisticsRange.Today,
        StatisticsRange.ThisWeek,
        StatisticsRange.ThisMonth,
        StatisticsRange.ThisYear
    };

    private void Start()
    {
        RegisterAppHandlerEvents();
        InitializePageDictionary();
        FilterByDate(StatisticsRange.AllTime);
        buildRoutine = StartCoroutine(BuildStartupSequence());
    }

    private void OnDestroy()
    {
        StopBuildCoroutines();
        UnregisterAppHandlerEvents();
    }

    public void TimeAll()
    {
        FilterByDate(StatisticsRange.AllTime);
    }

    public void TimeToday()
    {
        FilterByDate(StatisticsRange.Today);
    }

    public void TimeWeek()
    {
        FilterByDate(StatisticsRange.ThisWeek);
    }

    public void TimeMonth()
    {
        FilterByDate(StatisticsRange.ThisMonth);
    }

    public void TimeYear()
    {
        FilterByDate(StatisticsRange.ThisYear);
    }

    private void FilterByDate(StatisticsRange range)
    {
        foreach (var kvp in pageDictionary)
        {
            // Setzt true für die gewählte Range, false für alle anderen
            bool shouldActivate = (kvp.Key == range);
            
            foreach (var page in kvp.Value)
            {
                page.SetActive(shouldActivate);
            }
        }
    }

    private void StopBuildCoroutines()
    {
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        if (preloadRoutine != null)
        {
            StopCoroutine(preloadRoutine);
            preloadRoutine = null;
        }
    }

    private void RequestPriorityBuild(StatisticsRange range)
    {
        StopBuildCoroutines();
        buildRoutine = StartCoroutine(BuildRangeThenResumePreload(range));
    }

    private void RequestRebuildBuiltRanges()
    {
        var previouslyBuilt = builtRanges.ToList();
        builtRanges.Clear();
        StopBuildCoroutines();
        buildRoutine = StartCoroutine(RebuildRangesSequence(previouslyBuilt));
    }

    private IEnumerator BuildStartupSequence()
    {
        yield return BuildRangeAsync(StatisticsRange.AllTime);
        buildRoutine = null;
        preloadRoutine = StartCoroutine(PreloadRemainingRanges());
    }

    private IEnumerator BuildRangeThenResumePreload(StatisticsRange priorityRange)
    {
        yield return BuildRangeAsync(priorityRange);
        buildRoutine = null;
        preloadRoutine = StartCoroutine(PreloadRemainingRanges());
    }

    private IEnumerator RebuildRangesSequence(List<StatisticsRange> ranges)
    {
        var ordered = new List<StatisticsRange> { currentRange };
        foreach (var range in ranges)
        {
            if (range != currentRange)
                ordered.Add(range);
        }

        foreach (var range in ordered)
        {
            yield return BuildRangeAsync(range);
            yield return null;
        }

        buildRoutine = null;
        preloadRoutine = StartCoroutine(PreloadRemainingRanges());
    }

    private IEnumerator PreloadRemainingRanges()
    {
        foreach (var range in PreloadOrder)
        {
            if (!builtRanges.Contains(range))
            {
                yield return BuildRangeAsync(range);
                yield return null;
            }
        }

        preloadRoutine = null;
    }

    private IEnumerator BuildRangeAsync(StatisticsRange range)
    {
        CollectVisiblePlayerData(out var playerIds, out var playerNames);

        StatisticsPageBuildHelper.BeginBatch(
            allGamesParent, x01GamesParent, cricketGamesParent, atcGamesParent);

        try
        {
            yield return BuildAllGamesStatsForRange(range, playerIds, playerNames);
            yield return BuildX01StatsForRange(range, playerIds, playerNames);
            yield return BuildCricketStatsForRange(range, playerIds, playerNames);
            yield return BuildATCStatsForRange(range, playerIds, playerNames);
        }
        finally
        {
            StatisticsPageBuildHelper.EndBatch();
        }

        builtRanges.Add(range);
    }

    private void CollectVisiblePlayerData(out List<Guid> playerIds, out List<string> playerNames)
    {
        playerIds = GetVisiblePlayerIds();
        playerNames = new List<string>(playerIds.Count);

        foreach (Guid id in playerIds)
            playerNames.Add(appHandler.GetPlayerByID(id).GetName());
    }

    private IEnumerator BuildAllGamesStatsForRange(StatisticsRange range, List<Guid> playerIds, List<string> playerNames)
    {
        var stats = new List<GameStats>(playerIds.Count);

        foreach (Guid id in playerIds)
        {
            var player = appHandler.GetPlayerByID(id);
            stats.Add(range == StatisticsRange.AllTime
                ? GetAllModeAllTimeStatsForPlayer(player)
                : GetAllModeSpecificTimeStatsForPlayer(player, range));
        }

        yield return GetAllPage(range).ShowStatsAsync(stats, playerNames);
    }

    private IEnumerator BuildX01StatsForRange(StatisticsRange range, List<Guid> playerIds, List<string> playerNames)
    {
        var stats = new List<GameStatsX01>(playerIds.Count);

        foreach (Guid id in playerIds)
        {
            var player = appHandler.GetPlayerByID(id);
            stats.Add(range == StatisticsRange.AllTime
                ? player.GetX01Stats()
                : (GameStatsX01)player.GetTimebasedStats(range, GameMode.X01));
        }

        yield return GetX01Page(range).ShowStatsAsync(stats, playerNames);
    }

    private IEnumerator BuildCricketStatsForRange(StatisticsRange range, List<Guid> playerIds, List<string> playerNames)
    {
        var stats = new List<GameStatsCricket>(playerIds.Count);

        foreach (Guid id in playerIds)
        {
            var player = appHandler.GetPlayerByID(id);
            stats.Add(range == StatisticsRange.AllTime
                ? player.GetCricketStats()
                : (GameStatsCricket)player.GetTimebasedStats(range, GameMode.Cricket));
        }

        yield return GetCricketPage(range).ShowStatsAsync(stats, playerNames);
    }

    private IEnumerator BuildATCStatsForRange(StatisticsRange range, List<Guid> playerIds, List<string> playerNames)
    {
        var stats = new List<GameStatsATC>(playerIds.Count);

        foreach (Guid id in playerIds)
        {
            var player = appHandler.GetPlayerByID(id);
            stats.Add(range == StatisticsRange.AllTime
                ? player.GetATCStats()
                : (GameStatsATC)player.GetTimebasedStats(range, GameMode.ATC));
        }

        yield return GetATCPage(range).ShowStatsAsync(stats, playerNames);
    }

    private StatisticsPageAll GetAllPage(StatisticsRange range)
    {
        return range switch
        {
            StatisticsRange.AllTime => pageAllAlltime,
            StatisticsRange.Today => pageAllToday,
            StatisticsRange.ThisWeek => pageAllWeek,
            StatisticsRange.ThisMonth => pageAllMonth,
            StatisticsRange.ThisYear => pageAllYear,
            _ => pageAllAlltime
        };
    }

    private StatisticsPageX01 GetX01Page(StatisticsRange range)
    {
        return range switch
        {
            StatisticsRange.AllTime => pageX01Alltime,
            StatisticsRange.Today => pageX01Today,
            StatisticsRange.ThisWeek => pageX01Week,
            StatisticsRange.ThisMonth => pageX01Month,
            StatisticsRange.ThisYear => pageX01Year,
            _ => pageX01Alltime
        };
    }

    private StatisticsPageCricket GetCricketPage(StatisticsRange range)
    {
        return range switch
        {
            StatisticsRange.AllTime => pageCricketAlltime,
            StatisticsRange.Today => pageCricketToday,
            StatisticsRange.ThisWeek => pageCricketWeek,
            StatisticsRange.ThisMonth => pageCricketMonth,
            StatisticsRange.ThisYear => pageCricketYear,
            _ => pageCricketAlltime
        };
    }

    private StatisticsPageATC GetATCPage(StatisticsRange range)
    {
        return range switch
        {
            StatisticsRange.AllTime => pageATCAlltime,
            StatisticsRange.Today => pageATCToday,
            StatisticsRange.ThisWeek => pageATCWeek,
            StatisticsRange.ThisMonth => pageATCMonth,
            StatisticsRange.ThisYear => pageATCYear,
            _ => pageATCAlltime
        };
    }

    private GameStats GetAllModeAllTimeStatsForPlayer(BasePlayer player)
    {
        var sx01 = player.GetX01Stats();
        var scr = player.GetCricketStats();
        var satc = player.GetATCStats();

        int participated = 0;
        int won = 0;
        int wallCount = 0;
        int ceilingCount = 0;
        int allMissCount = 0;
        int threeOnesCount = 0;
        int schnapsCount = 0;
        int lostGame = 0;

        if (sx01 != null)
        {
            participated += sx01.gameCount;
            won += sx01.gamesWon;
            wallCount += sx01.wallCount;
            ceilingCount += sx01.ceilingCount;
            allMissCount += sx01.allMissCount;
            threeOnesCount += sx01.tripleOnesCount;
            schnapsCount += sx01.tripleDigitCount;
            lostGame += sx01.lostGame;
        }

        if (scr != null)
        {
            participated += scr.gameCount;
            won += scr.gamesWon;
            wallCount += scr.wallCount;
            ceilingCount += scr.ceilingCount;
            allMissCount += scr.allMissCount;
            threeOnesCount += scr.tripleOnesCount;
            schnapsCount += scr.tripleDigitCount;
            lostGame += scr.lostGame;
        }

        if (satc != null)
        {
            participated += satc.gameCount;
            won += satc.gamesWon;
            wallCount += satc.wallCount;
            ceilingCount += satc.ceilingCount;
            allMissCount += satc.allMissCount;
            threeOnesCount += satc.tripleOnesCount;
            schnapsCount += satc.tripleDigitCount;
            lostGame += satc.lostGame;
        }

        float totalCost = (sx01?.totalPenaltyCost ?? 0f) + (scr?.totalPenaltyCost ?? 0f) + (satc?.totalPenaltyCost ?? 0f);
        int totalLegsPlayed = (sx01?.totalLegsCount ?? 0) + (scr?.totalLegsCount ?? 0) + (satc?.totalLegsCount ?? 0);
        int totalLegsWon = (sx01?.totalLegsWon ?? 0) + (scr?.totalLegsWon ?? 0) + (satc?.totalLegsWon ?? 0);

        GameStats temp = new GameStats(player.GetID())
        {
            gameCount = participated,
            gamesWon = won,
            wallCount = wallCount,
            ceilingCount = ceilingCount,
            allMissCount = allMissCount,
            tripleOnesCount = threeOnesCount,
            tripleDigitCount = schnapsCount,
            lostGame = lostGame,
            totalPenaltyCost = totalCost,
            totalLegsCount = totalLegsPlayed,
            totalLegsWon = totalLegsWon
        };

        return temp;
    }

    private GameStats GetAllModeSpecificTimeStatsForPlayer(BasePlayer player, StatisticsRange range)
    {
        var sx01 = player.GetTimebasedStats(range, GameMode.X01);
        var scr = player.GetTimebasedStats(range, GameMode.Cricket);
        var satc = player.GetTimebasedStats(range, GameMode.ATC);

        int participated = 0;
        int won = 0;
        int wallCount = 0;
        int ceilingCount = 0;
        int allMissCount = 0;
        int threeOnesCount = 0;
        int schnapsCount = 0;
        int lostGame = 0;

        if (sx01 != null)
        {
            participated += sx01.gameCount;
            won += sx01.gamesWon;
            wallCount += sx01.wallCount;
            ceilingCount += sx01.ceilingCount;
            allMissCount += sx01.allMissCount;
            threeOnesCount += sx01.tripleOnesCount;
            schnapsCount += sx01.tripleDigitCount;
            lostGame += sx01.lostGame;
        }

        if (scr != null)
        {
            participated += scr.gameCount;
            won += scr.gamesWon;
            wallCount += scr.wallCount;
            ceilingCount += scr.ceilingCount;
            allMissCount += scr.allMissCount;
            threeOnesCount += scr.tripleOnesCount;
            schnapsCount += scr.tripleDigitCount;
            lostGame += scr.lostGame;
        }

        if (satc != null)
        {
            participated += satc.gameCount;
            won += satc.gamesWon;
            wallCount += satc.wallCount;
            ceilingCount += satc.ceilingCount;
            allMissCount += satc.allMissCount;
            threeOnesCount += satc.tripleOnesCount;
            schnapsCount += satc.tripleDigitCount;
            lostGame += satc.lostGame;
        }

        float totalCost = (sx01?.totalPenaltyCost ?? 0f) + (scr?.totalPenaltyCost ?? 0f) + (satc?.totalPenaltyCost ?? 0f);
        int totalLegsPlayed = (sx01?.totalLegsCount ?? 0) + (scr?.totalLegsCount ?? 0) + (satc?.totalLegsCount ?? 0);
        int totalLegsWon = (sx01?.totalLegsWon ?? 0) + (scr?.totalLegsWon ?? 0) + (satc?.totalLegsWon ?? 0);

        GameStats temp = new GameStats(player.GetID())
        {
            gameCount = participated,
            gamesWon = won,
            wallCount = wallCount,
            ceilingCount = ceilingCount,
            allMissCount = allMissCount,
            tripleOnesCount = threeOnesCount,
            tripleDigitCount = schnapsCount,
            lostGame = lostGame,
            totalPenaltyCost = totalCost,
            totalLegsCount = totalLegsPlayed,
            totalLegsWon = totalLegsWon
        };

        return temp;
    }


    // =========================
    // POPUP LIFECYCLE
    // =========================

    public void ShowPopup()
    {
        SetupPopup();
        windowHandler.ShowPopup(popup);
    }

    public void HidePopup()
    {
        windowHandler.HidePopup();
    }


    private void SetupPopup()
    {
        ClearPopup();
        BuildPopup();
    }

    private void BuildPopup()
    {
        foreach (BasePlayer p in appHandler.GetPlayers())
        {
            StatisticsFilterListItem item =
                Instantiate(prefabFilterList, popupParent);

            item.Initialize(
                p.IsVisibleInStatistics(),
                p.GetName(),
                p.GetID()
            );

            popupItems.Add(item);
        }
    }

    private void ClearPopup()
    {
        foreach (var item in popupItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        popupItems.Clear();
    }


    // =========================
    // USER ACTIONS
    // =========================

    public void SetPlayers()
    {
        foreach (StatisticsFilterListItem item in popupItems)
        {
            appHandler.SetPlayerVisibleInStatistics(
                item.GetPlayerID(),
                item.IsMarked()
            );
        }

        HidePopup();
        RequestRebuildBuiltRanges();
    }

    public void UnselectAll()
    {
        foreach (StatisticsFilterListItem item in popupItems)
        {
            item.Select(false);
        }
    }

    public void SelectAll()
    {
        foreach (StatisticsFilterListItem item in popupItems)
        {
            item.Select(true);
        }
    }



    private void HandleDartBotAdded(Guid botId)
    {
        RequestRebuildBuiltRanges();
    }

    private string GetPlayerName(Guid pid)
    {
        var name = appHandler != null ? appHandler.GetPlayerNameByID(pid) : null;
        return string.IsNullOrWhiteSpace(name) ? $"Player {pid}" : name;
    }

    private List<Guid> GetVisiblePlayerIds()
    {
        return appHandler.GetPlayers()
            .Where(p => p.IsVisibleInStatistics())
            .Select(p => p.GetID())
            .OrderBy(pid => GetPlayerName(pid))
            .ToList();
    }


    private void RegisterAppHandlerEvents()
    {
        if (appHandler == null)
            appHandler = FindFirstObjectByType<AppHandler>();

        appHandler.OnAddGame += HandleAddGame;
        appHandler.OnDeleteGame += HandleDeleteGame;
        appHandler.OnDeleteGamesOfMode += HandleDeleteGamesOfMode;
        appHandler.OnAllGamesDeleted += HandleDeleteAllGames;
        appHandler.OnPlayerAdded += HandleDartBotAdded;
    }

    private void UnregisterAppHandlerEvents()
    {
        if (appHandler == null)
            return;

        appHandler.OnAddGame -= HandleAddGame;
        appHandler.OnDeleteGame -= HandleDeleteGame;
        appHandler.OnDeleteGamesOfMode -= HandleDeleteGamesOfMode;
        appHandler.OnAllGamesDeleted -= HandleDeleteAllGames;
        appHandler.OnPlayerAdded -= HandleDartBotAdded;
    }

    private void HandleAddGame(Game game)
    {
        if (game == null)
            return;

        RequestRebuildBuiltRanges();
    }

    private void HandleDeleteGame(GameMode mode, Guid gameID)
    {
        RequestRebuildBuiltRanges();
    }

    private void HandleDeleteGamesOfMode(GameMode mode)
    {
        RequestRebuildBuiltRanges();
    }

    private void HandleDeleteAllGames()
    {
        RequestRebuildBuiltRanges();
    }

    private void InitializePageDictionary()
    {
        // Dictionary einmalig initialisieren
        pageDictionary = new Dictionary<StatisticsRange, GameObject[]>
        {
            { StatisticsRange.AllTime, new[] { pageAllAlltime.gameObject, pageX01Alltime.gameObject, pageCricketAlltime.gameObject, pageATCAlltime.gameObject } },
            { StatisticsRange.Today,   new[] { pageAllToday.gameObject, pageX01Today.gameObject, pageCricketToday.gameObject, pageATCToday.gameObject } },
            { StatisticsRange.ThisWeek,new[] { pageAllWeek.gameObject, pageX01Week.gameObject, pageCricketWeek.gameObject, pageATCWeek.gameObject } },
            { StatisticsRange.ThisMonth,new[] { pageAllMonth.gameObject, pageX01Month.gameObject, pageCricketMonth.gameObject, pageATCMonth.gameObject } },
            { StatisticsRange.ThisYear, new[] { pageAllYear.gameObject, pageX01Year.gameObject, pageCricketYear.gameObject, pageATCYear.gameObject } }
        };
    }

    public void OnShow()
    {

    }

    public void OnHide()
    {
        swipeMenu.ResetView();
    }
}
