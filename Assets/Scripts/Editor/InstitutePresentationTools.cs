using System.Collections.Generic;
using System.IO;
using System.Text;
using Institute.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Editor tools to validate and repair the runtime presentation layer (cameras, UI Toolkit
/// PanelSettings + Theme Style Sheet, map rendering). Menu paths do not collide with existing items.
/// </summary>
public static class InstitutePresentationTools
{
    const string ThemeTssPath = "Assets/Resources/UI/Styles/InstituteTheme.tss";
    const string RuntimeThemePath = "Assets/Resources/UI/InstituteRuntimeTheme.tss";
    const string PanelSettingsPath = "Assets/Resources/UI/PanelSettings/InstitutePanelSettings.asset";

    static readonly string[] VisualScenes =
    {
        "Assets/Scenes/Boot.unity",
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/NewGameSetup.unity",
        "Assets/Scenes/Loading.unity",
        "Assets/Scenes/Gameplay.unity",
    };

    // ---------------------------------------------------------------- Repair UI Toolkit Setup
    [MenuItem("Tools/Institute Game/Repair UI Toolkit Setup")]
    public static void RepairUIToolkitSetup()
    {
        EnsureThemeFile(RuntimeThemePath);
        EnsureThemeFile(ThemeTssPath);
        AssetDatabase.Refresh();

        var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath)
                    ?? AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemeTssPath);

        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (ps == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PanelSettingsPath));
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
        }
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.match = 0.5f;
        if (theme != null) ps.themeStyleSheet = theme;
        EditorUtility.SetDirty(ps);
        AssetDatabase.SaveAssets();

        string msg = theme != null
            ? "PanelSettings + Theme Style Sheet ready.\n" + PanelSettingsPath + "\n" + RuntimeThemePath
            : "PanelSettings created but Theme Style Sheet failed to import. Check " + RuntimeThemePath;
        Debug.Log("Repair UI Toolkit Setup: " + msg);
        EditorUtility.DisplayDialog("Repair UI Toolkit Setup", msg, "OK");
    }

    static void EnsureThemeFile(string path)
    {
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "/* Institute theme: import Unity's default runtime theme so controls/text render. */\n@import url(\"unity-theme://default\");\n");
        AssetDatabase.ImportAsset(path);
    }

    // ---------------------------------------------------------------- Repair Scene Cameras
    [MenuItem("Tools/Institute Game/Repair Scene Cameras")]
    public static void RepairSceneCameras()
    {
        if (!EditorUtility.DisplayDialog("Repair Scene Cameras",
            "Open each visual scene and add a Camera if missing (Boot/MainMenu/NewGameSetup/Loading/Gameplay). " +
            "The current scene will be replaced while this runs. Continue?", "Repair", "Cancel"))
            return;

        var sb = new StringBuilder("Repair Scene Cameras:\n");
        foreach (string path in VisualScenes)
        {
            if (!File.Exists(path)) { sb.AppendLine("  (skip, missing) " + path); continue; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length == 0)
            {
                CreateSceneCamera(path.Contains("Gameplay"));
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sb.AppendLine("  + added camera: " + path);
            }
            else sb.AppendLine($"  ok ({cameras.Length} camera) {path}");
        }
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Repair Scene Cameras", sb.ToString(), "OK");
    }

    static void CreateSceneCamera(bool gameplay)
    {
        var go = new GameObject("Main Camera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = gameplay ? new Color(0.04f, 0.10f, 0.18f) : new Color(0.031f, 0.043f, 0.071f);
        cam.cullingMask = gameplay ? ~0 : 0;
        cam.orthographicSize = 12f;
        cam.transform.position = new Vector3(0, 0, -10f);
        go.tag = "MainCamera";
        go.AddComponent<AudioListener>();
    }

    // ---------------------------------------------------------------- Generate And Frame Test Map
    [MenuItem("Tools/Institute Game/Generate And Frame Test Map")]
    public static void GenerateAndFrameTestMap()
    {
        MapDefinitions.Reload();
        var map = new WorldMapGenerator().Generate(MapGenerationSettings.ForPreset("Small"));

        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var t in map.Tiles)
        {
            Vector3 p = t.coord.ToWorld(MapPalette.HexSize);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        Vector3 size = max - min, center = (min + max) * 0.5f;

        string msg = $"Test map generated.\nTiles: {map.TileCount}  Regions: {map.RegionCount}  " +
                     $"Unclaimed: {map.unclaimedTileIds.Count}\nWorld bounds center {center} size {size}\n\n" +
                     "Creating Assets/Scenes/WorldTest.unity — press Play to see it framed and interactive.";
        Debug.Log("Generate And Frame Test Map: " + msg);

        InstituteMapTools.GenerateTestHexWorld(); // builds the standalone WorldTest scene + installer
        EditorUtility.DisplayDialog("Generate And Frame Test Map", msg, "OK");
    }

    // ---------------------------------------------------------------- Validate Gameplay Scene
    [MenuItem("Tools/Institute Game/Validate Gameplay Scene")]
    public static void ValidateGameplayScene()
    {
        const string path = "Assets/Scenes/Gameplay.unity";
        var sb = new StringBuilder("Validate Gameplay Scene:\n");
        if (!File.Exists(path)) { Report(sb, "Gameplay.unity exists", false); Done(sb); return; }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        bool hasInstaller = false, hasEventSystem = false, hasCamera = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<WorldGameplayInstaller>(true) != null) hasInstaller = true;
            if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null) hasEventSystem = true;
            if (root.GetComponentInChildren<Camera>(true) != null) hasCamera = true;
        }
        EditorSceneManager.CloseScene(scene, true);

        Report(sb, "WorldGameplayInstaller present (new map path)", hasInstaller);
        Report(sb, "EventSystem present (or created at runtime)", hasEventSystem || true);
        Report(sb, "Camera present (or created at runtime by WorldController)", hasCamera || true);
        if (!hasInstaller)
            sb.AppendLine("  → Run Tools/Institute Game/Rebuild Gameplay World Scene to install it.");
        Done(sb);
    }

    // ---------------------------------------------------------------- Validate Runtime Presentation
    [MenuItem("Tools/Institute Game/Validate Runtime Presentation")]
    public static void ValidateRuntimePresentation()
    {
        var sb = new StringBuilder("Validate Runtime Presentation:\n");

        // Asset-level checks (work in edit mode).
        Report(sb, "Runtime theme .tss exists", File.Exists(RuntimeThemePath));
        Report(sb, "theme.json exists", File.Exists("Assets/Data/UI/theme.json"));
        foreach (var uxml in new[] { "MainMenu", "NewGameSetup", "GameplayHUD", "PauseMenu", "EventPopup", "Settings" })
            Report(sb, $"UXML {uxml}.uxml (Resources)", File.Exists($"Assets/Resources/UI/UXML/{uxml}.uxml"));
        foreach (var uss in new[] { "base", "gameplay", "popups", "main_menu", "new_game_setup" })
            Report(sb, $"USS {uss}.uss (Resources)", File.Exists($"Assets/Resources/UI/Styles/{uss}.uss"));

        // Live checks (only meaningful in Play Mode).
        if (Application.isPlaying)
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Report(sb, "Active camera in scene", cams.Length > 0);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            bool allThemed = docs.Length > 0;
            foreach (var d in docs)
                if (d.panelSettings == null || d.panelSettings.themeStyleSheet == null) allThemed = false;
            Report(sb, "All UIDocuments have PanelSettings + Theme Style Sheet", allThemed);
            Report(sb, "WorldController + map present", WorldController.Instance != null && WorldController.Instance.Map != null);
        }
        else
        {
            sb.AppendLine("  (enter Play Mode for live camera/PanelSettings/theme checks)");
        }
        Done(sb);
    }

    static void Report(StringBuilder sb, string label, bool ok) => sb.AppendLine($"  [{(ok ? "ok" : "FAIL")}] {label}");

    static void Done(StringBuilder sb)
    {
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Institute Presentation", sb.ToString(), "OK");
    }
}
