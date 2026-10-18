using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Ladebildschirm beim App-Start (ein GO, keine Szene).
/// Ablauf: 1–2 Frames Loader painten lassen, dann auf echte Ready-Flags warten
/// (Statistik-Live-Build inkl. Reserve-Rows, History-Initial-Tab), danach
/// smooth über die CanvasGroup ausfaden und deaktivieren. Hängt nie fest
/// (Timeout-Sicherung). Input bleibt bis zum Fade-Ende geblockt.
/// Als Warte-Indikator pulsiert das Logo (Sinus-Skalierung).
/// Szene-Setup: Bootloader-GO als LETZTES Kind unter den Darts-Canvas (damit
/// es über der App rendert), darauf CanvasGroup + Logo, pulseTarget mit Logo verdrahten.
/// </summary>
public class BootLoader : MonoBehaviour
{
    [Header("Loader UI")]
    [Tooltip("CanvasGroup des Loaders (Alpha-Fade + Input-Block).")]
    public CanvasGroup loaderCanvasGroup;
    [Tooltip("Pulsierendes Element (üblicherweise das Logo). Skaliert weich auf/ab.")]
    public RectTransform pulseTarget;

    [Header("Puls")]
    [Tooltip("Max. Größenänderung (0 = aus, 0.08 = ±8 %). Obergrenze mit viel Spielraum.")]
    [Range(0f, 0.5f)]
    public float pulseAmplitude = 0.08f;
    [Tooltip("Puls-Geschwindigkeit in Hertz (Vollzyklen pro Sekunde). Obergrenze mit viel Spielraum.")]
    [Range(0.1f, 5f)]
    public float pulseFrequency = 1.5f;

    [Header("Ready-Quellen (optional, sonst Auto-Find)")]
    public StatisticsHandler statisticsHandler;
    public HistoryHandler historyHandler;
    public AudioLibrary audioLibrary;

    [Header("Ablauf")]
    [Tooltip("Fade-Dauer beim Ausblenden.")]
    public float fadeDuration = 0.3f;
    [Tooltip("Sicherung: Loader blendet spätestens hier aus, hängt nie fest.")]
    public float timeoutSeconds = 12f;

    private Vector3 baseScale = Vector3.one;
    private bool hasPulseTarget;

    private void Start()
    {
        if (statisticsHandler == null)
            statisticsHandler = FindFirstObjectByType<StatisticsHandler>();
        if (historyHandler == null)
            historyHandler = FindFirstObjectByType<HistoryHandler>();
        if (audioLibrary == null)
            audioLibrary = FindAnyObjectByType<AudioLibrary>();
        if (loaderCanvasGroup == null)
            loaderCanvasGroup = GetComponent<CanvasGroup>();
        if (pulseTarget == null)
        {
            Transform logoChild = transform.Find("Logo");
            if (logoChild is RectTransform logoRect)
                pulseTarget = logoRect;
        }

        hasPulseTarget = pulseTarget != null;
        if (hasPulseTarget)
            baseScale = pulseTarget.localScale;
        else
            Debug.LogWarning("[BootLoader] Kein pulseTarget verdrahtet — Logo pulsiert nicht. Bitte Logo-RectTransform zuweisen.", this);

        // Harte Obergrenzen (Inspector-Range ist nur UI-seitig, daher hier zusätzlich clampen).
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
        // Loader erst painten lassen (Logo sichtbar), bevor Flags
        // gepollt werden.
        yield return null;
        yield return null;

        float bootStart = Time.realtimeSinceStartup;
        float deadline = bootStart + Mathf.Max(1f, timeoutSeconds);

        while (true)
        {
            bool statsReady = statisticsHandler == null || statisticsHandler.IsInitialBuildDone;
            bool historyReady = historyHandler == null || historyHandler.IsInitialBuildDone;
            bool audioReady = audioLibrary == null || audioLibrary.IsReady;

            if (statsReady && historyReady && audioReady)
                break;
            if (Time.realtimeSinceStartup >= deadline)
                break;

            yield return null;
        }

        // Puffer: SetupPlayerList baut auf Frame 2 um — zwei Extra-Frames,
        // damit Rest-Bursts nicht in den Fade fallen.
        yield return null;
        yield return null;

        // Aktiver Push an CustomizationHandler: Clip-Counts neu einlesen,
        // sobald die AudioLibrary fertig geladen ist (nicht per Event,
        // da Rows ggf. noch nicht subscribed waren).
        var customizationHandler = Resources.FindObjectsOfTypeAll<CustomizationHandler>()
            .FirstOrDefault();
        customizationHandler?.RefreshCustomizationCounts();

        ResetPulseScale();
        yield return FadeOut();
        gameObject.SetActive(false);
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
