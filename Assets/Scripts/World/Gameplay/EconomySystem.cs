using System.Text;
using UnityEngine;

namespace Institute.World.Gameplay
{
    /// <summary>
    /// Daily-ticking economy + the player-global Exposure/Suspicion meter + win/loss evaluation.
    /// Subscribes to <see cref="TimeManager.OnNewDay"/>. Income scales with each region's development
    /// weighted by Institute influence; Ruined regions can yield rare Artifacts; Sanity drains under
    /// high Exposure / low stability and recovers when traces are cold.
    /// </summary>
    public class EconomySystem : MonoBehaviour
    {
        public static EconomySystem Instance { get; private set; }

        [Header("Tuning")]
        public int baseIncome = 5;
        public float incomeMultiplier = 0.3f;   // ×5 vs the old 0..100 scale (development is now 0..20)
        public float artifactChance = 0.05f;

        public int Exposure { get; private set; }          // 0..100 (Suspicion / traces of action)
        public int LastIncome { get; private set; }
        public int LastArtifacts { get; private set; }
        public int LastSanityDelta { get; private set; }

        public string MoneyTooltip { get; private set; } = "Daily income breakdown.";
        public string ArtifactTooltip { get; private set; } = "Artifacts from Institute presence in ruins.";
        public string SanityTooltip { get; private set; } = "Mental strain from exposure and moral pressure.";

