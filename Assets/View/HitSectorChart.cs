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


    public void SetData(string playerName, IReadOnlyDictionary<string, int> counts)
    {
        if (bars == null || bars.Count == 0)
            return;

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

        foreach (var pair in counts)
        {
            max = Mathf.Max(max, pair.Value);
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

