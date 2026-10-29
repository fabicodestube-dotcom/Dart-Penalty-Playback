using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public struct ThemeEntry
{
    public ThemeColorScheme scheme;
    public ThemeMode mode;
    public Theme theme;
}

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    [SerializeField] private List<ThemeEntry> themes;

    [Header("Defaults (Fallback für fehlende Rollen)")]
    [SerializeField] private Theme defaultDarkTheme;
    [SerializeField] private Theme defaultLightTheme;

    private Dictionary<(ThemeColorScheme, ThemeMode), Theme> _map;
    private Theme activeTheme;
    public event Action OnThemeChanged;
    public Theme ActiveTheme => activeTheme;
    public bool IsReady { get; private set; }
    private ThemeColorScheme currentScheme;
    private ThemeMode currentMode;
    public ThemeColorScheme CurrentScheme => currentScheme;
    public ThemeMode CurrentMode => currentMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _map = new Dictionary<(ThemeColorScheme, ThemeMode), Theme>();
        foreach (var t in themes)
        {
            if (t.theme == null)
                continue;
            _map[(t.scheme, t.mode)] = t.theme;
        }
    }

    public void Initialize(ThemeColorScheme scheme, ThemeMode mode)
    {
        SetTheme(scheme, mode, notify: false);

        ApplicationChrome.navigationBarColor = 0xFF000000;
        ApplicationChrome.statusBarColor = 0xFF000000;
        ApplicationChrome.navigationBarState = ApplicationChrome.States.Visible;
        ApplicationChrome.statusBarState = ApplicationChrome.States.VisibleOverContent;
        ApplicationChrome.statusbar_light_mode = false;

        IsReady = true;
    }

    public void SetTheme(ThemeColorScheme scheme, ThemeMode mode, bool notify = true)
    {
        if (!_map.TryGetValue((scheme, mode), out var theme))
        {
            // Fallback: gleiche Scheme, anderer Mode, dann beliebiger.
            if (!_map.TryGetValue((scheme, ThemeMode.Dark), out theme))
            {
                foreach (var kv in _map)
                {
                    theme = kv.Value;
                    break;
                }
            }
            if (theme == null)
            {
                Debug.LogError($"Theme fehlt: {scheme} {mode}");
                return;
            }
        }

        currentScheme = scheme;
        currentMode = mode;
        activeTheme = theme;
        activeTheme.Init();

        IsReady = true;

        if (notify)
            OnThemeChanged?.Invoke();
    }

    public void SetTheme(ThemeColorScheme scheme, bool notify = true)
    {
        SetTheme(scheme, currentMode, notify);
    }

    public void SetMode(ThemeMode mode, bool notify = true)
    {
        SetTheme(currentScheme, mode, notify);
    }

    public Color GetColor(ThemeColorRole role)
    {
        // Accent2 deprecated → Accent1
        if (role == ThemeColorRole.Accent2)
            role = ThemeColorRole.Accent1;

        if (activeTheme == null)
            return Color.magenta;

        if (activeTheme.Has(role))
            return activeTheme.Get(role);

        var def = currentMode == ThemeMode.Light ? defaultLightTheme : defaultDarkTheme;
        if (def != null && def.Has(role))
            return def.Get(role);

        return Color.magenta;
    }

    public Color GetColor(ThemeColorScheme scheme, ThemeColorRole role)
    {
        // Accent2 deprecated → Accent1
        if (role == ThemeColorRole.Accent2)
            role = ThemeColorRole.Accent1;

        if (!_map.TryGetValue((scheme, currentMode), out var theme))
        {
            if (!_map.TryGetValue((scheme, ThemeMode.Dark), out theme))
            {
                Debug.LogError($"Theme fehlt: {scheme}");
                return Color.magenta;
            }
        }

        if (theme.Has(role))
            return theme.Get(role);

        var def = currentMode == ThemeMode.Light ? defaultLightTheme : defaultDarkTheme;
        if (def != null && def.Has(role))
            return def.Get(role);

        return Color.magenta;
    }

    public Color GetBotDifficultyColor(DartBotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case DartBotDifficulty.Easy:
                return GetColor(ThemeColorScheme.Green, ThemeColorRole.Accent1);
            case DartBotDifficulty.Medium:
                return GetColor(ThemeColorRole.Double);
            case DartBotDifficulty.Hard:
                return GetColor(ThemeColorRole.Triple);
            case DartBotDifficulty.Pro:
                return GetColor(ThemeColorRole.Error);
            default:
                return Color.clear;
        }
    }

    public void ForceApplyAll()
    {
        OnThemeChanged?.Invoke();
    }
}
