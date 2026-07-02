using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HitSectorBar : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [Header("Data")]
    public string sectorKey;

    [Header("UI")]
    public TMP_Text valueLabel;

    [Header("Bar")]
    // The available vertical area for the bar.
    public RectTransform barArea;

    // The actual visual fill element.
    public RectTransform barFill;

    public Image barImage;

    [Header("Interaction")]
    public bool showValueWhilePressed = true;

    private void Awake()
    {
        ApplyValueVisibility(false);
    }

    /// <summary>
    /// Sets both the displayed value and the normalized bar height.
    /// normalizedValue is expected in range 0-1.
    /// </summary>
    public void SetData(int value, float normalizedValue, float areaHeight = -1f)
    {
        if (valueLabel != null)
            valueLabel.text = value.ToString();

        if (barFill == null || barArea == null)
            return;

        if (areaHeight < 0f)
            areaHeight = barArea.rect.height;
        if (areaHeight <= 0f)
            areaHeight = barArea.sizeDelta.y;

        float height = Mathf.Clamp01(normalizedValue) * areaHeight;

        Vector2 size = barFill.sizeDelta;
        size.y = height;
        barFill.sizeDelta = size;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!showValueWhilePressed)
            return;

        ApplyValueVisibility(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!showValueWhilePressed)
            return;

        ApplyValueVisibility(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!showValueWhilePressed)
            return;

        ApplyValueVisibility(false);
    }

    private void ApplyValueVisibility(bool visible)
    {
        if (valueLabel == null)
            return;

        valueLabel.enabled = visible;
    }
}