using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WinAnimationController : MonoBehaviour
{
    private const int CanvasSortingOrder = 1000;
    private const float ReferenceWidth = 1220f;
    private const float ReferenceHeight = 2712f;

    private const float DimAlpha = 0.78f;
    private const float FadeInDuration = 0.08f;
    private const float BannerPopDuration = 0.20f;
    private const float TitleDuration = 0.14f;
    private const float NameDuration = 0.12f;
    private const float HoldLegSetDuration = 0.75f;
    private const float HoldMatchDuration = 1.10f;
    private const float FadeOutDuration = 0.14f;

    private const float TitleFontSize = 132f;
    private const float NameFontSize = 72f;

    private static readonly Color LegColor = new Color32(0x4F, 0xC3, 0xF7, 0xFF);
    private static readonly Color SetColor = new Color32(0xFF, 0xB7, 0x4D, 0xFF);
    private static readonly Color MatchColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);

    private static TMP_FontAsset cachedFont;
    private static Sprite cachedGlowSprite;

    private readonly WinSequence sequence = new();

    private CanvasGroup canvasGroup;
    private RectTransform visualRoot;
    private Image dimBackground;
    private Image glowImage;
    private RectTransform bannerRect;
    private Image accentTop;
    private Image accentBottom;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI nameText;

    private bool built;
    private bool flushScheduled;

    public bool IsPlaying { get; private set; }

    public static WinAnimationController Create()
    {
        var existing = FindAnyObjectByType<WinAnimationController>();
        if (existing != null)
            return existing;

        var go = new GameObject(
            "WinAnimationOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var controller = go.AddComponent<WinAnimationController>();
        controller.Build();
        return controller;
    }

    public void NotifyWin(WinLevel level, string playerName, Action onComplete = null)
    {
        sequence.Report(level, playerName, onComplete);

        if (!flushScheduled)
        {
            flushScheduled = true;
            StartCoroutine(FlushAfterCascade());
        }
    }

    private void Awake()
    {
        Build();
    }

    private IEnumerator FlushAfterCascade()
    {
        yield return null;

        flushScheduled = false;

        if (!sequence.TryTake(out var level, out var playerName, out var complete))
            yield break;

        yield return PlayAnimation(level, playerName);

        complete?.Invoke();
    }

    private IEnumerator PlayAnimation(WinLevel level, string playerName)
    {
        IsPlaying = true;

        visualRoot.gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;

        Color accent = AccentFor(level);
        Color accentTransparent = new Color(accent.r, accent.g, accent.b, 0f);
        Color accentSolid = new Color(accent.r, accent.g, accent.b, 1f);

        titleText.text = TitleFor(level);
        nameText.text = playerName;

        dimBackground.color = new Color(0f, 0f, 0f, DimAlpha);
        glowImage.color = accentTransparent;
        glowImage.rectTransform.localScale = Vector3.one;
        accentTop.color = accentTransparent;
        accentBottom.color = accentTransparent;
        accentTop.rectTransform.localScale = new Vector3(0f, 1f, 1f);
        accentBottom.rectTransform.localScale = new Vector3(0f, 1f, 1f);

        bannerRect.localScale = Vector3.one * 0.72f;
        bannerRect.anchoredPosition = new Vector2(0f, 40f);

        titleText.alpha = 0f;
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 55f);
        nameText.alpha = 0f;
        nameText.rectTransform.anchoredPosition = new Vector2(0f, -47f);

        yield return FadeCanvasGroup(0f, 1f, FadeInDuration);
        yield return PopBanner(accent, accentSolid);
        yield return AnimateText(titleText, TitleDuration, 55f, 25f);
        yield return AnimateText(nameText, NameDuration, -47f, -65f);

        float holdDuration = level == WinLevel.Match ? HoldMatchDuration : HoldLegSetDuration;
        yield return HoldPulse(holdDuration, accent);

        yield return FadeOut();

        visualRoot.gameObject.SetActive(false);
        canvasGroup.blocksRaycasts = false;
        IsPlaying = false;
    }

    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            float p = Mathf.Clamp01(t / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, EaseOutCubic(p));
            t += DeltaTime();
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private IEnumerator PopBanner(Color accent, Color accentSolid)
    {
        float t = 0f;
        Color glowTarget = new Color(accent.r, accent.g, accent.b, 0.55f);

        while (t < BannerPopDuration)
        {
            float p = Mathf.Clamp01(t / BannerPopDuration);
            float pop = EaseOutBack(p);
            float fade = EaseOutCubic(p);

            bannerRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.72f, 1f, pop);
            glowImage.color = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0f, glowTarget.a, fade));
            accentTop.color = new Color(accentSolid.r, accentSolid.g, accentSolid.b, fade);
            accentBottom.color = accentTop.color;
            accentTop.rectTransform.localScale = new Vector3(fade, 1f, 1f);
            accentBottom.rectTransform.localScale = new Vector3(fade, 1f, 1f);

            t += DeltaTime();
            yield return null;
        }

        bannerRect.localScale = Vector3.one;
        glowImage.color = glowTarget;
        accentTop.color = accentSolid;
        accentBottom.color = accentSolid;
        accentTop.rectTransform.localScale = Vector3.one;
        accentBottom.rectTransform.localScale = Vector3.one;
    }

    private static IEnumerator AnimateText(TMP_Text text, float duration, float fromY, float toY)
    {
        float t = 0f;

        while (t < duration)
        {
            float p = Mathf.Clamp01(t / duration);
            float eased = EaseOutCubic(p);

            text.alpha = p;
            text.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(fromY, toY, eased));

            t += DeltaTime();
            yield return null;
        }

        text.alpha = 1f;
        text.rectTransform.anchoredPosition = new Vector2(0f, toY);
    }

    private IEnumerator HoldPulse(float duration, Color accent)
    {
        float t = 0f;

        while (t < duration)
        {
            float p = Mathf.Clamp01(t / duration);
            float pulse = Mathf.Sin(p * Mathf.PI * 3.2f) * 0.5f + 0.5f;

            glowImage.rectTransform.localScale = Vector3.one * (1f + pulse * 0.06f);
            glowImage.color = new Color(accent.r, accent.g, accent.b, 0.35f + pulse * 0.20f);
            bannerRect.localScale = Vector3.one * (1f + pulse * 0.012f);

            t += DeltaTime();
            yield return null;
        }

        glowImage.rectTransform.localScale = Vector3.one;
        bannerRect.localScale = Vector3.one;
    }

    private IEnumerator FadeOut()
    {
        float t = 0f;
        float startAlpha = canvasGroup.alpha;

        while (t < FadeOutDuration)
        {
            float p = Mathf.Clamp01(t / FadeOutDuration);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, EaseInCubic(p));
            bannerRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, p);

            t += DeltaTime();
            yield return null;
        }

        canvasGroup.alpha = 0f;
        bannerRect.localScale = Vector3.one;
    }

    private void Build()
    {
        if (built)
            return;

        built = true;

        SetStretch((RectTransform)transform);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        visualRoot = CreateUIObject("VisualRoot", transform);
        SetStretch(visualRoot);

        dimBackground = CreateImage("DimBackground", visualRoot, null, new Color(0f, 0f, 0f, DimAlpha));
        SetStretch(dimBackground.rectTransform);
        dimBackground.raycastTarget = true;

        glowImage = CreateImage("Glow", visualRoot, GetGlowSprite(), new Color(1f, 1f, 1f, 0f));
        glowImage.rectTransform.sizeDelta = new Vector2(1100f, 1100f);
        glowImage.rectTransform.anchoredPosition = new Vector2(0f, 60f);

        bannerRect = CreateUIObject("Banner", visualRoot);
        bannerRect.sizeDelta = new Vector2(1000f, 360f);
        bannerRect.anchoredPosition = new Vector2(0f, 40f);

        accentTop = CreateImage("AccentLineTop", bannerRect, null, new Color(1f, 1f, 1f, 0f));
        accentTop.rectTransform.sizeDelta = new Vector2(420f, 5f);
        accentTop.rectTransform.anchoredPosition = new Vector2(0f, 120f);

        accentBottom = CreateImage("AccentLineBottom", bannerRect, null, new Color(1f, 1f, 1f, 0f));
        accentBottom.rectTransform.sizeDelta = new Vector2(420f, 5f);
        accentBottom.rectTransform.anchoredPosition = new Vector2(0f, -120f);

        titleText = CreateText("TitleText", bannerRect, TitleFontSize, FontStyles.Bold);
        titleText.characterSpacing = 10f;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 80f;
        titleText.fontSizeMax = TitleFontSize;
        titleText.rectTransform.sizeDelta = new Vector2(1000f, 180f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 25f);

        nameText = CreateText("PlayerNameText", bannerRect, NameFontSize, FontStyles.Normal);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 40f;
        nameText.fontSizeMax = NameFontSize;
        nameText.rectTransform.sizeDelta = new Vector2(1000f, 100f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, -65f);

        visualRoot.gameObject.SetActive(false);
    }

    private static RectTransform CreateUIObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        var rect = CreateUIObject(objectName, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize, FontStyles fontStyle)
    {
        var rect = CreateUIObject(objectName, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;
        return text;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static TMP_FontAsset GetFont()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = TMP_Settings.defaultFontAsset;
        if (cachedFont != null)
            return cachedFont;

        var texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
            {
                cachedFont = texts[i].font;
                break;
            }
        }

        return cachedFont;
    }

    private static Sprite GetGlowSprite()
    {
        if (cachedGlowSprite != null)
            return cachedGlowSprite;

        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float maxDistance = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / maxDistance;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        cachedGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return cachedGlowSprite;
    }

    private static Color AccentFor(WinLevel level)
    {
        return level switch
        {
            WinLevel.Leg => LegColor,
            WinLevel.Set => SetColor,
            WinLevel.Match => MatchColor,
            _ => Color.white
        };
    }

    private static string TitleFor(WinLevel level)
    {
        return level switch
        {
            WinLevel.Leg => "LEG WIN",
            WinLevel.Set => "SET WIN",
            WinLevel.Match => "GAME WIN",
            _ => string.Empty
        };
    }

    private static float DeltaTime()
    {
        return Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
    }

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float p = 1f - x;
        return 1f - p * p * p;
    }

    private static float EaseInCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x;
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
