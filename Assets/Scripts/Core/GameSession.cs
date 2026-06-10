using UnityEngine;

public static class GameSession
{
    static DifficultyConfig activeDifficulty;
    static bool loadRequested;
    static string requestedLoadSlot;
    static NewGameSettings activeNewGameSettings;
    static string pendingSceneName;

    public static DifficultyConfig ActiveDifficulty
    {
        get
        {
            if (activeDifficulty == null)
                activeDifficulty = DifficultyConfig.CreatePreset(DifficultyPreset.Normal);

            activeDifficulty.Normalize();
            return activeDifficulty;
        }
    }

    public static bool LoadRequested => loadRequested;
    public static string RequestedLoadSlot => string.IsNullOrEmpty(requestedLoadSlot) ? SaveLoadManager.AutoSaveSlot : requestedLoadSlot;
    public static NewGameSettings ActiveNewGameSettings => activeNewGameSettings ?? NewGameSettings.FromDifficulty(ActiveDifficulty);
    public static string PendingSceneName => string.IsNullOrEmpty(pendingSceneName) ? InstituteSceneNames.Gameplay : pendingSceneName;

    public static void StartNewGame(DifficultyConfig difficulty)
    {
        activeDifficulty = difficulty != null ? difficulty.Clone() : DifficultyConfig.CreatePreset(DifficultyPreset.Normal);
        activeDifficulty.Normalize();
        if (activeDifficulty.useRandomSeed)
            activeDifficulty.randomSeed = Random.Range(int.MinValue / 2, int.MaxValue / 2);

        activeNewGameSettings = NewGameSettings.FromDifficulty(activeDifficulty);
        loadRequested = false;
        requestedLoadSlot = null;
    }

    public static void StartNewGame(NewGameSettings settings)
    {
        activeNewGameSettings = settings ?? NewGameSettings.FromDifficulty(DifficultyConfig.CreatePreset(DifficultyPreset.Normal));
        activeNewGameSettings.difficulty = activeNewGameSettings.difficulty != null
            ? activeNewGameSettings.difficulty.Clone()
            : DifficultyConfig.CreatePreset(DifficultyPreset.Normal);
        activeNewGameSettings.difficulty.mapWidth = activeNewGameSettings.mapWidth;
        activeNewGameSettings.difficulty.mapHeight = activeNewGameSettings.mapHeight;
        activeNewGameSettings.difficulty.randomSeed = activeNewGameSettings.seed;
        activeNewGameSettings.difficulty.useRandomSeed = false;
        StartNewGame(activeNewGameSettings.difficulty);
    }

    public static void RequestLoadGame(string slotName)
    {
        loadRequested = true;
        requestedLoadSlot = string.IsNullOrWhiteSpace(slotName) ? SaveLoadManager.AutoSaveSlot : slotName;
    }

    public static void SetDifficultyFromSave(DifficultyConfig difficulty)
    {
        activeDifficulty = difficulty != null ? difficulty.Clone() : DifficultyConfig.CreatePreset(DifficultyPreset.Normal);
        activeDifficulty.Normalize();
        activeNewGameSettings = NewGameSettings.FromDifficulty(activeDifficulty);
    }

    public static void ClearLoadRequest()
    {
        loadRequested = false;
        requestedLoadSlot = null;
    }

    public static void SetPendingScene(string sceneName)
    {
        pendingSceneName = string.IsNullOrEmpty(sceneName) ? InstituteSceneNames.Gameplay : sceneName;
    }

    public static void ClearPendingScene()
    {
        pendingSceneName = null;
    }
}
