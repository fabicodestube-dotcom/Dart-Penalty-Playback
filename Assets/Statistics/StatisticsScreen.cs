using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class StatisticsScreen : MonoBehaviour, IUIScreen
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

    [Header("Scroll Reset (beim Verlassen nach oben)")]
    [Tooltip("Die 4 vertikalen Modus-Scroller (ScrollRectEx aus der Szene reinziehen). Null-Einträge werden einmalig automatisch aufgelöst.")]
    public ScrollRect scrollAllGames;
    public ScrollRect scrollX01;
    public ScrollRect scrollCricket;
    public ScrollRect scrollATC;

    [Header("Statistic Pages (Live — eine pro Modus, range-neutral)")]
    public StatisticsPageAll pageAllAlltime;
    public StatisticsPageX01 pageX01Alltime;
    public StatisticsPageCricket pageCricketAlltime;
    public StatisticsPageATC pageATCAlltime;

    [Header("Popup Filter Players")]
    public UIScreen popup;
    public StatisticsFilterListItem prefabFilterList;
    public Transform popupParent;

    [Header("Popup Auto-Size (nur Liste scrollt)")]
    [Tooltip("ScrollRect auf dem 'Scroll View'-Objekt des Filter-Popups.")]
    public ScrollRect popupScrollRect;
    [Tooltip("RectTransform des 'Scroll View'-Objekts.")]
    public RectTransform popupScrollViewRect;
    [Tooltip("LayoutElement auf dem 'Scroll View'-Objekt. preferredHeight wird zur Laufzeit gesetzt.")]
    public LayoutElement popupListLayoutElement;
    [Tooltip("Listen-Content (Zeilen-Container, Child des Viewports). Falls leer, wird ScrollRect.content bzw. popupParent genommen.")]
    public RectTransform popupListContentRect;
    [Tooltip("Fenster (Parent des Scroll Views, mit VerticalLayoutGroup + ContentSizeFitter). Falls leer, wird es aus der Hierarchie gesucht.")]
    public RectTransform popupWindowRect;
    [Tooltip("Hintergrund-Dimmer des Popups (Vollbild). Dient als Referenz für die maximale Fensterhöhe. Falls leer, wird er per Name gesucht.")]
    public RectTransform popupBackgroundRect;
    [Tooltip("Vertikale Marge des Fensters gegenüber dem Hintergrund (100 oben + 100 unten). Der Hintergrund liegt im Screens-Container und ist bereits BottomBar-frei (BottomBar 175px), daher hier kein extra Abzug.")]
    public float popupWindowMarginY = 200f;
    [HideInInspector] public RectTransform popupOuterContentRect;

    [Header("Single Selection Group Timeline")]
    public SingleSelectionGroup timelineSelectionGroup;

    // Live Pages: exactly ONE set of 4 pages (one per swipe mode) is
    // permanently active and always shows the currentRange. timeline change is
    // a pure data rebind (chunked), no SetActive -toggle - deactivate
    // + Reactivating large hierarchies was the timeline lag spike.
    // The remaining 16 range pages are deactivated once and never again
    // touched (no layout, no overdraw, no rebuilds).
    private StatisticsPageAll liveAllPage;
    private StatisticsPageX01 liveX01Page;
    private StatisticsPageCricket liveCricketPage;
    private StatisticsPageATC liveATCPage;
    private bool livePagesBuilt;

    private List<StatisticsFilterListItem> popupItems = new List<StatisticsFilterListItem>();

    // Event Deltas Instead of Full Rebuild: Traders Only Mark Dirty That
    // Processing is chunked (max. PlayersPerFrame Players per frame) and
    // If the page is visible, the flags remain hidden.
    // stand and will be processed in the next OnShow — no GameObject touch
    // in the background, no SQLite read (aggregates come from BasePlayer).
    private readonly HashSet<Guid> dirtyPlayers = new HashSet<Guid>();
    private bool everythingDirty;
    private bool screenVisible;
    private DateTime lastRangeEvaluationDate = DateTime.Now.Date;
    private const int PlayersPerFrame = 6;

    // Popup Auto-Size: The window (VerticalLayoutGroup) grows with header +
    // List + Buttons. Only the list height is set:
    // LayoutElement.preferredHeight = min(formula, cap).
    // Formula: PadTop + PadBottom + N * line height + max(0, N-1) * Spacing
    // (N = Child number, line height live from first line, pad/spacing from VLG).
    // Cap: Background height (BottomBar -free, screens container minus 175) -
    // Window Margin (200 = 100 above + 100 below) - Chrome (everything except ScrollView).
    private const float PopupRowHeight = 130f;

    private bool popupIsOpen;
    private Coroutine popupScrollResetRoutine;
    private List<(Guid id, string name, bool visible)> popupBuiltState;

    private StatisticsRange currentRange = StatisticsRange.AllTime;
    private Coroutine buildRoutine;
    private Coroutine dirtyRoutine;
    private Coroutine spareRoutine;

    /// <summary>
    /// Reserve rows per table (for future new players, without Hitch).
    /// </summary>
    private const int SpareRowsPerTable = 4;

    /// <summary>
    /// True, once the 4 Live Pages are built once AND the Reserve Rows
    /// filled up (BootLoaderwaits for it before the loading screen)
    /// It is hidden.
    /// </summary>
    public bool IsInitialBuildDone => livePagesBuilt && spareRoutine == null && !SpareShortage();

    // One-time vertical scrollers (inspector fields, otherwise fallback).
    // No traversal per exit — ResetScrollPositions uses only these refs.
    private ScrollRect[] resolvedVerticalScrollers;

    private void Start()
    {
        RegisterAppHandlerEvents();
        InitializePageDictionary();
        lastRangeEvaluationDate = DateTime.Now.Date;
        screenVisible = gameObject.activeInHierarchy;
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
        // NO SetActive -Toggle more: The 4 Live Pages (one per swipe mode)
        // remain permanently active, only your data will be on the new range
        // And then there is no sign of the wickedness.
        // Deactivation/reactivation rebuild on timeline change, and
        // Swipe neighbors are always visible live.
        bool rangeChanged = currentRange != range;
        currentRange = range;

        // Still in the initial setup: redirect build to the new range.
        if (!livePagesBuilt)
        {
            if (screenVisible && rangeChanged && buildRoutine != null)
            {
                StopCoroutine(buildRoutine);
                buildRoutine = StartCoroutine(BuildStartupSequence());
            }
            return;
        }

        if (!screenVisible)
            return;

        if (rangeChanged)
        {
            MarkAllPlayersDirty();
            EnsureDirtyProcessing();
        }
    }

    private void StopBuildCoroutines()
    {
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        if (dirtyRoutine != null)
        {
            StopCoroutine(dirtyRoutine);
            dirtyRoutine = null;
        }

        if (spareRoutine != null)
        {
            StopCoroutine(spareRoutine);
            spareRoutine = null;
        }
    }

    private void StopAllBuildWork()
    {
        StopBuildCoroutines();
    }

    private IEnumerator BuildStartupSequence()
    {
        timelineSelectionGroup.Init(4);
        // Range dynamic: If the user taps the timeline before the end of the build,
        // the initial setup directly builds the desired range (see FilterByDate).
        yield return BuildLivePagesAsync(currentRange);
        yield return null;
        buildRoutine = null;
        livePagesBuilt = true;
        EnsureDirtyProcessing();
        EnsureSpareTopUp();
    }

    /// <summary>
    /// OnShow - Sequence: Check rollover, first set up live pages if necessary,
    /// No preload required — all ranks read the same
    /// In-memory aggregates, rebinds run over the queue.
    /// </summary>
    private IEnumerator OnShowWorkSequence()
    {
        if (appHandler != null && lastRangeEvaluationDate != DateTime.Now.Date)
        {
            // Start transition, then rebuild time snapshots
            // (once a day instead of per event). Live pages are then fully
            // re-incorporated.
            yield return null;
            appHandler.RebuildTimeBasedStatistics();
            lastRangeEvaluationDate = DateTime.Now.Date;
            MarkAllPlayersDirty();
        }

        if (!livePagesBuilt)
        {
            yield return BuildLivePagesAsync(currentRange);
            yield return null;
            livePagesBuilt = true;
        }

        buildRoutine = null;
        EnsureDirtyProcessing();
        EnsureSpareTopUp();
    }

    /// <summary>
    /// Processes dirty player chunks (max PlayersPerFrame per frame).
    /// Always write the currentRange on the 4 live pages.
    /// </summary>
    private IEnumerator ProcessDirtyQueue()
    {
        if (everythingDirty)
        {
            everythingDirty = false;
            if (appHandler != null)
            {
                foreach (var p in appHandler.GetPlayers())
                {
                    if (p != null)
                        dirtyPlayers.Add(p.GetID());
                }
            }
        }

        while (dirtyPlayers.Count > 0)
        {
            // Snapshot per chunk so that new events fall into the next round.
            var chunk = new List<Guid>(PlayersPerFrame);
            foreach (var pid in dirtyPlayers)
            {
                chunk.Add(pid);
                if (chunk.Count >= PlayersPerFrame)
                    break;
            }

            foreach (var pid in chunk)
            {
                dirtyPlayers.Remove(pid);
                RefreshPlayerEverywhere(pid);
            }

            yield return null;
        }

        dirtyRoutine = null;
        EnsureSpareTopUp();
    }

    /// <summary>
    /// Starts dirty processing when the page is visible and otherwise idle
    /// If a foreground build is running, its tail takes over the draining.
    /// </summary>
    private void EnsureDirtyProcessing()
    {
        if (!screenVisible || dirtyRoutine != null)
            return;
        if (dirtyPlayers.Count == 0 && !everythingDirty)
            return;
        if (buildRoutine != null)
            return;

        dirtyRoutine = StartCoroutine(ProcessDirtyQueue());
    }

    private void MarkAllPlayersDirty()
    {
        everythingDirty = true;
    }

    /// <summary>
    /// True, if any live page still needs reserve rows.
    /// </summary>
    private bool SpareShortage()
    {
        if (!livePagesBuilt)
            return false;

        return (liveAllPage != null && liveAllPage.SpareShortage(SpareRowsPerTable))
            || (liveX01Page != null && liveX01Page.SpareShortage(SpareRowsPerTable))
            || (liveCricketPage != null && liveCricketPage.SpareShortage(SpareRowsPerTable))
            || (liveATCPage != null && liveATCPage.SpareShortage(SpareRowsPerTable));
    }

    /// <summary>
    /// Fill in used-up reserve rows in the idle (only if nothing else)
    /// Keeps the Hitch guarantee for new players permanently.
    /// </summary>
    private void EnsureSpareTopUp()
    {
        if (!screenVisible || !livePagesBuilt || spareRoutine != null)
            return;
        if (buildRoutine != null || dirtyRoutine != null)
            return;
        if (!SpareShortage())
            return;

        spareRoutine = StartCoroutine(TopUpSparesAsync());
    }

    private IEnumerator TopUpSparesAsync()
    {
        if (liveAllPage != null)
            yield return liveAllPage.PreallocateSpareRowsAsync(SpareRowsPerTable);
        if (liveX01Page != null)
            yield return liveX01Page.PreallocateSpareRowsAsync(SpareRowsPerTable);
        if (liveCricketPage != null)
            yield return liveCricketPage.PreallocateSpareRowsAsync(SpareRowsPerTable);
        if (liveATCPage != null)
            yield return liveATCPage.PreallocateSpareRowsAsync(SpareRowsPerTable);

        spareRoutine = null;
    }

    /// <summary>
    /// Build the 4 Live Pages uniquely for a range (chunked).
    /// After that, all tables/charts are pooled — everything else is upserts.
    /// </summary>
    private IEnumerator BuildLivePagesAsync(StatisticsRange range)
    {
        CollectVisiblePlayerData(out var playerIds, out var playerNames);

        StatisticsPageBuildHelper.BeginBatch(
            allGamesParent, x01GamesParent, cricketGamesParent, atcGamesParent);

        try
        {
            yield return BuildAllGamesStatsForRange(range, playerIds, playerNames);
            yield return null;
            yield return BuildX01StatsForRange(range, playerIds, playerNames);
            yield return null;
            yield return BuildCricketStatsForRange(range, playerIds, playerNames);
            yield return null;
            yield return BuildATCStatsForRange(range, playerIds, playerNames);
        }
        finally
        {
            StatisticsPageBuildHelper.EndBatch();
        }
    }

    private void CollectVisiblePlayerData(out List<Guid> playerIds, out List<string> playerNames)
    {
        playerIds = GetVisiblePlayerIds();
        playerNames = new List<string>(playerIds.Count);

        foreach (Guid id in playerIds)
        {
            var player = appHandler.GetPlayerByID(id);
            playerNames.Add(player != null ? player.GetName() : GetPlayerName(id));
        }
    }

    private IEnumerator BuildAllGamesStatsForRange(
        StatisticsRange range,
        List<Guid> playerIds,
        List<string> playerNames)
    {
        // Aggregates come from BasePlayer (In-Memory, O(1) per player) —
        // No SQLite Read, no rescan of all games.
        var stats = new List<GameStats>(playerIds.Count);

        for (int i = 0; i < playerIds.Count; i++)
        {
            var player = appHandler.GetPlayerByID(playerIds[i]);
            stats.Add(player != null
                ? player.GetCombinedStats(range)
                : new GameStats(playerIds[i]));

            if ((i & 7) == 7)
                yield return null;
        }

        if (liveAllPage != null)
            yield return liveAllPage.ShowStatsAsync(stats, playerNames);
    }


    private IEnumerator BuildX01StatsForRange(
        StatisticsRange range,
        List<Guid> playerIds,
        List<string> playerNames)
    {
        var stats = new List<GameStatsX01>(playerIds.Count);

        for (int i = 0; i < playerIds.Count; i++)
        {
            var player = appHandler.GetPlayerByID(playerIds[i]);
            stats.Add(player != null
                ? player.GetModeStats(GameMode.X01, range) as GameStatsX01 ?? new GameStatsX01(playerIds[i])
                : new GameStatsX01(playerIds[i]));

            if ((i & 7) == 7)
                yield return null;
        }

        if (liveX01Page != null)
            yield return liveX01Page.ShowStatsAsync(stats, playerNames);
    }

    private IEnumerator BuildCricketStatsForRange(
        StatisticsRange range,
        List<Guid> playerIds,
        List<string> playerNames)
    {
        var stats = new List<GameStatsCricket>(playerIds.Count);

        for (int i = 0; i < playerIds.Count; i++)
        {
            var player = appHandler.GetPlayerByID(playerIds[i]);
            stats.Add(player != null
                ? player.GetModeStats(GameMode.Cricket, range) as GameStatsCricket ?? new GameStatsCricket(playerIds[i])
                : new GameStatsCricket(playerIds[i]));

            if ((i & 7) == 7)
                yield return null;
        }

        if (liveCricketPage != null)
            yield return liveCricketPage.ShowStatsAsync(stats, playerNames);
    }

    private IEnumerator BuildATCStatsForRange(
        StatisticsRange range,
        List<Guid> playerIds,
        List<string> playerNames)
    {
        var stats = new List<GameStatsATC>(playerIds.Count);

        for (int i = 0; i < playerIds.Count; i++)
        {
            var player = appHandler.GetPlayerByID(playerIds[i]);
            stats.Add(player != null
                ? player.GetModeStats(GameMode.ATC, range) as GameStatsATC ?? new GameStatsATC(playerIds[i])
                : new GameStatsATC(playerIds[i]));

            if ((i & 7) == 7)
                yield return null;
        }

        if (liveATCPage != null)
            yield return liveATCPage.ShowStatsAsync(stats, playerNames);
    }

    // =========================
    // POPUP LIFECYCLE
    // =========================
    // POPUP LIFECYCLE
    // =========================

    public void ShowPopup()
    {
        EnsurePopupRefs();

        // Rebuild lines only if the player list has changed
        // (added/deleted or visibility modified).
        // Leave lines untouched — no instantiate, no rebuild.
        if (!TryReusePopupRows())
            SetupPopup();

        popupIsOpen = true;

        // Activate first: All height measurements (Background.rect,
        // LayoutUtility) are valid only with active popup.
        // measured there were stale/0 values and the subsequent layout rebuild
        // has overwritten the scroll position again.
        if (windowHandler != null)
            windowHandler.ShowPopup(popup);
        else if (popup != null)
            popup.gameObject.SetActive(true);

        // Height + Scroll reset always – even in the Reuse path (screen size can be)
        // have changed, scroll offset is from the last opening.
        ApplyPopupListHeight();
        EnsurePopupScrollReset();
    }

    public void HidePopup()
    {
        popupIsOpen = false;

        if (popupScrollResetRoutine != null)
        {
            StopCoroutine(popupScrollResetRoutine);
            popupScrollResetRoutine = null;
        }

        ResetPopupScrollTop();

        if (windowHandler != null)
            windowHandler.HidePopup();
        else if (popup != null)
            popup.gameObject.SetActive(false);
    }


    private void SetupPopup()
    {
        ClearPopup();
        BuildPopup();
    }

    private void BuildPopup()
    {
        var players = appHandler.GetPlayers();

        foreach (BasePlayer p in players)
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

        SnapshotPopupState(players);
    }

    /// <summary>
    /// True, if the built-up lines still fit exactly to the player list
    /// (same players in same order + same names +
    /// same visibility) — then no rebuild, no rebuild.
    /// </summary>
    private bool TryReusePopupRows()
    {
        if (appHandler == null || popupParent == null || popupBuiltState == null)
            return false;

        var players = appHandler.GetPlayers();
        if (players == null
            || players.Count != popupBuiltState.Count
            || popupItems.Count != players.Count
            || popupParent.childCount != players.Count)
            return false;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null
                || p.GetID() != popupBuiltState[i].id
                || p.GetName() != popupBuiltState[i].name
                || p.IsVisibleInStatistics() != popupBuiltState[i].visible)
                return false;
        }

        return true;
    }

    private void SnapshotPopupState(List<BasePlayer> players)
    {
        popupBuiltState = new List<(Guid id, string name, bool visible)>(players.Count);
        foreach (var p in players)
            popupBuiltState.Add((p.GetID(), p.GetName(), p.IsVisibleInStatistics()));
    }

    /// <summary>
    /// Targeted attachment of a single line (e.g.bot during open popup)
    /// Added) instead of full rebuild. Height is pulled, list remains at the top.
    /// </summary>
    private void AppendPopupRow(Guid playerId)
    {
        if (appHandler == null || prefabFilterList == null)
            return;

        EnsurePopupRefs();
        if (popupParent == null)
            return;

        BasePlayer p = appHandler.GetPlayerByID(playerId);
        if (p == null)
            return;

        StatisticsFilterListItem item = Instantiate(prefabFilterList, popupParent);
        item.Initialize(p.IsVisibleInStatistics(), p.GetName(), p.GetID());
        popupItems.Add(item);

        SnapshotPopupState(appHandler.GetPlayers());
        ApplyPopupListHeight();
        ResetPopupScrollTop();
    }

    private void ClearPopup()
    {
        foreach (var item in popupItems)
        {
            if (item != null)
            {
                // Instantly remove from the layout so that no old lines
                // flash. Destroy() itself only takes effect at the end of the frame.
                item.transform.SetParent(null, false);
                Destroy(item.gameObject);
            }
        }

        popupItems.Clear();

        // If something without tracking is at popupParent (e.g.. to
        // broken off, also detach/destroy.
        if (popupParent != null)
        {
            for (int i = popupParent.childCount - 1; i >= 0; i--)
            {
                var child = popupParent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
    }

    // =========================
    // POPUP AUTO-SIZE
    // =========================

    /// <summary>
    /// ONLY adjust the list height (header + mark buttons + OK remain fixed).
    /// desired height by explicit formula (pad + N*line height + (N-1)*spacing),
    /// everything read live), capped at background level - margin - Chrome.
    /// Must run with active popup (ShowPopup activated first) — otherwise
    /// rect/LayoutUtility values stale. See EnsurePopupScrollReset for scroll reset.
    /// </summary>
    private void ApplyPopupListHeight()
    {
        if (!ArePopupSizeRefsValid())
            return;

        RebuildPopupLayouts();

        float listHeight = GetPopupListDesiredHeight();
        float maxList = GetPopupMaxListHeight();

        // TEMP-DIAG: What height was calculated from?
        try
        {
            float measured = popupListContentRect != null
                ? LayoutUtility.GetPreferredHeight(popupListContentRect)
                : -1f;
            Debug.Log($"[PopupDiag] Filter: children={popupListContentRect?.childCount ?? -1} " +
                $"rowH={GetPopupRowHeightLive():0.##} explicit={listHeight:0.##} measured={measured:0.##} max={maxList:0.##}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PopupDiag] Filter-Messung fehlgeschlagen: {e.Message}");
        }

        if (maxList <= 0f)
        {
            Debug.LogWarning($"[StatisticsScreen] Popup Auto-Size abgebrochen: maxList={maxList}, items={popupItems.Count}");
            return;
        }

        float target = Mathf.Min(listHeight, maxList);

        // Just write what changes — each setter dirtied the layout.
        if (!Mathf.Approximately(popupListLayoutElement.preferredHeight, target))
            popupListLayoutElement.preferredHeight = target;

        bool wantVertical = listHeight > maxList + 0.5f;
        if (popupScrollRect.vertical != wantVertical)
            popupScrollRect.vertical = wantVertical;

        ResetPopupScrollTop();
    }

    /// <summary>
    /// Forces synchronous layout through for content + window to allow the
    /// subsequent measurement (LayoutUtility / rect) of the freshly built
    /// Lines correspond instead of the pre-frame.
    /// </summary>
    private void RebuildPopupLayouts()
    {
        try
        {
            Canvas.ForceUpdateCanvases();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StatisticsScreen] ForceUpdateCanvases fehlgeschlagen: {e.Message}");
        }

        if (popupListContentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupListContentRect);
        if (popupWindowRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupWindowRect);
    }

    /// <summary>
    /// Sets the ScrollView synchronously up (first line under the fixed ones)
    /// Items. It's not enough when you open it — the layout rebuild after that.
    /// can move the offset again, therefore additionally
    /// EnsurePopupScrollReset (coroutine to frame end).
    /// </summary>
    private void ResetPopupScrollTop()
    {
        if (popupScrollRect == null)
            return;

        if (Mathf.Abs(popupScrollRect.verticalNormalizedPosition - 1f) > 0.001f)
        {
            popupScrollRect.StopMovement();
            popupScrollRect.normalizedPosition = Vector2.one;
        }
    }

    /// <summary>
    /// Starts the delayed scroll reset (runs until after the
    /// Layout rebuild at the frame end. Only if the popup is open.
    /// </summary>
    private void EnsurePopupScrollReset()
    {
        if (!popupIsOpen)
            return;
        if (popupScrollResetRoutine != null)
            StopCoroutine(popupScrollResetRoutine);
        popupScrollResetRoutine = StartCoroutine(PopupScrollTopAtFrameEnd());
    }

    private IEnumerator PopupScrollTopAtFrameEnd()
    {
        yield return new WaitForEndOfFrame();

        popupScrollResetRoutine = null;

        if (!popupIsOpen || popupScrollRect == null)
            yield break;

        RebuildPopupLayouts();
        ResetPopupScrollTop();
    }

    private bool ArePopupSizeRefsValid()
    {
        return popupScrollRect != null
            && popupScrollViewRect != null
            && popupListLayoutElement != null
            && popupListContentRect != null
            && popupWindowRect != null
            && popupBackgroundRect != null;
    }

    /// <summary>
    /// The desired height of the list — explicitly calculated (primary path):
    /// PadTop + PadBottom + N * line height + max(0, N-1) * Spacing.
    /// N = real child number in the content, line height live from the first line
    /// (Fallback Constant 130), Padding/Spacing live from the Content-VLG.
    /// Gaps only between lines (N=0 → only padding, N=1 → no spacing).
    /// The layout measurement (LayoutUtility ) turned out to be distance-free and
    /// serves only as a TEMP-DIAG comparison value in ApplyPopupListHeight.
    /// </summary>
    private float GetPopupListDesiredHeight()
    {
        return GetPopupFormulaHeight();
    }

    /// <summary>
    /// Live line height: preferredHeight of the first line in the content.
    /// Fallback constant (prefab root LayoutElement.preferredHeight).
    /// </summary>
    private float GetPopupRowHeightLive()
    {
        if (popupListContentRect != null)
        {
            for (int i = 0; i < popupListContentRect.childCount; i++)
            {
                var element = popupListContentRect.GetChild(i)?.GetComponent<LayoutElement>();
                if (element != null && element.preferredHeight > 0f)
                    return element.preferredHeight;
            }
        }

        return PopupRowHeight;
    }

    /// <summary>
    /// List height by formula (primary path, see GetPopupListDesiredHeight).
    /// </summary>
    private float GetPopupFormulaHeight()
    {
        int n = popupListContentRect != null
            ? popupListContentRect.childCount
            : popupItems.Count;

        float padTop = 0f, padBottom = 0f, spacing = 0f;
        if (popupListContentRect != null)
        {
            var group = popupListContentRect.GetComponent<VerticalLayoutGroup>();
            if (group != null)
            {
                padTop = group.padding.top;
                padBottom = group.padding.bottom;
                spacing = group.spacing;
            }
        }

        if (n <= 0)
            return padTop + padBottom;

        float rowH = GetPopupRowHeightLive();
        return n * rowH + padTop + padBottom + spacing * (n - 1);
    }

    /// <summary>
    /// Chrome = everything in the window EXCEPT ScrollView (headline, line,
    /// Mark buttons, ButtonPanel) incl. window padding/spacing.
    /// Uses the serialized LayoutElement values (all fixed) — none
    /// Layout measurement, therefore can be retrieved synchronously at any time.
    /// </summary>
    private float GetPopupWindowChromeHeight()
    {
        if (popupWindowRect == null)
            return -1f;

        var group = popupWindowRect.GetComponent<VerticalLayoutGroup>();
        float total = 0f;
        int counted = 0;

        for (int i = 0; i < popupWindowRect.childCount; i++)
        {
            var child = popupWindowRect.GetChild(i) as RectTransform;
            if (child == null || child == popupScrollViewRect)
                continue;
            if (!child.gameObject.activeInHierarchy)
                continue;

            float h = -1f;
            var element = child.GetComponent<LayoutElement>();
            if (element != null && element.preferredHeight >= 0f)
                h = element.preferredHeight;
            if (h < 0f)
                h = LayoutUtility.GetPreferredHeight(child);
            if (h < 0f)
                h = child.rect.height;
            if (h <= 0f)
                continue;

            total += h;
            counted++;
        }

        if (counted == 0)
            return -1f;

        if (group != null)
            total += group.padding.top + group.padding.bottom + group.spacing * counted;

        return total;
    }

    /// <summary>
    /// Cap for the ScrollView: Background Height - Window Margin - Chrome.
    /// Background is in the screens container (display minus BottomBar 175),
    /// So is already BottomBar -free — here NO extra BottomBar deduction,
    /// Margin 200 = 100 above + 100 below (analog X).
    /// Is freshly calculated from the live background height at each opening
    /// (no caching — the value depends on the screen size).
    /// </summary>
    private float GetPopupMaxListHeight()
    {
        if (popupWindowRect == null || popupBackgroundRect == null)
            return -1f;

        float bgHeight = popupBackgroundRect.rect.height;
        if (bgHeight <= 0f)
            return -1f;

        float chrome = GetPopupWindowChromeHeight();
        if (chrome < 0f)
            return bgHeight - popupWindowMarginY;

        return Mathf.Max(bgHeight - popupWindowMarginY - chrome, 0f);
    }

    private void EnsurePopupRefs()
    {
        if (popup == null)
            return;

        if (popupScrollRect == null)
            popupScrollRect = popup.GetComponentInChildren<ScrollRect>(true);

        if (popupScrollViewRect == null && popupScrollRect != null)
            popupScrollViewRect = popupScrollRect.GetComponent<RectTransform>();

        if (popupListLayoutElement == null && popupScrollViewRect != null)
            popupListLayoutElement = popupScrollViewRect.GetComponent<LayoutElement>();

        // List content: explicit field, otherwise popupParent, otherwise ScrollRect.content.
        if (popupListContentRect == null && popupParent != null)
            popupListContentRect = popupParent as RectTransform;
        if (popupListContentRect == null && popupScrollRect != null && popupScrollRect.content != null)
            popupListContentRect = popupScrollRect.content;
        if (popupParent == null && popupListContentRect != null)
            popupParent = popupListContentRect;

        // Window: explicit field, otherwise Parent of ScrollView (Window with VLG+CSF).
        if (popupWindowRect == null && popupScrollViewRect != null)
            popupWindowRect = popupScrollViewRect.parent as RectTransform;

        // Background: explicit field, otherwise search by name under the popup.
        if (popupBackgroundRect == null && popup != null)
        {
            Debug.LogWarning("[StatisticsScreen] popupBackgroundRect nicht verdrahtet; Fallback-Suche nach 'PopupFilterPlayersBackground'.", this);
            var bg = popup.transform.Find("PopupFilterPlayersBackground");
            if (bg != null)
                popupBackgroundRect = bg as RectTransform;
        }

        if (popupBackgroundRect == null && popupWindowRect != null)
        {
            var parent = popupWindowRect.parent as RectTransform;
            if (parent != null)
                popupBackgroundRect = parent as RectTransform;
        }
    }


    // =========================
    // USER ACTIONS
    // =========================

    public void SetPlayers()
    {
        if (appHandler == null)
        {
            HidePopup();
            return;
        }

        var oldVisible = new HashSet<Guid>(GetVisiblePlayerIds());

        foreach (StatisticsFilterListItem item in popupItems)
        {
            appHandler.SetPlayerVisibleInStatistics(
                item.GetPlayerID(),
                item.IsMarked()
            );
        }

        HidePopup();

        // Hide hidden players immediately (cheap: only SetActive on)
        // Iterates over the dirty queue (chunked).
        var newVisible = new HashSet<Guid>(GetVisiblePlayerIds());

        foreach (var pid in oldVisible)
        {
            if (!newVisible.Contains(pid))
                HidePlayerEverywhere(GetPlayerName(pid));
        }

        foreach (var pid in newVisible)
        {
            if (!oldVisible.Contains(pid))
                dirtyPlayers.Add(pid);
        }

        EnsureDirtyProcessing();

        SnapshotPopupState(appHandler.GetPlayers());
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
        // New player, no games yet: rowing through the dirty queue
        // (chunked, only built-up rows) instead of building up synchronously.
        dirtyPlayers.Add(botId);
        EnsureDirtyProcessing();

        // If the popup is open, attach a line instead of a full rebuild.
        if (popupIsOpen)
            AppendPopupRow(botId);
    }

    private void HandlePlayerDeleted(Guid playerId, string oldName)
    {
        dirtyPlayers.Remove(playerId);

        if (!string.IsNullOrEmpty(oldName))
            HidePlayerEverywhere(oldName);

        if (popupIsOpen)
            RefreshPopupList();
        else
            popupBuiltState = null;
    }

    private void HandlePlayerRenamed(Guid playerId, string oldName, string newName)
    {
        // Row keys are names: hide old lines, new ones over the queue
        // Then, then, it is only visible.
        if (!string.IsNullOrEmpty(oldName) && oldName != newName)
            HidePlayerEverywhere(oldName);

        dirtyPlayers.Add(playerId);
        EnsureDirtyProcessing();

        if (popupIsOpen)
            RefreshPopupList();
        else
            popupBuiltState = null;
    }

    /// <summary>
    /// Completely rebuilds the filter popup list (rarely: Delete/Rename on it)
    /// Open popup. Regular opening is TryReusePopupRows.
    /// </summary>
    private void RefreshPopupList()
    {
        if (appHandler == null)
            return;

        EnsurePopupRefs();
        ClearPopup();
        BuildPopup();
        ApplyPopupListHeight();
        EnsurePopupScrollReset();
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
        appHandler.OnPlayerDeleted += HandlePlayerDeleted;
        appHandler.OnPlayerRenamed += HandlePlayerRenamed;
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
        appHandler.OnPlayerDeleted -= HandlePlayerDeleted;
        appHandler.OnPlayerRenamed -= HandlePlayerRenamed;
    }

    private void HandleAddGame(Game game)
    {
        if (game == null || appHandler == null)
            return;

        // Unfinished games are not in any aggregation.
        if (!game.IsFinished())
            return;

        // BasePlayer - + DB aggregates are already up-to-date through the Save path —
        // Only the affected participants can be listed here via the dirty queue.
        // The game object itself carries all deltas, no reloading necessary.
        foreach (var pid in game.GetPlayerIDs())
            dirtyPlayers.Add(pid);

        EnsureDirtyProcessing();
    }

    private void HandleDeleteGame(GameMode mode, Guid gameID, List<Guid> affectedPlayerIds)
    {
        if (appHandler == null)
            return;

        // Aggregates are already subtracted through the Delete path,
        // Only row affected participants (from the event, no re-read).
        if (affectedPlayerIds != null)
        {
            foreach (var pid in affectedPlayerIds)
                dirtyPlayers.Add(pid);
        }

        EnsureDirtyProcessing();
    }

    private void HandleDeleteGamesOfMode(GameMode mode, List<Guid> affectedPlayerIds)
    {
        if (appHandler == null)
            return;

        if (affectedPlayerIds != null)
        {
            foreach (var pid in affectedPlayerIds)
                dirtyPlayers.Add(pid);
        }

        EnsureDirtyProcessing();
    }

    private void HandleDeleteAllGames()
    {
        if (appHandler == null)
            return;

        // No more games: row all players (fresh empty aggregates,
        // The queue is chunked.
        MarkAllPlayersDirty();
        EnsureDirtyProcessing();
    }

    // =========================
    // DIRECTED PLAYER UPDATES (always currentRange on the 4 live pages)
    // =========================

    /// <summary>
    /// Refresh ONE player on the 4 live pages (currentRange):
    /// Aggregates from BasePlayer (in-memory, no SQLite), lines per upsert.
    /// Only update path for Add/Delete/Mode Delete/Rename/Visibility
    /// and timeline rebinds.
    /// </summary>
    private void RefreshPlayerEverywhere(Guid playerId)
    {
        if (appHandler == null)
            return;

        var player = appHandler.GetPlayerByID(playerId);
        if (player == null)
            return;

        string name = player.GetName();
        bool visible = player.IsVisibleInStatistics();
        var range = currentRange;

        // ALL mode: combined in-memory aggregate.
        if (liveAllPage != null)
        {
            liveAllPage.UpsertPlayerRow(player.GetCombinedStats(range), name);
            liveAllPage.SetPlayerRowVisible(name, visible);
        }

        // X01 mode.
        if (liveX01Page != null)
        {
            var x01 = player.GetModeStats(GameMode.X01, range) as GameStatsX01
                ?? new GameStatsX01(playerId);
            liveX01Page.UpsertPlayerRow(x01, name);
            liveX01Page.SetPlayerRowVisible(name, visible);
        }

        // Cricket mode.
        if (liveCricketPage != null)
        {
            var cricket = player.GetModeStats(GameMode.Cricket, range) as GameStatsCricket
                ?? new GameStatsCricket(playerId);
            liveCricketPage.UpsertPlayerRow(cricket, name);
            liveCricketPage.SetPlayerRowVisible(name, visible);
        }

        // ATC mode.
        if (liveATCPage != null)
        {
            var atc = player.GetModeStats(GameMode.ATC, range) as GameStatsATC
                ?? new GameStatsATC(playerId);
            liveATCPage.UpsertPlayerRow(atc, name);
            liveATCPage.SetPlayerRowVisible(name, visible);
        }
    }

    /// <summary>
    /// Hide a player on the live pages.
    /// Don't create anything — just existing rows/charts.
    /// ONLY on the pages recalculated on which the visible
    /// The number of lines has really changed (number before!=number afterwards).
    /// </summary>
    private void HidePlayerEverywhere(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
            return;

        bool allChanged = liveAllPage != null && liveAllPage.SetPlayerRowVisible(playerName, false);
        bool x01Changed = liveX01Page != null && liveX01Page.SetPlayerRowVisible(playerName, false);
        bool cricketChanged = liveCricketPage != null && liveCricketPage.SetPlayerRowVisible(playerName, false);
        bool atcChanged = liveATCPage != null && liveATCPage.SetPlayerRowVisible(playerName, false);

        if (allChanged)
            liveAllPage.RefreshLayout();
        if (x01Changed)
            liveX01Page.RefreshLayout();
        if (cricketChanged)
            liveCricketPage.RefreshLayout();
        if (atcChanged)
            liveATCPage.RefreshLayout();
    }

    private void InitializePageDictionary()
    {
        // The AllTime instances are the permanently active live pages (one
        // The old Today/Week/Month/Year pages were made
        // deleted the scene — Timeline change is a pure data rebind,
        // No page-toggle.
        liveAllPage = pageAllAlltime;
        liveX01Page = pageX01Alltime;
        liveCricketPage = pageCricketAlltime;
        liveATCPage = pageATCAlltime;

        WirePenaltyResolvers();
    }

    /// <summary>
    /// Penalty taps freshly trigger their stats at tap time (BasePlayer
    /// + currentRange) (stale-closure fix): the build-time snapshot in the
    /// tap closure may be obsolete (upsert rewrites the row, the tap keeps the
    /// old object → popup with foreign counts/total). Resolvers read live.
    /// </summary>
    private void WirePenaltyResolvers()
    {
        if (liveAllPage != null)
            liveAllPage.penaltyStatsResolver = ResolveAllPenaltyStats;
        if (liveX01Page != null)
            liveX01Page.penaltyStatsResolver = ResolveX01PenaltyStats;
        if (liveCricketPage != null)
            liveCricketPage.penaltyStatsResolver = ResolveCricketPenaltyStats;
        if (liveATCPage != null)
            liveATCPage.penaltyStatsResolver = ResolveATCPenaltyStats;
    }

    private GameStats ResolveAllPenaltyStats(string playerName)
    {
        var player = GetPlayerByName(playerName);
        return player?.GetCombinedStats(currentRange);
    }

    private GameStats ResolveX01PenaltyStats(string playerName)
    {
        var player = GetPlayerByName(playerName);
        return player?.GetModeStats(GameMode.X01, currentRange) as GameStatsX01;
    }

    private GameStats ResolveCricketPenaltyStats(string playerName)
    {
        var player = GetPlayerByName(playerName);
        return player?.GetModeStats(GameMode.Cricket, currentRange) as GameStatsCricket;
    }

    private GameStats ResolveATCPenaltyStats(string playerName)
    {
        var player = GetPlayerByName(playerName);
        return player?.GetModeStats(GameMode.ATC, currentRange) as GameStatsATC;
    }

    private BasePlayer GetPlayerByName(string playerName)
    {
        if (appHandler == null || string.IsNullOrEmpty(playerName))
            return null;

        foreach (var p in appHandler.GetPlayers())
        {
            if (p != null && p.GetName() == playerName)
                return p;
        }

        return null;
    }

    public void OnShow()
    {
        screenVisible = true;
        RefreshLocalization();

        // Continue ongoing work (no double start).
        if (buildRoutine != null || dirtyRoutine != null)
            return;

        buildRoutine = StartCoroutine(OnShowWorkSequence());
    }

    private void RefreshLocalization()
    {
        pageAllAlltime?.RefreshLocalization();
        pageX01Alltime?.RefreshLocalization();
        pageCricketAlltime?.RefreshLocalization();
        pageATCAlltime?.RefreshLocalization();
        liveAllPage?.RefreshLocalization();
        liveX01Page?.RefreshLocalization();
        liveCricketPage?.RefreshLocalization();
        liveATCPage?.RefreshLocalization();
    }

    public void OnHide()
    {
        screenVisible = false;

        // Stop background work (flags remain, resume on OnShow).
        StopAllBuildWork();

        // Reset scroll positions (Swipe on page 0, all lists to
        // above), so that the next opening starts again from above.
        // cached references + guarded writings: no traversal, no SetActive,
        // no event — usually (even above) practically null costs.
        // Replaces the old swipeMenu.ResetView() which leaves one
        // GetComponentsInChildren traversal is used across all pages
        // (Close-Spike).
        ResetScrollPositions();
    }

    /// <summary>
    /// Sets the swipe position on page 0 and all 4 vertical mode lists
    /// Just write what really differs (each setter dirtied)
    /// ScrollRect + Layout. The 16 deactivated range pages have no
    /// They have their own scrollers and need nothing else.
    /// </summary>
    private void ResetScrollPositions()
    {
        EnsureScrollRefs();

        if (resolvedVerticalScrollers != null)
        {
            foreach (var scroller in resolvedVerticalScrollers)
            {
                if (scroller == null)
                    continue;

                scroller.velocity = Vector2.zero;

                if (Mathf.Abs(scroller.verticalNormalizedPosition - 1f) > 0.001f)
                    scroller.verticalNormalizedPosition = 1f;
            }
        }

        if (swipeMenu != null && swipeMenu.scrollRect != null)
        {
            var horizontal = swipeMenu.scrollRect;
            horizontal.velocity = Vector2.zero;

            if (Mathf.Abs(horizontal.horizontalNormalizedPosition) > 0.0005f)
                horizontal.horizontalNormalizedPosition = 0f;

            // SetPage maintains currentPage + TopBar + Indicator; fires
            // OnPageChanged (0), which does not have a subscriber on this page.
            if (swipeMenu.GetCurrentPage() != 0)
                swipeMenu.SetPage(0);
        }
    }

    /// <summary>
    /// Resets the 4 vertical scrollers once: inspector fields first,
    /// Missing entries by parent-walk from the associated mode-parent
    /// (no full traversal in operation).
    /// </summary>
    private void EnsureScrollRefs()
    {
        if (resolvedVerticalScrollers != null)
            return;

        resolvedVerticalScrollers = new ScrollRect[4];
        resolvedVerticalScrollers[0] = scrollAllGames != null
            ? scrollAllGames
            : FindVerticalScroller(allGamesParent);
        resolvedVerticalScrollers[1] = scrollX01 != null
            ? scrollX01
            : FindVerticalScroller(x01GamesParent);
        resolvedVerticalScrollers[2] = scrollCricket != null
            ? scrollCricket
            : FindVerticalScroller(cricketGamesParent);
        resolvedVerticalScrollers[3] = scrollATC != null
            ? scrollATC
            : FindVerticalScroller(atcGamesParent);
    }

    /// <summary>
    /// Next ScrollRect above the fashion parent who is NOT the horizontal
    /// Swipe scroller is (pattern like HistoryScreen.HookScrollRects).
    /// </summary>
    private ScrollRect FindVerticalScroller(Transform modeParent)
    {
        if (modeParent == null)
            return null;

        ScrollRect horizontal = swipeMenu != null ? swipeMenu.scrollRect : null;

        foreach (var scroller in modeParent.GetComponentsInParent<ScrollRect>(true))
        {
            if (scroller != null && scroller != horizontal)
                return scroller;
        }

        return null;
    }
}
