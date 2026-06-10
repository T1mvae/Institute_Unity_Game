using System;
using System.Collections.Generic;

[Serializable]
public class SaveGameData
{
    public int saveVersion = 1;
    public string savedAtUtc;
    public DifficultyConfig difficulty;
    public int randomSeed;
    public float elapsedGameTime;
    public int money;
    public int artifacts;
    public int sanity;
    public string selectedRegionId;
    public List<RegionSaveData> regions = new List<RegionSaveData>();
    public List<DecisionCooldownSaveData> decisionCooldowns = new List<DecisionCooldownSaveData>();
    public EventManagerSaveData eventState = new EventManagerSaveData();
    public List<CharacterSaveData> characters = new List<CharacterSaveData>();
    public List<string> globalFlags = new List<string>();
}

[Serializable]
public class RegionSaveData
{
    public string id;
    public string name;
    public int hexQ;
    public int hexR;
    public string regionType;
    public List<string> tags = new List<string>();
    public int influence;
    public int stability;
    public int development;
    public List<string> neighborIds = new List<string>();
    public List<ModifierSaveData> modifiers = new List<ModifierSaveData>();
}

[Serializable]
public class ModifierSaveData
{
    public string name;
    public float durationSeconds;
    public float tickIntervalSeconds;
    public int influencePerTick;
    public int stabilityPerTick;
    public int developmentPerTick;
    public float remainingDurationSeconds;
}

[Serializable]
public class DecisionCooldownSaveData
{
    public string decisionId;
    public string regionId;
    public float remainingSeconds;
}

[Serializable]
public class EventManagerSaveData
{
    public bool eventActive;
    public int activeEventId = -1;
    public string activeRegionId;
    public string activeCharacterId;
}

[Serializable]
public class CharacterSaveData
{
    public string id;
    public string displayName;
    public string title;
    public string portraitId;
    public float portraitR;
    public float portraitG;
    public float portraitB;
    public float portraitA;
    public string homeRegionId;
    public string currentRegionId;
    public string faction;
    public string role;
    public string status;
    public int relationshipWithPlayer;
    public int loyalty;
    public int fear;
    public int trust;
    public int ambition;
    public int competence;
    public int corruption;
    public int influencePower;
    public bool recruitedAsContact;
    public int lastPassiveDayApplied;
    public List<string> personalityTraits = new List<string>();
    public List<string> ideology = new List<string>();
    public List<string> tags = new List<string>();
    public List<string> hiddenTraits = new List<string>();
    public List<string> revealedTraits = new List<string>();
    public List<string> flags = new List<string>();
    public List<CharacterInteractionCooldownSaveData> cooldowns = new List<CharacterInteractionCooldownSaveData>();

    public static CharacterSaveData FromRuntimeCharacter(GameCharacter character)
    {
        CharacterSaveData data = new CharacterSaveData
        {
            id = character.id,
            displayName = character.displayName,
            title = character.title,
            portraitId = character.portraitId,
            portraitR = character.portraitColor.r,
            portraitG = character.portraitColor.g,
            portraitB = character.portraitColor.b,
            portraitA = character.portraitColor.a,
            homeRegionId = character.homeRegionId,
            currentRegionId = character.currentRegionId,
            faction = character.faction,
            role = character.role.ToString(),
            status = character.status.ToString(),
            relationshipWithPlayer = character.relationshipWithPlayer,
            loyalty = character.loyalty,
            fear = character.fear,
            trust = character.trust,
            ambition = character.ambition,
            competence = character.competence,
            corruption = character.corruption,
            influencePower = character.influencePower,
            recruitedAsContact = character.recruitedAsContact,
            lastPassiveDayApplied = character.lastPassiveDayApplied
        };

        data.personalityTraits.AddRange(character.personalityTraits);
        data.ideology.AddRange(character.ideology);
        data.tags.AddRange(character.tags);
        data.hiddenTraits.AddRange(character.hiddenTraits);
        data.revealedTraits.AddRange(character.revealedTraits);
        data.flags.AddRange(character.flags);

        for (int i = 0; i < character.cooldowns.Count; i++)
        {
            CharacterInteractionCooldown cooldown = character.cooldowns[i];
            data.cooldowns.Add(new CharacterInteractionCooldownSaveData
            {
                interactionType = cooldown.interactionType.ToString(),
                remainingSeconds = Math.Max(0f, cooldown.readyAtTime - UnityEngine.Time.time)
            });
        }

        return data;
    }

    public GameCharacter ToRuntimeCharacter()
    {
        Enum.TryParse(role, out CharacterRole parsedRole);
        Enum.TryParse(status, out CharacterStatus parsedStatus);

        GameCharacter character = new GameCharacter
        {
            id = id,
            displayName = displayName,
            title = title,
            portraitId = portraitId,
            portraitColor = new UnityEngine.Color(portraitR, portraitG, portraitB, portraitA),
            homeRegionId = homeRegionId,
            currentRegionId = currentRegionId,
            faction = faction,
            role = parsedRole,
            status = parsedStatus,
            relationshipWithPlayer = relationshipWithPlayer,
            loyalty = loyalty,
            fear = fear,
            trust = trust,
            ambition = ambition,
            competence = competence,
            corruption = corruption,
            influencePower = influencePower,
            recruitedAsContact = recruitedAsContact,
            lastPassiveDayApplied = lastPassiveDayApplied
        };

        character.personalityTraits.AddRange(personalityTraits ?? new List<string>());
        character.ideology.AddRange(ideology ?? new List<string>());
        character.tags.AddRange(tags ?? new List<string>());
        character.hiddenTraits.AddRange(hiddenTraits ?? new List<string>());
        character.revealedTraits.AddRange(revealedTraits ?? new List<string>());
        character.flags.AddRange(flags ?? new List<string>());

        if (cooldowns != null)
        {
            for (int i = 0; i < cooldowns.Count; i++)
            {
                CharacterInteractionCooldownSaveData saved = cooldowns[i];
                if (saved == null || !Enum.TryParse(saved.interactionType, out CharacterInteractionType parsedInteraction))
                    continue;

                character.cooldowns.Add(new CharacterInteractionCooldown
                {
                    interactionType = parsedInteraction,
                    readyAtTime = UnityEngine.Time.time + Math.Max(0f, saved.remainingSeconds)
                });
            }
        }

        character.ClampRuntimeValues();
        return character;
    }
}

[Serializable]
public class CharacterInteractionCooldownSaveData
{
    public string interactionType;
    public float remainingSeconds;
}
