using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the ATC diagram "Trials per field" based on the X01 hit sector prefab:
/// instantiate, cut bars to the ATC keys (Singles, Doubles, Triples),
/// Analogous to X01: single "20", double "D20", triple "T20" (no T25).
/// </summary>
public static class ATCAttemptsChart
{
    private static readonly HashSet<string> ATCKeys = BuildATCKeys();

    private static HashSet<string> BuildATCKeys()
    {
        var keys = new HashSet<string> { "0" };
        for (int i = 1; i <= 20; i++)
        {
            keys.Add(i.ToString());
            keys.Add("D" + i);
            keys.Add("T" + i);
        }
        keys.Add("25");
        keys.Add("D25");
        return keys; // 63 Keys, no T25 (analog X01-Prefab)
    }

    /// <summary>Directs a cropped chart and sets the data.</summary>
    public static HitSectorChart BuildChart(GameObject prefabHitSectorChart, Transform parent,
        string playerName, IReadOnlyDictionary<string, int> sectorCounts)
    {
        if (prefabHitSectorChart == null || parent == null)
            return null;

        var chart = Object.Instantiate(prefabHitSectorChart, parent).GetComponent<HitSectorChart>();
        if (chart == null)
        {
            Debug.LogError("prefabHitSectorChart has no HitSectorChart component");
            return null;
        }

        SetupChart(chart, playerName, sectorCounts);
        return chart;
    }

    /// <summary>crops a (possibly reused) chart and resets the data.</summary>
    public static void SetupChart(HitSectorChart chart, string playerName, IReadOnlyDictionary<string, int> sectorCounts)
    {
        if (chart == null)
            return;

        PruneBars(chart);
        chart.gameObject.SetActive(true);
        // New player without stats -> empty dict instead of null (SetData iterates over it)
        chart.SetData(playerName, sectorCounts ?? new Dictionary<string, int>());
    }

    /// <summary>
    /// Bars outside the ATC keys are only deactivated, not destroyed
    /// (Destroy = delayed rebuild + GC). Visually identical to the old solution.
    /// </summary>
    private static void PruneBars(HitSectorChart chart)
    {
        if (chart.bars == null)
            return;

        foreach (var bar in chart.bars)
        {
            if (bar == null)
                continue;

            bool keep = ATCKeys.Contains(bar.sectorKey);
            if (bar.gameObject.activeSelf != keep)
                bar.gameObject.SetActive(keep);
        }
    }
}