        bool _subscribed;
        bool _endChecked;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay -= OnNewDay;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!_subscribed && TimeManager.Instance != null)
            {
                TimeManager.Instance.OnNewDay += OnNewDay;
                _subscribed = true;
            }
        }

        // ---------- exposure (shadow instruments / events add to this) ----------
        public void AddExposure(int amount)
        {
            if (amount == 0) return;
            Exposure = Mathf.Clamp(Exposure + amount, 0, 100);
        }

        public void SetExposure(int value) => Exposure = Mathf.Clamp(value, 0, 100);

        // ---------- daily tick ----------
        void OnNewDay(int day)
        {
            WorldMapData map = WorldController.Instance != null ? WorldController.Instance.Map : null;
            if (map == null) return;
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

            ApplyDailyCharacterInfluence(map);
            WorldStateUtil.RecomputeAll(map);

            int money = ComputeIncome(map, out string moneyBreakdown);
            int artifacts = ComputeArtifacts(map, out string artifactBreakdown);
            int sanityDelta = ComputeSanity(map, out string sanityBreakdown);

            // Traces fade a little each day.
            Exposure = Mathf.Max(0, Exposure - 1);

            GameResources.Change(sanityDelta, money, artifacts);

            LastIncome = money; LastArtifacts = artifacts; LastSanityDelta = sanityDelta;
            MoneyTooltip = moneyBreakdown;
            ArtifactTooltip = artifactBreakdown;
            SanityTooltip = sanityBreakdown;

            PlayerLog.Add($"Day {day}: +{money} gold" +
                          (artifacts > 0 ? $", +{artifacts} artifact(s)" : "") +
                          $", sanity {(sanityDelta >= 0 ? "+" : "")}{sanityDelta}, exposure {Exposure}.");

            EvaluateEndConditions(map);
        }

        // Income is no longer passive: a region only contributes bonus development income if the
        // player has built a structure / contact network there (Clinic / Guild / School / Network).
        int ComputeIncome(WorldMapData map, out string breakdown)
        {
            float bonus = 0f;
            int builtRegionsCount = 0;
            foreach (var r in map.Regions)
            {
                bool hasStructure = false;
                foreach (var mod in r.modifiers)
                {
                    string n = mod.name;
                    if (n != null && (n.Contains("Clinic") || n.Contains("Guild") || n.Contains("School") || n.Contains("Network")))
                    {
                        hasStructure = true;
                        break;
                    }
                }
                if (hasStructure)
                {
                    bonus += r.development * (r.influence / 20f) * incomeMultiplier;
                    builtRegionsCount++;
                }
            }
            int total = baseIncome + Mathf.RoundToInt(bonus);
            breakdown = $"Daily Income: {total} gold\n  Base: {baseIncome}\n  From built structures ({builtRegionsCount} regions): +{Mathf.RoundToInt(bonus)}\n  (Income generated only from regions with active clinics, guilds, schools, or networks)";
            return total;
        }

        int ComputeArtifacts(WorldMapData map, out string breakdown)
        {
            int gained = 0, eligible = 0;
            foreach (var r in map.Regions)
            {
                if (!RegionYieldsArtifacts(map, r) || r.influence < 10) continue;
                eligible++;
                if (Random.value < artifactChance) gained++;
            }
            breakdown = $"Artifacts this day: {gained}\n  Eligible ruin regions (influence ≥ 10): {eligible}\n  Daily chance each: {(artifactChance * 100f):0}%";
            return gained;
        }

        static bool RegionYieldsArtifacts(WorldMapData map, RegionData region)
        {
            if (region.regionType == RegionType.RuinedZone) return true;
            if (region.tags.Contains("ruins") || region.tags.Contains("artifacts")) return true;
            foreach (int tid in region.tileIds)
            {
                HexTileData t = map.GetTile(tid);
                if (t != null && t.terrainType == TerrainType.Ruins) return true;
            }
            return false;
        }

        int ComputeSanity(WorldMapData map, out string breakdown)
        {
            int globalStab = GlobalStability(map);
            int delta = 0;
            var sb = new StringBuilder("Daily sanity change:\n");

            if (Exposure >= 50)
            {
                int loss = Mathf.CeilToInt((Exposure - 40) / 20f);
                delta -= loss;
                sb.AppendLine($"  Exposure strain (Exp {Exposure}): -{loss}");
            }
            if (globalStab < 30)
            {
                delta -= 1;
                sb.AppendLine($"  Moral trauma (global stability {globalStab}%): -1");
            }
            if (Exposure < 20)
            {
                delta += 1;
                sb.AppendLine("  Cold trail / rest: +1");
            }
            if (delta == 0) sb.AppendLine("  Stable: 0");
            breakdown = sb.ToString().TrimEnd();
            return delta;
        }

        // Loyal/recruited rulers slowly raise Institute influence in their region (and thus their
        // state). High-fear, hostile rulers erode it. Keeps the character layer tied to state influence.
        void ApplyDailyCharacterInfluence(WorldMapData map)
        {
            var cs = RegionCharacterSystem.Instance;
            if (cs == null) return;
            foreach (var c in cs.Characters)
            {
                if (c == null || string.IsNullOrEmpty(c.currentRegionId)) continue;
                RegionData region = map.GetRegion(c.currentRegionId);
                if (region == null) continue;
                int delta = 0;
                if (c.recruitedAsContact || c.loyalty > 60) delta += 1;
                if (c.relationshipWithPlayer < -40 && c.fear < 30) delta -= 1;
                if (delta != 0) { region.influence = Mathf.Clamp(region.influence + delta, 0, 20); }
            }
        }

        // Returns a 0..100 PERCENTAGE. Region stability is 0..20, so the mean is scaled ×5 — this keeps
        // the HUD "{stab}%" readout and the <30 / <15 thresholds meaningful on the new scale.
        public int GlobalStability(WorldMapData map)
        {
            if (map == null || map.RegionCount == 0) return 0;
            long sum = 0;
            foreach (var r in map.Regions) sum += r.stability;
            return (int)(sum * 5 / map.RegionCount);
        }

        public string ExposureTooltip => $"Exposure / Suspicion: {Exposure} / 100\nHigh-tech traces raise this; it fades ~1/day.\nAt 100 the elites purge you (Game Over).";

        // ---------- Part 4: win / loss ----------
        public static bool VictoryReformPassed; // set by a final reform decision in the largest empire

        void EvaluateEndConditions(WorldMapData map)
        {
            if (_endChecked) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.IsGameOver) return;

            // Loss conditions.
            if (GameResources.Sanity <= 0) { EndGame(false, "Sanity collapsed — mental breakdown."); return; }
            if (Exposure >= 100) { EndGame(false, "Exposure hit 100% — the elites purged you."); return; }
            if (GlobalStability(map) < 15) { EndGame(false, "Global stability fell below 15% — the world descends into chaos."); return; }

            // Victory conditions.
            if (VictoryReformPassed) { EndGame(true, "A great reform passed in the capital of the largest empire."); return; }
            if (map.StateCount > 0 && AllStatesDominated(map, 16))   // 16/20 = 80% on the new scale
            {
                EndGame(true, "Achieved 80% Institute influence across every major state.");
                return;
            }
        }

        static bool AllStatesDominated(WorldMapData map, int threshold)
        {
            foreach (var s in map.States)
                if (s.influence < threshold) return false;
            return true;
        }

        void EndGame(bool win, string reason)
        {
            _endChecked = true;
            PlayerLog.Add((win ? "VICTORY: " : "DEFEAT: ") + reason);
            Debug.Log((win ? "[Win] " : "[Loss] ") + reason);
            GameManager.Instance.SetGameOver(win);
        }
    }
}
