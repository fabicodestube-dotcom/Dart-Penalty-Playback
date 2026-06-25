using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UISwitch : MonoBehaviour
{
    public Toggle toggle;

    // ❗ getrennt: Position + Visual
    public RectTransform handleRect;
    public Image handleImage;

    public Image background;

    public float animationDuration = 0.2f;



    private Vector2 handleOnPos;
    private Vector2 handleOffPos;

    private Coroutine animRoutine;

    void Start()
    {
        float width = ((RectTransform)transform).rect.width;
        float handleWidth = handleRect.rect.width;

        float offset = (width - handleWidth) / 2f;

        handleOnPos = new Vector2(offset, 0);
        handleOffPos = new Vector2(-offset, 0);

        toggle.onValueChanged.AddListener(OnToggleChanged);

        ApplyInstant(toggle.isOn);

        if (ThemeManager.Instance != null && ThemeManager.Instance.IsReady)
        {
            ThemeManager.Instance.OnThemeChanged += Apply;
        }
        else
        {
            StartCoroutine(WaitForThemeAndApply());
        }
    }

    private System.Collections.IEnumerator WaitForThemeAndApply()
    {
        // wartet bis ThemeManager existiert UND ready ist
        while (ThemeManager.Instance == null || !ThemeManager.Instance.IsReady)
        {
            yield return null; // wartet 1 Frame
        }

        ThemeManager.Instance.OnThemeChanged += Apply;
    }

    

    private void OnToggleChanged(bool isOn)
    {
        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(AnimateSwitch(isOn));
    }

    private IEnumerator AnimateSwitch(bool isOn)
    {
        float time = 0f;

        Vector2 startPos = handleRect.anchoredPosition;
        Vector2 targetPos = isOn ? handleOnPos : handleOffPos;

        Color startHandleColor = handleImage.color;
        Color startBgColor = background.color;

        Color targetHandleColor = GetHandleColor(isOn);
        Color targetBgColor = GetBackgroundColor(isOn);

        while (time < animationDuration)
        {
            time += Time.deltaTime;
            float t = time / animationDuration;

            // Ease Out
            t = 1f - Mathf.Pow(1f - t, 3f);

            handleRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            handleImage.color = Color.Lerp(startHandleColor, targetHandleColor, t);
            background.color = Color.Lerp(startBgColor, targetBgColor, t);

            yield return null;
        }

        handleRect.anchoredPosition = targetPos;
        handleImage.color = targetHandleColor;
        background.color = targetBgColor;
    }

    private void Apply()
    {
        if (toggle == null || ThemeManager.Instance == null)
            return;

        bool isOn = toggle.isOn;

        // 🔥 NUR Farben setzen (keine Animation, keine Position)
        handleImage.color = GetHandleColor(isOn);
        background.color = GetBackgroundColor(isOn);
    }

    private void OnDestroy()
    {
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnThemeChanged -= Apply;
    }

    private void ApplyInstant(bool isOn)
    {
        handleRect.anchoredPosition = isOn ? handleOnPos : handleOffPos;
        handleImage.color = GetHandleColor(isOn);
        background.color = GetBackgroundColor(isOn);
    }

    private Color GetHandleColor(bool isOn)
    {
        return isOn
            ? ThemeManager.Instance.GetColor(ThemeColorRole.TextOnAccent1)
            : ThemeManager.Instance.GetColor(ThemeColorRole.SingleSelectionButtonInactive);
    }

    private Color GetBackgroundColor(bool isOn)
    {
        return isOn
            ? ThemeManager.Instance.GetColor(ThemeColorRole.Accent1)
            : ThemeManager.Instance.GetColor(ThemeColorRole.BottomBarInactive);
    }

    public void RefreshVisual()
    {
        ApplyInstant(toggle.isOn);
    }
}