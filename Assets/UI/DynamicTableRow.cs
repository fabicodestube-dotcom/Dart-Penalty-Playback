using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DynamicTableRow : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public RectTransform contentContainer;

    public GameObject cellPrefab;

    private class CellEntry
    {
        public GameObject gameObject;
        public TextMeshProUGUI text;
        public Image image;
        public LayoutElement layout;
        public Button button;
    }

    private readonly List<CellEntry> cells = new List<CellEntry>();

    public void Setup(string playerName, TableCellData[] values, float[] columnWidths, bool header)
    {
        // Perf: Zelltexte sind nie interaktiv. Nur schreiben was sich ändert —
        // jeder TMP-Setter baut das Mesh neu.
        if (playerNameText != null)
        {
            if (playerNameText.text != playerName)
                playerNameText.text = playerName;
            if (playerNameText.raycastTarget)
                playerNameText.raycastTarget = false;
        }

        BuildCells(values, columnWidths);
    }

    /// <summary>
    /// Hängt an jede SICHTBARE Daten-Zelle einen Tap (Index = Spaltenindex).
    /// Für Kopfzeilen (Detail-Popup pro Spalte). Idempotent: alte Listener
    /// werden entfernt (wichtig bei gepoolten Rows). Unsichtbare Zellen
    /// (gepoolte Überhänge) bekommen keinen Tap.
    /// </summary>
    public void WireCellTaps(Action<int> onCellTapped)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell == null || cell.gameObject == null || !cell.gameObject.activeSelf)
                continue;

            int index = i;
            var button = cell.button;
            if (button == null)
            {
                button = cell.gameObject.GetComponent<Button>();
                if (button == null)
                    button = cell.gameObject.AddComponent<Button>();
                cell.button = button;
            }

            var graphic = cell.gameObject.GetComponent<Image>();
            if (graphic == null)
            {
                graphic = cell.gameObject.AddComponent<Image>();
                graphic.color = new Color(0f, 0f, 0f, 0f);
            }
            graphic.raycastTarget = true;

            button.onClick.RemoveAllListeners();
            if (onCellTapped != null)
                button.onClick.AddListener(() => onCellTapped(index));
        }
    }

    /// <summary>
    /// Entfernt Zell-Taps (Gegenstück zu WireCellTaps): gepoolte Ex-Header
    /// dürfen als Daten-Rows keine alten Popup-Taps behalten.
    /// </summary>
    public void ClearCellTaps()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell == null || cell.gameObject == null)
                continue;

            var button = cell.button;
            if (button == null)
            {
                button = cell.gameObject.GetComponent<Button>();
                cell.button = button;
            }
            if (button != null)
                button.onClick.RemoveAllListeners();
        }
    }

    private void BuildCells(TableCellData[] values, float[] columnWidths)
    {
        int requiredCells = values.Length;

        for (int i = 0; i < requiredCells; i++)
        {
            CellEntry cellEntry;
            if (i < cells.Count)
            {
                cellEntry = cells[i];
                cellEntry.gameObject.SetActive(true);
            }
            else
            {
                var cellObject = Instantiate(cellPrefab, contentContainer);
                // Prefer a TableCell component on the prefab to avoid GetComponentInChildren each time
                var tableCell = cellObject.GetComponent<TableCell>();
                cellEntry = new CellEntry
                {
                    gameObject = cellObject,
                    layout = tableCell != null ? tableCell.layoutElement : cellObject.GetComponent<LayoutElement>(),
                    text = tableCell != null ? tableCell.text : cellObject.GetComponentInChildren<TextMeshProUGUI>(true),
                    image = tableCell != null ? tableCell.image : cellObject.GetComponentInChildren<Image>(true)
                };
                cells.Add(cellEntry);
            }

            if (cellEntry.layout != null && columnWidths != null && i + 1 < columnWidths.Length)
            {
                float width = columnWidths[i + 1];
                if (!Mathf.Approximately(cellEntry.layout.preferredWidth, width))
                    cellEntry.layout.preferredWidth = width;
                if (!Mathf.Approximately(cellEntry.layout.minWidth, width))
                    cellEntry.layout.minWidth = width;
            }

            if (values[i].isIcon)
            {
                if (cellEntry.text != null) cellEntry.text.enabled = false;
                if (cellEntry.image != null)
                {
                    cellEntry.image.enabled = true;
                    cellEntry.image.sprite = values[i].icon;
                    cellEntry.image.preserveAspect = true;
                }
            }
            else
            {
                if (cellEntry.image != null) cellEntry.image.enabled = false;
                if (cellEntry.text != null)
                {
                    cellEntry.text.enabled = true;
                    if (cellEntry.text.text != values[i].text)
                        cellEntry.text.text = values[i].text;
                    if (cellEntry.text.alignment != TextAlignmentOptions.Center)
                        cellEntry.text.alignment = TextAlignmentOptions.Center;
                    // Perf (M1): Zelltexte sind nie interaktiv.
                    if (cellEntry.text.raycastTarget)
                        cellEntry.text.raycastTarget = false;
                }
            }
        }

        for (int i = cells.Count - 1; i >= requiredCells; i--)
        {
            if (cells[i].gameObject != null)
                cells[i].gameObject.SetActive(false);
        }
    }
}


[System.Serializable]
public struct TableCellData
{
    public string text;
    public Sprite icon;
    public bool isIcon; // Bestimmt, ob Text oder Bild angezeigt wird

    // Hilfsmethode für schnelles Erstellen von Text-Zellen
    public static implicit operator TableCellData(string t) => new TableCellData { text = t, isIcon = false };
}
