using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Dartboard heatmap as an anti-aliased texture on a UGUI quad.
/// A static zone map (supersampling) is used once per parameter set.
/// generated and shared by all Heatmap instances. SetData() only colors
/// still the pixels new, so that edges smooth and dividing lines everywhere the same
/// are wide (width in board units instead of angles).
/// The fields are colored by hit frequency: white (never) → green →
/// yellow → orange → red. Inner + outer single share a key ("n")
/// And then you'll see that the key "0" is counted as text.
/// Color scale is relative per player: max count = red.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DartboardHeatmap : MaskableGraphic
{
    [Header("Heat")]
    [Tooltip("Weiß (nie geworfen) → grün → gelb → orange → rot (am häufigsten).")]
    public Gradient heatGradient = CreateDefaultHeatGradient();

    [Header("Umrandung")]
    public Color borderColor = new Color(21f / 255f, 21f / 255f, 21f / 255f);
    [Tooltip("Breite des schwarzen Außenrings in Board-Einheiten (Double außen = 1).")]
    [Range(0f, 0.1f)]
    public float rimWidth = 0.035f;
    [Tooltip("Konstante Breite der Trennlinien in Board-Einheiten (nicht Winkel!).")]
    [Range(0.001f, 0.03f)]
    public float borderWidth = 0.008f;

    [Header("Felder")]
    [Tooltip("Radiale Dicke der Triple-Felder relativ zur offiziellen Geometrie.")]
    [Range(1f, 1.5f)]
    public float tripleThicknessScale = 1.1f;
    [Tooltip("Radiale Dicke der Double-Felder relativ zur offiziellen Geometrie.")]
    [Range(1f, 1.5f)]
    public float doubleThicknessScale = 1.1f;

    [Header("Textur")]
    [Tooltip("Auflösung der Zonen-Map. Höher = schärfer, aber teurer beim ersten Aufbau.")]
    [Range(128, 1024)]
    public int textureResolution = 512;
    [Tooltip("Subsamples pro Achse für Kantenglättung (2 = 2x2).")]
    [Range(1, 4)]
    public int edgeSupersampling = 2;

    [Header("Labels")]
    public bool showNumberLabels = true;
    public TMP_Text missLabel;
    [Tooltip("Font der Scheiben-Labels. Leer = wird vom Headline-Label des Charts übernommen (Roboto).")]
    public TMP_FontAsset labelFont;

    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private int maxCount;
    private int missCount;
    private readonly List<TMP_Text> numberLabels = new List<TMP_Text>(20);
    private bool labelsBuilt;
    private bool missLabelAutoCreated;
    private string missBaseText = "Miss";

    private Texture2D heatTexture;
    private Color32[] pixelBuffer;
    private bool colorDirty = true;

    private static string[] s_ZoneKeys;
    private static readonly Dictionary<MapKey, ZoneMap> s_MapCache = new Dictionary<MapKey, ZoneMap>();

    // A slightly transparent edge so that the outer edge of the pane is also
    // soft leakage can be performed instead of being cut off at the texture boundary.
    private const float TexturePadding = 0.01f;

    private static string[] ZoneKeys
    {
        get
        {
            if (s_ZoneKeys == null)
            {
                var zones = DartboardGeometry.BuildZones();
                s_ZoneKeys = new string[zones.Count];
                for (int i = 0; i < zones.Count; i++)
                    s_ZoneKeys[i] = zones[i].sectorKey;
            }
            return s_ZoneKeys;
        }
    }

    private struct MapKey : IEquatable<MapKey>
    {
        public int resolution;
        public int supersampling;
        public float borderWidth;
        public float rimWidth;
        public float tripleScale;
        public float doubleScale;

        public bool Equals(MapKey other)
        {
            return resolution == other.resolution
                && supersampling == other.supersampling
                && borderWidth == other.borderWidth
                && rimWidth == other.rimWidth
                && tripleScale == other.tripleScale
                && doubleScale == other.doubleScale;
        }

        public override bool Equals(object obj) => obj is MapKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + resolution;
                h = h * 31 + supersampling;
                h = h * 31 + borderWidth.GetHashCode();
                h = h * 31 + rimWidth.GetHashCode();
                h = h * 31 + tripleScale.GetHashCode();
                h = h * 31 + doubleScale.GetHashCode();
                return h;
            }
        }
    }

    private sealed class ZoneMap
    {
        public int resolution;
        public byte[] zoneIndex;      // 0..81, 255 = no zone (edge/outside)
        public byte[] borderCoverage; // 0..255 edge color share
        public byte[] alpha;          // 0..255 proportion within the disc
    }

    private struct SampleParams
    {
        public float outer;
        public float rimEnd;
        public float borderHalf;
        public float bullInner;
        public float bullOuter;
        public float tripleInner;
        public float tripleOuter;
        public float doubleInner;
    }

    private static Gradient CreateDefaultHeatGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Hex("34A853"), 0.3f),
                new GradientColorKey(Hex("FBBC05"), 0.55f),
                new GradientColorKey(Hex("F77F00"), 0.8f),
                new GradientColorKey(Hex("EA4335"), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return g;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = 1f;
        return c;
    }

    protected override void Awake()
    {
        // Self-healing: if this object exists without CanvasRenderer
        // (Prefab merge, manually created), explicitly retrofit, BEFORE
        // graphic machinery accesses it.
        if (GetComponent<CanvasRenderer>() == null)
            gameObject.AddComponent<CanvasRenderer>();

        base.Awake();
        raycastTarget = false;
        // Perf (M2): Zone map and texture are created only when needed,
        // see EnsureHeatView (.) Bar users do not pay any additional costs.
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (labelsBuilt && colorDirty)
            ApplyColors();
    }

    /// <summary>
    /// Build numbers/miss labels when first displaying the heatmap and bump
    /// them. Idempotent; SetData() only updates colors/text afterwards.
    /// </summary>
    public void EnsureHeatView()
    {
        EnsureLabels();
        LayoutLabels();
        if (heatTexture == null)
            EnsureTexture(ResolveResolution());
        EnsureMap();
        SetVerticesDirty();
        if (colorDirty)
            ApplyColors();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        borderWidth = Mathf.Clamp(borderWidth, 0.001f, 0.03f);
        textureResolution = Mathf.Clamp(textureResolution, 128, 1024);
        edgeSupersampling = Mathf.Clamp(edgeSupersampling, 1, 4);
        tripleThicknessScale = Mathf.Clamp(tripleThicknessScale, 1f, 1.5f);
        doubleThicknessScale = Mathf.Clamp(doubleThicknessScale, 1f, 1.5f);
        colorDirty = true;
        if (labelsBuilt)
        {
            EnsureMap();
            ApplyColors();
        }
        LayoutLabels();
        SetVerticesDirty();
    }
