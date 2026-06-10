using System.Collections.Generic;
using UnityEngine;

namespace Institute.World.Gameplay
{
    /// <summary>
    /// Generates and manages characters attached to <see cref="RegionData"/> by regionId (never the
    /// legacy Region). Reuses the <c>GameCharacter</c> data class (already region-id keyed) and
    /// <see cref="WorldCharacterBridge"/> for region linkage + per-region stat effects.
    /// </summary>
    public class RegionCharacterSystem : MonoBehaviour
    {
        public static RegionCharacterSystem Instance { get; private set; }

        static readonly string[] FirstNames =
        {
            "Aldric", "Bryn", "Corin", "Dalia", "Edda", "Fenn", "Goran", "Hilde",
            "Ivo", "Jora", "Kael", "Lysa", "Mara", "Niall", "Osric", "Petra",
        };
        static readonly string[] Surnames =
        {
            "of the Vale", "Stonehand", "Ashford", "Greymantle", "Thornwood",
            "Bellwether", "Hollow", "Vance", "Mercer", "Calder",
        };

        readonly List<GameCharacter> _characters = new List<GameCharacter>();
        WorldMapData _map;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public IReadOnlyList<GameCharacter> Characters => _characters;

        /// <summary>Populate characters for a freshly generated world (deterministic by map seed).</summary>
        public void GenerateFor(WorldMapData map)
        {
            _map = map;
            _characters.Clear();
            if (map == null) return;

            var rng = new System.Random(map.seed ^ 0x5f3759df);
            var ordered = new List<RegionData>(map.regionsById.Values);
            ordered.Sort((a, b) => a.capitalTileId.CompareTo(b.capitalTileId));

            int idx = 0;
            foreach (var region in ordered)
            {
                region.characterIds.Clear();
                int count = rng.Next(0, 3); // 0..2 characters per region
                for (int i = 0; i < count; i++)
                {
                    GameCharacter ch = BuildCharacter(rng, region, idx++);
                    _characters.Add(ch);
                    WorldCharacterBridge.AttachCharacterToRegion(map, region.regionId, ch.id);
                }
            }
        }

        GameCharacter BuildCharacter(System.Random rng, RegionData region, int seq)
        {
            CharacterRole role = RoleForRegion(region.regionType, rng);
            var ch = new GameCharacter
            {
                id = "char_" + seq,
                displayName = FirstNames[rng.Next(FirstNames.Length)] + " " + Surnames[rng.Next(Surnames.Length)],
                title = TitleForRole(role),
                role = role,
                faction = region.displayName,
                homeRegionId = region.regionId,
                currentRegionId = region.regionId,
                relationshipWithPlayer = rng.Next(-20, 30),
                loyalty = rng.Next(20, 70),
                trust = rng.Next(20, 70),
                fear = rng.Next(0, 40),
                ambition = rng.Next(20, 90),
                competence = rng.Next(30, 90),
                corruption = rng.Next(0, 70),
                influencePower = rng.Next(20, 80),
                portraitColor = Color.HSVToRGB((float)rng.NextDouble(), 0.4f, 0.8f),
            };
            ch.ClampRuntimeValues();
            return ch;
        }

        static CharacterRole RoleForRegion(RegionType type, System.Random rng)
        {
            switch (type)
            {
                case RegionType.TempleDomain: return CharacterRole.TempleElder;
                case RegionType.TradeBasin:
                case RegionType.CoastalLeague: return CharacterRole.GuildRepresentative;
                case RegionType.FrontierMarch:
                case RegionType.TribalConfederation: return rng.Next(2) == 0 ? CharacterRole.RebelOrganizer : CharacterRole.VillageSpeaker;
                case RegionType.RuinedZone: return CharacterRole.Scholar;
                case RegionType.MountainClans: return CharacterRole.MercenaryCaptain;
                case RegionType.KingdomHeartland: return CharacterRole.LocalLord;
                default: return CharacterRole.LocalLord;
            }
        }

