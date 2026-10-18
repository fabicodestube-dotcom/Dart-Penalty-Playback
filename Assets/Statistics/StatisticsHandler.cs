using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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

    // Live-Pages: genau EIN Satz aus 4 Pages (eine pro Swipe-Mode) ist
    // dauerhaft aktiv und zeigt immer die currentRange. Timeline-Wechsel ist
    // ein reiner Daten-Rebind (gechunkt), kein SetActive-Toggle — Deaktivieren
    // + Reaktivieren großer Hierarchien war der Timeline-Lag-Spike.
    // Die übrigen 16 Range-Pages werden einmalig deaktiviert und nie wieder
    // angefasst (kein Layout, kein Overdraw, keine Rebuilds).
    private StatisticsPageAll liveAllPage;
    private StatisticsPageX01 liveX01Page;
    private StatisticsPageCricket liveCricketPage;
    private StatisticsPageATC liveATCPage;
    private bool livePagesBuilt;

    private List<StatisticsFilterListItem> popupItems = new List<StatisticsFilterListItem>();

    // Event-Deltas statt Voll-Rebuild: Handler markieren nur dirty, die
    // Verarbeitung läuft gechunkt (max. PlayersPerFrame Spieler pro Frame) und
    // nur wenn die Seite sichtbar ist. Bei hidden Seite bleiben die Flags
    // stehen und werden im nächsten OnShow abgearbeitet — kein GameObject-Touch
    // im Hintergrund, kein SQLite-Read (Aggregate kommen aus BasePlayer).
    private readonly HashSet<Guid> dirtyPlayers = new HashSet<Guid>();
    private bool everythingDirty;
    private bool screenVisible;
    private DateTime lastRangeEvaluationDate = DateTime.Now.Date;
    private const int PlayersPerFrame = 6;

    // Popup Auto-Size: Das Fenster (VerticalLayoutGroup) wächst mit Header +
    // Liste + Buttons. Nur die Listenhöhe wird gesetzt:
    // LayoutElement.preferredHeight = min(Formel, Cap).
    // Formel: PadTop + PadBottom + N * Zeilenhöhe + max(0, N-1) * Spacing
    // (N = Childzahl, Zeilenhöhe live aus erster Zeile, Pad/Spacing aus VLG).
    // Cap: Backgroundhöhe (BottomBar-frei, Screens-Container minus 175) -
    // Fenstermarge (200 = 100 oben + 100 unten) - Chrome (alles außer ScrollView).
    private const float PopupRowHeight = 130f;

    private bool popupIsOpen;
    private Coroutine popupScrollResetRoutine;
    private List<(Guid id, string name, bool visible)> popupBuiltState;

    private StatisticsRange currentRange = StatisticsRange.AllTime;
    private Coroutine buildRoutine;
    private Coroutine dirtyRoutine;
    private Coroutine spareRoutine;

    /// <summary>
    /// Reserve-Rows pro Tabelle (für künftig neue Spieler, ohne Hitch).
    /// </summary>
    private const int SpareRowsPerTable = 4;

    /// <summary>
    /// True, sobald die 4 Live-Pages einmalig aufgebaut UND die Reserve-Rows
    /// aufgefüllt sind (BootLoader wartet darauf, bevor der Ladebildschirm
    /// ausgeblendet wird).
    /// </summary>
    public bool IsInitialBuildDone => livePagesBuilt && spareRoutine == null && !SpareShortage();

    // Einmalig aufgelöste Vertikal-Scroller (Inspector-Felder, sonst Fallback).
    // Kein Traversal pro Verlassen — ResetScrollPositions nutzt nur diese Refs.
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
        // KEIN SetActive-Toggle mehr: Die 4 Live-Pages (eine pro Swipe-Mode)
        // bleiben dauerhaft aktiv, nur ihre Daten werden auf die neue Range
        // umgebunden (gechunkt über die Dirty-Queue). Dadurch kein
        // Deaktivierungs-/Reaktivierungs-Rebuild beim Timeline-Wechsel, und
        // Swipe-Nachbarn sind immer live sichtbar.
        bool rangeChanged = currentRange != range;
        currentRange = range;

        // Noch im Erstaufbau: Build auf die neue Range umlenken.
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
        // Range dynamisch: Falls der User vor Build-Ende die Timeline tippt,
        // baut der Erstaufbau direkt die gewünschte Range (siehe FilterByDate).
        yield return BuildLivePagesAsync(currentRange);
        yield return null;
        buildRoutine = null;
        livePagesBuilt = true;
        EnsureDirtyProcessing();
        EnsureSpareTopUp();
    }

    /// <summary>
    /// OnShow-Sequenz: Rollover prüfen, Live-Pages ggf. erstaufbauen,
    /// Dirty-Queue abarbeiten. Kein Preload nötig — alle Ranges lesen dieselben
    /// In-Memory-Aggregate, Rebinds laufen über die Queue.
    /// </summary>
    private IEnumerator OnShowWorkSequence()
    {
        if (appHandler != null && lastRangeEvaluationDate != DateTime.Now.Date)
        {
            // Transition anlaufen lassen, dann Time-Snapshots neu aufbauen
            // (einmal pro Tag statt pro Event). Live-Pages danach komplett
            // neu einbinden.
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
    /// Arbeitet dirty Spieler-Chunks ab (max. PlayersPerFrame pro Frame).
    /// Schreibt immer die currentRange auf die 4 Live-Pages.
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
            // Snapshot pro Chunk, damit neue Events in die nächste Runde fallen.
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
    /// Startet die Dirty-Verarbeitung, wenn die Seite sichtbar und sonst idle
    /// ist. Läuft ein Vordergrund-Build, übernimmt dessen Tail das Draining.
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
    /// True, wenn irgendeine Live-Page noch Reserve-Rows braucht.
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
    /// Füllt verbrauchte Reserve-Rows im Idle nach (nur wenn sonst nichts
    /// läuft). Hält die Hitch-Garantie für neue Spieler dauerhaft aufrecht.
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
    /// Baut die 4 Live-Pages einmalig für eine Range auf (gechunkt).
    /// Danach sind alle Tabellen/Charts gepoolt — alles Weitere sind Upserts.
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
        // Aggregate kommen aus BasePlayer (In-Memory, O(1) pro Spieler) —
        // kein SQLite-Read, kein Rescan über alle Spiele.
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

        // Zeilen nur neu aufbauen, wenn sich die Spielerliste geändert hat
        // (hinzugefügt/gelöscht oder Sichtbarkeit geändert). Sonst bestehende
        // Zeilen unangetastet lassen — kein Instantiate, kein Rebuild.
        if (!TryReusePopupRows())
            SetupPopup();

        popupIsOpen = true;

        // Zuerst aktivieren: Alle Höhenmessungen (Background.rect,
        // LayoutUtility) sind erst bei aktivem Popup gültig. Vorher synchron
        // gemessen gab es stale/0-Werte und der nachfolgende Layout-Rebuild
        // hat die Scrollposition wieder überschrieben.
        if (windowHandler != null)
            windowHandler.ShowPopup(popup);
        else if (popup != null)
            popup.gameObject.SetActive(true);

        // Höhe + Scroll-Reset immer — auch im Reuse-Pfad (Screen-Größe kann
        // sich geändert haben, Scroll-Offset stammt vom letzten Öffnen).
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
    /// True, wenn die aufgebauten Zeilen noch exakt zur Spielerliste passen
    /// (gleiche Spieler in gleicher Reihenfolge + gleiche Namen +
    /// gleiche Sichtbarkeit) — dann kein Neuaufbau, kein Rebuild.
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
    /// Gezieltes Anhängen einer einzelnen Zeile (z.B. Bot während offenem Popup
    /// hinzugefügt) statt Voll-Rebuild. Höhe wird nachgezogen, Liste bleibt oben.
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
                // Sofort aus dem Layout lösen, damit keine alten Zeilen
                // aufblitzen. Destroy() selbst wirkt erst am Frame-Ende.
                item.transform.SetParent(null, false);
                Destroy(item.gameObject);
            }
        }

        popupItems.Clear();

        // Falls etwas ohne Tracking unter popupParent liegt (z.B. nach
        // abgebrochenem Aufbau), ebenfalls lösen/zerstören.
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
    /// Passt NUR die Listenhöhe an (Header + Mark-Buttons + OK bleiben fix).
    /// Wunschhöhe per expliziter Formel (Pad + N*Zeilenhöhe + (N-1)*Spacing,
    /// alles live gelesen), gecappt auf Backgroundhöhe - Marge - Chrome.
    /// Muss bei aktivem Popup laufen (ShowPopup aktiviert zuerst) — sonst sind
    /// rect/LayoutUtility-Werte stale. Scroll-Reset siehe EnsurePopupScrollReset.
    /// </summary>
    private void ApplyPopupListHeight()
    {
        if (!ArePopupSizeRefsValid())
            return;

        RebuildPopupLayouts();

        float listHeight = GetPopupListDesiredHeight();
        float maxList = GetPopupMaxListHeight();

        // TEMP-DIAG: Welche Höhe wurde woraus gerechnet?
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
            Debug.LogWarning($"[StatisticsHandler] Popup Auto-Size abgebrochen: maxList={maxList}, items={popupItems.Count}");
            return;
        }

        float target = Mathf.Min(listHeight, maxList);

        // Nur schreiben, was sich ändert — jeder Setter dirtied das Layout.
        if (!Mathf.Approximately(popupListLayoutElement.preferredHeight, target))
            popupListLayoutElement.preferredHeight = target;

        bool wantVertical = listHeight > maxList + 0.5f;
        if (popupScrollRect.vertical != wantVertical)
            popupScrollRect.vertical = wantVertical;

        ResetPopupScrollTop();
    }

    /// <summary>
    /// Erzwingt synchronen Layout-Durchlauf für Content + Window, damit die
    /// anschließende Messung (LayoutUtility / rect) den frisch aufgebauten
    /// Zeilen entspricht statt dem Vorframe.
    /// </summary>
    private void RebuildPopupLayouts()
    {
        try
        {
            Canvas.ForceUpdateCanvases();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StatisticsHandler] ForceUpdateCanvases fehlgeschlagen: {e.Message}");
        }

        if (popupListContentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupListContentRect);
        if (popupWindowRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupWindowRect);
    }

    /// <summary>
    /// Setzt die ScrollView synchron nach oben (erste Zeile unter den fixen
    /// Elementen). Reicht allein nicht beim Öffnen — der Layout-Rebuild danach
    /// kann den Offset wieder verschieben, daher zusätzlich
    /// EnsurePopupScrollReset (Coroutine bis Frame-Ende).
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
    /// Startet den verzögerten Scroll-Reset (läuft bis nach dem
    /// Layout-Rebuild am Frame-Ende). Nur wenn das Popup offen ist.
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
    /// Wunschhöhe der Liste — explizit gerechnet (Primärpfad):
    /// PadTop + PadBottom + N * Zeilenhöhe + max(0, N-1) * Spacing.
    /// N = echte Childzahl im Content, Zeilenhöhe live aus der ersten Zeile
    /// (Fallback Konstante 130), Padding/Spacing live aus der Content-VLG.
    /// Lücken nur ZWISCHEN Zeilen (N=0 → nur Padding, N=1 → kein Spacing).
    /// Die Layout-Messung (LayoutUtility) erwies sich als spacing-los und
    /// dient nur noch als TEMP-DIAG-Vergleichswert in ApplyPopupListHeight.
    /// </summary>
    private float GetPopupListDesiredHeight()
    {
        return GetPopupFormulaHeight();
    }

    /// <summary>
    /// Live-Zeilenhöhe: preferredHeight der ersten Zeile im Content.
    /// Fallback Konstante (Prefab-Wurzel LayoutElement.preferredHeight).
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
    /// Listenhöhe per Formel (Primärpfad, siehe GetPopupListDesiredHeight).
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
    /// Chrome = alles im Fenster AUSSER der ScrollView (Headline, Line,
    /// Mark-Buttons, ButtonPanel) inkl. Window-Padding/Spacing.
    /// Nutzt die serialisierten LayoutElement-Werte (alle fix) — keine
    /// Layout-Messung, daher jederzeit synchron abrufbar.
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
    /// Cap für die ScrollView: Backgroundhöhe - Fenstermarge - Chrome.
    /// Background liegt im Screens-Container (Display minus BottomBar 175),
    /// ist also bereits BottomBar-frei — hier KEIN extra BottomBar-Abzug,
    /// sonst Doppelzählung. Marge 200 = 100 oben + 100 unten (analog X).
    /// Wird bei jedem Öffnen frisch aus der live Backgroundhöhe gerechnet
    /// (kein Caching — der Wert hängt von der Bildschirmgröße ab).
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

        // Listen-Content: explizites Feld, sonst popupParent, sonst ScrollRect.content.
        if (popupListContentRect == null && popupParent != null)
            popupListContentRect = popupParent as RectTransform;
        if (popupListContentRect == null && popupScrollRect != null && popupScrollRect.content != null)
            popupListContentRect = popupScrollRect.content;
        if (popupParent == null && popupListContentRect != null)
            popupParent = popupListContentRect;

        // Fenster: explizites Feld, sonst Parent der ScrollView (Window mit VLG+CSF).
        if (popupWindowRect == null && popupScrollViewRect != null)
            popupWindowRect = popupScrollViewRect.parent as RectTransform;

        // Hintergrund: explizites Feld, sonst per Name unter dem Popup suchen.
        if (popupBackgroundRect == null && popup != null)
        {
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

        // Ausgeblendete Spieler sofort verstecken (billig: nur SetActive auf
        // bestehenden Zeilen). Eingeblendete über die Dirty-Queue (gechunkt).
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
        // Neuer Spieler, noch keine Spiele: über die Dirty-Queue einreihen
        // (gechunkt, nur aufgebaute Ranges) statt synchron aufzubauen.
        dirtyPlayers.Add(botId);
        EnsureDirtyProcessing();

        // Bei offenem Popup gezielt eine Zeile anhängen statt Voll-Rebuild.
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
        // Row-Keys sind Namen: alte Zeilen ausblenden, neue über die Queue
        // anlegen (gechunkt, nur wenn sichtbar).
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
    /// Baut die Filter-Popup-Liste vollständig neu (selten: Delete/Rename bei
    /// offenem Popup). Reguläres Öffnen nutzt TryReusePopupRows.
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

        // Unfertige Spiele sind in keiner Aggregation.
        if (!game.IsFinished())
            return;

        // BasePlayer- + DB-Aggregate sind durch den Save-Pfad bereits aktuell —
        // hier nur die betroffenen Teilnehmer über die Dirty-Queue einreihen.
        // Das Game-Objekt selbst trägt alle Deltas, kein Nachladen nötig.
        foreach (var pid in game.GetPlayerIDs())
            dirtyPlayers.Add(pid);

        EnsureDirtyProcessing();
    }

    private void HandleDeleteGame(GameMode mode, Guid gameID, List<Guid> affectedPlayerIds)
    {
        if (appHandler == null)
            return;

        // Aggregate sind durch den Delete-Pfad bereits abgezogen —
        // nur betroffene Teilnehmer einreihen (aus dem Event, kein Re-Read).
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

        // Keine Spiele mehr: alle Spieler einreihen (frische Leer-Aggregate,
        // Zeilen bleiben bestehen). Chunked über die Queue.
        MarkAllPlayersDirty();
        EnsureDirtyProcessing();
    }

    // =========================
    // GEZIELTE SPIELER-UPDATES (immer currentRange auf den 4 Live-Pages)
    // =========================

    /// <summary>
    /// Frischt EINEN Spieler auf den 4 Live-Pages auf (currentRange):
    /// Aggregate aus BasePlayer (In-Memory, kein SQLite), Zeilen per Upsert.
    /// Einziger Update-Pfad für Add/Delete/Modus-Delete/Rename/Sichtbarkeit
    /// und Timeline-Rebinds.
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

        // ALL-Modus: kombiniertes In-Memory-Aggregat.
        if (liveAllPage != null)
        {
            liveAllPage.UpsertPlayerRow(player.GetCombinedStats(range), name);
            liveAllPage.SetPlayerRowVisible(name, visible);
        }

        // X01-Modus.
        if (liveX01Page != null)
        {
            var x01 = player.GetModeStats(GameMode.X01, range) as GameStatsX01
                ?? new GameStatsX01(playerId);
            liveX01Page.UpsertPlayerRow(x01, name);
            liveX01Page.SetPlayerRowVisible(name, visible);
        }

        // Cricket-Modus.
        if (liveCricketPage != null)
        {
            var cricket = player.GetModeStats(GameMode.Cricket, range) as GameStatsCricket
                ?? new GameStatsCricket(playerId);
            liveCricketPage.UpsertPlayerRow(cricket, name);
            liveCricketPage.SetPlayerRowVisible(name, visible);
        }

        // ATC-Modus.
        if (liveATCPage != null)
        {
            var atc = player.GetModeStats(GameMode.ATC, range) as GameStatsATC
                ?? new GameStatsATC(playerId);
            liveATCPage.UpsertPlayerRow(atc, name);
            liveATCPage.SetPlayerRowVisible(name, visible);
        }
    }

    /// <summary>
    /// Blendet EINEN Spieler auf den Live-Pages aus.
    /// Legt nichts an — nur bestehende Zeilen/Charts. Tabellenhöhen werden
    /// NUR auf den Pages neu berechnet, auf denen sich die sichtbare
    /// Zeilenzahl wirklich geändert hat (Anzahl vorher != Anzahl nachher).
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
        // Die AllTime-Instanzen sind die dauerhaft aktiven Live-Pages (eine
        // pro Swipe-Mode). Die alten Today/Week/Month/Year-Pages wurden aus
        // der Szene gelöscht — Timeline-Wechsel ist ein reiner Daten-Rebind,
        // kein Page-Toggle.
        liveAllPage = pageAllAlltime;
        liveX01Page = pageX01Alltime;
        liveCricketPage = pageCricketAlltime;
        liveATCPage = pageATCAlltime;

        WirePenaltyResolvers();
    }

    /// <summary>
    /// Penalty-Taps lösen ihre Stats zum Tap-Zeitpunkt frisch aus BasePlayer
    /// + currentRange auf (Stale-Closure-Fix): Der Build-Zeit-Snapshot in der
    /// Tap-Closure kann veralten (Upsert schreibt Zeile neu, Tap behielt das
    /// alte Objekt → Popup mit fremden Counts/Total). Resolver lesen live.
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

        // Laufende Arbeit fortsetzen lassen (kein Doppelstart).
        if (buildRoutine != null || dirtyRoutine != null)
            return;

        buildRoutine = StartCoroutine(OnShowWorkSequence());
    }

    public void OnHide()
    {
        screenVisible = false;

        // Hintergrund-Arbeit anhalten (Flags bleiben, Fortsetzung bei OnShow).
        StopAllBuildWork();

        // Scrollpositionen zurücksetzen (Swipe auf Seite 0, alle Listen nach
        // oben), damit das nächste Öffnen wieder von oben startet. Nur
        // gecachte Referenzen + guarded Writes: kein Traversal, kein SetActive,
        // kein Event — im Regelfall (eh schon oben) praktisch null Kosten.
        // Ersetzt das alte swipeMenu.ResetView(), das pro Verlassen einen
        // GetComponentsInChildren-Traversal über alle Pages gelegt hat
        // (Close-Spike).
        ResetScrollPositions();
    }

    /// <summary>
    /// Setzt die Swipe-Position auf Seite 0 und alle 4 vertikalen Modus-Listen
    /// nach oben. Schreibt nur, was wirklich abweicht (jeder Setter dirtied
    /// ScrollRect + Layout). Die 16 deaktivierten Range-Pages haben keine
    /// eigenen Scroller und brauchen nichts.
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

            // SetPage pflegt currentPage + TopBar + Indicator; feuert
            // OnPageChanged(0), das auf dieser Seite keinen Subscriber hat.
            if (swipeMenu.GetCurrentPage() != 0)
                swipeMenu.SetPage(0);
        }
    }

    /// <summary>
    /// Löst die 4 Vertikal-Scroller einmalig auf: Inspector-Felder zuerst,
    /// fehlende Einträge per Parent-Walk vom zugehörigen Mode-Parent
    /// (kein Full-Traversal im Betrieb).
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
    /// Nächster ScrollRect über dem Mode-Parent, der NICHT der horizontale
    /// Swipe-Scroller ist (Muster wie HistoryHandler.HookScrollRects).
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
