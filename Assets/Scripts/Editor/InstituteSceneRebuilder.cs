using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InstituteSceneRebuilder
{
    const string ScenePath = "Assets/Scenes/GameScreen.unity";
    const string RegionPrefabPath = "Assets/Prefabs/Map/RegionUI.prefab";
    const string DecisionButtonPrefabPath = "Assets/Prefabs/UI/decisionButton.prefab";
    const string OptionButtonPrefabPath = "Assets/Prefabs/UI/optionButton.prefab";
    const string EventPanelPrefabPath = "Assets/Prefabs/UI/EventPanel.prefab";

    static Font cachedFont;

    [MenuItem("Tools/Institute Game/Legacy/Rebuild Single Gameplay Scene")]
    public static void RebuildMainScene()
    {
        EnsureFolders();
        CreateOrUpdatePrefabs();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera mainCamera = CreateMainCamera();
        Canvas canvas = CreateCanvas(mainCamera, out GraphicRaycaster raycaster);
        EventSystem eventSystem = CreateEventSystem();

        GameObject systems = new GameObject("Game Systems");
        GameObject gameControllerObject = new GameObject("Game Controller");

        LevelController levelController = gameControllerObject.AddComponent<LevelController>();
        HexMapGenerator hexMapGenerator = gameControllerObject.AddComponent<HexMapGenerator>();
        RegionGridGenerator regionGridGenerator = gameControllerObject.AddComponent<RegionGridGenerator>();

        systems.AddComponent<GameManager>();
        systems.AddComponent<ResourceManager>();
        systems.AddComponent<TimeManager>();
        systems.AddComponent<RegionManager>();
        systems.AddComponent<DecisionPool>();
        systems.AddComponent<DecisionSelectionManager>();
        systems.AddComponent<SaveLoadManager>();
        systems.AddComponent<CharacterManager>();
        systems.AddComponent<GameDateTracker>();
        systems.AddComponent<TimerUI>();
        EventManager eventManager = systems.AddComponent<EventManager>();

        UIManager uiManager = canvas.gameObject.AddComponent<UIManager>();

        RectTransform root = canvas.transform as RectTransform;
        Image background = CreateImage("Tablet Background", root, UITheme.BackgroundDark);
        Stretch(background.rectTransform);
        background.raycastTarget = false;

        RectTransform topBar = CreatePanel("Resource Bar", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-32f, 72f));
        topBar.offsetMin = new Vector2(16f, -80f);
        topBar.offsetMax = new Vector2(-16f, -8f);
        topBar.GetComponent<Image>().color = new Color(0.035f, 0.043f, 0.085f, 0.96f);
        AddHorizontalLayout(topBar.gameObject, 18, 16, 10, TextAnchor.MiddleLeft);

        Text dateText = AddText(CreateLayoutItem("Date", topBar, 260, 42).transform as RectTransform, "DAY 001 — 01.01.2024", 18, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text moneyText = CreateResourceReadout(topBar, "FUNDS", "100");
        Text sanityText = CreateResourceReadout(topBar, "SANITY", "100");
        Text artifactsText = CreateResourceReadout(topBar, "ARTIFACTS", "5");
        Toggle pauseToggle = CreatePauseToggle(topBar);
        Button settingsButton = CreateButton("Settings Button", topBar, "SETTINGS", new Vector2(120f, 42f));
        settingsButton.GetComponent<TooltipTrigger>().message = "Settings placeholder. Hook options here when audio/save settings exist.";
        Button menuButton = CreateButton("Menu Button", topBar, "MENU", new Vector2(96f, 42f));
        menuButton.gameObject.AddComponent<ReturnToMenu>();

        RectTransform mapPanel = CreatePanel("World Map Panel", root, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        mapPanel.offsetMin = new Vector2(340f, 128f);
        mapPanel.offsetMax = new Vector2(-380f, -104f);
        mapPanel.GetComponent<Image>().color = new Color(0.025f, 0.032f, 0.06f, 0.98f);
        CreateGridLines(mapPanel, 12, 7);
        CreateCornerMarks(mapPanel);

        RectTransform regionLayer = CreateUIObject("Region Layer", mapPanel).transform as RectTransform;
        Stretch(regionLayer);
        regionLayer.gameObject.AddComponent<HexMapCameraController>();

        RectTransform mapModePanel = CreatePanel("Map Mode Buttons", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(600f, 56f));
        mapModePanel.GetComponent<Image>().color = new Color(0.035f, 0.043f, 0.085f, 0.92f);
        AddHorizontalLayout(mapModePanel.gameObject, 8, 8, 8, TextAnchor.MiddleCenter);
        MapViewController mapViewController = mapModePanel.gameObject.AddComponent<MapViewController>();
        Button defaultMapButton = CreateButton("Default Map Button", mapModePanel, "DEFAULT", new Vector2(136f, 38f));
        Button influenceMapButton = CreateButton("Influence Map Button", mapModePanel, "INFLUENCE", new Vector2(136f, 38f));
        Button stabilityMapButton = CreateButton("Stability Map Button", mapModePanel, "STABILITY", new Vector2(136f, 38f));
        Button developmentMapButton = CreateButton("Development Map Button", mapModePanel, "DEVELOP", new Vector2(136f, 38f));
        WireButton(defaultMapButton, mapViewController.ShowStandardMap);
        WireButton(influenceMapButton, mapViewController.ShowInfluenceMap);
        WireButton(stabilityMapButton, mapViewController.ShowStabilityMap);
        WireButton(developmentMapButton, mapViewController.ShowDevelopmentMap);

        RectTransform worldPanel = CreatePanel("World Overview Panel", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -12f), new Vector2(300f, -116f));
        worldPanel.offsetMin = new Vector2(18f, 114f);
        worldPanel.offsetMax = new Vector2(318f, -96f);
        AddVerticalLayout(worldPanel.gameObject, 14, 14, 10, TextAnchor.UpperLeft);
        Text regionTitle = AddText(CreateLayoutItem("Region Title", worldPanel, -1, 42).transform as RectTransform, "WORLD OVERVIEW", 22, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text influenceText = CreateStatReadout(worldPanel, "INFLUENCE", "--");
        Text stabilityText = CreateStatReadout(worldPanel, "STABILITY", "--");
        Text developmentText = CreateStatReadout(worldPanel, "DEVELOPMENT", "--");
        Text modifiersText = AddText(CreateLayoutItem("Modifiers", worldPanel, -1, 160).transform as RectTransform, "Modifiers: n/a", 13, UITheme.TextSecondary, TextAnchor.UpperLeft, FontStyle.Normal);

        RectTransform decisionPanel = CreatePanel("Decision Panel", root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, -12f), new Vector2(340f, -116f));
        decisionPanel.offsetMin = new Vector2(-358f, 114f);
        decisionPanel.offsetMax = new Vector2(-18f, -96f);
        AddVerticalLayout(decisionPanel.gameObject, 14, 14, 10, TextAnchor.UpperLeft);
        AddText(CreateLayoutItem("Decision Header", decisionPanel, -1, 36).transform as RectTransform, "DIRECTIVES", 22, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        ScrollRect decisionScroll = CreateScrollView("Decision Scroll", decisionPanel, out RectTransform decisionContent);
        DecisionPanelUI decisionPanelUI = decisionPanel.gameObject.AddComponent<DecisionPanelUI>();
        decisionPanelUI.contentParent = decisionContent;
        decisionPanelUI.decisionButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DecisionButtonPrefabPath);

        RectTransform logPanel = CreatePanel("Operations Log Panel", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(-36f, 72f));
        logPanel.offsetMin = new Vector2(18f, 16f);
        logPanel.offsetMax = new Vector2(-18f, 88f);
        Text logText = AddText(CreateLayoutItem("Operations Log Text", logPanel, -1, 64).transform as RectTransform, "Operations log initialized.", 12, UITheme.TextSecondary, TextAnchor.UpperLeft, FontStyle.Normal);
        logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        logText.verticalOverflow = VerticalWrapMode.Truncate;
        PlayerLogUI logUI = logPanel.gameObject.AddComponent<PlayerLogUI>();
        SetObjectReference(logUI, "logText", logText);
        canvas.gameObject.AddComponent<SaveLoadMenuUI>();
        canvas.gameObject.AddComponent<MapLegendUI>();
        canvas.gameObject.AddComponent<CharacterPanelUI>();

        RectTransform endPanel = CreatePanel("End Screen", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 240f));
        Text endText = AddText(endPanel, "END STATE", 30, UITheme.AccentPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        endPanel.gameObject.SetActive(false);

        CreateTooltip(root);

        levelController.regionsContainer = regionLayer;
        levelController.regionUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RegionPrefabPath);
        levelController.hexMapGenerator = hexMapGenerator;
        levelController.regionGridGenerator = regionGridGenerator;
        levelController.uiRaycaster = raycaster;
        levelController.eventSystem = eventSystem;
        levelController.finalMessage = endText;
        SetObjectReference(levelController, "worldClickSurface", mapPanel);

        SetObjectReference(hexMapGenerator, "levelController", levelController);
        SetObjectReference(hexMapGenerator, "regionsContainer", regionLayer);
        SetObjectReference(hexMapGenerator, "regionUIPrefab", levelController.regionUIPrefab);

        SetObjectReference(regionGridGenerator, "levelController", levelController);
        SetObjectReference(regionGridGenerator, "regionsContainer", regionLayer);
        SetObjectReference(regionGridGenerator, "regionUIPrefab", levelController.regionUIPrefab);
        SetSerializedValue(regionGridGenerator, "rows", 4);
        SetSerializedValue(regionGridGenerator, "columns", 6);
        SetSerializedValue(regionGridGenerator, "cellSize", new Vector2(142f, 96f));
        SetSerializedValue(regionGridGenerator, "spacing", new Vector2(18f, 18f));
        SetSerializedValue(regionGridGenerator, "continentCount", 4);
        SetSerializedValue(regionGridGenerator, "minRegionsPerContinent", 4);
        SetSerializedValue(regionGridGenerator, "maxRegionsPerContinent", 7);
        SetSerializedValue(regionGridGenerator, "generateContinentalGraphs", true);

        SetObjectReference(mapViewController, "levelController", levelController);
        SetObjectReference(mapViewController, "standardButton", defaultMapButton);
        SetObjectReference(mapViewController, "influenceButton", influenceMapButton);
        SetObjectReference(mapViewController, "stabilityButton", stabilityMapButton);
        SetObjectReference(mapViewController, "developmentButton", developmentMapButton);

        SetObjectReference(uiManager, "regionPanel", worldPanel.gameObject);
        uiManager.nameText = regionTitle;
        uiManager.influenceText = influenceText;
        uiManager.stabilityText = stabilityText;
        uiManager.developmentText = developmentText;
        uiManager.modifiersText = modifiersText;
        uiManager.sanityText = sanityText;
        uiManager.moneyText = moneyText;
        uiManager.artifactsText = artifactsText;
        uiManager.pauseToggle = pauseToggle;
        uiManager.timerText = dateText;
        uiManager.endScreen = endPanel.gameObject;
        uiManager.endText = endText;
        uiManager.worldTitle = "WORLD OVERVIEW";

        eventManager.eventPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventPanelPrefabPath);
        eventManager.canvasTransform = canvas.transform;

        RenderSettings.ambientLight = new Color(0.08f, 0.10f, 0.14f, 1f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("Institute main scene rebuilt: " + ScenePath);
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Prefabs/Map");
        EnsureFolder("Assets/Prefabs/Characters");
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Data");
        EnsureFolder("Assets/Data/Events");
        EnsureFolder("Assets/Data/Decisions");
        EnsureFolder("Assets/Docs");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string name = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void CreateOrUpdatePrefabs()
    {
        SavePrefab(CreateRegionPrefab(), RegionPrefabPath);
        SavePrefab(CreateDecisionButtonPrefab(), DecisionButtonPrefabPath);
        SavePrefab(CreateOptionButtonPrefab(), OptionButtonPrefabPath);
        SavePrefab(CreateEventPanelPrefab(), EventPanelPrefabPath);
    }

    static GameObject CreateRegionPrefab()
    {
        GameObject root = CreateUIObject("RegionUI", null);
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(150f, 100f);
        Image image = root.AddComponent<Image>();
        image.color = new Color(0.14f, 0.18f, 0.30f, 0.92f);
        image.raycastTarget = true;
        root.AddComponent<Button>().targetGraphic = image;
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 0.68f, 0.55f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        Text label = AddText(rect, "REGION", 13, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        label.raycastTarget = false;

        RegionUI regionUI = root.AddComponent<RegionUI>();
        regionUI.regionNameText = label;
        regionUI.regionImage = image;
        regionUI.hoverColor = UITheme.MapHighlight;
        return root;
    }

    static GameObject CreateDecisionButtonPrefab()
    {
        GameObject root = CreateUIObject("decisionButton", null);
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(320f, 96f);
        Image image = root.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        root.AddComponent<StyledButton>();
        root.AddComponent<TooltipTrigger>();
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 96f;
        layout.minHeight = 88f;

        Image cooldown = CreateImage("Cooldown Fill", rect, new Color(0f, 0f, 0f, 0.35f));
        Stretch(cooldown.rectTransform);
        cooldown.type = Image.Type.Filled;
        cooldown.fillMethod = Image.FillMethod.Vertical;
        cooldown.fillOrigin = (int)Image.OriginVertical.Top;
        cooldown.gameObject.SetActive(false);

        RectTransform content = CreateUIObject("Content", rect).transform as RectTransform;
        Stretch(content, new Vector2(12f, 8f), new Vector2(-12f, -8f));
        AddVerticalLayout(content.gameObject, 0, 0, 2, TextAnchor.MiddleLeft);
        Text title = AddText(CreateLayoutItem("Title", content, -1, 24).transform as RectTransform, "Decision", 15, UITheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text details = AddText(CreateLayoutItem("Details", content, -1, 42).transform as RectTransform, "Cost: none\nEffect: none", 11, UITheme.TextSecondary, TextAnchor.UpperLeft, FontStyle.Normal);
        Text status = AddText(CreateLayoutItem("Status", content, -1, 18).transform as RectTransform, "Ready", 10, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Italic);

        ActionButton actionButton = root.AddComponent<ActionButton>();
        actionButton.labelText = title;
        actionButton.detailText = details;
        actionButton.statusText = status;
        actionButton.cooldownFill = cooldown;
        return root;
    }

    static GameObject CreateOptionButtonPrefab()
    {
        GameObject root = CreateUIObject("optionButton", null);
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(680f, 72f);
        Image image = root.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        root.AddComponent<StyledButton>();
        root.AddComponent<TooltipTrigger>();
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 76f;
        Text label = AddText(rect, "Option", 14, UITheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Normal);
        RectTransform labelRect = label.transform as RectTransform;
        Stretch(labelRect, new Vector2(14f, 6f), new Vector2(-14f, -6f));
        return root;
    }

    static GameObject CreateEventPanelPrefab()
    {
        GameObject root = CreateUIObject("EventPanel", null);
        RectTransform rootRect = root.transform as RectTransform;
        Stretch(rootRect);
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.58f);

        RectTransform panel = CreatePanel("Panel", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 540f));
        AddVerticalLayout(panel.gameObject, 18, 18, 12, TextAnchor.UpperLeft);
        AddText(CreateLayoutItem("Header", panel, -1, 36).transform as RectTransform, "INSTITUTE EVENT", 22, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text description = AddText(CreateLayoutItem("DescriptionText", panel, -1, 190).transform as RectTransform, "Event description.", 15, UITheme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Normal);
        description.horizontalOverflow = HorizontalWrapMode.Wrap;
        description.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform featured = CreatePanel("FeaturedPeople", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 88f));
        LayoutElement featuredLayout = featured.gameObject.AddComponent<LayoutElement>();
        featuredLayout.preferredHeight = 88f;
        ScrollRect featuredScroll = CreateScrollView("PeopleScroll", featured, out RectTransform peopleContent);
        featuredScroll.gameObject.name = "Viewport";

        RectTransform options = CreateUIObject("OptionsContainer", panel).transform as RectTransform;
        LayoutElement optionsLayout = options.gameObject.AddComponent<LayoutElement>();
        optionsLayout.preferredHeight = 150f;
        AddVerticalLayout(options.gameObject, 0, 0, 8, TextAnchor.UpperCenter);

        EventPanelUI panelUI = root.AddComponent<EventPanelUI>();
        panelUI.descriptionText = description;
        panelUI.optionsContainer = options;
        panelUI.optionButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionButtonPrefabPath);
        return root;
    }

    static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static Camera CreateMainCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = UITheme.BackgroundDark;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = -10f;
        camera.farClipPlane = 100f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    static Canvas CreateCanvas(Camera camera, out GraphicRaycaster raycaster)
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static EventSystem CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        return eventSystem;
    }

    static Text CreateResourceReadout(RectTransform parent, string label, string value)
    {
        RectTransform item = CreateLayoutItem(label + " Resource", parent, 150, 46).transform as RectTransform;
        AddVerticalLayout(item.gameObject, 0, 0, 0, TextAnchor.MiddleLeft);
        AddText(CreateLayoutItem(label + " Label", item, -1, 16).transform as RectTransform, label, 10, UITheme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
        return AddText(CreateLayoutItem(label + " Value", item, -1, 26).transform as RectTransform, value, 20, UITheme.AccentSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    static Text CreateStatReadout(RectTransform parent, string label, string value)
    {
        RectTransform item = CreateLayoutItem(label + " Stat", parent, -1, 58).transform as RectTransform;
        AddVerticalLayout(item.gameObject, 0, 0, 2, TextAnchor.MiddleLeft);
        AddText(CreateLayoutItem(label + " Label", item, -1, 18).transform as RectTransform, label, 11, UITheme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
        return AddText(CreateLayoutItem(label + " Value", item, -1, 32).transform as RectTransform, value, 22, UITheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    static Toggle CreatePauseToggle(RectTransform parent)
    {
        RectTransform root = CreateLayoutItem("Pause Toggle", parent, 120, 42).transform as RectTransform;
        Image background = root.gameObject.AddComponent<Image>();
        background.color = UITheme.ButtonNormal;
        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        Text label = AddText(root, "PAUSE", 14, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        label.raycastTarget = false;
        return toggle;
    }

    static Button CreateButton(string name, RectTransform parent, string label, Vector2 size)
    {
        RectTransform rect = CreateLayoutItem(name, parent, size.x, size.y).transform as RectTransform;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        rect.gameObject.AddComponent<StyledButton>();
        rect.gameObject.AddComponent<TooltipTrigger>();
        Text text = AddText(rect, label, 13, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        text.raycastTarget = false;
        return button;
    }

    static ScrollRect CreateScrollView(string name, RectTransform parent, out RectTransform content)
    {
        RectTransform root = CreateUIObject(name, parent).transform as RectTransform;
        LayoutElement rootLayout = root.gameObject.AddComponent<LayoutElement>();
        rootLayout.flexibleHeight = 1f;
        Stretch(root);
        Image viewportImage = root.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.18f);
        Mask mask = root.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        content = CreateUIObject("Content", root).transform as RectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        AddVerticalLayout(content.gameObject, 8, 8, 8, TextAnchor.UpperCenter);
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = root;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 22f;
        return scroll;
    }

    static RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateUIObject(name, parent).transform as RectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = UITheme.PanelBackground;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(UITheme.PanelBorder.r, UITheme.PanelBorder.g, UITheme.PanelBorder.b, 0.65f);
        outline.effectDistance = new Vector2(1f, -1f);
        StyledPanel styledPanel = rect.gameObject.AddComponent<StyledPanel>();
        styledPanel.animateOnShow = false;
        return rect;
    }

    static Image CreateImage(string name, RectTransform parent, Color color)
    {
        RectTransform rect = CreateUIObject(name, parent).transform as RectTransform;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static Text AddText(RectTransform parent, string text, int size, Color color, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = CreateUIObject(parent.gameObject.name + " Text", parent);
        RectTransform rect = textObject.transform as RectTransform;
        Stretch(rect);
        Text label = textObject.AddComponent<Text>();
        label.font = BuiltInFont;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = style;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    static GameObject CreateLayoutItem(string name, RectTransform parent, float preferredWidth, float preferredHeight)
    {
        GameObject item = CreateUIObject(name, parent);
        LayoutElement layout = item.AddComponent<LayoutElement>();
        if (preferredWidth >= 0f)
            layout.preferredWidth = preferredWidth;
        if (preferredHeight >= 0f)
            layout.preferredHeight = preferredHeight;
        return item;
    }

    static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    static void AddVerticalLayout(GameObject go, int horizontalPadding, int verticalPadding, int spacing, TextAnchor alignment)
    {
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    static void AddHorizontalLayout(GameObject go, int horizontalPadding, int verticalPadding, int spacing, TextAnchor alignment)
    {
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    static void CreateGridLines(RectTransform parent, int columns, int rows)
    {
        for (int i = 1; i < columns; i++)
        {
            float x = (float)i / columns;
            Image line = CreateImage("Grid Vertical " + i, parent, new Color(0f, 0.85f, 0.68f, 0.055f));
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.sizeDelta = new Vector2(1f, 0f);
            rect.anchoredPosition = Vector2.zero;
            line.raycastTarget = false;
        }

        for (int i = 1; i < rows; i++)
        {
            float y = (float)i / rows;
            Image line = CreateImage("Grid Horizontal " + i, parent, new Color(0f, 0.85f, 0.68f, 0.055f));
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            line.raycastTarget = false;
        }
    }

    static void CreateCornerMarks(RectTransform parent)
    {
        Vector2[] anchors =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f)
        };

        for (int i = 0; i < anchors.Length; i++)
        {
            Image mark = CreateImage("Corner Mark " + i, parent, UITheme.AccentPrimary);
            mark.raycastTarget = false;
            RectTransform rect = mark.rectTransform;
            rect.anchorMin = anchors[i];
            rect.anchorMax = anchors[i];
            rect.pivot = anchors[i];
            rect.sizeDelta = new Vector2(34f, 3f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    static void CreateTooltip(RectTransform root)
    {
        RectTransform panel = CreatePanel("TooltipPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(320f, 96f));
        panel.gameObject.GetComponent<StyledPanel>().animateOnShow = false;
        Text tooltipText = AddText(panel, "", 13, UITheme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Normal);
        RectTransform textRect = tooltipText.transform as RectTransform;
        Stretch(textRect, new Vector2(10f, 8f), new Vector2(-10f, -8f));
        Tooltip tooltip = root.gameObject.AddComponent<Tooltip>();
        SetObjectReference(tooltip, "panel", panel.gameObject);
        SetObjectReference(tooltip, "legacyTooltipText", tooltipText);
        panel.gameObject.SetActive(false);
    }

    static void WireButton(Button button, UnityAction action)
    {
        button.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    static Font BuiltInFont
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cachedFont;
        }
    }

    static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"Could not find serialized property {propertyName} on {target.name}.");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedValue(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedValue(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedValue(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedValue(Object target, string propertyName, Vector2 value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.vector2Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
