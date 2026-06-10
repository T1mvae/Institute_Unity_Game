using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveLoadManager : MonoBehaviour
{
    public const int CurrentSaveVersion = 1;
    public const string AutoSaveSlot = "autosave";
    public const string ManualSaveSlot = "manual";

    public static SaveLoadManager Instance { get; private set; }

    [SerializeField] private float autosaveIntervalSeconds = 120f;

    float autosaveTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceLocator.Register<SaveLoadManager>(this);
    }

    void Update()
    {
        if (autosaveIntervalSeconds <= 0f || LevelController.Instance == null)
            return;

        autosaveTimer += Time.unscaledDeltaTime;
        if (autosaveTimer < autosaveIntervalSeconds)
            return;

        autosaveTimer = 0f;
        TrySaveGame(AutoSaveSlot);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<SaveLoadManager>();
            Instance = null;
        }
    }

    public bool TrySaveGame(string slotName = ManualSaveSlot)
    {
        try
        {
            SaveGameData data = CaptureGameState();
            string json = JsonUtility.ToJson(data, true);
            string path = GetSavePath(slotName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            Debug.Log("Saved game to " + path);
            PlayerLog.Add("Game saved: " + slotName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Save failed: " + ex.Message);
            return false;
        }
    }

    public bool TryLoadGame(string slotName = AutoSaveSlot)
    {
        string path = GetSavePath(slotName);
        if (!File.Exists(path))
        {
            Debug.LogWarning("Save file not found: " + path);
            GameSession.ClearLoadRequest();
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
            if (data == null)
            {
                Debug.LogWarning("Save file is empty or unreadable: " + path);
                GameSession.ClearLoadRequest();
                return false;
            }

            if (data.saveVersion != CurrentSaveVersion)
            {
                Debug.LogWarning($"Save version mismatch. Expected {CurrentSaveVersion}, got {data.saveVersion}.");
                GameSession.ClearLoadRequest();
                return false;
            }

            ApplyGameState(data);
            GameSession.ClearLoadRequest();
            PlayerLog.Add("Game loaded: " + slotName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Load failed: " + ex.Message);
            GameSession.ClearLoadRequest();
            return false;
        }
    }

    public SaveGameData CaptureGameState()
    {
        LevelController controller = LevelController.Instance;
        SaveGameData data = new SaveGameData
        {
            saveVersion = CurrentSaveVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            difficulty = GameSession.ActiveDifficulty.Clone(),
            randomSeed = GameSession.ActiveDifficulty.randomSeed,
            elapsedGameTime = GetElapsedTime(),
            money = controller != null ? controller.Money : GameSession.ActiveDifficulty.startingMoney,
            artifacts = controller != null ? controller.Artifacts : GameSession.ActiveDifficulty.startingArtifacts,
            sanity = controller != null ? controller.Sanity : GameSession.ActiveDifficulty.startingSanity,
            selectedRegionId = controller != null && controller.SelectedRegion != null ? controller.SelectedRegion.Id : string.Empty
        };

        if (controller != null)
        {
            for (int i = 0; i < controller.AllRegions.Count; i++)
                data.regions.Add(CaptureRegion(controller.AllRegions[i]));
        }

        if (DecisionSelectionManager.Instance != null)
            data.decisionCooldowns = DecisionSelectionManager.Instance.CaptureCooldowns();

        EventManager eventManager = FindFirstObjectByType<EventManager>();
        if (eventManager != null)
            data.eventState = eventManager.CaptureSaveState();

        if (CharacterManager.Instance != null)
            data.characters = CharacterManager.Instance.CaptureSaveData();

        return data;
    }

    public void ApplyGameState(SaveGameData data)
    {
        if (data == null)
            return;

        GameSession.SetDifficultyFromSave(data.difficulty);

        LevelController controller = LevelController.Instance;
        if (controller == null)
            return;

        HexMapGenerator hexGenerator = controller.HexMapGenerator;
        if (hexGenerator == null)
            hexGenerator = controller.GetComponent<HexMapGenerator>();

        if (hexGenerator != null)
            hexGenerator.LoadRegions(data.regions);

        if (CharacterManager.Instance != null)
            CharacterManager.Instance.RestoreCharacters(data.characters);

        controller.SetResources(data.sanity, data.money, data.artifacts);
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.SyncFromLevelController();
        SetElapsedTime(data.elapsedGameTime);

        Dictionary<string, Region> regionsById = BuildRegionLookup(controller.AllRegions);
        if (DecisionSelectionManager.Instance != null)
            DecisionSelectionManager.Instance.RestoreCooldowns(data.decisionCooldowns, regionsById);

        if (!string.IsNullOrEmpty(data.selectedRegionId) && regionsById.TryGetValue(data.selectedRegionId, out Region selectedRegion))
            controller.SelectRegion(selectedRegion);
        else
            controller.ShowWorldStats();

        EventManager eventManager = FindFirstObjectByType<EventManager>();
        if (eventManager != null)
            eventManager.RestoreSaveState(data.eventState, regionsById);

        if (UIManager.Instance != null)
            UIManager.Instance.RefreshSelectedRegion();
    }

    RegionSaveData CaptureRegion(Region region)
    {
        RegionSaveData data = new RegionSaveData
        {
            id = region.Id,
            name = region.Name,
            hexQ = region.HexQ,
            hexR = region.HexR,
            regionType = region.Type.ToString(),
            influence = region.Influence,
            stability = region.Stability,
            development = region.Development
        };

        data.tags.AddRange(region.Tags);
        data.neighborIds.AddRange(region.NeighborIds);

        IReadOnlyList<RegionModifier> modifiers = region.ActiveModifiers;
        for (int i = 0; i < modifiers.Count; i++)
        {
            RegionModifier modifier = modifiers[i];
            data.modifiers.Add(new ModifierSaveData
            {
                name = modifier.Name,
                durationSeconds = modifier.DurationSeconds,
                tickIntervalSeconds = modifier.TickIntervalSeconds,
                influencePerTick = modifier.InfluencePerTick,
                stabilityPerTick = modifier.StabilityPerTick,
                developmentPerTick = modifier.DevelopmentPerTick,
                remainingDurationSeconds = modifier.RemainingDuration
            });
        }

        return data;
    }

    Dictionary<string, Region> BuildRegionLookup(List<Region> regions)
    {
        Dictionary<string, Region> lookup = new Dictionary<string, Region>();
        if (regions == null)
            return lookup;

        for (int i = 0; i < regions.Count; i++)
        {
            Region region = regions[i];
            if (region != null && !string.IsNullOrEmpty(region.Id))
                lookup[region.Id] = region;
        }

        return lookup;
    }

    float GetElapsedTime()
    {
        if (TimeManager.Instance != null)
            return TimeManager.Instance.ElapsedTime;
        if (TimerUI.Instance != null)
            return TimerUI.Instance.timeElapsed;
        return Time.time;
    }

    void SetElapsedTime(float elapsedTime)
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.SetElapsedTime(elapsedTime);
        if (TimerUI.Instance != null)
            TimerUI.Instance.SetElapsedTime(elapsedTime);
    }

    public static bool SaveExists(string slotName = AutoSaveSlot)
    {
        return File.Exists(GetSavePath(slotName));
    }

    public static string GetSavePath(string slotName)
    {
        string safeSlot = string.IsNullOrWhiteSpace(slotName) ? AutoSaveSlot : slotName.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safeSlot = safeSlot.Replace(invalid, '_');

        return Path.Combine(Application.persistentDataPath, "Saves", safeSlot + ".json");
    }
}
