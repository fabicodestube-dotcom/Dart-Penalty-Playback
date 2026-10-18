using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Treibt den BorderGlow-Graphen (UI_BorderGlow): _Rotation ist die
/// Umfangsposition der Lücke als 0..1-Loop (eine volle Runde pro Loop).
/// Wird von außen (z.B. X01PlayerItem) per StartGlow()/StopGlow() gesteuert.
/// Sitzt auf demselben GameObject wie das Glow-Image.
/// WICHTIG: Graphic.material instanziiert bei UGUI NICHT automatisch,
/// daher wird hier explizit mit "new Material" kopiert. Das Shared-Asset
/// (M_BorderGlow) wird nie angefasst und nie destroyed.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class BorderGlowAnimator : MonoBehaviour
{
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");

    [Header("Animation")]
    [Tooltip("Umdrehungen pro Sekunde. 0.3 = eine Runde in ~3.3 Sekunden.")]
    [SerializeField] private float rotationsPerSecond = 0.3f;

    private Graphic _graphic;
    private RectTransform _rect;
    private Material _shared;   // Prefab-Asset, wird NIE zerstört
    private Material _instance; // eigene Kopie, wird bei Stop zerstört
    private float _rotation;
    private bool _glowing;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _rect = GetComponent<RectTransform>();
    }

    private void OnDisable() => StopGlow();

    private void OnDestroy() => StopGlow();

    private void Update()
    {
        if (!_glowing || _instance == null)
            return;

        _rotation += rotationsPerSecond * Time.deltaTime;
        _rotation %= 1f;
        _instance.SetFloat(RotationId, _rotation); // 0..1 = einmal Umfang
    }

    /// <summary>Startet den Glow (eigene Material-Instanz pro Item).</summary>
    public void StartGlow()
    {
        if (_graphic == null)
            _graphic = GetComponent<Graphic>();
        if (_graphic == null)
            return;

        if (_instance == null)
        {
            _shared = _graphic.material; // noch das Shared-Asset
            if (_shared == null)
                return;
            _instance = new Material(_shared); // explizite Kopie pro Item
            _graphic.material = _instance;
        }

        SyncAspect();
        _instance.SetFloat(RotationId, _rotation); // 0..1 = einmal Umfang
        _glowing = true;
        enabled = true;
    }

    /// <summary>Stoppt den Glow, stellt das Shared-Material wieder her.</summary>
    public void StopGlow()
    {
        _glowing = false;
        enabled = false;

        if (_instance != null)
        {
            // destroyed-Check: schützt vor Doppel-Stop beim Teardown
            if (_graphic != null)
                _graphic.material = _shared;
            Destroy(_instance);
            _instance = null;
        }
    }

    /// <summary>Setzt die Geschwindigkeit zur Laufzeit (Umdrehungen/Sekunde).</summary>
    public void SetSpeed(float value) => rotationsPerSecond = value;

    public bool IsGlowing => _glowing;

    private void SyncAspect()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();
        if (_rect == null || _instance == null)
            return;

        float w = Mathf.Max(1f, _rect.rect.width);
        float h = Mathf.Max(1f, _rect.rect.height);

        if (_instance.HasProperty(AspectId))
            _instance.SetFloat(AspectId, w / h);
    }
}
