using UnityEngine;
using UnityEngine.UIElements;

public class SettingsUIController : MonoBehaviour
{
    VisualElement root;
    Slider volumeSlider;
    Slider uiScaleSlider;
    Toggle fullscreenToggle;

    public bool IsVisible => root != null && !root.ClassListContains("hidden");

    public void AttachTo(VisualElement parent)
    {
        if (parent == null)
            return;

        if (root != null && root.parent == parent)
            return;

        root = new VisualElement { name = "SettingsRoot" };
        root.AddToClassList("popup-scrim");
        root.AddToClassList("hidden");

        VisualElement card = UIToolkitThemeUtility.Panel("popup-card");
        card.Add(UIToolkitThemeUtility.Label("SETTINGS", "header"));
        card.Add(UIToolkitThemeUtility.Label("Prototype options. These are wired as safe placeholders until platform settings are finalized.", "body-copy"));

        volumeSlider = new Slider("Master Volume", 0f, 100f) { value = 80f };
        volumeSlider.AddToClassList("input-field");
        card.Add(volumeSlider);

        uiScaleSlider = new Slider("UI Scale", 80f, 120f) { value = 100f };
        uiScaleSlider.AddToClassList("input-field");
        uiScaleSlider.RegisterValueChangedCallback(evt => parent.style.scale = new Scale(new Vector3(evt.newValue / 100f, evt.newValue / 100f, 1f)));
        card.Add(uiScaleSlider);

        fullscreenToggle = new Toggle("Fullscreen placeholder") { value = Screen.fullScreen };
        fullscreenToggle.RegisterValueChangedCallback(evt => Screen.fullScreen = evt.newValue);
        card.Add(fullscreenToggle);

        card.Add(UIToolkitThemeUtility.Label("Theme: InstituteDark. Future selector will load another theme JSON file.", "meta-label"));
        card.Add(UIToolkitThemeUtility.Button("BACK", Hide));
        root.Add(card);
        parent.Add(root);
    }

    public void Show()
    {
        if (root != null)
            root.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        if (root != null)
            root.AddToClassList("hidden");
    }
}
