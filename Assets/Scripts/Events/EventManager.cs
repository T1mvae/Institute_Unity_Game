using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Random = UnityEngine.Random;

public class EventManager : MonoBehaviour
{
    public List<GameEvent> Events = new List<GameEvent>();  
    public GameObject eventPanelPrefab;     // Префаб панели ивента
    public Transform canvasTransform;       // Куда инстанцировать (обычно Canvas)
    private GameObject currentEventPanel;   // Ссылка на текущую панель (для удаления)
    private Region currentEventRegion;

    [Header("Event Scheduling")]
    public float personalIntervalSeconds = 30f;
    public float localIntervalSeconds = 60f;
    [Range(0f, 1f)]
    public float personalEventChance = 1f;
    [Range(0f, 1f)]
    public float localEventChance = 0.5f;
    public float globalIntervalSeconds = 120f;
    [Range(0f, 1f)]
    public float globalEventChance = 0.25f;
    private float nextPersonalTime;
    private float nextLocalTime;
    private float nextGlobalTime;

    [Header("Bad Event Settings")]
    public float neutralAutoSuccessChance = 0.5f;
    public float suspicionChance = 0.25f;
    public int suspicionInfluencePenalty = 2;
    public int suspicionStabilityPenalty = 1;

    [Header("Featured People UI")]
    public Transform featuredPeopleContainer;
    [Tooltip("Prefab with a Text component used for each featured person line.")]
    public GameObject featuredPersonEntryPrefab;

    public static bool isEventActive = false;
    int activeEventId = -1;
    string activeEventRegionId;
    string activeEventCharacterId;
    GameCharacter currentEventCharacter;
    void Awake()
    {
        // Ensure static state is reset even if domain reload is disabled.
        isEventActive = false;
        currentEventPanel = null;
    }


    void Start()
    {
        Events = LoadEventsFromFile();
        ApplyDifficultyPacing();

        float now = GetCurrentTime();
        nextPersonalTime = now + personalIntervalSeconds;
        nextLocalTime = now + localIntervalSeconds;
        nextGlobalTime = now + globalIntervalSeconds;

        if (Events == null || Events.Count == 0)
            Debug.LogWarning("EventManager: no events loaded, check StreamingAssets/events.json");
    }

    void ApplyDifficultyPacing()
    {
        DifficultyConfig difficulty = GameSession.ActiveDifficulty;
        float frequency = Mathf.Max(0.1f, difficulty.eventFrequencyMultiplier);
        float danger = Mathf.Max(0.1f, difficulty.eventDangerMultiplier);

        personalIntervalSeconds = Mathf.Max(5f, personalIntervalSeconds / frequency);
        localIntervalSeconds = Mathf.Max(5f, localIntervalSeconds / frequency);
        globalIntervalSeconds = Mathf.Max(10f, globalIntervalSeconds / frequency);
        personalEventChance = Mathf.Clamp01(personalEventChance * frequency);
        localEventChance = Mathf.Clamp01(localEventChance * frequency);
        globalEventChance = Mathf.Clamp01(globalEventChance * frequency);
        suspicionChance = Mathf.Clamp01(suspicionChance * danger);
        suspicionInfluencePenalty = Mathf.Max(1, Mathf.RoundToInt(suspicionInfluencePenalty * danger));
        suspicionStabilityPenalty = Mathf.Max(1, Mathf.RoundToInt(suspicionStabilityPenalty * danger));
    }

