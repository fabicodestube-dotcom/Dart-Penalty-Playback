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
    public TMP_Text textWall;
    public TMP_Text textCeiling;
    public TMP_Text textSchnapszahl;
    public TMP_Text textThreeOnes;
    public TMP_Text textAllMiss;
    public TMP_Text textLostGame;
    public TMP_Text textPenaltySum;

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

            textWall.text = stats.wallCount.ToString();
            textCeiling.text = stats.ceilingCount.ToString();
            textAllMiss.text = stats.allMissCount.ToString();
            textThreeOnes.text = stats.tripleOnesCount.ToString();
            textLostGame.text = stats.lostGame.ToString();

            textSchnapszahl.text = stats.tripleDigitCount.ToString();

            textPenaltySum.text = stats.GetTotalPenaltyCosts().ToString("0");
        }
        else
        {
            setsText.text = "Sets: " + (setsWonOverride ?? 0).ToString();
            legsText.text = "Legs: " + (legsWonOverride ?? 0).ToString();
            textMetricLabel.text = metricLabel;

            textWall.text = "0";
            textCeiling.text = "0";
            textAllMiss.text = "0";
            textThreeOnes.text = "0";
            textLostGame.text = "0";

            textSchnapszahl.text = "0";

            textPenaltySum.text = "0";
        }
    }
}
