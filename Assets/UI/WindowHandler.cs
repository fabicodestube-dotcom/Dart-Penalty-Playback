using System;
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
    [SerializeField] private LeavingPopup leavingPopup;

    [Header("Quick Menu")]
    [SerializeField] private RectTransform quickMenuPanel;
    [SerializeField] private CanvasGroup quickMenuCanvas;

    [Header("Back")]
    [SerializeField] private float doubleBackExitWindow = 2f;

    private const float CrossFadeDuration = 0.22f;
    private const float SharedAxisDuration = 0.24f;
    private const float SharedAxisOffset = 64f;
    private const float QuickMenuOpenDuration = 0.18f;
    private const float QuickMenuCloseDuration = 0.15f;
    private const float PopupDuration = 0.14f;

    private enum ScreenKind
    {
        RootTab,
        Detail,
        Gameplay,
        Summary,
        QuickMenu,
        Overlay
    }

    private enum NavIntent
    {
        Push,
        Replace,
        SwitchTab,
        ResetTo,
        Back
    }

    private enum MotionKind
    {
        CrossFade,
        SharedAxisX,
        SharedAxisY
    }

    private readonly struct MotionSpec
    {
        public readonly MotionKind Kind;
        public readonly bool Forward;

        public MotionSpec(MotionKind kind, bool forward)
        {
            Kind = kind;
            Forward = forward;
        }
    }

    private readonly Dictionary<ScreenId, UIScreen> screenMap = new();
    private readonly List<ScreenId> backStack = new();

    private UIScreen current;
    private ScreenId currentId;
    private ScreenId? previousId;

    private bool isTransitioning;
    private bool isPopupActive;
    private bool isPopupClosing;
    private bool isQuickMenuOpen;

    private Coroutine quickMenuRoutine;
    private Coroutine popupRoutine;

    private float lastBackPressedAt = -100f;

    private void Awake()
    {
        BuildScreenMap();
        InitQuickMenu();

        if (!screenMap.ContainsKey(ScreenId.Zoggen))
        {
            Debug.LogError("Startscreen Zoggen ist nicht in der screens-Liste registriert.");
            return;
        }

        SetInitial(ScreenId.Zoggen);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            HandleBack();
    }

    private void BuildScreenMap()
    {
        screenMap.Clear();

        foreach (var screen in screens)
        {
            if (screen == null)
                continue;

            if (screenMap.ContainsKey(screen.id))
            {
                Debug.LogError($"ScreenId doppelt registriert: {screen.id}");
                continue;
            }

            screenMap[screen.id] = screen;
        }

        foreach (ScreenId id in Enum.GetValues(typeof(ScreenId)))
        {
            if (id == ScreenId.Popup || id == ScreenId.CustomClipHandler)
                continue;

            if (!screenMap.ContainsKey(id))
                Debug.LogError($"ScreenId nicht registriert: {id}");
        }
    }

    public void GoTo(ScreenId target)
    {
        if (KindOf(target) == ScreenKind.RootTab)
        {
            SwitchTab(target);
            return;
        }

        Navigate(target, NavIntent.Push);
    }

    public void Replace(ScreenId target)
    {
        Navigate(target, NavIntent.Replace);
    }

    public void ResetTo(ScreenId target)
    {
        Navigate(target, NavIntent.ResetTo);
    }

    public void SwitchTab(ScreenId target)
    {
        if (KindOf(target) != ScreenKind.RootTab)
        {
            Navigate(target, NavIntent.Push);
            return;
        }

        if (currentId == target)
            return;

        if (KindOf(currentId) == ScreenKind.Gameplay || currentId == ScreenId.Summary)
            return;

        Navigate(target, NavIntent.SwitchTab);
    }

    public void StartGameplay(ScreenId target)
    {
        if (KindOf(target) != ScreenKind.Gameplay || isTransitioning || currentId == target)
            return;

        if (!screenMap.TryGetValue(target, out var targetScreen))
        {
            Debug.LogError($"Screen nicht registriert (screens-Liste/UIScreen.id prüfen): {target}");
            return;
        }

        backStack.Clear();
        backStack.Add(ScreenId.Zoggen);

        previousId = currentId;
        lastBackPressedAt = -100f;

        StartCoroutine(Transition(current, targetScreen, target, ResolveMotion(currentId, target, NavIntent.Push)));
    }

    public void GoBack()
    {
        HandleBack();
    }

    public bool HandleBack()
    {
        if (isTransitioning)
            return true;

        if (isPopupActive || isPopupClosing)
        {
            if (KindOf(currentId) == ScreenKind.Gameplay)
                InvokeGameplayPopupCancel();
            else
                HidePopup();

            return true;
        }

        if (isQuickMenuOpen)
        {
            SetQuickMenuOpen(false);
            return true;
        }

        if (TryCloseScreenOverlay())
            return true;

        if (KindOf(currentId) == ScreenKind.Gameplay)
        {
            InvokeGameplayBack();
            return true;
        }

        if (currentId == ScreenId.Summary)
        {
            InvokeSummaryBack();
            return true;
        }

        if (backStack.Count > 0)
        {
            NavigateBack();
            return true;
        }

        if (currentId != ScreenId.Zoggen)
        {
            SwitchTab(ScreenId.Zoggen);
            return true;
        }

        HandleRootBack();
        return true;
    }

    private void Navigate(ScreenId target, NavIntent intent)
    {
        if (isTransitioning || currentId == target)
            return;

        if (!screenMap.TryGetValue(target, out var targetScreen))
        {
            Debug.LogError($"Screen nicht registriert (screens-Liste/UIScreen.id prüfen): {target}");
            return;
        }

        if (intent == NavIntent.SwitchTab && KindOf(currentId) == ScreenKind.Gameplay)
            return;

        switch (intent)
        {
            case NavIntent.Push:
                backStack.Add(currentId);
                break;

            case NavIntent.SwitchTab:
            case NavIntent.ResetTo:
                backStack.Clear();
                break;

            case NavIntent.Replace:
            case NavIntent.Back:
            default:
                break;
        }

        previousId = currentId;
        lastBackPressedAt = -100f;

        StartCoroutine(Transition(current, targetScreen, target, ResolveMotion(currentId, target, intent)));
    }

    private void NavigateBack()
    {
        int lastIndex = backStack.Count - 1;
        ScreenId target = backStack[lastIndex];
        backStack.RemoveAt(lastIndex);

        if (!screenMap.TryGetValue(target, out var targetScreen))
        {
            Debug.LogError($"Screen nicht registriert (screens-Liste/UIScreen.id prüfen): {target}");
            return;
        }

        previousId = currentId;
        lastBackPressedAt = -100f;

        StartCoroutine(Transition(current, targetScreen, target, ResolveMotion(currentId, target, NavIntent.Back)));
    }

    private void HandleRootBack()
    {
        if (Time.unscaledTime - lastBackPressedAt <= doubleBackExitWindow)
        {
            lastBackPressedAt = -100f;
            ShowLeavingPopup();
            return;
        }

        lastBackPressedAt = Time.unscaledTime;
    }

    private void ShowLeavingPopup()
    {
        if (leavingPopup == null)
            leavingPopup = GetComponentInChildren<LeavingPopup>(true);

        if (leavingPopup == null)
        {
            Debug.LogError("LeavingPopup nicht gefunden. Anwendung wird beendet.");
            Application.Quit();
            return;
        }

        leavingPopup.Show(this);
    }

    private bool TryCloseScreenOverlay()
    {
        if (current == null)
            return false;

        InputHandler inputHandler = current.GetComponentInChildren<InputHandler>(true);
        return inputHandler != null && inputHandler.TryHandleBack();
    }

    private void InvokeGameplayBack()
    {
        EnsureEngineReferences();

        switch (currentId)
        {
            case ScreenId.X01Game:
                if (x01Engine == null)
                {
                    Debug.LogError("X01GameEngine nicht gefunden.");
                    return;
                }

                x01Engine.OnClickReturnFromX01();
                break;

            case ScreenId.CricketGame:
                if (cricketEngine == null)
                {
                    Debug.LogError("CricketGameEngine nicht gefunden.");
                    return;
                }

                cricketEngine.OnClickReturnFromCricket();
                break;

            case ScreenId.ATCGame:
                if (atcEngine == null)
                {
                    Debug.LogError("ATCGameEngine nicht gefunden.");
                    return;
                }

                atcEngine.OnClickReturnFromATC();
                break;
        }
    }

    private void InvokeGameplayPopupCancel()
    {
        EnsureEngineReferences();

        switch (currentId)
        {
            case ScreenId.X01Game when x01Engine != null:
                x01Engine.OnClickAbortCancle();
                return;

            case ScreenId.CricketGame when cricketEngine != null:
                cricketEngine.OnClickAbortCancle();
                return;

            case ScreenId.ATCGame when atcEngine != null:
                atcEngine.OnClickAbortCancel();
                return;
        }

        HidePopup();
    }

    private void InvokeSummaryBack()
    {
        if (summaryScreen == null)
            summaryScreen = GetComponentInChildren<Summary>(true);

        if (summaryScreen == null)
        {
            Debug.LogError("Summary Screen nicht gefunden.");
            return;
        }

        summaryScreen.OnClickBackButton();
    }

    private void EnsureEngineReferences()
    {
        if (x01Engine == null)
            x01Engine = GetComponentInChildren<X01GameEngine>(true);

        if (cricketEngine == null)
            cricketEngine = GetComponentInChildren<CricketGameEngine>(true);

        if (atcEngine == null)
            atcEngine = GetComponentInChildren<ATCGameEngine>(true);
    }

    public void OpenOptions()
    {
        OpenFromQuickMenu(ScreenId.Optionen);
    }

    public void OpenAbout()
    {
        OpenFromQuickMenu(ScreenId.About);
    }

    private void OpenFromQuickMenu(ScreenId target)
    {
        if (isTransitioning)
            return;

        SetQuickMenuOpen(false);
        GoTo(target);
    }

    public void ToggleQuickMenu()
    {
        if (isTransitioning)
            return;

        SetQuickMenuOpen(!isQuickMenuOpen);
    }

    private void SetQuickMenuOpen(bool open)
    {
        if (quickMenuPanel == null || quickMenuCanvas == null)
            return;

        if (isQuickMenuOpen == open && quickMenuRoutine == null)
            return;

        if (quickMenuRoutine != null)
            StopCoroutine(quickMenuRoutine);

        isQuickMenuOpen = open;
        quickMenuRoutine = StartCoroutine(AnimateQuickMenu(open));
    }

    private IEnumerator AnimateQuickMenu(bool open)
    {
        float duration = open ? QuickMenuOpenDuration : QuickMenuCloseDuration;
        float startAlpha = quickMenuCanvas.alpha;
        Vector3 startScale = quickMenuPanel.localScale;
        float targetAlpha = open ? 1f : 0f;
        Vector3 targetScale = open ? Vector3.one : new Vector3(0.9f, 0.9f, 1f);

        if (open)
        {
            quickMenuCanvas.interactable = true;
            quickMenuCanvas.blocksRaycasts = true;
        }

        float t = 0f;

        while (t < duration)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);

            quickMenuCanvas.alpha = Mathf.Lerp(startAlpha, targetAlpha, p);
            quickMenuPanel.localScale = Vector3.Lerp(startScale, targetScale, p);

            t += Time.deltaTime;
            yield return null;
        }

        quickMenuCanvas.alpha = targetAlpha;
        quickMenuPanel.localScale = targetScale;

        if (!open)
        {
            quickMenuCanvas.interactable = false;
            quickMenuCanvas.blocksRaycasts = false;
        }

        quickMenuRoutine = null;
    }

    private void InitQuickMenu()
    {
        if (quickMenuPanel == null || quickMenuCanvas == null)
        {
            Debug.LogWarning("Quick Menu Komponenten nicht zugewiesen. Quick Menu wird deaktiviert.");
            return;
        }

        isQuickMenuOpen = false;
        quickMenuPanel.localScale = Vector3.zero;
        quickMenuCanvas.alpha = 0f;
        quickMenuCanvas.interactable = false;
        quickMenuCanvas.blocksRaycasts = false;
    }

    private static ScreenKind KindOf(ScreenId id)
    {
        return id switch
        {
            ScreenId.Zoggen => ScreenKind.RootTab,
            ScreenId.Players => ScreenKind.RootTab,
            ScreenId.History => ScreenKind.RootTab,
            ScreenId.Statistic => ScreenKind.RootTab,
            ScreenId.Customization => ScreenKind.RootTab,
            ScreenId.Optionen => ScreenKind.QuickMenu,
            ScreenId.About => ScreenKind.QuickMenu,
            ScreenId.X01Game => ScreenKind.Gameplay,
            ScreenId.CricketGame => ScreenKind.Gameplay,
            ScreenId.ATCGame => ScreenKind.Gameplay,
            ScreenId.Summary => ScreenKind.Summary,
            ScreenId.GameDetail => ScreenKind.Detail,
            ScreenId.CustomClipBrowser => ScreenKind.Detail,
            _ => ScreenKind.Overlay
        };
    }

    private static MotionSpec ResolveMotion(ScreenId from, ScreenId to, NavIntent intent)
    {
        if (intent == NavIntent.SwitchTab)
            return new MotionSpec(MotionKind.CrossFade, true);

        if (intent == NavIntent.ResetTo)
        {
            if (from == ScreenId.Summary && KindOf(to) == ScreenKind.RootTab)
                return new MotionSpec(MotionKind.SharedAxisY, false);

            return new MotionSpec(MotionKind.CrossFade, true);
        }

        if (from == ScreenId.Summary && to == ScreenId.GameDetail)
            return new MotionSpec(MotionKind.SharedAxisX, true);

        if (from == ScreenId.GameDetail && to == ScreenId.Summary)
            return new MotionSpec(MotionKind.SharedAxisX, false);

        if (to == ScreenId.Summary)
            return new MotionSpec(MotionKind.SharedAxisY, true);

        if (from == ScreenId.Summary)
            return new MotionSpec(MotionKind.SharedAxisY, intent != NavIntent.Back);

        if (KindOf(to) == ScreenKind.Detail || KindOf(from) == ScreenKind.Detail)
            return new MotionSpec(MotionKind.SharedAxisX, intent != NavIntent.Back);

        if (KindOf(to) == ScreenKind.Gameplay)
            return new MotionSpec(MotionKind.SharedAxisX, true);

        if (KindOf(to) == ScreenKind.QuickMenu)
            return new MotionSpec(MotionKind.SharedAxisX, true);

        if (KindOf(from) == ScreenKind.QuickMenu)
            return new MotionSpec(MotionKind.SharedAxisX, intent != NavIntent.Back);

        return new MotionSpec(MotionKind.CrossFade, true);
    }

    private IEnumerator Transition(UIScreen from, UIScreen to, ScreenId targetId, MotionSpec motion)
    {
        isTransitioning = true;

        if (menuSwitcher != null && KindOf(targetId) == ScreenKind.RootTab)
            menuSwitcher.SetActiveByScreen(targetId);

        Prepare(to, from);

        if (to.TryGetComponent<IUIScreen>(out var toScreen))
            toScreen.OnShow();

        if (from != null && from.TryGetComponent<IUIScreen>(out var fromScreen))
            fromScreen.OnHide();

        switch (motion.Kind)
        {
            case MotionKind.SharedAxisX:
                yield return SharedAxis(from, to, false, motion.Forward);
                break;

            case MotionKind.SharedAxisY:
                yield return SharedAxis(from, to, true, motion.Forward);
                break;

            default:
                yield return CrossFade(from, to);
                break;
        }

        Finish(from);

        current = to;
        currentId = targetId;

        yield return null;

        isTransitioning = false;
    }

    private void Prepare(UIScreen to, UIScreen from)
    {
        if (!to.gameObject.activeSelf)
            to.gameObject.SetActive(true);

        to.canvasGroup.alpha = 0f;
        to.canvasGroup.interactable = true;
        to.canvasGroup.blocksRaycasts = true;
        to.rect.anchoredPosition = Vector2.zero;
        to.rect.localScale = Vector3.one;

        if (from == null)
            return;

        from.canvasGroup.alpha = 1f;
        from.canvasGroup.interactable = false;
        from.canvasGroup.blocksRaycasts = false;
        from.rect.anchoredPosition = Vector2.zero;
        from.rect.localScale = Vector3.one;
    }

    private IEnumerator CrossFade(UIScreen from, UIScreen to)
    {
        float t = 0f;

        while (t < CrossFadeDuration)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / CrossFadeDuration);

            from.canvasGroup.alpha = 1f - p;
            to.canvasGroup.alpha = p;

            t += Time.deltaTime;
            yield return null;
        }

        Finish(from);
        to.canvasGroup.alpha = 1f;
    }

    private IEnumerator SharedAxis(UIScreen from, UIScreen to, bool vertical, bool forward)
    {
        float size = vertical
            ? to.rect.rect.height
            : to.rect.rect.width;

        float offset = Mathf.Min(SharedAxisOffset, size * 0.08f);

        Vector2 fromEnd;
        Vector2 toStart;

        if (vertical)
        {
            fromEnd = forward ? new Vector2(0f, offset) : new Vector2(0f, -offset);
            toStart = forward ? new Vector2(0f, -offset) : new Vector2(0f, offset);
        }
        else
        {
            fromEnd = forward ? new Vector2(-offset, 0f) : new Vector2(offset, 0f);
            toStart = forward ? new Vector2(offset, 0f) : new Vector2(-offset, 0f);
        }

        from.rect.anchoredPosition = Vector2.zero;
        to.rect.anchoredPosition = toStart;

        float t = 0f;
        bool fromFinished = false;

        while (t < SharedAxisDuration)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / SharedAxisDuration);

            if (!fromFinished)
            {
                from.canvasGroup.alpha = 1f - p;
                from.rect.anchoredPosition = Vector2.Lerp(Vector2.zero, fromEnd, p);

                if (p >= 0.92f)
                {
                    Finish(from);
                    fromFinished = true;
                }
            }

            to.canvasGroup.alpha = p;
            to.rect.anchoredPosition = Vector2.Lerp(toStart, Vector2.zero, p);

            t += Time.deltaTime;
            yield return null;
        }

        if (!fromFinished)
            Finish(from);

        from.canvasGroup.alpha = 0f;
        from.rect.anchoredPosition = Vector2.zero;
        to.canvasGroup.alpha = 1f;
        to.rect.anchoredPosition = Vector2.zero;
    }

    private void Finish(UIScreen screen)
    {
        if (screen == null)
            return;

        screen.canvasGroup.alpha = 0f;
        screen.canvasGroup.interactable = false;
        screen.canvasGroup.blocksRaycasts = false;
        screen.rect.anchoredPosition = Vector2.zero;
        screen.rect.localScale = Vector3.one;

        if (screen.deactivateWhenHidden)
            screen.gameObject.SetActive(false);
    }

    private void SetInitial(ScreenId id)
    {
        currentId = id;
        previousId = null;
        backStack.Clear();
        lastBackPressedAt = -100f;

        current = screenMap[id];

        foreach (var screen in screens)
        {
            if (screen == null)
                continue;

            bool activeScreen = screen.id == id;

            screen.canvasGroup.alpha = activeScreen ? 1f : 0f;
            screen.canvasGroup.interactable = activeScreen;
            screen.canvasGroup.blocksRaycasts = activeScreen;
            screen.rect.anchoredPosition = Vector2.zero;
            screen.rect.localScale = Vector3.one;

            if (!activeScreen && screen.deactivateWhenHidden)
                screen.gameObject.SetActive(false);
        }
    }

    public void ShowPopup(UIScreen popup, float duration = PopupDuration)
    {
        if (isTransitioning || isPopupActive || isPopupClosing || popup == null)
            return;

        isPopupActive = true;
        activePopup = popup;

        popup.gameObject.SetActive(true);

        if (popup.TryGetComponent<IUIScreen>(out var popupScreen))
            popupScreen.OnShow();

        popup.canvasGroup.alpha = 0f;
        popup.canvasGroup.interactable = true;
        popup.canvasGroup.blocksRaycasts = true;
        popup.rect.localScale = new Vector3(0.94f, 0.94f, 1f);

        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        popupRoutine = StartCoroutine(FadeInPopup(popup, duration));
    }

    private IEnumerator FadeInPopup(UIScreen popup, float duration)
    {
        if (duration <= 0f)
        {
            popup.canvasGroup.alpha = 1f;
            popup.rect.localScale = Vector3.one;
            popupRoutine = null;
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            popup.canvasGroup.alpha = p;
            popup.rect.localScale = Vector3.Lerp(new Vector3(0.94f, 0.94f, 1f), Vector3.one, p);
            t += Time.deltaTime;
            yield return null;
        }

        popup.canvasGroup.alpha = 1f;
        popup.rect.localScale = Vector3.one;
        popupRoutine = null;
    }

    public void HidePopup(float duration = PopupDuration)
    {
        if (!isPopupActive || activePopup == null || isPopupClosing)
            return;

        UIScreen popup = activePopup;
        isPopupClosing = true;

        if (popup.TryGetComponent<IUIScreen>(out var popupScreen))
            popupScreen.OnHide();

        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        if (duration <= 0f)
        {
            CompletePopupHide(popup);
            return;
        }

        popupRoutine = StartCoroutine(FadeOutPopup(popup, duration));
    }

    private IEnumerator FadeOutPopup(UIScreen popup, float duration)
    {
        float startAlpha = popup.canvasGroup.alpha;
        Vector3 startScale = popup.rect.localScale;
        float t = 0f;

        while (t < duration)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            popup.canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
            popup.rect.localScale = Vector3.Lerp(startScale, new Vector3(0.94f, 0.94f, 1f), p);
            t += Time.deltaTime;
            yield return null;
        }

        CompletePopupHide(popup);
    }

    private void CompletePopupHide(UIScreen popup)
    {
        if (popup != null)
        {
            popup.canvasGroup.alpha = 0f;
            popup.canvasGroup.interactable = false;
            popup.canvasGroup.blocksRaycasts = false;
            popup.rect.localScale = Vector3.one;
            popup.gameObject.SetActive(false);
        }

        activePopup = null;
        isPopupActive = false;
        isPopupClosing = false;
        popupRoutine = null;
    }

    public ScreenId GetCurrentScreen() => currentId;
    public ScreenId? GetPreviousScreen() => previousId;
}

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
    CustomClipBrowser,
    Customization
}
