using UnityEngine;
using UnityEditor;
using TMPro;

public static class FindCancelTypos
{
    #if UNITY_EDITOR
    [MenuItem("Tools/Find TMP Text 'Cancle'")]
    public static void FindTypos()
    {
        TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int count = 0;

        foreach (TMP_Text tmp in texts)
        {
            if (tmp.text.Contains("Cancle"))
            {
                count++;

                Debug.Log(
                    $"Found 'Cancle' on GameObject '{tmp.gameObject.name}'",
                    tmp.gameObject);
            }
        }

        Debug.Log($"Search complete. Found {count} TMP texts containing 'Cancle'.");
    }
    #endif
}