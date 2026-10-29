using UnityEngine;
using TMPro;
using System.Linq;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class X01PlayerItem : MonoBehaviour
{
    public TMP_Text nameText, remainingPointsText, avgText, wonLegsText, wonSetsText, lastTurnScoreText;
    public TMP_Text dartsThrownText; 
    public Image highlightPanel;
    public TMP_Text[] lastThrowTexts = new TMP_Text[3];
    public Image[] wallImages = new Image[3];
    public Image[] ceilingImages = new Image[3];

    public List<Image> imagesDarts;

    [Header("Glow Animation Setup")]
    public Image glowFrameImage;          // Das innere Image (das du per Code färbst)
    public BorderGlowAnimator glowAnimator; // Referenz auf den Animator am Glow-Image

    private static readonly Color colorBlack = new Color(20f/255f, 20f/255f, 20f/255f);
    private static readonly Color colorRed = new Color(165f/255f, 42f/255f, 42f/255f);

    private void Awake()
    {
        // Fallback: Animator am Glow-Image selbst finden, falls im Prefab nicht verdrahtet
        if (glowAnimator == null && glowFrameImage != null)
            glowAnimator = glowFrameImage.GetComponent<BorderGlowAnimator>();

        // Reine Anzeige-Texte: aus dem Touch-Raycast nehmen (Klicks laufen
        // über die Button-Grafik des Items, nicht über einzelne Texte).
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            text.raycastTarget = false;
    }

    public void Bind(X01UIStats stats)
    {
        highlightPanel.gameObject.SetActive(stats.isActive);

        // --- Glow nur beim aktiven Spieler, gesteuert über den Animator ---
        if (stats.isActive)
        {
            if (glowFrameImage != null)
            {
                glowFrameImage.gameObject.SetActive(true);
                if (glowAnimator != null)
                    glowAnimator.StartGlow();
            }
        }
        else
        {
            if (glowAnimator != null)
                glowAnimator.StopGlow();
            if (glowFrameImage != null)
            {
                glowFrameImage.gameObject.SetActive(false);
            }
        }
        // ------------------------------------------

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

        MarkDartImages(stats.isBust ? ThemeManager.Instance.GetColor(ThemeColorRole.Error) : ThemeManager.Instance.GetColor(ThemeColorRole.Background2));

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
            _ => t.Value.ToString()
        };
    }

    private void ResetDartSlot(int i)
    {
        lastThrowTexts[i].gameObject.SetActive(false);
        wallImages[i].gameObject.SetActive(false);
        ceilingImages[i].gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (glowAnimator != null)
            glowAnimator.StopGlow();
    }
}
