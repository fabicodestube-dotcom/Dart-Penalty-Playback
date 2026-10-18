using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Dartscheiben-Heatmap als EIN UGUI-Mesh (ein Draw Call pro Scheibe).
/// Das Board wird einmal aus Dreiecken aufgebaut (Bull als Fächer,
/// Ringe als Quad-Strips); SetData() schreibt nur Vertex-Farben neu.
/// Jede Zone ist schwarz umrandet (schwarze Basis-Scheibe unter den Zonen,
/// Lücken lassen das Schwarz durchscheinen). Die Felder sind nach
/// Trefferhäufigkeit eingefärbt: weiß (nie) → grün → gelb → orange → rot.
/// Inneres + äußeres Single teilen sich einen Key ("n") und zeigen
/// bewusst dieselbe Farbe. Key "0" (daneben) wird als Text gezählt.
/// Farbskala ist relativ pro Spieler: max Count = rot.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DartboardHeatmap : MaskableGraphic
{
    [Header("Heat")]
    [Tooltip("Weiß (nie geworfen) → grün → gelb → orange → rot (am häufigsten).")]
    public Gradient heatGradient = CreateDefaultHeatGradient();

    [Header("Umrandung")]
    public Color borderColor = new Color(21f /255f, 21f /255f,21f /255f);
    [Tooltip("Breite des schwarzen Außenrings in Board-Einheiten (Double außen = 1).")]
    [Range(0f, 0.1f)]
    public float rimWidth = 0.035f;

    [Header("Board")]
    [Range(0f, 2f)]
    public float angularGapDeg = 0.8f;
    [Range(0f, 0.03f)]
    public float radialGap = 0.008f;
    [Range(8, 64)]
    public int bullSegments = 40;
    [Range(2, 12)]
    public int ringSteps = 5;

    [Header("Labels")]
    public bool showNumberLabels = true;
    public TMP_Text missLabel;
    [Tooltip("Font der Scheiben-Labels. Leer = wird vom Headline-Label des Charts übernommen (Roboto).")]
    public TMP_FontAsset labelFont;

    private readonly List<BuiltZone> builtZones = new List<BuiltZone>(82);
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private int maxCount;
    private int missCount;
    private readonly List<TMP_Text> numberLabels = new List<TMP_Text>(20);
    private bool labelsBuilt;
    private bool missLabelAutoCreated;
    private string missBaseText = "Miss";
    private readonly List<Vector2> scratchTris = new List<Vector2>(256);

    private struct BuiltZone
    {
        public DartboardGeometry.Zone zone;
        public List<Vector2> tris; // normiert: Zentrum = 0, Double außen = 1
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
        // Selbstheilung: falls dieses Objekt ohne CanvasRenderer existiert
        // (Prefab-Merge, manuell angelegt), explizit nachrüsten, BEVOR
        // Graphic-Maschinerie darauf zugreift.
        if (GetComponent<CanvasRenderer>() == null)
            gameObject.AddComponent<CanvasRenderer>();

        base.Awake();
        raycastTarget = false;
        BuildZones();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (builtZones.Count == 0)
            BuildZones();
        // Perf (M2): Labels (21 TMP + Lokalisierung) erst bei Bedarf bauen,
        // siehe EnsureHeatView(). Balken-Nutzer zahlen keine Mehrkosten.
    }

    /// <summary>
    /// Perf (M2): Baut Zahlen-/Miss-Labels beim ersten Anzeigen der Heatmap.
    /// Idempotent; nachfolgendes SetData() aktualisiert nur Farben/Text.
    /// </summary>
    public void EnsureHeatView()
    {
        EnsureLabels();
        LayoutLabels();
        SetVerticesDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        bullSegments = Mathf.Clamp(bullSegments, 8, 64);
        ringSteps = Mathf.Clamp(ringSteps, 2, 12);
        BuildZones();
        LayoutLabels();
        SetVerticesDirty();
    }
#endif

    /// <summary>Neue Zähldaten (gleiche Keys wie HitSectorChart). Nur Farben werden neu.</summary>
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
        SetVerticesDirty();
    }

    // ---------------------------------------------------------
    // MESH
    // ---------------------------------------------------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (builtZones.Count == 0)
            BuildZones();

        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width, rect.height) * 0.5f * 0.80f;
        if (scale <= 0f)
            return;

        Vector2 center = rect.center;
        UIVertex vert = UIVertex.simpleVert;

        // Schwarze Basis-Scheibe zuerst: Sie liegt unter allen Zonen und
        // scheint durch die Lücken hindurch → schwarze Umrandung jeder Zone
        // plus schwarzer Außenring. (UI-Material: ZWrite off, daher Maler-
        // Reihenfolge = Dreiecks-Reihenfolge, kein Z-Fighting.)
        EmitFan(vh, ref vert, center, scale,
            DartboardGeometry.DoubleOuterRadius + rimWidth, borderColor);

        foreach (var bz in builtZones)
        {
            int count = 0;
            counts.TryGetValue(bz.zone.sectorKey, out count);
            float t = maxCount > 0 ? count / (float)maxCount : 0f;
            Color c = heatGradient != null
                ? heatGradient.Evaluate(Mathf.Clamp01(t))
                : Color.white;

            EmitTris(vh, ref vert, center, scale, bz.tris, c);
        }
    }

    private void EmitFan(VertexHelper vh, ref UIVertex vert, Vector2 center,
        float scale, float radius, Color color)
    {
        scratchTris.Clear();
        AddDisc(scratchTris, 0f, radius, 48);
        EmitTris(vh, ref vert, center, scale, scratchTris, color);
    }

    private static void EmitTris(VertexHelper vh, ref UIVertex vert, Vector2 center,
        float scale, List<Vector2> tris, Color color)
    {
        for (int i = 0; i + 2 < tris.Count; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector2 p = tris[i + k];
                vert.position = new Vector3(center.x + p.x * scale, center.y + p.y * scale, 0f);
                vert.color = color;
                vert.uv0 = Vector2.zero;
                vh.AddVert(vert);
            }
            int baseIndex = vh.currentVertCount - 3;
            vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
        }
    }

    private void BuildZones()
    {
        builtZones.Clear();

        foreach (var zone in DartboardGeometry.BuildZones())
        {
            var bz = new BuiltZone
            {
                zone = zone,
                tris = new List<Vector2>()
            };

            switch (zone.kind)
            {
                case DartboardGeometry.ZoneKind.Bull:
                    AddDisc(bz.tris, 0f, DartboardGeometry.BullInnerRadius - radialGap, bullSegments);
                    break;
                case DartboardGeometry.ZoneKind.OuterBull:
                    AddRing(bz.tris, DartboardGeometry.BullInnerRadius + radialGap,
                        DartboardGeometry.BullOuterRadius - radialGap, 0f, 360f, bullSegments);
                    break;
                default:
                    float centerAngle = DartboardGeometry.GetSegmentCenterAngle(zone.number);
                    float half = DartboardGeometry.SegmentAngle * 0.5f - angularGapDeg * 0.5f;
                    float a0 = centerAngle - half;
                    float a1 = centerAngle + half;

                    switch (zone.kind)
                    {
                        case DartboardGeometry.ZoneKind.InnerSingle:
                            AddRing(bz.tris, DartboardGeometry.BullOuterRadius + radialGap,
                                DartboardGeometry.TripleInnerRadius - radialGap, a0, a1, ringSteps);
                            break;
                        case DartboardGeometry.ZoneKind.Triple:
                            AddRing(bz.tris, DartboardGeometry.TripleInnerRadius + radialGap,
                                DartboardGeometry.TripleOuterRadius - radialGap, a0, a1, ringSteps);
                            break;
                        case DartboardGeometry.ZoneKind.OuterSingle:
                            AddRing(bz.tris, DartboardGeometry.TripleOuterRadius + radialGap,
                                DartboardGeometry.DoubleInnerRadius - radialGap, a0, a1, ringSteps);
                            break;
                        case DartboardGeometry.ZoneKind.Double:
                            AddRing(bz.tris, DartboardGeometry.DoubleInnerRadius + radialGap,
                                DartboardGeometry.DoubleOuterRadius, a0, a1, ringSteps);
                            break;
                    }
                    break;
            }

            builtZones.Add(bz);
        }
    }

    // 0° = oben (12 Uhr), Winkel im Uhrzeigersinn.
    private static Vector2 Polar(float angleDeg, float radius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
    }

    private static void AddDisc(List<Vector2> tris, float r0, float r1, int segments)
    {
        if (r1 <= r0)
            return;

        Vector2 c = Vector2.zero;
        if (r0 <= 0f)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = 360f * i / segments;
                float a1 = 360f * (i + 1) / segments;
                tris.Add(c);
                tris.Add(Polar(a0, r1));
                tris.Add(Polar(a1, r1));
            }
        }
        else
        {
            AddRing(tris, r0, r1, 0f, 360f, segments);
        }
    }

    private static void AddRing(List<Vector2> tris, float r0, float r1, float a0, float a1, int steps)
    {
        if (r1 <= r0 || a1 <= a0)
            return;

        steps = Mathf.Max(1, steps);
        for (int i = 0; i < steps; i++)
        {
            float sa0 = Mathf.Lerp(a0, a1, i / (float)steps);
            float sa1 = Mathf.Lerp(a0, a1, (i + 1) / (float)steps);

            Vector2 p00 = Polar(sa0, r0);
            Vector2 p01 = Polar(sa0, r1);
            Vector2 p10 = Polar(sa1, r0);
            Vector2 p11 = Polar(sa1, r1);

            tris.Add(p00); tris.Add(p01); tris.Add(p11);
            tris.Add(p00); tris.Add(p11); tris.Add(p10);
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
        UpdateMissLabel();
    }

    /// <summary>
    /// Hängt das "Miss"-Label an die StringTable (Key "Miss", Tabelle
    /// "LocalizationTable"). Pattern wie HistoryHandler: Listener VOR
    /// Referenz setzen, Text aus lokalisiertem Basis-String + Count bauen.
    /// Sprachwechsel aktualisieren sich dadurch automatisch.
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
    /// Font-Auflösung: zugewiesener Font → Headline-Font des Charts (Roboto)
    /// → TMP-Standard. Danach auf alle selbst erzeugten Labels anwenden.
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
        // CanvasRenderer ebenfalls explizit (siehe EnsureHeatmapView).
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
        return tmp;
    }

    private void LayoutLabels()
    {
        if (!labelsBuilt)
            return;

        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width, rect.height) * 0.5f * 0.80f;
        Vector2 center = rect.center;

        // Zahlen sitzen außerhalb des Doubles (Radius 1.14 in Board-Einheiten,
        // Board selbst geht bis 1.0) — passt in den 0.80-Skalierungs-Rand.
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
            // Links neben der Scheibe, auf Höhe der Board-Mitte: eine Reihe
            // mit der 9-Uhr-Zahl, aber weiter außen (Zahlenring + Abstand),
            // damit weder Board noch Zahlen überlappt werden.
            const float labelW = 260f;
            float x = -(scale * 1.14f + 30f + 40f + labelW * 0.5f);
            var rt = missLabel.rectTransform;
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(labelW, 48f);
        }
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
