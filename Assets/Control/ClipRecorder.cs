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

    [Tooltip("Ziel-Peak für normalisierte Aufnahmen")]
    public float normalizeTargetPeak = 1.2f;

    [Tooltip("Optionaler Referenzclip")]
    public AudioClip normalizationReferenceClip;

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
            float t = Mathf.InverseLerp(minThreshold, maxThreshold, silenceThreshold);
            float sliderValue = 1f - t;

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

    // private void HandleRecorderBlink()
    // {
    //     if (recordingImageGroup == null)
    //         return;

    //     if (!isRecording)
    //         return;

    //     recordingImageGroup.alpha =
    //         Mathf.Lerp(
    //             0.3f,
    //             1f,
    //             (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f
    //         );
    // }

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

        recordedClip = null;

        recordedClip = Microphone.Start(
            microphoneDevice,
            false,
            maxRecordingLength,
            sampleRate
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

        if (position <= 0)
        {
            recordedClip = null;
            clipBrowser?.SetLocked(false);

            Debug.LogWarning("Keine Aufnahme erkannt.");
            return;
        }

        float[] samples =
            new float[recordedClip.samples * recordedClip.channels];

        recordedClip.GetData(samples, 0);

        float[] clippedSamples =
            new float[position * recordedClip.channels];

        System.Array.Copy(
            samples,
            clippedSamples,
            clippedSamples.Length
        );

        recordedClip =
            CreateClipFromSamples(clippedSamples, recordedClip.channels);

        if (trimSilenceToggle.IsActive())
        {
            AudioClip trimmed =
                TrimSilence(
                    clippedSamples,
                    recordedClip.channels
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

        recordedClip =
            NormalizeRecordedClip(
                recordedClip,
                normalizationReferenceClip
            );
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
        silenceThreshold = Mathf.Lerp(minThreshold, maxThreshold, 1f - sliderValue);
        Debug.Log("Silence Threshold gesetzt auf: " + silenceThreshold);
    }


    private AudioClip CreateClipFromSamples(float[] samples, int channels)
    {
        AudioClip clip = AudioClip.Create(
            "Recording",
            samples.Length / channels,
            channels,
            sampleRate,
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
    private AudioClip TrimSilence(float[] samples, int channels)
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
            sampleRate,
            false
        );

        trimmedClip.SetData(trimmedSamples, 0);

        return trimmedClip;
    }


    // private AudioClip TrimSilence(float[] samples, int channels)
    // {
    //     int start = 0;
    //     int end = samples.Length - 1;

    //     for (int i = 0; i < samples.Length; i++)
    //     {
    //         if (Mathf.Abs(samples[i]) > silenceThreshold)
    //         {
    //             start = i;
    //             break;
    //         }
    //     }

    //     for (int i = samples.Length - 1; i >= 0; i--)
    //     {
    //         if (Mathf.Abs(samples[i]) > silenceThreshold)
    //         {
    //             end = i;
    //             break;
    //         }
    //     }

    //     int trimmedLength = end - start + 1;

    //     if (trimmedLength <= 0)
    //         return null;

    //     float[] trimmedSamples =
    //         new float[trimmedLength];

    //     System.Array.Copy(
    //         samples,
    //         start,
    //         trimmedSamples,
    //         0,
    //         trimmedLength
    //     );

    //     AudioClip trimmedClip =
    //         AudioClip.Create(
    //             "TrimmedRecording",
    //             trimmedSamples.Length / channels,
    //             channels,
    //             sampleRate,
    //             false
    //         );

    //     trimmedClip.SetData(trimmedSamples, 0);

    //     return trimmedClip;
    // }

    // ====================================================
    // NORMALIZATION
    // ====================================================

    private AudioClip NormalizeRecordedClip(
        AudioClip clip,
        AudioClip referenceClip)
    {
        if (clip == null)
            return null;

        if (normalizeTargetPeak <= 0f)
            return clip;

        int sampleCount = clip.samples * clip.channels;

        float[] samples = new float[sampleCount];

        clip.GetData(samples, 0);

        float peak = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float abs = Mathf.Abs(samples[i]);

            if (abs > peak)
                peak = abs;
        }

        if (peak <= 0f)
            return clip;

        float desiredPeak = normalizeTargetPeak;

        if (referenceClip != null)
        {
            float referencePeak =
                GetClipPeak(referenceClip);

            desiredPeak =
                Mathf.Max(desiredPeak, referencePeak);
        }

        float gain = desiredPeak / peak;
        gain = Mathf.Clamp(gain, 0f, 5f);

        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] *= gain;
        }

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

    private float GetClipPeak(AudioClip clip)
    {
        if (clip == null)
            return 0f;

        int sampleCount = clip.samples * clip.channels;

        float[] samples = new float[sampleCount];

        clip.GetData(samples, 0);

        float peak = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float abs = Mathf.Abs(samples[i]);

            if (abs > peak)
                peak = abs;
        }

        return peak;
    }

    // private void HandleIdlePulse()
    // {
    //     if (isRecording)
    //         return;

    //     if (idleImageTransform == null)
    //         return;

    //     idlePulseTimer += Time.deltaTime;

    //     float cycleLength =
    //         idlePulseInterval + idlePulseDuration;

    //     float cycleTime =
    //         idlePulseTimer % cycleLength;

    //     if (cycleTime > idlePulseDuration)
    //     {
    //         idleImageTransform.localScale = Vector3.one;
    //         return;
    //     }

    //     float t = cycleTime / idlePulseDuration;

    //     float pulse =
    //         Mathf.Sin(t * Mathf.PI);

    //     float scale =
    //         Mathf.Lerp(
    //             1f,
    //             idlePulseScale,
    //             pulse
    //         );

    //     idleImageTransform.localScale =
    //         Vector3.one * scale;
    // }

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