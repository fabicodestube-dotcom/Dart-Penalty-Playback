using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WindowHandler : MonoBehaviour
{
    [Header("Screens")]
    public List<UIScreen> screens;

    [Header("Optional UI")]
    public MenuSwitcher menuSwitcher;

    [Header("GoBack EdgeCases")]
    [SerializeField] private X01GameEngine x01Engine;
    [SerializeField] private CricketGameEngine cricketEngine;
    [SerializeField] private ATCGameEngine atcEngine;
    [SerializeField] private Summary summaryScreen;

    [Header("Popup")]
    public UIScreen activePopup;
    private bool isPopupActive = false;


    [Header("Quick Menu")]
    [SerializeField] private RectTransform quickMenuPanel;
    [SerializeField] private CanvasGroup quickMenuCanvas;

    private bool isQuickMenuOpen = false;

    private Dictionary<ScreenId, UIScreen> screenMap = new();
    private Dictionary<(ScreenId, ScreenId), (TransitionType type, SlideDirection dir)> transitions = new();

    private Stack<ScreenId> history = new();

    private UIScreen current;
    private ScreenId currentId;
    private ScreenId? previousId;


    private bool isTransitioning = false;


    // =========================================================
    // LIFECYCLE
    // =========================================================

    void Awake()
    {
        // Screen-Lookup für schnellen Zugriff bauen
        foreach (var s in screens)
            screenMap[s.id] = s;

        InitTransitions();

        InitQuickMenu();

        // Startscreen setzen
        SetInitial(ScreenId.Zoggen);
    }

    void Update()
    {
        // ESC Handling:
        // - Popup offen → schließen
        // - sonst → Navigation zurück
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPopupActive)
                HidePopup();
            else
                GoBack();
        }
    }


    // =========================================================
    // NAVIGATION
    // =========================================================

    public void GoTo(ScreenId target)
    {
        if (isTransitioning || currentId == target)
            return;

        if (!screenMap.TryGetValue(target, out var targetScreen))
        {
            Debug.LogError($"Screen nicht registriert (screens-Liste/UIScreen.id prüfen): {target}");
            return;
        }

        // Transition bevorzugt explizit definiert; sonst Fallback (verhindert "dead end" Navigation)
        if (!transitions.TryGetValue((currentId, target), out var transition))
        {
            Debug.LogWarning($"Kein Übergang definiert: {currentId} → {target}. Verwende Fallback Fade.");
            transition = (TransitionType.Fade, SlideDirection.Left);
        }

        history.Push(currentId);
        previousId = currentId;

        StartCoroutine(
            Transition(current, targetScreen, target, transition.type, transition.dir)
        );
    }

    public void GoBack()
    {
        if (isTransitioning || history.Count == 0)
            return;

        switch (currentId)
        {
            case ScreenId.X01Game:
                if (x01Engine == null)
                {
                    Debug.LogWarning("X01GameEngine nicht zugewiesen. Suche in Children.");
                    x01Engine = GetComponentInChildren<X01GameEngine>();
                }

                if (x01Engine == null)
                {
                    Debug.LogError("X01GameEngine nicht gefunden. OnClickReturnFromX01 wird nicht aufgerufen.");
                    break;
                }
                x01Engine.OnClickReturnFromX01();
                break;

            case ScreenId.CricketGame:
                if (cricketEngine == null)
                {
                    Debug.LogWarning("CricketGameEngine nicht zugewiesen. Suche in Children.");
                    cricketEngine = GetComponentInChildren<CricketGameEngine>();
                }

                if (cricketEngine == null)
                {
                    Debug.LogError("CricketGameEngine nicht gefunden. OnClickReturnFromCricket wird nicht aufgerufen.");
                    break;
                }
                cricketEngine.OnClickReturnFromCricket();
                break;

            case ScreenId.ATCGame:
                if (atcEngine == null)
                {
                    Debug.LogWarning("ATCGameEngine nicht zugewiesen. Suche in Children.");
                    atcEngine = GetComponentInChildren<ATCGameEngine>();
                }

                if (atcEngine == null)
                {
                    Debug.LogError("ATCGameEngine nicht gefunden. OnClickReturnFromATC wird nicht aufgerufen.");
                    break;
                }
                atcEngine.OnClickReturnFromATC();
                break;
            case ScreenId.Summary:
                if (summaryScreen == null)
                {
                    Debug.LogWarning("Summary Screen nicht zugewiesen. Suche in Children.");
                    summaryScreen = GetComponentInChildren<Summary>();
                }

                if (summaryScreen == null)
                {
                    Debug.LogError("Summary Screen nicht gefunden. OnClickReturnFromSummary wird nicht aufgerufen.");
                    break;
                }
                summaryScreen.OnClickBackButton();
                break;
            default:
                bool wasQuickMenuScreen =
                currentId == ScreenId.Optionen ||
                currentId == ScreenId.About ||
                currentId == ScreenId.CustomClipHandler;

                ScreenId target = history.Pop();

                if (!screenMap.TryGetValue(target, out var targetScreen))
                {
                    Debug.LogError($"Screen nicht registriert (screens-Liste/UIScreen.id prüfen): {target}");
                    return;
                }

                if (!transitions.TryGetValue((currentId, target), out var transition))
                {
                    Debug.LogWarning($"Kein Übergang zurück definiert: {currentId} → {target}. Verwende Fallback Fade.");
                    transition = (TransitionType.Fade, SlideDirection.Right);
                }

                previousId = currentId;

                StartCoroutine(
                    Transition(current, targetScreen, target, transition.type, transition.dir)
                );
                break;
        }
    }

    public void OpenOptions()
    {
        OpenFromQuickMenu(ScreenId.Optionen);
    }

    public void OpenAbout()
    {
        OpenFromQuickMenu(ScreenId.About);
    }

    public void OpenCustomClipHandler()
    {
        OpenFromQuickMenu(ScreenId.CustomClipHandler);
    }

    private void OpenFromQuickMenu(ScreenId target)
    {
        StartCoroutine(CloseQuickMenu());

        GoTo(target);
    }

    public void ToggleQuickMenu()
    {
        if (isTransitioning)
            return;

        if (isQuickMenuOpen)
            StartCoroutine(CloseQuickMenu());
        else
            StartCoroutine(OpenQuickMenu());
    }

    private IEnumerator OpenQuickMenu()
    {
        isQuickMenuOpen = true;

        float duration = 0.18f;
        float t = 0;

        quickMenuCanvas.interactable = true;
        quickMenuCanvas.blocksRaycasts = true;

        while (t < duration)
        {
            float p = t / duration;

            p = Mathf.SmoothStep(0, 1, p);

            quickMenuCanvas.alpha = p;

            quickMenuPanel.localScale = Vector3.Lerp(
                new Vector3(0.8f, 0.8f, 1),
                Vector3.one,
                p
            );

            t += Time.deltaTime;
            yield return null;
        }

        quickMenuCanvas.alpha = 1;
        quickMenuPanel.localScale = Vector3.one;

    }

    private IEnumerator CloseQuickMenu()
    {
        isQuickMenuOpen = false;

        float duration = 0.15f;
        float t = 0;

        while (t < duration)
        {
            float p = t / duration;

            p = Mathf.SmoothStep(0, 1, p);

            quickMenuCanvas.alpha = 1 - p;

            quickMenuPanel.localScale = Vector3.Lerp(
                Vector3.one,
                new Vector3(0.8f, 0.8f, 1),
                p
            );

            t += Time.deltaTime;
            yield return null;
        }

        quickMenuCanvas.alpha = 0;

        quickMenuCanvas.interactable = false;
        quickMenuCanvas.blocksRaycasts = false;

        quickMenuPanel.localScale = new Vector3(0.8f, 0.8f, 1);
    }


    // =========================================================
    // TRANSITIONS DEFINITION
    // =========================================================

    private void InitTransitions()
    {
        // =========================
        // ZOGGEN (Hauptmenü / Hub)
        // =========================

        // Bidirektionale Standard-Fades
        AddBidirectional(ScreenId.Zoggen, ScreenId.Players, TransitionType.Fade);
        AddBidirectional(ScreenId.Zoggen, ScreenId.History, TransitionType.Fade);
        AddBidirectional(ScreenId.Zoggen, ScreenId.Statistic, TransitionType.Fade);

        // Slide Navigation zu Games
        Add(ScreenId.Zoggen, ScreenId.X01Game, TransitionType.Slide, SlideDirection.Left);
        Add(ScreenId.X01Game, ScreenId.Zoggen, TransitionType.Slide, SlideDirection.Right);

        Add(ScreenId.Zoggen, ScreenId.ATCGame, TransitionType.Slide, SlideDirection.Left);
        Add(ScreenId.ATCGame, ScreenId.Zoggen, TransitionType.Slide, SlideDirection.Right);

        Add(ScreenId.Zoggen, ScreenId.CricketGame, TransitionType.Slide, SlideDirection.Left);
        Add(ScreenId.CricketGame, ScreenId.Zoggen, TransitionType.Slide, SlideDirection.Right);

        // Summary
        Add(ScreenId.Zoggen, ScreenId.Summary, TransitionType.Fade, SlideDirection.Left);
        Add(ScreenId.Summary, ScreenId.Zoggen, TransitionType.Fade, SlideDirection.Left);


        // =========================
        // HISTORY
        // =========================

        AddBidirectional(ScreenId.History, ScreenId.Players, TransitionType.Fade);
        AddBidirectional(ScreenId.History, ScreenId.Statistic, TransitionType.Fade);

        Add(ScreenId.History, ScreenId.GameDetail, TransitionType.Fade, SlideDirection.Left);
        Add(ScreenId.GameDetail, ScreenId.History, TransitionType.Fade, SlideDirection.Right);


        // =========================
        // STATISTIC
        // =========================

        AddBidirectional(ScreenId.Statistic, ScreenId.Players, TransitionType.Fade);


        // =========================
        // SUMMARY
        // =========================

        Add(ScreenId.Summary, ScreenId.GameDetail, TransitionType.Fade, SlideDirection.Left);
        Add(ScreenId.GameDetail, ScreenId.Summary, TransitionType.Fade, SlideDirection.Right);

        Add(ScreenId.Summary, ScreenId.X01Game, TransitionType.Fade, SlideDirection.Right);
        Add(ScreenId.X01Game, ScreenId.Summary, TransitionType.Fade, SlideDirection.Left);

        Add(ScreenId.Summary, ScreenId.CricketGame, TransitionType.Fade, SlideDirection.Right);
        Add(ScreenId.CricketGame, ScreenId.Summary, TransitionType.Fade, SlideDirection.Left);

        Add(ScreenId.Summary, ScreenId.ATCGame, TransitionType.Fade, SlideDirection.Left);
        Add(ScreenId.ATCGame, ScreenId.Summary, TransitionType.Fade, SlideDirection.Right);


        // =========================
        // GAME DETAIL
        // =========================

        Add(ScreenId.GameDetail, ScreenId.X01Game, TransitionType.Fade, SlideDirection.Left);
        Add(ScreenId.X01Game, ScreenId.GameDetail, TransitionType.Fade, SlideDirection.Right);

        Add(ScreenId.GameDetail, ScreenId.CricketGame, TransitionType.Fade, SlideDirection.Left);
        Add(ScreenId.CricketGame, ScreenId.GameDetail, TransitionType.Fade, SlideDirection.Right);

        Add(ScreenId.GameDetail, ScreenId.ATCGame, TransitionType.Fade, SlideDirection.Left);
        Add(ScreenId.ATCGame, ScreenId.GameDetail, TransitionType.Fade, SlideDirection.Right);


        // ==================================================
        // QUICK MENU SCREENS (global erreichbar)
        // ==================================================

        // ---------- OPTIONEN ----------

        AddBidirectional(ScreenId.Zoggen, ScreenId.Optionen, TransitionType.Slide);
        AddBidirectional(ScreenId.History, ScreenId.Optionen, TransitionType.Slide);
        AddBidirectional(ScreenId.Statistic, ScreenId.Optionen, TransitionType.Slide);
        AddBidirectional(ScreenId.Players, ScreenId.Optionen, TransitionType.Slide);

        AddBidirectional(ScreenId.X01Game, ScreenId.Optionen, TransitionType.Slide);
        AddBidirectional(ScreenId.CricketGame, ScreenId.Optionen, TransitionType.Slide);
        AddBidirectional(ScreenId.ATCGame, ScreenId.Optionen, TransitionType.Slide);

        AddBidirectional(ScreenId.Summary, ScreenId.Optionen, TransitionType.Slide);
        AddBidirectional(ScreenId.GameDetail, ScreenId.Optionen, TransitionType.Slide);


        // ---------- ABOUT ----------

        AddBidirectional(ScreenId.Zoggen, ScreenId.About, TransitionType.Slide);
        AddBidirectional(ScreenId.History, ScreenId.About, TransitionType.Slide);
        AddBidirectional(ScreenId.Statistic, ScreenId.About, TransitionType.Slide);
        AddBidirectional(ScreenId.Players, ScreenId.About, TransitionType.Slide);

        AddBidirectional(ScreenId.X01Game, ScreenId.About, TransitionType.Slide);
        AddBidirectional(ScreenId.CricketGame, ScreenId.About, TransitionType.Slide);
        AddBidirectional(ScreenId.ATCGame, ScreenId.About, TransitionType.Slide);

        AddBidirectional(ScreenId.Summary, ScreenId.About, TransitionType.Slide);
        AddBidirectional(ScreenId.GameDetail, ScreenId.About, TransitionType.Slide);


        // ---------- CustomClipHandler ----------

        AddBidirectional(ScreenId.Zoggen, ScreenId.CustomClipHandler, TransitionType.Slide);
        AddBidirectional(ScreenId.History, ScreenId.CustomClipHandler, TransitionType.Slide);
        AddBidirectional(ScreenId.Statistic, ScreenId.CustomClipHandler, TransitionType.Slide);
        AddBidirectional(ScreenId.Players, ScreenId.CustomClipHandler, TransitionType.Slide);

        AddBidirectional(ScreenId.X01Game, ScreenId.CustomClipHandler, TransitionType.Slide);
        AddBidirectional(ScreenId.CricketGame, ScreenId.CustomClipHandler, TransitionType.Slide);
        AddBidirectional(ScreenId.ATCGame, ScreenId.CustomClipHandler, TransitionType.Slide);

        AddBidirectional(ScreenId.Summary, ScreenId.CustomClipHandler, TransitionType.Slide);
        AddBidirectional(ScreenId.GameDetail, ScreenId.CustomClipHandler, TransitionType.Slide);


        // =========================
        // CUSTOM CLIP FLOW
        // =========================

        // CustomClipHandler <-> CustomClipBrowser
        AddBidirectional(ScreenId.CustomClipHandler, ScreenId.CustomClipBrowser, TransitionType.Slide);
    }

    private void Add(ScreenId from, ScreenId to, TransitionType type, SlideDirection dir)
    {
        transitions[(from, to)] = (type, dir);
    }

    private void AddBidirectional(ScreenId a, ScreenId b, TransitionType type)
    {
        // Links/Rechts wird automatisch gespiegelt
        Add(a, b, type, SlideDirection.Left);
        Add(b, a, type, SlideDirection.Right);
    }


    // =========================================================
    // TRANSITION RUNTIME
    // =========================================================

    private IEnumerator Transition(
        UIScreen from,
        UIScreen to,
        ScreenId targetId,
        TransitionType type,
        SlideDirection dir)
    {
        isTransitioning = true;

        Prepare(to, type);

        // Lifecycle Hooks (falls Screen Logik hat)
        if (to.TryGetComponent<IUIScreen>(out var toScreen))
            toScreen.OnShow();

        if (from.TryGetComponent<IUIScreen>(out var fromScreen))
            fromScreen.OnHide();

        // Animation abhängig vom Transition-Type
        if (type == TransitionType.Fade)
            yield return Fade(from, to);
        else
            yield return Slide(from, to, dir);

        Finish(from);

        current = to;
        currentId = targetId;

        if (menuSwitcher != null)
            menuSwitcher.SetActiveByScreen(currentId);

        yield return null;

        isTransitioning = false;
    }


    // =========================================================
    // ANIMATIONEN
    // =========================================================

    private IEnumerator Fade(UIScreen from, UIScreen to)
    {
        float duration = 0.25f;
        float t = 0;

        while (t < duration)
        {
            float p = t / duration;

            from.canvasGroup.alpha = 1 - p;
            to.canvasGroup.alpha = p;

            t += Time.deltaTime;
            yield return null;
        }

        from.canvasGroup.alpha = 0;
        to.canvasGroup.alpha = 1;
    }

    private IEnumerator Slide(UIScreen from, UIScreen to, SlideDirection dir)
    {
        float duration = 0.3f;
        float t = 0;

        float width = ((RectTransform)transform).rect.width;

        Vector2 fromStart = Vector2.zero;
        Vector2 fromEnd;
        Vector2 toStart;
        Vector2 toEnd = Vector2.zero;

        // Richtung bestimmt Startpositionen
        if (dir == SlideDirection.Left)
        {
            fromEnd = new Vector2(-width, 0);
            toStart = new Vector2(width, 0);
        }
        else
        {
            fromEnd = new Vector2(width, 0);
            toStart = new Vector2(-width, 0);
        }

        to.rect.anchoredPosition = toStart;

        while (t < duration)
        {
            float p = t / duration;

            from.rect.anchoredPosition = Vector2.Lerp(fromStart, fromEnd, p);
            to.rect.anchoredPosition = Vector2.Lerp(toStart, toEnd, p);

            t += Time.deltaTime;
            yield return null;
        }

        from.rect.anchoredPosition = fromEnd;
        to.rect.anchoredPosition = toEnd;
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private void Prepare(UIScreen s, TransitionType type)
    {
        if (!s.gameObject.activeSelf)
            s.gameObject.SetActive(true);

        if (type == TransitionType.Fade)
            s.canvasGroup.alpha = 0;
        else
            s.canvasGroup.alpha = 1;

        s.canvasGroup.interactable = true;
        s.canvasGroup.blocksRaycasts = true;
    }

    private void Finish(UIScreen s)
    {
        s.canvasGroup.alpha = 0;
        s.canvasGroup.interactable = false;
        s.canvasGroup.blocksRaycasts = false;
        s.rect.anchoredPosition = Vector2.zero;

        if (s.deactivateWhenHidden)
            s.gameObject.SetActive(false);
    }

    private void SetInitial(ScreenId id)
    {
        currentId = id;
        previousId = null;

        current = screenMap[id];

        foreach (var s in screens)
        {
            bool activeScreen = s.id == id;

            s.canvasGroup.alpha = activeScreen ? 1 : 0;
            s.canvasGroup.interactable = activeScreen;
            s.canvasGroup.blocksRaycasts = activeScreen;

            s.rect.anchoredPosition = Vector2.zero;

            if (!activeScreen && s.deactivateWhenHidden)
                s.gameObject.SetActive(false);
        }
    }


    // =========================================================
    // POPUPS
    // =========================================================

    public void ShowPopup(UIScreen popup, float duration = 0.1f)
    {
        if (isTransitioning || isPopupActive) return;

        activePopup = popup;
        isPopupActive = true;

        popup.gameObject.SetActive(true);

        popup.canvasGroup.alpha = 0;
        popup.canvasGroup.interactable = true;
        popup.canvasGroup.blocksRaycasts = true;

        StartCoroutine(FadeInPopup(activePopup, duration));
    }

    private IEnumerator FadeInPopup(UIScreen popup, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            popup.canvasGroup.alpha = Mathf.Lerp(0, 1, t / duration);
            t += Time.deltaTime;
            yield return null;
        }

        popup.canvasGroup.alpha = 1;
    }

    public void HidePopup(float duration = 0.1f)
    {
        if (!isPopupActive || activePopup == null) return;

        StartCoroutine(FadeOutPopup(activePopup, duration));
    }

    private IEnumerator FadeOutPopup(UIScreen popup, float duration)
    {
        float t = 0f;
        float startAlpha = popup.canvasGroup.alpha;

        while (t < duration)
        {
            popup.canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, t / duration);
            t += Time.deltaTime;
            yield return null;
        }

        popup.canvasGroup.alpha = 0;

        popup.canvasGroup.interactable = false;
        popup.canvasGroup.blocksRaycasts = false;

        popup.gameObject.SetActive(false);

        activePopup = null;
        isPopupActive = false;
    }


    // =========================================================
    // GETTERS / UTIL
    // =========================================================

    public ScreenId GetCurrentScreen() => currentId;
    public ScreenId? GetPreviousScreen() => previousId;

    private void InitQuickMenu()
    {
        if (quickMenuPanel == null || quickMenuCanvas == null)
        {
            Debug.LogWarning("Quick Menu Komponenten nicht zugewiesen. Quick Menu wird deaktiviert.");
            return;
        }

        quickMenuPanel.localScale = Vector3.zero;

        quickMenuCanvas.alpha = 0;
        quickMenuCanvas.interactable = false;
        quickMenuCanvas.blocksRaycasts = false;
    }
}


// =========================================================
// ENUMS
// =========================================================

public enum ScreenId
{
    Zoggen,
    Players,
    History,
    Statistic,
    Optionen,
    X01Game,
    Summary,
    GameDetail,
    ATCGame,
    CricketGame,
    Popup,
    About,
    CustomClipHandler,
    CustomClipBrowser
}

public enum TransitionType
{
    Fade,
    Slide
}

public enum SlideDirection
{
    Left,
    Right
}