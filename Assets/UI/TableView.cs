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

    private float[] columnWidths;

    private class RowEntry
    {
        public GameObject divider;
        public GameObject row;
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
        SetTitle(title);
        rowCount = 0;

        // Avoid forcing canvas/layout rebuilds here; calculate column widths directly.
        columnWidths = CalculateColumnWidths(header);

        var headerRow = Instantiate(headerPrefab, rowsContainer).GetComponent<DynamicTableRow>();
        headerRow.Setup(" ", header, columnWidths, true);

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
        rowEntries.Add(new RowEntry
        {
            divider = dividerObject,
            row = row.gameObject
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
            UpdateRow(existingEntry.row, name, values);
            return existingEntry.row;
        }

        GameObject dividerObject = null;
        if (dividerPrefab != null && rowCount > 1)
        {
            dividerObject = GetPooledDivider();
        }

        var row = GetPooledRow().GetComponent<DynamicTableRow>();
        row.Setup(name, values, columnWidths, false);

        var entry = new RowEntry
        {
            divider = dividerObject,
            row = row.gameObject
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

    public void SetRowActive(string key, bool active)
    {
        if (!string.IsNullOrEmpty(key) && keyedRows.TryGetValue(key, out var entry) && entry.row != null)
        {
            entry.row.SetActive(active);
        }
    }

    private void UpdateRow(GameObject rowObject, string name, TableCellData[] values)
    {
        if (rowObject == null)
            return;

        var row = rowObject.GetComponent<DynamicTableRow>();
        if (row != null)
        {
            row.Setup(name, values, columnWidths, false);
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
        headline.text = title;
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

        if (rootLayout != null)
        {
            rootLayout.preferredHeight = totalHeight;
            rootLayout.minHeight = totalHeight;
        }
            

        if (contentLayout != null)
        {
            float prefHeight = ((activeRowCount + 1) * RowHeight) + (activeDividerCount * DividerHeight);
            contentLayout.preferredHeight = prefHeight;
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

    private GameObject GetPooledRow()
    {
        if (rowPool.Count > 0)
        {
            var go = rowPool.Pop();
            go.transform.SetParent(rowsContainer, false);
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
            go.SetActive(true);
            return go;
        }

        return Instantiate(dividerPrefab, rowsContainer);
    }

    private float[] CalculateColumnWidths(TableCellData[] headerValues) 
    {
        float totalWidth = ((RectTransform)rowsContainer).rect.width;

        float playerWidth = 300f;
        float remaining = totalWidth - playerWidth - 60;

        float[] widths = new float[headerValues.Length + 1];

        widths[0] = playerWidth;

        float cellWidth = remaining / headerValues.Length;

        for (int i = 0; i < headerValues.Length; i++)
            widths[i + 1] = cellWidth;

        return widths;
    }
}