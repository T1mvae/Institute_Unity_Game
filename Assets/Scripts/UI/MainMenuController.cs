using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public Button startButton;
    public Button exitButton;
    public string gameSceneName = "GameScreen";

    RectTransform setupPanel;
    RectTransform customPanel;
    DifficultyConfig selectedDifficulty;
    readonly Dictionary<string, InputField> customFields = new Dictionary<string, InputField>();

    void Start()
    {
        selectedDifficulty = DifficultyConfig.CreatePreset(DifficultyPreset.Normal);

        if (startButton != null)
            startButton.onClick.AddListener(OpenSetupPanel);
        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);

        BuildSetupPanel();
    }

    void OpenSetupPanel()
    {
        if (setupPanel != null)
            setupPanel.gameObject.SetActive(true);
    }

    void BuildSetupPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        GameObject panelObject = new GameObject("PreGame Difficulty Setup", typeof(RectTransform));
        panelObject.transform.SetParent(canvas.transform, false);
        setupPanel = panelObject.transform as RectTransform;
        setupPanel.anchorMin = new Vector2(0.5f, 0.5f);
        setupPanel.anchorMax = new Vector2(0.5f, 0.5f);
        setupPanel.pivot = new Vector2(0.5f, 0.5f);
        setupPanel.sizeDelta = new Vector2(760f, 700f);
        setupPanel.anchoredPosition = Vector2.zero;

        Image background = panelObject.AddComponent<Image>();
        background.color = UITheme.PanelBackground;

        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 20, 20);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddText(setupPanel, "NEW INSTITUTE OPERATION", 24, UITheme.AccentPrimary, FontStyle.Bold, 38);
        AddText(setupPanel, "Choose a difficulty profile and seed before generating the planet.", 14, UITheme.TextSecondary, FontStyle.Normal, 34);

        RectTransform difficultyRow = CreateRow("Difficulty Row", setupPanel, 52);
        CreateButton(difficultyRow, "Easy", () => SelectPreset(DifficultyPreset.Easy));
        CreateButton(difficultyRow, "Normal", () => SelectPreset(DifficultyPreset.Normal));
        CreateButton(difficultyRow, "Hard", () => SelectPreset(DifficultyPreset.Hard));
        CreateButton(difficultyRow, "Custom", () => SelectPreset(DifficultyPreset.Custom));

        customPanel = CreateColumn("Custom Settings", setupPanel, 360);
        CreateInput("startingMoney", "Starting money", selectedDifficulty.startingMoney.ToString());
        CreateInput("startingArtifacts", "Starting artifacts", selectedDifficulty.startingArtifacts.ToString());
        CreateInput("startingSanity", "Starting sanity", selectedDifficulty.startingSanity.ToString());
        CreateInput("mapWidth", "Map width", selectedDifficulty.mapWidth.ToString());
        CreateInput("mapHeight", "Map height", selectedDifficulty.mapHeight.ToString());
        CreateInput("randomSeed", "Random seed", selectedDifficulty.randomSeed.ToString());
        CreateInput("eventFrequencyMultiplier", "Event frequency", selectedDifficulty.eventFrequencyMultiplier.ToString("0.##"));
        CreateInput("decisionCostMultiplier", "Decision cost", selectedDifficulty.decisionCostMultiplier.ToString("0.##"));
        CreateInput("startingStabilityModifier", "Stability modifier", selectedDifficulty.startingStabilityModifier.ToString());
        CreateInput("startingDevelopmentModifier", "Development modifier", selectedDifficulty.startingDevelopmentModifier.ToString());
        CreateInput("startingInfluenceModifier", "Influence modifier", selectedDifficulty.startingInfluenceModifier.ToString());

        RectTransform actionRow = CreateRow("Action Row", setupPanel, 52);
        CreateButton(actionRow, "Start New Game", StartSelectedGame);
        CreateButton(actionRow, "Randomize Seed", RandomizeSeed);
        CreateButton(actionRow, "Close", () => setupPanel.gameObject.SetActive(false));

        RectTransform loadRow = CreateRow("Load Row", setupPanel, 52);
        Button continueButton = CreateButton(loadRow, "Continue", () => LoadGame(SaveLoadManager.AutoSaveSlot));
        Button loadButton = CreateButton(loadRow, "Load Manual Save", () => LoadGame(SaveLoadManager.ManualSaveSlot));
        continueButton.interactable = SaveLoadManager.SaveExists(SaveLoadManager.AutoSaveSlot);
        loadButton.interactable = SaveLoadManager.SaveExists(SaveLoadManager.ManualSaveSlot);

        SelectPreset(DifficultyPreset.Normal);
        setupPanel.gameObject.SetActive(false);
    }

    void SelectPreset(DifficultyPreset preset)
    {
        selectedDifficulty = preset == DifficultyPreset.Custom
            ? selectedDifficulty.Clone()
            : DifficultyConfig.CreatePreset(preset);

        selectedDifficulty.preset = preset;
        selectedDifficulty.displayName = preset.ToString();
        selectedDifficulty.Normalize();
        RefreshCustomFields();

        if (customPanel != null)
            customPanel.gameObject.SetActive(preset == DifficultyPreset.Custom);
    }

    void StartSelectedGame()
    {
        DifficultyConfig config = selectedDifficulty.Clone();
        if (config.preset == DifficultyPreset.Custom)
            ApplyCustomFields(config);

        GameSession.StartNewGame(config);
        SceneManager.LoadScene(gameSceneName);
    }

    void LoadGame(string slotName)
    {
        GameSession.RequestLoadGame(slotName);
        SceneManager.LoadScene(gameSceneName);
    }

    void RandomizeSeed()
    {
        int seed = Random.Range(int.MinValue / 2, int.MaxValue / 2);
        if (customFields.TryGetValue("randomSeed", out InputField input))
            input.text = seed.ToString();
        selectedDifficulty.randomSeed = seed;
        selectedDifficulty.useRandomSeed = false;
    }

    void ApplyCustomFields(DifficultyConfig config)
    {
        config.preset = DifficultyPreset.Custom;
        config.displayName = "Custom";
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
        config.Normalize();
    }

    void RefreshCustomFields()
    {
        SetField("startingMoney", selectedDifficulty.startingMoney);
        SetField("startingArtifacts", selectedDifficulty.startingArtifacts);
        SetField("startingSanity", selectedDifficulty.startingSanity);
        SetField("mapWidth", selectedDifficulty.mapWidth);
        SetField("mapHeight", selectedDifficulty.mapHeight);
        SetField("randomSeed", selectedDifficulty.randomSeed);
        SetField("eventFrequencyMultiplier", selectedDifficulty.eventFrequencyMultiplier);
        SetField("decisionCostMultiplier", selectedDifficulty.decisionCostMultiplier);
        SetField("startingStabilityModifier", selectedDifficulty.startingStabilityModifier);
        SetField("startingDevelopmentModifier", selectedDifficulty.startingDevelopmentModifier);
        SetField("startingInfluenceModifier", selectedDifficulty.startingInfluenceModifier);
    }

    int ReadInt(string key, int fallback)
    {
        return customFields.TryGetValue(key, out InputField input) && int.TryParse(input.text, out int value) ? value : fallback;
    }

    float ReadFloat(string key, float fallback)
    {
        return customFields.TryGetValue(key, out InputField input) && float.TryParse(input.text, out float value) ? value : fallback;
    }

    void SetField(string key, int value)
    {
        if (customFields.TryGetValue(key, out InputField input))
            input.text = value.ToString();
    }

    void SetField(string key, float value)
    {
        if (customFields.TryGetValue(key, out InputField input))
            input.text = value.ToString("0.##");
    }

    void CreateInput(string key, string label, string value)
    {
        RectTransform row = CreateRow(label + " Row", customPanel, 28);
        Text labelText = AddText(row, label, 12, UITheme.TextSecondary, FontStyle.Normal, 26);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 220f;

        GameObject inputObject = new GameObject(key + " Input", typeof(RectTransform));
        inputObject.transform.SetParent(row, false);
        Image image = inputObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        InputField input = inputObject.AddComponent<InputField>();

        Text text = AddText(inputObject.transform as RectTransform, value, 12, UITheme.TextPrimary, FontStyle.Normal, 24);
        Text placeholder = AddText(inputObject.transform as RectTransform, value, 12, UITheme.TextSecondary, FontStyle.Italic, 24);
        input.textComponent = text;
        input.placeholder = placeholder;
        input.text = value;

        LayoutElement inputLayout = inputObject.AddComponent<LayoutElement>();
        inputLayout.flexibleWidth = 1f;
        customFields[key] = input;
    }

    RectTransform CreateRow(string name, RectTransform parent, float height)
    {
        GameObject rowObject = new GameObject(name, typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);
        RectTransform row = rowObject.transform as RectTransform;
        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return row;
    }

    RectTransform CreateColumn(string name, RectTransform parent, float height)
    {
        GameObject columnObject = new GameObject(name, typeof(RectTransform));
        columnObject.transform.SetParent(parent, false);
        RectTransform column = columnObject.transform as RectTransform;
        LayoutElement layoutElement = columnObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        VerticalLayoutGroup layout = columnObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return column;
    }

    Button CreateButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        buttonObject.AddComponent<StyledButton>();
        AddText(buttonObject.transform as RectTransform, label, 13, UITheme.TextPrimary, FontStyle.Bold, 30).raycastTarget = false;
        return button;
    }

    Text AddText(RectTransform parent, string content, int size, Color color, FontStyle style, float preferredHeight)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;
        return text;
    }

    void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
