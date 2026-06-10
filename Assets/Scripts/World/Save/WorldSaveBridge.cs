using System;
using System.IO;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Persists the corrected world map to its own JSON file under the save folder
    /// (<c>&lt;slot&gt;.world.json</c>). This is intentionally independent of the legacy
    /// SaveGameData payload (which encoded the wrong one-hex-one-region model), so loading an
    /// old game cannot corrupt the new tile/region data — old worlds are detected and rejected.
    /// </summary>
    public static class WorldSaveBridge
    {
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public static string PathForSlot(string slot)
        {
            string safe = string.IsNullOrWhiteSpace(slot) ? "autosave" : slot;
            return Path.Combine(SaveDirectory, safe + ".world.json");
        }

        public static bool HasWorld(string slot) => File.Exists(PathForSlot(slot));

        public static void Save(string slot, WorldMapData map)
        {
            if (map == null) return;
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(PathForSlot(slot), WorldMapSerializer.ToJson(map, true));
            }
            catch (Exception ex)
            {
                Debug.LogError("WorldSaveBridge: failed to save world '" + slot + "': " + ex.Message);
            }
        }

        /// <summary>
        /// Loads a world map. Returns null (and logs a clear warning) for missing, corrupt, or
        /// legacy-format saves instead of throwing.
        /// </summary>
        public static WorldMapData Load(string slot)
        {
            string path = PathForSlot(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning("WorldSaveBridge: no world save at " + path);
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                WorldMapData map = WorldMapSerializer.FromJson(json, out string error);
                if (map == null)
                {
                    Debug.LogWarning("WorldSaveBridge: cannot load world '" + slot + "': " + error +
                                     " A fresh world will be generated.");
                    return null;
                }
                return map;
            }
            catch (Exception ex)
            {
                Debug.LogError("WorldSaveBridge: exception loading world '" + slot + "': " + ex.Message);
                return null;
            }
        }
    }
}
