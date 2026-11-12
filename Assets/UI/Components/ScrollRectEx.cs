using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;

public class ScrollRectEx : ScrollRect {

    private bool routeToParent = false;

    /// <summary>
    /// Send the event ONLY to the components of the ONLY parent,
    /// The interface T is implemented by the higher parents.
    /// </summary>
    private void DoForClosestParent<T>(Action<T> action) where T : IEventSystemHandler
    {
        Transform parent = transform.parent;
        while (parent != null) {
            bool foundComponent = false;
            foreach (var component in parent.GetComponents<Component>()) {
                if (component is T) {
                    action((T)(IEventSystemHandler)component);
                    foundComponent = true; // Component found at this level
                }
            }
            
            // IMPORTANT: Once we have found something on a level, we break off!
            // This prevents the event from moving further up to Parent C.
            if (foundComponent) {
                break; 
            }
            
            parent = parent.parent;
        }
    }

    /// <summary>
    /// If we route to the vertical parent (B), we can do this.
    /// we radically cut off any horizontal movement (X).
    /// </summary>
    private PointerEventData GetFilteredEventData(PointerEventData originalData)
    {
        PointerEventData filteredData = new PointerEventData(EventSystem.current);
        
        // Copy base data
        filteredData.position = originalData.position;
        filteredData.pressPosition = originalData.pressPosition;
        filteredData.dragging = originalData.dragging;
        filteredData.button = originalData.button;

        Vector2 filteredDelta = originalData.delta;

        // If this Child (A) scrolls horizontally and forwards the Event,
        // This means that the user wipes VERTICAL.
        // I absolutely do not see any X movement.
        if (horizontal && !vertical)
        {
            filteredDelta.x = 0; 
        }
        // If you use the script in reverse (vertical child forwards horizontally)
        else if (vertical && !horizontal)
        {
            filteredDelta.y = 0;
        }

        filteredData.delta = filteredDelta;
        return filteredData;
    }

    public override void OnInitializePotentialDrag (PointerEventData eventData)
    {
        DoForClosestParent<IInitializePotentialDragHandler>((parent) => { parent.OnInitializePotentialDrag(eventData); });
        base.OnInitializePotentialDrag (eventData);
    }

    public override void OnDrag (UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (routeToParent)
        {
            PointerEventData filteredData = GetFilteredEventData(eventData);
            DoForClosestParent<IDragHandler>((parent) => { parent.OnDrag(filteredData); });
        }
        else
        {
            base.OnDrag (eventData);
        }
    }

    public override void OnBeginDrag (UnityEngine.EventSystems.PointerEventData eventData)
    {
        // Direction recognition: If we are configured horizontally,
        // But if the vertical movement (y) is predominant, we route to the parent.
        if (!horizontal && Math.Abs(eventData.delta.x) > Math.Abs(eventData.delta.y))
            routeToParent = true;
        else if (!vertical && Math.Abs(eventData.delta.x) < Math.Abs(eventData.delta.y))
            routeToParent = true;
        else
            routeToParent = false;

        if (routeToParent)
        {
            PointerEventData filteredData = GetFilteredEventData(eventData);
            DoForClosestParent<IBeginDragHandler>((parent) => { parent.OnBeginDrag(filteredData); });
        }
        else
        {
            base.OnBeginDrag (eventData);
        }
    }

    public override void OnEndDrag (UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (routeToParent)
        {
            PointerEventData filteredData = GetFilteredEventData(eventData);
            DoForClosestParent<IEndDragHandler>((parent) => { parent.OnEndDrag(filteredData); });
        }
        else
        {
            base.OnEndDrag (eventData);
        }
        routeToParent = false;
    }
}




// using UnityEngine;
// using System.Collections;
// using UnityEngine.UI;
// using System;
// using UnityEngine.EventSystems;

// public class ScrollRectEx : ScrollRect {

// private bool routeToParent = false;

// /// <summary>
// /// Do action for all parents
// /// </summary>
// private void DoForParents<T>(Action<T> action) where T:IEventSystemHandler
// {
// Transform parent = transform.parent;
// while(parent != null) {
// foreach(var component in parent.GetComponents<Component>()) {
// if(component is T)
// action((T)(IEventSystemHandler)component);
// }
// parent = parent.parent;
// }
// }

// /// <summary>
// /// Creates a modified copy of the EventData that only passes the allowed axis
// /// </summary>
// private PointerEventData GetFilteredEventData(PointerEventData originalData)
// {
// // We copy the original event data
// PointerEventData filteredData = new PointerEventData(EventSystem.current);
        
// // Copy important base data
// filteredData.position = originalData.position;
// filteredData.pressPosition = originalData.pressPosition;
// filteredData.dragging = originalData.dragging;
// filteredData.button = originalData.button;

// // Axis filtering based on the configuration of the child- ScrollRects
// Vector2 filteredDelta = originalData.delta;

// if (!horizontal && vertical)
// {
// // If this child ONLY scrolls vertically, it may be sent to the parent
// // ALSO ONLY send vertical movements (Y). X is zeroed.
// filteredDelta.x = 0;
// }
// else if (!vertical && horizontal)
// {
// // If this child ONLY scrolls horizontally, it may be sent to the parent
// // ALSO Send ONLY horizontal movements (X). Y is zeroed.
// filteredDelta.y = 0;
// }

// filteredData.delta = filteredDelta;
// return filteredData;
// }

// /// <summary>
// /// Always route initialize potential drag event to parents
// /// </summary>
// public override void OnInitializePotentialDrag (PointerEventData eventData)
// {
// DoForParents<IInitializePotentialDragHandler>((parent) => { parent.OnInitializePotentialDrag(eventData); });
// base.OnInitializePotentialDrag (eventData);
// }

// /// <summary>
// /// Drag event
// /// </summary>
// public override void OnDrag (UnityEngine.EventSystems.PointerEventData eventData)
// {
// if(routeToParent)
// {
// // Use the filtered event data for the parent
// PointerEventData filteredData = GetFilteredEventData(eventData);
// DoForParents<IDragHandler>((parent) => { parent.OnDrag(filteredData); });
// }
// else
// {
// base.OnDrag (eventData);
// }
// }

// /// <summary>
// /// Begin drag event
// /// </summary>
// public override void OnBeginDrag (UnityEngine.EventSystems.PointerEventData eventData)
// {
// if(!horizontal && Math.Abs (eventData.delta.x) > Math.Abs (eventData.delta.y))
// routeToParent = true;
// else if(!vertical && Math.Abs (eventData.delta.x) < Math.Abs (eventData.delta.y))
// routeToParent = true;
// else
// routeToParent = false;

// if(routeToParent)
// {
// // Use the filtered event data for the parent
// PointerEventData filteredData = GetFilteredEventData(eventData);
// DoForParents<IBeginDragHandler>((parent) => { parent.OnBeginDrag(filteredData); });
// }
// else
// {
// base.OnBeginDrag (eventData);
// }
// }

// /// <summary>
// /// End drag event
// /// </summary>
// public override void OnEndDrag (UnityEngine.EventSystems.PointerEventData eventData)
// {
// if(routeToParent)
// {
// // Use the filtered event data for the parent
// PointerEventData filteredData = GetFilteredEventData(eventData);
// DoForParents<IEndDragHandler>((parent) => { parent.OnEndDrag(filteredData); });
// }
// else
// {
// base.OnEndDrag (eventData);
// }
// routeToParent = false;
// }
// }
