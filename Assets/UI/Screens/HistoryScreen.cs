using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class HistoryScreen : MonoBehaviour, IUIScreen
{
    [Header("Handler")]
    public AppHandler appHandler;
    public WindowHandler windowHandler;
    public SwipeMenu swipeMenu;
    public List<UIScreen> popups;

    [Header("Pages")]
    public Transform allGamesParent;
    public Transform x01GamesParent;
    public Transform cricketGamesParent;
    public Transform atcGamesParent;

    public HistoryItem prefab;
    public GameObject prefabHeadline;

    [Header("Performance")]
    [Tooltip("Wie viele Items beim ersten Aufbau eines Tabs sofort (gechunkt) erstellt werden. Rest per Infinite Scroll.")]
    [SerializeField] private int initialPageSize = 24;
    [Tooltip("Wie viele Items pro Frame erstellt werden. Kleiner = flüssiger, aber Aufbau dauert länger.")]
    [SerializeField] private int itemsPerFrame = 4;
    [Tooltip("Ab welcher Scroll-Position (0=unten, 1=oben) die nächste Seite nachgeladen wird.")]
    [SerializeField] private float infiniteScrollThreshold = 0.15f;

    // Tab index: 0=All, 1=X01, 2=Cricket, 3=ATC (matches ConfirmDelete mapping + SwipeMenu pages)
    private class TabState
    {
        public Transform parent;
        public GameMode? mode;
        public List<GameSummary> source = new();
        public List<HistoryItem> items = new();
        public Dictionary<Guid, HistoryItem> lookup = new();
        public GameObject headline;
        public int builtCount;
        public bool buildRequested;
        public bool isBuilding;
        public Coroutine buildRoutine;
        public ScrollRect scrollRect;
        public UnityAction<Vector2> scrollListener;
        public string emptyKey;
        public string emptyFallback;
    }

    private TabState[] tabs;

    private readonly Stack<HistoryItem> itemPool = new Stack<HistoryItem>();
    private readonly HashSet<HistoryItem> pooledItems = new HashSet<HistoryItem>();
    private readonly Dictionary<Guid, string> playerNameCache = new Dictionary<Guid, string>();
    private List<GameSummary> cachedSummaries = new List<GameSummary>();

    private HashSet<Guid> existingGameIds = new HashSet<Guid>();

    private int deleteIndex;

    private void Start()
    {
        InitTabs();
        SnapshotSummaries();
        EnsurePlayerNameCache(cachedSummaries);
        HookScrollRects();
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

        if (swipeMenu != null)
        {
            swipeMenu.OnPageChanged += HandleSwipePageChanged;
            EnsureTabBuilt(Mathf.Clamp(swipeMenu.GetCurrentPage(), 0, tabs.Length - 1));
        }
        else
        {
            // Fallback without SwipeMenu: only all-tab immediately, rest lazy if needed.
            EnsureTabBuilt(0);
        }

        appHandler.OnDeleteGame += HandleDeleteGame;
        appHandler.OnAddGame += HandleAddGame;
        appHandler.OnDeleteGamesOfMode += HandleDeleteGamesOfMode;
        appHandler.OnAllGamesDeleted += HandleAllGamesDeleted;
    }

    // =========================================================
    // BUILD (lazy + chunked + infinite scroll)
    // =========================================================

    private void InitTabs()
    {
        tabs = new TabState[4];
        tabs[0] = new TabState
        {
            parent = allGamesParent, mode = null,
            emptyKey = "No_games_available", emptyFallback = "No games available"
        };
        tabs[1] = new TabState
        {
            parent = x01GamesParent, mode = GameMode.X01,
            emptyKey = "No_games_available_x01", emptyFallback = "No X01 games available"
        };
        tabs[2] = new TabState
        {
            parent = cricketGamesParent, mode = GameMode.Cricket,
            emptyKey = "No_games_available_cricket", emptyFallback = "No Cricket games available"
        };
        tabs[3] = new TabState
        {
            parent = atcGamesParent, mode = GameMode.ATC,
            emptyKey = "No_games_available_atc", emptyFallback = "No Around the Clock games available"
        };
    }

    private void SnapshotSummaries()
    {
        var loadedSummaries = appHandler.GetGameSummaries() ?? new List<GameSummary>();
        cachedSummaries = new List<GameSummary>(loadedSummaries.Count);
        existingGameIds = new HashSet<Guid>();

        foreach (var summary in loadedSummaries)
        {
            if (summary == null || !existingGameIds.Add(summary.Id))
                continue;

            cachedSummaries.Add(summary);
        }

        // Own lists per tab (no shared references), so
        // Insert/remove in traders do not double act.
        tabs[0].source = new List<GameSummary>(cachedSummaries);
        tabs[1].source = cachedSummaries.Where(s => s.GameMode == GameMode.X01).ToList();
        tabs[2].source = cachedSummaries.Where(s => s.GameMode == GameMode.Cricket).ToList();
        tabs[3].source = cachedSummaries.Where(s => s.GameMode == GameMode.ATC).ToList();
    }

    private void EnsurePlayerNameCache(IEnumerable<GameSummary> summaries)
    {
        if (appHandler == null)
            return;

        foreach (var summary in summaries)
        {
            if (summary?.PlayerIds == null)
                continue;
            foreach (var pid in summary.PlayerIds)
            {
                if (playerNameCache.ContainsKey(pid))
                    continue;
                var player = appHandler.GetPlayerByID(pid);
                playerNameCache[pid] = player != null ? player.GetName() : "?";
            }
        }
    }

    private string ResolvePlayerName(Guid playerId)
    {
        if (playerNameCache.TryGetValue(playerId, out var cached))
            return cached;

        var player = appHandler != null ? appHandler.GetPlayerByID(playerId) : null;
        var name = player != null ? player.GetName() : "?";
        playerNameCache[playerId] = name;
        return name;
    }

    private void HookScrollRects()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            if (tab.parent == null)
                continue;

            // The nearest ScrollRect is the vertical tab scroller
            // (not the outer horizontal SwipeMenu scroller).
            ScrollRect found = null;
            if (swipeMenu != null && swipeMenu.scrollRect != null)
            {
                var all = tab.parent.GetComponentsInParent<ScrollRect>(true);
                foreach (var sr in all)
                {
                    if (sr != swipeMenu.scrollRect)
                    {
                        found = sr;
                        break;
                    }
                }
            }
            else
            {
                found = tab.parent.GetComponentInParent<ScrollRect>();
            }

            if (found == null || found == swipeMenu?.scrollRect)
                continue;

            tab.scrollRect = found;
            int captured = i;
            tab.scrollListener = _ => OnTabScroll(captured);
            found.onValueChanged.AddListener(tab.scrollListener);
        }
    }

    private void HandleSwipePageChanged(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= tabs.Length)
            return;
        EnsureTabBuilt(pageIndex);
    }

    /// <summary>
    /// True, once the initial visible tab is set up (rest runs)
    /// by Infinite Scroll if needed. BootLoaderwaits for this before the
    /// The loading screen is hidden.
    /// </summary>
    public bool IsInitialBuildDone
    {
        get
        {
            if (tabs == null || tabs.Length == 0)
                return true;

            int page = 0;
            if (swipeMenu != null)
                page = Mathf.Clamp(swipeMenu.GetCurrentPage(), 0, tabs.Length - 1);

            var tab = tabs[page];
            if (tab == null || tab.parent == null)
                return true;

            return tab.buildRequested && !tab.isBuilding && tab.buildRoutine == null
                && tab.builtCount >= Mathf.Min(tab.source.Count, initialPageSize);
        }
    }

    private void EnsureTabBuilt(int tabIndex)
    {
        var tab = tabs[tabIndex];
        if (tab.buildRequested || tab.parent == null)
            return;

        tab.buildRequested = true;
        tab.buildRoutine = StartCoroutine(BuildTabIncremental(tabIndex, initialPageSize));
    }

    private void LoadMore(int tabIndex, int pageSize)
    {
        var tab = tabs[tabIndex];
        if (!tab.buildRequested || tab.isBuilding)
            return;
        if (tab.builtCount >= tab.source.Count)
            return;

        tab.buildRoutine = StartCoroutine(BuildTabIncremental(tabIndex, pageSize));
    }

    private void OnTabScroll(int tabIndex)
    {
        var tab = tabs[tabIndex];
        if (tab.scrollRect == null || tab.isBuilding)
            return;
        if (tab.builtCount >= tab.source.Count)
            return;

        // At 1 you are at the top, at 0 at the bottom.
        if (tab.scrollRect.verticalNormalizedPosition <= infiniteScrollThreshold)
            LoadMore(tabIndex, initialPageSize);
    }

    private IEnumerator BuildTabIncremental(int tabIndex, int pageSize)
    {
        var tab = tabs[tabIndex];
        tab.isBuilding = true;

        // First frame of transition (fade) not blocked.
        yield return null;

        DisableLayoutGroup(tab.parent);

        try
        {
            int target = Math.Min(tab.source.Count, tab.builtCount + Math.Max(1, pageSize));
            int createdThisFrame = 0;

            // Note: Source may change during build
            // (Add/Delete during background buildup).
            // Check against tab.source.Count, not just against target.
            while (tab.builtCount < target && tab.builtCount < tab.source.Count)
            {
                var summary = tab.source[tab.builtCount];
                CreatePooledItem(tab, summary);
                tab.builtCount++;
                createdThisFrame++;

                if (createdThisFrame >= Math.Max(1, itemsPerFrame))
                {
                    createdThisFrame = 0;
                    yield return null;
                }
            }

            RefreshEmptyHeadline(tab);
        }
        finally
        {
            EnableLayoutGroup(tab.parent);
        }

        tab.isBuilding = false;
        tab.buildRoutine = null;

        // Set a frame, then check if the content
        // is not yet scrollable (small list) -> reload directly.
        yield return null;
        if (tab.builtCount < tab.source.Count && ShouldAutoFill(tab))
            LoadMore(tabIndex, initialPageSize);
    }

    private bool ShouldAutoFill(TabState tab)
    {
        if (tab.scrollRect == null)
            return true;

        try
        {
            var content = tab.parent as RectTransform;
            var viewport = tab.scrollRect.viewport != null
                ? tab.scrollRect.viewport
                : tab.scrollRect.transform as RectTransform;
            if (content == null || viewport == null)
                return false;

            return content.rect.height <= viewport.rect.height + 50f;
        }
        catch
        {
            return false;
        }
    }

    private HistoryItem CreatePooledItem(TabState tab, GameSummary summary, bool insertAtTop = false)
    {
        if (tab.lookup.TryGetValue(summary.Id, out var existingItem))
            return existingItem;

        existingGameIds.Add(summary.Id);

        var item = GetPooledItem(tab.parent);
        item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked, ResolvePlayerName);

        if (insertAtTop)
        {
            item.transform.SetAsFirstSibling();
            tab.items.Insert(0, item);
        }
        else
        {
            tab.items.Add(item);
        }

        tab.lookup[summary.Id] = item;
        return item;
    }

    private HistoryItem GetPooledItem(Transform parent)
    {
        while (itemPool.Count > 0)
        {
            var pooled = itemPool.Pop();
            if (pooled == null)
                continue;
            pooledItems.Remove(pooled);
            pooled.transform.SetParent(parent, false);
            pooled.transform.SetAsLastSibling();
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        return Instantiate(prefab, parent);
    }

    private void ReleaseItem(HistoryItem item)
    {
        if (item == null || !pooledItems.Add(item))
            return;
        item.PrepareForPool();
        item.gameObject.SetActive(false);
        itemPool.Push(item);
    }

    private void RefreshEmptyHeadline(TabState tab)
    {
        if (tab.source.Count == 0 && tab.headline == null)
            tab.headline = CreateHeadline(tab.parent, tab.emptyKey, tab.emptyFallback);
        else if (tab.source.Count > 0 && tab.headline != null)
            RemoveHeadline(ref tab.headline);
    }

    private GameObject CreateHeadline(Transform parent, string localizationKey, string fallbackText)
    {
        if (prefabHeadline == null || parent == null)
            return null;

        var go = Instantiate(prefabHeadline, parent);
        var tmp = go.GetComponent<TMP_Text>();

        if (tmp != null)
        {
            // Immediately meaningful text, so that the prefab default ("title") is never written.
            tmp.text = fallbackText;
            tmp.raycastTarget = false;

            var localizeEvent = go.GetComponent<LocalizeStringEvent>()
                ?? go.AddComponent<LocalizeStringEvent>();

            // Attach listeners BEFORE setting the reference, otherwise the
            // (Asynchronous) update is lost.
            localizeEvent.OnUpdateString.RemoveAllListeners();
            localizeEvent.OnUpdateString.AddListener(localizedValue =>
            {
                if (!string.IsNullOrEmpty(localizedValue))
                    tmp.text = localizedValue;
            });
            localizeEvent.StringReference = new LocalizedString("LocalizationTable", localizationKey);
            localizeEvent.RefreshString();
        }

        return go;
    }

    private void DisableLayoutGroup(Transform parent)
    {
        if (parent == null) return;
        var layout = parent.GetComponent<LayoutGroup>();
        if (layout != null)
            layout.enabled = false;
    }

    private void EnableLayoutGroup(Transform parent)
    {
        if (parent == null) return;
        var layout = parent.GetComponent<LayoutGroup>();
        if (layout != null)
            layout.enabled = true;
    }

    private void RemoveHeadline(ref GameObject headline)
    {
        if (headline != null)
        {
            Destroy(headline);
            headline = null;
        }
    }

    // =========================================================
    // INTERACTION
    // =========================================================

    private void OnGameClicked(Guid gameId)
    {
        appHandler.SetSelectedGame(null);
        appHandler.SetLastClickedGameId(gameId);
        windowHandler.GoTo(ScreenId.GameDetail);
    }

    public void OnClickDeleteButton()
    {
        deleteIndex = swipeMenu != null ? swipeMenu.GetCurrentPage() : 0;
        windowHandler.ShowPopup(popups[deleteIndex]);
    }

    public void AbortDelete()
    {
        windowHandler.HidePopup();
    }

    public void ConfirmDelete()
    {
        bool deleted;

        if (deleteIndex == 0)
        {
            deleted = appHandler.DeleteAllGames();
        }
        else if (deleteIndex == 1)
        {
            deleted = appHandler.DeleteGamesOfMode(GameMode.X01);
        }
        else if (deleteIndex == 2)
        {
            deleted = appHandler.DeleteGamesOfMode(GameMode.Cricket);
        }
        else if (deleteIndex == 3)
        {
            deleted = appHandler.DeleteGamesOfMode(GameMode.ATC);
        }
        else
        {
            Debug.Log("[HistoryScreen] Unbekannter Index beim Löschen!");
            return;
        }

        if (!deleted)
        {
            Debug.LogError("[HistoryScreen] Löschen fehlgeschlagen.");
            return;
        }

        windowHandler.HidePopup();
    }

    private TabState TabForMode(GameMode mode)
    {
        return mode switch
        {
            GameMode.X01 => tabs[1],
            GameMode.Cricket => tabs[2],
            GameMode.ATC => tabs[3],
            _ => tabs[1]
        };
    }

    private void HandleDeleteGame(GameMode mode, Guid gameId, List<Guid> _ = null)
    {
        existingGameIds.Remove(gameId);
        cachedSummaries.RemoveAll(s => s.Id == gameId);

        var allTab = tabs[0];
        var modeTab = TabForMode(mode);

        DisableLayoutGroup(allTab.parent);
        DisableLayoutGroup(modeTab.parent);

        try
        {
            RemoveGameFromTab(allTab, gameId);
            RemoveGameFromTab(modeTab, gameId);

            RefreshEmptyHeadline(allTab);
            RefreshEmptyHeadline(modeTab);
        }
        finally
        {
            EnableLayoutGroup(allTab.parent);
            EnableLayoutGroup(modeTab.parent);
        }
    }

    private void RemoveGameFromTab(TabState tab, Guid gameId)
    {
        int sourceIndex = tab.source.FindIndex(s => s.Id == gameId);
        if (sourceIndex < 0)
            return;

        tab.source.RemoveAt(sourceIndex);

        if (sourceIndex < tab.builtCount)
            tab.builtCount = Math.Max(0, tab.builtCount - 1);

        if (tab.lookup.TryGetValue(gameId, out var item))
        {
            tab.items.Remove(item);
            tab.lookup.Remove(gameId);
            ReleaseItem(item);
        }
    }

    private void HandleDeleteGamesOfMode(GameMode mode, List<Guid> _ = null)
    {
        var modeTab = TabForMode(mode);
        var allTab = tabs[0];

        StopTabBuild(allTab);
        StopTabBuild(modeTab);

        DisableLayoutGroup(allTab.parent);
        DisableLayoutGroup(modeTab.parent);

        try
        {
            var idsToRemove = new HashSet<Guid>(modeTab.source.Select(s => s.Id));

            cachedSummaries.RemoveAll(s => s.GameMode == mode);
            foreach (var id in idsToRemove)
                existingGameIds.Remove(id);

            // Clear mode tab completely (into the pool).
            foreach (var item in modeTab.items)
                ReleaseItem(item);
            modeTab.items.Clear();
            modeTab.lookup.Clear();
            modeTab.source.Clear();
            modeTab.builtCount = 0;
            modeTab.buildRequested = true; // empty state is final, no rebuild necessary
            if (modeTab.headline == null)
                modeTab.headline = CreateHeadline(modeTab.parent, modeTab.emptyKey, modeTab.emptyFallback);

            // All-Tab: remove only the items of this mode, keep the rest.
            foreach (var id in idsToRemove.ToList())
                RemoveGameFromTab(allTab, id);

            RefreshEmptyHeadline(allTab);

            // If the all-tab was stopped in the middle of the chunk setup,
            //The rest will be resumed (otherwise he will remain partly built).
            if (allTab.buildRequested && !allTab.isBuilding && allTab.builtCount < allTab.source.Count)
                allTab.buildRoutine = StartCoroutine(BuildTabIncremental(0, initialPageSize));
        }
        finally
        {
            EnableLayoutGroup(allTab.parent);
            EnableLayoutGroup(modeTab.parent);
        }
    }

    private void HandleAllGamesDeleted()
    {
        foreach (var tab in tabs)
            StopTabBuild(tab);

        foreach (var tab in tabs)
            DisableLayoutGroup(tab.parent);

        try
        {
            cachedSummaries.Clear();
            existingGameIds.Clear();

            foreach (var tab in tabs)
            {
                foreach (var item in tab.items)
                    ReleaseItem(item);
                tab.items.Clear();
                tab.lookup.Clear();
                tab.source.Clear();
                tab.builtCount = 0;
                tab.buildRequested = true; // The empty state is final
                if (tab.headline == null)
                    tab.headline = CreateHeadline(tab.parent, tab.emptyKey, tab.emptyFallback);
            }
        }
        finally
        {
            foreach (var tab in tabs)
                EnableLayoutGroup(tab.parent);
        }
    }

    private void StopTabBuild(TabState tab)
    {
        if (tab.buildRoutine != null)
        {
            StopCoroutine(tab.buildRoutine);
            tab.buildRoutine = null;
        }
        tab.isBuilding = false;
    }

    private void HandleAddGame(Game game)
    {
        if (game == null) return;

        var summary = appHandler.GetGameSummary(game.GetID());
        if (summary == null) return;

        EnsurePlayerNameCache(new[] { summary });
        // Keep names fresh (e.g. after rename before last save).
        foreach (var pid in summary.PlayerIds)
        {
            var player = appHandler.GetPlayerByID(pid);
            if (player != null)
                playerNameCache[pid] = player.GetName();
        }

        var allTab = tabs[0];
        var modeTab = TabForMode(summary.GameMode);

        if (existingGameIds.Contains(summary.Id))
        {
            // Already present -> update and move up.
            MoveSourceToTop(cachedSummaries, summary.Id);

            DisableLayoutGroup(allTab.parent);
            DisableLayoutGroup(modeTab.parent);

            try
            {
                MoveSummaryToTop(allTab, summary);
                MoveSummaryToTop(modeTab, summary);
            }
            finally
            {
                EnableLayoutGroup(allTab.parent);
                EnableLayoutGroup(modeTab.parent);
            }

            return;
        }

        existingGameIds.Add(summary.Id);
        cachedSummaries.Insert(0, summary);

        PrependSummaryToTab(allTab, summary);
        PrependSummaryToTab(modeTab, summary);
    }

    private static void MoveSourceToTop(List<GameSummary> source, Guid id)
    {
        int index = source.FindIndex(s => s.Id == id);
        if (index <= 0)
            return;
        var entry = source[index];
        source.RemoveAt(index);
        source.Insert(0, entry);
    }

    private void MoveSummaryToTop(TabState tab, GameSummary summary)
    {
        if (tab == null || tab.parent == null)
            return;

        int sourceIndex = tab.source.FindIndex(s => s.Id == summary.Id);

        if (tab.lookup.TryGetValue(summary.Id, out var item))
        {
            item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked, ResolvePlayerName);

            if (sourceIndex > 0)
            {
                tab.source.RemoveAt(sourceIndex);
                tab.source.Insert(0, summary);
            }
            else if (sourceIndex < 0)
            {
                tab.source.Insert(0, summary);
            }

            item.transform.SetAsFirstSibling();
            tab.items.Remove(item);
            tab.items.Insert(0, item);
            return;
        }

        if (!tab.buildRequested)
        {
            if (sourceIndex > 0)
            {
                tab.source.RemoveAt(sourceIndex);
                tab.source.Insert(0, summary);
            }
            else if (sourceIndex < 0)
            {
                tab.source.Insert(0, summary);
            }
            return;
        }

        if (sourceIndex >= 0)
            tab.source.RemoveAt(sourceIndex);
        tab.source.Insert(0, summary);

        RemoveHeadline(ref tab.headline);
        CreatePooledItem(tab, summary, insertAtTop: true);
        tab.builtCount++;
    }

    /// <summary>
    /// Add a new summary to the top of a tab. Works even while
    /// A chunk build is running or the tab has not been built yet:
    /// As long as the tab still loads, the counter is carried consistently.
    /// </summary>
    private void PrependSummaryToTab(TabState tab, GameSummary summary)
    {
        tab.source.Insert(0, summary);

        if (!tab.buildRequested || tab.parent == null)
            return;

        DisableLayoutGroup(tab.parent);

        try
        {
            RemoveHeadline(ref tab.headline);

            CreatePooledItem(tab, summary, insertAtTop: true);
            // Manually created -> count as built so that the current
            // Chunk build will then continue correctly on the first unopened index.
            tab.builtCount++;
        }
        finally
        {
            EnableLayoutGroup(tab.parent);
        }
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

        if (swipeMenu != null)
            swipeMenu.OnPageChanged -= HandleSwipePageChanged;

        if (tabs != null)
        {
            foreach (var tab in tabs)
            {
                if (tab?.scrollRect != null && tab.scrollListener != null)
                    tab.scrollRect.onValueChanged.RemoveListener(tab.scrollListener);
            }
        }

        if (appHandler != null)
        {
            appHandler.OnDeleteGame -= HandleDeleteGame;
            appHandler.OnAddGame -= HandleAddGame;
            appHandler.OnDeleteGamesOfMode -= HandleDeleteGamesOfMode;
            appHandler.OnAllGamesDeleted -= HandleAllGamesDeleted;
        }
    }

    private void HandleLocaleChanged(Locale locale)
    {
        if (tabs == null)
            return;

        foreach (var tab in tabs)
        {
            if (tab?.items == null)
                continue;
            foreach (var item in tab.items)
            {
                if (item != null)
                    item.RefreshLocalization();
            }
        }
    }

    public void OnShow()
    {
        // Do not block: just make sure that the visible tab
        // (chunked, distributed over frames) is set up.
        if (tabs == null)
            return;

        int page = 0;
        if (swipeMenu != null)
            page = Mathf.Clamp(swipeMenu.GetCurrentPage(), 0, tabs.Length - 1);
        EnsureTabBuilt(page);
    }

    public void OnHide()
    {
        if (swipeMenu != null)
            swipeMenu.ResetView();
    }
}
