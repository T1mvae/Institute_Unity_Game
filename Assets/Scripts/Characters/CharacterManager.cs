using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    public event Action OnCharactersChanged;
    public event Action<GameCharacter> OnCharacterSelected;

    [SerializeField] private int passiveEffectDayInterval = 5;
    [SerializeField] private int maxCharacters = 80;

    readonly List<GameCharacter> characters = new List<GameCharacter>();
    readonly Dictionary<string, GameCharacter> characterById = new Dictionary<string, GameCharacter>();
    readonly Dictionary<CharacterInteractionType, CharacterInteractionDefinition> interactions = new Dictionary<CharacterInteractionType, CharacterInteractionDefinition>();
    readonly System.Random fallbackRng = new System.Random();

    GameCharacter selectedCharacter;
    float fallbackPassiveTimer;
    int changeVersion;

    public IReadOnlyList<GameCharacter> Characters => characters;
    public GameCharacter SelectedCharacter => selectedCharacter;
    public int ChangeVersion => changeVersion;

    static readonly string[] FirstNames =
    {
        "Aren", "Bela", "Cairn", "Dara", "Elian", "Fessa", "Garron", "Hale",
        "Ivara", "Joric", "Kessa", "Lorin", "Mara", "Nolan", "Orra", "Perrin",
        "Quill", "Rhea", "Soren", "Tamsin", "Ulric", "Vela", "Wren", "Ysolde"
    };

    static readonly string[] LastNames =
    {
        "Ashmark", "Brindle", "Cairnwell", "Duskmere", "Emberfall", "Farrow",
        "Greyhand", "Harth", "Ironbell", "Junewick", "Kestrel", "Lowmere",
        "Mourn", "Narrowgate", "Oldmarch", "Pale", "Rook", "Saltmere",
        "Thorn", "Umber", "Vey", "Westmere", "Yarrow", "Zephyr"
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceLocator.Register<CharacterManager>(this);
        BuildInteractionDefinitions();
    }

    void OnEnable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay += OnNewDay;
    }

    void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay -= OnNewDay;
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay += OnNewDay;
    }

    void Update()
    {
        if (TimeManager.Instance != null)
            return;

        fallbackPassiveTimer += Time.deltaTime;
        if (fallbackPassiveTimer < 15f)
            return;

        fallbackPassiveTimer = 0f;
        ApplyPassiveEffects(-1);
    }

    void OnDisable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay -= OnNewDay;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<CharacterManager>();
            Instance = null;
        }
    }

    public void GenerateForCurrentWorld()
    {
        LevelController controller = LevelController.Instance;
        if (controller == null || controller.AllRegions.Count == 0)
            return;

        characters.Clear();
        characterById.Clear();
        selectedCharacter = null;

        int seed = GameSession.ActiveDifficulty.randomSeed ^ 0x51A7C0DE;
        System.Random rng = new System.Random(seed);

        for (int i = 0; i < controller.AllRegions.Count && characters.Count < maxCharacters; i++)
        {
            Region region = controller.AllRegions[i];
            AddCharacter(CreateCharacterForRegion(region, rng, true));

            if (region.Development >= 10 && characters.Count < maxCharacters && rng.NextDouble() < 0.55)
                AddCharacter(CreateCharacterForRegion(region, rng, false));
        }

        changeVersion++;
        OnCharactersChanged?.Invoke();
        PlayerLog.Add($"Generated {characters.Count} regional characters.");
    }

    public void RestoreCharacters(List<CharacterSaveData> savedCharacters)
    {
        characters.Clear();
        characterById.Clear();
        selectedCharacter = null;

        if (savedCharacters != null)
        {
            for (int i = 0; i < savedCharacters.Count; i++)
            {
                CharacterSaveData saved = savedCharacters[i];
                if (saved == null)
                    continue;

                GameCharacter character = saved.ToRuntimeCharacter();
                AddCharacter(character);
            }
        }

        changeVersion++;
        OnCharactersChanged?.Invoke();
    }

    public List<CharacterSaveData> CaptureSaveData()
    {
        List<CharacterSaveData> data = new List<CharacterSaveData>();
        for (int i = 0; i < characters.Count; i++)
            data.Add(CharacterSaveData.FromRuntimeCharacter(characters[i]));
        return data;
    }

    public List<GameCharacter> GetCharactersInRegion(string regionId)
    {
        List<GameCharacter> result = new List<GameCharacter>();
        if (string.IsNullOrEmpty(regionId))
            return result;

        for (int i = 0; i < characters.Count; i++)
        {
            GameCharacter character = characters[i];
            if (character != null && character.currentRegionId == regionId && character.status != CharacterStatus.Dead)
                result.Add(character);
        }

        result.Sort((a, b) => b.influencePower.CompareTo(a.influencePower));
        return result;
    }

    public List<GameCharacter> SearchCharacters(string filter, bool sortByInfluence)
    {
        string needle = string.IsNullOrWhiteSpace(filter) ? string.Empty : filter.Trim().ToLowerInvariant();
        List<GameCharacter> result = new List<GameCharacter>();

        for (int i = 0; i < characters.Count; i++)
        {
            GameCharacter character = characters[i];
            if (character == null)
                continue;

            if (string.IsNullOrEmpty(needle) || MatchesFilter(character, needle))
                result.Add(character);
        }

        if (sortByInfluence)
            result.Sort((a, b) => b.influencePower.CompareTo(a.influencePower));
        else
            result.Sort((a, b) => b.relationshipWithPlayer.CompareTo(a.relationshipWithPlayer));

        return result;
    }

    public bool TryGetCharacter(string characterId, out GameCharacter character)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            character = null;
            return false;
        }

        return characterById.TryGetValue(characterId, out character);
    }

    public void SelectCharacter(GameCharacter character)
    {
        selectedCharacter = character;
        OnCharacterSelected?.Invoke(character);
    }

    public CharacterInteractionDefinition GetInteractionDefinition(CharacterInteractionType interactionType)
    {
        interactions.TryGetValue(interactionType, out CharacterInteractionDefinition definition);
        return definition;
    }

    public IReadOnlyCollection<CharacterInteractionDefinition> GetInteractionDefinitions()
    {
        return interactions.Values;
    }

    public CharacterInteractionResult TryApplyInteraction(GameCharacter character, CharacterInteractionType interactionType)
    {
        if (character == null)
            return Fail("No character selected.");
        if (!character.IsAvailable)
            return Fail($"{character.displayName} is not available.");
        if (!interactions.TryGetValue(interactionType, out CharacterInteractionDefinition definition))
            return Fail("Unknown interaction.");
        if (IsOnCooldown(character, interactionType, out float remaining))
            return Fail($"{definition.displayName} is cooling down for {Mathf.CeilToInt(remaining)}s.");
        if (!MeetsRequirements(character, definition, out string requirementMessage))
            return Fail(requirementMessage);
        if (!SpendCosts(definition))
            return Fail("Insufficient resources.");

        Region region = FindRegion(character.currentRegionId);
        ApplyInteractionEffects(character, region, interactionType);
        BeginCooldown(character, interactionType, definition.cooldownSeconds);
        character.ClampRuntimeValues();

        string message = BuildResultMessage(character, interactionType, region);
        PlayerLog.Add(message);
        changeVersion++;
        OnCharactersChanged?.Invoke();
        OnCharacterSelected?.Invoke(character);
        return new CharacterInteractionResult { success = true, message = message };
    }

    public string BuildRegionCharacterModifierSummary(string regionId)
    {
        List<GameCharacter> regionCharacters = GetCharactersInRegion(regionId);
        if (regionCharacters.Count == 0)
            return "Character modifiers: none";

        List<string> lines = new List<string> { "Character modifiers:" };
        for (int i = 0; i < regionCharacters.Count; i++)
        {
            GameCharacter character = regionCharacters[i];
            lines.Add($"- {character.displayName}: {character.BuildPassiveEffectSummary()}");
        }

        return string.Join("\n", lines);
    }

    void OnNewDay(int day)
    {
        ApplyPassiveEffects(day);
    }

    void ApplyPassiveEffects(int day)
    {
        if (day > 0 && passiveEffectDayInterval > 1 && day % passiveEffectDayInterval != 0)
            return;

        for (int i = 0; i < characters.Count; i++)
        {
            GameCharacter character = characters[i];
            if (character == null || character.status != CharacterStatus.Active)
                continue;
            if (day > 0 && character.lastPassiveDayApplied == day)
                continue;

            Region region = FindRegion(character.currentRegionId);
            if (region == null)
                continue;

            ApplyPassiveEffect(character, region);
            if (day > 0)
                character.lastPassiveDayApplied = day;
        }

        changeVersion++;
        OnCharactersChanged?.Invoke();
    }

    void ApplyPassiveEffect(GameCharacter character, Region region)
    {
        switch (character.role)
        {
            case CharacterRole.LocalLord:
                if (character.loyalty >= 55)
                    region.ChangeInfluence(1);
                else if (character.ambition > character.loyalty + 20)
                    region.ChangeStability(-1);
                break;
            case CharacterRole.TempleElder:
                region.ChangeStability(1);
                if (character.trust < 35 && region.Development > 6)
                    region.ChangeDevelopment(-1);
                break;
            case CharacterRole.GuildRepresentative:
                if (character.corruption >= 55)
                {
                    if (LevelController.Instance != null)
                        LevelController.Instance.ChangeMoney(2);
                    region.ChangeStability(-1);
                }
                else
                {
                    region.ChangeDevelopment(1);
                }
                break;
            case CharacterRole.RebelOrganizer:
                if (character.recruitedAsContact)
                    region.ChangeInfluence(1);
                else
                    region.ChangeStability(-1);
                break;
            case CharacterRole.Scholar:
                region.ChangeDevelopment(1);
                break;
            case CharacterRole.MercenaryCaptain:
                region.ChangeStability(1);
                if (character.fear > 65)
                    region.ChangeInfluence(1);
                break;
            case CharacterRole.InstituteSympathizer:
                region.ChangeInfluence(1);
                if (character.trust < 35 && fallbackRng.NextDouble() < 0.15)
                    region.ChangeStability(-1);
                break;
            default:
                if (character.relationshipWithPlayer > 40)
                    region.ChangeInfluence(1);
                break;
        }
    }

    void ApplyInteractionEffects(GameCharacter character, Region region, CharacterInteractionType interactionType)
    {
        switch (interactionType)
        {
            case CharacterInteractionType.Negotiate:
                character.trust += 8 + character.competence / 25;
                character.relationshipWithPlayer += 6;
                if (region != null)
                    region.ChangeInfluence(1);
                break;
            case CharacterInteractionType.Bribe:
                character.loyalty += 12;
                character.corruption += 8;
                character.relationshipWithPlayer += 3;
                if (region != null && character.corruption > 70 && UnityEngine.Random.value < 0.25f)
                    region.ChangeStability(-1);
                break;
            case CharacterInteractionType.Threaten:
                character.fear += 15;
                character.trust -= 9;
                character.relationshipWithPlayer -= 5;
                if (region != null)
                {
                    region.ChangeInfluence(1);
                    if (character.fear > 70)
                        region.ChangeStability(-1);
                }
                break;
            case CharacterInteractionType.Support:
                character.influencePower += 8;
                character.loyalty += 8;
                character.relationshipWithPlayer += 4;
                if (region != null)
                    ApplySupportRegionalEffect(character, region);
                break;
            case CharacterInteractionType.Undermine:
                character.influencePower -= 10;
                character.relationshipWithPlayer -= 10;
                character.fear += 6;
                if (region != null)
                    region.ChangeStability(-1);
                break;
            case CharacterInteractionType.RecruitAsContact:
                character.recruitedAsContact = true;
                character.tags.Add("Institute contact");
                character.relationshipWithPlayer += 12;
                character.loyalty += 10;
                if (region != null)
                    region.ChangeInfluence(2);
                break;
            case CharacterInteractionType.Investigate:
                RevealHiddenTrait(character);
                character.trust -= 2;
                break;
        }
    }

    void ApplySupportRegionalEffect(GameCharacter character, Region region)
    {
        switch (character.role)
        {
            case CharacterRole.Scholar:
            case CharacterRole.GuildRepresentative:
                region.ChangeDevelopment(1);
                break;
            case CharacterRole.TempleElder:
            case CharacterRole.MercenaryCaptain:
            case CharacterRole.LocalLord:
                region.ChangeStability(1);
                break;
            case CharacterRole.RebelOrganizer:
                region.ChangeStability(-1);
                region.ChangeInfluence(1);
                break;
            default:
                region.ChangeInfluence(1);
                break;
        }
    }

    void RevealHiddenTrait(GameCharacter character)
    {
        if (character.hiddenTraits.Count == 0)
        {
            if (!character.revealedTraits.Contains("No major secrets found"))
                character.revealedTraits.Add("No major secrets found");
            return;
        }

        string trait = character.hiddenTraits[0];
        character.hiddenTraits.RemoveAt(0);
        if (!character.revealedTraits.Contains(trait))
            character.revealedTraits.Add(trait);
    }

    bool MeetsRequirements(GameCharacter character, CharacterInteractionDefinition definition, out string message)
    {
        if (definition.interactionType == CharacterInteractionType.RecruitAsContact)
        {
            if (character.recruitedAsContact)
            {
                message = "Already recruited as an Institute contact.";
                return false;
            }

            bool hasTrustRoute = definition.minTrust <= 0 || character.trust >= definition.minTrust;
            bool hasFearRoute = definition.minFear <= 0 || character.fear >= definition.minFear;
            if (!hasTrustRoute && !hasFearRoute)
            {
                message = $"Requires Trust {definition.minTrust} or Fear {definition.minFear}.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        if (definition.minTrust > 0 && character.trust < definition.minTrust)
        {
            message = $"Requires Trust {definition.minTrust}.";
            return false;
        }
        if (definition.minFear > 0 && character.fear < definition.minFear)
        {
            message = $"Requires Fear {definition.minFear}.";
            return false;
        }
        if (definition.minRelationship > -100 && character.relationshipWithPlayer < definition.minRelationship)
        {
            message = $"Requires Relationship {definition.minRelationship}.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    bool SpendCosts(CharacterInteractionDefinition definition)
    {
        LevelController controller = LevelController.Instance;
        if (controller == null)
            return false;

        if (controller.Money < definition.moneyCost || controller.Sanity < definition.sanityCost || controller.Artifacts < definition.artifactCost)
            return false;

        if (definition.moneyCost > 0)
            controller.ChangeMoney(-definition.moneyCost);
        if (definition.sanityCost > 0)
            controller.ChangeSanity(-definition.sanityCost);
        if (definition.artifactCost > 0)
            controller.ChangeArtifacts(-definition.artifactCost);

        return true;
    }

    bool IsOnCooldown(GameCharacter character, CharacterInteractionType interactionType, out float remaining)
    {
        remaining = 0f;
        for (int i = character.cooldowns.Count - 1; i >= 0; i--)
        {
            CharacterInteractionCooldown cooldown = character.cooldowns[i];
            if (Time.time >= cooldown.readyAtTime)
            {
                character.cooldowns.RemoveAt(i);
                continue;
            }

            if (cooldown.interactionType == interactionType)
            {
                remaining = cooldown.readyAtTime - Time.time;
                return true;
            }
        }

        return false;
    }

    void BeginCooldown(GameCharacter character, CharacterInteractionType interactionType, float cooldownSeconds)
    {
        for (int i = character.cooldowns.Count - 1; i >= 0; i--)
        {
            if (character.cooldowns[i].interactionType == interactionType)
                character.cooldowns.RemoveAt(i);
        }

        character.cooldowns.Add(new CharacterInteractionCooldown
        {
            interactionType = interactionType,
            readyAtTime = Time.time + Mathf.Max(0f, cooldownSeconds)
        });
    }

    string BuildResultMessage(GameCharacter character, CharacterInteractionType interactionType, Region region)
    {
        string regionLabel = region != null ? $" in {region.Name}" : string.Empty;
        return $"{interactionType} with {character.displayName}{regionLabel}: Trust {character.trust}, Loyalty {character.loyalty}, Fear {character.fear}, Power {character.influencePower}.";
    }

    CharacterInteractionResult Fail(string message)
    {
        return new CharacterInteractionResult { success = false, message = message };
    }

    GameCharacter CreateCharacterForRegion(Region region, System.Random rng, bool primary)
    {
        CharacterRole role = PickRole(region, rng, primary);
        GameCharacter character = new GameCharacter
        {
            id = Guid.NewGuid().ToString("N"),
            displayName = PickName(rng),
            title = FormatRole(role),
            portraitId = role.ToString(),
            portraitColor = PickPortraitColor(role),
            homeRegionId = region.Id,
            currentRegionId = region.Id,
            faction = PickFaction(role, region, rng),
            role = role,
            status = CharacterStatus.Active,
            relationshipWithPlayer = rng.Next(-15, 26),
            loyalty = rng.Next(25, 66),
            fear = rng.Next(5, 36),
            trust = rng.Next(15, 56),
            ambition = rng.Next(20, 86),
            competence = rng.Next(25, 86),
            corruption = rng.Next(0, 71),
            influencePower = Mathf.Clamp(rng.Next(20, 75) + region.Influence, 0, 100)
        };

        AddRoleTags(character);
        AddTraits(character, rng);
        AddIdeology(character, role, rng);
        AddHiddenTraits(character, rng);
        character.ClampRuntimeValues();
        return character;
    }

    CharacterRole PickRole(Region region, System.Random rng, bool primary)
    {
        if (primary)
        {
            switch (region.Type)
            {
                case RegionType.CoreSettlement: return CharacterRole.LocalLord;
                case RegionType.ReligiousCenter: return CharacterRole.TempleElder;
                case RegionType.TradeHub: return CharacterRole.GuildRepresentative;
                case RegionType.Ruins: return rng.NextDouble() < 0.5 ? CharacterRole.Scholar : CharacterRole.InstituteSympathizer;
                case RegionType.Mountain: return CharacterRole.MercenaryCaptain;
                case RegionType.Frontier: return rng.NextDouble() < 0.5 ? CharacterRole.VillageSpeaker : CharacterRole.RebelOrganizer;
                default: return CharacterRole.VillageSpeaker;
            }
        }

        CharacterRole[] options =
        {
            CharacterRole.TempleElder,
            CharacterRole.GuildRepresentative,
            CharacterRole.RebelOrganizer,
            CharacterRole.Scholar,
            CharacterRole.MercenaryCaptain,
            CharacterRole.InstituteSympathizer,
            CharacterRole.VillageSpeaker
        };
        return options[rng.Next(options.Length)];
    }

    string PickName(System.Random rng)
    {
        return FirstNames[rng.Next(FirstNames.Length)] + " " + LastNames[rng.Next(LastNames.Length)];
    }

    string PickFaction(CharacterRole role, Region region, System.Random rng)
    {
        switch (role)
        {
            case CharacterRole.LocalLord: return "Feudal Court";
            case CharacterRole.TempleElder: return "Temple Compact";
            case CharacterRole.GuildRepresentative: return "Merchant Guild";
            case CharacterRole.RebelOrganizer: return "Common League";
            case CharacterRole.Scholar: return "Free Scholars";
            case CharacterRole.MercenaryCaptain: return "Iron Companies";
            case CharacterRole.InstituteSympathizer: return "Hidden Institute Cell";
            default: return region.Type == RegionType.Frontier ? "Frontier Villages" : "Local Assembly";
        }
    }

    void AddRoleTags(GameCharacter character)
    {
        switch (character.role)
        {
            case CharacterRole.LocalLord:
                character.tags.Add("Noble");
                break;
            case CharacterRole.TempleElder:
                character.tags.Add("Priest");
                break;
            case CharacterRole.GuildRepresentative:
                character.tags.Add("Merchant");
                break;
            case CharacterRole.RebelOrganizer:
                character.tags.Add("Rebel");
                character.tags.Add("Peasant leader");
                break;
            case CharacterRole.Scholar:
                character.tags.Add("Scholar");
                break;
            case CharacterRole.MercenaryCaptain:
                character.tags.Add("Commander");
                break;
            case CharacterRole.InstituteSympathizer:
                character.tags.Add("Spy");
                character.tags.Add("Institute contact");
                break;
            default:
                character.tags.Add("Peasant leader");
                break;
        }
    }

    void AddTraits(GameCharacter character, System.Random rng)
    {
        string[] pool = { "cautious", "zealous", "pragmatic", "vain", "patient", "ruthless", "curious", "superstitious", "honorable", "venal" };
        character.personalityTraits.Add(pool[rng.Next(pool.Length)]);
        character.personalityTraits.Add(pool[rng.Next(pool.Length)]);
    }

    void AddIdeology(GameCharacter character, CharacterRole role, System.Random rng)
    {
        if (role == CharacterRole.RebelOrganizer)
            character.ideology.Add("egalitarian");
        else if (role == CharacterRole.TempleElder)
            character.ideology.Add("traditionalist");
        else if (role == CharacterRole.Scholar || role == CharacterRole.InstituteSympathizer)
            character.ideology.Add("reformist");
        else
            character.ideology.Add(rng.NextDouble() < 0.5 ? "order-first" : "opportunist");
    }

    void AddHiddenTraits(GameCharacter character, System.Random rng)
    {
        if (character.corruption > 50)
            character.hiddenTraits.Add("Secret graft network");
        if (character.ambition > 70)
            character.hiddenTraits.Add("Succession ambitions");
        if (character.role == CharacterRole.InstituteSympathizer)
            character.hiddenTraits.Add("Knows Institute signal codes");
        if (rng.NextDouble() < 0.18)
            character.hiddenTraits.Add("Foreign faction link");
    }

    Color PickPortraitColor(CharacterRole role)
    {
        switch (role)
        {
            case CharacterRole.LocalLord: return new Color(0.48f, 0.38f, 0.18f);
            case CharacterRole.TempleElder: return new Color(0.45f, 0.32f, 0.62f);
            case CharacterRole.GuildRepresentative: return new Color(0.54f, 0.40f, 0.12f);
            case CharacterRole.RebelOrganizer: return new Color(0.58f, 0.18f, 0.18f);
            case CharacterRole.Scholar: return new Color(0.16f, 0.42f, 0.62f);
            case CharacterRole.MercenaryCaptain: return new Color(0.30f, 0.34f, 0.38f);
            case CharacterRole.InstituteSympathizer: return UITheme.AccentPrimary;
            default: return new Color(0.28f, 0.42f, 0.30f);
        }
    }

    string FormatRole(CharacterRole role)
    {
        switch (role)
        {
            case CharacterRole.LocalLord: return "Local Lord";
            case CharacterRole.TempleElder: return "Temple Elder";
            case CharacterRole.GuildRepresentative: return "Guild Representative";
            case CharacterRole.RebelOrganizer: return "Rebel Organizer";
            case CharacterRole.VillageSpeaker: return "Village Speaker";
            case CharacterRole.MercenaryCaptain: return "Mercenary Captain";
            case CharacterRole.InstituteSympathizer: return "Secret Institute Sympathizer";
            default: return role.ToString();
        }
    }

    void AddCharacter(GameCharacter character)
    {
        if (character == null || string.IsNullOrEmpty(character.id))
            return;

        character.ClampRuntimeValues();
        characters.Add(character);
        characterById[character.id] = character;
    }

    Region FindRegion(string regionId)
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

    bool MatchesFilter(GameCharacter character, string needle)
    {
        if (Contains(character.displayName, needle) ||
            Contains(character.title, needle) ||
            Contains(character.faction, needle) ||
            Contains(character.currentRegionId, needle) ||
            Contains(character.status.ToString(), needle) ||
            Contains(character.relationshipWithPlayer.ToString(), needle))
            return true;

        return ContainsAny(character.tags, needle) ||
               ContainsAny(character.personalityTraits, needle) ||
               ContainsAny(character.revealedTraits, needle);
    }

    bool Contains(string value, string needle)
    {
        return !string.IsNullOrEmpty(value) && value.ToLowerInvariant().Contains(needle);
    }

    bool ContainsAny(List<string> values, string needle)
    {
        if (values == null)
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (Contains(values[i], needle))
                return true;
        }

        return false;
    }

    void BuildInteractionDefinitions()
    {
        interactions[CharacterInteractionType.Negotiate] = new CharacterInteractionDefinition
        {
            interactionType = CharacterInteractionType.Negotiate,
            displayName = "Negotiate",
            moneyCost = 4,
            sanityCost = 3,
            minRelationship = -35,
            cooldownSeconds = 18f
        };
        interactions[CharacterInteractionType.Bribe] = new CharacterInteractionDefinition
        {
            interactionType = CharacterInteractionType.Bribe,
            displayName = "Bribe",
            moneyCost = 18,
            cooldownSeconds = 28f
        };
        interactions[CharacterInteractionType.Threaten] = new CharacterInteractionDefinition
        {
            interactionType = CharacterInteractionType.Threaten,
            displayName = "Threaten",
            sanityCost = 8,
            cooldownSeconds = 26f
        };
        interactions[CharacterInteractionType.Support] = new CharacterInteractionDefinition
        {
            interactionType = CharacterInteractionType.Support,
            displayName = "Support",
            moneyCost = 16,
            cooldownSeconds = 36f
        };
        interactions[CharacterInteractionType.Undermine] = new CharacterInteractionDefinition
        {
            interactionType = CharacterInteractionType.Undermine,
            displayName = "Undermine",
            moneyCost = 10,
            sanityCost = 5,
            cooldownSeconds = 32f
        };
        interactions[CharacterInteractionType.RecruitAsContact] = new CharacterInteractionDefinition
        {
            interactionType = CharacterInteractionType.RecruitAsContact,
            displayName = "Recruit Contact",
            artifactCost = 1,
            minTrust = 55,
            minFear = 35,
            cooldownSeconds = 60f
        };
        interactions[CharacterInteractionType.Investigate] = new CharacterInteractionDefinition
        {
            interactionType = CharacterInteractionType.Investigate,
            displayName = "Investigate",
            moneyCost = 6,
            sanityCost = 2,
            cooldownSeconds = 22f
        };
    }
}
