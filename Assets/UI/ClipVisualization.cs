using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ClipVisualization : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private RawImage waveformImage;
    [SerializeField] private TMP_Text defaultText;

    [Header("Waveform")]
    public int barWidth = 2;
    public int spacing = 2;

    private RectTransform rectTransform;

    [Header("Playhead")]
    public AudioSource audioSource;
    public bool showPlayhead = true;
    //public Color playheadColor = Color.red;
    public float playheadWidth = 5f;

    private RectTransform playheadLine;
    private Image playheadLineImage;

    private void Awake()
    {
        rectTransform = waveformImage.GetComponent<RectTransform>();

        if (waveformImage == null)
            Debug.LogError("WaveformImage fehlt.");

        if (defaultText == null)
            Debug.LogError("DefaultText fehlt.");

        ShowDefaultText(true);
    }

    private void Update()
    {
        if (!showPlayhead || !waveformImage.gameObject.activeSelf)
        {
            if (playheadLine != null)
                playheadLine.gameObject.SetActive(false);
            return;
        }

        EnsurePlayheadLine();
        playheadLine.gameObject.SetActive(true);

        float normalized = 0f;

        if (audioSource != null &&
            audioSource.clip != null &&
            audioSource.clip.samples > 0)
        {
            normalized = Mathf.Clamp01(
                audioSource.timeSamples / (float)audioSource.clip.samples
            );
        }

        float x = normalized * rectTransform.rect.width;

        playheadLine.anchoredPosition = new Vector2(x, 0f);
        playheadLine.sizeDelta = new Vector2(playheadWidth, rectTransform.rect.height);
        playheadLine.SetAsLastSibling();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (audioSource == null || audioSource.clip == null)
            return;

        if (!showPlayhead)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var localPoint))
            return;

        float normalized =
            Mathf.InverseLerp(rectTransform.rect.xMin, rectTransform.rect.xMax, localPoint.x);

        normalized = Mathf.Clamp01(normalized);

        audioSource.timeSamples =
            Mathf.RoundToInt(normalized * audioSource.clip.samples);

        if (audioSource.isPlaying)
            audioSource.Pause();
    }

    private void EnsurePlayheadLine()
    {
        if (playheadLine != null)
            return;

        GameObject go = new GameObject("Playhead", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(waveformImage.transform, false);

        playheadLine = go.GetComponent<RectTransform>();
        playheadLineImage = go.GetComponent<Image>();

        playheadLineImage.color = ThemeManager.Instance.GetColor(ThemeColorRole.Text3);
        playheadLineImage.raycastTarget = false;

        playheadLine.anchorMin = Vector2.zero;
        playheadLine.anchorMax = Vector2.zero;
        playheadLine.pivot = new Vector2(0.5f, 0f);
    }

    // =========================================================
    // PUBLIC API (FULL + LIVE)
    // =========================================================

    public void GenerateWaveform(AudioClip audioClip)
    {
        GenerateWaveform(audioClip, audioClip != null ? audioClip.samples : 0);
    }

    public void GenerateWaveform(AudioClip audioClip, int sampleLimit)
    {
        if (audioClip == null || sampleLimit <= 0)
        {
            ShowDefaultText(true);
            return;
        }

        ShowDefaultText(false);

        int width = Mathf.RoundToInt(rectTransform.rect.width);
        int height = Mathf.RoundToInt(rectTransform.rect.height);

        if (width <= 0 || height <= 0)
            return;

        int totalSamples = sampleLimit * audioClip.channels;

        float[] samples = new float[totalSamples];
        audioClip.GetData(samples, 0);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color bg = ThemeManager.Instance.GetColor(ThemeColorRole.Background2);

        Color[] fill = new Color[width * height];
        for (int i = 0; i < fill.Length; i++)
            fill[i] = bg;

        tex.SetPixels(fill);

        int bars = width / (barWidth + spacing);
        if (bars <= 0)
            return;

        int samplesPerBar = Mathf.Max(1, totalSamples / bars);

        Color wave = ThemeManager.Instance.GetColor(ThemeColorRole.Text3);

        int center = height / 2;

        for (int i = 0; i < bars; i++)
        {
            int start = i * samplesPerBar;

            float max = 0f;

            for (int j = 0; j < samplesPerBar; j++)
            {
                int idx = start + j;
                if (idx >= samples.Length)
                    break;

                float a = Mathf.Abs(samples[idx]);
                if (a > max) max = a;
            }

            int barH = Mathf.RoundToInt(max * center);

            int x0 = i * (barWidth + spacing);

            for (int x = 0; x < barWidth; x++)
            {
                int px = x0 + x;
                if (px >= width) continue;

                for (int y = -barH; y <= barH; y++)
                {
                    int py = center + y;
                    if (py < 0 || py >= height) continue;

                    tex.SetPixel(px, py, wave);
                }
            }
        }

        tex.Apply();

        if (waveformImage.texture != null)
            Destroy(waveformImage.texture);

        waveformImage.texture = tex;
    }

    private void ShowDefaultText(bool show)
    {
        defaultText.gameObject.SetActive(show);
        waveformImage.gameObject.SetActive(!show);
    }
}