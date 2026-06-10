using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class InstituteSceneStructureBuilder
{
    const string BootScenePath = "Assets/Scenes/Boot.unity";
    const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string NewGameScenePath = "Assets/Scenes/NewGameSetup.unity";
    const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    const string LoadingScenePath = "Assets/Scenes/Loading.unity";
    const string LegacyGameplayPath = "Assets/Scenes/Legacy_Gameplay_Backup.unity";
    const string LegacyMainMenuPath = "Assets/Scenes/Legacy_MainMenu_Backup.unity";

    [MenuItem("Tools/Institute Game/Rebuild Scene Structure")]
    public static void RebuildSceneStructure()
    {
        EnsureProjectStructure();
        EnsureDefaultThemeFiles();
        BackupLegacyScenes();
        RebuildBoot();
        RebuildMainMenu();
        RebuildNewGameSetup();
        RebuildLoading();
        RebuildGameplayUI();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Institute scene structure rebuilt: Boot -> MainMenu -> NewGameSetup/Loading -> Gameplay.");
    }

    [MenuItem("Tools/Institute Game/Rebuild Main Menu")]
    public static void RebuildMainMenu()
    {
        EnsureProjectStructure();
        Scene scene = NewScene(MainMenuScenePath);
        GameObject root = CreateRoot("Main Menu Screen");
        MainMenuUIController controller = root.AddComponent<MainMenuUIController>();
        AssignToolkitAssets(controller, "Assets/Resources/UI/UXML/MainMenu.uxml", "Assets/Resources/UI/Styles/base.uss", "Assets/Resources/UI/Styles/main_menu.uss", "Assets/Resources/UI/Styles/popups.uss");
        root.AddComponent<SettingsUIController>();
        Save(scene, MainMenuScenePath);
    }

    [MenuItem("Tools/Institute Game/Rebuild New Game Setup")]
    public static void RebuildNewGameSetup()
    {
        EnsureProjectStructure();
        Scene scene = NewScene(NewGameScenePath);
        GameObject root = CreateRoot("New Game Setup Screen");
        NewGameSetupUIController controller = root.AddComponent<NewGameSetupUIController>();
        AssignToolkitAssets(controller, "Assets/Resources/UI/UXML/NewGameSetup.uxml", "Assets/Resources/UI/Styles/base.uss", "Assets/Resources/UI/Styles/new_game_setup.uss", "Assets/Resources/UI/Styles/popups.uss");
        Save(scene, NewGameScenePath);
    }

    [MenuItem("Tools/Institute Game/Rebuild Gameplay UI")]
    public static void RebuildGameplayUI()
    {
        EnsureProjectStructure();
        Scene scene = NewScene(GameplayScenePath);
        GameObject root = CreateRoot("Gameplay Scene Installer");
        GameplaySceneInstaller installer = root.AddComponent<GameplaySceneInstaller>();
        AssignGameplayPrefabs(installer);
        Save(scene, GameplayScenePath);
    }

    [MenuItem("Tools/Institute Game/Validate UI Theme")]
    public static void ValidateTheme()
    {
        EnsureProjectStructure();
        EnsureDefaultThemeFiles();
        bool valid = ThemeLoader.ValidateTheme(out string message);
        if (valid)
            Debug.Log(message);
        else
            Debug.LogError(message);
    }

    public static void RebuildBoot()
    {
        EnsureProjectStructure();
        Scene scene = NewScene(BootScenePath);
        GameObject services = CreateRoot("Persistent Services");
        services.AddComponent<GameBootstrapper>();
        Save(scene, BootScenePath);
    }

    public static void RebuildLoading()
    {
        EnsureProjectStructure();
        Scene scene = NewScene(LoadingScenePath);
        GameObject root = CreateRoot("Loading Screen");
        LoadingScreenController controller = root.AddComponent<LoadingScreenController>();
        AssignToolkitAssets(controller, "Assets/Resources/UI/UXML/Loading.uxml", "Assets/Resources/UI/Styles/base.uss", "Assets/Resources/UI/Styles/main_menu.uss");
        Save(scene, LoadingScenePath);
    }

    static Scene NewScene(string path)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = Path.GetFileNameWithoutExtension(path);
        return scene;
    }

    static GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name);
        root.transform.position = Vector3.zero;
        return root;
    }

    static void Save(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, path);
    }

    static void AssignToolkitAssets(MonoBehaviour controller, string uxmlPath, params string[] ussPaths)
    {
        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty layout = serialized.FindProperty("layoutAsset");
        if (layout != null)
            layout.objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

        SerializedProperty styles = serialized.FindProperty("styleSheets");
        if (styles != null)
        {
            styles.arraySize = ussPaths.Length;
            for (int i = 0; i < ussPaths.Length; i++)
                styles.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPaths[i]);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    static void AssignGameplayPrefabs(GameplaySceneInstaller installer)
    {
        SerializedObject serialized = new SerializedObject(installer);
        SetAsset(serialized, "regionUIPrefab", "Assets/Prefabs/Map/RegionUI.prefab");
        SetAsset(serialized, "decisionButtonPrefab", "Assets/Prefabs/UI/decisionButton.prefab");
        SetAsset(serialized, "eventPanelPrefab", "Assets/Prefabs/UI/EventPanel.prefab");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetAsset(SerializedObject serialized, string propertyName, string assetPath)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }

    static void ConfigureBuildSettings()
    {
        string[] scenePaths =
        {
            BootScenePath,
            MainMenuScenePath,
            NewGameScenePath,
            LoadingScenePath,
            GameplayScenePath
        };

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        for (int i = 0; i < scenePaths.Length; i++)
        {
            if (File.Exists(scenePaths[i]))
                scenes.Add(new EditorBuildSettingsScene(scenePaths[i], true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void BackupLegacyScenes()
    {
        if (File.Exists("Assets/Scenes/GameScreen.unity") && !File.Exists(LegacyGameplayPath))
            AssetDatabase.CopyAsset("Assets/Scenes/GameScreen.unity", LegacyGameplayPath);
        if (File.Exists("Assets/Scenes/MainMenu.unity") && !File.Exists(LegacyMainMenuPath))
            AssetDatabase.CopyAsset("Assets/Scenes/MainMenu.unity", LegacyMainMenuPath);
    }

    static void EnsureProjectStructure()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Scripts/Core/SceneManagement");
        EnsureFolder("Assets/Scripts/UI/Common");
        EnsureFolder("Assets/Scripts/UI/MainMenu");
        EnsureFolder("Assets/Scripts/UI/NewGameSetup");
        EnsureFolder("Assets/Scripts/UI/Gameplay");
        EnsureFolder("Assets/Scripts/UI/Settings");
        EnsureFolder("Assets/Scripts/UI/Popups");
        EnsureFolder("Assets/Data/UI");
        EnsureFolder("Assets/Data/Difficulty");
        EnsureFolder("Assets/Data/Events");
        EnsureFolder("Assets/Data/Decisions");
        EnsureFolder("Assets/Resources/UI/UXML");
        EnsureFolder("Assets/Resources/UI/Styles");
        EnsureFolder("Assets/Resources/UI/Prefabs");
        EnsureFolder("Assets/Docs");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void EnsureDefaultThemeFiles()
    {
        if (!File.Exists("Assets/Data/UI/theme.json"))
            File.WriteAllText("Assets/Data/UI/theme.json", JsonUtility.ToJson(UIThemeConfig.CreateDefault(), true));
        EnsureTextFile("Assets/Data/UI/layout_main_menu.json", "{\n  \"layoutName\": \"MainMenu\"\n}\n");
        EnsureTextFile("Assets/Data/UI/layout_gameplay.json", "{\n  \"layoutName\": \"Gameplay\"\n}\n");
        EnsureTextFile("Assets/Resources/UI/Styles/base.uss", "/* Base Institute UI styles are generated by project setup. */\n");
        EnsureTextFile("Assets/Resources/UI/Styles/main_menu.uss", "/* Main menu styles. */\n");
        EnsureTextFile("Assets/Resources/UI/Styles/gameplay.uss", "/* Gameplay styles. */\n");
        EnsureTextFile("Assets/Resources/UI/Styles/popups.uss", "/* Popup styles. */\n");
    }

    static void EnsureTextFile(string path, string contents)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, contents);
    }
}
