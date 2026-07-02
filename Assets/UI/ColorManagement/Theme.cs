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

    public void Init()
    {
        _map = new Dictionary<ThemeColorRole, Color>();

        foreach (var c in colors)
            _map[c.role] = c.color;

        // 🔥 VALIDATION
        foreach (ThemeColorRole role in System.Enum.GetValues(typeof(ThemeColorRole)))
        {
            if (!_map.ContainsKey(role))
            {
                Debug.LogWarning($"Theme '{name}' fehlt Farbe für: {role}");
            }
        }
    }

    public Color Get(ThemeColorRole role)
    {
        if (_map == null)
            Init();

        return _map.TryGetValue(role, out var col)
            ? col
            : Color.magenta;
    }
}