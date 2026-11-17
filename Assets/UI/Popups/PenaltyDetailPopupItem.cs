using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line in the penalty detail popup: (Name | Count | Costs | Sum).
/// Filling exclusively via Bind() (pop-up code); fields wired in the prefab.
/// </summary>
public class PenaltyDetailPopupItem : MonoBehaviour
{
    [Header("Zellen")]
    public TMP_Text penaltyName;
    public TMP_Text penaltyCount;
    public TMP_Text penaltyCosts;
    public TMP_Text penaltySum;

    private Image divider;
    private LayoutElement rowLayout;
    private bool styleInitialized;

    /// <summary>Describes the line. All values already in display strings.</summary>
    public void Bind(string name, string cost, string count, string rowTotal, bool header = false)
    {
        EnsureStyleRefs();
        ApplyRowStyle(header);

        float size = header ? 28f : 44f;

        StyleText(penaltyName, name, size,
            header ? ThemeColorRole.Text3 : ThemeColorRole.Text1,
            TextAlignmentOptions.Left, header);

        StyleText(penaltyCount, count, size,
            header ? ThemeColorRole.Text3 : ThemeColorRole.Text2,
            TextAlignmentOptions.Center, header);

        StyleText(penaltyCosts, cost, size,
            header ? ThemeColorRole.Text3 : ThemeColorRole.Text2,
            TextAlignmentOptions.Center, header);

        StyleText(penaltySum, rowTotal, size,
            header ? ThemeColorRole.Text3 : ThemeColorRole.Accent1,
            TextAlignmentOptions.Right, true);
    }

    private void EnsureStyleRefs()
    {
        if (rowLayout == null)
            rowLayout = GetComponent<LayoutElement>();

        var rowGroup = GetComponent<HorizontalLayoutGroup>();
        if (rowGroup != null)
        {
            rowGroup.padding = new RectOffset(24, 24, 8, 8);
            rowGroup.spacing = 12;
        }

        EnsureDivider();

        if (styleInitialized)
            return;

        SetFlexibleWidth(penaltyName, 2.4f);
        SetFlexibleWidth(penaltyCount, 0.8f);
        SetFlexibleWidth(penaltyCosts, 1.1f);
        SetFlexibleWidth(penaltySum, 1.3f);

        styleInitialized = true;
    }

    private static void SetFlexibleWidth(TMP_Text text, float flexibleWidth)
    {
        if (text == null || text.transform.parent == null)
            return;

        var cell = text.transform.parent.GetComponent<LayoutElement>();
        if (cell == null)
            cell = text.transform.parent.gameObject.AddComponent<LayoutElement>();

        cell.flexibleWidth = flexibleWidth;
        cell.minWidth = 10f;
    }

    private void EnsureDivider()
    {
        if (divider != null)
            return;

        var go = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(0);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 1f);

        var element = go.AddComponent<LayoutElement>();
        element.ignoreLayout = true;

        divider = go.GetComponent<Image>();
        divider.raycastTarget = false;
    }

    private void ApplyRowStyle(bool header)
    {
        if (rowLayout != null)
            rowLayout.preferredHeight = header ? 64f : 120f;

        if (divider == null)
            return;

        Color tint = GetThemeColor(ThemeColorRole.Text3, new Color(1f, 1f, 1f, 0.5f));
        float alpha = header ? 0.28f : 0.1f;
        divider.color = new Color(tint.r, tint.g, tint.b, alpha);
    }

    private static void StyleText(TMP_Text text, string value, float size, ThemeColorRole role,
        TextAlignmentOptions alignment, bool bold)
    {
        if (text == null)
            return;

        text.text = value ?? "";
        text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.color = GetThemeColor(role, Color.white);
    }

    private static Color GetThemeColor(ThemeColorRole role, Color fallback)
    {
        if (ThemeManager.Instance == null)
            return fallback;

        try { return ThemeManager.Instance.GetColor(role); }
        catch { return fallback; }
    }
}
