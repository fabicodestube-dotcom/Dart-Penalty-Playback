#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;

public class TMPTextSearchWindow : EditorWindow
{
    private string searchTerm = "Cancle";
    private bool matchCase = true;

    [MenuItem("Tools/TMP Text Searcher")]
    public static void ShowWindow()
    {
        GetWindow<TMPTextSearchWindow>("TMP Search");
    }

    private void OnGUI()
    {
        GUILayout.Label("TMP Text Sucher & Selektor", EditorStyles.boldLabel);
        
        searchTerm = EditorGUILayout.TextField("Suchbegriff:", searchTerm);
        matchCase = EditorGUILayout.Toggle("Groß-/Kleinschreibung", matchCase);

        GUILayout.Space(10);

        if (GUILayout.Button("Suchen & Selektieren", GUILayout.Height(30)))
        {
            ExecuteSearchAndSelect();
        }
    }

    private void ExecuteSearchAndSelect()
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            Debug.LogWarning("Bitte gib zuerst einen Suchbegriff ein.");
            return;
        }

        TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        List<GameObject> foundObjects = new List<GameObject>();
        string comparisonTerm = matchCase ? searchTerm : searchTerm.ToLower();

        foreach (TMP_Text tmp in texts)
        {
            string textToSearch = matchCase ? tmp.text : tmp.text.ToLower();

            if (textToSearch.Contains(comparisonTerm))
            {
                foundObjects.Add(tmp.gameObject);
            }
        }

        if (foundObjects.Count > 0)
        {
            // Setzt die aktive Auswahl im Unity-Editor auf alle gefundenen GameObjects
            Selection.objects = foundObjects.ToArray();
            
            Debug.Log($"Suche abgeschlossen. {foundObjects.Count} Objekte selektiert. Du kannst jetzt die Localization-Komponente im Inspector hinzufügen!");
        }
        else
        {
            Debug.LogWarning($"Keine TMP-Texte mit dem Begriff '{searchTerm}' gefunden.");
            Selection.objects = new Object[0]; // Auswahl leeren, wenn nichts gefunden wurde
        }
    }
}


#endif