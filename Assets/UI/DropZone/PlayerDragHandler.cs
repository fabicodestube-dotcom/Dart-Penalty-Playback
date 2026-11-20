using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(CanvasGroup))]
public class PlayerDragHandler : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public Guid playerID;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private LayoutElement selfLayout;

    private Transform originalParent;
    private int originalIndex;

    private GameObject placeholder;
    private Transform placeholderParent;

    private bool isPointerDown;
    private bool isDragging;
    private float pressTime;
    private Vector2 startPointerPos;
    private Vector2 lastPointerPos;

    public static bool IsDraggingAny;

    [Header("Long Press")]
    [SerializeField] private float longPressDuration = 0.3f;
    [SerializeField] private float moveThreshold = 10f;

    [Header("Auto Scroll")]
    [SerializeField] private float autoScrollSpeed = 250f;
    [SerializeField] private float autoScrollEdgeSize = 80f;
    [Range(0f, 1f)]
    [SerializeField] private float autoScrollEdgeViewportRatio = 0.2f;

    private ScrollRect currentScrollRect;
    private PlayerDropZone activeZone;
    private PlayerDropZone[] dropZones;

    // AUTO SCROLL STATE
    private bool isAutoScrolling;
    private float autoScrollDir;
    private ScrollRect autoScrollTarget;

    private bool pressMoved;
    private bool forwardingScrollDrag;
    private bool layoutLocked;

    private Image placeholderImage;

    // =====================
    // PUBLIC PROPERTIES
    // =====================
    public bool IsDraggingThisItem => isDragging;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        selfLayout = GetComponent<LayoutElement>();
        if (selfLayout == null)
            selfLayout = gameObject.AddComponent<LayoutElement>();
    }

    void Update()
    {
        if (Pointer.current == null)
            return;

        Vector2 pointerPos = Pointer.current.position.ReadValue();

        if (isPointerDown && !isDragging)
        {
            float held = Time.unscaledTime - pressTime;
            float moved = Vector2.Distance(startPointerPos, pointerPos);

            if (moved >= moveThreshold)
                pressMoved = true;

            if (!pressMoved && held >= longPressDuration)
                StartDrag(pointerPos);
        }

        if (isDragging)
        {
            Vector2 delta = pointerPos - lastPointerPos;
            lastPointerPos = pointerPos;

            rectTransform.anchoredPosition += delta / GetCanvasScale();

            SimulateDrag(pointerPos);

            ApplyAutoScroll(); // FIX: controlled execution

            if (!Pointer.current.press.isPressed)
                EndDrag(pointerPos);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        pressMoved = false;
        pressTime = Time.unscaledTime;
        startPointerPos = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        pressMoved = false;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (isDragging)
            return;

        ScrollRect scroll = ResolveCurrentScrollRect();
        if (scroll != null)
            scroll.OnInitializePotentialDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            forwardingScrollDrag = false;
            return;
        }

        ScrollRect scroll = ResolveCurrentScrollRect();
        if (scroll == null)
            return;

        forwardingScrollDrag = true;
        scroll.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging || !forwardingScrollDrag)
            return;

        if (currentScrollRect != null)
            currentScrollRect.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (forwardingScrollDrag && currentScrollRect != null)
            currentScrollRect.OnEndDrag(eventData);

        forwardingScrollDrag = false;
    }

    private ScrollRect ResolveCurrentScrollRect()
    {
        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        if (scroll != null)
            currentScrollRect = scroll;

        return currentScrollRect;
    }

    private void StartDrag(Vector2 pointerPos)
    {
        VibrationManager.Instance.Vibrate();

        isDragging = true;
        IsDraggingAny = true;

        lastPointerPos = pointerPos;

        originalParent = transform.parent;
        originalIndex = transform.GetSiblingIndex();

        currentScrollRect = GetComponentInParent<ScrollRect>();

        if (forwardingScrollDrag && currentScrollRect != null)
        {
            PointerEventData endData = new PointerEventData(EventSystem.current);
            endData.position = pointerPos;
            endData.pressPosition = startPointerPos;
            endData.button = PointerEventData.InputButton.Left;
            currentScrollRect.OnEndDrag(endData);
        }
        forwardingScrollDrag = false;

        if (currentScrollRect != null)
            currentScrollRect.StopMovement();

        dropZones = GameObject.FindObjectsByType<PlayerDropZone>(FindObjectsSortMode.None);

        PlayerDropZone originZone = FindZoneForContent(originalParent);
        if (originZone != null)
        {
            PlayerDropZone allowedOther = originZone.singleZone ? null : originZone.OtherZone;
            dropZones = Array.FindAll(
                dropZones,
                zone => zone != null && (zone == originZone || zone == allowedOther)
            );
        }

        PointerEventData initialPointer = new PointerEventData(EventSystem.current);
        initialPointer.position = pointerPos;
        activeZone = GetCurrentDropZone(initialPointer);
        if (activeZone == null)
            activeZone = originZone;

        selfLayout.ignoreLayout = true;

        RectTransform itemRect = GetComponent<RectTransform>();
        CreatePlaceholder(itemRect);

        Transform placeholderInitialParent = activeZone != null ? activeZone.contentParent : originalParent;
        placeholder.transform.SetParent(placeholderInitialParent, false);
        placeholder.transform.SetSiblingIndex(originalIndex);

        placeholderParent = placeholderInitialParent;

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;

        transform.SetParent(GetRootCanvas().transform, true);
        transform.SetAsLastSibling();

        // reset scroll state
        isAutoScrolling = false;
        autoScrollDir = 0f;
        autoScrollTarget = null;
    }

    private void SimulateDrag(Vector2 pointerPos)
    {
        PointerEventData fake = new PointerEventData(EventSystem.current);
        fake.position = pointerPos;

        PlayerDropZone hoveredZone = GetCurrentDropZone(fake);
        if (hoveredZone != null)
            activeZone = hoveredZone;

        UpdateAutoScrollState(fake, activeZone);

        if (activeZone == null || layoutLocked) return;

        Transform newParent = activeZone.contentParent;
        bool changed = false;

        if (placeholderParent != newParent)
        {
            placeholder.transform.SetParent(newParent, false);
            placeholderParent = newParent;
            changed = true;
        }

        int oldIndex = placeholder.transform.GetSiblingIndex();
        int newIndex = activeZone.GetDropIndex(rectTransform);

        if (oldIndex != newIndex)
        {
            VibrationManager.Instance.Vibrate();
            placeholder.transform.SetSiblingIndex(newIndex);
            changed = true;
        }

        if (changed)
            RequestRebuild(newParent as RectTransform);
    }

    private void EndDrag(Vector2 pointerPos)
    {
        isDragging = false;
        IsDraggingAny = false;
        isPointerDown = false;
        pressMoved = false;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        isAutoScrolling = false;
        autoScrollDir = 0f;
        autoScrollTarget = null;
        forwardingScrollDrag = false;
        layoutLocked = false;

        PlayerDropZone targetZone = activeZone;

        PointerEventData endPointer = new PointerEventData(EventSystem.current);
        endPointer.position = pointerPos;

        PlayerDropZone hoveredAtRelease = GetCurrentDropZone(endPointer);
        if (hoveredAtRelease != null)
            targetZone = hoveredAtRelease;

        if (targetZone == null && placeholder != null)
            targetZone = FindZoneForContent(placeholder.transform.parent);

        if (targetZone != null)
        {
            Transform targetParent = targetZone.contentParent;

            if (placeholder != null)
            {
                if (placeholder.transform.parent != targetParent)
                    placeholder.transform.SetParent(targetParent, false);

                int targetIndex = targetZone.GetDropIndex(rectTransform);
                placeholder.transform.SetSiblingIndex(targetIndex);
            }

            int dropIndex = placeholder != null
                ? placeholder.transform.GetSiblingIndex()
                : targetZone.GetDropIndex(rectTransform);

            selfLayout.ignoreLayout = false;

            transform.SetParent(targetParent, false);
            transform.SetSiblingIndex(dropIndex);

            rectTransform.anchoredPosition = Vector2.zero;

            if (placeholder != null)
            {
                placeholder.SetActive(false);
                Destroy(placeholder);
            }

            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)targetParent);

            TriggerDrop(targetZone);
        }
        else
        {
            selfLayout.ignoreLayout = false;

            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalIndex);

            rectTransform.anchoredPosition = Vector2.zero;

            if (placeholder != null)
            {
                placeholder.SetActive(false);
                Destroy(placeholder);
            }
        }

        placeholder = null;
        placeholderParent = null;
        placeholderImage = null;
        activeZone = null;
        dropZones = null;
    }

    private void RequestRebuild(RectTransform target)
    {
        if (target == null) return;
        StartCoroutine(RebuildNextFrame(target));
    }

    private IEnumerator RebuildNextFrame(RectTransform target)
    {
        layoutLocked = true;
        yield return null;
        LayoutRebuilder.MarkLayoutForRebuild(target);
        layoutLocked = false;
    }

    private void TriggerDrop(PlayerDropZone zone)
    {
        if (zone == null) return;

        PointerEventData fake = new PointerEventData(EventSystem.current);
        fake.pointerDrag = gameObject;

        zone.OnDrop(fake);
    }

    private void CreatePlaceholder(RectTransform itemRect)
    {
        placeholder = new GameObject("Placeholder", typeof(RectTransform));

        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = itemRect.anchorMin;
        placeholderRect.anchorMax = itemRect.anchorMax;
        placeholderRect.pivot = itemRect.pivot;

        float width = itemRect.rect.width;
        float height = itemRect.rect.height;

        placeholderRect.sizeDelta = new Vector2(width, height);

        LayoutElement le = placeholder.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.preferredWidth = width;

        Image image = placeholder.AddComponent<Image>();

        Image sourceImage = GetComponent<Image>();
        if (sourceImage != null)
        {
            image.sprite = sourceImage.sprite;
            image.type = sourceImage.type;
            image.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
            image.preserveAspect = sourceImage.preserveAspect;
        }

        Color accent = ThemeManager.Instance != null
            ? ThemeManager.Instance.GetColor(ThemeColorRole.Accent1)
            : Color.white;
        accent.a = 0.25f;

        image.color = accent;
        image.raycastTarget = false;

        placeholderImage = image;
    }

    private void SetPlaceholderHighlight(bool highlighted)
    {
        if (placeholderImage == null) return;

        Color color = placeholderImage.color;
        float targetAlpha = highlighted ? 0.6f : 0.25f;

        if (Mathf.Approximately(color.a, targetAlpha)) return;

        color.a = targetAlpha;
        placeholderImage.color = color;
    }

    private PlayerDropZone FindZoneForContent(Transform content)
    {
        if (content == null) return null;

        if (dropZones == null)
            dropZones = GameObject.FindObjectsByType<PlayerDropZone>(FindObjectsSortMode.None);

        foreach (var z in dropZones)
            if (z != null && z.contentParent == content)
                return z;

        return null;
    }

    private PlayerDropZone GetCurrentDropZone(PointerEventData eventData)
    {
        if (dropZones == null)
            dropZones = GameObject.FindObjectsByType<PlayerDropZone>(FindObjectsSortMode.None);

        foreach (var z in dropZones)
            if (z != null && z.IsPointerOver(eventData))
                return z;

        return null;
    }

    private float GetCanvasScale()
    {
        Canvas c = GetComponentInParent<Canvas>();
        return c != null ? c.scaleFactor : 1f;
    }

    private Canvas GetRootCanvas()
    {
        Canvas c = GetComponentInParent<Canvas>();
        return c != null ? c.rootCanvas : null;
    }

    private void UpdateAutoScrollState(PointerEventData eventData, PlayerDropZone zone)
    {
        ScrollRect target = zone != null ? zone.ListScrollRect : null;

        if (target == null || target.viewport == null || target.content == null || !target.vertical)
        {
            SetAutoScroll(null, 0f);
            return;
        }

        RectTransform viewport = target.viewport;

        Vector3[] corners = new Vector3[4];
        viewport.GetWorldCorners(corners);

        float top = corners[1].y;
        float bottom = corners[0].y;

        float viewportHeight = top - bottom;
        float edgeSize = Mathf.Min(autoScrollEdgeSize, viewportHeight * autoScrollEdgeViewportRatio);

        if (edgeSize <= 0f)
        {
            SetAutoScroll(null, 0f);
            return;
        }

        float pointerY = eventData.position.y;
        float dir = 0f;

        if (pointerY > top - edgeSize)
            dir = Mathf.Clamp01((pointerY - (top - edgeSize)) / edgeSize);
        else if (pointerY < bottom + edgeSize)
            dir = -Mathf.Clamp01(((bottom + edgeSize) - pointerY) / edgeSize);

        SetAutoScroll(target, dir);
    }

    private void SetAutoScroll(ScrollRect target, float dir)
    {
        bool active = target != null && dir != 0f;

        isAutoScrolling = active;
        autoScrollDir = active ? dir : 0f;
        autoScrollTarget = active ? target : null;

        SetPlaceholderHighlight(active);
    }

    private void ApplyAutoScroll()
    {
        if (!isAutoScrolling || autoScrollTarget == null) return;

        ScrollRect scroll = autoScrollTarget;
        RectTransform viewport = scroll.viewport;
        RectTransform content = scroll.content;

        if (viewport == null || content == null) return;

        Vector3[] corners = new Vector3[4];
        viewport.GetWorldCorners(corners);

        float viewportScreenHeight = Mathf.Abs(corners[1].y - corners[0].y);
        float viewportLocalHeight = viewport.rect.height;

        if (viewportScreenHeight <= 0f || viewportLocalHeight <= 0f) return;

        float contentToViewport = content.rect.height / viewportLocalHeight;
        float scrollableScreenHeight = viewportScreenHeight * Mathf.Max(0f, contentToViewport - 1f);

        if (scrollableScreenHeight <= 1f) return;

        float current = scroll.verticalNormalizedPosition;

        if (current >= 1f && autoScrollDir > 0f) return;
        if (current <= 0f && autoScrollDir < 0f) return;

        float delta = autoScrollSpeed * autoScrollDir * Time.unscaledDeltaTime / scrollableScreenHeight;

        scroll.verticalNormalizedPosition = Mathf.Clamp01(current + delta);
    }
}