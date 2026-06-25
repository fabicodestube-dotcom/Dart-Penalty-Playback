using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public struct ThemeEntry
{
    public ThemeColorScheme scheme;
    public Theme theme;
}

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    [SerializeField] private List<ThemeEntry> themes;

    private Dictionary<ThemeColorScheme, Theme> _map;

    private Theme activeTheme;

    public event Action OnThemeChanged;

    public Theme ActiveTheme => activeTheme;

    public bool IsReady { get; private set; }

    private ThemeColorScheme currentScheme;

    public ThemeColorScheme CurrentScheme => currentScheme;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _map = new Dictionary<ThemeColorScheme, Theme>();

        foreach (var t in themes)
        {
            _map[t.scheme] = t.theme;
        }
    }

    public void Initialize(ThemeColorScheme scheme)
    {
        SetTheme(scheme, notify: false);

        // 🔥 Chrome initial setzen
        ApplicationChrome.navigationBarColor = 0xFF000000;
        ApplicationChrome.statusBarColor = 0xFF000000;

        ApplicationChrome.navigationBarState = ApplicationChrome.States.Visible;
        ApplicationChrome.statusBarState = ApplicationChrome.States.VisibleOverContent;

        ApplicationChrome.statusbar_light_mode = false; // dunkle Icons passen zu schwarzer Bar

        IsReady = true;
    }

    public void SetTheme(ThemeColorScheme scheme, bool notify = true)
    {
        if (!_map.TryGetValue(scheme, out var theme))
        {
            Debug.LogError($"Theme fehlt: {scheme}");
            return;
        }

        currentScheme = scheme;

        activeTheme = theme;
        activeTheme.Init();

        IsReady = true; // 🔥 NEU

        if (notify)
            OnThemeChanged?.Invoke();
    }

    public Color GetColor(ThemeColorRole role)
    {
        if (activeTheme == null)
        {
            //Debug.LogError("Kein aktives Theme gesetzt!");
            return Color.magenta;
        }

        return activeTheme.Get(role);
    }

    public Color GetColor(ThemeColorScheme scheme, ThemeColorRole role)
    {
        if (!_map.TryGetValue(scheme, out var theme))
        {
            Debug.LogError($"Theme fehlt: {scheme}");
            return Color.magenta;
        }

        return theme.Get(role);
    }

    public void ForceApplyAll()
    {
        OnThemeChanged?.Invoke();
    }
}