using System.Text;
using Institute.World.Gameplay;
using Institute.World.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Institute.World
{
    /// <summary>
    /// Lightweight runtime self-test. Logs a presentation report once on start and on demand (F9),
    /// and can draw a small on-screen panel when <see cref="showPanel"/> is enabled. Helps diagnose
    /// "no camera / no theme / no tiles" without digging through the console.
    /// </summary>
    public class RuntimeDiagnostics : MonoBehaviour
    {
        public bool showPanel = false;
        public KeyCode toggleKey = KeyCode.F9;

        string _cached = "";

        void Start() => Refresh(true);

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showPanel = !showPanel;
                Refresh(true);
            }
        }

        public void Refresh(bool log)
        {
            _cached = BuildReport();
            if (log) Debug.Log(_cached);
        }

        string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Institute Runtime Diagnostics ===");
            sb.AppendLine("Scene: " + SceneManager.GetActiveScene().name);

            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            sb.AppendLine($"Active cameras: {cameras.Length}   Main: {(Camera.main != null ? Camera.main.name : "<none>")}");
            foreach (var cam in cameras)
                sb.AppendLine($"  - {cam.name}  enabled={cam.enabled}  ortho={cam.orthographic}  cull={cam.cullingMask}");

            var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            sb.AppendLine($"UIDocuments: {docs.Length}");
            foreach (var d in docs)
            {
                bool hasPanel = d.panelSettings != null;
                bool hasTheme = hasPanel && d.panelSettings.themeStyleSheet != null;
                sb.AppendLine($"  - {d.name}  panelSettings={hasPanel}  themeStyleSheet={hasTheme}  sort={d.sortingOrder}");
            }

            var wc = WorldController.Instance;
            if (wc != null && wc.Map != null)
            {
                sb.AppendLine($"World: tiles={wc.Map.TileCount}  regions={wc.Map.RegionCount}  states={wc.Map.StateCount}  unclaimed={wc.Map.unclaimedTileIds.Count}");
                if (EconomySystem.Instance != null)
                    sb.AppendLine($"Economy: exposure={EconomySystem.Instance.Exposure}  globalStability={EconomySystem.Instance.GlobalStability(wc.Map)}%  lastIncome={EconomySystem.Instance.LastIncome}");
                sb.AppendLine($"Selected region: {(wc.SelectedRegion != null ? wc.SelectedRegion.displayName : "<none>")}  " +
                              $"Selected tile: {(wc.SelectedTile != null ? wc.SelectedTile.coord.ToString() : "<none>")}");
            }
            else sb.AppendLine("World: <no WorldController/Map>");

            var renderer = wc != null ? wc.GetComponentInChildren<MapRenderManager>() : FindFirstObjectByType<MapRenderManager>();
            sb.AppendLine($"MapRenderManager: {(renderer != null ? "present, bounds=" + renderer.WorldBounds.size : "MISSING")}");

            var overlayRenderer = FindFirstObjectByType<RegionOverlayRenderer>();
            sb.AppendLine($"RegionOverlayRenderer: {(overlayRenderer != null ? "present" : "MISSING")}");

            sb.AppendLine($"Characters: {(RegionCharacterSystem.Instance != null ? RegionCharacterSystem.Instance.Characters.Count : 0)}");
            sb.AppendLine($"Decisions loaded: {(RegionDecisionSystem.Instance != null ? RegionDecisionSystem.Instance.AllDecisions.Count : 0)}");
            sb.AppendLine($"Theme cached: {(UIToolkitThemeUtility.LoadDefaultTheme() != null)}");
            return sb.ToString();
        }

        void OnGUI()
        {
            if (!showPanel) return;
            GUI.color = Color.white;
            GUI.Box(new Rect(8, 8, 460, 320), "");
            GUI.Label(new Rect(16, 14, 446, 308), _cached);
        }
    }
}
