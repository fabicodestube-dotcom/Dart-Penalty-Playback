using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Baut das ATC-Diagramm "Versuche pro Feld" auf Basis des X01-Hit-Sektor-Prefabs:
/// instanziieren, Bars auf die ATC-Keys (Singles, Doubles, Triples) beschneiden,
/// Daten setzen. Analog zu X01: Single "20", Double "D20", Triple "T20" (kein T25).
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
        return keys; // 63 Keys, kein T25 (analog X01-Prefab)
    }

    /// <summary>Instanziert ein beschnittenes Chart und setzt die Daten.</summary>
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

    /// <summary>Beschneidet ein (ggf. wiederverwendetes) Chart und setzt die Daten neu.</summary>
    public static void SetupChart(HitSectorChart chart, string playerName, IReadOnlyDictionary<string, int> sectorCounts)
    {
        if (chart == null)
            return;

        PruneBars(chart);
        chart.gameObject.SetActive(true);
        // Neuer Spieler ohne Stats -> leeres Dict statt null (SetData iteriert darüber)
        chart.SetData(playerName, sectorCounts ?? new Dictionary<string, int>());
    }

    /// <summary>
    /// Bars außerhalb der ATC-Keys werden nur deaktiviert, nicht zerstört
    /// (Destroy = verzögerter Rebuild + GC). Optisch identisch zur alten Lösung.
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
