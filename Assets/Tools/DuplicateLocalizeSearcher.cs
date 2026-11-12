#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Localization.Components; // Required for LocalizeStringEvent

public class DuplicateLocalizeSearcher : EditorWindow
{
    private List<GameObject> _foundObjects = new List<GameObject>();
    private Vector2 _scrollPosition;

    [MenuItem("Tools/Localization/Find Multiple Localize Events")]
    public static void ShowWindow()
    {
        GetWindow<DuplicateLocalizeSearcher>("Localize Finder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Sucher für mehrfache Localize String Events", EditorStyles.boldLabel);

        if (GUILayout.Button("Szene durchsuchen", GUILayout.Height(30)))
        {
            FindObjects();
        }

        EditorGUILayout.Space();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        if (_foundObjects.Count == 0)
        {
            GUILayout.Label("Keine GameObjects mit mehrfachen Events gefunden.", EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label($"Gefundene Objekte: {_foundObjects.Count}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (GameObject go in _foundObjects)
            {
                // If the object has been deleted, skip
                if (go == null) continue;

                // A clickable button that looks like text/link
                GUIStyle linkStyle = new GUIStyle(EditorStyles.linkLabel);
                linkStyle.wordWrap = true;

                if (GUILayout.Button($"• {go.name} (Pfad: {GetGameObjectPath(go)})", linkStyle))
                {
                    // Ping in the hierarchy and selection in the inspector
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                }
                
                EditorGUILayout.Space(2);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void FindObjects()
    {
        _foundObjects.Clear();

        // Searches all GameObjects in the current scene (even inactive)
        LocalizeStringEvent[] allEvents = Resources.FindObjectsOfTypeAll<LocalizeStringEvent>();

        // Dictionary to count how often an GameObject occurs
        Dictionary<GameObject, int> counts = new Dictionary<GameObject, int>();

        foreach (var ev in allEvents)
        {
            // Ensure that the object belongs to the scene and there is no asset/prefab in the project folder
            if (ev.gameObject.scene.name == null) continue;

            if (counts.ContainsKey(ev.gameObject))
            {
                counts[ev.gameObject]++;
            }
            else
            {
                counts[ev.gameObject] = 1;
            }
        }

        // Filter: Add objects with 2 or more components only
        foreach (var pair in counts)
        {
            if (pair.Value >= 2)
            {
                _foundObjects.Add(pair.Key);
            }
        }
    }

    // Help function to show the exact path in the hierarchy
    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = obj.name + "/" + path;
        }
        return path;
    }
}


#endif