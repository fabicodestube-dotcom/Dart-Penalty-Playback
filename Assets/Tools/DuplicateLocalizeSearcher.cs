#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Localization.Components; // Benötigt für LocalizeStringEvent

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
                // Falls das Objekt gelöscht wurde, überspringen
                if (go == null) continue;

                // Ein klickbarer Button, der aussieht wie Text/Link
                GUIStyle linkStyle = new GUIStyle(EditorStyles.linkLabel);
                linkStyle.wordWrap = true;

                if (GUILayout.Button($"• {go.name} (Pfad: {GetGameObjectPath(go)})", linkStyle))
                {
                    // Ping in der Hierarchie und Auswahl im Inspector
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

        // Durchsucht alle GameObjects in der aktuellen Szene (auch inaktive)
        LocalizeStringEvent[] allEvents = Resources.FindObjectsOfTypeAll<LocalizeStringEvent>();

        // Dictionary, um zu zählen, wie oft ein GameObject vorkommt
        Dictionary<GameObject, int> counts = new Dictionary<GameObject, int>();

        foreach (var ev in allEvents)
        {
            // Sicherstellen, dass das Objekt zur Szene gehört und kein Asset/Prefab im Projektordner ist
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

        // Filtern: Nur Objekte mit 2 oder mehr Komponenten hinzufügen
        foreach (var pair in counts)
        {
            if (pair.Value >= 2)
            {
                _foundObjects.Add(pair.Key);
            }
        }
    }

    // Hilfsfunktion, um den genauen Pfad in der Hierarchie anzuzeigen
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