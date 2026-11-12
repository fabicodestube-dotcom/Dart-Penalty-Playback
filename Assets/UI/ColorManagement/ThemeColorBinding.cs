using UnityEngine; 
using UnityEngine.UI; 

[RequireComponent(typeof(Graphic))]
public class ThemeColorBinding : MonoBehaviour
{
    [SerializeField] private ThemeColorRole role;

    private Graphic _graphic;

    // Important: prevents double subscriptions
    private bool _isSubscribed;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
    }

    /// <summary>
    /// Initializes the binding at runtime (e.g. for dynamically generated
    /// Labels) and applies the role immediately.
    /// </summary>
    public void SetRole(ThemeColorRole newRole)
    {
        role = newRole;

        if (_graphic == null)
            _graphic = GetComponent<Graphic>();

        Apply();
    }

    private void Start()
    {
        // Register event (manually controlled)
        SubscribeToTheme();

        // Initial Sync after Start
        Apply();
    }

    /// <summary>
    /// Register this binding at ThemeManager.
    /// Manually controlled, no Unity Enable/Disable is required.
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
    /// Remove this binding cleanly from the event.
    /// Prevent MissingReferenceException at Destroy.
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
        // Safety Guards against destroyed / invalid state
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
        // CRITICAL: Remove the event cleanly before the object finally disappears
        UnsubscribeFromTheme();
    }
}