using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerDropZone : MonoBehaviour
{
    public enum ZoneType { Active, Reserve }

    public AppHandler appHandler;
    public Transform contentParent;
    public ZoneType zoneType;
    [SerializeField] private PlayerDropZone otherZone;


    public int GetDropIndex(RectTransform dragged)
    {
        List<RectTransform> items = new List<RectTransform>();

        for (int i = 0; i < contentParent.childCount; i++)
        {
            Transform child = contentParent.GetChild(i);

            if (child == null) continue;
            if (child.name == "Placeholder") continue;

            RectTransform rt = child as RectTransform;
            if (rt == null) continue;

            items.Add(rt);
        }

        // stabiler Snapshot (verhindert Layout feedback loop)
        items.Sort((a, b) => b.position.y.CompareTo(a.position.y));

        for (int i = 0; i < items.Count; i++)
        {
            if (dragged.position.y > items[i].position.y)
                return i;
        }

        return items.Count;
    }

    public bool IsPointerOver(PointerEventData eventData)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<PlayerDragHandler>();
        if (drag == null) return;

        StartCoroutine(CommitDrop());
    }

    private IEnumerator CommitDrop()
    {
        yield return null;

        // Allow Unity's layout system to update across the next frame(s) without forcing an immediate rebuild.
        yield return null;

        SyncZones();
    }

    private void SyncZones()
    {
        PlayerDropZone active = zoneType == ZoneType.Active ? this : otherZone;
        PlayerDropZone reserve = zoneType == ZoneType.Active ? otherZone : this;

        appHandler.SetPlayers(active.GetIDs(), reserve.GetIDs());
    }

        public List<Guid> GetIDs()
    {
        List<Guid> ids = new List<Guid>();

        for (int i = 0; i < contentParent.childCount; i++)
        {
            var drag = contentParent.GetChild(i).GetComponent<PlayerDragHandler>();
            if (drag != null)
                ids.Add(drag.playerID);
        }

        return ids;
    }
}