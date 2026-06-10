using Institute.World.Gameplay;
using Institute.World.UI;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Builds the corrected, map-centric gameplay scene at runtime and wires the RegionData-driven
    /// gameplay systems + UI Toolkit overlays. Replaces the legacy GameplaySceneInstaller (which used
    /// the uGUI one-hex-one-region setup). Drop on an empty GameObject in the Gameplay scene
    /// (the editor tool "Rebuild Gameplay World Scene" does this).
    /// </summary>
    public class WorldGameplayInstaller : MonoBehaviour
    {
        void Awake()
        {
            MapDefinitions.Reload();
            MapPalette.Reload();
            UIToolkitThemeUtility.EnsureEventSystem();

            // 1) Gameplay systems (RegionData-driven; no legacy Region).
            var systems = new GameObject("Gameplay Systems");
            systems.AddComponent<GameManager>();
            var resources = systems.AddComponent<ResourceManager>();
            SeedResources(resources);
            systems.AddComponent<GameDateTracker>();        // calendar (TimeManager reads it)
            systems.AddComponent<TimeManager>();            // fires OnNewDay -> economy tick
            systems.AddComponent<EconomySystem>();          // daily income/sanity/exposure + win/loss
            systems.AddComponent<RegionDecisionSystem>();
            systems.AddComponent<RegionEventSystem>();
            systems.AddComponent<RegionCharacterSystem>();
            systems.AddComponent<RuntimeDiagnostics>(); // F9 in Play Mode for a presentation report

            // 2) World + HUD (WorldController.Awake bootstraps the hex world during AddComponent).
            var worldGo = new GameObject("World");
            worldGo.AddComponent<WorldController>();
            worldGo.AddComponent<WorldHUDController>();

            // 3) UI Toolkit overlays (each its own panel, sorted above the HUD).
            new GameObject("Settings Overlay").AddComponent<WorldSettingsController>();
            new GameObject("Pause Overlay").AddComponent<WorldPauseController>();
            new GameObject("Event Overlay").AddComponent<WorldEventPopupController>();

            // 4) Route events to the popup.
            if (RegionEventSystem.Instance != null && WorldEventPopupController.Instance != null)
                RegionEventSystem.Instance.EventReady += WorldEventPopupController.Instance.Show;

            // 5) Populate gameplay state: load a saved game if requested, else generate characters.
            ResolveInitialState();
        }

        void SeedResources(ResourceManager resources)
        {
            if (resources == null) return;
            try
            {
                DifficultyConfig d = GameSession.ActiveDifficulty;
                resources.ChangeSanity(d.startingSanity - resources.Sanity);
                resources.ChangeMoney(d.startingMoney - resources.Money);
                resources.ChangeArtifacts(d.startingArtifacts - resources.Artifacts);
            }
            catch { /* standalone test scene without GameSession */ }
        }

        void ResolveInitialState()
        {
            WorldController wc = WorldController.Instance;
            if (wc == null || wc.Map == null) return;

            bool wantsLoad = false;
            string slot = "autosave";
            try { wantsLoad = GameSession.LoadRequested; slot = GameSession.RequestedLoadSlot; }
            catch { }

            if (wantsLoad && GameSaveService.HasSave(slot))
            {
                if (GameSaveService.LoadAll(slot)) return; // world + characters + cooldowns restored
                Debug.LogWarning("WorldGameplayInstaller: load failed or save was legacy; using generated world.");
            }

            // Fresh game (or failed load): generate region-attached characters for the current world.
            if (RegionCharacterSystem.Instance != null)
                RegionCharacterSystem.Instance.GenerateFor(wc.Map);
        }
    }
}
