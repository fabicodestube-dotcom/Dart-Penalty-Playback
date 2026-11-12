using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class X01PlayerList : MonoBehaviour
{
    public Transform container;
    public GameObject playerItemPrefab;
    public ScrollRect scrollRect;

    private List<X01PlayerItem> playerItems = new List<X01PlayerItem>();

    private Coroutine scrollRoutine;
    private int lastActiveIndex = -1;

    public void Init(List<X01UIStats> stats)
    {
        foreach (var item in playerItems)
            if (item) Destroy(item.gameObject);

        playerItems.Clear();

        foreach (var s in stats)
        {
            var go = Instantiate(playerItemPrefab, container);
            var item = go.GetComponent<X01PlayerItem>();
            item.Bind(s);
            playerItems.Add(item);
        }

        lastActiveIndex = -1; // reset wichtig
        RefreshUI(stats);
    }

    public void RefreshUI(List<X01UIStats> stats)
    {
        if (stats == null)
            return;

        if (stats.Count != playerItems.Count)
        {
            Debug.Log("[X01PlayerList] Anzahl Stats != Anzahl Items!");
        }

        for (int i = 0; i < stats.Count; i++)
        {
            var stat = stats[i];
            var item = playerItems[i];

            item.Bind(stat);

            if (stat.isActive && lastActiveIndex != i)
            {
                lastActiveIndex = i;
                ScrollToActivePlayerSmooth(i);
            }
        }
    }

    private void ScrollToActivePlayerSmooth(int activeIndex)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (scrollRect == null || scrollRect.content == null || !scrollRect.content.gameObject.activeInHierarchy)
            return;

        if (scrollRoutine != null)
            StopCoroutine(scrollRoutine);

        scrollRoutine = StartCoroutine(SmoothScrollCoroutine(activeIndex));
    }

    private IEnumerator SmoothScrollCoroutine(int activeIndex)
    {
        // Use existing layout; avoid forcing a full canvas rebuild here.
        float start = scrollRect.verticalNormalizedPosition;
        float target = CalculateNormalizedPosition(activeIndex);

        float duration = 0.25f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // 👉 Ease-Out (fühlt sich deutlich besser an)
            t = 1f - Mathf.Pow(1f - t, 3f);

            scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, target, t);
            yield return null;
        }

        scrollRect.verticalNormalizedPosition = target;
    }

    private float CalculateNormalizedPosition(int activeIndex)
    {
        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport;
        RectTransform target = playerItems[activeIndex].GetComponent<RectTransform>();

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        // 👉 Pivot-Korrektur: von Mittelpunkt auf Oberkante
        float itemCenterY = Mathf.Abs(target.anchoredPosition.y);
        float itemTop = itemCenterY - (target.rect.height * (1 - target.pivot.y));

        float maxScroll = contentHeight - viewportHeight;
        if (maxScroll <= 0)
            return 1f;

        float targetScrollY = Mathf.Clamp(itemTop, 0, maxScroll);

        return 1f - (targetScrollY / maxScroll);
    }
}