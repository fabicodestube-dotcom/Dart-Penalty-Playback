using UnityEngine;

public class UIScreen : MonoBehaviour
{
    public ScreenId id;
    public RectTransform rect;
    public CanvasGroup canvasGroup;
    
    [Header("Lifecycle")]
    public bool deactivateWhenHidden;

    void Awake()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }
}