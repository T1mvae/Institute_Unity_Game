using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ActionButton : MonoBehaviour
{
    [Header("Visuals")]
    public string actionName = "Action";
    public Text labelText;
    public Text detailText;
    public Text statusText;
    public Image cooldownFill;

    [Header("Decision Pool")]
    public bool assignFromPool;
    public string decisionId;

    [Header("Costs")]
    public int sanityCost;
    public int moneyCost;
    public int artifactsCost;

    [Header("Rewards")]
    public int sanityGain;
    public int moneyGain;
    public int artifactsGain;

    [Header("Region Stat Changes")]
    public int influenceDelta;
    public int stabilityDelta;
    public int developmentDelta;

    [Header("Timing")]
    public float cooldownSeconds = 3f;

    Button btn;
    StyledButton styledButton;
    bool definitionAssigned;
    DecisionDefinition assignedDefinition;

    void Awake()
    {
        btn = GetComponent<Button>();
        styledButton = GetComponent<StyledButton>();
        if (labelText == null)
            labelText = GetComponentInChildren<Text>();
        definitionAssigned = !assignFromPool;
    }

    void OnEnable()
    {
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnClick);
            btn.onClick.AddListener(OnClick);
        }
        TryAssignFromPool();
        UpdateLabel();
        UpdateDetails();
        UpdateInteractable();
    }

    void OnDisable()
    {
        if (btn != null)
            btn.onClick.RemoveListener(OnClick);
    }

    void Update()
    {
        if (assignFromPool && !definitionAssigned)
            TryAssignFromPool();

        UpdateInteractable();
    }

    void TryAssignFromPool()
    {
        if (!assignFromPool || DecisionPool.Instance == null)
            return;

        DecisionDefinition definition = null;

        if (!string.IsNullOrEmpty(decisionId))
            definition = DecisionPool.Instance.GetDecisionById(decisionId);

        if (definition == null)
            definition = DecisionPool.Instance.GetRandomDecision();

        if (definition != null)
            ApplyDefinition(definition);
    }

    public void ApplyDefinition(DecisionDefinition definition)
    {
        if (definition == null)
        {
            definitionAssigned = false;
            return;
        }

        assignedDefinition = definition;

        actionName = definition.displayName;
        sanityCost = definition.sanityCost;
        moneyCost = definition.moneyCost;
        artifactsCost = definition.artifactsCost;
        sanityGain = definition.sanityGain;
        moneyGain = definition.moneyGain;
        artifactsGain = definition.artifactsGain;
        influenceDelta = definition.influenceDelta;
        stabilityDelta = definition.stabilityDelta;
        developmentDelta = definition.developmentDelta;
        cooldownSeconds = definition.cooldownSeconds;
        decisionId = definition.id;
        definitionAssigned = true;

        UpdateLabel();
        UpdateDetails();

        // Привязываем тултип
        var tt = GetComponent<TooltipTrigger>();
        if (tt != null)
        {
            tt.message = BuildTooltip(definition);
        }
    }

    void OnClick()
    {
        if (DecisionSelectionManager.Instance == null)
            return;

        DecisionDefinition definition = BuildCurrentDefinition();
        if (definition == null)
            return;

        DecisionSelectionManager.Instance.SelectDecision(definition, this);
    }

    DecisionDefinition BuildCurrentDefinition()
    {
        // Always build a fresh snapshot so the applied decision matches the visible button state.
        return new DecisionDefinition
        {
            id = !string.IsNullOrEmpty(decisionId) ? decisionId : actionName,
            displayName = actionName,
            sanityCost = sanityCost,
            moneyCost = moneyCost,
            artifactsCost = artifactsCost,
            sanityGain = sanityGain,
            moneyGain = moneyGain,
            artifactsGain = artifactsGain,
            influenceDelta = influenceDelta,
            stabilityDelta = stabilityDelta,
            developmentDelta = developmentDelta,
            cooldownSeconds = cooldownSeconds
        };
    }

    // мгновенное применение (без региона)
    public bool ExecuteAction()
    {
        var controller = LevelController.Instance;
        if (controller == null)
            return false;

        if (!HasResources(controller))
            return false;

        SpendResources(controller);
        ApplyRewards(controller);
        return true;
    }

    // применение к региону
    public bool ExecuteActionOnRegion(Region region)
    {
        var controller = LevelController.Instance;
        if (controller == null || region == null) return false;

        if (!HasResources(controller)) return false;

        SpendResources(controller);
        ApplyRewards(controller);

        if (influenceDelta != 0)   region.ChangeInfluence(influenceDelta);
        if (stabilityDelta != 0)   region.ChangeStability(stabilityDelta);
        if (developmentDelta != 0) region.ChangeDevelopment(developmentDelta);

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateRegionInfo(region);

        return true;
    }

    bool HasResources(LevelController controller)
    {
        return controller.Sanity >= DecisionSelectionManager.GetEffectiveCost(sanityCost) &&
               controller.Money >= DecisionSelectionManager.GetEffectiveCost(moneyCost) &&
               controller.Artifacts >= DecisionSelectionManager.GetEffectiveCost(artifactsCost);
    }

    void SpendResources(LevelController controller)
    {
        if (sanityCost != 0)
            controller.ChangeSanity(-DecisionSelectionManager.GetEffectiveCost(sanityCost));
        if (moneyCost != 0)
            controller.ChangeMoney(-DecisionSelectionManager.GetEffectiveCost(moneyCost));
        if (artifactsCost != 0)
            controller.ChangeArtifacts(-DecisionSelectionManager.GetEffectiveCost(artifactsCost));
    }

    void ApplyRewards(LevelController controller)
    {
        if (sanityGain != 0)
            controller.ChangeSanity(sanityGain);
        if (moneyGain != 0)
            controller.ChangeMoney(moneyGain);
        if (artifactsGain != 0)
            controller.ChangeArtifacts(artifactsGain);
    }

    void UpdateLabel()
    {
        if (labelText != null)
            labelText.text = actionName;
    }

    void UpdateDetails()
    {
        if (detailText == null)
            return;

        DecisionDefinition definition = assignedDefinition ?? BuildCurrentDefinition();
        detailText.text = BuildCompactDetails(definition);
    }

    void UpdateInteractable()
    {
        if (btn == null)
            return;

        DecisionDefinition definition = assignedDefinition ?? BuildCurrentDefinition();
        bool hasDefinition = definition != null && (!assignFromPool || definitionAssigned);
        bool requiresRegion = RequiresRegion();
        Region selectedRegion = null;

        bool canUse = hasDefinition && LevelController.Instance != null;
        string status = string.Empty;

        if (requiresRegion)
        {
            selectedRegion = LevelController.Instance != null ? LevelController.Instance.SelectedRegion : null;
            if (selectedRegion == null)
            {
                canUse = false;
                status = "Select a region";
            }
        }

        var selectionManager = DecisionSelectionManager.Instance;
        if (canUse && selectionManager != null && definition != null)
        {
            float remaining = selectionManager.GetRemainingCooldown(definition, selectedRegion);
            if (remaining > 0f)
            {
                canUse = false;
                status = $"Cooldown {Mathf.CeilToInt(remaining)}s";
            }
        }

        if (canUse && LevelController.Instance != null && !HasResources(LevelController.Instance))
        {
            canUse = false;
            status = "Insufficient resources";
        }

        btn.interactable = canUse;
        if (styledButton != null)
            styledButton.SetDisabledVisual(!canUse);

        UpdateCooldownFill(definition, selectedRegion);

        if (statusText != null)
            statusText.text = canUse ? "Ready" : status;
    }

    bool RequiresRegion()
    {
        if (assignedDefinition != null)
            return DecisionSelectionManager.AffectsRegion(assignedDefinition);

        return influenceDelta != 0 || stabilityDelta != 0 || developmentDelta != 0;
    }

    void UpdateCooldownFill(DecisionDefinition definition, Region selectedRegion)
    {
        if (cooldownFill == null)
            return;

        float fill = 0f;
        if (definition != null && definition.cooldownSeconds > 0f && DecisionSelectionManager.Instance != null)
        {
            float remaining = DecisionSelectionManager.Instance.GetRemainingCooldown(definition, selectedRegion);
            fill = remaining / Mathf.Max(0.001f, definition.cooldownSeconds);
        }

        cooldownFill.gameObject.SetActive(fill > 0f);
        cooldownFill.fillAmount = Mathf.Clamp01(fill);
    }

    string BuildTooltip(DecisionDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        string cooldownText = definition.cooldownSeconds > 0f
            ? DecisionSelectionManager.AffectsRegion(definition)
                ? $"Cooldown: {definition.cooldownSeconds:0.#}s per region"
                : $"Cooldown: {definition.cooldownSeconds:0.#}s"
            : "Cooldown: none";

        return $"{definition.displayName}\n\n{BuildCostLine(definition)}\n{BuildRewardLine(definition)}\n{BuildEffectLine(definition)}\n{cooldownText}";
    }

    string BuildCompactDetails(DecisionDefinition definition)
    {
        if (definition == null)
            return "No decision data.";

        List<string> lines = new List<string>();
        string costs = BuildCostLine(definition);
        if (!string.IsNullOrEmpty(costs))
            lines.Add(costs);
        string effects = BuildEffectLine(definition);
        if (!string.IsNullOrEmpty(effects))
            lines.Add(effects);
        if (definition.cooldownSeconds > 0f)
            lines.Add($"{definition.cooldownSeconds:0.#}s cooldown");
        return string.Join("\n", lines);
    }

    string BuildCostLine(DecisionDefinition definition)
    {
        List<string> costs = new List<string>();
        if (definition.sanityCost > 0)
            costs.Add($"Sanity -{DecisionSelectionManager.GetEffectiveCost(definition.sanityCost)}");
        if (definition.moneyCost > 0)
            costs.Add($"Money -{DecisionSelectionManager.GetEffectiveCost(definition.moneyCost)}");
        if (definition.artifactsCost > 0)
            costs.Add($"Artifacts -{DecisionSelectionManager.GetEffectiveCost(definition.artifactsCost)}");
        return costs.Count > 0 ? "Cost: " + string.Join(", ", costs) : "Cost: none";
    }

    string BuildRewardLine(DecisionDefinition definition)
    {
        List<string> rewards = new List<string>();
        if (definition.sanityGain > 0)
            rewards.Add($"Sanity +{definition.sanityGain}");
        if (definition.moneyGain > 0)
            rewards.Add($"Money +{definition.moneyGain}");
        if (definition.artifactsGain > 0)
            rewards.Add($"Artifacts +{definition.artifactsGain}");
        return rewards.Count > 0 ? "Gain: " + string.Join(", ", rewards) : "Gain: none";
    }

    string BuildEffectLine(DecisionDefinition definition)
    {
        List<string> effects = new List<string>();
        AddDelta(effects, "Influence", definition.influenceDelta);
        AddDelta(effects, "Stability", definition.stabilityDelta);
        AddDelta(effects, "Development", definition.developmentDelta);
        return effects.Count > 0 ? "Effect: " + string.Join(", ", effects) : "Effect: none";
    }

    void AddDelta(List<string> parts, string label, int delta)
    {
        if (delta == 0)
            return;

        string sign = delta > 0 ? "+" : string.Empty;
        parts.Add($"{label} {sign}{delta}");
    }
}