    List<GameEvent> LoadEventsFromFile()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "events.json");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"EventManager: events file not found at {path}");
            return new List<GameEvent>();
        }

        try
        {
            string json = File.ReadAllText(path);
            RawEventsWrapper wrapper = JsonUtility.FromJson<RawEventsWrapper>("{\"events\":" + json + "}");
            List<GameEvent> loaded = ConvertRawEvents(wrapper?.events);
            LogLoadedScopes(loaded);
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.LogError("EventManager: failed to load events - " + ex.Message);
            return new List<GameEvent>();
        }
    }

    List<GameEvent> ConvertRawEvents(List<RawGameEvent> rawEvents)
    {
        List<GameEvent> result = new List<GameEvent>();
        if (rawEvents == null)
            return result;

        for (int i = 0; i < rawEvents.Count; i++)
        {
            RawGameEvent raw = rawEvents[i];
            if (raw == null)
                continue;

            GameEvent evt = new GameEvent
            {
                id = raw.id,
                description = raw.description,
                isBadEvent = raw.isBadEvent,
                options = raw.options ?? new List<EventOption>(),
                featuredPeople = raw.featuredPeople ?? new List<FeaturedPerson>(),
                scope = ParseScope(raw.scope),
                targetCharacterRole = raw.targetCharacterRole,
                minCharacterTrust = raw.minCharacterTrust,
                maxCharacterTrust = raw.maxCharacterTrust,
                minCharacterCorruption = raw.minCharacterCorruption,
                requiresRecruitedContact = raw.requiresRecruitedContact
            };

            result.Add(evt);
        }

        return result;
    }

    EventScope ParseScope(string rawScope)
    {
        if (string.IsNullOrEmpty(rawScope))
            return EventScope.Local;

        if (Enum.TryParse(rawScope, true, out EventScope parsed))
            return parsed;

        Debug.LogWarning("EventManager: unknown scope \"" + rawScope + "\"; defaulting to Local.");
        return EventScope.Local;
    }

    void LogLoadedScopes(List<GameEvent> events)
    {
        if (events == null)
            return;

        int local = 0, global = 0, personal = 0;
        for (int i = 0; i < events.Count; i++)
        {
            switch (events[i].scope)
            {
                case EventScope.Global:
                    global++;
                    break;
                case EventScope.Personal:
                    personal++;
                    break;
                default:
                    local++;
                    break;
            }
        }

        Debug.Log($"EventManager: loaded {events.Count} events (Local {local}, Global {global}, Personal {personal})");
    }

    void Update()
    {
        if (isEventActive && currentEventPanel == null)
            ApplyAcknowledgement();

        if (EventManager.isEventActive) return;

        float currentTime = GetCurrentTime();

        if (CheckAndTriggerScheduledEvent(EventScope.Personal, ref nextPersonalTime, personalIntervalSeconds, personalEventChance, currentTime))
            return;

        if (CheckAndTriggerScheduledEvent(EventScope.Local, ref nextLocalTime, localIntervalSeconds, localEventChance, currentTime))
            return;

        CheckAndTriggerScheduledEvent(EventScope.Global, ref nextGlobalTime, globalIntervalSeconds, globalEventChance, currentTime);
    }

    bool CheckAndTriggerScheduledEvent(EventScope scope, ref float nextTime, float interval, float chance, float currentTime)
    {
        if (currentTime < nextTime)
            return false;

        nextTime = currentTime + interval;

        if (chance < 1f && Random.value > chance)
            return false;

        return TryTriggerEvent(scope);
    }

    bool TryTriggerEvent(EventScope scope)
    {
        if (Events == null || Events.Count == 0)
            return false;

        List<GameEvent> matchingEvents = new List<GameEvent>();
        for (int i = 0; i < Events.Count; i++)
        {
            GameEvent evt = Events[i];
            if (evt != null && evt.scope == scope && CanResolveEventTarget(evt))
                matchingEvents.Add(evt);
        }

        if (matchingEvents.Count == 0)
            return false;

        GameEvent gameEvent = matchingEvents[Random.Range(0, matchingEvents.Count)];
        Region targetRegion = null;

        if (scope == EventScope.Local)
        {
            LevelController levelController = LevelController.Instance;
            if (levelController == null || levelController.AllRegions.Count == 0)
                return false;

            targetRegion = levelController.AllRegions[Random.Range(0, levelController.AllRegions.Count)];
        }

        ShowEvent(gameEvent, targetRegion);
        Debug.Log($"Scheduled {scope} event triggered: {gameEvent.description}");
        return true;
    }

    public void ShowEvent(GameEvent gameEvent, Region targetRegion)
    {
        ShowEvent(gameEvent, targetRegion, null);
    }

    public void ShowEvent(GameEvent gameEvent, Region targetRegion, GameCharacter targetCharacter)
    {
        isEventActive = true;
        if (GameManager.Instance != null)
            GameManager.Instance.SetEventActive(true);
        if (gameEvent != null)
            Debug.Log("ShowEvent called! " + gameEvent.description);

        GameCharacter resolvedCharacter = ResolveTargetCharacter(gameEvent, targetCharacter);
        Region resolvedRegion = ResolveTargetRegion(gameEvent, targetRegion, resolvedCharacter);
        currentEventRegion = resolvedRegion;
        currentEventCharacter = resolvedCharacter;
        activeEventId = gameEvent != null ? gameEvent.id : -1;
        activeEventRegionId = resolvedRegion != null ? resolvedRegion.Id : string.Empty;
        activeEventCharacterId = resolvedCharacter != null ? resolvedCharacter.id : string.Empty;
        LogEventOpened(gameEvent, resolvedRegion);

        if (currentEventPanel != null)
            Destroy(currentEventPanel);

        if (eventPanelPrefab == null)
        {
            Debug.LogError("EventManager: eventPanelPrefab is not assigned.");
            ApplyAcknowledgement();
            return;
        }

        currentEventPanel = Instantiate(eventPanelPrefab, canvasTransform);
        var panelUI = currentEventPanel.GetComponent<EventPanelUI>();

        if (panelUI == null)
        {
            Debug.LogError("EventManager: EventPanelUI component missing on event panel prefab.");
            ApplyAcknowledgement();
            Destroy(currentEventPanel);
            currentEventPanel = null;
            return;
        }

        if (panelUI.descriptionText != null)
            panelUI.descriptionText.text = ComposeEventDescription(gameEvent, resolvedRegion);

        PopulateFeaturedPeople(gameEvent);

        if (panelUI.optionsContainer != null)
        {
            foreach (Transform child in panelUI.optionsContainer)
                Destroy(child.gameObject);
        }

        if (gameEvent.isBadEvent)
        {
            HandleBadEvent(gameEvent, resolvedRegion, panelUI);
            return;
        }

        ShowBestStandardOption(gameEvent, resolvedRegion, panelUI);
    }

    void ShowBestStandardOption(GameEvent gameEvent, Region region, EventPanelUI panelUI)
    {
        EventOption chosen = null;

        for (int i = gameEvent.options.Count - 1; i >= 0; i--)
        {
            EventOption candidate = gameEvent.options[i];
            int currentSanity = LevelController.Instance != null ? LevelController.Instance.Sanity : 0;
            if (!ShouldShowOption(currentSanity, i))
                continue;

            if (CanExecuteOption(gameEvent, candidate, region))
            {
                chosen = candidate;
                break;
            }
        }

        if (chosen == null && gameEvent.options.Count > 0)
            chosen = gameEvent.options[0];

        if (chosen == null)
        {
            ApplyAcknowledgement();
            Destroy(currentEventPanel);
            currentEventPanel = null;
            return;
        }

        CreateSingleOptionButton(gameEvent, chosen, panelUI, region, false);
    }

    void HandleBadEvent(GameEvent gameEvent, Region region, EventPanelUI panelUI)
    {
        EventOption badOption = gameEvent.options.Count > 0 ? gameEvent.options[0] : null;
        EventOption neutralOption = gameEvent.options.Count > 1 ? gameEvent.options[1] : null;
        EventOption goodOption = gameEvent.options.Count > 2 ? gameEvent.options[2] : null;

        if (gameEvent.scope == EventScope.Local && region != null && neutralOption != null && region.Development > 10 && Random.value < neutralAutoSuccessChance)
        {
            Debug.Log("Neutral outcome automatically applied thanks to regional development.");
            ApplyOption(gameEvent, neutralOption);
            Destroy(currentEventPanel);
            currentEventPanel = null;
            return;
        }

        if (goodOption != null && CanExecuteOption(gameEvent, goodOption, region))
        {
            CreateSingleOptionButton(gameEvent, goodOption, panelUI, region, true);
            return;
        }

        if (neutralOption != null && CanExecuteOption(gameEvent, neutralOption, region))
        {
            CreateSingleOptionButton(gameEvent, neutralOption, panelUI, region, false);
            return;
        }

        if (badOption != null)
        {
            CreateSingleOptionButton(gameEvent, badOption, panelUI, region, false);
            return;
        }

        ApplyAcknowledgement();
        Destroy(currentEventPanel);
        currentEventPanel = null;
    }

    void CreateSingleOptionButton(GameEvent gameEvent, EventOption option, EventPanelUI panelUI, Region region, bool triggersSuspicion)
    {
        if (option == null || panelUI == null || panelUI.optionButtonPrefab == null)
            return;

        string optionLabel = BuildOptionLabel(option);

        GameObject btnObj = Instantiate(panelUI.optionButtonPrefab, panelUI.optionsContainer);
        Text buttonText = btnObj.GetComponentInChildren<Text>();
        if (buttonText != null)
            buttonText.text = optionLabel;

        TooltipTrigger tooltip = btnObj.GetComponent<TooltipTrigger>();
        if (tooltip != null)
            tooltip.message = BuildOptionTooltip(gameEvent, option, region);

        Button button = btnObj.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = CanExecuteOption(gameEvent, option, region);
            button.onClick.AddListener(() =>
            {
                if (!TrySpendResources(gameEvent, option, region))
                    return;

                Region affected = ApplyOption(gameEvent, option);
                if (triggersSuspicion && gameEvent.scope == EventScope.Local)
                    MaybeTriggerSuspicion(affected);
                Destroy(currentEventPanel);
                currentEventPanel = null;
            });
        }
    }

    bool ShouldShowOption(int sanity, int optionIndex)
    {
        if (sanity < 25)
            return optionIndex == 0;

        if (sanity >= 25 && sanity < 50)
            return optionIndex == 0 || optionIndex == 1;

        if (sanity >= 50 && sanity < 75)
            return optionIndex == 1 || optionIndex == 2;

        if (sanity >= 75)
            return optionIndex == 2;

        return false;
    }

    bool CanExecuteOption(GameEvent gameEvent, EventOption option, Region region)
    {
        if (option == null)
            return false;

        var controller = LevelController.Instance;
        if (controller == null)
            return false;

        if (gameEvent != null && gameEvent.scope == EventScope.Local)
        {
            Region targetRegion = region != null ? region : currentEventRegion;
            if (targetRegion == null)
                targetRegion = controller.SelectedRegion;

            if (option.minInfluenceRequired > 0 && (targetRegion == null || targetRegion.Influence < option.minInfluenceRequired))
                return false;

            if (option.minDevelopmentRequired > 0 && (targetRegion == null || targetRegion.Development < option.minDevelopmentRequired))
                return false;
        }

        if (RequiresCharacterTarget(gameEvent) && currentEventCharacter == null)
            return false;

        if (controller.Sanity < option.sanityRequired)
            return false;
        if (controller.Money < option.moneyRequired)
            return false;
        if (controller.Artifacts < option.artifactsRequired)
            return false;

        return true;
    }

    bool TrySpendResources(GameEvent gameEvent, EventOption option, Region region)
    {
        if (!CanExecuteOption(gameEvent, option, region))
        {
            Debug.Log("Requirements not met for option: " + BuildOptionLabel(option));
            return false;
        }

        var controller = LevelController.Instance;
        if (controller == null)
            return false;

        if (option.sanityRequired != 0)
            controller.ChangeSanity(-option.sanityRequired);
        if (option.moneyRequired != 0)
            controller.ChangeMoney(-option.moneyRequired);
        if (option.artifactsRequired != 0)
            controller.ChangeArtifacts(-option.artifactsRequired);

        return true;
    }

    string BuildOptionLabel(EventOption option)
    {
        if (option == null)
            return string.Empty;

        string baseText = option.text ?? string.Empty;
        string costSuffix = BuildCostSuffix(option);
        string consequenceSuffix = BuildConsequenceSuffix(option);

        if (!string.IsNullOrEmpty(costSuffix))
            baseText += costSuffix;
        if (!string.IsNullOrEmpty(consequenceSuffix))
            baseText += consequenceSuffix;

        return baseText;
    }

    string BuildCostSuffix(EventOption option)
    {
        List<string> costs = new List<string>();

        if (option.sanityRequired > 0)
            costs.Add(option.sanityRequired + " Sanity");
        if (option.moneyRequired > 0)
            costs.Add(option.moneyRequired + " Money");
        if (option.artifactsRequired > 0)
            costs.Add(option.artifactsRequired + " Artifacts");
        if (option.minInfluenceRequired > 0)
            costs.Add("Req Influence " + option.minInfluenceRequired);
        if (option.minDevelopmentRequired > 0)
            costs.Add("Req Development " + option.minDevelopmentRequired);

        if (costs.Count == 0)
            return string.Empty;

        return " (Cost: " + string.Join(", ", costs) + ")";
    }

    string BuildConsequenceSuffix(EventOption option)
    {
        string summary = BuildOptionChangeSummary(option);
        if (string.IsNullOrEmpty(summary))
            return string.Empty;

        return "\nConsequences: " + summary;
    }

    string BuildOptionTooltip(GameEvent gameEvent, EventOption option, Region region)
    {
        if (option == null)
            return string.Empty;

        List<string> lines = new List<string>();
        lines.Add(option.text ?? "Option");

        string costs = BuildCostSuffix(option);
        if (!string.IsNullOrEmpty(costs))
            lines.Add(costs.Trim(' ', '(', ')'));

        string consequences = BuildOptionChangeSummary(option);
        if (!string.IsNullOrEmpty(consequences))
            lines.Add("Consequences: " + consequences);

        if (gameEvent != null && gameEvent.scope == EventScope.Local)
        {
            string target = region != null ? region.Name : "selected region";
            lines.Add("Target: " + target);
        }
        else if (gameEvent != null)
        {
            lines.Add("Scope: " + gameEvent.scope);
        }

        if (gameEvent != null && gameEvent.scope == EventScope.Personal && currentEventCharacter != null)
            lines.Add("Character: " + currentEventCharacter.displayName);

        return string.Join("\n", lines);
    }

    void ApplyAcknowledgement()
    {
        isEventActive = false;
        if (GameManager.Instance != null)
            GameManager.Instance.SetEventActive(false);
        currentEventRegion = null;
        currentEventCharacter = null;
        activeEventId = -1;
        activeEventRegionId = string.Empty;
        activeEventCharacterId = string.Empty;
    }

    Region ApplyOption(GameEvent gameEvent, EventOption option)
    {
        isEventActive = false;
        if (GameManager.Instance != null)
            GameManager.Instance.SetEventActive(false);

        if (option == null)
        {
            currentEventRegion = null;
            currentEventCharacter = null;
            return null;
        }

        LevelController controller = LevelController.Instance;
        if (controller == null)
        {
            currentEventRegion = null;
            currentEventCharacter = null;
            return null;
        }

        Region affectedRegion = null;
        EventScope scope = gameEvent != null ? gameEvent.scope : EventScope.Local;

        switch (scope)
        {
            case EventScope.Local:
                affectedRegion = currentEventRegion != null ? currentEventRegion : controller.SelectedRegion;
                if (affectedRegion == null)
                {
                    Debug.LogWarning("ApplyOption called for local event but no region is assigned.");
                }
                else
                {
                    ApplyOptionToRegion(option, affectedRegion);
                }
                break;

            case EventScope.Global:
                ApplyOptionToAllRegions(option, controller.AllRegions);
                break;

            case EventScope.Personal:
                affectedRegion = currentEventRegion;
                if (affectedRegion != null)
                    ApplyOptionToRegion(option, affectedRegion);
                break;
        }

        ApplyResourceChanges(option, controller);
        ApplyCharacterChanges(option);
        RefreshUIAfterOption(scope, affectedRegion, controller);
        LogOptionChoice(gameEvent, option, scope, affectedRegion);

        currentEventRegion = null;
        currentEventCharacter = null;
        activeEventId = -1;
        activeEventRegionId = string.Empty;
        activeEventCharacterId = string.Empty;

        return affectedRegion;
    }

    public EventManagerSaveData CaptureSaveState()
    {
        return new EventManagerSaveData
        {
            eventActive = isEventActive && activeEventId >= 0,
            activeEventId = activeEventId,
            activeRegionId = activeEventRegionId,
            activeCharacterId = activeEventCharacterId
        };
    }

    public void RestoreSaveState(EventManagerSaveData state, Dictionary<string, Region> regionsById)
    {
        ApplyAcknowledgement();

        if (state == null || !state.eventActive || state.activeEventId < 0)
            return;

        if (Events == null || Events.Count == 0)
            Events = LoadEventsFromFile();

        GameEvent restoredEvent = null;
        for (int i = 0; i < Events.Count; i++)
        {
            if (Events[i] != null && Events[i].id == state.activeEventId)
            {
                restoredEvent = Events[i];
                break;
            }
        }

        if (restoredEvent == null)
            return;

        Region restoredRegion = null;
        if (!string.IsNullOrEmpty(state.activeRegionId) && regionsById != null)
            regionsById.TryGetValue(state.activeRegionId, out restoredRegion);

        GameCharacter restoredCharacter = null;
        if (!string.IsNullOrEmpty(state.activeCharacterId) && CharacterManager.Instance != null)
            CharacterManager.Instance.TryGetCharacter(state.activeCharacterId, out restoredCharacter);

        ShowEvent(restoredEvent, restoredRegion, restoredCharacter);
    }

    void ApplyOptionToRegion(EventOption option, Region region)
    {
        if (option == null || region == null)
            return;

        region.ChangeInfluence(option.influenceChange);
        region.ChangeStability(option.stabilityChange);
        region.ChangeDevelopment(option.developmentChange);

        if (option.modifiers == null)
            return;

        for (int i = 0; i < option.modifiers.Count; i++)
        {
            RegionModifierDefinition definition = option.modifiers[i];
            if (definition == null)
                continue;

            RegionModifier modifier = definition.CreateRuntimeModifier();
            region.AddModifier(modifier);
        }
    }

    void ApplyOptionToAllRegions(EventOption option, List<Region> regions)
    {
        if (option == null || regions == null)
            return;

        for (int i = 0; i < regions.Count; i++)
        {
            Region region = regions[i];
            ApplyOptionToRegion(option, region);
        }
    }

    void ApplyResourceChanges(EventOption option, LevelController controller)
    {
        if (option == null || controller == null)
            return;

        if (option.sanityChange != 0)
            controller.ChangeSanity(option.sanityChange);
        if (option.moneyChange != 0)
            controller.ChangeMoney(option.moneyChange);
        if (option.artifactsChange != 0)
            controller.ChangeArtifacts(option.artifactsChange);
    }

    void ApplyCharacterChanges(EventOption option)
    {
        if (option == null || currentEventCharacter == null)
            return;

        currentEventCharacter.trust += option.trustChange;
        currentEventCharacter.loyalty += option.loyaltyChange;
        currentEventCharacter.fear += option.fearChange;
        currentEventCharacter.relationshipWithPlayer += option.relationshipChange;
        currentEventCharacter.corruption += option.corruptionChange;
        currentEventCharacter.influencePower += option.influencePowerChange;
        currentEventCharacter.ClampRuntimeValues();

        if (CharacterManager.Instance != null)
            CharacterManager.Instance.SelectCharacter(currentEventCharacter);
    }

    void RefreshUIAfterOption(EventScope scope, Region affectedRegion, LevelController controller)
    {
        if (UIManager.Instance == null || controller == null)
            return;

        switch (scope)
        {
            case EventScope.Local:
                if (affectedRegion != null)
                    UIManager.Instance.UpdateRegionInfo(affectedRegion);
                else
                    UIManager.Instance.RefreshSelectedRegion();
                break;
            case EventScope.Global:
                UIManager.Instance.RefreshSelectedRegion();
                UIManager.Instance.ShowWorldStats();
                break;
            case EventScope.Personal:
                UIManager.Instance.UpdateSanity(controller.Sanity);
                UIManager.Instance.UpdateMoney(controller.Money);
                UIManager.Instance.UpdateArtifacts(controller.Artifacts);
                UIManager.Instance.RefreshSelectedRegion();
                break;
        }
    }

    bool CanResolveEventTarget(GameEvent gameEvent)
    {
        if (!RequiresCharacterTarget(gameEvent))
            return true;

        return ResolveTargetCharacter(gameEvent, null) != null;
    }

    bool RequiresCharacterTarget(GameEvent gameEvent)
    {
        return gameEvent != null &&
               gameEvent.scope == EventScope.Personal &&
               (!string.IsNullOrEmpty(gameEvent.targetCharacterRole) ||
                gameEvent.minCharacterTrust > 0 ||
                gameEvent.maxCharacterTrust > 0 ||
                gameEvent.minCharacterCorruption > 0 ||
                gameEvent.requiresRecruitedContact);
    }

    GameCharacter ResolveTargetCharacter(GameEvent gameEvent, GameCharacter providedCharacter)
    {
        if (providedCharacter != null)
            return providedCharacter;

        if (!RequiresCharacterTarget(gameEvent) || CharacterManager.Instance == null)
            return null;

        IReadOnlyList<GameCharacter> candidates = CharacterManager.Instance.Characters;
        List<GameCharacter> matching = new List<GameCharacter>();
        for (int i = 0; i < candidates.Count; i++)
        {
            GameCharacter character = candidates[i];
            if (character == null || !character.IsAvailable)
                continue;
            if (!string.IsNullOrEmpty(gameEvent.targetCharacterRole) && character.role.ToString() != gameEvent.targetCharacterRole)
                continue;
            if (gameEvent.minCharacterTrust > 0 && character.trust < gameEvent.minCharacterTrust)
                continue;
            if (gameEvent.maxCharacterTrust > 0 && character.trust > gameEvent.maxCharacterTrust)
                continue;
            if (gameEvent.minCharacterCorruption > 0 && character.corruption < gameEvent.minCharacterCorruption)
                continue;
            if (gameEvent.requiresRecruitedContact && !character.recruitedAsContact)
                continue;

            matching.Add(character);
        }

        return matching.Count > 0 ? matching[Random.Range(0, matching.Count)] : null;
    }

    Region ResolveTargetRegion(GameEvent gameEvent, Region providedRegion, GameCharacter character = null)
    {
        if (gameEvent == null)
            return providedRegion;

        if (character != null && !string.IsNullOrEmpty(character.currentRegionId))
            return FindRegionById(character.currentRegionId);

        if (gameEvent.scope != EventScope.Local)
            return null;

        if (providedRegion != null)
            return providedRegion;

        LevelController controller = LevelController.Instance;
        if (controller == null)
            return null;

        if (controller.SelectedRegion != null)
            return controller.SelectedRegion;

        return null;
    }

    Region FindRegionById(string regionId)
    {
        LevelController controller = LevelController.Instance;
        if (controller == null || string.IsNullOrEmpty(regionId))
            return null;

        for (int i = 0; i < controller.AllRegions.Count; i++)
        {
            Region region = controller.AllRegions[i];
            if (region != null && region.Id == regionId)
                return region;
        }

        return null;
    }

    string ComposeEventDescription(GameEvent gameEvent, Region region)
    {
        if (gameEvent == null)
            return string.Empty;

        string description = string.IsNullOrEmpty(gameEvent.description) ? string.Empty : gameEvent.description;

        if (gameEvent.scope == EventScope.Local && region != null)
            description += "\n\nRegion: " + region.Name;
        else if (gameEvent.scope == EventScope.Personal && currentEventCharacter != null)
            description += "\n\nCharacter: " + currentEventCharacter.displayName + " — " + currentEventCharacter.title;
        else if (gameEvent.scope != EventScope.Local)
            description += "\n\nScope: " + gameEvent.scope;

        string featuredSummary = BuildFeaturedPeopleSummary(gameEvent);
        if (!string.IsNullOrEmpty(featuredSummary))
            description += "\n\nFeatured people:\n" + featuredSummary;

        return description;
    }

    void PopulateFeaturedPeople(GameEvent gameEvent)
    {
        Transform container = GetFeaturedPeopleContainer();
        if (container == null)
            return;

        foreach (Transform child in container)
            Destroy(child.gameObject);

        if (gameEvent == null || gameEvent.scope != EventScope.Personal)
        {
            container.gameObject.SetActive(false);
            return;
        }

        if (gameEvent.featuredPeople == null || gameEvent.featuredPeople.Count == 0)
        {
            container.gameObject.SetActive(false);
            return;
        }

        if (featuredPersonEntryPrefab == null)
        {
            Debug.LogWarning("EventManager: featuredPersonEntryPrefab is not assigned on EventManager.");
            container.gameObject.SetActive(false);
            return;
        }

        container.gameObject.SetActive(true);

        for (int i = 0; i < gameEvent.featuredPeople.Count; i++)
        {
            FeaturedPerson person = gameEvent.featuredPeople[i];
            if (person == null)
                continue;

            GameObject entry = Instantiate(featuredPersonEntryPrefab, container);
            Text label = entry.GetComponentInChildren<Text>();
            if (label != null)
                label.text = BuildFeaturedPersonLine(person);
        }
    }

    Transform GetFeaturedPeopleContainer()
    {
        if (featuredPeopleContainer != null)
            return featuredPeopleContainer;

        if (currentEventPanel == null)
            return null;

        Transform namedContainer = currentEventPanel.transform.Find("Panel/FeaturedPeople/Viewport/Content");
        if (namedContainer != null)
            return namedContainer;

        // Fallback: if only one ScrollRect exists, use its content as container.
        ScrollRect scroll = currentEventPanel.GetComponentInChildren<ScrollRect>();
        if (scroll != null && scroll.content != null)
            return scroll.content;

        return null;
    }

    void MaybeTriggerSuspicion(Region region)
    {
        if (region == null)
            return;

        if (Random.value > suspicionChance)
            return;

        region.ChangeInfluence(-Mathf.Abs(suspicionInfluencePenalty));
        region.ChangeStability(-Mathf.Abs(suspicionStabilityPenalty));

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateRegionInfo(region);

        Debug.Log("Citizens grow suspicious of artifact usage, reducing influence and stability.");
        PlayerLog.Add($"Suspicion rises in {region.Name}: Influence -{Mathf.Abs(suspicionInfluencePenalty)}, Stability -{Mathf.Abs(suspicionStabilityPenalty)}");
    }

    string BuildFeaturedPeopleSummary(GameEvent gameEvent)
    {
        if (gameEvent == null || gameEvent.featuredPeople == null || gameEvent.featuredPeople.Count == 0)
            return string.Empty;

        List<string> lines = new List<string>();
        for (int i = 0; i < gameEvent.featuredPeople.Count; i++)
        {
            FeaturedPerson person = gameEvent.featuredPeople[i];
            if (person == null)
                continue;

            string line = BuildFeaturedPersonLine(person);
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }

        return string.Join("\n", lines);
    }

    string BuildFeaturedPersonLine(FeaturedPerson person)
    {
        if (person == null)
            return string.Empty;

        string line = string.IsNullOrEmpty(person.name) ? "Unknown" : person.name;
        if (!string.IsNullOrEmpty(person.position))
            line += " — " + person.position;
        if (!string.IsNullOrEmpty(person.gender))
            line += " (" + person.gender + ")";

        return line;
    }

    void LogEventOpened(GameEvent gameEvent, Region region)
    {
        if (gameEvent == null)
            return;

        string scope = gameEvent.scope.ToString();
        string target = gameEvent.scope == EventScope.Local && region != null ? $" ({region.Name})" : string.Empty;
        if (gameEvent.scope == EventScope.Personal && currentEventCharacter != null)
            target = $" ({currentEventCharacter.displayName})";
        PlayerLog.Add($"New {scope} event: {gameEvent.description}{target}");
    }

    void LogOptionChoice(GameEvent gameEvent, EventOption option, EventScope scope, Region region)
    {
        if (option == null)
            return;

        string optionLabel = BuildOptionLabel(option);
        string title = gameEvent != null ? gameEvent.description : "Event";
        string scopeText = scope == EventScope.Local && region != null ? $" in {region.Name}" : $" ({scope})";
        if (scope == EventScope.Personal && currentEventCharacter != null)
            scopeText = $" involving {currentEventCharacter.displayName}";
        string changeSummary = BuildOptionChangeSummary(option);
        string message = $"Chose \"{optionLabel}\" for \"{title}\"{scopeText}";

        if (!string.IsNullOrEmpty(changeSummary))
            message += $" | {changeSummary}";

        PlayerLog.Add(message);
    }

    string BuildOptionChangeSummary(EventOption option)
    {
        List<string> changes = new List<string>();

        AppendChange(changes, "Sanity", option.sanityChange);
        AppendChange(changes, "Money", option.moneyChange);
        AppendChange(changes, "Artifacts", option.artifactsChange);
        AppendChange(changes, "Influence", option.influenceChange);
        AppendChange(changes, "Stability", option.stabilityChange);
        AppendChange(changes, "Development", option.developmentChange);
        AppendChange(changes, "Trust", option.trustChange);
        AppendChange(changes, "Loyalty", option.loyaltyChange);
        AppendChange(changes, "Fear", option.fearChange);
        AppendChange(changes, "Relationship", option.relationshipChange);
        AppendChange(changes, "Corruption", option.corruptionChange);
        AppendChange(changes, "Power", option.influencePowerChange);

        return changes.Count > 0 ? string.Join(", ", changes) : string.Empty;
    }

    void AppendChange(List<string> container, string label, int delta)
    {
        if (delta == 0)
            return;

        string sign = delta > 0 ? "+" : string.Empty;
        container.Add($"{label} {sign}{delta}");
    }

    float GetCurrentTime()
    {
        // Use scaled time so scheduling respects pause and time scale changes.
        return Time.time;
    }
}

[System.Serializable]
public class RawEventsWrapper
{
    public List<RawGameEvent> events;
}

[System.Serializable]
public class RawGameEvent
{
    public int id;
    public string description;
    public List<EventOption> options;
    public bool isBadEvent;
    public string scope;
    public List<FeaturedPerson> featuredPeople = new List<FeaturedPerson>();
    public string targetCharacterRole;
    public int minCharacterTrust;
    public int maxCharacterTrust;
    public int minCharacterCorruption;
    public bool requiresRecruitedContact;
}
