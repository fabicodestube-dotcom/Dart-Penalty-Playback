using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class ThemeColorBinding : MonoBehaviour
{
    [SerializeField] private ThemeColorRole role;

    private Graphic _graphic;

    // 🔥 wichtig: verhindert doppelte Subscriptions
    private bool _isSubscribed;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
    }

    private void Start()
    {
        // 🔥 Event registrieren (manuell kontrolliert)
        SubscribeToTheme();

        // 🔥 Initialer Sync nach Start
        Apply();
    }

    /// <summary>
    /// Registriert dieses Binding beim ThemeManager.
    /// Wird manuell kontrolliert, kein Unity Enable/Disable nötig.
    /// </summary>
    private void SubscribeToTheme()
    {
        if (_isSubscribed)
            return;

        if (ThemeManager.Instance == null)
            return;

        ThemeManager.Instance.OnThemeChanged += Apply;
        _isSubscribed = true;
    }

    /// <summary>
    /// Entfernt dieses Binding sauber aus dem Event.
    /// Verhindert MissingReferenceException bei Destroy.
    /// </summary>
    private void UnsubscribeFromTheme()
    {
        if (!_isSubscribed)
            return;

        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.OnThemeChanged -= Apply;
        }

        _isSubscribed = false;
    }

    private void Apply()
    {
        // 🔥 harte Safety Guards gegen destroyed / invalid state
        if (this == null)
            return;

        if (_graphic == null)
            return;

        if (ThemeManager.Instance == null)
            return;

        _graphic.color = ThemeManager.Instance.GetColor(role);
    }

    private void OnDestroy()
    {
        // 🔥 KRITISCH: Event sauber entfernen bevor Objekt endgültig verschwindet
        UnsubscribeFromTheme();
    }
}