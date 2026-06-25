using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;

#if UNITY_EDITOR
public class FontFilterAsset : EditorWindow
{
    
    private const string TARGET_FONT_NAME = "Roboto-Regular SDF";

    [MenuItem("Tools/Find Wrong Fonts")]
    public static void FindWrongFonts()
    {
        Debug.Log("<b>[Font Scanner]</b> Starte Suche nach falschen Fonts...");
        int totalFound = 0;

        // 1. Suche in der aktuellen Szene
        TMP_Text[] sceneTexts = GameObject.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var txt in sceneTexts)
        {
            if (txt.font == null || txt.font.name != TARGET_FONT_NAME)
            {
                string fontName = txt.font != null ? txt.font.name : "KEINE FONT";
                Debug.LogWarning($"[SZENE] Falsche Font ({fontName}) auf GO: '{GetHierarchyPath(txt.transform)}'", txt.gameObject);
                totalFound++;
            }
        }

        // 2. Suche in allen Prefabs im Projekt-Ordner
        string[] allPrefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in allPrefabGUIDs)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            
            if (prefabGO != null)
            {
                TMP_Text[] prefabTexts = prefabGO.GetComponentsInChildren<TMP_Text>(true);
                foreach (var txt in prefabTexts)
                {
                    if (txt.font == null || txt.font.name != TARGET_FONT_NAME)
                    {
                        string fontName = txt.font != null ? txt.font.name : "KEINE FONT";
                        Debug.LogWarning($"[PREFAB] Falsche Font ({fontName}) in Asset: '{assetPath}' auf Child: '{txt.name}'", prefabGO);
                        totalFound++;
                    }
                }
            }
        }

        Debug.Log($"<b>[Font Scanner]</b> Suche beendet. Insgesamt {totalFound} Felder mit abweichenden Fonts gefunden.");
    }

    // Hilfsmethode, um den vollen Pfad in der Hierarchie anzuzeigen
    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
    
}

#endif
