using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Detailed popup for ONE player (detail button per table line):
    /// Lines per penalty (name | cost | count | line total) in its own
/// Scroll area + Grand Total in the header. ONE reused instance
/// (Top level popup next to the other popups, NOT in table layouts).
/// Wiring: put script on popup object, drag titleText + contentParent,
/// sumText → Sum text (optional), Close button → Close().
/// windowHandler/appHandler optional (auto-find, incl. inactive objects).
/// </summary>
public class PenaltyDetailPopup : UIScreen, IUIScreen
{
    [Header("Control")]
    public WindowHandler windowHandler;
    public AppHandler appHandler;

    [Header("UI")]
    public TMP_Text titleText;
    public TMP_Text sumText;
    public Transform contentParent;

    [Header("Zeilen-Prefab")]
    [Tooltip("PenaltyDetailPopupItem-Prefab (wird pro Penalty instanziiert). Leer = Code-Zeilen als Fallback.")]
    public GameObject itemPrefab;

    [Header("Leer-Zustand")]
    [Tooltip("Prefab für 'keine Penalties' (eine Zeile: TMP + LayoutElement + LocalizeStringEvent + ThemeColorBinding). Leer = Code-Zeile als Fallback.")]
    public GameObject emptyStatePrefab;

    [Header("Auto-Size (nur Liste scrollt)")]
    [Tooltip("ScrollRect auf dem 'Scroll View'-Objekt des Popups. Leer = wird gesucht.")]
    public ScrollRect scrollRect;
    [Tooltip("RectTransform des 'Scroll View'-Objekts. Leer = wird gesucht.")]
    public RectTransform scrollViewRect;
    [Tooltip("LayoutElement auf dem 'Scroll View'-Objekt. preferredHeight wird zur Laufzeit gesetzt.")]
    public LayoutElement listLayoutElement;
    [Tooltip("Listen-Content (Zeilen-Container). Leer = contentParent.")]
    public RectTransform listContentRect;
    [Tooltip("Fenster (Parent der ScrollView, mit VerticalLayoutGroup + ContentSizeFitter). Leer = wird gesucht.")]
    public RectTransform windowRect;
    [Tooltip("Hintergrund-Dimmer des Popups. Dient als Referenz für die maximale Fensterhöhe. Leer = wird gesucht.")]
    public RectTransform backgroundRect;
    [Tooltip("Vertikale Marge des Fensters gegenüber dem Hintergrund (100 oben + 100 unten). Der Hintergrund liegt im Screens-Container und ist bereits BottomBar-frei (BottomBar 175px), daher hier kein extra Abzug.")]
    public float windowMarginY = 200f;

    // Auto-Size: Only the list height is set:
    // LayoutElement.preferredHeight = min(formula, cap).
    // Formula: PadTop + PadBottom + sum(child heights) + max(0, N-1) * Spacing
    // (N = Child number, heights live per row, pad/spacing from VLG).
    // Cap: Background Height (BottomBar -free) - Margin (200) - Chrome (Top +)
    // ButtonRow + window padding/spacing, measured live, fallback 450).
    private const float RowHeight = 120f;
    private const float HeaderRowHeight = 64f;
    private const float ChromeFallback = 450f;

    private Coroutine scrollResetRoutine;
    private string currentTitle = string.Empty;
    private string currentTotalText = string.Empty;

    /// <summary>
    /// Find the pop-up instance belonging to the visible screen.
    /// There are two instances (Statistics + GameDetail); the first one
    /// Auto-Find hit can hang under an inactive screen and remain
    /// Invisible to ShowPopup. Therefore, instance with active parent chain
    /// I prefer first as fallback.
    /// </summary>
    public static PenaltyDetailPopup FindVisibleInstance()
    {
        var all = FindObjectsByType<PenaltyDetailPopup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
            return null;

        PenaltyDetailPopup fallback = null;
        foreach (var p in all)
        {
            if (p == null)
                continue;
            if (fallback == null)
                fallback = p;
            if (HasActiveScreen(p))
                return p;
        }

        return fallback;
    }

