using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class StatisticsPageBuildHelper
{
    private static int batchDepth;
    private static readonly List<LayoutGroup> suspendedLayoutGroups = new List<LayoutGroup>();

    public static void BeginBatch(params Transform[] parents)
    {
        if (batchDepth++ > 0)
            return;

        suspendedLayoutGroups.Clear();

        foreach (var parent in parents)
        {
            if (parent == null)
                continue;

            var layout = parent.GetComponent<LayoutGroup>();
            if (layout != null && layout.enabled)
            {
                layout.enabled = false;
                suspendedLayoutGroups.Add(layout);
            }
        }
    }

    public static void EndBatch()
    {
        if (--batchDepth > 0)
            return;

        foreach (var layout in suspendedLayoutGroups)
        {
            if (layout != null)
                layout.enabled = true;
        }

        foreach (var layout in suspendedLayoutGroups)
        {
            if (layout != null)
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)layout.transform);
        }

        suspendedLayoutGroups.Clear();
    }

    public static LayoutGroup SuspendPageLayout(Transform pageParent)
    {
        if (pageParent == null)
            return null;

        var layout = pageParent.GetComponent<LayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        return layout;
    }

    public static void ApplyTablesOnParent(Transform pageParent, IEnumerable<TableView> tables)
    {
        if (pageParent == null || tables == null)
            return;

        foreach (var table in tables)
        {
            if (table != null && table.transform.parent == pageParent)
                table.ApplyLayout();
        }
    }

    public static void FinalizePage(Transform pageParent, LayoutGroup pageLayout, bool restoreActive)
    {
        if (pageParent == null)
            return;

        if (pageLayout != null)
            pageLayout.enabled = true;

        pageParent.gameObject.SetActive(restoreActive);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)pageParent);
    }
}
