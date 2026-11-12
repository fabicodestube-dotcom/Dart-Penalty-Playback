using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the BorderGlow graph (UI_BorderGlow): _Rotation is the
/// Circumferential position of the gap as 0..1 loop (one full round per loop).
/// Controlled from the outside (e.g. X01PlayerItem) by StartGlow()/StopGlow()
/// Sits on the same GameObject as the Glow image.
/// IMPORTANT: Graphic.material does NOT instantiate automatically at UGUI,
/// So this is copied explicitly with "new material."
/// (M_BorderGlow) is never touched and never destroyed.
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
    private Material _shared;   // Prefab asset, will NEVER be destroyed
    private Material _instance; // own copy, is destroyed at Stop
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
        _instance.SetFloat(RotationId, _rotation); // 0..1 = one circumference
    }

    /// <summary>Starts the Glow (own material instance per item).</summary>
    public void StartGlow()
    {
        if (_graphic == null)
            _graphic = GetComponent<Graphic>();
        if (_graphic == null)
            return;

        if (_instance == null)
        {
            _shared = _graphic.material; // The Shared Asset
            if (_shared == null)
                return;
            _instance = new Material(_shared); // Explicit copy per item
            _graphic.material = _instance;
        }

        SyncAspect();
        _instance.SetFloat(RotationId, _rotation); // 0..1 = one circumference
        _glowing = true;
        enabled = true;
    }

    /// <summary>Stop the Glow, restore the shared material.</summary>
    public void StopGlow()
    {
        _glowing = false;
        enabled = false;

        if (_instance != null)
        {
            // destroyed-Check: protects against double-stop during the teardown
            if (_graphic != null)
                _graphic.material = _shared;
            Destroy(_instance);
            _instance = null;
        }
    }

    /// <summary>Sets the speed to runtime (turns/second).</summary>
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
