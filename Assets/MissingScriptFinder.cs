using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MissingScriptFinder
{
#if UNITY_EDITOR
    [MenuItem("Tools/Find Missing Scripts in Scene")]
    public static void FindMissing()
    {
        var allGameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        int count = 0;

        foreach (var go in allGameObjects)
        {
            var components = go.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    Debug.LogError("Missing Script found on: " + go.name, go);
                    count++;
                }
            }
        }

        Debug.Log("Done. Missing scripts found: " + count);
    }
#endif
}