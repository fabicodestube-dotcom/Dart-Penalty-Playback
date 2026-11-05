using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LeavingPopup : UIScreen, IUIScreen
{
    [Header("References")]
    public WindowHandler windowHandler;
    public AppHandler appHandler;

    protected override void Awake()
    {
        base.Awake();
        id = ScreenId.Popup;

        if (windowHandler == null)
            windowHandler = FindAnyObjectByType<WindowHandler>();
        if (appHandler == null)
            appHandler = FindAnyObjectByType<AppHandler>();
    }

    public void Show(WindowHandler handler)
    {
        if (handler != null)
            windowHandler = handler;

        if (windowHandler == null)
        {
            Debug.LogError("LeavingPopup: Kein WindowHandler gefunden.");
            return;
        }

        windowHandler.ShowPopup(this);
    }

    public void OnShow()
    {
    }

    public void OnHide()
    {
    }

    public void OnClickCancel()
    {
        if (windowHandler != null)
            windowHandler.HidePopup();
        else
            gameObject.SetActive(false);
    }

    public void OnClickConfirm()
    {
        AppSettingsManager.Instance?.Save();

        if (appHandler == null)
            appHandler = FindAnyObjectByType<AppHandler>();

        if (appHandler != null && !appHandler.SaveBeforeExit())
            Debug.LogError("LeavingPopup: Speichern vor dem Beenden fehlgeschlagen.");

        if (windowHandler != null)
            windowHandler.HidePopup(0f);
        else
            gameObject.SetActive(false);

        QuitApplication();
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
