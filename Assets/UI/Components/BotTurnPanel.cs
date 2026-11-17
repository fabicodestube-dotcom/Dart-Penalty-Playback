using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BotTurnPanel : MonoBehaviour
{
    private enum DartPhase
    {
        None,
        Flight,
        Impact,
        Hold
    }

    private const float CardMargin = 24f;
    private const float CardBorderInset = 4f;
    private const float CardPadding = 24f;
    private const float InfoLeftPadding = 50f;
    private const float ColumnGap = 16f;
    private const float InfoColumnWidth = 0.42f;
    private const float FooterGap = 12f;
    private const float FooterWidth = 360f;
    private const float FooterHeight = 116f;

    private const float TightCardMargin = 12f;
    private const float TightCardPadding = 12f;
    private const float TightFooterGap = 8f;
    private const float TightFooterHeight = 96f;
    private const float TightLayoutHeight = 600f;

    private const float ReferenceBoardSize = 408f;
    private const float BaseBoardRadius = 192f;
    private const float BaseLabelRadius = 178f;
    private const float RightPadding = 32f;

    private const float BotIconSize = 200f;
    private const float NameFontSize = 50f;
    private const float NameTextHeight = 60f;
    private const float NameIconGap = 6f;
    private const float InfoFontSize = 45f;
    private const float StatsBlockHeight = NameTextHeight + NameIconGap + BotIconSize;

    private const float OverlayCardMargin = 20f;
    private const float OverlayCardPadding = 32f;
    private const float OverlayHeaderHeight = 250f;
    private const float OverlayHeaderGap = 20f;
    private const float OverlayIconSize = 180f;
    private const float OverlayHeaderTextGap = 24f;
    private const float OverlayTopBarHeight = 200f;
    private const float OverlayLeftCardMaxWidthFraction = 0.38f;
    private const float OverlayFooterWidth = 420f;
    private const float OverlayFooterHeight = 116f;
    private const float OverlayFooterGap = 12f;
    private const float ModeButtonWidth = 280f;
    private const float ModeButtonHeight = 68f;
    private const float ModeButtonFontSize = 28f;

    private const float BaseBoardMarkerSize = 34f;
    private const float BaseBoardMarkerOutlineSize = 40f;
    private const float BaseBoardMarkerThickness = 3f;
    private const float BaseBoardMarkerOutlineThickness = 7f;
    private const float BaseDartSize = 153.3f;
    private const float DartTipPixelX = 0f;
    private const float DartTipPixelYFromBottom = 0f;

    private const float TargetPreviewDuration = 0.8f;
    private const float FlightDuration = 0.45f;
    private const float ImpactDuration = 0.12f;
    private const float HitHoldDuration = 1f;
    private const float HitPopupDuration = 0.32f;
    private const float HitPopupFadeDuration = 0.15f;
    private const float HitPopupOutlineWidth = 0.2f;

    private const float TargetLockSnapDuration = 0.15f;
    private const float TargetLockPulseDuration = 0.8f;
    private const float TargetLockReticleZoomDuration = 0.55f;
    private const float TargetLockSnapScale = 1.25f;
    private const float TargetLockPulseScale = 1.08f;
    private const float TargetLockReticlePulseScale = 1.06f;
    private const float TargetLockReticleBoardPadding = 1.06f;
    private const float TargetLockCrosshairMinAlpha = 0.82f;
    private const float TargetLockReticleMaxAlpha = 0.7f;
    private const float TargetLockReticleMinAlpha = 0.35f;

    private static readonly Color NumberColor = new Color(0.96f, 0.96f, 0.96f, 1f);
    private static readonly Color MarkerColor = new Color(1f, 1f, 1f, 0.98f);
    private static readonly Color MarkerOutlineColor = new Color(0f, 0f, 0f, 0.95f);

    private const float SingleOuterRingRadius = 0.618f;
    private const float TripleRingRadius = 0.4335f;
    private const float DoubleRingRadius = 0.7875f;
    private const float OuterBullRingRadius = 0.07f;

    private const float VisualBullInnerRadius = 0.036f;
    private const float VisualOuterBullRadius = 0.094f;
    private const float VisualTripleInnerRadius = 0.409f;
    private const float VisualTripleOuterRadius = 0.456f;
    private const float VisualDoubleInnerRadius = 0.749f;
    private const float VisualDoubleOuterRadius = 0.796f;
    private const float VisualBoardOuterRadius = 1.05f;

    private const float PhysicalBullInnerMm = 6.35f;
    private const float PhysicalOuterBullRadiusMm = 11.125f;
    private const float PhysicalOuterBullOuterMm = 15.9f;
    private const float PhysicalTripleInnerMm = 99f;
    private const float PhysicalTripleRadiusMm = 103f;
    private const float PhysicalTripleOuterMm = 107f;
    private const float PhysicalSingleRadiusMm = 134.5f;
    private const float PhysicalDoubleInnerMm = 162f;
    private const float PhysicalDoubleRadiusMm = 165.5f;
    private const float PhysicalDoubleOuterMm = 170f;
    private const float PhysicalOffBoardMaxMm = 220f;

    private static bool _sessionOverlaySuppressed;

    private GameObject _root;
    private GameObject _playingContainer;
    private GameObject _pausedContainer;
    private Sprite _dartSprite;
    private Sprite _boardSprite;
    private Transform _botIconRow;
    private Image _botIconSource;
    private GameObject _playerCardPrefab;
    private BotOverlayCardPlacement _cardPlacement;

    private bool _built;
    private bool _overlayMode;
    private GameObject _generatedRoot;
    private Image _rootImage;
    private Transform _compactParent;
    private int _compactSiblingIndex;
    private Vector2 _compactAnchorMin;
    private Vector2 _compactAnchorMax;
    private Vector2 _compactOffsetMin;
    private Vector2 _compactOffsetMax;
    private Vector2 _compactPivot;
    private Vector2 _compactAnchoredPosition;
    private Vector3 _compactLocalScale;
    private Quaternion _compactLocalRotation;
    private bool _compactRootRaycastTarget;
    private bool _rootBindingStripped;

    private RectTransform _card;
    private RectTransform _content;
    private RectTransform _infoColumn;
    private RectTransform _header;
    private Image _cardImage;
    private Image _cardBorder;
    private RectTransform _boardArea;
    private Image _boardImage;
    private RectTransform _footer;
    private RectTransform _animationLayer;
    private Button _modeButton;
    private Image _modeButtonImage;
    private TMP_Text _modeButtonText;

    private float _boardAreaSize;
    private float _boardSize;
    private float _boardRadius;
    private float _labelRadius;
    private float _boardMarkerSize;
    private float _boardMarkerOutlineSize;
    private float _boardMarkerThickness;
    private float _boardMarkerOutlineThickness;
    private float _dartSize;

    private Image _botIcon;
    private TMP_Text _nameText;
    private RectTransform _targetCrosshair;
    private CanvasGroup _targetCrosshairGroup;
    private readonly List<Image> _targetCrosshairParts = new List<Image>();
    private RectTransform _targetReticleRoot;
    private CanvasGroup _targetReticleGroup;
    private readonly List<Image> _targetReticleParts = new List<Image>();
    private float _targetReticleBaseSize;
    private Coroutine _targetLockRoutine;
    private int _targetLockVersion;
    private GameObject _playerCardInstance;
    private RectTransform _playerCardRect;
    private X01PlayerItem _x01PlayerCard;
    private CricketPlayerListItem _cricketPlayerCard;
    private ATCPlayerListItem _atcPlayerCard;
    private object _playerStats;
    private float _overlayCardVisualWidth;
    private float _overlayCardVisualHeight;
    private float _overlayHeaderExtent;
    private Image _dartImage;
    private Outline _dartOutline;
    private RectTransform _dartRect;
    private TMP_Text _hitPopupText;
    private CanvasGroup _hitPopupGroup;
    private Vector2 _hitPopupOffset;

    private TMP_FontAsset _font;

    private DartBot _currentBot;
    private bool _hasTarget;
    private bool _hitHoldActive;
    private bool _hitPopupActive;
    private Coroutine _hitPopupRoutine;
    private bool _animationPaused;
    private DartPhase _dartPhase;
    private float _dartFlightProgress;
    private Vector2 _activeLandingNormalized;
    private int _throwGeneration;
    private float _lastRootWidth;
    private float _lastRootHeight;
    private int _targetValue;
    private DartMultiplier _targetMultiplier;

    private ThemeManager _subscribedThemeManager;

    public void Initialize(
        GameObject root,
        GameObject playingContainer,
        GameObject pausedContainer,
        Sprite dartSprite,
        Sprite boardSprite,
        Transform botIconRow = null,
        Image botIcon = null,
        GameObject playerCardPrefab = null,
        BotOverlayCardPlacement cardPlacement = BotOverlayCardPlacement.Top)
    {
        _root = root;
        _playingContainer = playingContainer;
        _pausedContainer = pausedContainer;
        _dartSprite = dartSprite;
        _boardSprite = boardSprite;
        _botIconRow = botIconRow;
        _botIconSource = botIcon;
        _playerCardPrefab = playerCardPrefab;
        _cardPlacement = cardPlacement;

        CaptureCompactRootState();
        EnsureBuilt();
    }

    public bool IsHitHoldActive => _hitHoldActive;

    public void SetAnimationPaused(bool paused)
    {
        _animationPaused = paused;
    }

    public void SetPlayerStats(object stats)
    {
        _playerStats = stats;

        if (_playerCardInstance == null || stats == null)
            return;

        switch (stats)
        {
            case X01UIStats x01Stats:
                if (_x01PlayerCard == null)
                    _x01PlayerCard = _playerCardInstance.GetComponent<X01PlayerItem>()
                        ?? _playerCardInstance.GetComponentInChildren<X01PlayerItem>(true);
                _x01PlayerCard?.Bind(x01Stats);
                break;
            case CricketUIStats cricketStats:
                if (_cricketPlayerCard == null)
                    _cricketPlayerCard = _playerCardInstance.GetComponent<CricketPlayerListItem>()
                        ?? _playerCardInstance.GetComponentInChildren<CricketPlayerListItem>(true);
                _cricketPlayerCard?.Bind(cricketStats);
                break;
            case ATCUIStats atcStats:
                if (_atcPlayerCard == null)
                    _atcPlayerCard = _playerCardInstance.GetComponent<ATCPlayerListItem>()
                        ?? _playerCardInstance.GetComponentInChildren<ATCPlayerListItem>(true);
                _atcPlayerCard?.Bind(atcStats);
                break;
        }
    }

    public void CancelHitHold()
    {
        _hitHoldActive = false;
    }

    public void ShowBot(DartBot bot)
    {
        EnsureBuilt();
        _currentBot = bot;

        if (_nameText != null)
            _nameText.text = bot != null ? bot.GetName() : "Bot";

        ResetVisuals();
        ApplyTheme();
    }

    public void ResetVisuals()
    {
        StopTargetLock(true);

        if (_dartImage != null)
            _dartImage.gameObject.SetActive(false);

        if (_targetCrosshair != null)
            _targetCrosshair.gameObject.SetActive(false);

        if (_targetReticleRoot != null)
            _targetReticleRoot.gameObject.SetActive(false);

        HideHitPopup();

        _hasTarget = false;
        _hitHoldActive = false;
        _dartPhase = DartPhase.None;
        _dartFlightProgress = 0f;
        _activeLandingNormalized = Vector2.zero;
        _throwGeneration++;
        _targetValue = -1;
        _targetMultiplier = DartMultiplier.Single;
    }

    public IEnumerator PlayThrow(Throw throwData, Action onImpact = null)
    {
        EnsureBuilt();
        int generation = ++_throwGeneration;
        UpdateTarget(throwData);

        float previewDuration = ScaledDuration(TargetPreviewDuration);
        float flightDuration = ScaledDuration(FlightDuration);
        float impactDuration = ScaledDuration(ImpactDuration);

        if (_dartImage == null || _dartSprite == null || _animationLayer == null)
        {
            yield return WaitAnimation(previewDuration + flightDuration + impactDuration);
            if (generation != _throwGeneration)
                yield break;

            StopTargetLock(true);
            yield return FinishThrow(throwData, onImpact);
            yield break;
        }

        yield return WaitAnimation(previewDuration);
        if (generation != _throwGeneration)
            yield break;

        StopTargetLock(true);

        Vector2 landingNormalized = GetBoardNormalizedPositionForThrow(throwData);
        _activeLandingNormalized = landingNormalized;
        float arcHeight = Mathf.Max(30f, _boardAreaSize * 0.12f);

        _dartImage.gameObject.SetActive(true);
        _dartRect.localScale = new Vector3(0.75f, 0.75f, 1f);
        _dartPhase = DartPhase.Flight;
        _dartFlightProgress = 0f;
        LogDartDiagnostics(throwData, GetLayerPositionForBoardNormalized(landingNormalized), "end");

        float elapsed = 0f;

        while (elapsed < flightDuration)
        {
            if (generation != _throwGeneration)
                yield break;

            if (_animationPaused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flightDuration);
            _dartFlightProgress = progress;
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            Vector2 start = GetLayerStartPosition();
            Vector2 end = GetLayerPositionForBoardNormalized(landingNormalized);
            Vector2 position = Vector2.LerpUnclamped(start, end, eased);
            position.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;

            _dartRect.anchoredPosition = position;
            _dartRect.localRotation = GetDartRotation(start, end);

            float scale = Mathf.Lerp(0.75f, 1f, eased);
            _dartRect.localScale = new Vector3(scale, scale, 1f);

            yield return null;
        }

        if (generation != _throwGeneration)
            yield break;

        _dartPhase = DartPhase.Impact;
        _dartFlightProgress = 1f;
        Vector2 impactStart = GetLayerStartPosition();
        Vector2 impactEnd = GetLayerPositionForBoardNormalized(landingNormalized);
        _dartRect.anchoredPosition = impactEnd;
        _dartRect.localRotation = GetDartRotation(impactStart, impactEnd);
        LogDartDiagnostics(throwData, impactEnd, "impact");

        float impactElapsed = 0f;
        while (impactElapsed < impactDuration)
        {
            if (generation != _throwGeneration)
                yield break;

            if (_animationPaused)
            {
                yield return null;
                continue;
            }

            impactElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(impactElapsed / impactDuration);
            float punch = 1f + 0.12f * Mathf.Sin(progress * Mathf.PI);
            _dartRect.localScale = new Vector3(punch, punch, 1f);

            impactStart = GetLayerStartPosition();
            impactEnd = GetLayerPositionForBoardNormalized(landingNormalized);
            _dartRect.anchoredPosition = impactEnd;
            _dartRect.localRotation = GetDartRotation(impactStart, impactEnd);

            yield return null;
        }

        if (generation != _throwGeneration)
            yield break;

        _dartRect.localScale = Vector3.one;

        _dartPhase = DartPhase.Hold;
        yield return FinishThrow(throwData, onImpact);
    }

    private IEnumerator FinishThrow(Throw throwData, Action onImpact)
    {
        ShowHit(throwData);
        _hitHoldActive = true;
        onImpact?.Invoke();
        yield return WaitAnimation(ScaledDuration(HitHoldDuration));
        _hitHoldActive = false;

        if (_dartRect != null)
            LogDartDiagnostics(throwData, _dartRect.anchoredPosition, "hold");
    }

    private IEnumerator WaitAnimation(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!_animationPaused)
                elapsed += Time.deltaTime;

            yield return null;
        }
    }

    private void StartTargetLock()
    {
        StopTargetLock(false);

        if (_targetCrosshair == null || _targetCrosshairGroup == null)
            return;

        _targetLockVersion++;
        _targetLockRoutine = StartCoroutine(TargetLockRoutine());
    }

    private void StopTargetLock(bool resetVisuals)
    {
        if (_targetLockRoutine != null)
            StopCoroutine(_targetLockRoutine);

        _targetLockRoutine = null;
        _targetLockVersion++;

        if (resetVisuals)
            ResetTargetLockVisuals();
    }

    private void ResetTargetLockVisuals()
    {
        if (_targetCrosshair != null)
            _targetCrosshair.localScale = Vector3.one;

        if (_targetCrosshairGroup != null)
            _targetCrosshairGroup.alpha = 1f;

        if (_targetReticleRoot != null)
        {
            _targetReticleRoot.anchoredPosition = Vector2.zero;
            _targetReticleRoot.localScale = Vector3.one;
        }

        if (_targetReticleGroup != null)
            _targetReticleGroup.alpha = 0f;
    }

    private IEnumerator TargetLockRoutine()
    {
        int version = _targetLockVersion;
        RectTransform marker = _targetCrosshair;
        CanvasGroup markerGroup = _targetCrosshairGroup;
        RectTransform reticle = _targetReticleRoot;
        CanvasGroup reticleGroup = _targetReticleGroup;

        if (marker == null || markerGroup == null)
        {
            _targetLockRoutine = null;
            yield break;
        }

        marker.localScale = Vector3.one * TargetLockSnapScale;
        markerGroup.alpha = 0f;

        Vector2 targetPosition = marker.anchoredPosition;
        float reticleStartScale = GetTargetReticleStartScale();

        if (reticle != null)
        {
            reticle.anchoredPosition = Vector2.zero;
            reticle.localScale = Vector3.one * reticleStartScale;
        }

        if (reticleGroup != null)
            reticleGroup.alpha = 0f;

        float snapDuration = Mathf.Max(0.01f, ScaledDuration(TargetLockSnapDuration));
        float snapElapsed = 0f;

        while (snapElapsed < snapDuration)
        {
            if (version != _targetLockVersion)
                yield break;

            if (!_animationPaused)
            {
                snapElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(snapElapsed / snapDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);

                marker.localScale = Vector3.one * Mathf.Lerp(TargetLockSnapScale, 1f, eased);
                markerGroup.alpha = eased;

                if (reticleGroup != null)
                    reticleGroup.alpha = TargetLockReticleMaxAlpha * eased;
            }

            yield return null;
        }

        marker.localScale = Vector3.one;
        markerGroup.alpha = 1f;

        if (reticle != null)
        {
            reticle.anchoredPosition = Vector2.zero;
            reticle.localScale = Vector3.one * reticleStartScale;
        }

        if (reticleGroup != null)
            reticleGroup.alpha = TargetLockReticleMaxAlpha;

        float zoomDuration = Mathf.Max(0.01f, ScaledDuration(TargetLockReticleZoomDuration));
        float zoomElapsed = 0f;

        while (zoomElapsed < zoomDuration)
        {
            if (version != _targetLockVersion)
                yield break;

            if (!_animationPaused)
            {
                zoomElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(zoomElapsed / zoomDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);

                if (reticle != null)
                {
                    reticle.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, targetPosition, eased);
                    float scale = Mathf.LerpUnclamped(reticleStartScale, 1f, eased);
                    reticle.localScale = Vector3.one * scale;
                }
            }

            yield return null;
        }

        if (reticle != null)
        {
            reticle.anchoredPosition = targetPosition;
            reticle.localScale = Vector3.one;
        }

        if (reticleGroup != null)
            reticleGroup.alpha = TargetLockReticleMaxAlpha;

        float pulseDuration = Mathf.Max(0.1f, ScaledDuration(TargetLockPulseDuration));
        float pulseElapsed = 0f;

        while (version == _targetLockVersion)
        {
            if (!_animationPaused)
            {
                pulseElapsed += Time.deltaTime;
                float phase = Mathf.Repeat(pulseElapsed / pulseDuration, 1f);
                float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);

                marker.localScale = Vector3.one * Mathf.Lerp(1f, TargetLockPulseScale, wave);
                markerGroup.alpha = Mathf.Lerp(1f, TargetLockCrosshairMinAlpha, wave);

                if (reticle != null)
                {
                    reticle.anchoredPosition = targetPosition;
                    float scale = Mathf.Lerp(1f, TargetLockReticlePulseScale, wave);
                    reticle.localScale = Vector3.one * scale;
                }

                if (reticleGroup != null)
                {
                    reticleGroup.alpha = Mathf.Lerp(
                        TargetLockReticleMaxAlpha,
                        TargetLockReticleMinAlpha,
                        wave);
                }
            }

            yield return null;
        }

        if (version == _targetLockVersion)
            _targetLockRoutine = null;
    }

    private float GetTargetReticleStartScale()
    {
        if (_boardAreaSize <= 0.001f)
            return 1f;

        float baseSize = _targetReticleBaseSize > 0.001f
            ? _targetReticleBaseSize
            : Mathf.Max(52f, _boardMarkerOutlineSize * 1.8f);

        return Mathf.Max(1f, (_boardAreaSize * TargetLockReticleBoardPadding) / baseSize);
    }

    private void LateUpdate()
    {
        if (!_built)
            return;

        DetectRootSizeChange();
        UpdateDartLayout();
        if (_hitPopupActive)
            RepositionHitPopup();
    }

    private void OnDisable()
    {
        StopTargetLock(true);
    }

    private void UpdateDartLayout()
    {
        if (_dartRect == null || _dartImage == null || !_dartImage.gameObject.activeSelf)
            return;

        if (_dartPhase == DartPhase.Flight)
        {
            if (!_animationPaused)
                return;

            float eased = 1f - Mathf.Pow(1f - _dartFlightProgress, 3f);
            Vector2 start = GetLayerStartPosition();
            Vector2 end = GetLayerPositionForBoardNormalized(_activeLandingNormalized);
            Vector2 position = Vector2.LerpUnclamped(start, end, eased);
            position.y += Mathf.Sin(_dartFlightProgress * Mathf.PI) * Mathf.Max(30f, _boardAreaSize * 0.12f);

            _dartRect.anchoredPosition = position;
            _dartRect.localRotation = GetDartRotation(start, end);

            float scale = Mathf.Lerp(0.75f, 1f, eased);
            _dartRect.localScale = new Vector3(scale, scale, 1f);
            return;
        }

        if (_dartPhase != DartPhase.Impact && _dartPhase != DartPhase.Hold)
            return;

        Vector2 impactStart = GetLayerStartPosition();
        Vector2 impactEnd = GetLayerPositionForBoardNormalized(_activeLandingNormalized);
        _dartRect.anchoredPosition = impactEnd;
        _dartRect.localRotation = GetDartRotation(impactStart, impactEnd);
    }

    private IEnumerator AnimateHitPopup()
    {
        float introDuration = Mathf.Max(0.01f, ScaledDuration(HitPopupDuration));
        float holdDuration = Mathf.Max(introDuration + 0.2f, ScaledDuration(HitHoldDuration));
        float fadeDuration = Mathf.Max(0.01f, ScaledDuration(HitPopupFadeDuration));
        float elapsed = 0f;

        ApplyHitPopupProgress(elapsed, introDuration, holdDuration, fadeDuration);

        while (elapsed < holdDuration)
        {
            if (!_animationPaused)
            {
                elapsed += Time.deltaTime;
                ApplyHitPopupProgress(elapsed, introDuration, holdDuration, fadeDuration);
            }

            yield return null;
        }

        _hitPopupActive = false;
        _hitPopupOffset = Vector2.zero;
        if (_hitPopupText != null)
            _hitPopupText.gameObject.SetActive(false);

        _hitPopupRoutine = null;
    }

    private void ApplyHitPopupProgress(float elapsed, float introDuration, float holdDuration, float fadeDuration)
    {
        if (_hitPopupText == null || _hitPopupGroup == null)
            return;

        float popInDuration = introDuration * 0.55f;
        float fadeInDuration = Mathf.Max(0.01f, introDuration * 0.25f);
        float fadeStart = Mathf.Max(introDuration, holdDuration - fadeDuration);
        float alpha = 1f;
        float scale;
        float lift;

        if (elapsed < popInDuration)
        {
            float t = Mathf.Clamp01(elapsed / popInDuration);
            float eased = EaseOutCubic(t);
            scale = Mathf.LerpUnclamped(0.45f, 1.18f, eased);
            alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            lift = Mathf.Lerp(-12f, 0f, eased);
        }
        else if (elapsed < introDuration)
        {
            float t = Mathf.Clamp01((elapsed - popInDuration) / Mathf.Max(0.001f, introDuration - popInDuration));
            scale = Mathf.Lerp(1.18f, 1f, Mathf.SmoothStep(0f, 1f, t));
            lift = 0f;
        }
        else
        {
            float breatheTime = elapsed - introDuration;
            scale = 1f + 0.015f * Mathf.Sin(breatheTime * Mathf.PI * 2f / 0.55f);

            float fadeT = Mathf.Clamp01((elapsed - fadeStart) / fadeDuration);
            alpha = 1f - Mathf.SmoothStep(0f, 1f, fadeT);
            scale = Mathf.Lerp(scale, 1.06f, fadeT);
            lift = Mathf.Lerp(0f, 8f, fadeT);
        }

        _hitPopupGroup.alpha = alpha;
        _hitPopupText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        _hitPopupOffset = new Vector2(0f, lift);
        RepositionHitPopup();
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    private void DetectRootSizeChange()
    {
        if (_root == null)
            return;

        var rootRect = (RectTransform)_root.transform;
        float width = rootRect.rect.width;
        float height = rootRect.rect.height;
        if (width <= 1f || height <= 1f)
            return;

        if (Mathf.Abs(width - _lastRootWidth) <= 1f && Mathf.Abs(height - _lastRootHeight) <= 1f)
            return;

        _lastRootWidth = width;
        _lastRootHeight = height;
        RebuildForMode(_overlayMode);
    }

    public void ShowTarget(Throw throwData)
    {
        EnsureBuilt();
        UpdateTarget(throwData);
    }

    private void UpdateTarget(Throw throwData)
    {
        _hasTarget = throwData != null && throwData.TargetValue > 0;
        _targetValue = _hasTarget ? throwData.TargetValue : -1;
        _targetMultiplier = throwData != null ? throwData.TargetMultiplier : DartMultiplier.Single;

        if (_targetCrosshair != null)
        {
            _targetCrosshair.gameObject.SetActive(_hasTarget);
            if (_hasTarget)
                _targetCrosshair.anchoredPosition = GetBoardPosition(_targetValue, _targetMultiplier);
        }

        if (_targetReticleRoot != null)
            _targetReticleRoot.gameObject.SetActive(_hasTarget);

        if (_hitPopupActive)
            HideHitPopup();

        if (_dartImage != null)
            _dartImage.gameObject.SetActive(false);

        _hitHoldActive = false;
        _dartPhase = DartPhase.None;
        _dartFlightProgress = 0f;
        _activeLandingNormalized = Vector2.zero;

        ApplyTheme();

        if (_hasTarget)
            StartTargetLock();
        else
            StopTargetLock(true);
    }

    private void ShowHit(Throw throwData)
    {
        if (_hitPopupText == null || _hitPopupGroup == null)
            EnsureHitPopup();

        if (_hitPopupText == null || _hitPopupGroup == null)
            return;

        if (_hitPopupRoutine != null)
        {
            StopCoroutine(_hitPopupRoutine);
            _hitPopupRoutine = null;
        }

        string actual = FormatActual(throwData);
        _hitPopupText.text = actual;
        _hitPopupText.color = GetColor(ThemeColorRole.Text1, Color.white);
        _hitPopupText.gameObject.SetActive(true);
        ApplyHitPopupOutline();
        _hitPopupText.rectTransform.localScale = new Vector3(0.45f, 0.45f, 1f);
        _hitPopupGroup.alpha = 0f;
        _hitPopupOffset = Vector2.zero;
        _hitPopupActive = true;
        RepositionHitPopup();
        _hitPopupRoutine = StartCoroutine(AnimateHitPopup());
    }

    private void HideHitPopup()
    {
        _hitPopupActive = false;
        _hitPopupOffset = Vector2.zero;

        if (_hitPopupRoutine != null)
        {
            StopCoroutine(_hitPopupRoutine);
            _hitPopupRoutine = null;
        }

        if (_hitPopupGroup != null)
            _hitPopupGroup.alpha = 0f;

        if (_hitPopupText != null)
        {
            _hitPopupText.text = string.Empty;
            _hitPopupText.gameObject.SetActive(false);
        }
    }

    private Vector2 GetBoardNormalizedPositionForThrow(Throw throwData)
    {
        if (_boardArea == null || _boardRadius <= 0.001f || throwData == null)
            return Vector2.zero;

        if (throwData.HasLandingPoint)
            return GetBoardPositionFromNormalized(throwData.LandingX, throwData.LandingY) / _boardRadius;

        bool isExactHit = _hasTarget
            && throwData.Value == _targetValue
            && throwData.Multiplier == _targetMultiplier
            && _targetCrosshair != null
            && _targetCrosshair.gameObject.activeInHierarchy;

        if (isExactHit)
            return _targetCrosshair.anchoredPosition / _boardRadius;

        Vector2 boardPosition;
        bool valid = throwData.Value > 0
            && (throwData.HitType == HitType.Board || throwData.HitType == HitType.Custom);

        if (valid)
        {
            boardPosition = GetBoardPosition(throwData.Value, throwData.Multiplier);
        }
        else
        {
            float angle = _hasTarget ? GetSegmentAngle(_targetValue) : UnityEngine.Random.Range(0f, 360f);
            float rad = angle * Mathf.Deg2Rad;
            boardPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (_boardRadius * 1.05f);
        }

        return boardPosition / _boardRadius;
    }

    private Vector2 GetLayerPositionForBoardNormalized(Vector2 normalized)
    {
        if (_boardArea == null || _animationLayer == null)
            return Vector2.zero;

        Vector3 world = _boardArea.TransformPoint(normalized * _boardRadius);
        return _animationLayer.InverseTransformPoint(world);
    }

    private Vector2 GetLayerStartPosition()
    {
        if (_animationLayer == null)
            return Vector2.zero;

        Rect layerRect = _animationLayer.rect;
        if (_card == null)
        {
            return new Vector2(
                layerRect.xMin + layerRect.width * 0.15f,
                layerRect.yMin + layerRect.height * 0.15f);
        }

        var corners = new Vector3[4];
        _card.GetWorldCorners(corners);
        Vector2 bottomLeft = _animationLayer.InverseTransformPoint(corners[0]);
        Vector2 topRight = _animationLayer.InverseTransformPoint(corners[2]);

        Vector2 start = new Vector2(
            Mathf.Lerp(bottomLeft.x, topRight.x, 0.15f),
            Mathf.Lerp(bottomLeft.y, topRight.y, 0.15f));

        start.x = Mathf.Clamp(start.x, layerRect.xMin, layerRect.xMax);
        start.y = Mathf.Clamp(start.y, layerRect.yMin, layerRect.yMax);
        return start;
    }

    private void RepositionHitPopup()
    {
        if (_hitPopupText == null || _animationLayer == null || _boardArea == null)
            return;

        Vector3 world = _boardArea.TransformPoint(Vector3.zero);
        Vector2 local = _animationLayer.InverseTransformPoint(world);
        _hitPopupText.rectTransform.anchoredPosition = local + _hitPopupOffset;
    }

    private Vector2 GetBoardPositionFromNormalized(float x, float y)
    {
        float radiusNormalized = Mathf.Sqrt(x * x + y * y);

        if (radiusNormalized < 0.0001f)
            return Vector2.zero;

        float radiusMm = radiusNormalized * DartboardGeometry.BoardRadiusMm;
        float visualRadius = PhysicalRadiusToVisualFraction(radiusMm) * _boardRadius;
        Vector2 direction = new Vector2(x, y) / radiusNormalized;

        return direction * visualRadius;
    }

    private Vector2 GetBoardPosition(int value, DartMultiplier multiplier)
    {
        float ringRadius = _boardRadius * GetVisualRingFraction(value, multiplier);

        if (value == 25)
            return new Vector2(0f, ringRadius);

        if (value <= 0)
            return Vector2.zero;

        float angle = GetSegmentAngle(value);
        float radians = angle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Sin(radians) * ringRadius, Mathf.Cos(radians) * ringRadius);
    }

    public static float GetVisualRingFraction(int value, DartMultiplier multiplier)
    {
        if (value == 25)
            return multiplier == DartMultiplier.Double ? 0f : OuterBullRingRadius;

        if (value <= 0)
            return 0f;

        return multiplier switch
        {
            DartMultiplier.Triple => TripleRingRadius,
            DartMultiplier.Double => DoubleRingRadius,
            _ => SingleOuterRingRadius
        };
    }

    public static float PhysicalRadiusToVisualFraction(float radiusMm)
    {
        if (radiusMm <= 0f)
            return 0f;

        if (radiusMm < PhysicalBullInnerMm)
            return LerpFraction(0f, VisualBullInnerRadius, radiusMm / PhysicalBullInnerMm);

        if (radiusMm < PhysicalOuterBullRadiusMm)
            return LerpFraction(VisualBullInnerRadius, OuterBullRingRadius,
                (radiusMm - PhysicalBullInnerMm) / (PhysicalOuterBullRadiusMm - PhysicalBullInnerMm));

        if (radiusMm < PhysicalOuterBullOuterMm)
            return LerpFraction(OuterBullRingRadius, VisualOuterBullRadius,
                (radiusMm - PhysicalOuterBullRadiusMm) / (PhysicalOuterBullOuterMm - PhysicalOuterBullRadiusMm));

        if (radiusMm < PhysicalTripleInnerMm)
            return LerpFraction(VisualOuterBullRadius, VisualTripleInnerRadius,
                (radiusMm - PhysicalOuterBullOuterMm) / (PhysicalTripleInnerMm - PhysicalOuterBullOuterMm));

        if (radiusMm < PhysicalTripleRadiusMm)
            return LerpFraction(VisualTripleInnerRadius, TripleRingRadius,
                (radiusMm - PhysicalTripleInnerMm) / (PhysicalTripleRadiusMm - PhysicalTripleInnerMm));

        if (radiusMm < PhysicalTripleOuterMm)
            return LerpFraction(TripleRingRadius, VisualTripleOuterRadius,
                (radiusMm - PhysicalTripleRadiusMm) / (PhysicalTripleOuterMm - PhysicalTripleRadiusMm));

        if (radiusMm < PhysicalSingleRadiusMm)
            return LerpFraction(VisualTripleOuterRadius, SingleOuterRingRadius,
                (radiusMm - PhysicalTripleOuterMm) / (PhysicalSingleRadiusMm - PhysicalTripleOuterMm));

        if (radiusMm < PhysicalDoubleInnerMm)
            return LerpFraction(SingleOuterRingRadius, VisualDoubleInnerRadius,
                (radiusMm - PhysicalSingleRadiusMm) / (PhysicalDoubleInnerMm - PhysicalSingleRadiusMm));

        if (radiusMm < PhysicalDoubleRadiusMm)
            return LerpFraction(VisualDoubleInnerRadius, DoubleRingRadius,
                (radiusMm - PhysicalDoubleInnerMm) / (PhysicalDoubleRadiusMm - PhysicalDoubleInnerMm));

        if (radiusMm < PhysicalDoubleOuterMm)
            return LerpFraction(DoubleRingRadius, VisualDoubleOuterRadius,
                (radiusMm - PhysicalDoubleRadiusMm) / (PhysicalDoubleOuterMm - PhysicalDoubleRadiusMm));

        if (radiusMm < PhysicalOffBoardMaxMm)
            return LerpFraction(VisualDoubleOuterRadius, VisualBoardOuterRadius,
                (radiusMm - PhysicalDoubleOuterMm) / (PhysicalOffBoardMaxMm - PhysicalDoubleOuterMm));

        return VisualBoardOuterRadius;
    }

    private static float LerpFraction(float from, float to, float t)
    {
        return from + (to - from) * Mathf.Clamp01(t);
    }

    private static float GetSegmentAngle(int value)
    {
        return DartboardGeometry.GetSegmentCenterAngle(value);
    }

    private static Quaternion GetDartRotation(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < 0.001f)
            return Quaternion.identity;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle + 135f);
    }

    private static Vector2 GetDartTipPivot(Sprite sprite)
    {
        if (sprite == null)
            return new Vector2(0.08f, 0.08f);

        Rect rect = sprite.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return new Vector2(0.08f, 0.08f);

        return new Vector2(
            Mathf.Clamp01((DartTipPixelX - rect.x) / rect.width),
            Mathf.Clamp01((DartTipPixelYFromBottom - rect.y) / rect.height));
    }

    private void LogDartDiagnostics(Throw throwData, Vector2 end, string phase)
    {
        if (_dartRect == null || _targetCrosshair == null || _dartSprite == null || _boardArea == null || _animationLayer == null)
            return;

        Canvas canvas = _card != null ? _card.GetComponentInParent<Canvas>() : null;
        Vector3 crosshairWorld = _targetCrosshair.position;
        Vector3 dartWorld = _animationLayer.TransformPoint(end);
        Vector3 delta = dartWorld - crosshairWorld;

        Debug.Log(
            $"[BotDart:{phase}] actual={throwData?.Value}/{throwData?.Multiplier} " +
            $"target={_targetValue}/{_targetMultiplier} hasTarget={_hasTarget} isHit={throwData?.IsTargetHit} " +
            $"crosshair={crosshairWorld} dartWorld={dartWorld} dartRect={_dartRect.position} delta={delta} " +
            $"endLocal={end} rotZ={_dartRect.localRotation.eulerAngles.z} dartPivot={_dartRect.pivot} " +
            $"spriteRect={_dartSprite.rect} textureRect={_dartSprite.textureRect} spritePivot={_dartSprite.pivot} " +
            $"boardRect={_boardArea.rect} boardPos={_boardArea.position} boardScale={_boardArea.lossyScale} " +
            $"layerRect={_animationLayer.rect} layerPos={_animationLayer.position} layerScale={_animationLayer.lossyScale} " +
            $"cardRect={_card?.rect} contentRect={_content?.rect} canvasScale={(canvas != null ? canvas.scaleFactor : -1f)} " +
            $"screen={Screen.width}x{Screen.height}");
    }

    private static string FormatValue(int value, DartMultiplier multiplier)
    {
        if (value <= 0)
            return LocalizationText.Get("Miss", "Miss");

        return multiplier switch
        {
            DartMultiplier.Double => "D" + value,
            DartMultiplier.Triple => "T" + value,
            _ => value.ToString()
        };
    }

    private static string FormatActual(Throw throwData)
    {
        if (throwData == null)
            return LocalizationText.Get("Miss", "Miss");

        bool onBoard = throwData.HitType == HitType.Board || throwData.HitType == HitType.Custom;
        if (!onBoard || throwData.Value <= 0)
            return LocalizationText.Get("Miss", "Miss");

        return FormatValue(throwData.Value, throwData.Multiplier);
    }

    public static void ResetSessionOverlaySuppression()
    {
        _sessionOverlaySuppressed = false;
    }

    private static bool IsOverlayEnabledInSettings()
    {
        AppSettingsManager manager = AppSettingsManager.Instance;
        if (manager == null || manager.Settings == null || manager.Settings.BotAnimation == null)
            return false;

        return manager.Settings.BotAnimation.FullscreenOverlay;
    }

    private static bool ShouldUseOverlay()
    {
        return IsOverlayEnabledInSettings() && !_sessionOverlaySuppressed;
    }

    private static float GetAnimationSpeed()
    {
        AppSettingsManager manager = AppSettingsManager.Instance;
        if (manager == null || manager.Settings == null || manager.Settings.BotAnimation == null)
            return 1f;

        float speed = manager.Settings.BotAnimation.AnimationSpeed;
        return Mathf.Clamp(float.IsNaN(speed) || float.IsInfinity(speed) ? 1f : speed, 0.5f, 2f);
    }

    private static float ScaledDuration(float duration)
    {
        return duration / GetAnimationSpeed();
    }

    private void CaptureCompactRootState()
    {
        if (_root == null)
            return;

        var rect = (RectTransform)_root.transform;
        _compactParent = _root.transform.parent;
        _compactSiblingIndex = _root.transform.GetSiblingIndex();
        _compactAnchorMin = rect.anchorMin;
        _compactAnchorMax = rect.anchorMax;
        _compactOffsetMin = rect.offsetMin;
        _compactOffsetMax = rect.offsetMax;
        _compactPivot = rect.pivot;
        _compactAnchoredPosition = rect.anchoredPosition;
        _compactLocalScale = rect.localScale;
        _compactLocalRotation = rect.localRotation;

        var image = _root.GetComponent<Image>();
        _compactRootRaycastTarget = image != null && image.raycastTarget;
    }

    private void ConfigureRootForMode(bool overlay)
    {
        if (_root == null)
            return;

        _rootImage = _root.GetComponent<Image>();
        if (_rootImage != null)
            _rootImage.raycastTarget = overlay ? true : _compactRootRaycastTarget;

        var rect = (RectTransform)_root.transform;

        if (overlay)
        {
            Transform overlayParent = FindParentScreen(_root.transform);
            if (overlayParent == null)
                overlayParent = _compactParent;

            if (overlayParent != null)
            {
                _root.transform.SetParent(overlayParent, false);
                _root.transform.SetAsLastSibling();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, -OverlayTopBarHeight);
        }
        else
        {
            if (_compactParent != null)
            {
                _root.transform.SetParent(_compactParent, false);
                int index = Mathf.Clamp(_compactSiblingIndex, 0, _compactParent.childCount - 1);
                _root.transform.SetSiblingIndex(index);
            }

            rect.anchorMin = _compactAnchorMin;
            rect.anchorMax = _compactAnchorMax;
            rect.offsetMin = _compactOffsetMin;
            rect.offsetMax = _compactOffsetMax;
            rect.pivot = _compactPivot;
            rect.anchoredPosition = _compactAnchoredPosition;
            rect.localScale = _compactLocalScale;
            rect.localRotation = _compactLocalRotation;
        }

        if (overlay)
        {
            var bindings = _root.GetComponents<ThemeColorBinding>();
            if (bindings.Length > 0)
            {
                _rootBindingStripped = true;
                for (int i = 0; i < bindings.Length; i++)
                    Destroy(bindings[i]);
            }
        }
        else if (_rootBindingStripped && _rootImage != null)
        {
            var binding = _root.AddComponent<ThemeColorBinding>();
            binding.SetRole(ThemeColorRole.Background3);
            _rootBindingStripped = false;
        }
    }

    private void RebuildForMode(bool overlay)
    {
        StopTargetLock(true);
        DetachPersistentControls();

        if (_generatedRoot != null)
        {
            _generatedRoot.SetActive(false);
            Destroy(_generatedRoot);
        }

        _generatedRoot = null;
        ResetGeneratedRefs();
        _built = false;
        _overlayMode = overlay;
        EnsureBuilt();
    }

    private void DetachPersistentControls()
    {
        MoveToRoot(_playingContainer);
        MoveToRoot(_pausedContainer);
        if (_botIcon != null)
            MoveToRoot(_botIcon.gameObject);
    }

    private void MoveToRoot(GameObject target)
    {
        if (target != null && _root != null && target.transform.parent != _root.transform)
            target.transform.SetParent(_root.transform, false);
    }

    private void ResetGeneratedRefs()
    {
        _card = null;
        _content = null;
        _infoColumn = null;
        _header = null;
        _cardImage = null;
        _cardBorder = null;
        _boardArea = null;
        _boardImage = null;
        _footer = null;
        _modeButton = null;
        _modeButtonImage = null;
        _modeButtonText = null;
        _nameText = null;
        _targetCrosshair = null;
        _targetCrosshairGroup = null;
        _targetCrosshairParts.Clear();
        _targetReticleRoot = null;
        _targetReticleGroup = null;
        _targetReticleParts.Clear();
        _targetReticleBaseSize = 0f;
        _targetLockRoutine = null;
        _playerCardInstance = null;
        _playerCardRect = null;
        _x01PlayerCard = null;
        _cricketPlayerCard = null;
        _atcPlayerCard = null;
        _overlayCardVisualWidth = 0f;
        _overlayCardVisualHeight = 0f;
        _overlayHeaderExtent = 0f;
    }

    private void EnsureBuilt()
    {
        if (_root == null)
            return;

        bool overlay = ShouldUseOverlay();
        if (_built)
        {
            if (_overlayMode != overlay)
                RebuildForMode(overlay);

            return;
        }

        _built = true;
        _overlayMode = overlay;
        ConfigureRootForMode(overlay);

        var layout = _root.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        CreateGeneratedRoot();

        BuildCard();
        BuildContent();
        if (_overlayMode)
            BuildOverlayHeader();
        else
            BuildInfoColumn();
        BuildBoard();
        BuildFooter();
        BuildAnimationLayer();
        BuildModeButton();

        ApplyTheme();
        SubscribeToTheme();
        RefreshCurrentState();

        var rootRect = (RectTransform)_root.transform;
        _lastRootWidth = rootRect.rect.width;
        _lastRootHeight = rootRect.rect.height;
    }

    private void RefreshCurrentState()
    {
        if (_nameText != null)
            _nameText.text = _currentBot != null ? _currentBot.GetName() : "Bot";

        if (_targetCrosshair != null)
        {
            _targetCrosshair.gameObject.SetActive(_hasTarget);
            if (_hasTarget)
                _targetCrosshair.anchoredPosition = GetBoardPosition(_targetValue, _targetMultiplier);
        }

        if (_targetReticleRoot != null)
            _targetReticleRoot.gameObject.SetActive(_hasTarget);

        if (_hasTarget)
            StartTargetLock();
        else
            StopTargetLock(true);

        UpdateDartLayout();
        RepositionHitPopup();
    }

    private void CreateGeneratedRoot()
    {
        if (_generatedRoot != null)
            return;

        var go = CreateUIObject("BotGeneratedUI", _root.transform);
        _generatedRoot = go;
        SetStretchRect((RectTransform)go.transform, 0f, 0f, 0f, 0f);
    }

    private void BuildCard()
    {
        if (_overlayMode)
        {
            var rootRect = (RectTransform)_root.transform;
            float rootWidth = rootRect.rect.width > 1f ? rootRect.rect.width : 1220f;
            float rootHeight = rootRect.rect.height > 1f ? rootRect.rect.height : 1400f;

            float cardWidth = Mathf.Max(320f, rootWidth - OverlayCardMargin * 2f);
            float cardHeight = Mathf.Max(600f, rootHeight - OverlayCardMargin * 2f);

            _cardBorder = CreateImage("BotCardBorder", _generatedRoot.transform, null, Color.white);
            SetRect(_cardBorder.rectTransform, new Vector2(cardWidth + CardBorderInset * 2f, cardHeight + CardBorderInset * 2f), Vector2.zero);

            _cardImage = CreateImage("BotCard", _generatedRoot.transform, null, Color.white);
            SetRect(_cardImage.rectTransform, new Vector2(cardWidth, cardHeight), Vector2.zero);
            _card = _cardImage.rectTransform;
            return;
        }

        float margin = GetCompactCardMargin();
        float borderOffset = margin - CardBorderInset;

        _cardBorder = CreateImage("BotCardBorder", _generatedRoot.transform, null, Color.white);
        SetStretchRect(_cardBorder.rectTransform, borderOffset, borderOffset, borderOffset, borderOffset);

        _cardImage = CreateImage("BotCard", _generatedRoot.transform, null, Color.white);
        SetStretchRect(_cardImage.rectTransform, margin, margin, margin, margin);
        _card = _cardImage.rectTransform;
    }

    private void BuildContent()
    {
        float padding = _overlayMode ? OverlayCardPadding : GetCompactCardPadding();
        float footerHeight = _overlayMode ? OverlayFooterHeight : GetCompactFooterHeight();
        float footerGap = _overlayMode ? OverlayFooterGap : GetCompactFooterGap();

        var contentGo = CreateUIObject("BotContent", _card);
        _content = (RectTransform)contentGo.transform;
        float horizontalPadding = _overlayMode ? padding : 0f;

        _content.anchorMin = Vector2.zero;
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 0.5f);
        _content.offsetMin = new Vector2(horizontalPadding, padding + footerHeight + footerGap);
        _content.offsetMax = new Vector2(-horizontalPadding, -padding);
        _content.localScale = Vector3.one;
        _content.localRotation = Quaternion.identity;
    }

    private void BuildOverlayHeader()
    {
        if (_playerCardPrefab != null)
            BuildOverlayPlayerCard();
        else
            BuildOverlayFallbackHeader();
    }

    private void BuildOverlayPlayerCard()
    {
        var cardGo = Instantiate(_playerCardPrefab, _content, false);
        cardGo.name = "BotPlayerCard";
        _playerCardInstance = cardGo;
        _playerCardRect = (RectTransform)cardGo.transform;

        float nativeWidth = Mathf.Max(1f, _playerCardRect.sizeDelta.x);
        float nativeHeight = Mathf.Max(1f, _playerCardRect.sizeDelta.y);
        if (nativeWidth < 10f)
            nativeWidth = Mathf.Max(1f, _playerCardRect.rect.width);
        if (nativeHeight < 10f)
            nativeHeight = Mathf.Max(1f, _playerCardRect.rect.height);

        Vector2 contentSize = GetContentSize();
        bool top = _cardPlacement == BotOverlayCardPlacement.Top;

        float maxWidth = top
            ? contentSize.x
            : contentSize.x * OverlayLeftCardMaxWidthFraction;
        float maxHeight = top
            ? Mathf.Max(120f, contentSize.y * 0.42f)
            : Mathf.Max(120f, contentSize.y);

        float scale = Mathf.Min(maxWidth / nativeWidth, maxHeight / nativeHeight);
        scale = Mathf.Max(0.05f, scale);

        _overlayCardVisualWidth = nativeWidth * scale;
        _overlayCardVisualHeight = nativeHeight * scale;

        _playerCardRect.localScale = new Vector3(scale, scale, 1f);
        _playerCardRect.localRotation = Quaternion.identity;

        if (top)
        {
            _playerCardRect.anchorMin = new Vector2(0.5f, 1f);
            _playerCardRect.anchorMax = new Vector2(0.5f, 1f);
            _playerCardRect.pivot = new Vector2(0.5f, 1f);
            _playerCardRect.anchoredPosition = Vector2.zero;
        }
        else
        {
            _playerCardRect.anchorMin = new Vector2(0f, 1f);
            _playerCardRect.anchorMax = new Vector2(0f, 1f);
            _playerCardRect.pivot = new Vector2(0f, 1f);
            _playerCardRect.anchoredPosition = Vector2.zero;
        }

        if (_botIconRow != null)
            _botIconRow.gameObject.SetActive(false);

        if (_botIcon != null)
            _botIcon.gameObject.SetActive(false);
        else if (_botIconSource != null)
            _botIconSource.gameObject.SetActive(false);

        _overlayHeaderExtent = _overlayCardVisualHeight;
        SetPlayerStats(_playerStats);
    }

    private void BuildOverlayFallbackHeader()
    {
        var headerGo = CreateUIObject("BotHeader", _content);
        _header = (RectTransform)headerGo.transform;
        _header.anchorMin = new Vector2(0f, 1f);
        _header.anchorMax = new Vector2(1f, 1f);
        _header.pivot = new Vector2(0.5f, 1f);
        _header.offsetMin = new Vector2(0f, -OverlayHeaderHeight);
        _header.offsetMax = Vector2.zero;
        _header.localScale = Vector3.one;
        _header.localRotation = Quaternion.identity;

        Transform botIcon = ResolveBotIconTransform(_root.transform);
        if (botIcon != null)
        {
            botIcon.SetParent(_header, false);

            var iconRect = (RectTransform)botIcon;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(OverlayIconSize, OverlayIconSize);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;

            _botIcon = botIcon.GetComponent<Image>();
            if (_botIcon != null)
                _botIcon.preserveAspect = true;

            var bindings = botIcon.GetComponents<ThemeColorBinding>();
            for (int i = 0; i < bindings.Length; i++)
                Destroy(bindings[i]);

            botIcon.gameObject.SetActive(true);
        }

        if (_botIconRow != null)
            Destroy(_botIconRow.gameObject);

        float textLeft = OverlayIconSize + OverlayHeaderTextGap;

        _nameText = CreateText("BotName", _header, Vector2.zero, new Vector2(100f, NameTextHeight), NameFontSize);
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.alignment = TextAlignmentOptions.Left;
        SetTopTextRect(_nameText.rectTransform, textLeft, 0f, NameTextHeight);
    }

    private static void SetTopTextRect(RectTransform rect, float left, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(0f, -top);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private Transform ResolveBotIconTransform(Transform fallbackParent)
    {
        if (_botIcon != null)
            return _botIcon.transform;

        if (_botIconSource != null)
            return _botIconSource.transform;

        if (_botIconRow != null)
        {
            Transform fromRow = FindChild(_botIconRow, "BotIcon");
            if (fromRow != null)
                return fromRow;
        }

        return fallbackParent != null ? FindChild(fallbackParent, "BotIcon") : null;
    }

    private void BuildInfoColumn()
    {
        var infoGo = CreateUIObject("BotInfoColumn", _content);
        var infoColumn = (RectTransform)infoGo.transform;
        _infoColumn = infoColumn;
        infoColumn.anchorMin = new Vector2(0f, 0f);
        infoColumn.anchorMax = new Vector2(InfoColumnWidth, 1f);
        infoColumn.pivot = new Vector2(0.5f, 0.5f);
        infoColumn.offsetMin = new Vector2(InfoLeftPadding, 0f);
        infoColumn.offsetMax = new Vector2(-ColumnGap, 0f);
        infoColumn.localScale = Vector3.one;
        infoColumn.localRotation = Quaternion.identity;

        var statsGo = CreateUIObject("BotStatsBlock", infoColumn);
        var stats = (RectTransform)statsGo.transform;
        stats.anchorMin = new Vector2(0f, 0.5f);
        stats.anchorMax = new Vector2(1f, 0.5f);
        stats.pivot = new Vector2(0.5f, 0.5f);
        stats.sizeDelta = new Vector2(0f, StatsBlockHeight);
        stats.anchoredPosition = Vector2.zero;

        float contentHeight = GetContentSize().y;
        float statsScale = contentHeight > 1f
            ? Mathf.Clamp(contentHeight / StatsBlockHeight, 0.5f, 1f)
            : 1f;
        stats.localScale = new Vector3(statsScale, statsScale, 1f);
        stats.localRotation = Quaternion.identity;

        _nameText = CreateText("BotName", stats, Vector2.zero, new Vector2(100f, NameTextHeight), NameFontSize);
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.alignment = TextAlignmentOptions.Center;
        var nameRect = _nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.offsetMin = new Vector2(0f, -NameTextHeight);
        nameRect.offsetMax = Vector2.zero;

        Transform iconRow = _botIconRow;
        if (iconRow == null)
            iconRow = FindChild(_root.transform, "BotIconRow");

        Transform botIcon = ResolveBotIconTransform(_root.transform);

        if (botIcon != null)
        {
            botIcon.SetParent(stats, false);

            var iconRect = (RectTransform)botIcon;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(BotIconSize, BotIconSize);
            iconRect.anchoredPosition = new Vector2(0f, -(NameTextHeight + NameIconGap));
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;

            _botIcon = botIcon.GetComponent<Image>();
            if (_botIcon != null)
                _botIcon.preserveAspect = true;

            var bindings = botIcon.GetComponents<ThemeColorBinding>();
            for (int i = 0; i < bindings.Length; i++)
                Destroy(bindings[i]);

            botIcon.gameObject.SetActive(true);
        }

        if (iconRow != null)
            Destroy(iconRow.gameObject);
    }

    private Vector2 GetContentSize()
    {
        float rootWidth = GetRootWidth();
        float rootHeight = GetRootHeight();

        if (_overlayMode)
        {
            float cardWidth = Mathf.Max(320f, rootWidth - OverlayCardMargin * 2f);
            float cardHeight = Mathf.Max(600f, rootHeight - OverlayCardMargin * 2f);
            float width = Mathf.Max(1f, cardWidth - OverlayCardPadding * 2f);
            float height = Mathf.Max(1f, cardHeight - OverlayCardPadding * 2f
                - OverlayFooterHeight - OverlayFooterGap);
            return new Vector2(width, height);
        }

        float margin = GetCompactCardMargin();
        float padding = GetCompactCardPadding();
        float footerHeight = GetCompactFooterHeight();
        float footerGap = GetCompactFooterGap();
        float compactWidth = Mathf.Max(200f, rootWidth - margin * 2f);
        float compactHeight = Mathf.Max(200f, rootHeight - margin * 2f);
        return new Vector2(
            compactWidth,
            Mathf.Max(1f, compactHeight - padding * 2f - footerHeight - footerGap));
    }

    private float GetRootWidth()
    {
        if (_root == null)
            return 1220f;

        var rect = (RectTransform)_root.transform;
        return rect.rect.width > 1f ? rect.rect.width : 1220f;
    }

    private float GetRootHeight()
    {
        if (_root == null)
            return _overlayMode ? 1400f : 850f;

        var rect = (RectTransform)_root.transform;
        if (rect.rect.height > 1f)
            return rect.rect.height;

        return _overlayMode ? 1400f : 850f;
    }

    private bool IsTightCompactLayout()
    {
        return !_overlayMode && GetRootHeight() < TightLayoutHeight;
    }

    private float GetCompactCardMargin()
    {
        return IsTightCompactLayout() ? TightCardMargin : CardMargin;
    }

    private float GetCompactCardPadding()
    {
        return IsTightCompactLayout() ? TightCardPadding : CardPadding;
    }

    private float GetCompactFooterHeight()
    {
        return IsTightCompactLayout() ? TightFooterHeight : FooterHeight;
    }

    private float GetCompactFooterGap()
    {
        return IsTightCompactLayout() ? TightFooterGap : FooterGap;
    }

    private void BuildBoard()
    {
        Vector2 contentSize = GetContentSize();
        float overlayAvailableHeight = 0f;

        if (_overlayMode)
        {
            if (_cardPlacement == BotOverlayCardPlacement.Top)
            {
                overlayAvailableHeight = contentSize.y - _overlayHeaderExtent - OverlayHeaderGap;
                _boardAreaSize = Mathf.Max(120f, Mathf.Min(contentSize.x, overlayAvailableHeight));
            }
            else
            {
                overlayAvailableHeight = contentSize.y;
                float boardWidth = contentSize.x - _overlayCardVisualWidth - OverlayHeaderGap;
                _boardAreaSize = Mathf.Max(120f, Mathf.Min(contentSize.y, boardWidth));
            }

            _boardSize = _boardAreaSize;
        }
        else
        {
            float boardWidth = contentSize.x * (1f - InfoColumnWidth) + ColumnGap - RightPadding;
            _boardAreaSize = Mathf.Max(120f, Mathf.Min(contentSize.y, boardWidth));
            _boardSize = _boardAreaSize;
        }

        float visualScale = _boardSize / ReferenceBoardSize;
        _boardRadius = BaseBoardRadius * visualScale;
        _labelRadius = BaseLabelRadius * visualScale;
        _boardMarkerSize = BaseBoardMarkerSize * visualScale;
        _boardMarkerOutlineSize = BaseBoardMarkerOutlineSize * visualScale;
        _boardMarkerThickness = Mathf.Max(2f, BaseBoardMarkerThickness * visualScale);
        _boardMarkerOutlineThickness = Mathf.Max(3f, BaseBoardMarkerOutlineThickness * visualScale);
        _dartSize = BaseDartSize * visualScale;

        var areaGo = CreateUIObject("BotBoardArea", _content);
        _boardArea = (RectTransform)areaGo.transform;
        _boardArea.anchorMin = new Vector2(1f, 0.5f);
        _boardArea.anchorMax = new Vector2(1f, 0.5f);
        _boardArea.pivot = new Vector2(0.5f, 0.5f);
        _boardArea.sizeDelta = new Vector2(_boardAreaSize, _boardAreaSize);
        _boardArea.anchoredPosition = new Vector2(-(RightPadding + _boardAreaSize * 0.5f), 0f);

        if (_overlayMode)
        {
            if (_cardPlacement == BotOverlayCardPlacement.Top)
            {
                _boardArea.anchorMin = new Vector2(0.5f, 0f);
                _boardArea.anchorMax = new Vector2(0.5f, 0f);
                _boardArea.pivot = new Vector2(0.5f, 0.5f);
                _boardArea.anchoredPosition = new Vector2(
                    0f,
                    Mathf.Max(_boardAreaSize * 0.5f, overlayAvailableHeight * 0.5f));
            }
            else
            {
                _boardArea.anchorMin = new Vector2(1f, 0.5f);
                _boardArea.anchorMax = new Vector2(1f, 0.5f);
                _boardArea.pivot = new Vector2(0.5f, 0.5f);
                _boardArea.anchoredPosition = new Vector2(-_boardAreaSize * 0.5f, 0f);
            }
        }

        _boardArea.localScale = Vector3.one;
        _boardArea.localRotation = Quaternion.identity;

        _boardImage = CreateImage("BotBoard", _boardArea, _boardSprite, Color.white);
        SetRect(_boardImage.rectTransform, new Vector2(_boardSize, _boardSize), Vector2.zero);
        _boardImage.preserveAspect = true;

        int labelFontSize = Mathf.Max(11, Mathf.RoundToInt(18f * visualScale));
        Vector2 labelSize = new Vector2(
            Mathf.Max(30f, 48f * visualScale),
            Mathf.Max(16f, 26f * visualScale));

        for (int i = 0; i < DartboardGeometry.NumberOrder.Length; i++)
        {
            int number = DartboardGeometry.NumberOrder[i];
            float angle = GetSegmentAngle(number);
            float radians = angle * Mathf.Deg2Rad;

            var label = CreateText("BoardNumber_" + number, _boardArea, Vector2.zero, labelSize, labelFontSize);
            label.text = number.ToString();
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = NumberColor;

            var rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(
                Mathf.Sin(radians) * _labelRadius,
                Mathf.Cos(radians) * _labelRadius);
            rect.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        _targetCrosshair = (RectTransform)CreateUIObject("BotTargetMarker", _boardArea).transform;
        SetRect(_targetCrosshair, new Vector2(_boardMarkerSize, _boardMarkerSize), Vector2.zero);
        _targetCrosshair.gameObject.SetActive(false);

        var markerHShadow = CreateImage("MarkerHShadow", _targetCrosshair.transform, null, MarkerOutlineColor);
        SetRect(markerHShadow.rectTransform, new Vector2(_boardMarkerOutlineSize, _boardMarkerOutlineThickness), Vector2.zero);

        var markerVShadow = CreateImage("MarkerVShadow", _targetCrosshair.transform, null, MarkerOutlineColor);
        SetRect(markerVShadow.rectTransform, new Vector2(_boardMarkerOutlineThickness, _boardMarkerOutlineSize), Vector2.zero);

        var markerH = CreateImage("MarkerH", _targetCrosshair.transform, null, MarkerColor);
        SetRect(markerH.rectTransform, new Vector2(_boardMarkerSize, _boardMarkerThickness), Vector2.zero);

        var markerV = CreateImage("MarkerV", _targetCrosshair.transform, null, MarkerColor);
        SetRect(markerV.rectTransform, new Vector2(_boardMarkerThickness, _boardMarkerSize), Vector2.zero);

        _targetCrosshairParts.Clear();
        _targetCrosshairParts.Add(markerH);
        _targetCrosshairParts.Add(markerV);

        _targetCrosshairGroup = _targetCrosshair.gameObject.AddComponent<CanvasGroup>();
        _targetCrosshairGroup.interactable = false;
        _targetCrosshairGroup.blocksRaycasts = false;
        _targetCrosshairGroup.alpha = 1f;

        BuildTargetReticle();
    }

    private void BuildTargetReticle()
    {
        var reticleGo = CreateUIObject("BotTargetReticle", _boardArea);
        _targetReticleRoot = (RectTransform)reticleGo.transform;
        SetRect(_targetReticleRoot, Vector2.zero, Vector2.zero);
        reticleGo.SetActive(false);

        _targetReticleGroup = reticleGo.AddComponent<CanvasGroup>();
        _targetReticleGroup.interactable = false;
        _targetReticleGroup.blocksRaycasts = false;
        _targetReticleGroup.alpha = 0f;

        float reticleSize = Mathf.Max(52f, _boardMarkerOutlineSize * 1.8f);
        _targetReticleBaseSize = reticleSize;
        float armLength = Mathf.Max(10f, reticleSize * 0.22f);
        float thickness = Mathf.Max(2.5f, _boardMarkerThickness * 0.85f);
        float outline = Mathf.Max(2f, thickness * 0.6f);
        float half = reticleSize * 0.5f;

        _targetReticleParts.Clear();

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                var horizontalShadow = CreateImage("ReticleHShadow", _targetReticleRoot, null, MarkerOutlineColor);
                SetRect(horizontalShadow.rectTransform,
                    new Vector2(armLength + outline * 2f, thickness + outline * 2f),
                    new Vector2(x * (half - armLength * 0.5f), y * (half - thickness * 0.5f)));

                var verticalShadow = CreateImage("ReticleVShadow", _targetReticleRoot, null, MarkerOutlineColor);
                SetRect(verticalShadow.rectTransform,
                    new Vector2(thickness + outline * 2f, armLength + outline * 2f),
                    new Vector2(x * (half - thickness * 0.5f), y * (half - armLength * 0.5f)));
            }
        }

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                var horizontal = CreateImage("ReticleH", _targetReticleRoot, null, MarkerColor);
                SetRect(horizontal.rectTransform,
                    new Vector2(armLength, thickness),
                    new Vector2(x * (half - armLength * 0.5f), y * (half - thickness * 0.5f)));
                _targetReticleParts.Add(horizontal);

                var vertical = CreateImage("ReticleV", _targetReticleRoot, null, MarkerColor);
                SetRect(vertical.rectTransform,
                    new Vector2(thickness, armLength),
                    new Vector2(x * (half - thickness * 0.5f), y * (half - armLength * 0.5f)));
                _targetReticleParts.Add(vertical);
            }
        }
    }

    private void BuildFooter()
    {
        float footerWidth = _overlayMode ? OverlayFooterWidth : FooterWidth;
        float footerHeight = _overlayMode ? OverlayFooterHeight : GetCompactFooterHeight();
        float footerPadding = _overlayMode ? OverlayCardPadding : GetCompactCardPadding();

        var footerGo = CreateUIObject("BotFooter", _card);
        _footer = (RectTransform)footerGo.transform;
        _footer.anchorMin = new Vector2(0.5f, 0f);
        _footer.anchorMax = new Vector2(0.5f, 0f);
        _footer.pivot = new Vector2(0.5f, 0f);
        _footer.sizeDelta = new Vector2(footerWidth, footerHeight);
        _footer.anchoredPosition = new Vector2(0f, footerPadding);
        _footer.localScale = Vector3.one;
        _footer.localRotation = Quaternion.identity;

        float buttonSize = Mathf.Clamp(footerHeight - 8f, 76f, 110f);

        if (_playingContainer != null)
        {
            _playingContainer.transform.SetParent(_footer, false);
            var rect = (RectTransform)_playingContainer.transform;
            SetRect(rect, new Vector2(footerWidth, footerHeight), Vector2.zero);
            PositionButtons(_playingContainer, false, buttonSize);
        }

        if (_pausedContainer != null)
        {
            _pausedContainer.transform.SetParent(_footer, false);
            var rect = (RectTransform)_pausedContainer.transform;
            SetRect(rect, new Vector2(footerWidth, footerHeight), Vector2.zero);
            PositionButtons(_pausedContainer, true, buttonSize);
        }
    }

    private void BuildAnimationLayer()
    {
        if (_animationLayer == null)
        {
            var layerGo = CreateUIObject("BotAnimationLayer", _root.transform);
            _animationLayer = (RectTransform)layerGo.transform;
        }
        else
        {
            _animationLayer.SetParent(_root.transform, false);
        }

        SetStretchRect(_animationLayer, 0f, 0f, 0f, 0f);
        _animationLayer.SetAsLastSibling();

        EnsureDart();
        EnsureHitPopup();
        ApplyAnimationGeometry();

        if (_hitPopupText != null)
            _hitPopupText.transform.SetAsLastSibling();
    }

    private void EnsureDart()
    {
        if (_dartSprite == null)
            return;

        if (_dartImage == null)
        {
            _dartImage = CreateImage("BotDart", _animationLayer, _dartSprite, GetColor(ThemeColorRole.Text1, Color.white));
            _dartRect = _dartImage.rectTransform;
            _dartRect.anchorMin = new Vector2(0.5f, 0.5f);
            _dartRect.anchorMax = new Vector2(0.5f, 0.5f);
            _dartRect.pivot = GetDartTipPivot(_dartSprite);
            _dartRect.anchoredPosition = Vector2.zero;
            _dartImage.preserveAspect = true;
            _dartImage.raycastTarget = false;

            _dartOutline = _dartImage.gameObject.AddComponent<Outline>();
            _dartOutline.useGraphicAlpha = true;
            ApplyDartTheme();

            _dartImage.gameObject.SetActive(false);
        }
        else
        {
            _dartImage.sprite = _dartSprite;
        }

        _dartRect.pivot = GetDartTipPivot(_dartSprite);
    }

    private void EnsureHitPopup()
    {
        if (_animationLayer == null)
            return;

        if (_hitPopupText != null)
        {
            if (_hitPopupGroup == null)
                _hitPopupGroup = _hitPopupText.gameObject.AddComponent<CanvasGroup>();

            return;
        }

        _hitPopupText = CreateText("BotHitPopup", _animationLayer, Vector2.zero,
            new Vector2(100f, 100f), InfoFontSize);
        _hitPopupText.fontStyle = FontStyles.Bold;
        _hitPopupText.alignment = TextAlignmentOptions.Center;
        _hitPopupText.textWrappingMode = TextWrappingModes.NoWrap;
        _hitPopupText.overflowMode = TextOverflowModes.Overflow;
        _hitPopupText.color = GetColor(ThemeColorRole.Text1, Color.white);

        _hitPopupGroup = _hitPopupText.gameObject.AddComponent<CanvasGroup>();
        _hitPopupGroup.alpha = 0f;
        _hitPopupGroup.interactable = false;
        _hitPopupGroup.blocksRaycasts = false;

        ApplyHitPopupOutline();
        _hitPopupText.gameObject.SetActive(false);
    }

    private void ApplyHitPopupOutline()
    {
        if (_hitPopupText == null)
            return;

        Material material = _hitPopupText.fontMaterial;
        if (material == null)
            return;

        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        _hitPopupText.outlineWidth = HitPopupOutlineWidth;
        _hitPopupText.outlineColor = Color.black;
    }

    private void ApplyAnimationGeometry()
    {
        if (_dartRect != null)
            _dartRect.sizeDelta = new Vector2(_dartSize, _dartSize);

        if (_dartOutline != null)
            _dartOutline.effectDistance = Vector2.one * Mathf.Clamp(_dartSize * 0.01f, 1f, 2f);

        if (_hitPopupText == null)
            return;

        float popupWidth = Mathf.Max(120f, _boardAreaSize * 1.12f);
        float popupHeight = Mathf.Max(70f, _boardAreaSize * 0.42f);
        _hitPopupText.rectTransform.sizeDelta = new Vector2(popupWidth, popupHeight);
        _hitPopupText.enableAutoSizing = true;
        _hitPopupText.fontSizeMin = Mathf.Max(40f, _boardAreaSize * 0.07f);
        _hitPopupText.fontSizeMax = Mathf.Max(72f, _boardAreaSize * 0.20f);
        _hitPopupText.fontSize = _hitPopupText.fontSizeMax;
        _hitPopupText.margin = new Vector4(8f, 0f, 8f, 0f);

        ApplyHitPopupOutline();
        RepositionHitPopup();
    }

    private void BuildModeButton()
    {
        bool show = _overlayMode || (_sessionOverlaySuppressed && IsOverlayEnabledInSettings());
        if (!show || _card == null)
            return;

        float padding = _overlayMode ? OverlayCardPadding : GetCompactCardPadding();
        var go = CreateUIObject("BotModeButton", _card);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(ModeButtonWidth, ModeButtonHeight);
        rect.anchoredPosition = new Vector2(-padding, padding);

        _modeButtonImage = go.AddComponent<Image>();
        _modeButtonImage.raycastTarget = true;

        _modeButton = go.AddComponent<Button>();
        _modeButton.targetGraphic = _modeButtonImage;
        _modeButton.onClick.AddListener(OnModeButtonClicked);

        string key = _overlayMode ? "Minimize" : "Fullscreen";
        _modeButtonText = CreateText("Label", go.transform, Vector2.zero,
            new Vector2(ModeButtonWidth - 24f, ModeButtonHeight), ModeButtonFontSize);
        _modeButtonText.text = LocalizationText.Get(key, key);
        _modeButtonText.fontStyle = FontStyles.Bold;
        _modeButtonText.alignment = TextAlignmentOptions.Center;
        SetStretchRect(_modeButtonText.rectTransform, 12f, 4f, 12f, 4f);
    }

    private void OnModeButtonClicked()
    {
        _sessionOverlaySuppressed = _overlayMode;

        if (_modeButton != null)
            _modeButton.interactable = false;

        EnsureBuilt();
    }

    private void PositionButtons(GameObject container, bool twoButtons, float buttonSize)
    {
        var buttons = container.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            var rect = (RectTransform)button.transform;
            SetRect(rect, new Vector2(buttonSize, buttonSize), Vector2.zero);

            var buttonImage = button.GetComponent<Image>();
            var images = button.GetComponentsInChildren<Image>(true);
            for (int j = 0; j < images.Length; j++)
            {
                if (images[j] != buttonImage && images[j].sprite != null)
                    images[j].preserveAspect = true;
            }
        }

        if (!twoButtons)
        {
            if (buttons.Length > 0)
                ((RectTransform)buttons[0].transform).anchoredPosition = Vector2.zero;
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            var rect = (RectTransform)buttons[i].transform;
            string buttonName = buttons[i].name;

            if (buttonName.IndexOf("Play", StringComparison.OrdinalIgnoreCase) >= 0)
                rect.anchoredPosition = new Vector2(-70f, 0f);
            else if (buttonName.IndexOf("Undo", StringComparison.OrdinalIgnoreCase) >= 0)
                rect.anchoredPosition = new Vector2(70f, 0f);
            else
                rect.anchoredPosition = new Vector2(i == 0 ? -70f : 70f, 0f);
        }
    }

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        SetRect(rect, size, position);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.color = Color.white;
        return text;
    }

    private TMP_FontAsset GetFont()
    {
        if (_font != null)
            return _font;

        _font = TMP_Settings.defaultFontAsset;
        if (_font != null)
            return _font;

        var texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
            {
                _font = texts[i].font;
                break;
            }
        }

        return _font;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return go;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        var go = CreateUIObject(objectName, parent);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetStretchRect(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static Transform FindParentScreen(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            UIScreen screen = current.GetComponent<UIScreen>();
            if (screen != null)
                return screen.transform;

            current = current.parent;
        }

        return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform nested = FindChild(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void SubscribeToTheme()
    {
        ThemeManager manager = ThemeManager.Instance;
        if (manager == null || _subscribedThemeManager == manager)
            return;

        if (_subscribedThemeManager != null)
            _subscribedThemeManager.OnThemeChanged -= ApplyTheme;

        _subscribedThemeManager = manager;
        _subscribedThemeManager.OnThemeChanged += ApplyTheme;
    }

    private void ApplyTheme()
    {
        if (!_built)
            return;

        if (_overlayMode && _rootImage != null)
        {
            Color backdrop = GetColor(ThemeColorRole.Background1, Color.black);
            backdrop.a = 0.88f;
            _rootImage.color = backdrop;
        }

        if (_cardImage != null)
            _cardImage.color = GetColor(ThemeColorRole.Background2, new Color(0.12f, 0.12f, 0.12f, 1f));

        if (_cardBorder != null)
        {
            var borderColor = GetColor(ThemeColorRole.Accent1, Color.gray);
            borderColor.a = 0.55f;
            _cardBorder.color = borderColor;
        }

        if (_nameText != null)
            _nameText.color = GetColor(ThemeColorRole.Text1, Color.white);

        if (_hitPopupText != null)
        {
            _hitPopupText.color = GetColor(ThemeColorRole.Text1, Color.white);
            ApplyHitPopupOutline();
        }

        for (int i = 0; i < _targetCrosshairParts.Count; i++)
        {
            if (_targetCrosshairParts[i] != null)
                _targetCrosshairParts[i].color = MarkerColor;
        }

        for (int i = 0; i < _targetReticleParts.Count; i++)
        {
            if (_targetReticleParts[i] != null)
                _targetReticleParts[i].color = MarkerColor;
        }

        if (_botIcon != null)
        {
            _botIcon.color = _currentBot != null && ThemeManager.Instance != null
                ? ThemeManager.Instance.GetBotSkillColor(_currentBot.GetSkillRating())
                : GetColor(ThemeColorRole.Text1, Color.white);
        }

        if (_modeButtonImage != null)
            _modeButtonImage.color = GetColor(ThemeColorRole.Accent1, Color.gray);

        if (_modeButtonText != null)
            _modeButtonText.color = GetColor(ThemeColorRole.TextOnAccent1, Color.white);

        ApplyDartTheme();
    }

    private void ApplyDartTheme()
    {
        if (_dartImage != null)
            _dartImage.color = GetColor(ThemeColorRole.Text1, Color.white);

        if (_dartOutline != null)
        {
            Color outlineColor = GetColor(ThemeColorRole.Background1, Color.black);
            outlineColor.a = 0.9f;
            _dartOutline.effectColor = outlineColor;
        }
    }

    private static Color GetColor(ThemeColorRole role, Color fallback)
    {
        return ThemeManager.Instance != null
            ? ThemeManager.Instance.GetColor(role)
            : fallback;
    }

    private void OnDestroy()
    {
        if (_subscribedThemeManager != null)
            _subscribedThemeManager.OnThemeChanged -= ApplyTheme;
    }
}
