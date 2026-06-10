namespace Institute.World
{
    /// <summary>
    /// Cross-scene carrier for the chosen map-generation settings. The New Game Setup screen
    /// fills this in; the Gameplay scene's <see cref="WorldController"/> consumes it.
    ///
    /// Kept separate from the legacy DifficultyConfig because that type clamps map size to a
    /// tiny 3..30 range meant for the old one-hex-one-region model. Civ-like maps are larger.
    /// </summary>
    public static class WorldSetup
    {
        public static MapGenerationSettings PendingSettings;

        public static MapGenerationSettings ResolveOrDefault()
        {
            return PendingSettings != null ? PendingSettings.Clone() : MapGenerationSettings.ForPreset("Medium");
        }

        public static void Clear() => PendingSettings = null;
    }
}
