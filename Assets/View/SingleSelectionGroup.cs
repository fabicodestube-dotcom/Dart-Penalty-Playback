using System.Collections.Generic;
using UnityEngine;

public class SingleSelectionGroup : MonoBehaviour
{
    [SerializeField] private List<SingleSelectionButton> buttons;

    private SingleSelectionButton _active;

    private void Start()
    {
        ThemeManager.Instance.OnThemeChanged += ApplyTheme;

        if (buttons.Count > 0)
        {
            if (_active == null)
                _active = buttons[0];

            foreach (var b in buttons)
            {
                b.Init(this);
            }
        }

        // 🔥 Initial Sync NUR wenn Theme existiert
        if (ThemeManager.Instance.ActiveTheme != null)
        {
            ApplyTheme();
        }
    }

    private void OnDestroy()
    {
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.OnThemeChanged -= ApplyTheme;
    }

    public void Init(int index)
    {
        if (index < 0 || index >= buttons.Count)
            return;

        SetActive(buttons[index]);
    }

    public void SetActive(SingleSelectionButton button)
    {
        _active = button;

        if (ThemeManager.Instance.ActiveTheme != null)
        {
            ApplyTheme();
        }
    }

    public int GetSelectedIndex()
    {
        return buttons.IndexOf(_active);
    }

    private void ApplyTheme()
    {
        foreach (var b in buttons)
        {
            b.Apply(b == _active);
        }
    }
}