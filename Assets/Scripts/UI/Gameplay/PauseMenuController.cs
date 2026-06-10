using UnityEngine;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private Slider uiScaleSlider;

    Canvas rootCanvas;

    public void Build(Canvas canvas)
    {
        rootCanvas = canvas;
        if (overlay != null || rootCanvas == null)
            return;

        overlay = new GameObject("Pause Menu Overlay", typeof(RectTransform));
        overlay.transform.SetParent(rootCanvas.transform, false);
        RectTransform rect = overlay.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image scrim = overlay.AddComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, ThemeLoader.Current.opacity.overlay);

        GameObject card = new GameObject("Pause Menu Card", typeof(RectTransform));
        card.transform.SetParent(overlay.transform, false);
        RectTransform cardRect = card.transform as RectTransform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(420f, 420f);
        UITheme.StylePanel(card);

        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;

        AddLabel(card.transform, "PAUSED", 30, UITheme.TextHeader, FontStyle.Bold);
        AddButton(card.transform, "RESUME", Resume);
        AddButton(card.transform, "SAVE GAME", () => SaveLoadManager.Instance?.TrySaveGame(SaveLoadManager.ManualSaveSlot));
        AddButton(card.transform, "AUTOSAVE NOW", () => SaveLoadManager.Instance?.TrySaveGame(SaveLoadManager.AutoSaveSlot));
        AddScaleSlider(card.transform);
        AddButton(card.transform, "RETURN TO MAIN MENU", () => SceneFlowManager.EnsureExists().ReturnToMainMenu());

        overlay.SetActive(false);
    }

    public void Toggle()
    {
        SetVisible(overlay == null || !overlay.activeSelf);
    }

    public void SetVisible(bool visible)
    {
        if (overlay == null)
            return;

        overlay.SetActive(visible);
        if (LevelController.Instance != null)
            LevelController.Instance.SetPauseFromUI(visible);
    }

    void Resume()
    {
        SetVisible(false);
    }

    void AddScaleSlider(Transform parent)
    {
        GameObject row = new GameObject("UI Scale Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 48f;
        HorizontalLayoutGroup group = row.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 10;
        group.childControlWidth = true;
        group.childForceExpandWidth = false;

        AddLabel(row.transform, "UI SCALE", 13, UITheme.TextSecondary, FontStyle.Normal, 100f);
        GameObject sliderObject = new GameObject("UI Scale Slider", typeof(RectTransform));
        sliderObject.transform.SetParent(row.transform, false);
        uiScaleSlider = sliderObject.AddComponent<Slider>();
        uiScaleSlider.minValue = 0.85f;
        uiScaleSlider.maxValue = 1.15f;
        uiScaleSlider.value = 1f;
        uiScaleSlider.onValueChanged.AddListener(value =>
        {
            if (rootCanvas != null)
                rootCanvas.transform.localScale = Vector3.one * value;
        });
        LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
        sliderLayout.flexibleWidth = 1f;
    }

    Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        UITheme.StyleButton(button);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 44f;
        AddLabel(buttonObject.transform, label, 14, UITheme.TextPrimary, FontStyle.Bold);
        return button;
    }

    Text AddLabel(Transform parent, string text, int size, Color color, FontStyle style, float preferredWidth = -1f)
    {
        GameObject labelObject = new GameObject(text + " Label", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        Text label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.preferredHeight = size + 16;
        if (preferredWidth > 0f)
            layout.preferredWidth = preferredWidth;
        return label;
    }
}
