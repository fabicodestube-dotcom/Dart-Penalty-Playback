using UnityEngine;
using UnityEngine.UI;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class ClipRecorder : MonoBehaviour
{
    [Header("Browser")]
    public ClipBrowser clipBrowser;

    [Header("Recorder Button")]
    public GameObject idleImage;
    public CanvasGroup recordingImageGroup;

    [Header("Visualization")]
    public ClipVisualization visualization;

    public float waveformRefreshRate = 0.1f;

    private float waveformTimer;

    public float blinkSpeed = 4f;


    [Header("Recording")]
    public int maxRecordingLength = 300;
    public int sampleRate = 44100;
    public float blinkFrequency = 1.5f;

    [Tooltip("Sicherer Maximal-Peak nach Normalisierung (0.95 = -0.4 dB, verhindert Clipping). Wird auf max. 0.99 begrenzt.")]
    public float normalizeTargetPeak = 0.95f;

    [Tooltip("Mindest-Ziel-Lautheit in dBFS RMS. Aufnahmen werden mindestens auf diesen Wert angehoben.")]
    public float targetLoudnessDbfs = -14f;

    [Tooltip("Minimaler Gain (Aufnahmen werden nie leiser als das gemacht)")]
    public float minNormalizationGain = 0.25f;

    [Tooltip("Maximaler Gain (begrenzt, wie stark leise Aufnahmen verstärkt werden).")]
    public float maxNormalizationGain = 64f;

    [Header("Silence Trimming")]
    [SerializeField] private float minThreshold = 0.01f;
    [SerializeField] private float maxThreshold = 0.2f;
    public float silenceThreshold = 0.05f;
    public CanvasGroup trimSliderCanvasGroup;
    public CustomToggle trimSilenceToggle;
    [SerializeField] private Slider trimSilenceThresholdSlider;


    [Header("Idle Animation")]
    public RippleAnimation rippleAnimation;

    private AudioClip recordedClip;
    private float idlePulseTimer;
    private bool isRecording;
    private string microphoneDevice;


    // ====================================================
    // UNITY
    // ====================================================

    private void Start()
    {
        Debug.Log("ClipRecorder gestartet - erfrage Mic Permission");

        #if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
        #endif

        if (trimSilenceToggle == null)
        {
            trimSilenceToggle = GetComponentInChildren<CustomToggle>();
        }

        if (trimSilenceToggle != null)
        {
            trimSilenceToggle.Initialize(false);
        }

        if (trimSilenceThresholdSlider != null)
        {
            float sliderValue = Mathf.InverseLerp(minThreshold, maxThreshold, silenceThreshold);

            trimSilenceThresholdSlider.SetValueWithoutNotify(sliderValue);
            trimSilenceThresholdSlider.onValueChanged.AddListener(SetSilenceThreshold);
        }

        UpdateTrimUI();
        UpdateRecorderVisuals();
    }

    private void Update()
    {
        HandleRecorderBlink();
        UpdateRecordingWaveform();
    }

    private void OnEnable()
    {
        rippleAnimation.StartRipples();
    }

    private void OnDisable()
    {
        rippleAnimation.StopRipples();
    }

    // ====================================================
    // UI
    // ====================================================

    private void HandleRecorderBlink()
    {
        if (recordingImageGroup == null || !isRecording)
            return;

        float t =
            Mathf.PingPong(
                Time.time * blinkFrequency * 2f,
                1f
            );

        recordingImageGroup.alpha =
            Mathf.Lerp(0.3f, 1f, t);
    }

    // ====================================================
    // RECORDING
    // ====================================================

    public void ToggleRecording()
    {
        if (clipBrowser != null && clipBrowser.IsInCutMode)
            return;

        if (!isRecording)
            StartRecording();
        else
            StopRecording();
    }

    private void StartRecording()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.LogWarning("Mikrofonberechtigung fehlt.");
            return;
        }
        #endif

        if (clipBrowser != null)
        {
            clipBrowser.SetLocked(true);
        }

        if (Microphone.devices.Length <= 0)
        {
            Debug.LogWarning("Kein Mikrofon gefunden.");
            return;
        }

        microphoneDevice = Microphone.devices[0];

        int recordingSampleRate = sampleRate;

        Microphone.GetDeviceCaps(
            microphoneDevice,
            out int minFrequency,
            out int maxFrequency);

        if (maxFrequency > 0)
        {
            recordingSampleRate = Mathf.Clamp(
                sampleRate,
                minFrequency,
                maxFrequency);
        }

        recordedClip = null;

        recordedClip = Microphone.Start(
            microphoneDevice,
            false,
            maxRecordingLength,
            recordingSampleRate
        );

        isRecording = true;

        UpdateRecorderVisuals();
    }

    private void StopRecording()
    {
        if (!isRecording)
        {
            clipBrowser?.SetLocked(false);
            return;
        }
            

        int position = Microphone.GetPosition(microphoneDevice);

        Microphone.End(microphoneDevice);

        isRecording = false;

        UpdateRecorderVisuals();
        if (clipBrowser != null)
        {
            clipBrowser.SetLocked(false);
        }

        if (recordedClip == null)
        {
            Debug.LogWarning("Keine Aufnahme erkannt.");
            return;
        }

        int recordedChannels = recordedClip.channels;
        int recordedFrequency = recordedClip.frequency;

        if (position <= 0 || recordedChannels <= 0)
        {
            recordedClip = null;
            clipBrowser?.SetLocked(false);

            Debug.LogWarning("Keine Aufnahme erkannt.");
            return;
        }

        position = Mathf.Clamp(position, 0, recordedClip.samples);

        float[] samples =
            new float[recordedClip.samples * recordedChannels];

        if (!recordedClip.GetData(samples, 0))
        {
            recordedClip = null;
            clipBrowser?.SetLocked(false);

            Debug.LogWarning("Aufnahmedaten konnten nicht gelesen werden.");
            return;
        }

        float[] clippedSamples =
            new float[position * recordedChannels];

        System.Array.Copy(
            samples,
            clippedSamples,
            clippedSamples.Length
        );

        recordedClip =
            CreateClipFromSamples(
                clippedSamples,
                recordedChannels,
                recordedFrequency);

        if (trimSilenceToggle.IsActive())
        {
            AudioClip trimmed =
                TrimSilence(
                    clippedSamples,
                    recordedChannels,
                    recordedFrequency
                );

            if (trimmed != null)
                recordedClip = trimmed;
        }

        if (recordedClip == null)
        {
            Debug.LogWarning("Aufnahme bestand nur aus Stille.");
            clipBrowser?.SetLocked(false);
            return;
        }

        recordedClip = NormalizeRecordedClip(recordedClip);
        AddRecordedClipToBrowser();
        ResetRecorder();
    }

    public void ToggleTrimSilence()
    {
        trimSilenceToggle.Toggle();
        UpdateTrimUI();
    }

    public void SetSilenceThreshold(float sliderValue)
    {
        silenceThreshold = Mathf.Lerp(minThreshold, maxThreshold, sliderValue);
        Debug.Log("Silence Threshold gesetzt auf: " + silenceThreshold);
    }


    private AudioClip CreateClipFromSamples(
        float[] samples,
        int channels,
        int frequency)
    {
        AudioClip clip = AudioClip.Create(
            "Recording",
            samples.Length / channels,
            channels,
            frequency,
            false
        );

        clip.SetData(samples, 0);
        return clip;
    }

    private void AddRecordedClipToBrowser()
    {
        if (recordedClip == null)
            return;

        if (clipBrowser == null)
            return;

        clipBrowser.AddClip(recordedClip);
    }

    private void ResetRecorder()
    {
        recordedClip = null;
    }

    private void UpdateRecorderVisuals()
    {
        if (idleImage != null)
        {
            idleImage.SetActive(!isRecording);
        }

        if (rippleAnimation != null)
        {
            if (isRecording)
                rippleAnimation.StopRipples();
            else
                rippleAnimation.StartRipples();
        }

        if (recordingImageGroup != null)
        {
            recordingImageGroup.gameObject.SetActive(isRecording);

            if (!isRecording)
            {
                recordingImageGroup.alpha = 1f;
            }
        }
    }

    // ====================================================
    // SILENCE TRIMMING
    // ====================================================
    private AudioClip TrimSilence(
        float[] samples,
        int channels,
        int frequency)
    {
        if (samples == null || samples.Length == 0)
            return null;

        int start = -1;
        int end = -1;

        // Anfang suchen (framebasiert)
        for (int i = 0; i < samples.Length; i += channels)
        {
            bool audible = false;

            for (int c = 0; c < channels; c++)
            {
                if (Mathf.Abs(samples[i + c]) > silenceThreshold)
                {
                    audible = true;
                    break;
                }
            }

            if (audible)
            {
                start = i;
                break;
            }
        }

        // Ende suchen (framebasiert)
        for (int i = samples.Length - channels; i >= 0; i -= channels)
        {
            bool audible = false;

            for (int c = 0; c < channels; c++)
            {
                if (Mathf.Abs(samples[i + c]) > silenceThreshold)
                {
                    audible = true;
                    break;
                }
            }

            if (audible)
            {
                end = i + channels - 1;
                break;
            }
        }

        // Nur Stille
        if (start < 0 || end < 0)
            return null;

        int trimmedLength = end - start + 1;

        if (trimmedLength <= 0)
            return null;

        float[] trimmedSamples = new float[trimmedLength];

        System.Array.Copy(
            samples,
            start,
            trimmedSamples,
            0,
            trimmedLength
        );

        AudioClip trimmedClip = AudioClip.Create(
            "TrimmedRecording",
            trimmedSamples.Length / channels,
            channels,
            frequency,
            false
        );

        trimmedClip.SetData(trimmedSamples, 0);

        return trimmedClip;
    }


    // ====================================================
    // NORMALIZATION
    // ====================================================

    private AudioClip NormalizeRecordedClip(AudioClip clip)
    {
        if (clip == null)
            return null;

        float ceiling = normalizeTargetPeak <= 0f ? 0.95f : normalizeTargetPeak;
        ceiling = Mathf.Clamp(ceiling, 0.05f, 0.99f);

        int sampleCount = clip.samples * clip.channels;

        if (sampleCount <= 0)
            return clip;

        float[] samples = new float[sampleCount];

        if (!clip.GetData(samples, 0))
        {
            Debug.LogWarning("[RECORDER] Aufnahmedaten konnten nicht gelesen werden.");
            return clip;
        }

        ComputeStats(samples, out float recPeak, out float recRms);

        if (!IsFinite(recPeak) || !IsFinite(recRms) ||
            recPeak <= 1e-6f || recRms <= 1e-6f)
        {
            Debug.LogWarning(
                $"[RECORDER] Keine gültigen Samples: peak={recPeak}, rms={recRms}");
            return clip;
        }

        float targetRms = Mathf.Pow(
            10f,
            Mathf.Clamp(targetLoudnessDbfs, -60f, -1f) / 20f);

        if (!IsFinite(targetRms) || targetRms <= 0f)
            targetRms = 0.2f;

        float gain = targetRms / recRms;

        float minGain = Mathf.Max(0.01f, minNormalizationGain);
        float maxGain = Mathf.Max(minGain, maxNormalizationGain);
        gain = Mathf.Clamp(gain, minGain, maxGain);

        if (!IsFinite(gain))
            return clip;

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] *= gain;
        }

        ApplySoftLimiter(samples, ceiling);

        ComputeStats(samples, out float outPeak, out float outRms);

        Debug.Log(
            $"[RECORDER] recPeak={recPeak:F4} recRms={recRms:F4} " +
            $"targetRms={targetRms:F4} gain={gain:F2} " +
            $"outPeak={outPeak:F4} outRms={outRms:F4}");

        AudioClip normalizedClip =
            AudioClip.Create(
                clip.name + "_normalized",
                clip.samples,
                clip.channels,
                clip.frequency,
                false
            );

        normalizedClip.SetData(samples, 0);

        return normalizedClip;
    }

    private static void ApplySoftLimiter(float[] samples, float ceiling)
    {
        if (samples == null || samples.Length == 0)
            return;

        float knee = ceiling * 0.7f;
        float room = ceiling - knee;

        if (room <= 1e-6f)
            return;

        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i];

            if (!IsFinite(sample))
            {
                samples[i] = 0f;
                continue;
            }

            float abs = Mathf.Abs(sample);

            if (abs <= knee)
                continue;

            float over = abs - knee;
            float limited = knee + room * (1f - Mathf.Exp(-over / room));

            samples[i] = Mathf.Sign(sample) * limited;
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Berechnet Peak + RMS. Entfernt vorher den DC-Offset (Mittelwert),
    /// damit ein Offset nicht die Lautheitsmessung verfälscht.
    /// </summary>
    private void ComputeStats(float[] samples, out float peak, out float rms)
    {
        peak = 0f;
        rms = 0f;

        if (samples == null || samples.Length == 0)
            return;

        double sum = 0.0;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i];
        float mean = (float)(sum / samples.Length);

        double sumSq = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i] - mean;
            samples[i] = s;
            float abs = Mathf.Abs(s);
            if (abs > peak)
                peak = abs;
            sumSq += (double)s * s;
        }

        rms = Mathf.Sqrt((float)(sumSq / samples.Length));
    }

    private void UpdateRecordingWaveform()
    {
        if (!isRecording)
            return;

        if (visualization == null)
            return;

        if (recordedClip == null)
            return;

        waveformTimer += Time.deltaTime;

        if (waveformTimer < waveformRefreshRate)
            return;

        waveformTimer = 0f;

        int position =
            Microphone.GetPosition(microphoneDevice);

        visualization.GenerateWaveform(
            recordedClip,
            position
        );
    }

    private void UpdateTrimUI()
    {
        if (trimSliderCanvasGroup == null)
            return;

        bool toggleActive = trimSilenceToggle.IsActive();
        Debug.Log("Trim Silence Toggle: " + toggleActive);

        trimSliderCanvasGroup.interactable =
            toggleActive;

        trimSliderCanvasGroup.blocksRaycasts =
            toggleActive;

        trimSliderCanvasGroup.alpha =
            toggleActive ? 1f : 0.4f;
    }
    
}