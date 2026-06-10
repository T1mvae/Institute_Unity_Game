using UnityEngine;
using UnityEngine.UIElements;

namespace Institute.World.UI
{
    /// <summary>
    /// UI Toolkit settings overlay (Settings.uxml). Openable from the pause menu (and reusable from
    /// the main menu). Persists to PlayerPrefs. UI scale adjusts the shared HUD/menu PanelSettings
    /// reference resolution; volume sets AudioListener.volume; the grid toggle is a stored placeholder.
    /// </summary>
    public class WorldSettingsController : MonoBehaviour
    {
        public static WorldSettingsController Instance { get; private set; }

        const string KeyVolume = "inst_volume";
        const string KeyUiScale = "inst_uiscale";
        const string KeyGrid = "inst_grid";

        UIDocument _doc;
        VisualElement _root;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Build();
            ApplySavedSettings();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            _doc = OverlayUtil.CreateDocument(gameObject, 300);
            _root = _doc.rootVisualElement;
            _root.Clear();
            OverlayUtil.AddStyles(_root, "UI/Styles/base", "UI/Styles/popups");

            VisualTreeAsset uxml = OverlayUtil.LoadUxml("UI/UXML/Settings");
            if (uxml != null) uxml.CloneTree(_root); else BuildFallback(_root);

            Bind(_root);
            OverlayUtil.ApplyOverlayLayout(_root, "settings-root");
            SetVisible(false);
        }

        void Bind(VisualElement root)
        {
            var volume = root.Q<Slider>("slider-volume");
            if (volume != null)
            {
                volume.value = PlayerPrefs.GetFloat(KeyVolume, 0.8f);
                volume.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat(KeyVolume, e.newValue); AudioListener.volume = e.newValue; });
            }
            var uiScale = root.Q<Slider>("slider-uiscale");
            if (uiScale != null)
            {
                uiScale.value = PlayerPrefs.GetFloat(KeyUiScale, 1f);
                uiScale.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat(KeyUiScale, e.newValue); ApplyUiScale(e.newValue); });
            }
            var grid = root.Q<Toggle>("toggle-grid");
            if (grid != null)
            {
                grid.value = PlayerPrefs.GetInt(KeyGrid, 1) == 1;
                grid.RegisterValueChangedCallback(e => PlayerPrefs.SetInt(KeyGrid, e.newValue ? 1 : 0));
            }
            var close = root.Q<Button>("btn-close");
            if (close != null) close.clicked += Close;
        }

        void BuildFallback(VisualElement root)
        {
            var overlay = new VisualElement { name = "settings-root" }; overlay.AddToClassList("overlay");
            var dialog = new VisualElement(); dialog.AddToClassList("dialog");
            var title = new Label("SETTINGS"); title.AddToClassList("dialog-title"); dialog.Add(title);
            AddRow(dialog, "Master Volume", new Slider(0f, 1f) { name = "slider-volume", value = 0.8f });
            AddRow(dialog, "UI Scale", new Slider(0.75f, 1.5f) { name = "slider-uiscale", value = 1f });
            AddRow(dialog, "Show Tile Grid", new Toggle { name = "toggle-grid", value = true });
            var close = new Button { name = "btn-close", text = "Close" }; close.AddToClassList("btn"); close.AddToClassList("btn-primary");
            dialog.Add(close);
            overlay.Add(dialog); root.Add(overlay);
        }

        static void AddRow(VisualElement parent, string label, VisualElement control)
        {
            var row = new VisualElement(); row.AddToClassList("form-row");
            var l = new Label(label); l.AddToClassList("form-label");
            control.AddToClassList("form-control");
            row.Add(l); row.Add(control); parent.Add(row);
        }

        public void Open() { ApplySavedSettings(); SetVisible(true); }
        public void Close() { SetVisible(false); }

        void SetVisible(bool visible)
        {
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) MapInteractionGate.PointerOverUI = true;
        }

        void ApplySavedSettings()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            ApplyUiScale(PlayerPrefs.GetFloat(KeyUiScale, 1f));
        }

        static void ApplyUiScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.75f, 1.5f);
            // Larger scale -> smaller reference resolution -> bigger UI, under ScaleWithScreenSize.
            PanelSettings ps = UIToolkitThemeUtility.GetOrCreatePanelSettings();
            if (ps != null)
                ps.referenceResolution = new Vector2Int(Mathf.RoundToInt(1920f / scale), Mathf.RoundToInt(1080f / scale));
        }
    }
}
