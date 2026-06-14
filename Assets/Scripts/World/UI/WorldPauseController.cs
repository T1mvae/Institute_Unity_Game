using Institute.World.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Institute.World.UI
{
    /// <summary>
    /// UI Toolkit pause menu (PauseMenu.uxml). Replaces the HUD's direct Time.timeScale stopgap:
    /// pausing/resuming goes through GameManager when present (falls back to Time.timeScale).
    /// Opening Settings keeps the pause state.
    /// </summary>
    public class WorldPauseController : MonoBehaviour
    {
        public static WorldPauseController Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        Label _feedback;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Build();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            _doc = OverlayUtil.CreateDocument(gameObject, 200);
            _root = _doc.rootVisualElement;
            _root.Clear();
            // gameplay.uss carries the .btn/.btn-primary/.btn-danger theming the menu buttons use.
            OverlayUtil.AddStyles(_root, "UI/Styles/base", "UI/Styles/popups", "UI/Styles/gameplay");

            VisualTreeAsset uxml = OverlayUtil.LoadUxml("UI/UXML/PauseMenu");
            if (uxml != null)
            {
                uxml.CloneTree(_root);
                Bind(_root);
            }
            else
            {
                BuildFallback(_root);
            }
            OverlayUtil.ApplyOverlayLayout(_root, "pause-root");
            SetVisible(false);
        }

        void Bind(VisualElement root)
        {
            Wire(root.Q<Button>("btn-resume"), Resume);
            Wire(root.Q<Button>("btn-save"), SaveGame);
            Wire(root.Q<Button>("btn-load"), LoadGame);
            Wire(root.Q<Button>("btn-settings"), OpenSettings);
            Wire(root.Q<Button>("btn-menu"), ReturnToMenu);
            Wire(root.Q<Button>("btn-quit"), Quit);
            _feedback = root.Q<Label>("pause-feedback");
            if (_feedback == null)
            {
                _feedback = new Label("") { name = "pause-feedback" };
                _feedback.AddToClassList("dialog-body");
                root.Q<VisualElement>("pause-root")?.Add(_feedback);
            }
        }

        static void Wire(Button b, System.Action action) { if (b != null) b.clicked += action; }

        void BuildFallback(VisualElement root)
        {
            var overlay = new VisualElement { name = "pause-root" };
            overlay.AddToClassList("overlay");
            var dialog = new VisualElement(); dialog.AddToClassList("dialog");
            var title = new Label("PAUSED"); title.AddToClassList("dialog-title");
            dialog.Add(title);
            dialog.Add(OverlayUtil.Button("Resume", "btn-primary", Resume));
            dialog.Add(OverlayUtil.Button("Save Game", null, SaveGame));
            dialog.Add(OverlayUtil.Button("Load Game", null, LoadGame));
            dialog.Add(OverlayUtil.Button("Settings", null, OpenSettings));
            dialog.Add(OverlayUtil.Button("Return to Main Menu", null, ReturnToMenu));
            dialog.Add(OverlayUtil.Button("Quit Game", "btn-danger", Quit));
            _feedback = new Label("") { name = "pause-feedback" }; _feedback.AddToClassList("dialog-body");
            dialog.Add(_feedback);
            overlay.Add(dialog);
            root.Add(overlay);
        }

        public void Open()
        {
            PauseTime(true);
            SetVisible(true);
            if (_feedback != null) _feedback.text = "";
        }

        public void Resume()
        {
            PauseTime(false);
            SetVisible(false);
        }

        public void Toggle()
        {
            if (IsOpen) Resume(); else Open();
        }

        void SetVisible(bool visible)
        {
            IsOpen = visible;
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            MapInteractionGate.PointerOverUI = visible;
        }

        static void PauseTime(bool pause)
        {
            // Freeze time for a real pause, and also flip GameManager state so systems that gate on
            // IsPaused (e.g. the event scheduler) stop too.
            Time.timeScale = pause ? 0f : 1f;
            if (GameManager.Instance != null)
            {
                if (pause) GameManager.Instance.PauseGame(); else GameManager.Instance.ResumeGame();
            }
        }

        void SaveGame()
        {
            bool ok = GameSaveService.SaveAll("autosave");
            if (_feedback != null) _feedback.text = ok ? "Game saved." : "Save failed (no world).";
        }

        void LoadGame()
        {
            if (!GameSaveService.HasSave("autosave"))
            {
                if (_feedback != null) _feedback.text = "No save found.";
                return;
            }
            bool ok = GameSaveService.LoadAll("autosave");
            if (_feedback != null) _feedback.text = ok ? "Game loaded." : "Load failed (old or invalid save).";
            if (ok) Resume();
        }

        void OpenSettings()
        {
            if (WorldSettingsController.Instance != null) WorldSettingsController.Instance.Open();
        }

        void ReturnToMenu()
        {
            PauseTime(false);
            var flow = SceneFlowManager.EnsureExists();
            if (flow != null) flow.ReturnToMainMenu();
        }

        void Quit()
        {
            var flow = SceneFlowManager.EnsureExists();
            if (flow != null) flow.QuitGame(); else Application.Quit();
        }
    }
}
