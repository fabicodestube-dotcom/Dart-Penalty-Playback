using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TableView : MonoBehaviour
{
    public Transform rowsContainer;

    public TMP_Text headline;
    public GameObject headerPrefab;
    public GameObject rowPrefab;
    public GameObject dividerPrefab;

    [Header("Layout")]
    public LayoutElement rootLayout;
    public LayoutElement contentLayout;

    [Header("Horizontal-Modus (Penalty-Tabellen)")]
    [Tooltip("An: feste Spaltenbreiten + horizontaler Scroll statt Gleichverteilung. Opt-in, andere Tabellen unberührt.")]
    public bool horizontalScroll = false;
    [Tooltip("Feste Breite je Daten-Spalte im Horizontal-Modus.")]
    public float fixedColumnWidth = 72f;

    /// <summary>Header of last Build() call (for tap wiring).</summary>
    public DynamicTableRow HeaderRow { get; private set; }

    private float[] columnWidths;
    private string originalTitle;
    private TableCellData[] originalHeader;

    /// <summary>Last valid column widths (fallback, see CalculateColumnWidths).</summary>
    private float[] lastGoodColumnWidths;
    private ScrollRect horizontalScrollRect;
    private LayoutElement horizontalViewportLayout;

    private class RowEntry
    {
        public GameObject divider;
        public GameObject row;
        public DynamicTableRow rowComponent;
    }

    private readonly List<RowEntry> rowEntries = new List<RowEntry>();
    private readonly Dictionary<string, RowEntry> keyedRows = new Dictionary<string, RowEntry>();
    public IReadOnlyList<GameObject> DataRows => rowEntries.Select(e => e.row).ToArray();

    // Simple object pools to avoid frequent Instantiate/Destroy
    private readonly Stack<GameObject> rowPool = new Stack<GameObject>();
    private readonly Stack<GameObject> dividerPool = new Stack<GameObject>();

    private int rowCount = 0;

    private const float RowHeight = 160;
    private const float HeadlineHeight = 150f;
    private const float DividerHeight = 2f;

    public void Build(string title, TableCellData[] header)
    {
        Clear();
        originalHeader = header;
        SetTitle(title);

        // Perf (M1): Title is never interactive.
        if (headline != null && headline.raycastTarget)
            headline.raycastTarget = false;

        rowCount = 0;

        // Avoid forcing canvas/layout rebuilds here; calculate column widths directly.
        columnWidths = CalculateColumnWidths(header);

        var headerRow = Instantiate(headerPrefab, rowsContainer).GetComponent<DynamicTableRow>();
        headerRow.Setup(" ", LocalizeHeader(header), columnWidths, true);
        HeaderRow = headerRow;
        // Explicit forward: Clear() retains old children, Instantiate hangs
        // Back to, pool-reuse does not re-sort (SetParent retains position).
        headerRow.transform.SetAsFirstSibling();

        if (horizontalScroll)
            EnsureHorizontalScroll();

        rowCount = 1;
        UpdateLayoutHeights(0, 0);
    }

    public GameObject AddRow(string name, TableCellData[] values)
    {
        GameObject dividerObject = null;
        if (dividerPrefab != null && rowCount > 1)
        {
            dividerObject = GetPooledDivider();
        }

        var row = GetPooledRow().GetComponent<DynamicTableRow>();
        row.Setup(name, values, columnWidths, false);
        row.ClearCellTaps();
        rowEntries.Add(new RowEntry
        {
            divider = dividerObject,
            row = row.gameObject,
            rowComponent = row
        });

        rowCount++;
        UpdateLayoutHeights(rowCount - 1, rowCount - 2);
        return row.gameObject;
    }

    public GameObject AddOrUpdateRow(string key, string name, TableCellData[] values)
    {
        return AddOrUpdateRow(key, name, values, true);
    }

    public GameObject AddOrUpdateRow(string key, string name, TableCellData[] values, bool updateLayout)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (keyedRows.TryGetValue(key, out var existingEntry))
        {
            UpdateRow(existingEntry, name, values);
            return existingEntry.row;
        }

        GameObject dividerObject = null;
        if (dividerPrefab != null && rowCount > 1)
        {
            dividerObject = GetPooledDivider();
        }

        var row = GetPooledRow().GetComponent<DynamicTableRow>();
        row.Setup(name, values, columnWidths, false);
        row.ClearCellTaps();

        var entry = new RowEntry
        {
            divider = dividerObject,
            row = row.gameObject,
            rowComponent = row
        };

        rowEntries.Add(entry);
        keyedRows[key] = entry;

        rowCount++;
        if (updateLayout)
            UpdateLayoutHeights(rowCount - 1, rowCount - 2);
        return row.gameObject;
    }

    public bool HasRow(string key)
    {
        return !string.IsNullOrEmpty(key) && keyedRows.ContainsKey(key);
    }

    public IEnumerable<string> GetRowKeys()
    {
        return keyedRows.Keys;
    }

    public bool TryGetRow(string key, out GameObject row)
    {
        if (!string.IsNullOrEmpty(key) && keyedRows.TryGetValue(key, out var entry))
        {
            row = entry.row;
            return true;
        }

        row = null;
        return false;
    }

    /// <summary>
    /// Switches a line. Returns true if an existing line
    /// has REALLY changed its state (only then the table height must be
    /// recalculated via ApplyLayout.
    /// </summary>
    public bool SetRowActive(string key, bool active)
    {
        if (!string.IsNullOrEmpty(key) && keyedRows.TryGetValue(key, out var entry) && entry.row != null)
        {
            if (entry.row.activeSelf != active)
            {
                entry.row.SetActive(active);
                return true;
            }
        }

        return false;
    }

    private void UpdateRow(RowEntry entry, string name, TableCellData[] values)
    {
        if (entry == null || entry.row == null)
            return;

        var row = entry.rowComponent;
        if (row == null)
        {
            row = entry.row.GetComponent<DynamicTableRow>();
            entry.rowComponent = row;
        }

        if (row != null)
        {
            row.Setup(name, values, columnWidths, false);
        row.ClearCellTaps();
        }
    }

    public void ClearRows()
    {
        foreach (var entry in rowEntries)
        {
            if (entry.row != null)
            {
                var go = entry.row;
                go.SetActive(false);
                rowPool.Push(go);
            }
            if (entry.divider != null)
            {
                var d = entry.divider;
                d.SetActive(false);
                dividerPool.Push(d);
            }
        }

        rowEntries.Clear();
        keyedRows.Clear();
        rowCount = 1;
        UpdateLayoutHeights(0, 0);
    }

    public void SetTitle(string title)
    {
        originalTitle = title;
        if (headline != null)
            headline.text = LocalizationText.GetByEnglish(title, title);
    }

    public void SetHeader(TableCellData[] header)
    {
        originalHeader = header;
        if (HeaderRow == null)
            return;

        columnWidths = CalculateColumnWidths(header);
        HeaderRow.Setup(" ", LocalizeHeader(header), columnWidths, true);
    }

    public void RefreshLocalization()
    {
        if (headline != null)
            headline.text = LocalizationText.GetByEnglish(originalTitle, originalTitle);

        if (HeaderRow != null && originalHeader != null)
        {
            columnWidths = CalculateColumnWidths(originalHeader);
            HeaderRow.Setup(" ", LocalizeHeader(originalHeader), columnWidths, true);
        }
    }

    private static TableCellData[] LocalizeHeader(TableCellData[] header)
    {
        if (header == null)
            return null;

        var localized = new TableCellData[header.Length];
        for (int i = 0; i < header.Length; i++)
        {
            localized[i] = header[i];
            if (!header[i].isIcon)
                localized[i].text = LocalizationText.GetByEnglish(header[i].text, header[i].text);
        }
        return localized;
    }

    public void ApplyLayout()
    {
        int activeRows = 0;
        int activeDividers = 0;
        bool hasVisibleDataRow = false;

        foreach (var entry in rowEntries)
        {
            bool rowVisible = entry.row != null && entry.row.activeSelf;
            if (entry.divider != null)
            {
                bool showDivider = rowVisible && hasVisibleDataRow;
                entry.divider.SetActive(showDivider);
                if (showDivider)
                    activeDividers++;
            }

            if (rowVisible)
            {
                activeRows++;
                hasVisibleDataRow = true;
            }
            else if (entry.divider != null)
            {
                entry.divider.SetActive(false);
            }
        }

        UpdateLayoutHeights(activeRows, activeDividers);

        if (horizontalScroll)
            ApplyRowWidths();
    }

    public void RefreshLayout()
    {
        ApplyLayout();
    }

    private void UpdateLayoutHeights(int activeRowCount, int activeDividerCount)
    {
        float totalHeight =
            HeadlineHeight +
            ((activeRowCount + 1) * RowHeight) +
            (activeDividerCount * DividerHeight);

        // Just write what changes — each setter directs the layout.
        if (rootLayout != null)
        {
            if (!Mathf.Approximately(rootLayout.preferredHeight, totalHeight))
                rootLayout.preferredHeight = totalHeight;
            if (!Mathf.Approximately(rootLayout.minHeight, totalHeight))
                rootLayout.minHeight = totalHeight;
        }
            

        if (contentLayout != null)
        {
            float prefHeight = ((activeRowCount + 1) * RowHeight) + (activeDividerCount * DividerHeight);
            if (!Mathf.Approximately(contentLayout.preferredHeight, prefHeight))
                contentLayout.preferredHeight = prefHeight;
            if (!Mathf.Approximately(contentLayout.minHeight, prefHeight))
                contentLayout.minHeight = prefHeight;
        }
            
    }

    private void Clear()
    {
        for (int i = rowsContainer.childCount - 1; i >= 0; i--)
        {
            var go = rowsContainer.GetChild(i).gameObject;
            go.SetActive(false);
            rowPool.Push(go);
        }

        rowEntries.Clear();
        keyedRows.Clear();
        rowCount = 0;
    }

    /// <summary>Number of currently pooled (inactive) reserve rows.</summary>
    public int PooledSpareRows => rowPool.Count;

    /// <summary>
    /// Ensures that at least `desired` inactive reserve rows (with
    /// full cell expansion) are in the pool. New players therefore
    /// immediately find finished rows — no instantiate hit in visible operation.
    /// These are not entries: no influence on visible rows or
    /// layout heights.
    /// </summary>
    public void EnsureSpareRows(int desired)
    {
        if (desired <= 0 || rowPrefab == null || rowsContainer == null)
            return;
        if (columnWidths == null)
            return; // Only after the first build (needs column widths).

        int cellCount = Mathf.Max(1, columnWidths.Length - 1);

        while (rowPool.Count < desired)
        {
            var go = Instantiate(rowPrefab, rowsContainer);
            var row = go.GetComponent<DynamicTableRow>();
            if (row == null)
            {
                Destroy(go);
                return;
            }

            // Full cell expansion with dummy values (one time), then inactive
            // Real values come in later setup.
            row.Setup(" ", new TableCellData[cellCount], columnWidths, false);
            row.ClearCellTaps();
            go.SetActive(false);
            rowPool.Push(go);

            if (dividerPrefab != null)
            {
                var divider = Instantiate(dividerPrefab, rowsContainer);
                divider.SetActive(false);
                dividerPool.Push(divider);
            }
        }
    }

    private GameObject GetPooledRow()
    {
        if (rowPool.Count > 0)
        {
            var go = rowPool.Pop();
            go.transform.SetParent(rowsContainer, false);
            go.transform.SetAsLastSibling();
            go.SetActive(true);
            return go;
        }

        return Instantiate(rowPrefab, rowsContainer);
    }

    private GameObject GetPooledDivider()
    {
        if (dividerPool.Count > 0)
        {
            var go = dividerPool.Pop();
            go.transform.SetParent(rowsContainer, false);
            go.transform.SetAsLastSibling();
            go.SetActive(true);
            return go;
        }

        return Instantiate(dividerPrefab, rowsContainer);
    }

    private float[] CalculateColumnWidths(TableCellData[] headerValues)
    {
        float[] widths = new float[headerValues.Length + 1];

        // Player column retains its width (optics as before).
        widths[0] = 300f;

        if (horizontalScroll)
        {
            // Fixed widths: long names inflate nothing (ShortLabel ).
            for (int i = 0; i < headerValues.Length; i++)
                widths[i + 1] = Mathf.Max(1f, fixedColumnWidth);
            return widths;
        }

        float totalWidth = rowsContainer != null
            ? ((RectTransform)rowsContainer).rect.width
            : 0f;

        // Without live layout (inactive/before first rebuild) the width is 0 —
        // then fall back to the last valid width instead of creating negative
        // column widths.
        if (totalWidth <= 0f
            && lastGoodColumnWidths != null
            && lastGoodColumnWidths.Length == headerValues.Length + 1)
            return (float[])lastGoodColumnWidths.Clone();

        float playerWidth = 300f;
        float remaining = Mathf.Max(0f, totalWidth - playerWidth - 60);

        widths[0] = playerWidth;

        float cellWidth = remaining / headerValues.Length;

        for (int i = 0; i < headerValues.Length; i++)
            widths[i + 1] = cellWidth;

        lastGoodColumnWidths = widths;
        return widths;
    }

    /// <summary>
    /// Hangs the rows area uniquely in a horizontal ScrollRect
    /// (Viewport + RectMask2D, horizontal only). Idempotent, horizontal only.
    /// </summary>
    private void EnsureHorizontalScroll()
    {
        if (!horizontalScroll || rowsContainer == null)
            return;
        if (horizontalScrollRect != null)
            return;

        var parent = rowsContainer.parent;
        if (parent == null)
            return;
        int siblingIndex = rowsContainer.GetSiblingIndex();

        var viewport = new GameObject("HScrollViewport", typeof(RectTransform));
        viewport.transform.SetParent(parent, false);
        viewport.transform.SetSiblingIndex(siblingIndex);

        // Anchor explicit: Viewport fills the table width (stretched).
        // (Freshly created RectTransforms have engine defaults — on this
        // Let us not leave ourselves.
        var viewportRect = viewport.transform as RectTransform;
        if (viewportRect != null)
        {
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
        }

        var viewportLayout = viewport.AddComponent<LayoutElement>();
        viewportLayout.flexibleWidth = 1f;
        viewportLayout.flexibleHeight = 1f;

        // RectMask2D instead of mask: pure rectangular clipping without stencil and
        // without graphics (Stencil masks like to swallow everything on mobile GPUs).
        viewport.AddComponent<RectMask2D>();

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;

        rowsContainer.SetParent(viewport.transform, false);
        scroll.content = rowsContainer as RectTransform;

        // Content geometry explicit (one-time): bottom-left + pivot (0,0) +
        // Position (0.0) + size from ApplyRowWidths. rendered with Center Pivot
        // The content is half-sized (bottom right) and the
        // Scroll mapping runs next to it — exactly the image observed.
        var contentRectPre = rowsContainer as RectTransform;
        if (contentRectPre != null)
        {
            contentRectPre.anchorMin = Vector2.zero;
            contentRectPre.anchorMax = Vector2.zero;
            contentRectPre.pivot = Vector2.zero;
            contentRectPre.anchoredPosition = Vector2.zero;
        }

        horizontalScrollRect = scroll;
        horizontalViewportLayout = viewportLayout;

        var containerLayout = rowsContainer.GetComponent<VerticalLayoutGroup>();
        if (containerLayout != null)
        {
            // Rows keep their set width (no stretching/compressing).
            containerLayout.childControlWidth = false;
            containerLayout.childForceExpandWidth = false;
        }

        // We explicitly set width (ApplyRowWidths) — an existing one if applicable
        // Fitter is not allowed to have a say horizontally (vertical remains as in the prefab).
        var existingFitter = rowsContainer.GetComponent<ContentSizeFitter>();
        if (existingFitter != null)
            existingFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    /// <summary>
    /// Fixes content size (width = column sum, height = table height) and
    /// No ContentSizeFitter here: He struggles with
    /// the fitter further up in the hierarchy around the size calculation.
    /// </summary>
    private void ApplyRowWidths()
    {
        if (!horizontalScroll || rowsContainer == null)
            return;

        float totalWidth = 300f;
        if (columnWidths != null)
        {
            totalWidth = 0f;
            foreach (var w in columnWidths)
                totalWidth += w;
        }

        float contentHeight = contentLayout != null
            ? Mathf.Max(1f, contentLayout.preferredHeight)
            : 1f;

        var contentRect = rowsContainer as RectTransform;
        if (contentRect != null)
            contentRect.sizeDelta = new Vector2(totalWidth, contentHeight);

        for (int i = 0; i < rowsContainer.childCount; i++)
        {
            var child = rowsContainer.GetChild(i);
            var layout = child.GetComponent<LayoutElement>();
            if (layout == null)
                layout = child.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = totalWidth;
            layout.preferredWidth = totalWidth;
            layout.flexibleWidth = 0f;
        }

        if (horizontalViewportLayout != null)
        {
            horizontalViewportLayout.preferredHeight = contentHeight;
            horizontalViewportLayout.minHeight = contentHeight;
        }
    }
}