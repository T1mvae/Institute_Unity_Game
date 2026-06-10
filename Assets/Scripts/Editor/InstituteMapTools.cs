using System.IO;
using System.Text;
using Institute.World;
using Institute.World.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor automation for the corrected world-map system. Menu paths are chosen to NOT collide
/// with the pre-existing InstituteSceneStructureBuilder menu items.
/// </summary>
public static class InstituteMapTools
{
    const string TestScenePath = "Assets/Scenes/WorldTest.unity";
    const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

    [MenuItem("Tools/Institute Game/Audit Project")]
    public static void AuditProject()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Institute Project Audit ===");

        sb.AppendLine("\nScenes:");
        foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            sb.AppendLine("  " + AssetDatabase.GUIDToAssetPath(guid));

        sb.AppendLine("\nBuild scenes:");
        foreach (var s in EditorBuildSettings.scenes)
            sb.AppendLine($"  [{(s.enabled ? "x" : " ")}] {s.path}");

        sb.AppendLine("\nKey data files:");
        foreach (var path in new[]
        {
            "Assets/Data/UI/theme.json",
            MapDefinitions.TerrainAssetPath,
            MapDefinitions.RegionTypeAssetPath,
            MapPresets.AssetPath,
            "Assets/Data/Difficulty/difficulty_presets.json",
        })
            sb.AppendLine($"  [{(File.Exists(path) ? "x" : " ")}] {path}");

        sb.AppendLine("\nUI Toolkit:");
        foreach (var path in new[]
        {
            "Assets/Resources/UI/UXML/GameplayHUD.uxml", "Assets/Resources/UI/UXML/PauseMenu.uxml",
            "Assets/Resources/UI/UXML/EventPopup.uxml", "Assets/Resources/UI/UXML/Settings.uxml",
        })
            sb.AppendLine($"  [{(File.Exists(path) ? "x" : " ")}] {path}");

