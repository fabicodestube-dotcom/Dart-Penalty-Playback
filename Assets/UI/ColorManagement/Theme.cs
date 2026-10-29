using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "UI/Theme")]
public class Theme : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public ThemeColorRole role;
        public Color color;
    }

    public List<Entry> colors;

    private Dictionary<ThemeColorRole, Color> _map;

    public bool Has(ThemeColorRole role)
    {
        if (_map == null)
            Init();
        return _map != null && _map.ContainsKey(role);
    }

    public void Init()
    {
        _map = new Dictionary<ThemeColorRole, Color>();

        foreach (var c in colors)
            _map[c.role] = c.color;

        // Validation: nur warnen, wenn Theme komplett leer ist.
        if (_map.Count == 0)
            Debug.LogWarning($"Theme '{name}' hat keine Farben definiert.");
    }

    public Color Get(ThemeColorRole role)
    {
        // Accent2 ist deprecated → Accent1 verwenden.
        if (role == ThemeColorRole.Accent2)
            role = ThemeColorRole.Accent1;

        if (_map == null)
            Init();

        return _map.TryGetValue(role, out var col)
            ? col
            : Color.magenta;
    }
}