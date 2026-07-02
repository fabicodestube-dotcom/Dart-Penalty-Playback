using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ATCPlayerListItem : MonoBehaviour
{
    [Header("Player Info")]
    public TMP_Text nameText;
    public GameObject activeIndicator;

    [Header("Target Info")]
    public TMP_Text currentTargetText;
    public TMP_Text progressText; // e.g. "5/21"

    [Header("Status")]
    public TMP_Text hitsInRoundText; // Anzahl Treffer in der aktuellen Runde
    public ATCStreakEffect streakIndicator; // Optional: Ein visuelles Element, das anzeigt, ob der Spieler in einer Treffer-Serie ist
    public TMP_Text wonSetsText;
    public TMP_Text wonLegsText;
    public TMP_Text thrownDartsText;
    public TMP_Text hitRateText;
    public Image[] hitImages;
    public Image[] missImages;
    public Image[] wallImages;
    public Image[] ceilingImages;

    public void Bind(ATCUIStats stats)
    {
        if (nameText != null)
            nameText.text = stats.playerName;

        if (activeIndicator != null)
            activeIndicator.SetActive(stats.isActive);

        SetTarget(stats);
        
        if (progressText != null)
            progressText.text = stats.progress;

        if (hitsInRoundText != null)
            hitsInRoundText.text = stats.hitsInRound.ToString();

        if (stats.hitsInRound >= 3)
        {
            if (streakIndicator != null)
                streakIndicator.StartStreak();
        }
        else
        {
            if (streakIndicator != null)
                streakIndicator.StopStreak();
        }

        if (wonSetsText != null)
            wonSetsText.text = stats.wonSets.ToString();

        if (wonLegsText != null)
            wonLegsText.text = stats.wonLegs.ToString();

        if (thrownDartsText != null)
            thrownDartsText.text = stats.thrownDartsCount.ToString();

        if (hitRateText != null)
            hitRateText.text = $"{stats.averageHitRate:F1}%";

        SetLastThreeDarts(stats.lastThreeThrows);
    }

    private void SetTarget(ATCUIStats stats)
    {
        if (currentTargetText == null)
            return;

        if (stats.isFinished)
        {
            currentTargetText.text = "Finished";
            return;
        }

        if (stats.currentTarget == -1)
        {
            currentTargetText.text = "N/A";
            return;
        }

        string prefix = "";

        switch (stats.targetType)
        {
            case ATCTargetType.Doubles:
                prefix = "D";
                break;
            case ATCTargetType.Triples:
                prefix = "T";
                break;
        }

        currentTargetText.text = prefix + stats.currentTarget;
    }

    public void SetLastThreeDarts(System.Collections.Generic.List<Throw> throws)
    {
        for (int i = 0; i < 3; i++)
        {
            SetImageRow(i, false, false, false, false);
        }

        for (int i = 0; i < throws.Count && i < 3; i++)
        {
            var thr = throws[i];
            if (thr.HitType == HitType.Wall)
            {
                SetImageRow(i, false, false, true, false);
            }
            else if (thr.HitType == HitType.Ceiling)
            {
                SetImageRow(i, false, false, false, true);
            }
            else
            {
                bool isHit = thr.IsTargetHit;
                SetImageRow(i, isHit, !isHit, false, false);
            }
        }
    }

    private void SetImageRow(int index, bool hit, bool miss, bool wall, bool ceiling)
    {
        SetImageActive(hitImages, index, hit);
        SetImageActive(missImages, index, miss);
        SetImageActive(wallImages, index, wall);
        SetImageActive(ceilingImages, index, ceiling);
    }

    private void SetImageActive(Image[] images, int index, bool active)
    {
        if (images == null || index < 0 || index >= images.Length)
            return;

        if (images[index] != null)
            images[index].gameObject.SetActive(active);
    }
}