        sb.AppendLine("\nLegacy (one-hex-one-region) scripts still present:");
        foreach (var path in new[]
        {
            "Assets/Scripts/Map/HexMapGenerator.cs", "Assets/Scripts/Map/RegionGridGenerator.cs",
            "Assets/Scripts/Map/VoronoiRegionGenerator.cs", "Assets/Scripts/Map/Region.cs",
        })
            if (File.Exists(path)) sb.AppendLine("  (legacy) " + path);

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Institute Audit", "Audit written to the Console.\n\n" +
            "See Assets/Docs/PREVIOUS_WORK_AUDIT.md for the full review.", "OK");
    }

    [MenuItem("Tools/Institute Game/Rebuild Map Data Files")]
    public static void RebuildMapDataFiles()
    {
        WriteIfMissingOrConfirm(MapDefinitions.TerrainAssetPath,
            JsonUtility.ToJson(TerrainDefinitionCollection.CreateDefault(), true));
        WriteIfMissingOrConfirm(MapDefinitions.RegionTypeAssetPath,
            JsonUtility.ToJson(RegionTypeDefinitionCollection.CreateDefault(), true));
        AssetDatabase.Refresh();
        MapDefinitions.Reload();
        MapPresets.Reload();
        Debug.Log("Institute: map data files ensured.");
    }

    [MenuItem("Tools/Institute Game/Rebuild UI Theme Files")]
    public static void RebuildThemeFiles()
    {
        ThemeLoader.LoadOrCreateDefault(); // writes theme.json default if missing
        MapPalette.Reload();
        AssetDatabase.Refresh();
        ThemeLoader.ValidateTheme(out string msg);
        Debug.Log("Institute: theme files ensured. " + msg);
        EditorUtility.DisplayDialog("Institute Theme", msg, "OK");
    }

    [MenuItem("Tools/Institute Game/Validate Map Data")]
    public static void ValidateMapData()
    {
        MapDefinitions.Reload();
        MapPalette.Reload();
        MapGenerationSettings settings = MapGenerationSettings.ForPreset("Medium");
        var map = new WorldMapGenerator().Generate(settings);
        ValidationResult result = WorldMapValidator.Validate(map);
        Debug.Log(result.ToReport());

        // Extra invariant: region count must be far smaller than tile count.
        string summary = $"Tiles: {map.TileCount}  Regions: {map.RegionCount}  Unclaimed land: {map.unclaimedTileIds.Count}\n\n" +
                         (result.IsValid ? "PASS — model is correct (regions are multi-tile, regions << tiles, unclaimed land exists)."
                                         : "FAIL — see Console for details.");
        EditorUtility.DisplayDialog("Validate Map Data", summary, "OK");
    }

    [MenuItem("Tools/Institute Game/Validate Save Data")]
    public static void ValidateSaveData()
    {
        string dir = WorldSaveBridge.SaveDirectory;
        if (!Directory.Exists(dir))
        {
            EditorUtility.DisplayDialog("Validate Save Data", "No save directory yet:\n" + dir, "OK");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("World saves in " + dir + ":");
        bool any = false;
        foreach (string file in Directory.GetFiles(dir, "*.world.json"))
        {
            any = true;
            string json = File.ReadAllText(file);
            WorldMapData map = WorldMapSerializer.FromJson(json, out string error);
            if (map == null) sb.AppendLine($"  [REJECT] {Path.GetFileName(file)} — {error}");
            else
            {
                ValidationResult r = WorldMapValidator.Validate(map);
                sb.AppendLine($"  [{(r.IsValid ? "OK" : "WARN")}] {Path.GetFileName(file)} — {map.TileCount} tiles, {map.RegionCount} regions");
            }
        }

        // Detect legacy SaveGameData (one-hex-one-region) payloads too.
        foreach (string file in Directory.GetFiles(dir, "*.json"))
        {
            if (file.EndsWith(".world.json")) continue;
            sb.AppendLine($"  [legacy] {Path.GetFileName(file)} — pre-refactor SaveGameData; world layer stored separately as .world.json");
        }

        if (!any) sb.AppendLine("  (no .world.json files found)");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Validate Save Data", sb.ToString(), "OK");
    }

    [MenuItem("Tools/Institute Game/Generate Test Hex World")]
    public static void GenerateTestHexWorld()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera");
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.tag = "MainCamera";

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Full integrated gameplay (world + HUD + decision/event/character systems + overlays).
        var installer = new GameObject("Gameplay Installer");
        installer.AddComponent<WorldGameplayInstaller>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TestScenePath);
        AssetDatabase.Refresh();
        Debug.Log("Institute: created " + TestScenePath + ". Press Play to generate and explore the hex world.");
        EditorUtility.DisplayDialog("Generate Test Hex World",
            "Created " + TestScenePath + ".\nPress Play to generate the Civ-like hex world and explore it.", "OK");
    }

    [MenuItem("Tools/Institute Game/Rebuild Gameplay World Scene")]
    public static void RebuildGameplayWorldScene()
    {
        if (!EditorUtility.DisplayDialog("Rebuild Gameplay World Scene",
            "This rewrites " + GameplayScenePath + " to use the new map-centric WorldGameplayInstaller " +
            "(the legacy one-hex-one-region gameplay setup is replaced). Continue?", "Rebuild", "Cancel"))
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        var installer = new GameObject("Gameplay Installer");
        installer.AddComponent<WorldGameplayInstaller>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, GameplayScenePath);
        AssetDatabase.Refresh();
        Debug.Log("Institute: rebuilt " + GameplayScenePath + " with WorldGameplayInstaller.");
    }

    [MenuItem("Tools/Institute Game/Validate Gameplay Integration")]
    public static void ValidateGameplayIntegration()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Gameplay Integration Validation ===");
        bool ok = true;

        void Check(string label, bool pass)
        {
            sb.AppendLine($"  [{(pass ? "ok" : "FAIL")}] {label}");
            if (!pass) ok = false;
        }

        // Required new RegionData-driven systems / overlays exist as types.
        Check("RegionDecisionSystem present", TypeExists("Institute.World.Gameplay.RegionDecisionSystem"));
        Check("RegionEventSystem present", TypeExists("Institute.World.Gameplay.RegionEventSystem"));
        Check("RegionCharacterSystem present", TypeExists("Institute.World.Gameplay.RegionCharacterSystem"));
        Check("GameSaveService present", TypeExists("Institute.World.Gameplay.GameSaveService"));
        Check("WorldPauseController present", TypeExists("Institute.World.UI.WorldPauseController"));
        Check("WorldEventPopupController present", TypeExists("Institute.World.UI.WorldEventPopupController"));
        Check("WorldSettingsController present", TypeExists("Institute.World.UI.WorldSettingsController"));

        // WorldController exposes the RegionDataChanged observable.
        var wcType = System.Type.GetType("Institute.World.WorldController, Assembly-CSharp");
        Check("WorldController.RegionDataChanged event", wcType != null && wcType.GetEvent("RegionDataChanged") != null);

        // Authored overlay UXML present.
        Check("PauseMenu.uxml", File.Exists("Assets/Resources/UI/UXML/PauseMenu.uxml"));
        Check("EventPopup.uxml", File.Exists("Assets/Resources/UI/UXML/EventPopup.uxml"));
        Check("Settings.uxml", File.Exists("Assets/Resources/UI/UXML/Settings.uxml"));

        // Content data the systems consume.
        Check("decisions.json", File.Exists(Path.Combine(Application.streamingAssetsPath, "decisions.json")));
        Check("events.json", File.Exists(Path.Combine(Application.streamingAssetsPath, "events.json")));

        // The new installer must NOT reference the legacy Region-coupled managers.
        string installer = "Assets/Scripts/World/WorldGameplayInstaller.cs";
        if (File.Exists(installer))
        {
            string text = File.ReadAllText(installer);
            Check("Installer does not use legacy EventManager", !text.Contains("EventManager"));
            Check("Installer does not use legacy CharacterManager", !text.Contains("CharacterManager"));
            Check("Installer does not use legacy DecisionSelectionManager", !text.Contains("DecisionSelectionManager"));
        }

        // Map model still correct.
        MapDefinitions.Reload();
        var map = new WorldMapGenerator().Generate(MapGenerationSettings.ForPreset("Small"));
        ValidationResult r = WorldMapValidator.Validate(map);
        Check("Map model valid (regions << tiles, multi-tile, unclaimed land)", r.IsValid);

        sb.AppendLine(ok ? "\nRESULT: PASS" : "\nRESULT: FAIL (see above)");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Validate Gameplay Integration", sb.ToString(), "OK");
    }

    static bool TypeExists(string fullName)
    {
        return System.Type.GetType(fullName + ", Assembly-CSharp") != null;
    }

    static void WriteIfMissingOrConfirm(string assetPath, string content)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        if (File.Exists(full))
        {
            if (!EditorUtility.DisplayDialog("Overwrite?", assetPath + " exists. Overwrite with defaults?", "Overwrite", "Keep"))
                return;
        }
        File.WriteAllText(full, content);
        Debug.Log("Institute: wrote " + assetPath);
    }
}
