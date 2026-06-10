using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterStatus
{
    Active,
    Wounded,
    Imprisoned,
    Exiled,
    Dead
}

public enum CharacterRole
{
    LocalLord,
    TempleElder,
    GuildRepresentative,
    RebelOrganizer,
    VillageSpeaker,
    Scholar,
    MercenaryCaptain,
    InstituteSympathizer
}

public enum CharacterInteractionType
{
    Negotiate,
    Bribe,
    Threaten,
    Support,
    Undermine,
    RecruitAsContact,
    Investigate
}

[Serializable]
public class GameCharacter
{
    public string id;
    public string displayName;
    public string title;
    public string portraitId;
    public Color portraitColor = Color.white;
    public string homeRegionId;
    public string currentRegionId;
    public string faction;
    public List<string> personalityTraits = new List<string>();
    public List<string> ideology = new List<string>();
    public List<string> tags = new List<string>();
    public List<string> hiddenTraits = new List<string>();
    public List<string> revealedTraits = new List<string>();
    public int relationshipWithPlayer;
    public int loyalty;
    public int fear;
    public int trust;
    public int ambition;
    public int competence;
    public int corruption;
    public int influencePower;
    public CharacterRole role;
    public CharacterStatus status = CharacterStatus.Active;
    public bool recruitedAsContact;
    public int lastPassiveDayApplied;
    public List<CharacterInteractionCooldown> cooldowns = new List<CharacterInteractionCooldown>();
    public List<string> flags = new List<string>();

    public string RegionLabel => currentRegionId;

    public bool IsAvailable => status == CharacterStatus.Active || status == CharacterStatus.Wounded;

    public void ClampRuntimeValues()
    {
        relationshipWithPlayer = Mathf.Clamp(relationshipWithPlayer, -100, 100);
        loyalty = Mathf.Clamp(loyalty, 0, 100);
        fear = Mathf.Clamp(fear, 0, 100);
        trust = Mathf.Clamp(trust, 0, 100);
        ambition = Mathf.Clamp(ambition, 0, 100);
        competence = Mathf.Clamp(competence, 0, 100);
        corruption = Mathf.Clamp(corruption, 0, 100);
        influencePower = Mathf.Clamp(influencePower, 0, 100);
    }

    public string BuildShortDescription()
    {
        string traitText = personalityTraits.Count > 0 ? string.Join(", ", personalityTraits) : "opaque";
        string ideologyText = ideology.Count > 0 ? string.Join(", ", ideology) : "pragmatic";
        string contact = recruitedAsContact ? " Recruited Institute contact." : string.Empty;
        return $"{title} aligned with {faction}. Traits: {traitText}. Worldview: {ideologyText}.{contact}";
    }

    public string BuildPassiveEffectSummary()
    {
        switch (role)
        {
            case CharacterRole.LocalLord:
                return loyalty >= 55 ? "Loyal lord: +Influence over time" : "Unreliable lord: possible Stability drag";
            case CharacterRole.TempleElder:
                return "Temple elder: tends to stabilize, may resist Development";
            case CharacterRole.GuildRepresentative:
                return corruption >= 55 ? "Corrupt merchant: money opportunities, Stability risk" : "Guild contact: Development support";
            case CharacterRole.RebelOrganizer:
                return recruitedAsContact ? "Converted rebel: covert Influence source" : "Ignored rebel: Stability pressure";
            case CharacterRole.Scholar:
                return "Scholar: Development pressure over time";
            case CharacterRole.MercenaryCaptain:
                return "Commander: Stability protection, Fear pressure";
            case CharacterRole.InstituteSympathizer:
                return "Institute contact: passive Influence, exposure risk";
            default:
                return "Local voice: small regional pressure";
        }
    }
}

[Serializable]
public class CharacterInteractionCooldown
{
    public CharacterInteractionType interactionType;
    public float readyAtTime;
}

[Serializable]
public class CharacterInteractionDefinition
{
    public CharacterInteractionType interactionType;
    public string displayName;
    public int moneyCost;
    public int sanityCost;
    public int artifactCost;
    public int minTrust;
    public int minFear;
    public int minRelationship = -100;
    public float cooldownSeconds = 20f;
}

public class CharacterInteractionResult
{
    public bool success;
    public string message;
}