        static string TitleForRole(CharacterRole role)
        {
            switch (role)
            {
                case CharacterRole.LocalLord: return "Local Lord";
                case CharacterRole.TempleElder: return "Temple Elder";
                case CharacterRole.GuildRepresentative: return "Guild Representative";
                case CharacterRole.RebelOrganizer: return "Rebel Organizer";
                case CharacterRole.VillageSpeaker: return "Village Speaker";
                case CharacterRole.Scholar: return "Scholar";
                case CharacterRole.MercenaryCaptain: return "Mercenary Captain";
                case CharacterRole.InstituteSympathizer: return "Institute Contact";
                default: return "Notable";
            }
        }

        public List<GameCharacter> GetCharactersInRegion(string regionId)
        {
            var list = new List<GameCharacter>();
            if (string.IsNullOrEmpty(regionId)) return list;
            foreach (var c in _characters)
                if (c != null && c.currentRegionId == regionId) list.Add(c);
            return list;
        }

        public bool TryGetCharacter(string id, out GameCharacter character)
        {
            character = null;
            foreach (var c in _characters)
                if (c != null && c.id == id) { character = c; return true; }
            return false;
        }

        // ---------- interactions ----------
        public static readonly Dictionary<CharacterInteractionType, CharacterInteractionDefinition> Interactions =
            new Dictionary<CharacterInteractionType, CharacterInteractionDefinition>
        {
            { CharacterInteractionType.Negotiate, new CharacterInteractionDefinition { interactionType = CharacterInteractionType.Negotiate, displayName = "Negotiate", sanityCost = 2, cooldownSeconds = 15f } },
            { CharacterInteractionType.Bribe, new CharacterInteractionDefinition { interactionType = CharacterInteractionType.Bribe, displayName = "Bribe", moneyCost = 20, cooldownSeconds = 20f } },
            { CharacterInteractionType.Threaten, new CharacterInteractionDefinition { interactionType = CharacterInteractionType.Threaten, displayName = "Threaten", sanityCost = 4, cooldownSeconds = 25f } },
            { CharacterInteractionType.Support, new CharacterInteractionDefinition { interactionType = CharacterInteractionType.Support, displayName = "Support", moneyCost = 15, cooldownSeconds = 20f } },
            { CharacterInteractionType.Undermine, new CharacterInteractionDefinition { interactionType = CharacterInteractionType.Undermine, displayName = "Undermine", sanityCost = 5, cooldownSeconds = 30f } },
            { CharacterInteractionType.RecruitAsContact, new CharacterInteractionDefinition { interactionType = CharacterInteractionType.RecruitAsContact, displayName = "Recruit as Contact", artifactCost = 1, minTrust = 50, cooldownSeconds = 40f } },
            { CharacterInteractionType.Investigate, new CharacterInteractionDefinition { interactionType = CharacterInteractionType.Investigate, displayName = "Investigate", sanityCost = 3, cooldownSeconds = 20f } },
        };

        public bool CanInteract(GameCharacter character, CharacterInteractionType type, out string reason)
        {
            reason = null;
            if (character == null) { reason = "No character."; return false; }
            CharacterInteractionDefinition def = Interactions[type];
            if (character.trust < def.minTrust) { reason = $"Needs trust {def.minTrust}."; return false; }
            if (RemainingCooldown(character, type) > 0f) { reason = "On cooldown."; return false; }
            if (!GameResources.CanAfford(def.sanityCost, def.moneyCost, def.artifactCost)) { reason = "Not enough resources."; return false; }
            return true;
        }

