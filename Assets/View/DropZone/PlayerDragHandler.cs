using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(CanvasGroup))]
public class PlayerDragHandler : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler
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

    private ScrollRect currentScrollRect;

    // 🔥 AUTO SCROLL STATE
    private bool isAutoScrolling;
    private float autoScrollDir;

    private bool layoutLocked;

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

            if (held >= longPressDuration && moved < moveThreshold)
                StartDrag(pointerPos);
        }

        if (isDragging)
        {
            Vector2 delta = pointerPos - lastPointerPos;
            lastPointerPos = pointerPos;

            rectTransform.anchoredPosition += delta / GetCanvasScale();

            SimulateDrag(pointerPos);

            ApplyAutoScroll(); // 🔥 FIX: controlled execution

            if (!Pointer.current.press.isPressed)
                EndDrag(pointerPos);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        pressTime = Time.unscaledTime;
        startPointerPos = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
    }

    private void StartDrag(Vector2 pointerPos)
    {
        isDragging = true;
        IsDraggingAny = true;

        lastPointerPos = pointerPos;

        originalParent = transform.parent;
        originalIndex = transform.GetSiblingIndex();

        currentScrollRect = GetComponentInParent<ScrollRect>();

        if (currentScrollRect != null)
            currentScrollRect.StopMovement();

        selfLayout.ignoreLayout = true;

        placeholder = new GameObject("Placeholder");
        LayoutElement le = placeholder.AddComponent<LayoutElement>();

        RectTransform myRT = GetComponent<RectTransform>();
        le.preferredHeight = myRT.rect.height;
        le.preferredWidth = myRT.rect.width;

        placeholder.transform.SetParent(originalParent);
        placeholder.transform.SetSiblingIndex(originalIndex);

        placeholderParent = originalParent;

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;

        transform.SetParent(GetRootCanvas().transform, true);
        transform.SetAsLastSibling();

        // reset scroll state
        isAutoScrolling = false;
        autoScrollDir = 0f;
    }

    private void SimulateDrag(Vector2 pointerPos)
    {
        if (layoutLocked) return;

        PointerEventData fake = new PointerEventData(EventSystem.current);
        fake.position = pointerPos;

        PlayerDropZone zone = GetCurrentDropZone(fake);
        if (zone == null) return;

        Transform newParent = zone.contentParent;

        if (placeholderParent != newParent)
        {
            placeholder.transform.SetParent(newParent);
            placeholderParent = newParent;
        }

        int newIndex = zone.GetDropIndex(rectTransform);
        placeholder.transform.SetSiblingIndex(newIndex);

        RequestRebuild(placeholder.transform.parent as RectTransform);

        HandleAutoScroll(fake); // 🔥 only sets STATE
    }

    private void EndDrag(Vector2 pointerPos)
    {
        isDragging = false;
        IsDraggingAny = false;
        isPointerDown = false;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        isAutoScrolling = false;
        autoScrollDir = 0f;

        if (placeholder != null)
        {
            Transform targetParent = placeholder.transform.parent;
            int targetIndex = placeholder.transform.GetSiblingIndex();

            selfLayout.ignoreLayout = false;

            transform.SetParent(targetParent, false);
            transform.SetSiblingIndex(targetIndex);

            rectTransform.anchoredPosition = Vector2.zero;

            Destroy(placeholder);

            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)targetParent);
        }

        TriggerDrop(pointerPos);

        PointerEventData fake = new PointerEventData(EventSystem.current);
        fake.position = pointerPos;

        if (GetCurrentDropZone(fake) == null)
        {
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalIndex);
            rectTransform.anchoredPosition = Vector2.zero;
        }
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

    private void TriggerDrop(Vector2 pointerPos)
    {
        PointerEventData fake = new PointerEventData(EventSystem.current);
        fake.position = pointerPos;
        fake.pointerDrag = gameObject;

        PlayerDropZone zone = GetCurrentDropZone(fake);
        if (zone != null)
            zone.OnDrop(fake);
    }

    private PlayerDropZone GetCurrentDropZone(PointerEventData eventData)
    {
        var zones = GameObject.FindObjectsByType<PlayerDropZone>(FindObjectsSortMode.None);

        foreach (var z in zones)
            if (z.IsPointerOver(eventData))
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

    // =========================
    // 🔥 FIXED AUTO SCROLL
    // =========================

    private void HandleAutoScroll(PointerEventData eventData)
    {
        if (currentScrollRect == null) return;

        RectTransform viewport = currentScrollRect.viewport;
        if (viewport == null) return;

        Vector3[] corners = new Vector3[4];
        viewport.GetWorldCorners(corners);

        float top = corners[1].y;
        float bottom = corners[0].y;

        float pointerY = eventData.position.y;

        float edgeSize = 80f;

        float dir = 0f;

        if (pointerY > top - edgeSize)
        {
            float t = (pointerY - (top - edgeSize)) / edgeSize;
            dir = t;
        }
        else if (pointerY < bottom + edgeSize)
        {
            float t = ((bottom + edgeSize) - pointerY) / edgeSize;
            dir = -t;
        }

        if (dir != 0f)
        {
            isAutoScrolling = true;
            autoScrollDir = dir;
        }
        else
        {
            isAutoScrolling = false;
            autoScrollDir = 0f;
        }
    }

    private void ApplyAutoScroll()
    {
        if (!isAutoScrolling) return;
        if (currentScrollRect == null) return;

        float current = currentScrollRect.verticalNormalizedPosition;

        if (current >= 1f && autoScrollDir > 0f) return;
        if (current <= 0f && autoScrollDir < 0f) return;

        float speed = 2.5f;

        current += autoScrollDir * speed * Time.unscaledDeltaTime;

        currentScrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(current);
    }

    
}