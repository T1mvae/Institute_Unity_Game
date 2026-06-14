using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Institute.World.Gameplay
{
    /// <summary>
    /// Decision logic on the new model. Decisions read/modify <see cref="RegionData"/> (never the
    /// legacy Region). Costs/rewards go through <see cref="GameResources"/>. Reuses the data class
    /// <c>DecisionDefinition</c> and the region-free static helpers on the legacy
    /// DecisionSelectionManager (GetEffectiveCost / AffectsRegion) so cost math stays consistent.
    /// </summary>
    public class RegionDecisionSystem : MonoBehaviour
    {
        public static RegionDecisionSystem Instance { get; private set; }

        public readonly List<DecisionDefinition> decisions = new List<DecisionDefinition>();

        // Cooldowns keyed by "<decisionId>|<regionId>" (regionId empty for global decisions).
        readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            LoadDecisions();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void LoadDecisions()
        {
            decisions.Clear();
            // Reuse a DecisionPool if one is already in the scene; otherwise load the JSON ourselves.
            if (DecisionPool.Instance != null && DecisionPool.Instance.decisions != null && DecisionPool.Instance.decisions.Count > 0)
            {
                decisions.AddRange(DecisionPool.Instance.decisions);
                return;
            }

            string path = Path.Combine(Application.streamingAssetsPath, "decisions.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("RegionDecisionSystem: decisions.json not found at " + path);
                return;
            }
            try
            {
                string json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<DecisionListWrapper>("{\"decisions\":" + json + "}");
                if (wrapper != null && wrapper.decisions != null) decisions.AddRange(wrapper.decisions);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("RegionDecisionSystem: failed to load decisions - " + ex.Message);
            }
        }

        [System.Serializable]
        class DecisionListWrapper { public List<DecisionDefinition> decisions; }

        public IReadOnlyList<DecisionDefinition> AllDecisions => decisions;

        public static string TargetOf(DecisionDefinition d) => string.IsNullOrEmpty(d.targetType) ? "Region" : d.targetType;
        public static bool IsSelf(DecisionDefinition d) => d.isShadowInstrument || TargetOf(d).Equals("Self", System.StringComparison.OrdinalIgnoreCase);
        public static bool IsState(DecisionDefinition d) => !IsSelf(d) && TargetOf(d).Equals("State", System.StringComparison.OrdinalIgnoreCase);

        static WorldMapData Map => WorldController.Instance != null ? WorldController.Instance.Map : null;

        /// <summary>
        /// Decisions valid for the current selection: Self/Shadow are always offered; State decisions
        /// need a region that belongs to a state; Region decisions need a region (unless non-regional).
        /// </summary>
        public List<DecisionDefinition> GetDecisionsFor(RegionData region)
        {
            var list = new List<DecisionDefinition>();
            foreach (var d in decisions)
            {
                if (d == null) continue;
                if (IsSelf(d)) { list.Add(d); continue; }
                if (IsState(d)) { if (region != null && !string.IsNullOrEmpty(region.stateId)) list.Add(d); continue; }
                if (region == null && DecisionSelectionManager.AffectsRegion(d)) continue; // region decision needs a region
                list.Add(d);
            }
            return list;
        }

        public bool CanApplyDecision(DecisionDefinition decision, RegionData region, out string reason)
        {
            reason = null;
            if (decision == null) { reason = "No decision."; return false; }

            if (IsState(decision))
            {
                StateData state = region != null && Map != null ? Map.GetState(region.stateId) : null;
                if (state == null) { reason = "Select a region inside a state."; return false; }
                if (decision.minStateStability > 0 && state.stability < decision.minStateStability)
                { reason = $"Needs state stability ≥ {decision.minStateStability}."; return false; }
            }
            else if (!IsSelf(decision) && DecisionSelectionManager.AffectsRegion(decision) && region == null)
            {
                reason = "Select a region first."; return false;
            }

            float remaining = GetRemainingCooldown(decision, region);
            if (remaining > 0f) { reason = $"On cooldown ({remaining:0}s)."; return false; }

            int s = DecisionSelectionManager.GetEffectiveCost(decision.sanityCost);
            int m = DecisionSelectionManager.GetEffectiveCost(decision.moneyCost);
            int a = DecisionSelectionManager.GetEffectiveCost(decision.artifactsCost);
            if (!GameResources.CanAfford(s, m, a)) { reason = "Not enough resources."; return false; }

            return true;
        }

        public string GetDisabledReason(DecisionDefinition decision, RegionData region)
        {
            return CanApplyDecision(decision, region, out string reason) ? null : reason;
        }

        /// <summary>Applies a decision (Region/State/Self) + resources + exposure, starts cooldown, logs.</summary>
        public bool ApplyDecision(DecisionDefinition decision, RegionData region)
        {
            if (!CanApplyDecision(decision, region, out _)) return false;

            int s = DecisionSelectionManager.GetEffectiveCost(decision.sanityCost);
            int m = DecisionSelectionManager.GetEffectiveCost(decision.moneyCost);
            int a = DecisionSelectionManager.GetEffectiveCost(decision.artifactsCost);
            if (!GameResources.TrySpend(s, m, a)) return false;
            GameResources.Change(decision.sanityGain, decision.moneyGain, decision.artifactsGain);

            WorldMapData map = Map;
            if (IsState(decision) && region != null && map != null)
                ApplyStateDeltas(decision, map.GetState(region.stateId), map);

            // Region-level deltas land on the selected region for Region, State and Self/Shadow decisions.
            ApplyRegionDeltas(decision, region, map);

            // Building a structure plants a permanent marker modifier that unlocks daily income there.
            if (!string.IsNullOrEmpty(decision.structureName) && region != null && !HasStructure(region, decision.structureName))
            {
                region.modifiers.Add(new RegionModifierState(decision.structureName, 0, 0, 0, 0f));
                PlayerLog.Add($"Built {decision.structureName} in {region.displayName}.");
            }

            // Shadow instruments / risky actions raise Exposure / Suspicion.
            if (decision.exposureRisk != 0 && EconomySystem.Instance != null)
                EconomySystem.Instance.AddExposure(decision.exposureRisk);

            BeginCooldown(decision, region);
            string label = IsSelf(decision) ? "Shadow instrument" : "Decision";
            string target = region != null ? $" on {region.displayName}" : string.Empty;
            string expo = decision.exposureRisk != 0 ? $" (exposure +{decision.exposureRisk})" : string.Empty;
            PlayerLog.Add($"{label} used: {decision.displayName}{target}{expo}");
            return true;
        }

        void ApplyStateDeltas(DecisionDefinition d, StateData state, WorldMapData map)
        {
            if (state == null) return;
            state.stability = Mathf.Clamp(state.stability + d.stateStabilityDelta, 0, 20);
            state.influence = Mathf.Clamp(state.influence + d.stateInfluenceDelta, 0, 20);
            state.development = Mathf.Clamp(state.development + d.stateDevelopmentDelta, 0, 20);

            // A portion of a state-level change propagates to every member region.
            foreach (string rid in state.regionIds)
            {
                RegionData r = map.GetRegion(rid);
                if (r == null) continue;
                r.stability += d.stateStabilityDelta / 2;
                r.influence += d.stateInfluenceDelta / 2;
                r.development += d.stateDevelopmentDelta / 2;
                r.ClampStats();
            }
            WorldStateUtil.RecomputeStats(map, state);
        }

        static bool HasStructure(RegionData region, string structureName)
        {
            foreach (var m in region.modifiers)
                if (m.name == structureName) return true;
            return false;
        }

        void ApplyRegionDeltas(DecisionDefinition d, RegionData region, WorldMapData map)
        {
            if (region == null) return;
            if (d.influenceDelta == 0 && d.stabilityDelta == 0 && d.developmentDelta == 0) return;
            region.influence += d.influenceDelta;
            region.stability += d.stabilityDelta;
            region.development += d.developmentDelta;
            region.ClampStats();
            if (map != null) WorldStateUtil.RecomputeStats(map, map.GetState(region.stateId));
            WorldController.Instance?.RaiseRegionDataChanged(region);
        }

        // ---------- cooldowns ----------
        static string Key(DecisionDefinition d, RegionData region)
        {
            string id = !string.IsNullOrEmpty(d.id) ? d.id : d.displayName;
            string scope;
            if (IsState(d)) scope = region != null ? (region.stateId ?? "") : "";
            else if (IsSelf(d)) scope = "";  // global cooldown for shadow / self instruments
            else scope = DecisionSelectionManager.AffectsRegion(d) && region != null ? region.regionId : "";
            return id + "|" + scope;
        }

        public float GetRemainingCooldown(DecisionDefinition decision, RegionData region)
        {
            if (decision == null || decision.cooldownSeconds <= 0f) return 0f;
            if (_readyAt.TryGetValue(Key(decision, region), out float t))
                return Mathf.Max(0f, t - Time.time);
            return 0f;
        }

        void BeginCooldown(DecisionDefinition decision, RegionData region)
        {
            if (decision == null || decision.cooldownSeconds <= 0f) return;
            _readyAt[Key(decision, region)] = Time.time + decision.cooldownSeconds;
        }

        public List<DecisionCooldownSaveData> CaptureCooldowns()
        {
            var data = new List<DecisionCooldownSaveData>();
            float now = Time.time;
            foreach (var kv in _readyAt)
            {
                float remaining = kv.Value - now;
                if (remaining <= 0f) continue;
                int bar = kv.Key.IndexOf('|');
                data.Add(new DecisionCooldownSaveData
                {
                    decisionId = bar >= 0 ? kv.Key.Substring(0, bar) : kv.Key,
                    regionId = bar >= 0 ? kv.Key.Substring(bar + 1) : "",
                    remainingSeconds = remaining,
                });
            }
            return data;
        }

        public void RestoreCooldowns(List<DecisionCooldownSaveData> cooldowns)
        {
            _readyAt.Clear();
            if (cooldowns == null) return;
            float now = Time.time;
            foreach (var c in cooldowns)
            {
                if (c == null || string.IsNullOrEmpty(c.decisionId) || c.remainingSeconds <= 0f) continue;
                _readyAt[c.decisionId + "|" + (c.regionId ?? "")] = now + c.remainingSeconds;
            }
        }
    }
}
