using System.Collections.Generic;
using UnityEngine;

public class DecisionSelectionManager : MonoBehaviour
{
    public static DecisionSelectionManager Instance { get; private set; }

    int lastSelectionFrame = -1;
    bool isProcessingSelection;
    readonly Dictionary<string, float> globalCooldowns = new Dictionary<string, float>();
    readonly Dictionary<string, Dictionary<Region, float>> regionalCooldowns = new Dictionary<string, Dictionary<Region, float>>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SelectDecision(DecisionDefinition definition, ActionButton sourceButton = null)
    {
        if (definition == null)
            return;

        if (isProcessingSelection || lastSelectionFrame == Time.frameCount)
            return;

        var controller = LevelController.Instance;
        if (controller == null)
            return;

        isProcessingSelection = true;
        lastSelectionFrame = Time.frameCount;

        try
        {
            bool affectsRegion = AffectsRegion(definition);

            if (affectsRegion)
            {
                var targetRegion = controller.SelectedRegion;
                if (IsOnCooldown(definition, targetRegion))
                    return;

                ApplyToRegion(definition, targetRegion);
            }
            else
            {
                if (IsOnCooldown(definition, null))
                    return;

                ExecuteImmediate(definition);
            }
        }
        finally
        {
            isProcessingSelection = false;
        }
    }

    void ExecuteImmediate(DecisionDefinition definition)
    {
        bool executed = TryApplyDefinitionImmediate(definition);

        if (executed)
        {
            BeginCooldown(definition, null);
            LogDecisionApplied(definition, null);
        }
    }

    bool ApplyToRegion(DecisionDefinition definition, Region region)
    {
        if (region == null)
            return false;

        bool executed = TryApplyDefinitionOnRegion(definition, region);

        if (executed)
        {
            BeginCooldown(definition, region);
            LogDecisionApplied(definition, region);
        }

        return executed;
    }

    bool TryApplyDefinitionImmediate(DecisionDefinition definition)
    {
        var controller = LevelController.Instance;
        if (controller == null || definition == null)
            return false;

        if (!CanAfford(controller, definition))
            return false;

        SpendResources(controller, definition);
        ApplyRewards(controller, definition);
        return true;
    }

    bool TryApplyDefinitionOnRegion(DecisionDefinition definition, Region region)
    {
        var controller = LevelController.Instance;
        if (controller == null || definition == null || region == null)
            return false;

        if (!CanAfford(controller, definition))
            return false;

        SpendResources(controller, definition);
        ApplyRewards(controller, definition);

        if (definition.influenceDelta != 0)   region.ChangeInfluence(definition.influenceDelta);
        if (definition.stabilityDelta != 0)   region.ChangeStability(definition.stabilityDelta);
        if (definition.developmentDelta != 0) region.ChangeDevelopment(definition.developmentDelta);

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateRegionInfo(region);

        return true;
    }

    Region GetCurrentRegion()
    {
        var controller = LevelController.Instance;
        if (controller == null)
            return null;

        return controller.SelectedRegion;
    }

    public bool IsOnCooldown(DecisionDefinition definition, Region region)
    {
        if (definition == null || definition.cooldownSeconds <= 0f)
            return false;

        float now = Time.time;
        string key = GetDecisionKey(definition);

        if (AffectsRegion(definition))
        {
            if (region == null)
                return false;

            if (regionalCooldowns.TryGetValue(key, out var perRegion) &&
                perRegion.TryGetValue(region, out var readyAt))
            {
                if (now >= readyAt)
                {
                    perRegion.Remove(region);
                    return false;
                }
                return true;
            }
            return false;
        }

        if (globalCooldowns.TryGetValue(key, out var globalReadyAt))
        {
            if (now >= globalReadyAt)
            {
                globalCooldowns.Remove(key);
                return false;
            }
            return true;
        }

        return false;
    }

    public float GetRemainingCooldown(DecisionDefinition definition, Region region)
    {
        if (definition == null || definition.cooldownSeconds <= 0f)
            return 0f;

        float now = Time.time;
        string key = GetDecisionKey(definition);

        if (AffectsRegion(definition))
        {
            if (region == null)
                return 0f;

            if (regionalCooldowns.TryGetValue(key, out var perRegion) &&
                perRegion.TryGetValue(region, out var readyAt))
                return Mathf.Max(0f, readyAt - now);

            return 0f;
        }

        if (globalCooldowns.TryGetValue(key, out var globalReadyAt))
            return Mathf.Max(0f, globalReadyAt - now);

        return 0f;
    }

