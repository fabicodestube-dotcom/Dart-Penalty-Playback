using System.Collections;
using System.Collections.Generic;
using UnityEngine;




public class CricketTwoLayoutPlayerList : MonoBehaviour
{
    public CricketGameEngine gameEngine;
    public AppHandler appHandler;

    public CricketPlayerListItem cricketPlayerListItemLeft;
    public CricketPlayerListItem cricketPlayerListItemRight;

    [SerializeField] private List<CanvasGroup> numberCanvasGroups;

    private List<BasePlayer> players;

    private readonly int[] numberOrder = { 20, 19, 18, 17, 16, 15, 25 };
    private const float LockedAlpha = 0.4f;
    private const float DefaultAlpha = 1f;

    private Dictionary<CanvasGroup, Coroutine> fadeRoutines = new();

    public void Init(List<BasePlayer> activePlayers)
    {
        if (activePlayers == null || activePlayers.Count != 2)
        {
            Debug.LogError("CricketTwoLayoutPlayerList.Init: activePlayers must contain exactly 2 players.");
            return;
        }

        players = activePlayers;


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
            var item = i == 0 ? cricketPlayerListItemLeft : cricketPlayerListItemRight;

            var stats = new CricketUIStats(game, player);
            item.Bind(stats);

            if (stats.isActive)
                activeIndex = i;
        }

        RefreshSideBanner();
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
