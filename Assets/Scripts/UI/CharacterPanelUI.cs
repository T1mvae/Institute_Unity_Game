using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterPanelUI : MonoBehaviour
{
    RectTransform panel;
    RectTransform regionListContent;
    RectTransform globalListContent;
    RectTransform interactionContent;
    Text detailText;
    Text resultText;
    InputField filterInput;

    string lastRegionId;
    int lastVersion = -1;
    bool sortGlobalByInfluence = true;

    void Start()
    {
        BuildPanel();
        Subscribe();
        RefreshAll();
    }

    void OnDestroy()
    {
        if (CharacterManager.Instance != null)
        {
            CharacterManager.Instance.OnCharactersChanged -= RefreshAll;
            CharacterManager.Instance.OnCharacterSelected -= RefreshDetails;
        }
    }

    void Update()
    {
        CharacterManager manager = CharacterManager.Instance;
        LevelController controller = LevelController.Instance;
        string selectedRegionId = controller != null && controller.SelectedRegion != null ? controller.SelectedRegion.Id : string.Empty;
        int version = manager != null ? manager.ChangeVersion : -1;

        if (selectedRegionId != lastRegionId || version != lastVersion)
            RefreshAll();
    }

    void Subscribe()
    {
        if (CharacterManager.Instance == null)
            return;

        CharacterManager.Instance.OnCharactersChanged -= RefreshAll;
        CharacterManager.Instance.OnCharactersChanged += RefreshAll;
        CharacterManager.Instance.OnCharacterSelected -= RefreshDetails;
        CharacterManager.Instance.OnCharacterSelected += RefreshDetails;
    }

    void BuildPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        bool useExistingPanel = transform is RectTransform && transform != canvas.transform;
        GameObject panelObject;
        if (useExistingPanel)
        {
            panelObject = gameObject;
            panel = transform as RectTransform;
        }
        else
        {
            panelObject = new GameObject("Character Panel", typeof(RectTransform));
            panelObject.transform.SetParent(canvas.transform, false);
            panel = panelObject.transform as RectTransform;
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.offsetMin = new Vector2(18f, 244f);
            panel.offsetMax = new Vector2(330f, -94f);
        }

        Image background = panelObject.GetComponent<Image>();
        if (background == null)
            background = panelObject.AddComponent<Image>();
        background.color = UITheme.PanelBackground;
        Outline outline = panelObject.GetComponent<Outline>();
        if (outline == null)
            outline = panelObject.AddComponent<Outline>();
        outline.effectColor = UITheme.PanelBorder;
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddLabel(panel, "CHARACTERS", 18, UITheme.AccentPrimary, FontStyle.Bold, 28);
        AddLabel(panel, "Selected Region", 12, UITheme.TextSecondary, FontStyle.Bold, 18);
        CreateScroll("Region Character List", panel, 118f, out regionListContent);

        AddLabel(panel, "Selected Character", 12, UITheme.TextSecondary, FontStyle.Bold, 18);
        detailText = AddLabel(panel, "No character selected.", 11, UITheme.TextPrimary, FontStyle.Normal, 128);

        AddLabel(panel, "Interactions", 12, UITheme.TextSecondary, FontStyle.Bold, 18);
        interactionContent = CreateColumn("Interactions", panel, 124f);

        resultText = AddLabel(panel, "", 10, UITheme.AccentSecondary, FontStyle.Italic, 36);

        AddLabel(panel, "Global Character Search", 12, UITheme.TextSecondary, FontStyle.Bold, 18);
        filterInput = CreateInput(panel, "role, faction, status, trait...");
        filterInput.onValueChanged.AddListener(_ => RefreshGlobalList());
        Button sortButton = CreateButton(panel, "Toggle Sort", () =>
        {
            sortGlobalByInfluence = !sortGlobalByInfluence;
            RefreshGlobalList();
        }, 26f);
        sortButton.GetComponentInChildren<Text>().text = "SORT: POWER / RELATION";

        CreateScroll("Global Character List", panel, 148f, out globalListContent);
    }

    void RefreshAll()
    {
        if (panel == null)
            return;

        CharacterManager manager = CharacterManager.Instance;
        LevelController controller = LevelController.Instance;
        lastRegionId = controller != null && controller.SelectedRegion != null ? controller.SelectedRegion.Id : string.Empty;
        lastVersion = manager != null ? manager.ChangeVersion : -1;

        RefreshRegionList();
        RefreshGlobalList();
        RefreshDetails(manager != null ? manager.SelectedCharacter : null);
    }

    void RefreshRegionList()
    {
        Clear(regionListContent);
        CharacterManager manager = CharacterManager.Instance;
        LevelController controller = LevelController.Instance;

        if (manager == null || controller == null || controller.SelectedRegion == null)
        {
            AddListLabel(regionListContent, "Select a region.");
            return;
        }

        List<GameCharacter> regionCharacters = manager.GetCharactersInRegion(controller.SelectedRegion.Id);
        if (regionCharacters.Count == 0)
        {
            AddListLabel(regionListContent, "No known local figures.");
            return;
        }

        for (int i = 0; i < regionCharacters.Count; i++)
            AddCharacterButton(regionListContent, regionCharacters[i]);
    }

    void RefreshGlobalList()
    {
        Clear(globalListContent);
        CharacterManager manager = CharacterManager.Instance;
        if (manager == null)
        {
            AddListLabel(globalListContent, "Character manager unavailable.");
            return;
        }

        string filter = filterInput != null ? filterInput.text : string.Empty;
        List<GameCharacter> results = manager.SearchCharacters(filter, sortGlobalByInfluence);
        for (int i = 0; i < Mathf.Min(results.Count, 40); i++)
            AddCharacterButton(globalListContent, results[i]);
    }

    void RefreshDetails(GameCharacter character)
    {
        Clear(interactionContent);

        if (detailText == null)
            return;

        if (character == null)
        {
            detailText.text = "No character selected.";
            return;
        }

        detailText.text =
            $"{character.displayName}\n" +
            $"{character.title} — {character.faction}\n" +
            $"Status: {character.status} | Relation: {character.relationshipWithPlayer}\n" +
            $"Loyalty {character.loyalty}  Trust {character.trust}  Fear {character.fear}\n" +
            $"Ambition {character.ambition}  Power {character.influencePower}\n" +
            $"Corruption {(character.revealedTraits.Count > 0 ? character.corruption.ToString() : "???")}\n" +
            $"{character.BuildShortDescription()}\n" +
            $"Passive: {character.BuildPassiveEffectSummary()}";

        CharacterManager manager = CharacterManager.Instance;
        if (manager == null)
            return;

        foreach (CharacterInteractionDefinition definition in manager.GetInteractionDefinitions())
        {
            CharacterInteractionType interactionType = definition.interactionType;
            Button button = CreateButton(interactionContent, BuildInteractionLabel(definition), () =>
            {
                CharacterInteractionResult result = CharacterManager.Instance.TryApplyInteraction(CharacterManager.Instance.SelectedCharacter, interactionType);
                if (resultText != null)
                    resultText.text = result.message;
                RefreshAll();
            }, 26f);
            TooltipTrigger tooltip = button.gameObject.AddComponent<TooltipTrigger>();
            tooltip.message = BuildInteractionTooltip(definition);
        }
    }

    string BuildInteractionLabel(CharacterInteractionDefinition definition)
    {
        List<string> costs = new List<string>();
        if (definition.moneyCost > 0) costs.Add("$" + definition.moneyCost);
        if (definition.sanityCost > 0) costs.Add("Sanity " + definition.sanityCost);
        if (definition.artifactCost > 0) costs.Add("Artifact " + definition.artifactCost);
        string suffix = costs.Count > 0 ? " (" + string.Join(", ", costs) + ")" : string.Empty;
        return definition.displayName + suffix;
    }

    string BuildInteractionTooltip(CharacterInteractionDefinition definition)
    {
        List<string> lines = new List<string> { definition.displayName };
        if (definition.minTrust > 0) lines.Add("Requires Trust " + definition.minTrust);
        if (definition.minFear > 0) lines.Add(definition.interactionType == CharacterInteractionType.RecruitAsContact ? "or Fear " + definition.minFear : "Requires Fear " + definition.minFear);
        if (definition.minRelationship > -100) lines.Add("Requires Relation " + definition.minRelationship);
        lines.Add("Cooldown: " + definition.cooldownSeconds.ToString("0") + "s");
        return string.Join("\n", lines);
    }

    void AddCharacterButton(RectTransform parent, GameCharacter character)
    {
        Button button = CreateButton(parent, $"{character.displayName} — {character.title}\n{character.faction} | Pwr {character.influencePower} | Rel {character.relationshipWithPlayer}", () =>
        {
            CharacterManager.Instance.SelectCharacter(character);
            RefreshDetails(character);
        }, 44f);

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = Color.Lerp(UITheme.ButtonNormal, character.portraitColor, 0.25f);
    }

    Text AddListLabel(RectTransform parent, string content)
    {
        return AddLabel(parent, content, 11, UITheme.TextSecondary, FontStyle.Italic, 24);
    }

    Text AddLabel(RectTransform parent, string content, int size, Color color, FontStyle style, float preferredHeight)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;

        Text text = labelObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    Button CreateButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action, float height)
    {
        GameObject buttonObject = new GameObject("Character Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;

        Image image = buttonObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        buttonObject.AddComponent<StyledButton>();

        Text text = AddLabel(buttonObject.transform as RectTransform, label, 10, UITheme.TextPrimary, FontStyle.Normal, height);
        RectTransform rect = text.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 2f);
        rect.offsetMax = new Vector2(-6f, -2f);
        text.raycastTarget = false;
        return button;
    }

    InputField CreateInput(RectTransform parent, string placeholderText)
    {
        GameObject inputObject = new GameObject("Character Filter Input", typeof(RectTransform));
        inputObject.transform.SetParent(parent, false);
        LayoutElement layout = inputObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 28f;
        Image image = inputObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;

        InputField input = inputObject.AddComponent<InputField>();
        Text text = AddLabel(inputObject.transform as RectTransform, "", 11, UITheme.TextPrimary, FontStyle.Normal, 28);
        Text placeholder = AddLabel(inputObject.transform as RectTransform, placeholderText, 11, UITheme.TextSecondary, FontStyle.Italic, 28);
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    void CreateScroll(string name, RectTransform parent, float height, out RectTransform content)
    {
        GameObject viewport = new GameObject(name, typeof(RectTransform));
        viewport.transform.SetParent(parent, false);
        LayoutElement viewportLayout = viewport.AddComponent<LayoutElement>();
        viewportLayout.preferredHeight = height;
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.16f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        content = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
        content.SetParent(viewport.transform, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 4;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewport.transform as RectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;
    }

    RectTransform CreateColumn(string name, RectTransform parent, float height)
    {
        GameObject columnObject = new GameObject(name, typeof(RectTransform));
        columnObject.transform.SetParent(parent, false);
        LayoutElement layoutElement = columnObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        RectTransform column = columnObject.transform as RectTransform;
        VerticalLayoutGroup layout = columnObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return column;
    }

    void Clear(RectTransform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
