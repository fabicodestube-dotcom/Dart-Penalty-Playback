using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SwipeMenuIndicator : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform indicator;
    public RectTransform container;
    public SwipeMenu swipeMenu;

    private int count;
    private float width;

    public void Init(int count)
    {
        this.count = count;

        if (container == null || indicator == null || count <= 0)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(container);

        width = container.rect.width / count;
        indicator.sizeDelta = new Vector2(width, indicator.sizeDelta.y);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        swipeMenu?.SetDragging(true);
        UpdateDragPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateDragPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        swipeMenu?.SetDragging(false);
        swipeMenu?.OnDragEnded();
    }

    private void UpdateDragPosition(PointerEventData eventData)
    {
        if (container == null || indicator == null || count <= 0 || swipeMenu == null)
            return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(container, eventData.position, eventData.pressEventCamera, out localPoint);

        float clampedX = Mathf.Clamp(localPoint.x, container.rect.xMin, container.rect.xMax);
        float normalizedPos = Mathf.InverseLerp(container.rect.xMin, container.rect.xMax, clampedX);

        swipeMenu.SetHorizontalScrollPosition(normalizedPos);
    }

    public void UpdatePosition(float normalizedPos)
    {
        float totalWidth = container.rect.width - width;
        float x = normalizedPos * totalWidth;

        indicator.anchoredPosition = new Vector2(x, indicator.anchoredPosition.y);
    }

    public void SetActive(int index)
    {
        float x = width * index;
        indicator.anchoredPosition = new Vector2(x, indicator.anchoredPosition.y);
    }
}