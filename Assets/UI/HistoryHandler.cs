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
using UnityEngine.UI;

public class HistoryHandler : MonoBehaviour, IUIScreen
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

    // Tab-Index: 0=All, 1=X01, 2=Cricket, 3=ATC (entspricht ConfirmDelete-Mapping + SwipeMenu-Pages)
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

        if (swipeMenu != null)
        {
            swipeMenu.OnPageChanged += HandleSwipePageChanged;
            EnsureTabBuilt(Mathf.Clamp(swipeMenu.GetCurrentPage(), 0, tabs.Length - 1));
        }
        else
        {
            // Fallback ohne SwipeMenu: nur All-Tab sofort, Rest lazy bei Bedarf.
            EnsureTabBuilt(0);
        }

        appHandler.OnDeleteGame += HandleDeleteGame;
        appHandler.OnAddGame += HandleAddGame;
        appHandler.OnDeleteGamesOfMode += HandleDeleteGamesOfMode;
        appHandler.OnAllGamesDeleted += HandleAllGamesDeleted;
    }

    // =========================================================
    // AUFBAU (lazy + gechunkt + Infinite Scroll)
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
        cachedSummaries = appHandler.GetGameSummaries() ?? new List<GameSummary>();
        existingGameIds = new HashSet<Guid>(cachedSummaries.Select(s => s.Id));

        // Eigene Listen pro Tab (keine geteilten Referenzen), damit
        // Insert/Remove in Handlern nicht doppelt wirken.
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

            // Nächstgelegener ScrollRect ist der vertikale Tab-Scroller
            // (nicht der äußere horizontale SwipeMenu-Scroller).
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
    /// True, sobald der initiale sichtbare Tab einmal aufgebaut ist (Rest läuft
    /// per Infinite Scroll bei Bedarf). Der BootLoader wartet darauf, bevor der
    /// Ladebildschirm ausgeblendet wird.
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

        // Bei 1 ist man oben, bei 0 unten.
        if (tab.scrollRect.verticalNormalizedPosition <= infiniteScrollThreshold)
            LoadMore(tabIndex, initialPageSize);
    }

    private IEnumerator BuildTabIncremental(int tabIndex, int pageSize)
    {
        var tab = tabs[tabIndex];
        tab.isBuilding = true;

        // Ersten Frame der Transition (Fade) nicht blockieren.
        yield return null;

        DisableLayoutGroup(tab.parent);

        try
        {
            int target = Math.Min(tab.source.Count, tab.builtCount + Math.Max(1, pageSize));
            int createdThisFrame = 0;

            // Hinweis: Die Source kann sich während des Builds ändern
            // (Add/Delete während Hintergrundaufbau). Daher zusätzlich
            // gegen tab.source.Count prüfen, nicht nur gegen target.
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

        // Layout einen Frame setzen lassen, dann prüfen ob der Inhalt
        // noch nicht scrollbar ist (kleine Liste) -> direkt weiterladen.
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

    private void CreatePooledItem(TabState tab, GameSummary summary)
    {
        existingGameIds.Add(summary.Id);

        var item = GetPooledItem(tab.parent);
        item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked, ResolvePlayerName);

        tab.items.Add(item);
        tab.lookup[summary.Id] = item;
    }

    private HistoryItem GetPooledItem(Transform parent)
    {
        while (itemPool.Count > 0)
        {
            var pooled = itemPool.Pop();
            if (pooled == null)
                continue;
            pooled.transform.SetParent(parent, false);
            pooled.transform.SetAsLastSibling();
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        return Instantiate(prefab, parent);
    }

    private void ReleaseItem(HistoryItem item)
    {
        if (item == null)
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
            // Sofort sinnvoller Text, damit nie das Prefab-Default ("Titel") steht.
            tmp.text = fallbackText;
            tmp.raycastTarget = false;

            var localizeEvent = go.GetComponent<LocalizeStringEvent>()
                ?? go.AddComponent<LocalizeStringEvent>();

            // Listener VOR dem Setzen der Referenz anhängen, sonst kann das
            // (asynchrone) Update verloren gehen.
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
    // INTERAKTION
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
        if (deleteIndex == 0)
        {
            appHandler.DeleteAllGames();
        }
        else if (deleteIndex == 1)
        {
            appHandler.DeleteGamesOfMode(GameMode.X01);
        }
        else if (deleteIndex == 2)
        {
            appHandler.DeleteGamesOfMode(GameMode.Cricket);
        }
        else if (deleteIndex == 3)
        {
            appHandler.DeleteGamesOfMode(GameMode.ATC);
        }
        else
        {
            Debug.Log("[HistoryHandler] Unbekannter Index beim Löschen!");
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
        tab.source.RemoveAll(s => s.Id == gameId);

        if (tab.lookup.TryGetValue(gameId, out var item))
        {
            tab.items.Remove(item);
            tab.lookup.Remove(gameId);
            ReleaseItem(item);
            tab.builtCount = Math.Max(0, tab.builtCount - 1);
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

            // Modus-Tab komplett leeren (in den Pool).
            foreach (var item in modeTab.items)
                ReleaseItem(item);
            modeTab.items.Clear();
            modeTab.lookup.Clear();
            modeTab.source.Clear();
            modeTab.builtCount = 0;
            modeTab.buildRequested = true; // leerer Zustand ist final, kein Rebuild nötig
            if (modeTab.headline == null)
                modeTab.headline = CreateHeadline(modeTab.parent, modeTab.emptyKey, modeTab.emptyFallback);

            // All-Tab: nur die Items dieses Modus entfernen, Rest behalten.
            foreach (var id in idsToRemove.ToList())
                RemoveGameFromTab(allTab, id);

            RefreshEmptyHeadline(allTab);

            // Falls der All-Tab mitten im Chunk-Aufbau gestoppt wurde,
            //Rest wieder aufnehmen (sonst bliebe er teilaufgebaut stehen).
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
                tab.buildRequested = true; // leerer Zustand ist final
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
        // Namen frisch halten (z.B. nach Rename vor dem letzten Save).
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
            // Bereits vorhanden -> aktualisieren und nach oben schieben.
            MoveSourceToTop(cachedSummaries, summary.Id);
            MoveSourceToTop(allTab.source, summary.Id);
            MoveSourceToTop(modeTab.source, summary.Id);

            DisableLayoutGroup(allTab.parent);
            DisableLayoutGroup(modeTab.parent);

            try
            {
                UpdateAndMoveToTop(allTab, summary);
                UpdateAndMoveToTop(modeTab, summary);
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

    /// <summary>
    /// Fügt eine neue Summary oben in einen Tab ein. Funktioniert auch während
    /// ein Chunk-Build läuft oder der Tab noch gar nicht gebaut wurde:
    /// Solange der Tab noch lädt, wird der Zähler konsistent mitgeführt.
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

            var item = GetPooledItem(tab.parent);
            item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked, ResolvePlayerName);
            item.transform.SetSiblingIndex(0);
            tab.items.Insert(0, item);
            tab.lookup[summary.Id] = item;
            // Manuell erstellt -> als gebaut zählen, damit der laufende
            // Chunk-Build danach korrekt beim ersten ungeöffneten Index fortfährt.
            tab.builtCount++;
        }
        finally
        {
            EnableLayoutGroup(tab.parent);
        }
    }

    private void UpdateAndMoveToTop(TabState tab, GameSummary summary)
    {
        if (!tab.lookup.TryGetValue(summary.Id, out var item))
            return;

        item.Setup(appHandler, summary, HistoryItemMode.History, OnGameClicked, ResolvePlayerName);

        item.transform.SetAsFirstSibling();

        tab.items.Remove(item);
        tab.items.Insert(0, item);
    }

    private void OnDestroy()
    {
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

    public void OnShow()
    {
        // Nicht blockieren: nur sicherstellen, dass der sichtbare Tab
        // (gechunkt, über Frames verteilt) aufgebaut wird.
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
