using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SwipeMenu : MonoBehaviour
{
    [Header("References")]
    public ScrollRect scrollRect;
    public SwipeMenuTopBar topBar;
    public SwipeMenuIndicator indicator;

    [Header("Settings")]
    public float snapSpeed = 10f;

    private int pageCount;
    private int currentPage = 0;
    private float[] pagePositions;

    private bool isDragging;
    private bool wasDragging;

    public Action<int> OnPageChanged;

    private void Start()
    {
        pageCount = scrollRect.content.childCount;

        pagePositions = new float[pageCount];
        float step = (pageCount <= 1) ? 0 : 1f / (pageCount - 1);

        for (int i = 0; i < pageCount; i++)
            pagePositions[i] = i * step;

        scrollRect.onValueChanged.AddListener(OnScroll);

        topBar.Init(this, pageCount);
        indicator.Init(pageCount);

        SetPage(0);
    }

    private void Update()
    {
        if (isDragging) return;

        float target = pagePositions[currentPage];

        scrollRect.horizontalNormalizedPosition = Mathf.Lerp(
            scrollRect.horizontalNormalizedPosition,
            target,
            Time.deltaTime * snapSpeed
        );
    }

    private void OnScroll(Vector2 pos)
    {
        float current = scrollRect.horizontalNormalizedPosition;

        indicator.UpdatePosition(current);

        if (isDragging)
        {
            wasDragging = true;
            int nearest = GetNearestPage(current);
            topBar.SetActive(nearest);
            return;
        }

        if (wasDragging)
        {
            wasDragging = false;

            scrollRect.velocity = Vector2.zero;

            int nearest = GetNearestPage(current);
            SetPage(nearest);
        }
    }

    private int GetNearestPage(float value)
    {
        int nearest = 0;
        float minDist = Mathf.Abs(value - pagePositions[0]);

        for (int i = 1; i < pagePositions.Length; i++)
        {
            float dist = Mathf.Abs(value - pagePositions[i]);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = i;
            }
        }

        return nearest;
    }

    public void SetPage(int index)
    {
        if (index < 0 || index >= pageCount)
            return;

        currentPage = index;

        topBar.SetActive(index);
        indicator.SetActive(index);

        OnPageChanged?.Invoke(index);
    }

    // 👉 wird vom ScrollRectDragForwarder aufgerufen
    public void SetDragging(bool dragging)
    {
        isDragging = dragging;
    }

    public void SetHorizontalScrollPosition(float normalizedPos)
    {
        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(normalizedPos);
    }

    // optional, falls du ihn noch nutzt
    public void OnDragEnded()
    {
        scrollRect.velocity = Vector2.zero;

        float current = scrollRect.horizontalNormalizedPosition;
        int nearest = GetNearestPage(current);

        SetPage(nearest);
    }

    public int GetCurrentPage()
    {
        return currentPage;
    }

    public void ResetView()
    {
        // Horizontal zurück auf erste Seite
        currentPage = 0;

        scrollRect.velocity = Vector2.zero;
        scrollRect.horizontalNormalizedPosition = pagePositions[0];

        topBar.SetActive(0);
        indicator.SetActive(0);

        // Alle vertikalen ScrollViews zurück nach oben
        ScrollRect[] allScrollRects = scrollRect.content.GetComponentsInChildren<ScrollRect>(true);

        foreach (var sr in allScrollRects)
        {
            // Haupt-Horizontal-ScrollRect ignorieren
            if (sr == scrollRect)
                continue;

            sr.velocity = Vector2.zero;
            sr.verticalNormalizedPosition = 1f;
        }

        OnPageChanged?.Invoke(0);
    }
}