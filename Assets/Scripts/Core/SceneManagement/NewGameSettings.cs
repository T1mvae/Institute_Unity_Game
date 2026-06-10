using System;

[Serializable]
public class NewGameSettings
{
    public DifficultyConfig difficulty = DifficultyConfig.CreatePreset(DifficultyPreset.Normal);
    public int seed;
    public int mapWidth = 8;
    public int mapHeight = 6;

    public static NewGameSettings FromDifficulty(DifficultyConfig config)
    {
        DifficultyConfig clone = config != null ? config.Clone() : DifficultyConfig.CreatePreset(DifficultyPreset.Normal);
        clone.Normalize();
        return new NewGameSettings
        {
            difficulty = clone,
            seed = clone.randomSeed,
            mapWidth = clone.mapWidth,
            mapHeight = clone.mapHeight
        };
    }
}