        public CharacterInteractionResult ApplyInteraction(GameCharacter character, CharacterInteractionType type)
        {
            if (!CanInteract(character, type, out string reason))
                return new CharacterInteractionResult { success = false, message = reason };

            CharacterInteractionDefinition def = Interactions[type];
            if (!GameResources.TrySpend(def.sanityCost, def.moneyCost, def.artifactCost))
                return new CharacterInteractionResult { success = false, message = "Not enough resources." };

            RegionData region = _map != null ? _map.GetRegion(character.currentRegionId) : null;
            int infl = 0, stab = 0, dev = 0;
            string msg;

            switch (type)
            {
                case CharacterInteractionType.Negotiate:
                    character.trust += 8; character.relationshipWithPlayer += 6; infl = 2; msg = "Negotiations improved relations."; break;
                case CharacterInteractionType.Bribe:
                    character.loyalty += 10; character.corruption += 8; character.relationshipWithPlayer += 8; stab = -1; infl = 3; msg = "A discreet payment was made."; break;
                case CharacterInteractionType.Threaten:
                    character.fear += 15; character.relationshipWithPlayer -= 10; character.loyalty -= 4; stab = -2; infl = 4; msg = "Threats were issued."; break;
                case CharacterInteractionType.Support:
                    character.loyalty += 8; character.relationshipWithPlayer += 6; stab = 3; dev = 2; msg = "Public support strengthened the region."; break;
                case CharacterInteractionType.Undermine:
                    character.relationshipWithPlayer -= 12; stab = -4; infl = 2; msg = "Quiet sabotage destabilized rivals."; break;
                case CharacterInteractionType.RecruitAsContact:
                    character.recruitedAsContact = true; character.loyalty += 12; infl = 5; msg = $"{character.displayName} is now an Institute contact."; break;
                case CharacterInteractionType.Investigate:
                    if (character.hiddenTraits.Count > 0)
                    {
                        string t = character.hiddenTraits[0];
                        character.hiddenTraits.RemoveAt(0);
                        if (!character.revealedTraits.Contains(t)) character.revealedTraits.Add(t);
                    }
                    msg = "Investigation revealed new information."; break;
                default: msg = "Nothing happened."; break;
            }

            character.ClampRuntimeValues();
            if (region != null && (infl != 0 || stab != 0 || dev != 0))
                WorldCharacterBridge.ApplyEffect(region, infl, stab, dev, character.id);
            // A character's standing flows up to their feudal state's player-influence.
            if (region != null && _map != null) WorldStateUtil.RecomputeStats(_map, _map.GetState(region.stateId));
            if (region != null) WorldController.Instance?.RaiseRegionDataChanged(region);

            BeginCooldown(character, type, def.cooldownSeconds);
            PlayerLog.Add($"{def.displayName} → {character.displayName}: {msg}");
            return new CharacterInteractionResult { success = true, message = msg };
        }

        float RemainingCooldown(GameCharacter character, CharacterInteractionType type)
        {
            foreach (var cd in character.cooldowns)
                if (cd.interactionType == type) return Mathf.Max(0f, cd.readyAtTime - Time.time);
            return 0f;
        }

        void BeginCooldown(GameCharacter character, CharacterInteractionType type, float seconds)
        {
            foreach (var cd in character.cooldowns)
                if (cd.interactionType == type) { cd.readyAtTime = Time.time + seconds; return; }
            character.cooldowns.Add(new CharacterInteractionCooldown { interactionType = type, readyAtTime = Time.time + seconds });
        }

        // ---------- save / load ----------
        public List<CharacterSaveData> Capture()
        {
            var list = new List<CharacterSaveData>();
            foreach (var c in _characters)
                if (c != null) list.Add(CharacterSaveData.FromRuntimeCharacter(c));
            return list;
        }

        public void Restore(List<CharacterSaveData> saved, WorldMapData map)
        {
            _map = map;
            _characters.Clear();
            if (saved == null) return;
            foreach (var region in map.Regions) region.characterIds.Clear();
            foreach (var s in saved)
            {
                if (s == null) continue;
                GameCharacter c = s.ToRuntimeCharacter();
                _characters.Add(c);
                if (!string.IsNullOrEmpty(c.currentRegionId))
                    WorldCharacterBridge.AttachCharacterToRegion(map, c.currentRegionId, c.id);
            }
        }
    }
}
