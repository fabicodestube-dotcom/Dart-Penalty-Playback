using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerList : MonoBehaviour
{
    public GameObject prefab;
    public Transform contentParent;

    private List<PlayerListItem> items = new List<PlayerListItem>();

    private PlayerHandler handler;

    public void Initialize(PlayerHandler handler)
    {
        this.handler = handler;
    }

    public void UpdateList(List<BasePlayer> players)
    {
        int i = 0;

        // Update / Create
        for (; i < players.Count; i++)
        {
            BasePlayer player = players[i];

            if (i < items.Count)
            {
                items[i].SetData(player);
            }
            else
            {
                GameObject goObj = Instantiate(prefab, contentParent);
                PlayerListItem item = goObj.GetComponent<PlayerListItem>();

                item.Initialize(this);
                item.SetData(player);

                items.Add(item);
            }
        }

        // Überschüssige deaktivieren
        for (; i < items.Count; i++)
        {
            items[i].gameObject.SetActive(false);
        }

        // Aktivieren falls wieder benötigt
        for (int j = 0; j < players.Count; j++)
        {
            items[j].gameObject.SetActive(true);
        }
    }

    public void RenameClicked(Guid id)
    {
        handler.OpenRenamePopup(id);
    }

    public void DeleteClicked(Guid id)
    {
        handler.OpenDeletePopup(id);
    }
}