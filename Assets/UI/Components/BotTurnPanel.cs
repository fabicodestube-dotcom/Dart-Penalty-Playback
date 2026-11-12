using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BotTurnPanel : MonoBehaviour
{
    private const float CardMargin = 24f;
    private const float CardBorderInset = 4f;
    private const float CardPadding = 24f;
    private const float InfoLeftPadding = 50f;
    private const float ColumnGap = 24f;
    private const float InfoColumnWidth = 0.55f;
    private const float FooterGap = 12f;
    private const float FooterWidth = 360f;
    private const float FooterHeight = 116f;

    private const float ReferenceBoardSize = 408f;
    private const float MaxBoardSize = 440f;
    private const float MaxBoardAreaSize = 492f;
    private const float BaseBoardRadius = 192f;
    private const float BaseLabelRadius = 178f;
    private const float RightPadding = 40f;

    private const float BotIconSize = 200f;
    private const float NameFontSize = 50f;
    private const float NameTextHeight = 60f;
    private const float NameIconGap = 6f;
    private const float IconTargetGap = 10f;
    private const float InfoFontSize = 45f;
    private const float InfoTextHeight = 54f;
    private const float InfoTextGap = 6f;
    private const float TargetBlockHeight = InfoTextHeight + InfoTextGap + InfoTextHeight;
    private const float StatsBlockHeight = NameTextHeight + NameIconGap + BotIconSize + IconTargetGap + TargetBlockHeight;

    private const float BaseBoardMarkerSize = 34f;
    private const float BaseBoardMarkerOutlineSize = 40f;
    private const float BaseBoardMarkerThickness = 3f;
    private const float BaseBoardMarkerOutlineThickness = 7f;
    private const float BaseDartSize = 180f;
    private const float DartTipPixelX = 38f;
    private const float DartTipPixelYFromBottom = 38f;

    private const float TargetPreviewDuration = 0.8f;
    private const float FlightDuration = 0.45f;
    private const float ImpactDuration = 0.12f;
    private const float HitHoldDuration = 1f;

    private static readonly Color NumberColor = new Color(0.96f, 0.96f, 0.96f, 1f);
    private static readonly Color MarkerColor = new Color(1f, 1f, 1f, 0.98f);
    private static readonly Color MarkerOutlineColor = new Color(0f, 0f, 0f, 0.72f);

    private const float SingleOuterRingRadius = 0.618f;
    private const float TripleRingRadius = 0.4335f;
    private const float DoubleRingRadius = 0.7875f;

    private GameObject _root;
    private GameObject _playingContainer;
    private GameObject _pausedContainer;
    private Sprite _dartSprite;
    private Sprite _boardSprite;
    private Transform _botIconRow;
    private Image _botIconSource;

    private bool _built;

    private RectTransform _card;
    private RectTransform _content;
    private RectTransform _infoColumn;
    private Image _cardImage;
    private Image _cardBorder;
    private RectTransform _boardArea;
    private Image _boardImage;
    private RectTransform _footer;
    private RectTransform _animationLayer;

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
    private TMP_Text _targetText;
    private TMP_Text _hitText;
    private RectTransform _targetCrosshair;
    private readonly List<Image> _targetCrosshairParts = new List<Image>();
    private Image _dartImage;
    private Outline _dartOutline;
    private RectTransform _dartRect;

    private TMP_FontAsset _font;

    private DartBot _currentBot;
    private bool _hasTarget;
    private bool _hitHoldActive;
    private int _targetValue;
    private DartMultiplier _targetMultiplier;

    private ThemeManager _subscribedThemeManager;

    public void Initialize(GameObject root, GameObject playingContainer, GameObject pausedContainer, Sprite dartSprite, Sprite boardSprite, Transform botIconRow = null, Image botIcon = null)
    {
        _root = root;
        _playingContainer = playingContainer;
        _pausedContainer = pausedContainer;
        _dartSprite = dartSprite;
        _boardSprite = boardSprite;
        _botIconRow = botIconRow;
        _botIconSource = botIcon;

        EnsureBuilt();
    }

    public bool IsHitHoldActive => _hitHoldActive;

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
        if (_dartImage != null)
            _dartImage.gameObject.SetActive(false);

        if (_targetCrosshair != null)
            _targetCrosshair.gameObject.SetActive(false);

        if (_targetText != null)
        {
            _targetText.text = GetTargetLabel() + "-";
            _targetText.color = GetColor(ThemeColorRole.Text1, Color.white);
        }

        if (_hitText != null)
        {
            _hitText.text = string.Empty;
            _hitText.gameObject.SetActive(false);
        }

        _hasTarget = false;
        _hitHoldActive = false;
        _targetValue = -1;
        _targetMultiplier = DartMultiplier.Single;

        if (_boardImage != null)
            _boardImage.rectTransform.localScale = Vector3.one;
    }

    public IEnumerator PlayThrow(Throw throwData, Action onImpact = null)
    {
        EnsureBuilt();

        UpdateTarget(throwData);

        if (_dartImage == null || _dartSprite == null)
        {
            yield return new WaitForSeconds(TargetPreviewDuration + FlightDuration + ImpactDuration);
            yield return FinishThrow(throwData, onImpact);
            yield break;
        }

        yield return new WaitForSeconds(TargetPreviewDuration);

        Vector2 end = GetCardPositionForThrow(throwData);
        Rect layerRect = _animationLayer.rect;
        Vector2 start = new Vector2(
            layerRect.xMin + layerRect.width * 0.15f,
            layerRect.yMin - layerRect.height * 0.15f);
        Quaternion dartRotation = GetDartRotation(start, end);

        LogDartDiagnostics(throwData, end, "end");

        _dartImage.gameObject.SetActive(true);
        _dartRect.anchoredPosition = start;
        _dartRect.localScale = new Vector3(0.75f, 0.75f, 1f);
        _dartRect.localRotation = dartRotation;

        float elapsed = 0f;

        while (elapsed < FlightDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / FlightDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            Vector2 position = Vector2.LerpUnclamped(start, end, eased);
            position.y += Mathf.Sin(progress * Mathf.PI) * 55f;

            _dartRect.anchoredPosition = position;

            float scale = Mathf.Lerp(0.75f, 1f, eased);
            _dartRect.localScale = new Vector3(scale, scale, 1f);

            yield return null;
        }

        _dartRect.anchoredPosition = end;
        _dartRect.localRotation = dartRotation;
        LogDartDiagnostics(throwData, end, "impact");

        float impactElapsed = 0f;
        while (impactElapsed < ImpactDuration)
        {
            impactElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(impactElapsed / ImpactDuration);
            float punch = 1f + 0.12f * Mathf.Sin(progress * Mathf.PI);
            _dartRect.localScale = new Vector3(punch, punch, 1f);

            if (_boardImage != null)
            {
                float boardPunch = 1f + 0.025f * Mathf.Sin(progress * Mathf.PI);
                _boardImage.rectTransform.localScale = new Vector3(boardPunch, boardPunch, 1f);
            }

            yield return null;
        }

        _dartRect.localScale = Vector3.one;
        if (_boardImage != null)
            _boardImage.rectTransform.localScale = Vector3.one;

        yield return FinishThrow(throwData, onImpact);
    }

    private IEnumerator FinishThrow(Throw throwData, Action onImpact)
    {
        ShowHit(throwData);
        _hitHoldActive = true;
        onImpact?.Invoke();
        yield return new WaitForSeconds(HitHoldDuration);
        _hitHoldActive = false;

        if (_dartRect != null)
            LogDartDiagnostics(throwData, _dartRect.anchoredPosition, "hold");
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

        if (_targetText != null)
        {
            _targetText.text = GetTargetLabel() + (_hasTarget
                ? FormatValue(_targetValue, _targetMultiplier)
                : "-");
        }

        if (_targetCrosshair != null)
        {
            _targetCrosshair.gameObject.SetActive(_hasTarget);
            if (_hasTarget)
                _targetCrosshair.anchoredPosition = GetBoardPosition(_targetValue, _targetMultiplier);
        }

        if (_hitText != null)
        {
            _hitText.text = string.Empty;
            _hitText.gameObject.SetActive(false);
        }

        if (_dartImage != null)
            _dartImage.gameObject.SetActive(false);

        if (_boardImage != null)
            _boardImage.rectTransform.localScale = Vector3.one;

        _hitHoldActive = false;

        ApplyTheme();
    }

    private void ShowHit(Throw throwData)
    {
        if (_hitText == null)
            return;

        string actual = FormatActual(throwData);
        _hitText.text = LocalizationText.Get("Hit", "Hit") + ": " + actual;
        _hitText.gameObject.SetActive(true);
        _hitText.color = GetColor(ThemeColorRole.Text1, Color.white);
    }

    private Vector2 GetCardPositionForThrow(Throw throwData)
    {
        if (_boardArea == null || _animationLayer == null)
            return Vector2.zero;

        if (throwData == null)
            return _animationLayer.InverseTransformPoint(_boardArea.position);

        bool isExactHit = _hasTarget
            && throwData.Value == _targetValue
            && throwData.Multiplier == _targetMultiplier
            && _targetCrosshair != null
            && _targetCrosshair.gameObject.activeInHierarchy;

        if (isExactHit)
            return _animationLayer.InverseTransformPoint(_targetCrosshair.position);

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

        Vector3 world = _boardArea.TransformPoint(boardPosition);
        return _animationLayer.InverseTransformPoint(world);
    }

    private Vector2 GetBoardPosition(int value, DartMultiplier multiplier)
    {
        if (value == 25)
        {
            float radius = multiplier == DartMultiplier.Double
                ? 0f
                : _boardRadius * 0.07f;
            return new Vector2(0f, radius);
        }

        if (value <= 0)
            return Vector2.zero;

        float angle = GetSegmentAngle(value);
        float radians = angle * Mathf.Deg2Rad;

        float ringFactor = multiplier switch
        {
            DartMultiplier.Triple => TripleRingRadius,
            DartMultiplier.Double => DoubleRingRadius,
            _ => SingleOuterRingRadius
        };

        float ringRadius = _boardRadius * ringFactor;
        return new Vector2(Mathf.Sin(radians) * ringRadius, Mathf.Cos(radians) * ringRadius);
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

    private static string GetTargetLabel()
    {
        return "Target: ";
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

    private void EnsureBuilt()
    {
        if (_built || _root == null)
            return;

        _built = true;

        var layout = _root.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        BuildCard();
        BuildContent();
        BuildInfoColumn();
        BuildBoard();
        BuildFooter();
        BuildAnimationLayer();

        ApplyTheme();
        SubscribeToTheme();
    }

    private void BuildCard()
    {
        float borderOffset = CardMargin - CardBorderInset;

        _cardBorder = CreateImage("BotCardBorder", _root.transform, null, Color.white);
        SetStretchRect(_cardBorder.rectTransform, borderOffset, borderOffset, borderOffset, borderOffset);

        _cardImage = CreateImage("BotCard", _root.transform, null, Color.white);
        SetStretchRect(_cardImage.rectTransform, CardMargin, CardMargin, CardMargin, CardMargin);
        _card = _cardImage.rectTransform;
    }

    private void BuildContent()
    {
        var contentGo = CreateUIObject("BotContent", _card);
        _content = (RectTransform)contentGo.transform;
        _content.anchorMin = Vector2.zero;
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 0.5f);
        _content.offsetMin = new Vector2(0f, CardPadding + FooterHeight + FooterGap);
        _content.offsetMax = new Vector2(0f, -CardPadding);
        _content.localScale = Vector3.one;
        _content.localRotation = Quaternion.identity;
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
        stats.localScale = Vector3.one;
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
        {
            Debug.LogWarning("[BotTurnPanel] botIconRow nicht verdrahtet; Fallback-Suche nach 'BotIconRow'.", this);
            iconRow = FindChild(_root.transform, "BotIconRow");
        }

        Transform botIcon = _botIconSource != null ? _botIconSource.transform : null;
        if (botIcon == null && iconRow != null)
        {
            Debug.LogWarning("[BotTurnPanel] botIcon nicht verdrahtet; Fallback-Suche nach 'BotIcon'.", this);
            botIcon = FindChild(iconRow, "BotIcon");
        }

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

        var targetGo = CreateUIObject("BotTargetBlock", stats);
        var targetBlock = (RectTransform)targetGo.transform;
        targetBlock.anchorMin = new Vector2(0f, 1f);
        targetBlock.anchorMax = new Vector2(1f, 1f);
        targetBlock.pivot = new Vector2(0.5f, 1f);
        targetBlock.sizeDelta = new Vector2(0f, TargetBlockHeight);
        targetBlock.anchoredPosition = new Vector2(0f, -(NameTextHeight + NameIconGap + BotIconSize + IconTargetGap));
        targetBlock.localScale = Vector3.one;
        targetBlock.localRotation = Quaternion.identity;

        _targetText = CreateText("BotTarget", targetBlock, Vector2.zero, new Vector2(100f, InfoTextHeight), InfoFontSize);
        _targetText.fontStyle = FontStyles.Bold;
        _targetText.alignment = TextAlignmentOptions.Center;
        _targetText.color = GetColor(ThemeColorRole.Text1, Color.white);
        var targetRect = _targetText.rectTransform;
        targetRect.anchorMin = new Vector2(0f, 1f);
        targetRect.anchorMax = new Vector2(1f, 1f);
        targetRect.pivot = new Vector2(0.5f, 1f);
        targetRect.offsetMin = new Vector2(0f, -InfoTextHeight);
        targetRect.offsetMax = Vector2.zero;

        _hitText = CreateText("BotHit", targetBlock, Vector2.zero, new Vector2(100f, InfoTextHeight), InfoFontSize);
        _hitText.alignment = TextAlignmentOptions.Center;
        _hitText.color = GetColor(ThemeColorRole.Text1, Color.white);
        var hitRect = _hitText.rectTransform;
        hitRect.anchorMin = new Vector2(0f, 0f);
        hitRect.anchorMax = new Vector2(1f, 0f);
        hitRect.pivot = new Vector2(0.5f, 0f);
        hitRect.offsetMin = Vector2.zero;
        hitRect.offsetMax = new Vector2(0f, InfoTextHeight);
        _hitText.gameObject.SetActive(false);
    }

    private void BuildBoard()
    {
        float contentHeight = _content != null ? _content.rect.height : 0f;
        var rootRect = _root != null ? _root.GetComponent<RectTransform>() : null;
        if (contentHeight <= 1f && rootRect != null)
            contentHeight = rootRect.rect.height - CardMargin * 2f - CardPadding * 2f - FooterHeight - FooterGap;
        if (contentHeight <= 1f && _card != null)
            contentHeight = _card.rect.height - CardPadding * 2f - FooterHeight - FooterGap;
        if (contentHeight <= 1f)
            contentHeight = MaxBoardAreaSize;

        _boardAreaSize = Mathf.Min(MaxBoardAreaSize, contentHeight);
        float scale = _boardAreaSize / MaxBoardAreaSize;
        _boardSize = MaxBoardSize * scale;
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
    }

    private void BuildFooter()
    {
        var footerGo = CreateUIObject("BotFooter", _card);
        _footer = (RectTransform)footerGo.transform;
        _footer.anchorMin = new Vector2(0.5f, 0f);
        _footer.anchorMax = new Vector2(0.5f, 0f);
        _footer.pivot = new Vector2(0.5f, 0f);
        _footer.sizeDelta = new Vector2(FooterWidth, FooterHeight);
        _footer.anchoredPosition = new Vector2(0f, CardPadding);
        _footer.localScale = Vector3.one;
        _footer.localRotation = Quaternion.identity;

        if (_playingContainer != null)
        {
            _playingContainer.transform.SetParent(_footer, false);
            var rect = (RectTransform)_playingContainer.transform;
            SetRect(rect, new Vector2(FooterWidth, FooterHeight), Vector2.zero);
            PositionButtons(_playingContainer, false);
        }

        if (_pausedContainer != null)
        {
            _pausedContainer.transform.SetParent(_footer, false);
            var rect = (RectTransform)_pausedContainer.transform;
            SetRect(rect, new Vector2(FooterWidth, FooterHeight), Vector2.zero);
            PositionButtons(_pausedContainer, true);
        }
    }

    private void BuildAnimationLayer()
    {
        var layerGo = CreateUIObject("BotAnimationLayer", _card);
        _animationLayer = (RectTransform)layerGo.transform;
        SetStretchRect(_animationLayer, 0f, 0f, 0f, 0f);

        if (_dartSprite == null)
            return;

        _dartImage = CreateImage("BotDart", _animationLayer, _dartSprite, GetColor(ThemeColorRole.Text1, Color.white));
        _dartRect = _dartImage.rectTransform;
        _dartRect.anchorMin = new Vector2(0.5f, 0.5f);
        _dartRect.anchorMax = new Vector2(0.5f, 0.5f);
        _dartRect.pivot = GetDartTipPivot(_dartSprite);
        _dartRect.sizeDelta = new Vector2(_dartSize, _dartSize);
        _dartRect.anchoredPosition = Vector2.zero;
        _dartImage.preserveAspect = true;
        _dartImage.raycastTarget = false;

        _dartOutline = _dartImage.gameObject.AddComponent<Outline>();
        _dartOutline.effectDistance = Vector2.one * Mathf.Max(1.5f, _dartSize * 0.015f);
        _dartOutline.useGraphicAlpha = true;
        ApplyDartTheme();

        _dartImage.gameObject.SetActive(false);
    }

    private void PositionButtons(GameObject container, bool twoButtons)
    {
        var buttons = container.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            var rect = (RectTransform)button.transform;
            SetRect(rect, new Vector2(110f, 110f), Vector2.zero);

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
        if (ThemeManager.Instance == null)
            return;

        _subscribedThemeManager = ThemeManager.Instance;
        _subscribedThemeManager.OnThemeChanged += ApplyTheme;
    }

    private void ApplyTheme()
    {
        if (!_built)
            return;

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

        if (_hitText != null)
            _hitText.color = GetColor(ThemeColorRole.Text1, Color.white);

        if (_targetText != null)
            _targetText.color = GetColor(ThemeColorRole.Text1, Color.white);

        for (int i = 0; i < _targetCrosshairParts.Count; i++)
        {
            if (_targetCrosshairParts[i] != null)
                _targetCrosshairParts[i].color = MarkerColor;
        }

        if (_botIcon != null)
        {
            _botIcon.color = _currentBot != null && ThemeManager.Instance != null
                ? ThemeManager.Instance.GetBotSkillColor(_currentBot.GetSkillRating())
                : GetColor(ThemeColorRole.Text1, Color.white);
        }

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
