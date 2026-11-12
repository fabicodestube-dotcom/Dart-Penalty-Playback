using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HitSectorChart : MonoBehaviour
{
    [Header("Headline")]
    public TMP_Text headline;

    [Header("ScrollView")]
    public ScrollRect scrollRect;
    public Transform content;

    [Header("Bars")]
    public List<HitSectorBar> bars = new List<HitSectorBar>();

    [Header("Y-Achsis Labels")]
    public TMP_Text[] yAxisLabels;

    [Header("Views (optional verdrahten)")]
    [Tooltip("Balken-Ansicht. Fällt automatisch auf ScrollRect/Content zurück. Wird von ShowBars()/ShowHeatmap() umgeschaltet.")]
    public GameObject barView;
    [Tooltip("Heatmap-Ansicht. Wird zur Laufzeit erzeugt, falls nicht zugewiesen.")]
    public DartboardHeatmap heatmapView;
    [Tooltip("Startansicht: Balken oder Heatmap.")]
    public bool startWithHeatmap = false;

    private readonly Dictionary<string, int> lastCounts = new Dictionary<string, int>();
    private bool heatmapActive;

    private void Awake()
    {
        heatmapActive = startWithHeatmap;
        // Perf (M1): Headline + Y-Labels sind nie interaktiv.
        if (headline != null && headline.raycastTarget)
            headline.raycastTarget = false;

        if (yAxisLabels != null)
        {
            foreach (var label in yAxisLabels)
            {
                if (label != null && label.raycastTarget)
                    label.raycastTarget = false;
            }
        }

        ResolveBarView();
        EnsureHeatmapView();
        ApplyView();
    }

    /// <summary>Button-Methode: Balken-Ansicht anzeigen (für OnClick verdrahten).</summary>
    public void ShowBars()
    {
        heatmapActive = false;
        ApplyView();
    }

    /// <summary>Button-Methode: Heatmap-Ansicht anzeigen (für OnClick verdrahten).</summary>
    public void ShowHeatmap()
    {
        heatmapActive = true;
        EnsureHeatmapView();
        if (heatmapView != null)
        {
            heatmapView.EnsureHeatView();
            heatmapView.SetData(lastCounts);
        }
        ApplyView();
    }

    private void ResolveBarView()
    {
        if (barView != null)
            return;

        // Primär: Balken-Container des Prefabs (heißt dort "Graphic").
        var graphicChild = transform.Find("Graphic") ?? transform.Find("Graphics");
        if (graphicChild != null)
        {
            barView = graphicChild.gameObject;
            return;
        }

        if (scrollRect != null)
            barView = scrollRect.gameObject;
        else if (content != null)
            barView = content.transform.parent != null ? content.transform.parent.gameObject : content.gameObject;
    }

    private void EnsureHeatmapView()
    {
        if (heatmapView != null)
        {
            EnsureRenderer(heatmapView.gameObject);
            return;
        }

        var existing = transform.Find("HeatmapView");
        GameObject go = existing != null ? existing.gameObject : null;

        if (go != null)
            heatmapView = go.GetComponent<DartboardHeatmap>();

        if (go == null)
        {
            // CanvasRenderer steht explizit in der Liste: RequireComponent-
            // Heilung per AddComponent greift nicht in jedem Pfad
            // (Prefab-Merge, manuell angelegte Objekte).
            go = new GameObject("HeatmapView", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;
            go.SetActive(false);
        }

        EnsureRenderer(go);

        if (heatmapView == null)
            heatmapView = go.AddComponent<DartboardHeatmap>();

        EnsureRenderer(go);
    }

    private static void EnsureRenderer(GameObject go)
    {
        if (go != null && go.GetComponent<CanvasRenderer>() == null)
            go.AddComponent<CanvasRenderer>();
    }

    private void ApplyView()
    {
        if (barView != null)
            barView.SetActive(!heatmapActive);

        if (heatmapView != null && heatmapView.gameObject != null)
            heatmapView.gameObject.SetActive(heatmapActive);
    }

    public void SetData(string playerName, IReadOnlyDictionary<string, int> counts)
    {
        lastCounts.Clear();
        if (counts != null)
        {
            foreach (var pair in counts)
                lastCounts[pair.Key] = pair.Value;
        }

        // -------------------------------------------------
        // Name setzen
        // -------------------------------------------------
        if (headline != null)
        {
            headline.text = playerName;
        }

        // -------------------------------------------------
        // Maximum bestimmen
        // -------------------------------------------------

        int max = 0;

        if (counts != null)
        {
            foreach (var pair in counts)
            {
                max = Mathf.Max(max, pair.Value);
            }
        }

        // -------------------------------------------------
        // Auf nächste durch 4 teilbare Zahl aufrunden
        // -------------------------------------------------

        int axisMax = Mathf.Max(4, max);

        while (axisMax % 4 != 0)
            axisMax++;

        // -------------------------------------------------
        // Y-Achsen Labels setzen
        // -------------------------------------------------

        if (yAxisLabels != null && yAxisLabels.Length >= 5)
        {
            for (int i = 0; i < 5; i++)
            {
                int tickValue = (axisMax / 4) * i;
                yAxisLabels[i].text = tickValue.ToString();
            }
        }

        // Balken (Prefab-Objekte) füttern.
        if (bars == null || bars.Count == 0)
        {
            ForwardHeatmap(counts);
            return;
        }

        float sharedAreaHeight = ResolveSharedBarAreaHeight();

        foreach (var bar in bars)
        {
            if (bar == null)
                continue;

            int value = 0;

            if (counts != null)
                counts.TryGetValue(bar.sectorKey, out value);

            float normalized =
                axisMax <= 0
                    ? 0f
                    : value / (float)axisMax;

            bar.SetData(value, normalized, sharedAreaHeight);
        }

        ForwardHeatmap(counts);
    }

    private void ForwardHeatmap(IReadOnlyDictionary<string, int> counts)
    {
        if (heatmapView != null)
        {
            if (heatmapActive)
                heatmapView.EnsureHeatView();
            heatmapView.SetData(counts);
        }
    }

    private float ResolveSharedBarAreaHeight()
    {
        foreach (var bar in bars)
        {
            if (bar?.barArea == null)
                continue;

            float height = bar.barArea.rect.height;
            if (height > 0f)
                return height;

            height = bar.barArea.sizeDelta.y;
            if (height > 0f)
                return height;
        }

        return 200f;
    }

}