    public List<DecisionCooldownSaveData> CaptureCooldowns()
    {
        List<DecisionCooldownSaveData> data = new List<DecisionCooldownSaveData>();
        float now = Time.time;

        foreach (KeyValuePair<string, float> entry in globalCooldowns)
        {
            float remaining = Mathf.Max(0f, entry.Value - now);
            if (remaining <= 0f)
                continue;

            data.Add(new DecisionCooldownSaveData
            {
                decisionId = entry.Key,
                regionId = string.Empty,
                remainingSeconds = remaining
            });
        }

        foreach (KeyValuePair<string, Dictionary<Region, float>> decisionEntry in regionalCooldowns)
        {
            foreach (KeyValuePair<Region, float> regionEntry in decisionEntry.Value)
            {
                if (regionEntry.Key == null)
                    continue;

                float remaining = Mathf.Max(0f, regionEntry.Value - now);
                if (remaining <= 0f)
                    continue;

                data.Add(new DecisionCooldownSaveData
                {
                    decisionId = decisionEntry.Key,
                    regionId = regionEntry.Key.Id,
                    remainingSeconds = remaining
                });
            }
        }

        return data;
    }

    public void RestoreCooldowns(List<DecisionCooldownSaveData> cooldowns, Dictionary<string, Region> regionsById)
    {
        globalCooldowns.Clear();
        regionalCooldowns.Clear();

        if (cooldowns == null)
            return;

        float now = Time.time;
        for (int i = 0; i < cooldowns.Count; i++)
        {
            DecisionCooldownSaveData saved = cooldowns[i];
            if (saved == null || string.IsNullOrEmpty(saved.decisionId) || saved.remainingSeconds <= 0f)
                continue;

            float readyAt = now + saved.remainingSeconds;
            if (string.IsNullOrEmpty(saved.regionId))
            {
                globalCooldowns[saved.decisionId] = readyAt;
                continue;
            }

            if (regionsById == null || !regionsById.TryGetValue(saved.regionId, out Region region) || region == null)
                continue;

            if (!regionalCooldowns.TryGetValue(saved.decisionId, out Dictionary<Region, float> perRegion))
            {
                perRegion = new Dictionary<Region, float>();
                regionalCooldowns[saved.decisionId] = perRegion;
            }

            perRegion[region] = readyAt;
        }
    }

    void BeginCooldown(DecisionDefinition definition, Region region)
    {
        if (definition == null || definition.cooldownSeconds <= 0f)
            return;

        float readyAt = Time.time + Mathf.Max(0f, definition.cooldownSeconds);
        string key = GetDecisionKey(definition);

        if (AffectsRegion(definition))
        {
            if (region == null)
                return;

            if (!regionalCooldowns.TryGetValue(key, out var perRegion))
            {
                perRegion = new Dictionary<Region, float>();
                regionalCooldowns[key] = perRegion;
            }
            perRegion[region] = readyAt;
            return;
        }

        globalCooldowns[key] = readyAt;
    }

    string GetDecisionKey(DecisionDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        if (!string.IsNullOrEmpty(definition.id))
            return definition.id;

        if (!string.IsNullOrEmpty(definition.displayName))
            return definition.displayName;

        return "definition_" + definition.GetHashCode();
    }

    void LogDecisionApplied(DecisionDefinition definition, Region region)
    {
        if (definition == null)
            return;

        string target = region != null ? $" on {region.Name}" : string.Empty;
        PlayerLog.Add($"Decision used: {definition.displayName}{target}");
    }

    public static bool AffectsRegion(DecisionDefinition definition)
    {
        return definition != null &&
               (definition.influenceDelta != 0 ||
                definition.stabilityDelta != 0 ||
                definition.developmentDelta != 0);
    }

    bool CanAfford(LevelController controller, DecisionDefinition definition)
    {
        if (controller == null || definition == null)
            return false;

        return controller.Sanity   >= GetEffectiveCost(definition.sanityCost) &&
               controller.Money    >= GetEffectiveCost(definition.moneyCost) &&
               controller.Artifacts >= GetEffectiveCost(definition.artifactsCost);
    }

    void SpendResources(LevelController controller, DecisionDefinition definition)
    {
        if (definition.sanityCost != 0)
            controller.ChangeSanity(-GetEffectiveCost(definition.sanityCost));
        if (definition.moneyCost != 0)
            controller.ChangeMoney(-GetEffectiveCost(definition.moneyCost));
        if (definition.artifactsCost != 0)
            controller.ChangeArtifacts(-GetEffectiveCost(definition.artifactsCost));
    }

    void ApplyRewards(LevelController controller, DecisionDefinition definition)
    {
        if (definition.sanityGain != 0)
            controller.ChangeSanity(definition.sanityGain);
        if (definition.moneyGain != 0)
            controller.ChangeMoney(definition.moneyGain);
        if (definition.artifactsGain != 0)
            controller.ChangeArtifacts(definition.artifactsGain);
    }

    public static int GetEffectiveCost(int baseCost)
    {
        if (baseCost <= 0)
            return 0;

        float multiplier = GameSession.ActiveDifficulty.decisionCostMultiplier;
        return Mathf.Max(1, Mathf.CeilToInt(baseCost * multiplier));
    }
}
