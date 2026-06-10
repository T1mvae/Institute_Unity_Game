using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameplaySceneInstaller : MonoBehaviour
{
    [SerializeField] private GameObject regionUIPrefab;
    [SerializeField] private GameObject decisionButtonPrefab;
    [SerializeField] private GameObject eventPanelPrefab;

    public void Awake()
    {
        ThemeLoader.LoadOrCreateDefault();
        SceneFlowManager.EnsureExists();

        if (FindFirstObjectByType<LevelController>() != null)
            return;

        BuildGameplayScene();
    }

    public void BuildGameplayScene()
    {
        Camera mainCamera = CreateCamera();
        Canvas canvas = CreateCanvas(mainCamera, out GraphicRaycaster raycaster);
        EventSystem eventSystem = CreateEventSystem();

        GameObject systems = new GameObject("Gameplay Systems");
        EnsureComponent<GameManager>(systems);
        EnsureComponent<ResourceManager>(systems);
        EnsureComponent<TimeManager>(systems);
        EnsureComponent<RegionManager>(systems);
        EnsureComponent<DecisionPool>(systems);
        EnsureComponent<DecisionSelectionManager>(systems);
        if (SaveLoadManager.Instance == null)
            systems.AddComponent<SaveLoadManager>();
        if (CharacterManager.Instance == null)
            systems.AddComponent<CharacterManager>();
        EnsureComponent<GameDateTracker>(systems);
        EnsureComponent<TimerUI>(systems);
        EventManager eventManager = EnsureComponent<EventManager>(systems);

        GameObject controllerObject = new GameObject("Gameplay Controller");
        LevelController levelController = controllerObject.AddComponent<LevelController>();
        HexMapGenerator hexMapGenerator = controllerObject.AddComponent<HexMapGenerator>();
        controllerObject.AddComponent<RegionGridGenerator>();

        RectTransform root = canvas.transform as RectTransform;
        Image background = CreateImage("Gameplay Background", root, UITheme.BackgroundDark);
        Stretch(background.rectTransform);
        background.raycastTarget = false;

        RectTransform topBar = CreatePanel("Top Resource Bar", root);
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.pivot = new Vector2(0.5f, 1f);
        topBar.offsetMin = new Vector2(16f, -88f);
        topBar.offsetMax = new Vector2(-16f, -16f);
        AddHorizontalLayout(topBar.gameObject, 14, 14, 12, TextAnchor.MiddleLeft);

        Text dateText = CreateReadout(topBar, "DATE", "DAY 001", 220f);
        Text moneyText = CreateReadout(topBar, "MONEY", "--", 130f);
        Text artifactsText = CreateReadout(topBar, "ARTIFACTS", "--", 150f);
        Text sanityText = CreateReadout(topBar, "SANITY", "--", 130f);
        Toggle pauseToggle = CreatePauseToggle(topBar);

        RectTransform mapPanel = CreatePanel("Map Viewport", root);
        mapPanel.anchorMin = Vector2.zero;
        mapPanel.anchorMax = Vector2.one;
        mapPanel.offsetMin = new Vector2(352f, 158f);
        mapPanel.offsetMax = new Vector2(-392f, -104f);
        mapPanel.GetComponent<Image>().color = UITheme.PanelAlt;
        CreateGridOverlay(mapPanel);

        RectTransform regionLayer = CreateUIObject("Hex Region Layer", mapPanel).transform as RectTransform;
        Stretch(regionLayer, new Vector2(18f, 18f), new Vector2(-18f, -18f));
        regionLayer.gameObject.AddComponent<HexMapCameraController>();

        RectTransform leftPanel = CreatePanel("Region Dossier Panel", root);
        leftPanel.anchorMin = new Vector2(0f, 0f);
        leftPanel.anchorMax = new Vector2(0f, 1f);
        leftPanel.pivot = new Vector2(0f, 0.5f);
        leftPanel.offsetMin = new Vector2(16f, 158f);
        leftPanel.offsetMax = new Vector2(336f, -104f);
        AddVerticalLayout(leftPanel.gameObject, 14, 14, 10, TextAnchor.UpperLeft);
        Text regionTitle = AddText(CreateLayoutItem("Region Title", leftPanel, -1, 48).transform as RectTransform, "WORLD OVERVIEW", 20, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text influenceText = CreateStatReadout(leftPanel, "INFLUENCE", "--");
        Text stabilityText = CreateStatReadout(leftPanel, "STABILITY", "--");
        Text developmentText = CreateStatReadout(leftPanel, "DEVELOPMENT", "--");
        Text modifiersText = AddText(CreateLayoutItem("Region Modifiers", leftPanel, -1, 220).transform as RectTransform, "Modifiers: n/a", 12, UITheme.TextSecondary, TextAnchor.UpperLeft, FontStyle.Normal);

        RectTransform rightPanel = CreatePanel("Context Actions Panel", root);
        rightPanel.anchorMin = new Vector2(1f, 0f);
        rightPanel.anchorMax = new Vector2(1f, 1f);
        rightPanel.pivot = new Vector2(1f, 0.5f);
        rightPanel.offsetMin = new Vector2(-376f, 158f);
        rightPanel.offsetMax = new Vector2(-16f, -104f);
        AddVerticalLayout(rightPanel.gameObject, 12, 12, 10, TextAnchor.UpperLeft);
        AddText(CreateLayoutItem("Directive Header", rightPanel, -1, 30).transform as RectTransform, "DIRECTIVES", 18, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        ScrollRect decisionScroll = CreateScrollView("Decision Scroll", rightPanel, 210f, out RectTransform decisionContent);
        DecisionPanelUI decisionPanelUI = decisionScroll.gameObject.AddComponent<DecisionPanelUI>();
        decisionPanelUI.contentParent = decisionContent;
        decisionPanelUI.decisionButtonPrefab = decisionButtonPrefab != null ? decisionButtonPrefab : CreateDecisionButtonPrefab();
        decisionScroll.gameObject.AddComponent<DecisionPanelController>();

        RectTransform characterHost = CreateLayoutItem("Character Interaction Host", rightPanel, -1, 270).transform as RectTransform;
        CharacterPanelUI characterPanel = characterHost.gameObject.AddComponent<CharacterPanelUI>();

        RectTransform bottomLog = CreatePanel("Operations Log", root);
        bottomLog.anchorMin = new Vector2(0f, 0f);
        bottomLog.anchorMax = new Vector2(1f, 0f);
        bottomLog.pivot = new Vector2(0.5f, 0f);
        bottomLog.offsetMin = new Vector2(16f, 16f);
        bottomLog.offsetMax = new Vector2(-16f, 142f);
        Text logText = AddText(bottomLog, "Operations log initialized.", 12, UITheme.TextSecondary, TextAnchor.UpperLeft, FontStyle.Normal);
        Stretch(logText.rectTransform, new Vector2(14f, 10f), new Vector2(-14f, -10f));
        PlayerLogUI logUI = bottomLog.gameObject.AddComponent<PlayerLogUI>();
        SetObjectReference(logUI, "logText", logText);
        bottomLog.gameObject.AddComponent<GameLogUIController>();

        RectTransform mapModeBar = CreatePanel("Map Mode Bar", root);
        mapModeBar.anchorMin = new Vector2(0.5f, 0f);
        mapModeBar.anchorMax = new Vector2(0.5f, 0f);
        mapModeBar.pivot = new Vector2(0.5f, 0f);
        mapModeBar.sizeDelta = new Vector2(640f, 48f);
        mapModeBar.anchoredPosition = new Vector2(0f, 150f);
        AddHorizontalLayout(mapModeBar.gameObject, 8, 8, 8, TextAnchor.MiddleCenter);
        MapViewController mapViewController = mapModeBar.gameObject.AddComponent<MapViewController>();
        Button defaultMapButton = CreateButton("Default Map Button", mapModeBar, "DEFAULT", new Vector2(130f, 34f));
        Button influenceMapButton = CreateButton("Influence Map Button", mapModeBar, "INFLUENCE", new Vector2(130f, 34f));
        Button stabilityMapButton = CreateButton("Stability Map Button", mapModeBar, "STABILITY", new Vector2(130f, 34f));
        Button developmentMapButton = CreateButton("Development Map Button", mapModeBar, "DEVELOP", new Vector2(130f, 34f));
        defaultMapButton.onClick.AddListener(mapViewController.ShowStandardMap);
        influenceMapButton.onClick.AddListener(mapViewController.ShowInfluenceMap);
        stabilityMapButton.onClick.AddListener(mapViewController.ShowStabilityMap);
        developmentMapButton.onClick.AddListener(mapViewController.ShowDevelopmentMap);

        RectTransform popupLayer = CreateUIObject("Popup Layer", root).transform as RectTransform;
        Stretch(popupLayer);
        popupLayer.gameObject.AddComponent<EventPopupController>();

        RectTransform endPanel = CreatePanel("End Screen", root);
        endPanel.anchorMin = new Vector2(0.5f, 0.5f);
        endPanel.anchorMax = new Vector2(0.5f, 0.5f);
        endPanel.pivot = new Vector2(0.5f, 0.5f);
        endPanel.sizeDelta = new Vector2(520f, 240f);
        Text endText = AddText(endPanel, "END STATE", 30, UITheme.AccentPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        endPanel.gameObject.SetActive(false);

        PauseMenuController pauseMenu = canvas.gameObject.AddComponent<PauseMenuController>();
        pauseMenu.Build(canvas);
        pauseToggle.onValueChanged.AddListener(isPaused => pauseMenu.SetVisible(isPaused));

        GameplayHUDController hud = canvas.gameObject.AddComponent<GameplayHUDController>();
        hud.Bind(moneyText, artifactsText, sanityText, dateText);

        UIManager uiManager = canvas.gameObject.AddComponent<UIManager>();
        SetObjectReference(uiManager, "regionPanel", leftPanel.gameObject);
        uiManager.nameText = regionTitle;
        uiManager.influenceText = influenceText;
        uiManager.stabilityText = stabilityText;
        uiManager.developmentText = developmentText;
        uiManager.modifiersText = modifiersText;
        uiManager.moneyText = moneyText;
        uiManager.artifactsText = artifactsText;
        uiManager.sanityText = sanityText;
        uiManager.pauseToggle = pauseToggle;
        uiManager.timerText = dateText;
        uiManager.endScreen = endPanel.gameObject;
        uiManager.endText = endText;
        uiManager.worldTitle = "WORLD OVERVIEW";

        regionUIPrefab = regionUIPrefab != null ? regionUIPrefab : CreateRegionPrefab();
        eventPanelPrefab = eventPanelPrefab != null ? eventPanelPrefab : CreateEventPanelPrefab();

        levelController.regionsContainer = regionLayer;
        levelController.regionUIPrefab = regionUIPrefab;
        levelController.hexMapGenerator = hexMapGenerator;
        levelController.uiRaycaster = raycaster;
        levelController.eventSystem = eventSystem;
        levelController.finalMessage = endText;
        SetObjectReference(levelController, "worldClickSurface", mapPanel);

        SetObjectReference(hexMapGenerator, "levelController", levelController);
        SetObjectReference(hexMapGenerator, "regionsContainer", regionLayer);
        SetObjectReference(hexMapGenerator, "regionUIPrefab", regionUIPrefab);

        SetObjectReference(mapViewController, "levelController", levelController);
        SetObjectReference(mapViewController, "standardButton", defaultMapButton);
        SetObjectReference(mapViewController, "influenceButton", influenceMapButton);
        SetObjectReference(mapViewController, "stabilityButton", stabilityMapButton);
        SetObjectReference(mapViewController, "developmentButton", developmentMapButton);

        eventManager.eventPanelPrefab = eventPanelPrefab;
        eventManager.canvasTransform = popupLayer;

        PlayerLog.Add("Gameplay scene initialized.");
    }

    static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T existing = gameObject.GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }

    Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = UITheme.BackgroundDark;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraObject.tag = "MainCamera";
        return camera;
    }

    Canvas CreateCanvas(Camera camera, out GraphicRaycaster raycaster)
    {
        GameObject canvasObject = new GameObject("Gameplay Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    EventSystem CreateEventSystem()
    {
        EventSystem existing = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (existing != null)
            return existing;

        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        return eventSystem;
    }

    RectTransform CreatePanel(string name, RectTransform parent)
    {
        RectTransform rect = CreateUIObject(name, parent).transform as RectTransform;
        UITheme.StylePanel(rect.gameObject);
        return rect;
    }

    Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    GameObject CreateUIObject(string name, RectTransform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    void AddHorizontalLayout(GameObject gameObject, int left, int right, int spacing, TextAnchor alignment)
    {
        HorizontalLayoutGroup layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(left, right, 8, 8);
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
    }

    void AddVerticalLayout(GameObject gameObject, int horizontal, int vertical, int spacing, TextAnchor alignment)
    {
        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical);
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
    }

    Text CreateReadout(RectTransform parent, string label, string value, float width)
    {
        RectTransform item = CreateLayoutItem(label + " Readout", parent, width, 48).transform as RectTransform;
        AddVerticalLayout(item.gameObject, 0, 0, 0, TextAnchor.MiddleLeft);
        AddText(CreateLayoutItem(label + " Label", item, -1, 16).transform as RectTransform, label, 10, UITheme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
        return AddText(CreateLayoutItem(label + " Value", item, -1, 28).transform as RectTransform, value, 20, UITheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    Text CreateStatReadout(RectTransform parent, string label, string value)
    {
        RectTransform item = CreateLayoutItem(label + " Stat", parent, -1, 60).transform as RectTransform;
        AddVerticalLayout(item.gameObject, 0, 0, 2, TextAnchor.MiddleLeft);
        AddText(CreateLayoutItem(label + " Label", item, -1, 18).transform as RectTransform, label, 11, UITheme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
        return AddText(CreateLayoutItem(label + " Value", item, -1, 34).transform as RectTransform, value, 22, UITheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
    }

    GameObject CreateLayoutItem(string name, RectTransform parent, float width, float height)
    {
        GameObject item = CreateUIObject(name, parent);
        LayoutElement layout = item.AddComponent<LayoutElement>();
        if (width > 0f)
            layout.preferredWidth = width;
        else
            layout.flexibleWidth = 1f;
        if (height > 0f)
            layout.preferredHeight = height;
        return item;
    }

    Text AddText(RectTransform parent, string content, int size, Color color, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = CreateUIObject("Text", parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform rect = text.transform as RectTransform;
        Stretch(rect);
        return text;
    }

    Toggle CreatePauseToggle(RectTransform parent)
    {
        RectTransform root = CreateLayoutItem("Pause Toggle", parent, 110, 44).transform as RectTransform;
        Image background = root.gameObject.AddComponent<Image>();
        background.color = UITheme.ButtonNormal;
        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        AddText(root, "PAUSE", 13, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
        return toggle;
    }

    Button CreateButton(string name, RectTransform parent, string label, Vector2 size)
    {
        RectTransform root = CreateUIObject(name, parent).transform as RectTransform;
        root.sizeDelta = size;
        LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;
        Image image = root.gameObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UITheme.StyleButton(button);
        AddText(root, label, 12, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold).raycastTarget = false;
        return button;
    }

    ScrollRect CreateScrollView(string name, RectTransform parent, float height, out RectTransform content)
    {
        RectTransform root = CreateLayoutItem(name, parent, -1, height).transform as RectTransform;
        Image image = root.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.12f);
        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = CreateUIObject("Viewport", root).transform as RectTransform;
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        content = CreateUIObject("Content", viewport).transform as RectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        scroll.horizontal = false;
        return scroll;
    }

    GameObject CreateRegionPrefab()
    {
        GameObject root = CreateUIObject("RegionUI Runtime Prefab", null);
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(150f, 100f);
        Image image = root.AddComponent<Image>();
        image.color = UITheme.PanelAlt;
        image.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = UITheme.AccentPrimary;
        outline.effectDistance = new Vector2(1f, -1f);
        Text label = AddText(rect, "REGION", 13, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
        label.raycastTarget = false;
        RegionUI regionUI = root.AddComponent<RegionUI>();
        regionUI.regionNameText = label;
        regionUI.regionImage = image;
        regionUI.hoverColor = UITheme.MapHighlight;
        root.SetActive(false);
        return root;
    }

    GameObject CreateDecisionButtonPrefab()
    {
        GameObject root = CreateUIObject("Decision Button Runtime Prefab", null);
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(330f, 94f);
        Image image = root.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        root.AddComponent<StyledButton>();
        root.AddComponent<TooltipTrigger>();
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 94f;

        RectTransform content = CreateUIObject("Content", rect).transform as RectTransform;
        Stretch(content, new Vector2(12f, 8f), new Vector2(-12f, -8f));
        AddVerticalLayout(content.gameObject, 0, 0, 2, TextAnchor.MiddleLeft);
        Text title = AddText(CreateLayoutItem("Title", content, -1, 22).transform as RectTransform, "Decision", 14, UITheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text detail = AddText(CreateLayoutItem("Details", content, -1, 44).transform as RectTransform, "Cost: none", 11, UITheme.TextSecondary, TextAnchor.UpperLeft, FontStyle.Normal);
        Text status = AddText(CreateLayoutItem("Status", content, -1, 18).transform as RectTransform, "Ready", 10, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Italic);
        ActionButton actionButton = root.AddComponent<ActionButton>();
        actionButton.labelText = title;
        actionButton.detailText = detail;
        actionButton.statusText = status;
        root.SetActive(false);
        return root;
    }

    GameObject CreateEventPanelPrefab()
    {
        GameObject root = CreateUIObject("Event Panel Runtime Prefab", null);
        RectTransform rootRect = root.transform as RectTransform;
        Stretch(rootRect);
        Image overlay = root.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, ThemeLoader.Current.opacity.overlay);

        RectTransform card = CreatePanel("Event Card", rootRect);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(720f, 520f);
        AddVerticalLayout(card.gameObject, 18, 18, 12, TextAnchor.UpperLeft);
        AddText(CreateLayoutItem("Header", card, -1, 36).transform as RectTransform, "INSTITUTE EVENT", 22, UITheme.AccentPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text description = AddText(CreateLayoutItem("DescriptionText", card, -1, 190).transform as RectTransform, "Event description.", 15, UITheme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Normal);
        ScrollRect optionsScroll = CreateScrollView("Options", card, 220f, out RectTransform optionsContent);
        EventPanelUI eventPanel = root.AddComponent<EventPanelUI>();
        eventPanel.descriptionText = description;
        eventPanel.optionsContainer = optionsContent;
        eventPanel.optionButtonPrefab = CreateOptionButtonPrefab();
        root.SetActive(false);
        return root;
    }

    GameObject CreateOptionButtonPrefab()
    {
        GameObject root = CreateUIObject("Option Button Runtime Prefab", null);
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
        Stretch(label.rectTransform, new Vector2(14f, 6f), new Vector2(-14f, -6f));
        root.SetActive(false);
        return root;
    }

    void CreateGridOverlay(RectTransform parent)
    {
        for (int i = 1; i < 12; i++)
        {
            Image line = CreateImage("Vertical Grid " + i, parent, new Color(UITheme.PanelBorder.r, UITheme.PanelBorder.g, UITheme.PanelBorder.b, 0.13f));
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(i / 12f, 0f);
            rect.anchorMax = new Vector2(i / 12f, 1f);
            rect.sizeDelta = new Vector2(1f, 0f);
        }

        for (int i = 1; i < 8; i++)
        {
            Image line = CreateImage("Horizontal Grid " + i, parent, new Color(UITheme.PanelBorder.r, UITheme.PanelBorder.g, UITheme.PanelBorder.b, 0.10f));
            RectTransform rect = line.rectTransform;
            rect.anchorMin = new Vector2(0f, i / 8f);
            rect.anchorMax = new Vector2(1f, i / 8f);
            rect.sizeDelta = new Vector2(0f, 1f);
        }
    }

    static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    static void SetObjectReference(Object target, string fieldName, Object value)
    {
        if (target == null)
            return;

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(target, value);
    }
}
