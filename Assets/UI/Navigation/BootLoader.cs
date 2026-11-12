using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Loading screen at app launch (one GO, no scene).
/// Process: Let 1–2 frames loader paint, then wait for real ready flags
/// (statistics live build incl. reserve rows, history initial tab), then
/// smoothly spread over the CanvasGroup and deactivate.
/// (Timeout backup) Input remains blocked until the fade end.
/// As a waiting indicator, the logo pulsates (sinus scaling).
/// Scene Setup: Bootloader GO as LAST Child Among the Darts Canvas (with it)
/// renders it over the app), on it CanvasGroup + logo, pulseTarget wire with logo.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class BootLoader : MonoBehaviour
{
    [Header("Loader UI")]
    [Tooltip("CanvasGroup des Loaders (Alpha-Fade + Input-Block).")]
    public CanvasGroup loaderCanvasGroup;
    [Tooltip("Pulsierendes Element (üblicherweise das Logo). Skaliert weich auf/ab.")]
    public RectTransform pulseTarget;

    [Header("Datenbank-Fehler")]
    [Tooltip("Optionales Panel für einen blockierenden Datenbankfehler.")]
    public GameObject databaseErrorPanel;
    [Tooltip("Optionaler Text für einen blockierenden Datenbankfehler.")]
    public TMPro.TMP_Text databaseErrorText;

    [Header("Puls")]
    [Tooltip("Max. Größenänderung (0 = aus, 0.08 = ±8 %). Obergrenze mit viel Spielraum.")]
    [Range(0f, 0.5f)]
    public float pulseAmplitude = 0.08f;
    [Tooltip("Puls-Geschwindigkeit in Hertz (Vollzyklen pro Sekunde). Obergrenze mit viel Spielraum.")]
    [Range(0.1f, 5f)]
    public float pulseFrequency = 1.5f;

    [Header("Ready-Quellen (optional, sonst Auto-Find)")]
    public StatisticsScreen statisticsHandler;
    public HistoryScreen historyHandler;
    public AudioLibrary audioLibrary;

    [Header("Ablauf")]
    [Tooltip("Fade-Dauer beim Ausblenden.")]
    public float fadeDuration = 0.3f;
    [Tooltip("Sicherung: Loader blendet spätestens hier aus, hängt nie fest.")]
    public float timeoutSeconds = 12f;

    private Vector3 baseScale = Vector3.one;
    private bool hasPulseTarget;

    private void Awake()
    {
        // Must run before AppHandler.Awake and AppSettingsManager.Awake.
        var bootstrapResult = DatabaseBootstrapper.Prepare();
        if (bootstrapResult == null || !bootstrapResult.Success)
            ShowDatabaseError(bootstrapResult != null ? bootstrapResult.Error : "Unbekannter Datenbankfehler.");
    }

    private void Start()
    {
        if (DatabaseBootstrapper.HasFailed)
        {
            ShowDatabaseError(DatabaseBootstrapper.Result != null
                ? DatabaseBootstrapper.Result.Error
                : "Unbekannter Datenbankfehler.");
            return;
        }

        if (statisticsHandler == null)
            statisticsHandler = FindFirstObjectByType<StatisticsScreen>();
        if (historyHandler == null)
            historyHandler = FindFirstObjectByType<HistoryScreen>();
        if (audioLibrary == null)
            audioLibrary = FindAnyObjectByType<AudioLibrary>();
        if (loaderCanvasGroup == null)
            loaderCanvasGroup = GetComponent<CanvasGroup>();
        if (pulseTarget == null)
        {
            Debug.LogWarning("[BootLoader] pulseTarget nicht verdrahtet; Fallback-Suche nach 'Logo'.", this);
            Transform logoChild = transform.Find("Logo");
            if (logoChild is RectTransform logoRect)
                pulseTarget = logoRect;
        }

        hasPulseTarget = pulseTarget != null;
        if (hasPulseTarget)
            baseScale = pulseTarget.localScale;
        else
            Debug.LogWarning("[BootLoader] Kein pulseTarget verdrahtet — Logo pulsiert nicht. Bitte Logo-RectTransform zuweisen.", this);

        // Hard upper limits (Inspector range is only UI-side, therefore here additionally clamp).
        pulseAmplitude = Mathf.Clamp(pulseAmplitude, 0f, 0.5f);
        pulseFrequency = Mathf.Clamp(pulseFrequency, 0.1f, 5f);

        if (loaderCanvasGroup != null)
        {
            loaderCanvasGroup.alpha = 1f;
            loaderCanvasGroup.blocksRaycasts = true;
            loaderCanvasGroup.interactable = false;
        }

        StartCoroutine(BootSequence());
    }

    private void Update()
    {
        if (!hasPulseTarget || pulseTarget == null)
            return;

        float wave = Mathf.Sin(Time.unscaledTime * pulseFrequency * Mathf.PI * 2f);
        float s = 1f + pulseAmplitude * wave;
        pulseTarget.localScale = baseScale * s;
    }

    private IEnumerator BootSequence()
    {
        // Let the loader paint first (logo visible) before flags
        // are polled.
        yield return null;
        yield return null;

        if (!DatabaseBootstrapper.IsReady || SaveSystem.HasLoadFailed)
        {
            ShowDatabaseError(GetDatabaseError());
            yield break;
        }

        float bootStart = Time.realtimeSinceStartup;
        float deadline = bootStart + Mathf.Max(1f, timeoutSeconds);

        while (true)
        {
            if (!DatabaseBootstrapper.IsReady || SaveSystem.HasLoadFailed)
            {
                ShowDatabaseError(GetDatabaseError());
                yield break;
            }

            bool statsReady = statisticsHandler == null || statisticsHandler.IsInitialBuildDone;
            bool historyReady = historyHandler == null || historyHandler.IsInitialBuildDone;
            bool audioReady = audioLibrary == null || audioLibrary.IsReady;

            if (statsReady && historyReady && audioReady)
                break;
            if (Time.realtimeSinceStartup >= deadline)
                break;

            yield return null;
        }

        // Buffer: SetupPlayerList rebuilds to Frame 2 — two extra frames,
        // so that rest bursts do not fall into the fade.
        yield return null;
        yield return null;

        // Active push to CustomizationScreen: re-read clip counts,
        // as soon as the AudioLibrary is ready loaded (not by event),
        // Rows may not have been subscribed yet.
        var customizationHandler = Resources.FindObjectsOfTypeAll<CustomizationScreen>()
            .FirstOrDefault();
        customizationHandler?.RefreshCustomizationCounts();

        ResetPulseScale();
        yield return FadeOut();
        gameObject.SetActive(false);
    }

    private void ShowDatabaseError(string message)
    {
        Debug.LogError("[BootLoader] App-Start blockiert: " + message);

        if (loaderCanvasGroup == null)
            loaderCanvasGroup = GetComponent<CanvasGroup>();

        if (loaderCanvasGroup != null)
        {
            loaderCanvasGroup.alpha = 1f;
            loaderCanvasGroup.blocksRaycasts = true;
            loaderCanvasGroup.interactable = false;
        }

        if (databaseErrorPanel != null)
            databaseErrorPanel.SetActive(true);

        if (databaseErrorText != null)
            databaseErrorText.text = "Datenbank konnte nicht vorbereitet werden.\n" + message;
    }

    private string GetDatabaseError()
    {
        if (SaveSystem.HasLoadFailed)
            return "Die Datenbank konnte nicht geladen werden.";

        var result = DatabaseBootstrapper.Result;
        return result != null && !string.IsNullOrWhiteSpace(result.Error)
            ? result.Error
            : "Unbekannter Datenbankfehler.";
    }

    private void ResetPulseScale()
    {
        if (hasPulseTarget && pulseTarget != null)
            pulseTarget.localScale = baseScale;
    }

    private IEnumerator FadeOut()
    {
        if (loaderCanvasGroup == null)
            yield break;

        loaderCanvasGroup.blocksRaycasts = true;
        loaderCanvasGroup.interactable = false;

        float duration = Mathf.Max(0.001f, fadeDuration);
        float t = 0f;

        while (t < duration)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            loaderCanvasGroup.alpha = 1f - p;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        loaderCanvasGroup.alpha = 0f;
        loaderCanvasGroup.blocksRaycasts = false;
    }
}
