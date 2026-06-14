using System;
using UnityEngine;

public enum DifficultyPreset
{
    Easy,
    Normal,
    Hard,
    Custom
}

[Serializable]
public class DifficultyConfig
{
    public DifficultyPreset preset = DifficultyPreset.Normal;
    public string displayName = "Normal";

    [Header("Starting Resources")]
    public int startingMoney = 100;
    public int startingArtifacts = 5;
    public int startingSanity = 100;

    [Header("Map")]
    public int mapWidth = 8;
    public int mapHeight = 6;
    public int randomSeed = 12345;
    public bool useRandomSeed = true;

    [Header("Pacing")]
    public float eventFrequencyMultiplier = 1f;
    public float eventDangerMultiplier = 1f;
    public float decisionCostMultiplier = 1f;
    public float sanityChangePerTenSeconds = 1f;

    [Header("Starting Region Modifiers")]
    public int startingInfluenceModifier;
    public int startingStabilityModifier;
    public int startingDevelopmentModifier;

    public static DifficultyConfig CreatePreset(DifficultyPreset preset)
    {
        switch (preset)
        {
            case DifficultyPreset.Easy:
                return new DifficultyConfig
                {
                    preset = DifficultyPreset.Easy,
                    displayName = "Easy",
                    startingMoney = 150,
                    startingArtifacts = 8,
                    startingSanity = 100,
                    mapWidth = 7,
                    mapHeight = 5,
                    useRandomSeed = true,
                    eventFrequencyMultiplier = 0.75f,
                    eventDangerMultiplier = 0.75f,
                    decisionCostMultiplier = 0.85f,
                    sanityChangePerTenSeconds = 2f,
                    startingStabilityModifier = 3,
                    startingDevelopmentModifier = 1
                };
            case DifficultyPreset.Hard:
                return new DifficultyConfig
                {
                    preset = DifficultyPreset.Hard,
                    displayName = "Hard",
                    startingMoney = 70,
                    startingArtifacts = 3,
                    startingSanity = 80,
                    mapWidth = 9,
                    mapHeight = 7,
                    useRandomSeed = true,
                    eventFrequencyMultiplier = 1.35f,
                    eventDangerMultiplier = 1.35f,
                    decisionCostMultiplier = 1.25f,
                    sanityChangePerTenSeconds = -1f,
                    startingInfluenceModifier = -1,
                    startingStabilityModifier = -3,
                    startingDevelopmentModifier = -1
                };
            case DifficultyPreset.Custom:
                return new DifficultyConfig
                {
                    preset = DifficultyPreset.Custom,
                    displayName = "Custom",
                    startingMoney = 100,
                    startingArtifacts = 5,
                    startingSanity = 100,
                    mapWidth = 8,
                    mapHeight = 6,
                    useRandomSeed = true,
                    eventFrequencyMultiplier = 1f,
                    eventDangerMultiplier = 1f,
                    decisionCostMultiplier = 1f,
                    sanityChangePerTenSeconds = 1f
                };
            default:
                return new DifficultyConfig
                {
                    preset = DifficultyPreset.Normal,
                    displayName = "Normal",
                    startingMoney = 100,
                    startingArtifacts = 5,
                    startingSanity = 100,
                    mapWidth = 8,
                    mapHeight = 6,
                    useRandomSeed = true,
                    eventFrequencyMultiplier = 1f,
                    eventDangerMultiplier = 1f,
                    decisionCostMultiplier = 1f,
                    sanityChangePerTenSeconds = 1f
                };
        }
    }

    public DifficultyConfig Clone()
    {
        return new DifficultyConfig
        {
            preset = preset,
            displayName = displayName,
            startingMoney = startingMoney,
            startingArtifacts = startingArtifacts,
            startingSanity = startingSanity,
            mapWidth = mapWidth,
            mapHeight = mapHeight,
            randomSeed = randomSeed,
            useRandomSeed = useRandomSeed,
            eventFrequencyMultiplier = eventFrequencyMultiplier,
            eventDangerMultiplier = eventDangerMultiplier,
            decisionCostMultiplier = decisionCostMultiplier,
            sanityChangePerTenSeconds = sanityChangePerTenSeconds,
            startingInfluenceModifier = startingInfluenceModifier,
            startingStabilityModifier = startingStabilityModifier,
            startingDevelopmentModifier = startingDevelopmentModifier
        };
    }

    public void Normalize()
    {
        startingMoney = Mathf.Max(0, startingMoney);
        startingArtifacts = Mathf.Max(0, startingArtifacts);
        startingSanity = Mathf.Clamp(startingSanity, 0, 100);
        mapWidth = Mathf.Clamp(mapWidth, 3, 30);
        mapHeight = Mathf.Clamp(mapHeight, 3, 30);
        eventFrequencyMultiplier = Mathf.Clamp(eventFrequencyMultiplier, 0.1f, 5f);
        eventDangerMultiplier = Mathf.Clamp(eventDangerMultiplier, 0.1f, 5f);
        decisionCostMultiplier = Mathf.Clamp(decisionCostMultiplier, 0.1f, 5f);
        sanityChangePerTenSeconds = Mathf.Clamp(sanityChangePerTenSeconds, -5f, 5f);
        startingInfluenceModifier = Mathf.Clamp(startingInfluenceModifier, -10, 10);
        startingStabilityModifier = Mathf.Clamp(startingStabilityModifier, -10, 10);
        startingDevelopmentModifier = Mathf.Clamp(startingDevelopmentModifier, -10, 10);
    }
}
