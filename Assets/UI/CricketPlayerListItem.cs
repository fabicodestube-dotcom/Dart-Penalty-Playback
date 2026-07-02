using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class CricketNumberCell
{
    [Tooltip("Valid cricket numbers: 15,16,17,18,19,20,25 (bull).")]
    public int number;

    public Image[] hitImages = new Image[3];
    public CanvasGroup lockCanvasGroup;
}

public class CricketPlayerListItem : MonoBehaviour
{
    [Header("Top Row")]
    public TMP_Text nameText;
    public TMP_Text scoreText;
    public GameObject activeIndicator;

    [Header("Cricket Cells (15-20 + Bull=25)")]
    public CricketNumberCell[] numberCells = new CricketNumberCell[7];

    [Header("Bottom Row")]
    public TMP_Text setsText;
    public TMP_Text legsText;
    public TMP_Text dartsText;
    public TMP_Text mprText;

    [Header("Last 3 Throws")]
    public TMP_Text[] lastThrowTexts = new TMP_Text[3];
    public Image[] wallImages = new Image[3];
    public Image[] ceilingImages = new Image[3];

    private const float LockedAlpha = 0.4f;
    private const float UnlockedAlpha = 1f;

    private Dictionary<CanvasGroup, Coroutine> fadeRoutines = new();

    public void Bind(CricketUIStats stats)
    {
        SetHeader(stats);
        SetBottom(stats);
        SetNumbers(stats);
        SetLastThrows(stats.lastThreeThrows);
    }

    private void SetHeader(CricketUIStats stats)
    {
        if (nameText != null)
            nameText.text = stats.playerName;

        if (scoreText != null)
            scoreText.text = stats.currentScore.ToString();

        if (activeIndicator != null)
            activeIndicator.SetActive(stats.isActive);
    }

    private void SetBottom(CricketUIStats stats)
    {
        if (setsText != null)
            setsText.text = stats.wonSets.ToString();

        if (legsText != null)
            legsText.text = stats.wonLegs.ToString();

        if (dartsText != null)
            dartsText.text = stats.thrownDartsCount.ToString();

        if (mprText != null)
            mprText.text = stats.averageMPR.ToString("0.00");
    }

    private void SetNumbers(CricketUIStats stats)
    {
        foreach (var n in stats.numbers)
        {
            UpdateNumberCellInternal(n);
        }
    }

    private void UpdateNumberCellInternal(CricketNumberStats stat)
    {
        var cell = FindCell(stat.number);
        if (cell == null) return;

        int clampedHits = Mathf.Clamp(stat.hits, 0, 3);

        for (int i = 0; i < cell.hitImages.Length; i++)
        {
            var img = cell.hitImages[i];
            if (img != null)
                img.gameObject.SetActive(i < clampedHits);
        }

        if (cell.lockCanvasGroup != null)
        {
            // cell.lockCanvasGroup.alpha = locked ? LockedAlpha : UnlockedAlpha;
            // cell.lockCanvasGroup.interactable = !locked;
            // cell.lockCanvasGroup.blocksRaycasts = !locked;

            bool locked = stat.isLocked;

            float targetAlpha = locked ? LockedAlpha : UnlockedAlpha;

            // 🔥 nur animieren wenn nötig
            if (!Mathf.Approximately(cell.lockCanvasGroup.alpha, targetAlpha))
            {
                FadeTo(cell.lockCanvasGroup, targetAlpha);
            }

            // Interaktion kannst du direkt setzen (kein Fade nötig)
            cell.lockCanvasGroup.interactable = !locked;
            cell.lockCanvasGroup.blocksRaycasts = !locked;
        }
    }

    private CricketNumberCell FindCell(int number)
    {
        if (numberCells == null) return null;

        for (int i = 0; i < numberCells.Length; i++)
        {
            var cell = numberCells[i];
            if (cell != null && cell.number == number)
                return cell;
        }

        return null;
    }

    private void SetLastThrows(List<Throw> lastThrows)
    {
        for (int i = 0; i < 3; i++)
        {
            ResetThrowSlot(i);

            Throw thr = (lastThrows != null && i < lastThrows.Count)
                ? lastThrows[i]
                : null;

            if (thr == null)
            {
                ShowThrowText(i, "-");
                continue;
            }

            if (thr.HitType == HitType.Wall)
            {
                if (i < wallImages.Length && wallImages[i] != null)
                    wallImages[i].gameObject.SetActive(true);
                else
                    ShowThrowText(i, "WALL");

                continue;
            }

            if (thr.HitType == HitType.Ceiling)
            {
                if (i < ceilingImages.Length && ceilingImages[i] != null)
                    ceilingImages[i].gameObject.SetActive(true);
                else
                    ShowThrowText(i, "CEIL");

                continue;
            }

            ShowThrowText(i, GetThrowLabel(thr));
        }
    }

    private void ResetThrowSlot(int index)
    {
        if (index < lastThrowTexts.Length && lastThrowTexts[index] != null)
            lastThrowTexts[index].gameObject.SetActive(false);

        if (index < wallImages.Length && wallImages[index] != null)
            wallImages[index].gameObject.SetActive(false);

        if (index < ceilingImages.Length && ceilingImages[index] != null)
            ceilingImages[index].gameObject.SetActive(false);
    }

    private void ShowThrowText(int index, string text)
    {
        if (index >= lastThrowTexts.Length || lastThrowTexts[index] == null)
            return;

        lastThrowTexts[index].gameObject.SetActive(true);
        lastThrowTexts[index].text = text;
    }

    private string GetThrowLabel(Throw t)
    {
        if (t == null) return "-";

        if (t.Value == 25)
        {
            if (t.Multiplier == DartMultiplier.Double)
                return "DB";

            return "B";
        }

        return t.Multiplier switch
        {
            DartMultiplier.Double => "D" + t.Value,
            DartMultiplier.Triple => "T" + t.Value,
            _ => t.Value.ToString()
        };
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        float startAlpha = cg.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;
            t = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        cg.alpha = targetAlpha;
    }

    private void FadeTo(CanvasGroup cg, float targetAlpha, float duration = 1f)
    {
        if (cg == null) return;

        if (fadeRoutines.TryGetValue(cg, out var running))
        {
            if (running != null)
                StopCoroutine(running);
        }

        var routine = StartCoroutine(FadeCanvasGroup(cg, targetAlpha, duration));
        fadeRoutines[cg] = routine;
    }
}