using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ATCPlayerList : MonoBehaviour
{
    public ATCGameEngine gameEngine;
    public AppHandler appHandler;

    public Transform container;
    public GameObject playerItemPrefab;
    public ScrollRect scrollRect;

    private readonly List<ATCPlayerListItem> playerItems = new List<ATCPlayerListItem>();
    private List<BasePlayer> players;

    private Coroutine scrollRoutine;
    private int lastActiveIndex = -1;

    public void Init(List<BasePlayer> activePlayers)
    {
        players = activePlayers;

        foreach (var item in playerItems)
            if (item) Destroy(item.gameObject);

        playerItems.Clear();

        foreach (var p in players)
        {
            var go = Instantiate(playerItemPrefab, container);
            var item = go.GetComponent<ATCPlayerListItem>();

            if (item == null)
            {
                Debug.LogError("ATCPlayerList.Init: Prefab has no ATCPlayerListItem component.");
                Destroy(go);
                continue;
            }

            playerItems.Add(item);
        }

        lastActiveIndex = -1;
        RefreshUI();
    }

    public void Refresh(ATCGame game)
    {
        if (game == null || players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var item = playerItems[i];

            var stats = new ATCUIStats(game, player);

            item.Bind(stats);

            if (stats.isActive && lastActiveIndex != i)
            {
                lastActiveIndex = i;
                ScrollToPlayer(i);
            }
        }
    }

    private void RefreshUI()
    {
        if (gameEngine == null || gameEngine.GetGame() == null)
            return;

        Refresh(gameEngine.GetGame());
    }

    private void ScrollToPlayer(int index)
    {
        if (scrollRect == null || playerItems.Count == 0)
            return;

        if (!gameObject.activeInHierarchy || !scrollRect.gameObject.activeInHierarchy)
            return;

        if (scrollRoutine != null)
            StopCoroutine(scrollRoutine);

        scrollRoutine = StartCoroutine(ScrollToPlayerRoutine(index));
    }

    private IEnumerator ScrollToPlayerRoutine(int index)
    {
        yield return new WaitForEndOfFrame();

        var item = playerItems[index];
        var viewport = scrollRect.viewport;
        var content = scrollRect.content;

        var itemRect = item.GetComponent<RectTransform>();
        var viewportRect = viewport.GetComponent<RectTransform>();

        // Calculate the position to scroll to
        float itemCenter = itemRect.anchoredPosition.y + itemRect.rect.height / 2;
        float viewportHeight = viewportRect.rect.height;
        float contentHeight = content.rect.height;

        float targetY = Mathf.Clamp(itemCenter - viewportHeight / 2, 0, contentHeight - viewportHeight);
        float normalizedPosition = 1 - (targetY / (contentHeight - viewportHeight));

        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
    }
}