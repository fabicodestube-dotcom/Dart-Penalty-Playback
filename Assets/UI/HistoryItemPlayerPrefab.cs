using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class HistoryItemPlayerPrefab : MonoBehaviour
{
    [Header("Player")]
    public TMP_Text textWinnerOrder;
    public TMP_Text textPlayerName;

    [Header("Metrics")]
    public TMP_Text setsText;
    public TMP_Text legsText;
    public TMP_Text textMetricLabel;

    [Header("Penalties")]
    public TMP_Text textTotalPenaltyCostSum;

    private void Awake()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
            text.raycastTarget = false;
    }

    public void ShowPlayer(
        int rank,
        string playerName,
        string metricLabel,
        GameStats stats,
        int? setsWonOverride = null,
        int? legsWonOverride = null
    )
    {
        this.textWinnerOrder.text = $"{rank}.";
        this.textPlayerName.text = playerName;

        if (stats != null)
        {
            setsText.text = "Sets: " + (setsWonOverride ?? stats.totalSetsWon).ToString();
            legsText.text = "Legs: " + (legsWonOverride ?? stats.currentLegCount).ToString();
            textMetricLabel.text = metricLabel;

            if (textTotalPenaltyCostSum != null)
                textTotalPenaltyCostSum.text = stats.GetTotalPenaltyCosts().ToString("0");
        }
        else
        {
            setsText.text = "Sets: " + (setsWonOverride ?? 0).ToString();
            legsText.text = "Legs: " + (legsWonOverride ?? 0).ToString();
            textMetricLabel.text = metricLabel;

            if (textTotalPenaltyCostSum != null)
                textTotalPenaltyCostSum.text = "0";
        }
    }
}
