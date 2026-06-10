using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class DecisionDefinition
{
    public string id;
    public string displayName;

    public int sanityCost;
    public int moneyCost;
    public int artifactsCost;

    public int sanityGain;
    public int moneyGain;
    public int artifactsGain;

    public int influenceDelta;
    public int stabilityDelta;
    public int developmentDelta;

    public float cooldownSeconds = 3f;

    // --- Hard to Be a God extensions (consumed by Institute.World.Gameplay.RegionDecisionSystem) ---
    /// <summary>"Region" (default), "State", or "Self" (Shadow Instrument).</summary>
    public string targetType = "Region";
    /// <summary>Instant high-tech micro-decision (spy drone / covert toxin): low cost, high exposure.</summary>
    public bool isShadowInstrument;
    /// <summary>Adds to the player-global Exposure / Suspicion meter when used.</summary>
    public int exposureRisk;
    /// <summary>State-level requirement: the owning state must have at least this stability.</summary>
    public int minStateStability;
    /// <summary>State-level deltas (a portion also propagates to member regions).</summary>
    public int stateStabilityDelta;
    public int stateInfluenceDelta;
    public int stateDevelopmentDelta;

    /// <summary>
    /// If set (e.g. "Clinic", "Guild", "School", "Network"), applying this decision plants a permanent
    /// structure modifier of that name on the target region. Regions with a structure are the only ones
    /// that generate bonus daily income (see EconomySystem.ComputeIncome).
    /// </summary>
    public string structureName = "";
}

public class DecisionPool : MonoBehaviour
{
    public static DecisionPool Instance { get; private set; }

    [Tooltip("Predefined decision templates that ActionButtons can use.")]
    public List<DecisionDefinition> decisions = new List<DecisionDefinition>();
    public string decisionsFileName = "decisions.json";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadDecisions();
    }

    public DecisionDefinition GetDecisionById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < decisions.Count; i++)
        {
            if (decisions[i] != null && decisions[i].id == id)
                return decisions[i];
        }
        return null;
    }

    public DecisionDefinition GetRandomDecision()
    {
        if (decisions == null || decisions.Count == 0)
            return null;

        int index = Random.Range(0, decisions.Count);
        return decisions[index];
    }

    void LoadDecisions()
    {
        string path = Path.Combine(Application.streamingAssetsPath, decisionsFileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"DecisionPool: file not found at {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            DecisionCollection collection = JsonUtility.FromJson<DecisionCollection>(WrapJson(json));
            if (collection != null && collection.decisions != null)
            {
                decisions = collection.decisions;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("DecisionPool: failed to load decisions - " + ex.Message);
        }
    }

    string WrapJson(string rawArray)
    {
        return "{\"decisions\":" + rawArray + "}";
    }

    [System.Serializable]
    class DecisionCollection
    {
        public List<DecisionDefinition> decisions;
    }
}
