// using UnityEngine;
// using UnityEngine.EventSystems;
// using UnityEngine.UI;

// public class NestedScrollRectHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
// {
//     public ScrollRect parentScrollRect;
//     public SwipeMenu swipeMenu; // 🔥 NEU

//     private ScrollRect selfScrollRect;
//     private bool routeToParent = false;

//     private void Awake()
//     {
//         selfScrollRect = GetComponent<ScrollRect>();
//     }

//     public void OnBeginDrag(PointerEventData eventData)
//     {
//         routeToParent = Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y);

//         if (routeToParent)
//         {
//             swipeMenu.SetDragging(true); // 🔥 WICHTIG
//             parentScrollRect.OnBeginDrag(eventData);
//         }
//         else
//         {
//             swipeMenu.SetDragging(false);
//             selfScrollRect.OnBeginDrag(eventData);
//         }
//     }

//     public void OnDrag(PointerEventData eventData)
//     {
//         if (routeToParent)
//         {
//             parentScrollRect.OnDrag(eventData);
//         }
//         else
//         {
//             selfScrollRect.OnDrag(eventData);
//         }
//     }

//     public void OnEndDrag(PointerEventData eventData)
//     {
//         if (routeToParent)
//         {
//             parentScrollRect.OnEndDrag(eventData);
//             swipeMenu.SetDragging(false); // 🔥 wichtig
//             swipeMenu.OnDragEnded();      // 🔥 snap auslösen
//         }
//         else
//         {
//             selfScrollRect.OnEndDrag(eventData);
//         }

//         routeToParent = false;
//     }
// }