#endif

    /// <summary>New counting data (same keys as HitSectorChart ) Only colors become new.</summary>
    public void SetData(IReadOnlyDictionary<string, int> newCounts)
    {
        counts.Clear();
        maxCount = 0;
        missCount = 0;

        if (newCounts != null)
        {
            foreach (var pair in newCounts)
            {
                counts[pair.Key] = pair.Value;
                if (pair.Key == "0")
                    missCount = pair.Value;
                else if (pair.Value > maxCount)
                    maxCount = pair.Value;
            }
        }

        UpdateMissLabel();
        colorDirty = true;
        if (labelsBuilt)
            ApplyColors();
    }

    // ---------------------------------------------------------
    // TEXTURE / RENDERING
    // ---------------------------------------------------------

    public override Texture mainTexture => heatTexture;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // No data/texture yet: invisible drawing instead of black.
        if (heatTexture == null)
            return;

        Rect rect = GetPixelAdjustedRect();
        float unitsPerBoard = Mathf.Min(rect.width, rect.height) * 0.5f * 0.80f;
        float extent = unitsPerBoard *
            (DartboardGeometry.DoubleOuterRadius + Mathf.Max(0f, rimWidth) + TexturePadding);
        if (extent <= 0f)
            return;

        Vector2 center = rect.center;
        Color32 tint = color;

        int baseIndex = vh.currentVertCount;
        vh.AddVert(new Vector3(center.x - extent, center.y - extent, 0f), tint, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(center.x - extent, center.y + extent, 0f), tint, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(center.x + extent, center.y + extent, 0f), tint, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(center.x + extent, center.y - extent, 0f), tint, new Vector2(1f, 0f));
        vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
        vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
    }

    private int ResolveResolution()
    {
        return Mathf.Clamp(textureResolution, 128, 1024);
    }

    private void EnsureTexture(int resolution)
    {
        if (heatTexture != null && heatTexture.width == resolution)
            return;

        if (heatTexture != null)
        {
            if (Application.isPlaying)
                Destroy(heatTexture);
            else
                DestroyImmediate(heatTexture);
        }

        heatTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        pixelBuffer = null;
        SetMaterialDirty();
    }

    private void EnsureMap()
    {
        GetZoneMap();
    }

    private ZoneMap GetZoneMap()
    {
        var key = new MapKey
        {
            resolution = ResolveResolution(),
            supersampling = Mathf.Clamp(edgeSupersampling, 1, 4),
            borderWidth = borderWidth,
            rimWidth = Mathf.Max(0f, rimWidth),
            tripleScale = Mathf.Max(0f, tripleThicknessScale),
            doubleScale = Mathf.Max(0f, doubleThicknessScale)
        };

        if (s_MapCache.TryGetValue(key, out ZoneMap cached))
            return cached;

        // Editor sliders can create many variants; keep cache small.
        if (s_MapCache.Count >= 8)
            s_MapCache.Clear();

        ZoneMap map = BuildZoneMap(key);
        s_MapCache[key] = map;
        return map;
    }

    private void ApplyColors()
    {
        ZoneMap map = GetZoneMap();
        EnsureTexture(map.resolution);

        int total = map.resolution * map.resolution;
        if (pixelBuffer == null || pixelBuffer.Length != total)
            pixelBuffer = new Color32[total];

        string[] keys = ZoneKeys;
        var palette = new Color[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            counts.TryGetValue(keys[i], out int count);
            float t = maxCount > 0 ? count / (float)maxCount : 0f;
            palette[i] = heatGradient != null
                ? heatGradient.Evaluate(Mathf.Clamp01(t))
                : Color.white;
        }

        Color border = borderColor;
        for (int i = 0; i < total; i++)
        {
            byte alphaByte = map.alpha[i];
            if (alphaByte == 0)
            {
                pixelBuffer[i] = new Color32(0, 0, 0, 0);
                continue;
            }

            int zone = map.zoneIndex[i];
            Color zoneColor = zone < palette.Length ? palette[zone] : border;
            float coverage = map.borderCoverage[i] / 255f;
            Color c = Color.Lerp(zoneColor, border, coverage);
            c.a *= alphaByte / 255f;
            pixelBuffer[i] = c;
        }

        heatTexture.SetPixels32(pixelBuffer);
        heatTexture.Apply(false, false);
        colorDirty = false;
        SetMaterialDirty();
    }

    // ---------------------------------------------------------
    // ZONE MAP
    // ---------------------------------------------------------

    private static ZoneMap BuildZoneMap(MapKey key)
    {
        int res = key.resolution;
        int samples = key.supersampling;
        int total = res * res;

        var map = new ZoneMap
        {
            resolution = res,
            zoneIndex = new byte[total],
            borderCoverage = new byte[total],
            alpha = new byte[total]
        };

        float outer = DartboardGeometry.DoubleOuterRadius;
        float halfExtent = outer + key.rimWidth + TexturePadding;
        float pixelSize = 2f * halfExtent / res;
        float invSamples = 1f / samples;

        const float tripleInner = DartboardGeometry.TripleInnerRadius;
        const float tripleOuter = DartboardGeometry.TripleOuterRadius;
        float tripleMid = (tripleInner + tripleOuter) * 0.5f;
        float tripleHalf = (tripleOuter - tripleInner) * 0.5f * key.tripleScale;

        var sampleParams = new SampleParams
        {
            outer = outer,
            rimEnd = outer + key.rimWidth,
            borderHalf = key.borderWidth * 0.5f,
            bullInner = DartboardGeometry.BullInnerRadius,
            bullOuter = DartboardGeometry.BullOuterRadius,
            tripleInner = tripleMid - tripleHalf,
            tripleOuter = tripleMid + tripleHalf,
            doubleInner = outer - (outer - DartboardGeometry.DoubleInnerRadius) * key.doubleScale
        };

        var votes = new int[82];
        var touched = new List<int>(4);

        for (int py = 0; py < res; py++)
        {
            float boardY = (((py + 0.5f) / res) * 2f - 1f) * halfExtent;
            for (int px = 0; px < res; px++)
            {
                float boardX = (((px + 0.5f) / res) * 2f - 1f) * halfExtent;

                int insideCount = 0;
                int borderCount = 0;
                touched.Clear();

                for (int sy = 0; sy < samples; sy++)
                {
                    float sampleY = boardY + (((sy + 0.5f) * invSamples) - 0.5f) * pixelSize;
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float sampleX = boardX + (((sx + 0.5f) * invSamples) - 0.5f) * pixelSize;
                        SamplePoint(sampleX, sampleY, sampleParams,
                            out int zone, out bool isBorder, out bool inside);

                        if (!inside)
                            continue;

                        insideCount++;
                        if (isBorder || zone < 0)
                        {
                            borderCount++;
                        }
                        else
                        {
                            if (votes[zone] == 0)
                                touched.Add(zone);
                            votes[zone]++;
                        }
                    }
                }

                int index = py * res + px;
                int sampleTotal = samples * samples;

                if (insideCount == 0)
                {
                    map.zoneIndex[index] = 255;
                    map.borderCoverage[index] = 0;
                    map.alpha[index] = 0;
                    continue;
                }

                int bestZone = 0;
                int bestVotes = 0;
                for (int t = 0; t < touched.Count; t++)
                {
                    int zone = touched[t];
                    if (votes[zone] > bestVotes)
                    {
                        bestVotes = votes[zone];
                        bestZone = zone;
                    }
                    votes[zone] = 0;
                }

                map.zoneIndex[index] = (byte)bestZone;
                map.borderCoverage[index] = (byte)Mathf.RoundToInt(255f * borderCount / insideCount);
                map.alpha[index] = (byte)Mathf.RoundToInt(255f * insideCount / sampleTotal);
            }
        }

        return map;
    }

    private static void SamplePoint(float x, float y, SampleParams p,
        out int zone, out bool isBorder, out bool inside)
    {
        float r = Mathf.Sqrt(x * x + y * y);

        if (r > p.rimEnd)
        {
            zone = -1;
            isBorder = false;
            inside = false;
            return;
        }

        inside = true;

        // Outer ring (Rim) is completely rim.
        if (r >= p.outer)
        {
            zone = -1;
            isBorder = true;
            return;
        }

        if (r < p.bullInner)
        {
            zone = 0; // D25
            isBorder = Mathf.Abs(r - p.bullInner) <= p.borderHalf;
            return;
        }

        if (r < p.bullOuter)
        {
            zone = 1; // 25
            isBorder = Mathf.Abs(r - p.bullInner) <= p.borderHalf
                || Mathf.Abs(r - p.bullOuter) <= p.borderHalf;
            return;
        }

        bool onBorder =
            Mathf.Abs(r - p.bullOuter) <= p.borderHalf
            || Mathf.Abs(r - p.tripleInner) <= p.borderHalf
            || Mathf.Abs(r - p.tripleOuter) <= p.borderHalf
            || Mathf.Abs(r - p.doubleInner) <= p.borderHalf;

        float angle = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
        if (angle < 0f)
            angle += 360f;

        int numberCount = DartboardGeometry.NumberOrder.Length;
        int sector = Mathf.RoundToInt(angle / DartboardGeometry.SegmentAngle);
        sector %= numberCount;
        if (sector < 0)
            sector += numberCount;
        int number = DartboardGeometry.NumberOrder[sector];

        // Distance to the next sector edge (edges are 9° + n*18°).
        float halfSegment = DartboardGeometry.SegmentAngle * 0.5f;
        float nearestBoundary =
            Mathf.Round((angle - halfSegment) / DartboardGeometry.SegmentAngle)
            * DartboardGeometry.SegmentAngle
            + halfSegment;
        float angularDistanceDeg = Mathf.Abs(angle - nearestBoundary);
        float perpendicularDistance = r * angularDistanceDeg * Mathf.Deg2Rad;

        if (perpendicularDistance <= p.borderHalf)
            onBorder = true;

        DartboardGeometry.ZoneKind kind;
        if (r < p.tripleInner)
            kind = DartboardGeometry.ZoneKind.InnerSingle;
        else if (r < p.tripleOuter)
            kind = DartboardGeometry.ZoneKind.Triple;
        else if (r < p.doubleInner)
            kind = DartboardGeometry.ZoneKind.OuterSingle;
        else
            kind = DartboardGeometry.ZoneKind.Double;

        zone = ZoneIndex(number, kind);
        isBorder = onBorder;
    }

    private static int ZoneIndex(int number, DartboardGeometry.ZoneKind kind)
    {
        if (number == 25)
            return kind == DartboardGeometry.ZoneKind.Bull ? 0 : 1;

        int offset;
        switch (kind)
        {
            case DartboardGeometry.ZoneKind.InnerSingle: offset = 0; break;
            case DartboardGeometry.ZoneKind.Triple: offset = 1; break;
            case DartboardGeometry.ZoneKind.OuterSingle: offset = 2; break;
            case DartboardGeometry.ZoneKind.Double: offset = 3; break;
            default: offset = 0; break;
        }

        return 2 + (number - 1) * 4 + offset;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (heatTexture != null)
        {
            if (Application.isPlaying)
                Destroy(heatTexture);
            else
                DestroyImmediate(heatTexture);
            heatTexture = null;
        }
    }

    // ---------------------------------------------------------
    // LABELS
    // ---------------------------------------------------------

    private void EnsureLabels()
    {
        if (labelsBuilt)
            return;
        labelsBuilt = true;

        ResolveLabelFont();

        if (showNumberLabels)
        {
            for (int i = 0; i < DartboardGeometry.NumberOrder.Length; i++)
            {
                var tmp = CreateLabel("Num_" + DartboardGeometry.NumberOrder[i], 30);
                tmp.text = DartboardGeometry.NumberOrder[i].ToString();
                tmp.fontStyle = FontStyles.Bold;
                numberLabels.Add(tmp);
            }
        }

        if (missLabel == null)
        {
            missLabel = CreateLabel("MissLabel", 35);
            missLabel.alignment = TextAlignmentOptions.Right;
            missLabelAutoCreated = true;
            SetupMissLocalization(missLabel);
        }

        EnsureLabelTheme(missLabel);
        UpdateMissLabel();
    }

    private static void EnsureLabelTheme(TMP_Text label)
    {
        if (label == null || label.GetComponent<ThemeColorBinding>() != null)
            return;

        var binding = label.gameObject.AddComponent<ThemeColorBinding>();
        binding.SetRole(ThemeColorRole.Text3);
    }

    /// <summary>
    /// Hangs the "Miss" label on the StringTable (Key "Miss", table)
    /// "LocalizationTable". Pattern like HistoryScreen: set listener BEFORE
    /// setting the reference, build text from localized base string + count.
    /// Language changes then update automatically.
    /// </summary>
    private void SetupMissLocalization(TMP_Text tmp)
    {
        var go = tmp.gameObject;
        var localizeEvent = go.GetComponent<LocalizeStringEvent>()
            ?? go.AddComponent<LocalizeStringEvent>();

        localizeEvent.OnUpdateString.RemoveAllListeners();
        localizeEvent.OnUpdateString.AddListener(localizedValue =>
        {
            if (!string.IsNullOrEmpty(localizedValue))
                missBaseText = localizedValue;
            UpdateMissLabel();
        });
        localizeEvent.StringReference = new LocalizedString("LocalizationTable", "Miss");
        localizeEvent.RefreshString();
    }

    /// <summary>
    /// Font resolution: assigned font → headline font of the chart (roboto)
    /// → TMP standard. Then apply to all self-produced labels.
    /// </summary>
    public void RefreshLabelFont()
    {
        ResolveLabelFont();
        ApplyLabelFont();
    }

    private void ResolveLabelFont()
    {
        if (labelFont != null)
            return;

        var chart = GetComponentInParent<HitSectorChart>();
        if (chart != null && chart.headline != null && chart.headline.font != null)
            labelFont = chart.headline.font;
    }

    private void ApplyLabelFont()
    {
        if (labelFont == null)
            return;

        foreach (var label in numberLabels)
        {
            if (label != null)
                label.font = labelFont;
        }

        if (missLabelAutoCreated && missLabel != null)
            missLabel.font = labelFont;
    }

    private TMP_Text CreateLabel(string goName, int fontSize)
    {
        // CanvasRenderer also explicitly (see EnsureHeatmapView).
        var go = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (labelFont != null)
            tmp.font = labelFont;

        EnsureLabelTheme(tmp);

        return tmp;
    }

    private void LayoutLabels()
    {
        if (!labelsBuilt)
            return;

        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width, rect.height) * 0.5f * 0.80f;
        Vector2 center = rect.center;

        // Numbers sit outside the double (Radius 1.14 in board units,
        // Board itself goes up 1.0 — fits into the 0.80 scaling edge.
        for (int i = 0; i < numberLabels.Count && i < DartboardGeometry.NumberOrder.Length; i++)
        {
            var label = numberLabels[i];
            if (label == null)
                continue;

            float angle = DartboardGeometry.GetSegmentCenterAngle(DartboardGeometry.NumberOrder[i]);
            Vector2 boardPos = Polar(angle, 1.14f);
            var rt = label.rectTransform;
            rt.anchoredPosition = new Vector2(
                center.x - rect.center.x + boardPos.x * scale,
                center.y - rect.center.y + boardPos.y * scale);
            rt.sizeDelta = new Vector2(60f, 40f);
        }

        if (missLabel != null)
        {
            // To the left of the window, at the height of the board center: a row
            // with the 9 o'clock number, but further out (number ring + distance),
            // so that neither board nor numbers are overlapped.
            const float labelW = 260f;
            float x = -(scale * 1.14f + 30f + 40f + labelW * 0.5f);
            var rt = missLabel.rectTransform;
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(labelW, 48f);
        }
    }

    private static Vector2 Polar(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
    }

    private void UpdateMissLabel()
    {
        if (missLabel != null)
            missLabel.text = missCount > 0 ? missBaseText + ": " + missCount : "";
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        LayoutLabels();
    }
}
