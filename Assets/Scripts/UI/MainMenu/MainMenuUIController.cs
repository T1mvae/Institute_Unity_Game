using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuUIController : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset layoutAsset;
    [SerializeField] private StyleSheet[] styleSheets;

    UIDocument document;
    SettingsUIController settingsController;

    void Start()
    {
        ThemeLoader.LoadOrCreateDefault();
        SceneFlowManager.EnsureExists();
        LoadEditorAssetsIfNeeded();
        document = UIToolkitThemeUtility.EnsureDocument(gameObject);
        BuildUI();
    }

    void LoadEditorAssetsIfNeeded()
    {
        // Resources-first so the menu is styled in the standalone build too.
        if (layoutAsset == null)
            layoutAsset = Institute.World.UI.OverlayUtil.LoadUxml("UI/UXML/MainMenu");
        if (styleSheets == null || styleSheets.Length == 0)
        {
            styleSheets = new[]
            {
                Institute.World.UI.OverlayUtil.LoadStyle("UI/Styles/base"),
                Institute.World.UI.OverlayUtil.LoadStyle("UI/Styles/main_menu"),
                Institute.World.UI.OverlayUtil.LoadStyle("UI/Styles/popups")
            };
        }
    }

    void BuildUI()
    {
        VisualElement root = document.rootVisualElement;
        root.Clear();
        UIToolkitThemeUtility.ApplyRootTheme(root);
        AddStyleSheets(root);

        if (layoutAsset != null)
        {
            layoutAsset.CloneTree(root);
            WireUxml(root);
        }
        else
        {
            BuildFallback(root);
        }

        settingsController = gameObject.GetComponent<SettingsUIController>();
        if (settingsController == null)
            settingsController = gameObject.AddComponent<SettingsUIController>();
        settingsController.AttachTo(root);
    }

    void WireUxml(VisualElement root)
    {
        Button newGame = root.Q<Button>("NewGameButton");
        Button continueButton = root.Q<Button>("ContinueButton");
        Button loadButton = root.Q<Button>("LoadGameButton");
        Button settings = root.Q<Button>("SettingsButton");
        Button credits = root.Q<Button>("CreditsButton");
        Button exit = root.Q<Button>("ExitButton");
        Label version = root.Q<Label>("VersionLabel");

        if (version != null)
            version.text = $"Prototype {Application.version}";
        WireButtons(newGame, continueButton, loadButton, settings, credits, exit);
    }

    void BuildFallback(VisualElement root)
    {
        root.AddToClassList("root-screen");
        root.AddToClassList("menu-root");
        BuildBackgroundGrid(root);

        VisualElement grid = new VisualElement();
        grid.AddToClassList("menu-grid");
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.justifyContent = Justify.SpaceBetween;
        grid.style.alignItems = Align.Center;
        grid.style.width = Length.Percent(92);
        grid.style.maxWidth = 1180;

        VisualElement titleColumn = new VisualElement();
        titleColumn.AddToClassList("menu-title-column");
        titleColumn.style.width = 620;
        titleColumn.Add(UIToolkitThemeUtility.Label("THE INSTITUTE", "title"));
        titleColumn.Add(UIToolkitThemeUtility.Label("Strategic influence console for a low-development world.", "menu-subtitle"));

        VisualElement about = UIToolkitThemeUtility.Panel("about-card");
        about.Add(UIToolkitThemeUtility.Label("Planetary dossier, covert assets, unstable nobility, and a worrying amount of responsibility.", "body-copy"));
        titleColumn.Add(about);

        VisualElement buttons = UIToolkitThemeUtility.Panel("menu-button-column");
        buttons.style.width = 340;
        Button newGame = UIToolkitThemeUtility.Button("NEW GAME", null, "primary-button");
        Button continueButton = UIToolkitThemeUtility.Button("CONTINUE", null);
        Button loadButton = UIToolkitThemeUtility.Button("LOAD GAME", null);
        Button settings = UIToolkitThemeUtility.Button("SETTINGS", null);
        Button credits = UIToolkitThemeUtility.Button("ABOUT", null);
        Button exit = UIToolkitThemeUtility.Button("EXIT", null, "danger-button");
        buttons.Add(newGame);
        buttons.Add(continueButton);
        buttons.Add(loadButton);
        buttons.Add(settings);
        buttons.Add(credits);
        buttons.Add(exit);

        grid.Add(titleColumn);
        grid.Add(buttons);
        root.Add(grid);

        VisualElement footer = new VisualElement();
        footer.AddToClassList("menu-footer");
        footer.style.position = Position.Absolute;
        footer.style.left = 24;
        footer.style.right = 24;
        footer.style.bottom = 18;
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.Add(UIToolkitThemeUtility.Label($"Prototype {Application.version}", "meta-label"));
        footer.Add(UIToolkitThemeUtility.Label("Institute strategic tablet interface", "meta-label"));
        root.Add(footer);

        WireButtons(newGame, continueButton, loadButton, settings, credits, exit);
    }

    void BuildBackgroundGrid(VisualElement root)
    {
        UIThemeConfig theme = ThemeLoader.Current;
        for (int i = 0; i < 18; i++)
        {
            VisualElement line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.left = Length.Percent(i * 100f / 17f);
            line.style.top = 0;
            line.style.bottom = 0;
            line.style.width = 1;
            line.style.backgroundColor = new Color(theme.PanelBorder.r, theme.PanelBorder.g, theme.PanelBorder.b, 0.18f);
            root.Add(line);
        }

        for (int i = 0; i < 10; i++)
        {
            VisualElement line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.left = 0;
            line.style.right = 0;
            line.style.top = Length.Percent(i * 100f / 9f);
            line.style.height = 1;
            line.style.backgroundColor = new Color(theme.PanelBorder.r, theme.PanelBorder.g, theme.PanelBorder.b, 0.14f);
            root.Add(line);
        }
    }

    void WireButtons(Button newGame, Button continueButton, Button loadButton, Button settings, Button credits, Button exit)
    {
        if (newGame != null)
            newGame.clicked += () => SceneFlowManager.EnsureExists().GoToNewGameSetup();
        if (continueButton != null)
        {
            // Use the NEW save system (WorldSaveBridge writes <slot>.world.json); the legacy
            // SaveLoadManager.SaveExists checks <slot>.json and would never see an in-game save.
            continueButton.SetEnabled(Institute.World.Gameplay.GameSaveService.HasSave(SaveLoadManager.AutoSaveSlot));
            continueButton.clicked += () => SceneFlowManager.EnsureExists().ContinueGame();
        }
        if (loadButton != null)
        {
            loadButton.SetEnabled(Institute.World.Gameplay.GameSaveService.HasSave(SaveLoadManager.ManualSaveSlot));
            loadButton.clicked += () => SceneFlowManager.EnsureExists().LoadGame(SaveLoadManager.ManualSaveSlot);
        }
        if (settings != null)
            settings.clicked += () => settingsController?.Show();
        if (credits != null)
            credits.clicked += ShowAbout;
        if (exit != null)
            exit.clicked += () => SceneFlowManager.EnsureExists().QuitGame();
    }

    void ShowAbout()
    {
        Debug.Log("The Institute: grand strategy political simulation prototype.");
    }

    void AddStyleSheets(VisualElement root)
    {
        if (styleSheets == null)
            return;

        for (int i = 0; i < styleSheets.Length; i++)
        {
            if (styleSheets[i] != null && !root.styleSheets.Contains(styleSheets[i]))
                root.styleSheets.Add(styleSheets[i]);
        }
    }
}
