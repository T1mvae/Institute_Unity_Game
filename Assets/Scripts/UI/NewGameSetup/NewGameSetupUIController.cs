using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NewGameSetupUIController : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset layoutAsset;
    [SerializeField] private StyleSheet[] styleSheets;

    readonly Dictionary<DifficultyPreset, VisualElement> difficultyCards = new Dictionary<DifficultyPreset, VisualElement>();
    readonly Dictionary<string, TextField> customFields = new Dictionary<string, TextField>();

    UIDocument document;
    VisualElement difficultyList;
    VisualElement detailsPanel;
    DifficultyConfig selectedDifficulty;

    void Start()
    {
        ThemeLoader.LoadOrCreateDefault();
        SceneFlowManager.EnsureExists();
        LoadEditorAssetsIfNeeded();
        selectedDifficulty = DifficultyConfig.CreatePreset(DifficultyPreset.Normal);
        document = UIToolkitThemeUtility.EnsureDocument(gameObject);
        BuildUI();
        SelectDifficulty(DifficultyPreset.Normal);
    }

    void LoadEditorAssetsIfNeeded()
    {
        if (layoutAsset == null)
            layoutAsset = Institute.World.UI.OverlayUtil.LoadUxml("UI/UXML/NewGameSetup");
        if (styleSheets == null || styleSheets.Length == 0)
        {
            styleSheets = new[]
            {
                Institute.World.UI.OverlayUtil.LoadStyle("UI/Styles/base"),
                Institute.World.UI.OverlayUtil.LoadStyle("UI/Styles/new_game_setup"),
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
    }

    void WireUxml(VisualElement root)
    {
        difficultyList = root.Q<VisualElement>("DifficultyList");
        detailsPanel = root.Q<VisualElement>("DifficultyDetails");
        Button back = root.Q<Button>("BackButton");
        Button randomSeed = root.Q<Button>("RandomSeedButton");
        Button start = root.Q<Button>("StartGameButton");

        if (back != null)
            back.clicked += () => SceneFlowManager.EnsureExists().GoToMainMenu();
        if (randomSeed != null)
            randomSeed.clicked += RandomizeSeed;
        if (start != null)
            start.clicked += StartGame;

        BuildDifficultyCards();
    }

    void BuildFallback(VisualElement root)
    {
        root.AddToClassList("root-screen");
        root.AddToClassList("setup-root");
        root.style.paddingLeft = 28;
        root.style.paddingRight = 28;
        root.style.paddingTop = 28;
        root.style.paddingBottom = 20;

        VisualElement header = new VisualElement();
        header.AddToClassList("setup-header");
        header.style.height = 74;
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.Add(UIToolkitThemeUtility.Label("NEW OPERATION SETUP", "header"));
        header.Add(UIToolkitThemeUtility.Button("BACK", () => SceneFlowManager.EnsureExists().GoToMainMenu()));
        root.Add(header);

        VisualElement content = new VisualElement();
        content.AddToClassList("setup-content");
        content.style.flexGrow = 1;
        content.style.flexDirection = FlexDirection.Row;

        difficultyList = new ScrollView();
        difficultyList.AddToClassList("panel");
        difficultyList.AddToClassList("difficulty-list");
        difficultyList.style.width = Length.Percent(42);
        difficultyList.style.marginRight = 18;

        detailsPanel = UIToolkitThemeUtility.Panel("difficulty-details");
        detailsPanel.style.flexGrow = 1;

        content.Add(difficultyList);
        content.Add(detailsPanel);
        root.Add(content);

        VisualElement actions = new VisualElement();
        actions.AddToClassList("setup-actions");
        actions.style.height = 58;
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.justifyContent = Justify.FlexEnd;
        Button randomSeed = UIToolkitThemeUtility.Button("RANDOMIZE SEED", RandomizeSeed);
        randomSeed.style.marginRight = 10;
        actions.Add(randomSeed);
        actions.Add(UIToolkitThemeUtility.Button("START GAME", StartGame, "primary-button"));
        root.Add(actions);

        BuildDifficultyCards();
    }

    void BuildDifficultyCards()
    {
        difficultyCards.Clear();
        difficultyList.Clear();
        AddDifficultyCard(DifficultyPreset.Easy, "Forgiving start, stable regions, slower pressure.");
        AddDifficultyCard(DifficultyPreset.Normal, "Balanced resources, regional risk, and event pressure.");
        AddDifficultyCard(DifficultyPreset.Hard, "Lower resources, unstable politics, sharper sanity pressure.");
        AddDifficultyCard(DifficultyPreset.Custom, "Manual tuning for resources, seed, map size, and modifiers.");
    }

    void AddDifficultyCard(DifficultyPreset preset, string description)
    {
        DifficultyConfig config = preset == DifficultyPreset.Custom ? selectedDifficulty.Clone() : DifficultyConfig.CreatePreset(preset);
        VisualElement card = new VisualElement();
        card.AddToClassList("difficulty-card");
        card.style.marginBottom = 12;
        card.RegisterCallback<ClickEvent>(_ => SelectDifficulty(preset));

        Label title = UIToolkitThemeUtility.Label(config.displayName.ToUpperInvariant(), "section-title");
        Label body = UIToolkitThemeUtility.Label(description, "body-copy");
        Label meta = UIToolkitThemeUtility.Label(BuildModifierSummary(config), "meta-label");
        card.Add(title);
        card.Add(body);
        card.Add(meta);

        difficultyCards[preset] = card;
        difficultyList.Add(card);
    }

    void SelectDifficulty(DifficultyPreset preset)
    {
        selectedDifficulty = preset == DifficultyPreset.Custom
            ? selectedDifficulty.Clone()
            : DifficultyConfig.CreatePreset(preset);
        selectedDifficulty.preset = preset;
        selectedDifficulty.displayName = preset.ToString();
        selectedDifficulty.Normalize();

        foreach (KeyValuePair<DifficultyPreset, VisualElement> pair in difficultyCards)
        {
            if (pair.Key == preset)
                pair.Value.AddToClassList("difficulty-card-selected");
            else
                pair.Value.RemoveFromClassList("difficulty-card-selected");
        }

        RefreshDetails();
    }

    void RefreshDetails()
    {
        if (detailsPanel == null)
            return;

        detailsPanel.Clear();
        customFields.Clear();
        detailsPanel.Add(UIToolkitThemeUtility.Label(selectedDifficulty.displayName.ToUpperInvariant(), "header"));
        detailsPanel.Add(UIToolkitThemeUtility.Label(BuildDescription(selectedDifficulty.preset), "body-copy"));
        detailsPanel.Add(UIToolkitThemeUtility.Label(BuildModifierSummary(selectedDifficulty), "meta-label"));

        detailsPanel.Add(CreateField("startingMoney", "Starting Money", selectedDifficulty.startingMoney.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("startingArtifacts", "Starting Artifacts", selectedDifficulty.startingArtifacts.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("startingSanity", "Starting Sanity", selectedDifficulty.startingSanity.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("mapWidth", "Map Width", selectedDifficulty.mapWidth.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("mapHeight", "Map Height", selectedDifficulty.mapHeight.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("randomSeed", "Random Seed", selectedDifficulty.randomSeed.ToString(), true));
        detailsPanel.Add(CreateField("eventFrequencyMultiplier", "Event Frequency", selectedDifficulty.eventFrequencyMultiplier.ToString("0.##"), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("decisionCostMultiplier", "Decision Cost", selectedDifficulty.decisionCostMultiplier.ToString("0.##"), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("startingStabilityModifier", "Stability Modifier", selectedDifficulty.startingStabilityModifier.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("startingDevelopmentModifier", "Development Modifier", selectedDifficulty.startingDevelopmentModifier.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));
        detailsPanel.Add(CreateField("startingInfluenceModifier", "Influence Modifier", selectedDifficulty.startingInfluenceModifier.ToString(), selectedDifficulty.preset == DifficultyPreset.Custom));

        // --- Civ-like world map controls (drive the new Institute.World generator) ---
        detailsPanel.Add(UIToolkitThemeUtility.Label("WORLD MAP", "section-title"));
        string sizeId = DefaultWorldSize(selectedDifficulty.preset);
        Institute.World.MapPresetEntry entry = Institute.World.MapPresets.Get(sizeId);
        detailsPanel.Add(CreateField("worldSize", "Map Size (Small/Medium/Large)", sizeId, true));
        detailsPanel.Add(CreateField("regionCount", "Region Count Target", entry.targetRegionCount.ToString(), true));
        detailsPanel.Add(CreateField("unclaimedPercent", "Unclaimed Land %", Mathf.RoundToInt(entry.unclaimedLandFraction * 100f).ToString(), true));
        detailsPanel.Add(CreateField("terrainRoughness", "Terrain Roughness (0-1)", entry.terrainRoughness.ToString("0.##"), true));
    }

    static string DefaultWorldSize(DifficultyPreset preset)
    {
        switch (preset)
        {
            case DifficultyPreset.Easy: return "Small";
            case DifficultyPreset.Hard: return "Large";
            default: return "Medium";
        }
    }

    VisualElement CreateField(string key, string label, string value, bool enabled)
    {
        TextField field = new TextField(label) { value = value };
        field.AddToClassList("input-field");
        field.SetEnabled(enabled);
        customFields[key] = field;
        return field;
    }

    void RandomizeSeed()
    {
        int seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        selectedDifficulty.randomSeed = seed;
        selectedDifficulty.useRandomSeed = false;
        if (customFields.TryGetValue("randomSeed", out TextField field))
            field.value = seed.ToString();
    }

    void StartGame()
    {
        DifficultyConfig config = selectedDifficulty.Clone();
        ApplyFields(config);
        config.Normalize();
        ComposeWorldSetup(config);
        SceneFlowManager.EnsureExists().StartNewGame(NewGameSettings.FromDifficulty(config));
    }

    // Translates the setup choices into the new world generator's settings. Map size is
    // decoupled from DifficultyConfig (which clamps to a tiny legacy range) and uses the
    // data-driven map presets instead.
    void ComposeWorldSetup(DifficultyConfig config)
    {
        string sizeId = ReadString("worldSize", "Medium");
        Institute.World.MapPresetEntry entry = Institute.World.MapPresets.Get(sizeId);
        Institute.World.MapGenerationSettings settings = entry.ToSettings(config.randomSeed, config.preset.ToString());
        settings.targetRegionCount = Mathf.Clamp(ReadInt("regionCount", settings.targetRegionCount), 1, settings.width * settings.height / 4);
        settings.unclaimedLandFraction = Mathf.Clamp01(ReadInt("unclaimedPercent", Mathf.RoundToInt(settings.unclaimedLandFraction * 100f)) / 100f);
        settings.terrainRoughness = Mathf.Clamp01(ReadFloat("terrainRoughness", settings.terrainRoughness));
        Institute.World.WorldSetup.PendingSettings = settings;
    }

    string ReadString(string key, string fallback)
    {
        return customFields.TryGetValue(key, out TextField field) && !string.IsNullOrWhiteSpace(field.value) ? field.value.Trim() : fallback;
    }

    void ApplyFields(DifficultyConfig config)
    {
        config.useRandomSeed = false;
        config.startingMoney = ReadInt("startingMoney", config.startingMoney);
        config.startingArtifacts = ReadInt("startingArtifacts", config.startingArtifacts);
        config.startingSanity = ReadInt("startingSanity", config.startingSanity);
        config.mapWidth = ReadInt("mapWidth", config.mapWidth);
        config.mapHeight = ReadInt("mapHeight", config.mapHeight);
        config.randomSeed = ReadInt("randomSeed", config.randomSeed);
        config.eventFrequencyMultiplier = ReadFloat("eventFrequencyMultiplier", config.eventFrequencyMultiplier);
        config.decisionCostMultiplier = ReadFloat("decisionCostMultiplier", config.decisionCostMultiplier);
        config.startingStabilityModifier = ReadInt("startingStabilityModifier", config.startingStabilityModifier);
        config.startingDevelopmentModifier = ReadInt("startingDevelopmentModifier", config.startingDevelopmentModifier);
        config.startingInfluenceModifier = ReadInt("startingInfluenceModifier", config.startingInfluenceModifier);
        config.displayName = config.preset == DifficultyPreset.Custom ? "Custom" : config.preset.ToString();
    }

    int ReadInt(string key, int fallback)
    {
        return customFields.TryGetValue(key, out TextField field) && int.TryParse(field.value, out int value) ? value : fallback;
    }

    float ReadFloat(string key, float fallback)
    {
        return customFields.TryGetValue(key, out TextField field) && float.TryParse(field.value, out float value) ? value : fallback;
    }

    string BuildDescription(DifficultyPreset preset)
    {
        switch (preset)
        {
            case DifficultyPreset.Easy: return "Higher resources and more stable regions. Intended for learning the strategic loops.";
            case DifficultyPreset.Hard: return "Lower resources, harsher events, more expensive directives, and fragile politics.";
            case DifficultyPreset.Custom: return "Tune core starting parameters directly before generating the world.";
            default: return "Balanced pressure across resources, events, and regional instability.";
        }
    }

    string BuildModifierSummary(DifficultyConfig config)
    {
        return $"Money {config.startingMoney} | Artifacts {config.startingArtifacts} | Sanity {config.startingSanity} | Map {config.mapWidth}x{config.mapHeight} | Event x{config.eventFrequencyMultiplier:0.##} | Decision x{config.decisionCostMultiplier:0.##} | Stability {config.startingStabilityModifier:+#;-#;0}";
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
