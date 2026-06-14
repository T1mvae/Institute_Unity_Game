using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Institute.World.Gameplay
{
    /// <summary>A choice shown in the event popup. <see cref="Apply"/> performs all effects.</summary>
    public class EventChoice
    {
        public string label;
        public string detail;
        public bool enabled = true;
        public Action Apply;
    }

    /// <summary>Everything the event popup needs to render one event.</summary>
    public class EventPresentation
    {
        public string title = "Institute Event";
        public string body = "";
        public string scope = "Local";
        public string regionName = "";
        public string characterName = "";
        public readonly List<EventChoice> choices = new List<EventChoice>();
    }

    /// <summary>
    /// Schedules and resolves events against the new model. Local events target a
    /// <see cref="RegionData"/>, personal events a <c>GameCharacter</c> (+ its region), global events
    /// all regions. Reuses the <c>GameEvent</c>/<c>EventOption</c> data classes; applies effects to
    /// RegionData / resources / characters. Raises <see cref="EventReady"/> for the UI-Toolkit popup.
    /// </summary>
    public class RegionEventSystem : MonoBehaviour
    {
        public static RegionEventSystem Instance { get; private set; }

        public event Action<EventPresentation> EventReady;

        readonly List<GameEvent> _events = new List<GameEvent>();
        bool _active;

        float _personalInterval = 30f, _localInterval = 60f, _globalInterval = 120f, _stateInterval = 90f;
        float _personalChance = 1f, _localChance = 0.5f, _globalChance = 0.25f, _stateChance = 0.3f;
        float _nextPersonal, _nextLocal, _nextGlobal, _nextState;
        float _antonInterval = 75f, _nextAnton;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            LoadEvents();
            ApplyDifficultyPacing();
            float now = Time.time;
            _nextPersonal = now + _personalInterval;
            _nextLocal = now + _localInterval;
            _nextGlobal = now + _globalInterval;
            _nextState = now + _stateInterval;
            _nextAnton = now + _antonInterval;
        }

        void ApplyDifficultyPacing()
        {
            try
            {
                DifficultyConfig d = GameSession.ActiveDifficulty;
                float f = Mathf.Max(0.1f, d.eventFrequencyMultiplier);
                _personalInterval = Mathf.Max(5f, _personalInterval / f);
                _localInterval = Mathf.Max(5f, _localInterval / f);
                _globalInterval = Mathf.Max(10f, _globalInterval / f);
            }
            catch { /* standalone */ }
        }

        bool IsBusy =>
            _active ||
            (GameManager.Instance != null && GameManager.Instance.IsPaused) ||
            Time.timeScale == 0f ||
            WorldController.Instance == null || WorldController.Instance.Map == null;

        void Update()
        {
            if (IsBusy) return;
            float now = Time.time;

            // Mentor check-in (Anton) appears during strain (low sanity).
            if (now >= _nextAnton)
            {
                _nextAnton = now + _antonInterval;
                if (GameResources.Sanity < 70) { PresentAnton(); return; }
            }

            if (TrySchedule(EventScope.Personal, ref _nextPersonal, _personalInterval, _personalChance, now)) return;
            if (TrySchedule(EventScope.Local, ref _nextLocal, _localInterval, _localChance, now)) return;
            if (TrySchedule(EventScope.State, ref _nextState, _stateInterval, _stateChance, now)) return;
            TrySchedule(EventScope.Global, ref _nextGlobal, _globalInterval, _globalChance, now);
        }

        bool TrySchedule(EventScope scope, ref float nextTime, float interval, float chance, float now)
        {
            if (now < nextTime) return false;
            nextTime = now + interval;
            if (chance < 1f && Random.value > chance) return false;
            return Trigger(scope);
        }

        /// <summary>Public entry to force an event of a scope (used by debug tools / "trigger now").</summary>
        public bool Trigger(EventScope scope)
        {
            if (_active || _events.Count == 0) return false;
            WorldMapData map = WorldController.Instance != null ? WorldController.Instance.Map : null;
            if (map == null) return false;

            var matching = new List<GameEvent>();
            foreach (var e in _events)
                if (e != null && e.scope == scope) matching.Add(e);
            // State scope reuses Local-flavored events (applied kingdom-wide) when no State events exist.
            if (matching.Count == 0 && scope == EventScope.State)
                foreach (var e in _events)
                    if (e != null && e.scope == EventScope.Local) matching.Add(e);
            if (matching.Count == 0) return false;

            GameEvent evt = matching[Random.Range(0, matching.Count)];

            GameCharacter character = null;
            RegionData region = null;

            if (scope == EventScope.Personal)
            {
                character = PickCharacter(evt);
                if (character == null) return false;
                region = map.GetRegion(character.currentRegionId);
            }
            else if (scope == EventScope.Local)
            {
                region = PickRegion(map);
                if (region == null) return false;
            }
            else if (scope == EventScope.State)
            {
                StateData st = PickState(map);
                if (st == null) return false;
                region = map.GetRegion(st.capitalRegionId);
                if (region == null) return false;
            }

            Present(evt, region, character, scope);
            return true;
        }

        StateData PickState(WorldMapData map)
        {
            var states = new List<StateData>(map.statesById.Values);
            return states.Count > 0 ? states[Random.Range(0, states.Count)] : null;
        }

        RegionData PickRegion(WorldMapData map)
        {
            var regions = new List<RegionData>(map.regionsById.Values);
            if (regions.Count == 0) return null;
            return regions[Random.Range(0, regions.Count)];
        }

        GameCharacter PickCharacter(GameEvent evt)
        {
            var cs = RegionCharacterSystem.Instance;
            if (cs == null) return null;
            var matching = new List<GameCharacter>();
            foreach (var c in cs.Characters)
            {
                if (c == null || !c.IsAvailable) continue;
                if (!string.IsNullOrEmpty(evt.targetCharacterRole) && c.role.ToString() != evt.targetCharacterRole) continue;
                if (evt.minCharacterTrust > 0 && c.trust < evt.minCharacterTrust) continue;
                if (evt.maxCharacterTrust > 0 && c.trust > evt.maxCharacterTrust) continue;
                if (evt.minCharacterCorruption > 0 && c.corruption < evt.minCharacterCorruption) continue;
                if (evt.requiresRecruitedContact && !c.recruitedAsContact) continue;
                matching.Add(c);
            }
            return matching.Count > 0 ? matching[Random.Range(0, matching.Count)] : null;
        }

        void Present(GameEvent evt, RegionData region, GameCharacter character, EventScope applyScope)
        {
            _active = true;
            if (GameManager.Instance != null) GameManager.Instance.SetEventActive(true);

            WorldMapData map = WorldController.Instance != null ? WorldController.Instance.Map : null;
            StateData state = applyScope == EventScope.State && region != null && map != null ? map.GetState(region.stateId) : null;

            var p = new EventPresentation
            {
                title = evt.isBadEvent ? "Crisis" : "Institute Event",
                scope = applyScope.ToString(),
                regionName = state != null ? state.displayName : (region != null ? region.displayName : ""),
                characterName = character != null ? character.displayName : "",
                body = evt.description ?? "",
            };

            if (evt.options != null)
            {
                foreach (var opt in evt.options)
                {
                    if (opt == null) continue;
                    EventOption captured = opt;
                    bool enabled = MeetsRequirements(captured, region);
                    p.choices.Add(new EventChoice
                    {
                        label = captured.text ?? "Option",
                        detail = BuildDetail(captured),
                        enabled = enabled,
                        Apply = () => Resolve(evt, captured, region, character, applyScope),
                    });
                }
            }
            if (p.choices.Count == 0)
                p.choices.Add(new EventChoice { label = "Acknowledge", enabled = true, Apply = () => Resolve(evt, null, region, character, applyScope) });

            PlayerLog.Add($"New {applyScope} event: {evt.description}");

            if (EventReady != null) EventReady.Invoke(p);
            else
            {
                // No popup wired: auto-resolve the first enabled choice so we never deadlock.
                foreach (var c in p.choices) { if (c.enabled) { c.Apply(); return; } }
                Resolve(evt, null, region, character, applyScope);
            }
        }

        bool MeetsRequirements(EventOption opt, RegionData region)
        {
            if (!GameResources.CanAfford(opt.sanityRequired, opt.moneyRequired, opt.artifactsRequired)) return false;
            if (opt.minInfluenceRequired > 0 && (region == null || region.influence < opt.minInfluenceRequired)) return false;
            if (opt.minDevelopmentRequired > 0 && (region == null || region.development < opt.minDevelopmentRequired)) return false;
            return true;
        }

        string BuildDetail(EventOption opt)
        {
            var costs = new List<string>();
            if (opt.sanityRequired > 0) costs.Add(opt.sanityRequired + " Sanity");
            if (opt.moneyRequired > 0) costs.Add(opt.moneyRequired + " Money");
            if (opt.artifactsRequired > 0) costs.Add(opt.artifactsRequired + " Artifacts");
            if (opt.minInfluenceRequired > 0) costs.Add("Req Influence " + opt.minInfluenceRequired);
            if (opt.minDevelopmentRequired > 0) costs.Add("Req Development " + opt.minDevelopmentRequired);

            var fx = new List<string>();
            void Add(string n, int v) { if (v != 0) fx.Add($"{n} {(v > 0 ? "+" : "")}{v}"); }
            Add("Influence", opt.influenceChange); Add("Stability", opt.stabilityChange); Add("Development", opt.developmentChange);
            Add("Sanity", opt.sanityChange); Add("Money", opt.moneyChange); Add("Artifacts", opt.artifactsChange);

            string s = "";
            if (costs.Count > 0) s += "Cost: " + string.Join(", ", costs);
            if (fx.Count > 0) s += (s.Length > 0 ? "  •  " : "") + string.Join(", ", fx);
            return s;
        }

        void Resolve(GameEvent evt, EventOption opt, RegionData region, GameCharacter character, EventScope applyScope)
        {
            _active = false;
            if (GameManager.Instance != null) GameManager.Instance.SetEventActive(false);

            if (opt != null)
            {
                // Pay requirements.
                GameResources.TrySpend(opt.sanityRequired, opt.moneyRequired, opt.artifactsRequired);

                WorldMapData map = WorldController.Instance != null ? WorldController.Instance.Map : null;

                if (applyScope == EventScope.Global && map != null)
                {
                    foreach (var r in map.Regions) ApplyToRegion(opt, r);
                }
                else if (applyScope == EventScope.State && region != null && map != null)
                {
                    // State events affect the entire kingdom.
                    StateData st = map.GetState(region.stateId);
                    if (st != null)
                        foreach (string rid in st.regionIds) ApplyToRegion(opt, map.GetRegion(rid));
                    else ApplyToRegion(opt, region);
                }
                else if (region != null)
                {
                    ApplyToRegion(opt, region);
                }

                GameResources.Change(opt.sanityChange, opt.moneyChange, opt.artifactsChange);
                ApplyCharacter(opt, character);

                if (region != null && map != null) WorldStateUtil.RecomputeStats(map, map.GetState(region.stateId));
                if (region != null) WorldController.Instance?.RaiseRegionDataChanged(region);
                PlayerLog.Add($"Chose \"{opt.text}\" for \"{evt?.description}\".");
            }

            EventResolved?.Invoke();
        }

        // ---------- Anton (mentor) personal events ----------
        static readonly string[] AntonLines =
        {
            "Anton finds you in the dark: \"You came to save them. Look at your hands now.\"",
            "Anton pours wine he didn't offer you: \"Gods don't get tired. You're not a god, are you?\"",
            "\"Every man you spare today,\" Anton says, \"writes the death warrant of ten tomorrow. Still counting?\"",
            "Anton studies you. \"I broke the rules once. The world didn't thank me. Neither will yours.\"",
        };

        void PresentAnton()
        {
            if (_active) return;
            _active = true;
            if (GameManager.Instance != null) GameManager.Instance.SetEventActive(true);

            var p = new EventPresentation
            {
                title = "Anton — Mentor",
                scope = "Personal",
                characterName = "Anton",
                body = AntonLines[Random.Range(0, AntonLines.Length)],
            };
            p.choices.Add(new EventChoice
            {
                label = "Heed his cynical counsel", detail = "Sanity +6, Exposure +4", enabled = true,
                Apply = () => { GameResources.Change(6, 0, 0); EconomySystem.Instance?.AddExposure(4); FinishAnton("You take Anton's pragmatic path."); },
            });
            p.choices.Add(new EventChoice
            {
                label = "Reject his fatalism", detail = "Sanity -4, resolve hardens", enabled = true,
                Apply = () => { GameResources.Change(-4, 0, 0); FinishAnton("You refuse to become like Anton."); },
            });

            PlayerLog.Add("Anton seeks you out.");
            if (EventReady != null) EventReady.Invoke(p);
            else p.choices[0].Apply();
        }

        void FinishAnton(string message)
        {
            _active = false;
            if (GameManager.Instance != null) GameManager.Instance.SetEventActive(false);
            PlayerLog.Add(message);
            EventResolved?.Invoke();
        }

        public event Action EventResolved;

        void ApplyToRegion(EventOption opt, RegionData region)
        {
            if (region == null) return;
            region.influence += opt.influenceChange;
            region.stability += opt.stabilityChange;
            region.development += opt.developmentChange;

            // Translate per-tick modifiers into an immediate equivalent delta (the new model has no
            // tick processor). Recorded on the region for display. TODO: optional over-time ticker.
            if (opt.modifiers != null)
            {
                foreach (var def in opt.modifiers)
                {
                    if (def == null) continue;
                    int ticks = def.tickIntervalSeconds > 0f ? Mathf.Max(1, Mathf.RoundToInt(def.durationSeconds / def.tickIntervalSeconds)) : 1;
                    int mInfl = ScaleModifier(def.influencePerTick * ticks);
                    int mStab = ScaleModifier(def.stabilityPerTick * ticks);
                    int mDev = ScaleModifier(def.developmentPerTick * ticks);
                    region.influence += mInfl;
                    region.stability += mStab;
                    region.development += mDev;
                    region.modifiers.Add(new RegionModifierState(def.name, mInfl, mStab, mDev, 0f));
                }
            }
            region.ClampStats();
        }

        // Modifier per-tick totals were authored for the old 0..100 scale. Compress them to the
        // 0..20 scale (÷5) with a floor of 1 so a non-zero over-time effect never rounds away.
        static int ScaleModifier(int total)
        {
            if (total == 0) return 0;
            int mag = Mathf.Max(1, Mathf.Abs(total) / 5);
            return total > 0 ? mag : -mag;
        }

        void ApplyCharacter(EventOption opt, GameCharacter character)
        {
            if (character == null) return;
            character.trust += opt.trustChange;
            character.loyalty += opt.loyaltyChange;
            character.fear += opt.fearChange;
            character.relationshipWithPlayer += opt.relationshipChange;
            character.corruption += opt.corruptionChange;
            character.influencePower += opt.influencePowerChange;
            character.ClampRuntimeValues();
        }

        // ---------- loading ----------
        void LoadEvents()
        {
            _events.Clear();
            string path = Path.Combine(Application.streamingAssetsPath, "events.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("RegionEventSystem: events.json not found at " + path);
                return;
            }
            try
            {
                string json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<RawEventsWrapper>("{\"events\":" + json + "}");
                if (wrapper?.events == null) return;
                foreach (var raw in wrapper.events)
                {
                    if (raw == null) continue;
                    _events.Add(new GameEvent
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
                        requiresRecruitedContact = raw.requiresRecruitedContact,
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("RegionEventSystem: failed to load events - " + ex.Message);
            }
        }

        static EventScope ParseScope(string raw)
        {
            return !string.IsNullOrEmpty(raw) && Enum.TryParse(raw, true, out EventScope s) ? s : EventScope.Local;
        }
    }
}
