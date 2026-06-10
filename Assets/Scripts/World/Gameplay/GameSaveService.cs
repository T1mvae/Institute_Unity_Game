using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Institute.World.Gameplay
{
    [Serializable]
    public class GameplaySave
    {
        public string savedAtUtc = "";
        public List<CharacterSaveData> characters = new List<CharacterSaveData>();
        public List<DecisionCooldownSaveData> decisionCooldowns = new List<DecisionCooldownSaveData>();
        public string selectedRegionId = "";
        public int exposure;
    }

    /// <summary>
    /// Saves/loads the full new-model gameplay state: the world (via <see cref="WorldSaveBridge"/>)
    /// plus a companion <c>&lt;slot&gt;.gameplay.json</c> holding characters and decision cooldowns.
    /// Old SaveGameData (one-hex-one-region) is untouched and remains rejected by the world loader.
    /// </summary>
    public static class GameSaveService
    {
        static string CompanionPath(string slot)
        {
            string safe = string.IsNullOrWhiteSpace(slot) ? "autosave" : slot;
            return Path.Combine(WorldSaveBridge.SaveDirectory, safe + ".gameplay.json");
        }

        public static bool SaveAll(string slot)
        {
            WorldController wc = WorldController.Instance;
            if (wc == null || wc.Map == null) return false;

            WorldSaveBridge.Save(slot, wc.Map);

            var save = new GameplaySave
            {
                selectedRegionId = wc.SelectedRegion != null ? wc.SelectedRegion.regionId : "",
            };
            if (RegionCharacterSystem.Instance != null) save.characters = RegionCharacterSystem.Instance.Capture();
            if (RegionDecisionSystem.Instance != null) save.decisionCooldowns = RegionDecisionSystem.Instance.CaptureCooldowns();
            if (EconomySystem.Instance != null) save.exposure = EconomySystem.Instance.Exposure;

            try
            {
                Directory.CreateDirectory(WorldSaveBridge.SaveDirectory);
                File.WriteAllText(CompanionPath(slot), JsonUtility.ToJson(save, true));
            }
            catch (Exception ex)
            {
                Debug.LogError("GameSaveService: failed to write companion save: " + ex.Message);
                return false;
            }
            Debug.Log("GameSaveService: saved slot '" + slot + "'.");
            return true;
        }

        public static bool HasSave(string slot) => WorldSaveBridge.HasWorld(slot);

        /// <summary>
        /// Loads a full game into the live WorldController. Returns false (no crash) for missing or
        /// legacy/invalid saves.
        /// </summary>
        public static bool LoadAll(string slot)
        {
            WorldController wc = WorldController.Instance;
            if (wc == null) return false;

            WorldMapData map = WorldSaveBridge.Load(slot);
            if (map == null) return false; // missing / legacy / corrupt -> handled by caller

            wc.BuildWorld(map);

            GameplaySave companion = ReadCompanion(slot);
            if (companion != null)
            {
                if (RegionCharacterSystem.Instance != null)
                    RegionCharacterSystem.Instance.Restore(companion.characters, map);
                if (RegionDecisionSystem.Instance != null)
                    RegionDecisionSystem.Instance.RestoreCooldowns(companion.decisionCooldowns);
                if (EconomySystem.Instance != null) EconomySystem.Instance.SetExposure(companion.exposure);
                if (!string.IsNullOrEmpty(companion.selectedRegionId))
                    wc.SelectRegion(companion.selectedRegionId);
                else
                    wc.ClearSelection();
            }
            else if (RegionCharacterSystem.Instance != null)
            {
                // World loaded but no companion: regenerate characters so the panel isn't empty.
                RegionCharacterSystem.Instance.GenerateFor(map);
            }

            Debug.Log("GameSaveService: loaded slot '" + slot + "'.");
            return true;
        }

        static GameplaySave ReadCompanion(string slot)
        {
            string path = CompanionPath(slot);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<GameplaySave>(File.ReadAllText(path)); }
            catch (Exception ex) { Debug.LogWarning("GameSaveService: companion read failed: " + ex.Message); return null; }
        }
    }
}
