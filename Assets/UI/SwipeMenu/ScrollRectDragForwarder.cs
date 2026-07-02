using UnityEngine;
using UnityEngine.EventSystems;


public class ScrollRectDragForwarder : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public SwipeMenu swipeMenu;

    public void OnBeginDrag(PointerEventData eventData)
    {
        swipeMenu.SetDragging(true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        swipeMenu.SetDragging(false);
        swipeMenu.OnDragEnded();
    }
}