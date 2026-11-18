using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VectorGraphics;
using Unity.VectorGraphics.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Scene = UnityEngine.SceneManagement.Scene;

public static class SvgIconMigration
{
    private const string ScenePath = "Assets/Scenes/App.unity";
    private const string GraphicsFolder = "Assets/Graphics";
    private const string SvgImageScriptGuid = "ef8b96895a0ec4ba685178a052544251";
    private const string SvgImageClassId = "Unity.VectorGraphics::Unity.VectorGraphics.SVGImage";
    private const string MarkerPath = "Temp/svg-icon-migration.pending";
    private const int TargetResolution = 2048;

    private static readonly HashSet<string> Exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Rounded40px@4x.svg",
        "clipart_outline_sprite.svg",
        "clipart_dartboard.svg"
    };

    private static readonly string[] CellPrefabs =
    {
        "Assets/UI/Prefabs/CellBody.prefab",
        "Assets/UI/Prefabs/CellHeader.prefab"
    };

    [MenuItem("Tools/Graphics/SVG Migration/Dry Run")]
    public static void DryRun()
    {
        Run(false, true);
    }

    [MenuItem("Tools/Graphics/SVG Migration/Apply")]
    public static void Apply()
    {
        Run(true, true);
    }

    [MenuItem("Tools/Graphics/SVG Migration/Validate")]
    public static void Validate()
    {
        var scene = FindOpenScene(ScenePath);
        bool opened = false;
        if (!scene.IsValid())
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        var report = BuildReport();
        Debug.Log($"[SvgIconMigration] Validation: eligibleSvgs={report.SvgPaths.Count}, remainingEligibleImages={report.RemainingImages}, existingSvgImages={report.ExistingSvgImages}, exceptions={report.ExceptionImages}");
        ValidateReferences();

        if (opened)
            EditorSceneManager.CloseScene(scene, true);
    }

    [MenuItem("Tools/Graphics/SVG Migration/Validate References")]
    public static void ValidateReferences()
    {
        int broken = 0;
        foreach (var prefabPath in FindPrefabPaths())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                continue;
            broken += ValidateReferences(prefab, prefabPath);
        }

        var scene = FindOpenScene(ScenePath);
        bool opened = false;
        if (!scene.IsValid())
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        if (scene.IsValid())
        {
            foreach (var root in scene.GetRootGameObjects())
                broken += ValidateReferences(root, ScenePath);
        }

        if (opened)
            EditorSceneManager.CloseScene(scene, true);

        Debug.Log($"[SvgIconMigration] Reference validation: broken={broken}");
    }

    private static int ValidateReferences(GameObject root, string source)
    {
        int broken = 0;
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            var serialized = new SerializedObject(behaviour);
            var iterator = serialized.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                if (iterator.objectReferenceValue != null || iterator.objectReferenceInstanceIDValue == 0)
                    continue;

#pragma warning disable 0618
                var referenced = EditorUtility.InstanceIDToObject(iterator.objectReferenceInstanceIDValue);
#pragma warning restore 0618
                if (referenced is not SVGImage && referenced is not Image)
                    continue;

                broken++;
                if (broken <= 50)
                    Debug.LogError($"[SvgIconMigration] Broken reference: {source} / {behaviour.GetType().Name} / {iterator.propertyPath} / {referenced.GetType().Name}");
            }
        }

        broken += ValidateRequiredReferences(root, source);
        return broken;
    }

    private static int ValidateRequiredReferences(GameObject root, string source)
    {
        int broken = 0;
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            var serialized = new SerializedObject(behaviour);
            switch (behaviour)
            {
                case MenuSwitcher:
                    foreach (string field in new[] { "game.image", "players.image", "customization.image", "history.image", "statistics.image" })
                        ValidateRequiredReference(serialized, field, 1, source, behaviour, ref broken);
                    break;
                case RippleAnimation:
                    ValidateRequiredReference(serialized, "ripplePrefab", 1, source, behaviour, ref broken);
                    break;
                case SetupScreen:
                    ValidateRequiredReference(serialized, "botSkillIcon", 1, source, behaviour, ref broken);
                    break;
                case AudioToggleButton:
                    ValidateRequiredReference(serialized, "buttonImage", 1, source, behaviour, ref broken);
                    break;
                case InputHandler:
                    ValidateRequiredReference(serialized, "botIcon", 1, source, behaviour, ref broken);
                    break;
                case BotItem:
                    ValidateRequiredReference(serialized, "botImage", 1, source, behaviour, ref broken);
                    break;
                case CricketPlayerListItem:
                    ValidateRequiredArraySize(serialized, "numberCells", 7, source, behaviour, ref broken);
                    for (int i = 0; i < 7; i++)
                        ValidateRequiredReference(serialized, $"numberCells.Array.data[{i}].hitImages", 3, source, behaviour, ref broken);
                    ValidateRequiredReference(serialized, "wallImages", 3, source, behaviour, ref broken);
                    ValidateRequiredReference(serialized, "ceilingImages", 3, source, behaviour, ref broken);
                    break;
                case ATCPlayerListItem:
                    ValidateRequiredReference(serialized, "hitImages", 3, source, behaviour, ref broken);
                    ValidateRequiredReference(serialized, "missImages", 3, source, behaviour, ref broken);
                    ValidateRequiredReference(serialized, "wallImages", 3, source, behaviour, ref broken);
                    ValidateRequiredReference(serialized, "ceilingImages", 3, source, behaviour, ref broken);
                    break;
                case X01PlayerItem:
                    ValidateRequiredReference(serialized, "wallImages", 3, source, behaviour, ref broken);
                    ValidateRequiredReference(serialized, "ceilingImages", 3, source, behaviour, ref broken);
                    break;
            }
        }

        return broken;
    }

    private static void ValidateRequiredArraySize(
        SerializedObject serialized,
        string path,
        int expectedArraySize,
        string source,
        MonoBehaviour behaviour,
        ref int broken)
    {
        var property = serialized.FindProperty(path);
        if (property == null || !property.isArray)
        {
            broken++;
            Debug.LogError($"[SvgIconMigration] Missing array: {source} / {behaviour.GetType().Name} / {path}");
            return;
        }

        if (property.arraySize != expectedArraySize)
        {
            broken++;
            Debug.LogError($"[SvgIconMigration] Wrong array size: {source} / {behaviour.GetType().Name} / {path} / expected={expectedArraySize}, actual={property.arraySize}");
        }
    }

    private static void ValidateRequiredReference(
        SerializedObject serialized,
        string path,
        int expectedArraySize,
        string source,
        MonoBehaviour behaviour,
        ref int broken)
    {
        var property = serialized.FindProperty(path);
        if (property == null)
        {
            broken++;
            Debug.LogError($"[SvgIconMigration] Missing property: {source} / {behaviour.GetType().Name} / {path}");
            return;
        }

        if (property.isArray)
        {
            if (property.arraySize != expectedArraySize)
            {
                broken++;
                Debug.LogError($"[SvgIconMigration] Wrong array size: {source} / {behaviour.GetType().Name} / {path} / expected={expectedArraySize}, actual={property.arraySize}");
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue is not SVGImage)
                {
                    broken++;
                    Debug.LogError($"[SvgIconMigration] Missing SVG reference: {source} / {behaviour.GetType().Name} / {path}[{i}]");
                }
            }

            return;
        }

        if (property.objectReferenceValue is not SVGImage)
        {
            broken++;
            Debug.LogError($"[SvgIconMigration] Missing SVG reference: {source} / {behaviour.GetType().Name} / {path}");
        }
    }

    public static void BatchDryRun()
    {
        bool ok = Run(false, false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    public static void BatchApply()
    {
        bool ok = Run(true, false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [InitializeOnLoadMethod]
    private static void CheckPendingMarker()
    {
        if (!File.Exists(MarkerPath))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (Run(true, false))
            {
                try { File.Delete(MarkerPath); }
                catch (Exception e) { Debug.LogWarning($"[SvgIconMigration] Could not delete marker: {e.Message}"); }
            }
        };
    }

    private static bool Run(bool apply, bool interactive)
    {
        var svgPaths = GetSvgPaths();
        var pngToSvg = BuildPngToSvgMap(svgPaths);
        var svgNameToPath = svgPaths.ToDictionary(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        Debug.Log($"[SvgIconMigration] {(apply ? "Apply" : "Dry run")}: svgFiles={svgPaths.Count}, pngMappings={pngToSvg.Count}");

        if (svgPaths.Count == 0 || pngToSvg.Count == 0)
        {
            Debug.LogError("[SvgIconMigration] No SVG/PNG mappings found.");
            return false;
        }

        Scene appScene = FindOpenScene(ScenePath);
        if (apply && !interactive && appScene.IsValid() && appScene.isDirty)
        {
            Debug.LogError("[SvgIconMigration] App.unity has unsaved changes. Save it and run Tools/Graphics/SVG Migration/Apply.");
            return false;
        }

        if (apply && interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        if (apply)
        {
            int imported = ApplyImportSettings(svgPaths);
            Debug.Log($"[SvgIconMigration] Import settings updated: {imported}");
            AssetDatabase.Refresh();
        }

        var svgSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        if (apply)
        {
            foreach (var path in svgPaths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    svgSprites[path] = sprite;
                else
                    Debug.LogError($"[SvgIconMigration] Missing Sprite after import: {path}");
            }

            if (svgSprites.Count != svgPaths.Count)
                return false;
        }

        var svgScript = LoadSvgImageScript();
        if (svgScript == null)
        {
            Debug.LogError("[SvgIconMigration] SVGImage MonoScript not found.");
            return false;
        }

        int prefabImages = 0;
        int prefabSvgImages = 0;
        int prefabAssets = 0;
        foreach (var prefabPath in FindPrefabPaths())
        {
            bool isCellPrefab = CellPrefabs.Contains(prefabPath, StringComparer.OrdinalIgnoreCase);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                continue;

            var images = prefab.GetComponentsInChildren<Image>(true);
            var targets = images.Where(image => isCellPrefab || IsMappedSprite(image.sprite, pngToSvg)).ToArray();
            var existingSvgImages = prefab.GetComponentsInChildren<SVGImage>(true);

            if (targets.Length == 0 && existingSvgImages.Length == 0)
                continue;

            prefabAssets++;
            prefabImages += targets.Length;
            prefabSvgImages += existingSvgImages.Length;

            if (!apply)
                continue;

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var contentsImages = contents.GetComponentsInChildren<Image>(true);
                foreach (var image in contentsImages)
                {
                    if (!isCellPrefab && !IsMappedSprite(image.sprite, pngToSvg))
                        continue;

                    Sprite sprite = null;
                    if (image.sprite != null && pngToSvg.TryGetValue(AssetDatabase.GetAssetPath(image.sprite), out var svgPath))
                        svgSprites.TryGetValue(svgPath, out sprite);

                    ConvertImage(image, sprite, svgScript);
                }

                RefreshExistingSvgImages(contents.GetComponentsInChildren<SVGImage>(true), svgSprites);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        Debug.Log($"[SvgIconMigration] Prefabs: assets={prefabAssets}, images={prefabImages}, svgImages={prefabSvgImages}");

        if (apply)
            AssetDatabase.SaveAssets();

        bool openedScene = false;
        if (!appScene.IsValid())
        {
            appScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            openedScene = true;
        }

        if (!appScene.IsValid())
        {
            Debug.LogError($"[SvgIconMigration] Could not open {ScenePath}.");
            return false;
        }

        var roots = appScene.GetRootGameObjects();
        var sceneImages = roots.SelectMany(root => root.GetComponentsInChildren<Image>(true)).ToArray();
        var sceneTargets = sceneImages.Where(image => IsMappedSprite(image.sprite, pngToSvg)).ToArray();
        var sceneSvgImages = roots.SelectMany(root => root.GetComponentsInChildren<SVGImage>(true)).ToArray();

        Debug.Log($"[SvgIconMigration] Scene: images={sceneImages.Length}, targets={sceneTargets.Length}, existingSvgImages={sceneSvgImages.Length}");

        if (apply)
        {
            int converted = 0;
            foreach (var image in sceneTargets)
            {
                if (!pngToSvg.TryGetValue(AssetDatabase.GetAssetPath(image.sprite), out var svgPath))
                    continue;
                if (!svgSprites.TryGetValue(svgPath, out var sprite))
                    continue;

                if (ConvertImage(image, sprite, svgScript))
                    converted++;
            }

            int refreshed = RefreshExistingSvgImages(sceneSvgImages, svgSprites);
            int spriteFields = AssignSceneSpriteFields(roots, svgNameToPath, svgSprites);

            if (converted > 0 || refreshed > 0 || spriteFields > 0)
                EditorSceneManager.MarkSceneDirty(appScene);

            if (converted > 0 || refreshed > 0 || spriteFields > 0)
                EditorSceneManager.SaveScene(appScene);

            Debug.Log($"[SvgIconMigration] Scene applied: converted={converted}, refreshedSvgImages={refreshed}, spriteFields={spriteFields}");
        }

        if (openedScene)
            EditorSceneManager.CloseScene(appScene, true);

        var finalReport = BuildReport();
        Debug.Log($"[SvgIconMigration] Result: remainingEligibleImages={finalReport.RemainingImages}, existingSvgImages={finalReport.ExistingSvgImages}, exceptions={finalReport.ExceptionImages}");
        return true;
    }

    private static int ApplyImportSettings(IEnumerable<string> svgPaths)
    {
        int count = 0;
        foreach (var path in svgPaths)
        {
            if (AssetImporter.GetAtPath(path) is not SVGImporter importer)
            {
                Debug.LogError($"[SvgIconMigration] Not an SVGImporter: {path}");
                continue;
            }

            if (importer.SvgType == SVGType.UISVGImage &&
                importer.TargetResolution == TargetResolution)
                continue;

            importer.SvgType = SVGType.UISVGImage;
            importer.TargetResolution = TargetResolution;
            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(path);
            importer.SaveAndReimport();
            count++;
        }

        return count;
    }

    private static bool ConvertImage(Image image, Sprite sprite, MonoScript svgScript)
    {
        if (image == null || svgScript == null)
            return false;

        var go = image.gameObject;
        if (go.GetComponent<SVGImage>() != null)
            return false;

        bool preserveAspect = image.preserveAspect;
        Color color = image.color;
        bool raycastTarget = image.raycastTarget;
        bool maskable = image.maskable;

        var serialized = new SerializedObject(image);
        var scriptProperty = serialized.FindProperty("m_Script");
        var classProperty = serialized.FindProperty("m_EditorClassIdentifier");
        if (scriptProperty == null || classProperty == null)
        {
            Debug.LogError($"[SvgIconMigration] Missing script properties on {go.name}");
            return false;
        }

        scriptProperty.objectReferenceValue = svgScript;
        classProperty.stringValue = SvgImageClassId;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var svgImage = go.GetComponent<SVGImage>();
        if (svgImage == null)
        {
            Debug.LogError($"[SvgIconMigration] Script swap failed on {go.name}");
            return false;
        }

        svgImage.sprite = sprite;
        svgImage.preserveAspect = preserveAspect;
        svgImage.color = color;
        svgImage.raycastTarget = raycastTarget;
        svgImage.maskable = maskable;
        EditorUtility.SetDirty(svgImage);
        return true;
    }

    private static int RefreshExistingSvgImages(IEnumerable<SVGImage> images, IReadOnlyDictionary<string, Sprite> svgSprites)
    {
        int count = 0;
        foreach (var image in images)
        {
            if (image == null || image.sprite == null)
                continue;

            string path = AssetDatabase.GetAssetPath(image.sprite);
            if (!svgSprites.TryGetValue(path, out var sprite) || sprite == image.sprite)
                continue;

            image.sprite = sprite;
            EditorUtility.SetDirty(image);
            count++;
        }

        return count;
    }

    private static int AssignSceneSpriteFields(GameObject[] roots, IReadOnlyDictionary<string, string> svgNameToPath, IReadOnlyDictionary<string, Sprite> svgSprites)
    {
        int count = 0;

        foreach (var root in roots)
        {
            foreach (var page in root.GetComponentsInChildren<StatisticsPageX01>(true))
            {
                count += SetSprite(page, "iconWall", "clipart_penalty_wall.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconCeiling", "clipart_penalty_ceiling.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconMiss", "clipart_penalty_allMiss.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconOnes", "clipart_penalty_tripleone.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconSchnaps", "clipart_penalty_tripledigit.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconLost", "clipart_penalty_sum.svg", svgNameToPath, svgSprites);
            }

            foreach (var page in root.GetComponentsInChildren<StatisticsPageCricket>(true))
            {
                count += SetSprite(page, "iconWall", "clipart_penalty_wall.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconCeiling", "clipart_penalty_ceiling.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconMiss", "clipart_penalty_allMiss.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconOnes", "clipart_penalty_tripleone.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconSchnaps", "clipart_penalty_tripledigit.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconLost", "clipart_penalty_sum.svg", svgNameToPath, svgSprites);
            }

            foreach (var page in root.GetComponentsInChildren<StatisticsPageATC>(true))
            {
                count += SetSprite(page, "iconWall", "clipart_penalty_wall.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconCeiling", "clipart_penalty_ceiling.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconMiss", "clipart_penalty_allMiss.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconOnes", "clipart_penalty_tripleone.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconSchnaps", "clipart_penalty_tripledigit.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconLost", "clipart_penalty_sum.svg", svgNameToPath, svgSprites);
            }

            foreach (var page in root.GetComponentsInChildren<StatisticsPageAll>(true))
            {
                count += SetSprite(page, "iconWall", "clipart_penalty_wall.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconCeiling", "clipart_penalty_ceiling.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconMiss", "clipart_penalty_allMiss.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconOnes", "clipart_penalty_tripleone.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconSchnaps", "clipart_penalty_tripledigit.svg", svgNameToPath, svgSprites);
                count += SetSprite(page, "iconLost", "clipart_penalty_sum.svg", svgNameToPath, svgSprites);
            }

            foreach (var screen in root.GetComponentsInChildren<GameDetailScreen>(true))
            {
                count += SetSprite(screen, "iconWall", "clipart_penalty_wall.svg", svgNameToPath, svgSprites);
                count += SetSprite(screen, "iconCeiling", "clipart_penalty_ceiling.svg", svgNameToPath, svgSprites);
                count += SetSprite(screen, "iconAllMiss", "clipart_penalty_allMiss.svg", svgNameToPath, svgSprites);
                count += SetSprite(screen, "iconTripleOnes", "clipart_penalty_tripleone.svg", svgNameToPath, svgSprites);
                count += SetSprite(screen, "iconTripleDigit", "clipart_penalty_tripledigit.svg", svgNameToPath, svgSprites);
                count += SetSprite(screen, "iconLostGame", "clipart_penalty_sum.svg", svgNameToPath, svgSprites);
            }

            foreach (var toggle in root.GetComponentsInChildren<AudioToggleButton>(true))
            {
                count += SetSprite(toggle, "soundOnSprite", "clipart_audio_on.svg", svgNameToPath, svgSprites);
                count += SetSprite(toggle, "soundOffSprite", "clipart_audio_off.svg", svgNameToPath, svgSprites);
            }

            foreach (var handler in root.GetComponentsInChildren<InputHandler>(true))
                count += SetSprite(handler, "botDartSprite", "clipart_dart.svg", svgNameToPath, svgSprites);
        }

        return count;
    }

    private static int SetSprite(Object target, string propertyName, string svgFileName, IReadOnlyDictionary<string, string> svgNameToPath, IReadOnlyDictionary<string, Sprite> svgSprites)
    {
        if (target == null || !svgNameToPath.TryGetValue(svgFileName, out var svgPath) || !svgSprites.TryGetValue(svgPath, out var sprite))
            return 0;

        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"[SvgIconMigration] Missing property {propertyName} on {target.GetType().Name}");
            return 0;
        }

        if (property.objectReferenceValue == sprite)
            return 0;

        property.objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        return 1;
    }

    private static MonoScript LoadSvgImageScript()
    {
        string path = AssetDatabase.GUIDToAssetPath(SvgImageScriptGuid);
        var script = !string.IsNullOrEmpty(path) ? AssetDatabase.LoadAssetAtPath<MonoScript>(path) : null;
        if (script != null)
            return script;

        return AssetDatabase.FindAssets("SVGImage t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<MonoScript>)
            .FirstOrDefault(candidate => candidate != null && candidate.GetClass() == typeof(SVGImage));
    }

    private static Scene FindOpenScene(string path)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase))
                return scene;
        }

        return default;
    }

    private static List<string> GetSvgPaths()
    {
        return Directory.GetFiles(GraphicsFolder, "*.svg", SearchOption.AllDirectories)
            .Select(NormalizePath)
            .Where(path => !Exceptions.Contains(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> BuildPngToSvgMap(IEnumerable<string> svgPaths)
    {
        var svgByName = svgPaths.ToDictionary(Path.GetFileNameWithoutExtension, path => path, StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var png in Directory.GetFiles(GraphicsFolder, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(png);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
                continue;

            string name = Path.GetFileNameWithoutExtension(png);
            if (svgByName.TryGetValue(name, out var svg))
                map[NormalizePath(png)] = svg;
        }

        return map;
    }

    private static IEnumerable<string> FindPrefabPaths()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsMappedSprite(Sprite sprite, IReadOnlyDictionary<string, string> pngToSvg)
    {
        if (sprite == null)
            return false;

        return pngToSvg.ContainsKey(AssetDatabase.GetAssetPath(sprite));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static MigrationReport BuildReport()
    {
        var svgPaths = GetSvgPaths();
        var pngToSvg = BuildPngToSvgMap(svgPaths);
        int remainingImages = 0;
        int existingSvgImages = 0;
        int exceptionImages = 0;

        foreach (var prefabPath in FindPrefabPaths())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                continue;
            Count(prefab.GetComponentsInChildren<Image>(true), prefab.GetComponentsInChildren<SVGImage>(true), pngToSvg, ref remainingImages, ref existingSvgImages, ref exceptionImages);
        }

        var scene = FindOpenScene(ScenePath);
        if (scene.IsValid())
        {
            var roots = scene.GetRootGameObjects();
            Count(roots.SelectMany(root => root.GetComponentsInChildren<Image>(true)), roots.SelectMany(root => root.GetComponentsInChildren<SVGImage>(true)), pngToSvg, ref remainingImages, ref existingSvgImages, ref exceptionImages);
        }

        return new MigrationReport
        {
            SvgPaths = svgPaths,
            RemainingImages = remainingImages,
            ExistingSvgImages = existingSvgImages,
            ExceptionImages = exceptionImages
        };
    }

    private static void Count(IEnumerable<Image> images, IEnumerable<SVGImage> svgImages, IReadOnlyDictionary<string, string> pngToSvg, ref int remainingImages, ref int existingSvgImages, ref int exceptionImages)
    {
        foreach (var image in images)
        {
            if (image == null || image.sprite == null)
                continue;

            string path = AssetDatabase.GetAssetPath(image.sprite);
            if (pngToSvg.ContainsKey(path))
                remainingImages++;
            else if (path.StartsWith(GraphicsFolder, StringComparison.OrdinalIgnoreCase))
                exceptionImages++;
        }

        existingSvgImages += svgImages.Count(image => image != null);
    }

    private sealed class MigrationReport
    {
        public List<string> SvgPaths;
        public int RemainingImages;
        public int ExistingSvgImages;
        public int ExceptionImages;
    }
}
