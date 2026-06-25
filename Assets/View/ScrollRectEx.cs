using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;

public class ScrollRectEx : ScrollRect {

    private bool routeToParent = false;

    /// <summary>
    /// Sendet das Event NUR an die Komponenten des ALLERNÄCHSTEN Parents,
    /// der das Interface T implementiert. Höhere Parents werden ignoriert.
    /// </summary>
    private void DoForClosestParent<T>(Action<T> action) where T : IEventSystemHandler
    {
        Transform parent = transform.parent;
        while (parent != null) {
            bool foundComponent = false;
            foreach (var component in parent.GetComponents<Component>()) {
                if (component is T) {
                    action((T)(IEventSystemHandler)component);
                    foundComponent = true; // Komponente auf dieser Ebene gefunden
                }
            }
            
            // WICHTIG: Sobald wir auf einer Ebene fündig wurden, brechen wir ab!
            // Das verhindert, dass das Event weiter hoch zu Parent C wandert.
            if (foundComponent) {
                break; 
            }
            
            parent = parent.parent;
        }
    }

    /// <summary>
    /// Filtert das Event rigoros. Wenn wir an den vertikalen Parent (B) routen,
    /// schneiden wir jegliche horizontale Bewegung (X) radikal ab.
    /// </summary>
    private PointerEventData GetFilteredEventData(PointerEventData originalData)
    {
        PointerEventData filteredData = new PointerEventData(EventSystem.current);
        
        // Basisdaten kopieren
        filteredData.position = originalData.position;
        filteredData.pressPosition = originalData.pressPosition;
        filteredData.dragging = originalData.dragging;
        filteredData.button = originalData.button;

        Vector2 filteredDelta = originalData.delta;

        // Wenn dieses Child (A) horizontal scrollt und das Event weiterleitet,
        // bedeutet das, der User wischt VERTIKAL. Der Parent (B) darf also 
        // absolut KEINE X-Bewegung sehen.
        if (horizontal && !vertical)
        {
            filteredDelta.x = 0; 
        }
        // Falls du das Skript umgekehrt nutzt (Vertikales Child leitet horizontal weiter)
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
        // Erkennung der Richtung: Wenn wir horizontal konfiguriert sind, 
        // aber die vertikale Bewegung (y) überwiegt, routen wir an den Parent.
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

//     private bool routeToParent = false;

//     /// <summary>
//     /// Do action for all parents
//     /// </summary>
//     private void DoForParents<T>(Action<T> action) where T:IEventSystemHandler
//     {
//         Transform parent = transform.parent;
//         while(parent != null) {
//             foreach(var component in parent.GetComponents<Component>()) {
//                 if(component is T)
//                     action((T)(IEventSystemHandler)component);
//             }
//             parent = parent.parent;
//         }
//     }

//     /// <summary>
//     /// Erstellt eine modifizierte Kopie der EventData, die nur die erlaubte Achse weitergibt
//     /// </summary>
//     private PointerEventData GetFilteredEventData(PointerEventData originalData)
//     {
//         // Wir kopieren die originalen Event-Daten
//         PointerEventData filteredData = new PointerEventData(EventSystem.current);
        
//         // Wichtige Basisdaten kopieren
//         filteredData.position = originalData.position;
//         filteredData.pressPosition = originalData.pressPosition;
//         filteredData.dragging = originalData.dragging;
//         filteredData.button = originalData.button;

//         // Achsen-Filterung basierend auf der Konfiguration des Child-ScrollRects
//         Vector2 filteredDelta = originalData.delta;

//         if (!horizontal && vertical)
//         {
//             // Wenn dieses Child NUR vertikal scrollt, darf es an den Parent 
//             // AUCH NUR vertikale Bewegungen (Y) senden. X wird genullt.
//             filteredDelta.x = 0;
//         }
//         else if (!vertical && horizontal)
//         {
//             // Wenn dieses Child NUR horizontal scrollt, darf es an den Parent 
//             // AUCH NUR horizontale Bewegungen (X) senden. Y wird genullt.
//             filteredDelta.y = 0;
//         }

//         filteredData.delta = filteredDelta;
//         return filteredData;
//     }

//     /// <summary>
//     /// Always route initialize potential drag event to parents
//     /// </summary>
//     public override void OnInitializePotentialDrag (PointerEventData eventData)
//     {
//         DoForParents<IInitializePotentialDragHandler>((parent) => { parent.OnInitializePotentialDrag(eventData); });
//         base.OnInitializePotentialDrag (eventData);
//     }

//     /// <summary>
//     /// Drag event
//     /// </summary>
//     public override void OnDrag (UnityEngine.EventSystems.PointerEventData eventData)
//     {
//         if(routeToParent)
//         {
//             // Nutze die gefilterten Event-Daten für den Parent
//             PointerEventData filteredData = GetFilteredEventData(eventData);
//             DoForParents<IDragHandler>((parent) => { parent.OnDrag(filteredData); });
//         }
//         else
//         {
//             base.OnDrag (eventData);
//         }
//     }

//     /// <summary>
//     /// Begin drag event
//     /// </summary>
//     public override void OnBeginDrag (UnityEngine.EventSystems.PointerEventData eventData)
//     {
//         if(!horizontal && Math.Abs (eventData.delta.x) > Math.Abs (eventData.delta.y))
//             routeToParent = true;
//         else if(!vertical && Math.Abs (eventData.delta.x) < Math.Abs (eventData.delta.y))
//             routeToParent = true;
//         else
//             routeToParent = false;

//         if(routeToParent)
//         {
//             // Nutze die gefilterten Event-Daten für den Parent
//             PointerEventData filteredData = GetFilteredEventData(eventData);
//             DoForParents<IBeginDragHandler>((parent) => { parent.OnBeginDrag(filteredData); });
//         }
//         else
//         {
//             base.OnBeginDrag (eventData);
//         }
//     }

//     /// <summary>
//     /// End drag event
//     /// </summary>
//     public override void OnEndDrag (UnityEngine.EventSystems.PointerEventData eventData)
//     {
//         if(routeToParent)
//         {
//             // Nutze die gefilterten Event-Daten für den Parent
//             PointerEventData filteredData = GetFilteredEventData(eventData);
//             DoForParents<IEndDragHandler>((parent) => { parent.OnEndDrag(filteredData); });
//         }
//         else
//         {
//             base.OnEndDrag (eventData);
//         }
//         routeToParent = false;
//     }
// }