    /// <summary>
    /// True, when the parent chain (screen) is active — the popup itself
    /// Except (hidden popups are off deactivateWhenHidden).
    /// </summary>
    private static bool HasActiveScreen(PenaltyDetailPopup p)
    {
        var t = p.transform.parent;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                return false;
            t = t.parent;
        }
        return true;
    }

    private void OnValidate()
    {
        id = ScreenId.Popup;

        if (rect == null)
            rect = GetComponent<RectTransform>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    protected override void Awake()
    {
        base.Awake();

        id = ScreenId.Popup;

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>(FindObjectsInactive.Include);
        if (appHandler == null)
            appHandler = FindAnyObjectByType<AppHandler>(FindObjectsInactive.Include);

        DisableHeaderLocalization();
    }

    public void OnShow()
    {
        DisableHeaderLocalization();
        ApplyHeader();
    }

    public void OnHide()
    {
        if (scrollResetRoutine != null)
        {
            StopCoroutine(scrollResetRoutine);
            scrollResetRoutine = null;
        }

        ResetScrollTop();
    }

    public void Close()
    {
        if (windowHandler != null)
            windowHandler.HidePopup();
        else
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Breakdown of a player in a game (GameDetail): lines for ALL
    /// ACTIVES (Legacy per Settings + Snapshot Customs) – also with
    /// 0 hits so that the list is always complete. Grand Total in the header.
    /// Everything from Summary + Snapshot (delete-safe).
    /// </summary>
    public void ShowPlayerPenalties(GameSummary summary, Guid playerId, string playerName)
    {
        if (summary == null)
            return;

        ClearContent();
        SetTitle(string.IsNullOrWhiteSpace(playerName) ? LocalizationText.Get("Penalties", "Penalties") : playerName);
        AddTableHeader();

        var entry = summary.PlayerEntries?.Find(e => e != null && e.PlayerId == playerId);
        var settings = LoadGameSettings(summary);

        float grandTotal = 0f;
        bool anyRow = false;

        // Legacy: only types with bookings (old games).
        // there 0 (system decommissioned) — Settings gates no longer count.
        // Without readable settings: Fallback to counted types with a cost of 0.
        bool settingsOk = settings?.Penalties != null;
        foreach (var legacy in PenaltyDisplay.LegacyPenaltyTypes)
        {
            int count = entry != null ? LegacyCountFor(entry, legacy) : 0;

            if (count <= 0)
                continue;

            int cost = settingsOk ? settings.Penalties.GetCost(legacy) : 0;

            AddItemRow(PenaltyDisplay.BuiltInDisplayName(legacy),
                cost.ToString(),
                count + "×",
                (count * cost).ToString("0.##"));
            grandTotal += count * cost;
            anyRow = true;
        }

        // Customs: Only activated snapshot defs that the player can use in THIS
        // Game also has cashed (number >= 1) — analogous to the statistic way.
        var snapshot = summary.CustomPenaltySnapshot;
        if (snapshot != null)
        {
            foreach (var def in snapshot)
            {
                if (def == null || !def.enabled || string.IsNullOrEmpty(def.id))
                    continue;

                int count = 0;
                entry?.CustomPenaltyCounts?.TryGetValue(def.id, out count);
                if (count <= 0)
                    continue;

                AddItemRow(def.name, def.cost.ToString(), count + "×",
                    (count * def.cost).ToString("0.##"));
                grandTotal += count * def.cost;
                anyRow = true;
            }
        }

        if (!anyRow)
            AddInfoRow(LocalizationText.Get("No_penalties", "No penalties applied."), "");

        SetTotal(grandTotal);
        FinishShow();
    }

    /// <summary>
    /// Breakdown of a player in the aggregate: Legacy without
    /// Cost (different per game → stroke), Customs with the latest price.
    /// </summary>
    public void ShowPlayerAggregates(string playerName, GameStats stats)
    {
        ClearContent();
        SetTitle(string.IsNullOrWhiteSpace(playerName) ? LocalizationText.Get("Penalties", "Penalties") : playerName);
        AddTableHeader();

        float grandTotal = 0f;
        bool anyRow = false;
        int legacyRows = 0, customRows = 0;

        foreach (var legacy in PenaltyDisplay.LegacyPenaltyTypes)
        {
            int count = PenaltyDisplay.LegacyCount(stats, legacy);
            if (count <= 0)
                continue;

            AddItemRow(PenaltyDisplay.BuiltInDisplayName(legacy), "–", count + "×", "–");
            anyRow = true;
            legacyRows++;
        }

        if (stats?.customPenaltyCounts != null)
        {
            foreach (var kvp in stats.customPenaltyCounts)
            {
                if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0)
                    continue;

                var def = FindYoungestDef(kvp.Key);
                string name = def?.name ?? kvp.Key;
                int cost = def?.cost ?? 0;
                AddItemRow(name, cost.ToString(), kvp.Value + "×",
                    (kvp.Value * cost).ToString("0.##"));
                grandTotal += kvp.Value * cost;
                anyRow = true;
                customRows++;
            }
        }

        if (!anyRow)
            AddInfoRow(LocalizationText.Get("No_penalties", "No penalties applied."), "");

        // TEMP-DIAG: Object Identity Tap vs. Table + Line Balance.
        int childCount = contentParent != null ? contentParent.childCount : -1;
        int customIds = 0;
        if (stats?.customPenaltyCounts != null)
        {
            foreach (var kvp in stats.customPenaltyCounts)
                if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value > 0)
                    customIds++;
        }
        Debug.Log($"[PenaltyDiag] Popup {playerName}: stats(hash={(stats != null ? stats.GetHashCode().ToString() : "null")}," +
            $"anzahl={(stats != null ? PenaltyDisplay.TotalPenaltyCount(stats).ToString() : "-")}," +
            $"kosten={(stats != null ? stats.totalPenaltyCost.ToString("0.##") : "-")}," +
            $"customIds={customIds}) legacyRows={legacyRows} customRows={customRows} childCount={childCount}");

        // Legacy shares without stable costs: Grand Total from the aggregate sum.
        float aggregateTotal = stats?.totalPenaltyCost ?? 0f;
        SetTotal(aggregateTotal > 0f ? aggregateTotal : grandTotal);
        FinishShow();
    }

    private CustomPenaltyDef FindYoungestDef(string columnId)
    {
        if (appHandler == null)
            appHandler = FindAnyObjectByType<AppHandler>();
        if (appHandler == null)
            return null;

        // GetGameSummaries is sorted descending by time: first hit wins.
        foreach (var summary in appHandler.GetGameSummaries())
        {
            var def = summary?.CustomPenaltySnapshot?.Find(d => d != null && d.id == columnId);
            if (def != null)
                return def;
        }

        return null;
    }

    private static GameSettings LoadGameSettings(GameSummary summary)
    {
        if (summary == null || string.IsNullOrEmpty(summary.SettingsJson))
            return null;

        try
        {
            return summary.GameMode switch
            {
                GameMode.X01 => Newtonsoft.Json.JsonConvert.DeserializeObject<X01GameSettings>(summary.SettingsJson),
                GameMode.Cricket => Newtonsoft.Json.JsonConvert.DeserializeObject<CricketGameSettings>(summary.SettingsJson),
                GameMode.ATC => Newtonsoft.Json.JsonConvert.DeserializeObject<ATCGameSettings>(summary.SettingsJson),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static int LegacyCountFor(PlayerSummaryEntry entry, PenaltyType type)
    {
        return type switch
        {
            PenaltyType.Wall => entry.WallCount,
            PenaltyType.Ceiling => entry.CeilingCount,
            PenaltyType.AllMiss => entry.AllMissCount,
            PenaltyType.ThreeOnes => entry.ThreeOnesCount,
            PenaltyType.Schnapszahl => entry.TripleDigitCount,
            PenaltyType.LostGame => entry.LostGameCount,
            _ => 0
        };
    }

    private void SetTitle(string title)
    {
        currentTitle = title ?? "";
        ApplyHeader();
    }

    /// <summary>Grand Total to the header area (sumText) or title suffix.</summary>
    private void SetTotal(float grandTotal)
    {
        string label = LocalizationText.Get("Total", "Total");
        currentTotalText = $"<size=65%>{label}</size>  {grandTotal:0.##}";
        ApplyHeader();
    }

    private void ApplyHeader()
    {
        if (titleText != null)
        {
            titleText.fontStyle = FontStyles.Bold;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 28f;
            titleText.fontSizeMax = 46f;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.alignment = TextAlignmentOptions.Left;
            ConfigureHeaderWidth(titleText, 1.8f);

            if (sumText != null)
            {
                titleText.text = currentTitle;
                sumText.fontStyle = FontStyles.Bold;
                sumText.enableAutoSizing = false;
                sumText.fontSize = 38f;
                sumText.textWrappingMode = TextWrappingModes.NoWrap;
                sumText.overflowMode = TextOverflowModes.Ellipsis;
                sumText.alignment = TextAlignmentOptions.Right;
                ConfigureHeaderWidth(sumText, 1f);
                sumText.text = currentTotalText;
            }
            else
            {
                titleText.text = string.IsNullOrEmpty(currentTotalText)
                    ? currentTitle
                    : currentTitle + " (" + currentTotalText + ")";
            }
        }
    }

    private static void ConfigureHeaderWidth(TMP_Text text, float flexibleWidth)
    {
        if (text == null)
            return;

        var element = text.GetComponent<LayoutElement>();
        if (element == null)
            element = text.gameObject.AddComponent<LayoutElement>();

        element.flexibleWidth = flexibleWidth;
        element.minWidth = 100f;
    }

    private static void DisableHeaderLocalizationFor(TMP_Text text)
    {
        if (text == null)
            return;

        var events = text.GetComponents<LocalizeStringEvent>();
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i] != null)
                events[i].enabled = false;
        }
    }

    private void DisableHeaderLocalization()
    {
        DisableHeaderLocalizationFor(titleText);
        DisableHeaderLocalizationFor(sumText);
    }

    /// <summary>Header of the 4 detail columns (name | count | cost | sum).</summary>
    private void AddTableHeader()
    {
        AddItemRow(
            LocalizationText.Get("Name", "Name"),
            LocalizationText.Get("Costs", "Costs"),
            LocalizationText.Get("Count", "Count"),
            LocalizationText.Get("Sum", "Sum"),
            true);
    }

    /// <summary>
    /// Detail line (name | cost | number | sum): prefers the
    /// wired prefab, otherwise code line as fallback (silent emergency operation).
    /// </summary>
    private void AddItemRow(string name, string cost, string count, string rowTotal, bool header = false)
    {
        if (contentParent == null)
        {
            Debug.LogWarning("[PenaltyDetailPopup] AddItemRow ohne contentParent — Zeile verworfen.");
            return;
        }

        if (itemPrefab != null)
        {
            var go = Instantiate(itemPrefab, contentParent);
            var item = go.GetComponent<PenaltyDetailPopupItem>();
            if (item != null)
            {
                item.Bind(name, cost, count, rowTotal, header);
                return;
            }
            Destroy(go);
        }

        AddDetailRow(name, cost, count, rowTotal, header);
    }

    /// <summary>Detail line with 4 columns (name | count | cost | sum).</summary>
    private void AddDetailRow(string name, string cost, string count, string rowTotal, bool header = false)
    {
        if (contentParent == null)
            return;

        var go = new GameObject("DetailRow", typeof(RectTransform));
        go.transform.SetParent(contentParent, false);

        var rowLayout = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(24, 24, 8, 8);
        rowLayout.spacing = 12f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        var rowSize = go.AddComponent<UnityEngine.UI.LayoutElement>();
        rowSize.preferredHeight = header ? HeaderRowHeight : RowHeight;
        rowSize.flexibleHeight = 0f;

        AddDetailCell(go.transform, name, 2.4f, header, TextAlignmentOptions.Left, ThemeColorRole.Text1);
        AddDetailCell(go.transform, count, 0.8f, header, TextAlignmentOptions.Center, ThemeColorRole.Text2);
        AddDetailCell(go.transform, cost, 1.1f, header, TextAlignmentOptions.Center, ThemeColorRole.Text2);
        AddDetailCell(go.transform, rowTotal, 1.3f, header, TextAlignmentOptions.Right, ThemeColorRole.Accent1);
    }

    private void AddDetailCell(Transform parent, string text, float flexibleWidth, bool header, TextAlignmentOptions alignment, ThemeColorRole dataRole)
    {
        var go = new GameObject("DetailCell", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var size = go.AddComponent<UnityEngine.UI.LayoutElement>();
        size.flexibleWidth = flexibleWidth;
        size.minWidth = 10f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text ?? "";
        tmp.fontSize = header ? 28f : 44f;
        tmp.fontStyle = header || dataRole == ThemeColorRole.Accent1 ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        ThemeColorRole role = header ? ThemeColorRole.Text3 : dataRole;
        tmp.color = GetThemeColor(role, Color.white);
    }

    private static Color GetThemeColor(ThemeColorRole role, Color fallback)
    {
        if (ThemeManager.Instance == null)
            return fallback;

        try { return ThemeManager.Instance.GetColor(role); }
        catch { return fallback; }
    }

    /// <summary>2@@ - key for empty case (table maintained separately).</summary>
    private const string NoPenaltiesKey = "No_penalties";

    private void AddInfoRow(string label, string value)
    {
        if (contentParent == null)
            return;

        // Preferred: empty prefab (TMP + LayoutElement + LocalizeStringEvent +
        // ThemeColorBinding on one line. Key is set to runtime.
        if (emptyStatePrefab != null)
        {
            var go = Instantiate(emptyStatePrefab, contentParent);

            var emptyTmp = go.GetComponentInChildren<TMP_Text>(true);
            if (emptyTmp != null)
            {
                emptyTmp.fontSize = 34f;
                emptyTmp.fontStyle = FontStyles.Normal;
                emptyTmp.alignment = TextAlignmentOptions.Center;
                emptyTmp.textWrappingMode = TextWrappingModes.Normal;
                emptyTmp.overflowMode = TextOverflowModes.Overflow;
                emptyTmp.raycastTarget = false;
                emptyTmp.color = GetThemeColor(ThemeColorRole.Text3, Color.white);
            }

            var emptyLayout = go.GetComponent<LayoutElement>();
            if (emptyLayout != null)
                emptyLayout.preferredHeight = 120f;

            var localizeEvent = go.GetComponent<LocalizeStringEvent>();
            if (localizeEvent != null)
            {
                localizeEvent.StringReference =
                    new LocalizedString("LocalizationTable", NoPenaltiesKey);
                localizeEvent.RefreshString();
            }
            else
            {
                Debug.LogWarning("[PenaltyDetailPopup] emptyStatePrefab ohne LocalizeStringEvent — Fallback-Text bleibt stehen.");
            }

            return;
        }

        // Fallback without prefab: code line (silent fallback mode).
        var row = new GameObject("DetailRow", typeof(RectTransform));
        row.transform.SetParent(contentParent, false);

        var layout = row.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.preferredHeight = 120f;
        layout.flexibleHeight = 0f;

        var tmp = row.AddComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrEmpty(value) ? label ?? "" : label + ": " + value;
        tmp.fontSize = 34f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.color = GetThemeColor(ThemeColorRole.Text3, Color.white);

        // Localize empty text (HistoryScreen pattern): fallback is immediate,
        // LocalizedString overwrites only in the case of non-empty table value —
        // It also works as long as the key is not yet in the table.
        var codeLocalizeEvent = row.AddComponent<LocalizeStringEvent>();
        codeLocalizeEvent.OnUpdateString.AddListener(localizedValue =>
        {
            if (!string.IsNullOrEmpty(localizedValue))
                tmp.text = localizedValue;
        });
        codeLocalizeEvent.StringReference = new LocalizedString("LocalizationTable", NoPenaltiesKey);
        codeLocalizeEvent.RefreshString();
    }

    private void ClearContent()
    {
        if (contentParent == null)
            return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            var child = contentParent.GetChild(i);
            // Immediately remove from the layout so that no old lines are placed in the
            // Destroy() itself only acts at the frame end.
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void ShowViaWindowHandler()
    {
        if (windowHandler != null)
            windowHandler.ShowPopup(this);
        else
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Shows the popup (activates first — measurements are valid only then),
    /// adjust the list height and park the ScrollView above (synchronous +)
    /// delayed until after the layout rebuild at the frame end.
    /// </summary>
    private void FinishShow()
    {
        EnsureSizeRefs();
        ShowViaWindowHandler();
        DisableHeaderLocalization();
        ApplyHeader();
        ApplyListHeight();
        EnsureScrollReset();
    }

    // =========================
    // AUTO-SIZE (only list scrolls)
    // =========================

    private void EnsureSizeRefs()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (scrollViewRect == null && scrollRect != null)
            scrollViewRect = scrollRect.GetComponent<RectTransform>();

        if (listLayoutElement == null && scrollViewRect != null)
            listLayoutElement = scrollViewRect.GetComponent<LayoutElement>();

        if (listContentRect == null && contentParent != null)
            listContentRect = contentParent as RectTransform;
        if (listContentRect == null && scrollRect != null && scrollRect.content != null)
            listContentRect = scrollRect.content;

        if (windowRect == null && scrollViewRect != null)
            windowRect = scrollViewRect.parent as RectTransform;

        if (backgroundRect == null)
        {
            Debug.LogWarning("[PenaltyDetailPopup] backgroundRect nicht verdrahtet; Fallback-Suche nach 'PenaltyColumnDetailPopupBackground'.", this);
            var bg = transform.Find("PenaltyColumnDetailPopupBackground");
            if (bg != null)
                backgroundRect = bg as RectTransform;
        }

        if (backgroundRect == null && windowRect != null)
            backgroundRect = windowRect.parent as RectTransform;
    }

    private bool AreSizeRefsValid()
    {
        return scrollRect != null
            && scrollViewRect != null
            && listLayoutElement != null
            && listContentRect != null
            && windowRect != null
            && backgroundRect != null;
    }

    /// <summary>
    /// ONLY adjust the list height (top + ButtonRow remain fixed).
    /// desired height by explicit formula (pad + N*line height + (N-1)*spacing),
    /// everything read live), capped at background level - margin - Chrome.
    /// Must run with active popup (FinishShow activated first).
    /// </summary>
    private void ApplyListHeight()
    {
        if (!AreSizeRefsValid())
            return;

        RebuildLayouts();

        float listHeight = GetDesiredListHeight();
        float maxList = GetMaxListHeight();

        // TEMP-DIAG: What height was calculated from?
        try
        {
            float measured = listContentRect != null
                ? LayoutUtility.GetPreferredHeight(listContentRect)
                : -1f;
            Debug.Log($"[PopupDiag] Penalty: children={listContentRect?.childCount ?? -1} " +
                $"rowH={GetLiveRowHeight():0.##} explicit={listHeight:0.##} measured={measured:0.##} max={maxList:0.##}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PopupDiag] Penalty-Messung fehlgeschlagen: {e.Message}");
        }

        if (maxList <= 0f)
        {
            Debug.LogWarning($"[PenaltyDetailPopup] Auto-Size abgebrochen: maxList={maxList}, rows={listContentRect.childCount}");
            return;
        }

        float target = Mathf.Min(listHeight, maxList);

        // Just write what changes — each setter dirtied the layout.
        if (!Mathf.Approximately(listLayoutElement.preferredHeight, target))
            listLayoutElement.preferredHeight = target;

        bool wantVertical = listHeight > maxList + 0.5f;
        if (scrollRect.vertical != wantVertical)
            scrollRect.vertical = wantVertical;

        ResetScrollTop();
    }

    /// <summary>
    /// Forces synchronous layout through for content + window to allow the
    /// subsequent measurement corresponds to the freshly constructed lines.
    /// </summary>
    private void RebuildLayouts()
    {
        try
        {
            Canvas.ForceUpdateCanvases();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PenaltyDetailPopup] ForceUpdateCanvases fehlgeschlagen: {e.Message}");
        }

        if (listContentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContentRect);
        if (windowRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(windowRect);
    }

    /// <summary>
    /// The desired height of the list — explicitly calculated (primary path):
    /// PadTop + PadBottom + N * line height + max(0, N-1) * Spacing.
    /// N = real child number in the content, line height live from the first line
    /// (Fallback Constant 130), Padding/Spacing live from the Content-VLG.
    /// Gaps only between lines (N=0 → only padding, N=1 → no spacing).
    /// </summary>
    private float GetDesiredListHeight()
    {
        int n = listContentRect != null ? listContentRect.childCount : 0;

        float padTop = 0f, padBottom = 0f, spacing = 0f;
        if (listContentRect != null)
        {
            var group = listContentRect.GetComponent<VerticalLayoutGroup>();
            if (group != null)
            {
                padTop = group.padding.top;
                padBottom = group.padding.bottom;
                spacing = group.spacing;
            }
        }

        if (n <= 0)
            return padTop + padBottom;

        float total = padTop + padBottom + spacing * (n - 1);
        for (int i = 0; i < n; i++)
        {
            var element = listContentRect.GetChild(i)?.GetComponent<LayoutElement>();
            total += element != null && element.preferredHeight > 0f
                ? element.preferredHeight
                : GetLiveRowHeight();
        }

        return total;
    }

    /// <summary>
    /// Live line height: preferredHeight of the first line in the content.
    /// Fallback constant (prefab root LayoutElement.preferredHeight).
    /// </summary>
    private float GetLiveRowHeight()
    {
        if (listContentRect != null)
        {
            for (int i = 0; i < listContentRect.childCount; i++)
            {
                var element = listContentRect.GetChild(i)?.GetComponent<LayoutElement>();
                if (element != null && element.preferredHeight > 0f)
                    return element.preferredHeight;
            }
        }

        return RowHeight;
    }

    /// <summary>
    /// Chrome = everything in the window EXCEPT of ScrollView (top + ButtonRow)
    /// incl. window padding/spacing. Live measured after rebuild, fallback 450
    /// (130 Top + 120 ButtonRow + 100 Padding + 100 Spacing).
    /// </summary>
    private float GetWindowChromeHeight()
    {
        if (windowRect == null)
            return -1f;

        var group = windowRect.GetComponent<VerticalLayoutGroup>();
        float total = 0f;
        int counted = 0;

        for (int i = 0; i < windowRect.childCount; i++)
        {
            var child = windowRect.GetChild(i) as RectTransform;
            if (child == null || child == scrollViewRect)
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
    /// Cap for the ScrollView: Background height (BottomBar -free) - Margin
    /// (200 = 100 above + 100 below) - Chrome. Fresh calculated per open.
    /// </summary>
    private float GetMaxListHeight()
    {
        if (windowRect == null || backgroundRect == null)
            return -1f;

        float bgHeight = backgroundRect.rect.height;
        if (bgHeight <= 0f)
            return -1f;

        float chrome = GetWindowChromeHeight();
        if (chrome < 0f)
            chrome = ChromeFallback;

        return Mathf.Max(bgHeight - windowMarginY - chrome, 0f);
    }

    /// <summary>
    /// Set the ScrollView up synchronously.
    /// Layout rebuild after that can move the offset, therefore, additionally
    /// EnsureScrollReset (coroutine to frame end).
    /// </summary>
    private void ResetScrollTop()
    {
        if (scrollRect == null)
            return;

        if (Mathf.Abs(scrollRect.verticalNormalizedPosition - 1f) > 0.001f)
        {
            scrollRect.StopMovement();
            scrollRect.normalizedPosition = Vector2.one;
        }
    }

    /// <summary>
    /// Starts the delayed scroll reset (runs until after the
    /// Layout rebuild at the frame end.
    /// </summary>
    private void EnsureScrollReset()
    {
        if (!gameObject.activeInHierarchy)
            return;
        if (scrollResetRoutine != null)
            StopCoroutine(scrollResetRoutine);
        scrollResetRoutine = StartCoroutine(ScrollTopAtFrameEnd());
    }

    private IEnumerator ScrollTopAtFrameEnd()
    {
        yield return new WaitForEndOfFrame();

        scrollResetRoutine = null;

        if (!gameObject.activeInHierarchy || scrollRect == null)
            yield break;

        RebuildLayouts();
        ResetScrollTop();
    }
}
