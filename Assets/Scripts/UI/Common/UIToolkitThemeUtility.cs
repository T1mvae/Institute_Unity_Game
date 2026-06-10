using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class UIToolkitThemeUtility
{
    static PanelSettings runtimePanelSettings;
    static ThemeStyleSheet cachedTheme;
    static bool themeWarningLogged;

    /// <summary>Resources path (no extension) of the runtime Theme Style Sheet.</summary>
    public const string ThemeResourcePath = "UI/InstituteRuntimeTheme";

    public static UIDocument EnsureDocument(GameObject host)
    {
        EnsureEventSystem();
        EnsureCamera();
        UIDocument document = host.GetComponent<UIDocument>();
        if (document == null)
            document = host.AddComponent<UIDocument>();

        if (document.panelSettings == null)
            document.panelSettings = GetOrCreatePanelSettings();

        return document;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    /// <summary>
    /// Ensures the active scene has at least one Camera so Unity does not report
    /// "Display 1 No cameras rendering". Pure-UI scenes get a clear-only camera (culling mask 0);
    /// the gameplay scene's WorldController sets up its own world-rendering camera.
    /// </summary>
    public static Camera EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null) return cam;

        var go = new GameObject("UI Camera");
        cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = ThemeLoader.Current.Background;
        cam.orthographic = true;
        cam.cullingMask = 0;          // pure-UI scene: nothing in the world to draw
        cam.depth = -10f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        if (Camera.main == null) go.tag = "MainCamera";
        return cam;
    }

    public static PanelSettings GetOrCreatePanelSettings()
    {
        if (runtimePanelSettings != null)
            return runtimePanelSettings;

        runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        runtimePanelSettings.name = "Runtime Institute Panel Settings";
        runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        runtimePanelSettings.referenceResolution = new Vector2Int(1920, 1080);
        runtimePanelSettings.match = 0.5f;
        runtimePanelSettings.sortingOrder = 0;

        // Critical: assign a Theme Style Sheet or text/controls won't render (and Unity warns).
        ThemeStyleSheet theme = LoadDefaultTheme();
        if (theme != null)
            runtimePanelSettings.themeStyleSheet = theme;
        else if (!themeWarningLogged)
        {
            themeWarningLogged = true;
            Debug.LogWarning("UIToolkitThemeUtility: could not load a Theme Style Sheet " +
                "(Resources/UI/InstituteRuntimeTheme.tss). Run Tools/Institute Game/Repair UI Toolkit Setup.");
        }
        return runtimePanelSettings;
    }

    /// <summary>Loads the runtime Theme Style Sheet (Resources first, editor asset fallbacks).</summary>
    public static ThemeStyleSheet LoadDefaultTheme()
    {
        if (cachedTheme != null) return cachedTheme;

        cachedTheme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
#if UNITY_EDITOR
        if (cachedTheme == null)
            cachedTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Resources/UI/InstituteRuntimeTheme.tss");
        if (cachedTheme == null)
            cachedTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Resources/UI/Styles/InstituteTheme.tss");
        if (cachedTheme == null)
            cachedTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityDefaultRuntimeTheme.tss");
#endif
        return cachedTheme;
    }

    public static void ApplyRootTheme(VisualElement root)
    {
        if (root == null)
            return;

        UIThemeConfig theme = ThemeLoader.Current;
        root.style.flexGrow = 1f;
        root.style.backgroundColor = theme.Background;
        root.style.color = theme.TextPrimary;
    }

    public static Label Label(string text, string className = null)
    {
        Label label = new Label(text);
        if (!string.IsNullOrEmpty(className))
            label.AddToClassList(className);
        return label;
    }

    public static Button Button(string text, System.Action clicked, string extraClass = null)
    {
        Button button = new Button(clicked) { text = text };
        button.AddToClassList("institute-button");
        if (!string.IsNullOrEmpty(extraClass))
            button.AddToClassList(extraClass);
        return button;
    }

    public static VisualElement Panel(string extraClass = null)
    {
        VisualElement panel = new VisualElement();
        panel.AddToClassList("panel");
        if (!string.IsNullOrEmpty(extraClass))
            panel.AddToClassList(extraClass);
        return panel;
    }
}
