using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CricketDefaultPlayerList : MonoBehaviour
{
    public CricketGameEngine gameEngine;
    public AppHandler appHandler;

    public Transform container;
    public GameObject playerItemPrefab;
    public ScrollRect scrollRect;
    [SerializeField] private List<CanvasGroup> numberCanvasGroups;

    private readonly List<CricketPlayerListItem> playerItems = new List<CricketPlayerListItem>();
    private List<BasePlayer> players;

    private readonly int[] numberOrder = { 20, 19, 18, 17, 16, 15, 25 };

    private Coroutine scrollRoutine;
    private int lastActiveIndex = -1;

    private const float LockedAlpha = 0.4f;
    private const float DefaultAlpha = 1f;

    private Dictionary<CanvasGroup, Coroutine> fadeRoutines = new();

    public void Init(List<BasePlayer> activePlayers)
    {
        Debug.Log("CricketDefaultPlayerList.Init: Initializing with " + activePlayers.Count + " players.");
        players = activePlayers;

        foreach (var item in playerItems)
            if (item) Destroy(item.gameObject);

        playerItems.Clear();

        foreach (var p in players)
        {
            var go = Instantiate(playerItemPrefab, container);
            var item = go.GetComponent<CricketPlayerListItem>();

            if (item == null)
            {
                Debug.LogError("CricketPlayerList.Init: Prefab has no CricketPlayerListItem component.");
                Destroy(go);
                continue;
            }

            playerItems.Add(item);
        }

        lastActiveIndex = -1;
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (gameEngine == null || players == null || players.Count == 0)
            return;

        var game = gameEngine.GetGame();
        if (game == null) return;

        

        int activeIndex = 0;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var item = playerItems[i];

            var stats = new CricketUIStats(game, player);
            item.Bind(stats);

            if (stats.isActive)
                activeIndex = i;
        }

        if (activeIndex != lastActiveIndex)
        {
            ScrollToActivePlayerSmooth(activeIndex);
            lastActiveIndex = activeIndex;
        }

        RefreshSideBanner();
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
        Canvas.ForceUpdateCanvases();

        float start = scrollRect.horizontalNormalizedPosition;
        float target = CalculateNormalizedPosition(activeIndex);

        float duration = 0.25f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);

            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(start, target, t);

            yield return null;
        }

        scrollRect.horizontalNormalizedPosition = target;
    }

    private float CalculateNormalizedPosition(int activeIndex)
    {
        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport;
        RectTransform target = playerItems[activeIndex].GetComponent<RectTransform>();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float contentWidth = content.rect.width;
        float viewportWidth = viewport.rect.width;

        if (contentWidth <= viewportWidth)
            return 0f;

        // Position relativ zum Content-Start
        float targetX = Mathf.Abs(target.anchoredPosition.x);

        // Center item in viewport (optional, aber meistens besser)
        float targetCentered = targetX - (viewportWidth * 0.5f) + (target.rect.width * 0.5f);

        float maxScroll = contentWidth - viewportWidth;
        float clamped = Mathf.Clamp(targetCentered, 0, maxScroll);

        return clamped / maxScroll;
    }


    private void RefreshSideBanner()
    {
        if (gameEngine == null) return;

        var game = gameEngine.GetGame();
        if (game == null) return;

        for (int i = 0; i < numberOrder.Length; i++)
        {
            int number = numberOrder[i];
            bool isLocked = game.IsLocked(number);

            var cg = numberCanvasGroups[i];
            if (cg == null) continue;

            float target = isLocked ? LockedAlpha : DefaultAlpha;

            // 🔥 Nur animieren wenn sich wirklich was ändert
            if (!Mathf.Approximately(cg.alpha, target))
            {
                FadeTo(cg, target);
            }
        }
    }

    private void FadeTo(CanvasGroup cg, float targetAlpha, float duration = 1f)
    {
        if (cg == null) return;

        if (fadeRoutines.TryGetValue(cg, out var running))
        {
            if (running != null)
                StopCoroutine(running);
        }

        var routine = StartCoroutine(FadeCanvasGroup(cg, targetAlpha, duration));
        fadeRoutines[cg] = routine;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        float startAlpha = cg.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = time / duration;
            t = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        cg.alpha = targetAlpha;
    }

}