using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ThemePreviewButton : MonoBehaviour
{
    [SerializeField] private ThemeColorScheme scheme;

    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;

    private void Start()
    {
        ThemeManager.Instance.OnThemeChanged += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnThemeChanged -= Apply;
    }

    private void Apply()
    {
        if (ThemeManager.Instance == null)
            return;


        background.color = ThemeManager.Instance.GetColor(
            scheme,
            ThemeColorRole.Accent1
        );

        label.color = ThemeManager.Instance.GetColor(
            scheme,
            ThemeColorRole.TextOnAccent1
        );
    }
}