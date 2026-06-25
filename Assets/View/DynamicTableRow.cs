using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

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
    }

    private readonly List<CellEntry> cells = new List<CellEntry>();

    public void Setup(string playerName, TableCellData[] values, float[] columnWidths, bool header)
    {
        playerNameText.text = playerName;
        BuildCells(values, columnWidths);
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

            if (cellEntry.layout != null)
            {
                float width = columnWidths[i + 1];
                cellEntry.layout.preferredWidth = width;
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
                    cellEntry.text.text = values[i].text;
                    cellEntry.text.alignment = TextAlignmentOptions.Center;
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
