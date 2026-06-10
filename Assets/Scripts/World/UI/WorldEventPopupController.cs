using Institute.World.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Institute.World.UI
{
    /// <summary>
    /// UI Toolkit event popup (EventPopup.uxml). Subscribed to <see cref="RegionEventSystem.EventReady"/>;
    /// renders the title/body/affected region+character and one button per choice (disabled choices
    /// stay visible but non-interactable). Choosing runs the choice's Apply() then closes. Modal: it
    /// pauses time while open so the world doesn't advance under the player.
    /// </summary>
    public class WorldEventPopupController : MonoBehaviour
    {
        public static WorldEventPopupController Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        Label _title, _body;
        VisualElement _options;
        float _prevTimeScale = 1f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Build();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            _doc = OverlayUtil.CreateDocument(gameObject, 250);
            _root = _doc.rootVisualElement;
            _root.Clear();
            OverlayUtil.AddStyles(_root, "UI/Styles/base", "UI/Styles/popups");

            VisualTreeAsset uxml = OverlayUtil.LoadUxml("UI/UXML/EventPopup");
            if (uxml != null) uxml.CloneTree(_root); else BuildFallback(_root);

            _title = _root.Q<Label>("event-title");
            _body = _root.Q<Label>("event-body");
            _options = _root.Q<VisualElement>("event-options");
            OverlayUtil.ApplyOverlayLayout(_root, "event-root");
            SetVisible(false);
        }

        void BuildFallback(VisualElement root)
        {
            var overlay = new VisualElement { name = "event-root" }; overlay.AddToClassList("overlay");
            var dialog = new VisualElement(); dialog.AddToClassList("dialog"); dialog.AddToClassList("dialog-wide");
            dialog.Add(new Label("INSTITUTE EVENT") { name = "event-title" }.WithClass("dialog-title"));
            dialog.Add(new Label("") { name = "event-body" }.WithClass("dialog-body"));
            var options = new VisualElement { name = "event-options" }; options.AddToClassList("dialog-options");
            dialog.Add(options);
            overlay.Add(dialog); root.Add(overlay);
        }

        public void Show(EventPresentation presentation)
        {
            if (presentation == null) return;
            if (_title != null) _title.text = string.IsNullOrEmpty(presentation.title) ? "Institute Event" : presentation.title;

            string body = presentation.body ?? "";
            if (!string.IsNullOrEmpty(presentation.regionName)) body += $"\n\nRegion: {presentation.regionName}";
            if (!string.IsNullOrEmpty(presentation.characterName)) body += $"\n\nCharacter: {presentation.characterName}";
            body += $"\n\nScope: {presentation.scope}";
            if (_body != null) _body.text = body;

            if (_options != null)
            {
                _options.Clear();
                foreach (var choice in presentation.choices)
                {
                    EventChoice captured = choice;
                    var card = new VisualElement(); card.AddToClassList("action-card");
                    var label = new Label(captured.label); label.AddToClassList("action-title");
                    card.Add(label);
                    if (!string.IsNullOrEmpty(captured.detail))
                    {
                        var detail = new Label(captured.detail); detail.AddToClassList("action-desc");
                        card.Add(detail);
                    }
                    card.SetEnabled(captured.enabled);
                    if (captured.enabled)
                        card.RegisterCallback<ClickEvent>(_ => Choose(captured));
                    _options.Add(card);
                }
            }

            PlayerLog.Add("Event popup opened: " + presentation.title);
            PauseForModal(true);
            SetVisible(true);
        }

        void Choose(EventChoice choice)
        {
            PlayerLog.Add("Event choice selected: " + choice.label);
            choice.Apply?.Invoke();
            PauseForModal(false);
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            MapInteractionGate.PointerOverUI = visible;
        }

        void PauseForModal(bool pause)
        {
            // Don't fight an open pause menu.
            if (WorldPauseController.Instance != null && WorldPauseController.Instance.IsOpen) return;
            if (pause) { _prevTimeScale = Time.timeScale; Time.timeScale = 0f; }
            else { Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale; }
        }
    }

    static class VisualElementExtensions
    {
        public static Label WithClass(this Label label, string cls) { label.AddToClassList(cls); return label; }
    }
}
