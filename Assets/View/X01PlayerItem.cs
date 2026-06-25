using UnityEngine;
using TMPro;
using System.Linq;
using UnityEngine.UI;
using System.Collections.Generic;

public class X01PlayerItem : MonoBehaviour
{
    public TMP_Text nameText, remainingPointsText, avgText, wonLegsText, wonSetsText, lastTurnScoreText;
    public TMP_Text dartsThrownText; // <-- Neues Feld für die Anzahl geworfener Darts
    public Image highlightPanel;
    public TMP_Text[] lastThrowTexts = new TMP_Text[3];
    public Image[] wallImages = new Image[3];
    public Image[] ceilingImages = new Image[3];

    public List<Image> imagesDarts;

    private static readonly Color colorBlack = new Color(20f/255f, 20f/255f, 20f/255f);
    private static readonly Color colorRed = new Color(165f/255f, 42f/255f, 42f/255f);

    public void Bind(X01UIStats stats)
    {
        highlightPanel.gameObject.SetActive(stats.isActive);

        nameText.text = stats.playerName;
        remainingPointsText.text = stats.currentScore.ToString();
        avgText.text = stats.averageScorePerTurn.ToString();
        wonLegsText.text = stats.wonLegs.ToString();
        wonSetsText.text = stats.wonSets.ToString();
        dartsThrownText.text = stats.thrownDartsCount.ToString();

        var displayThrows = stats.lastThreeThrows;

        for (int i = 0; i < lastThrowTexts.Length; i++)
        {
            ResetDartSlot(i);

            if (i < displayThrows.Count)
            {
                var t = displayThrows[i];

                if (t == null)
                {
                    lastThrowTexts[i].gameObject.SetActive(true);
                    lastThrowTexts[i].text = "-";
                    continue;
                }

                switch (t.HitType)
                {
                    case HitType.Wall:
                        wallImages[i].gameObject.SetActive(true);
                        break;

                    case HitType.Ceiling:
                        ceilingImages[i].gameObject.SetActive(true);
                        break;

                    default:
                        lastThrowTexts[i].gameObject.SetActive(true);
                        lastThrowTexts[i].text = GetDartLabel(t);
                        break;
                }
            }
            else
            {
                lastThrowTexts[i].gameObject.SetActive(true);
                lastThrowTexts[i].text = "-";
            }
        }

        int totalTurnPoints = stats.turnScore;
        Color totalPointsColor = stats.isBust ? colorRed : Color.white;

        MarkDartImages(stats.isBust ? colorRed : colorBlack);

        lastTurnScoreText.text = totalTurnPoints.ToString();
        lastTurnScoreText.color = totalPointsColor;
    }

    private void MarkDartImages(Color c)
    {
        foreach (Image i in imagesDarts)
        {
            i.color = c;
        }
    }

    private string GetDartLabel(Throw t)
    {
        if (t == null) return "-";

        return t.Multiplier switch
        {
            DartMultiplier.Double => "D" + t.Value.ToString(),
            DartMultiplier.Triple => "T" + t.Value.ToString(),
            _ => t.Value.ToString() // Single: nur Zahl
        };
    }

    private void ResetDartSlot(int i)
    {
        lastThrowTexts[i].gameObject.SetActive(false);
        wallImages[i].gameObject.SetActive(false);
        ceilingImages[i].gameObject.SetActive(false);
    }
}