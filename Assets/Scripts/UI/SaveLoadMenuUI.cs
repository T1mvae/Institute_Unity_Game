using UnityEngine;
using UnityEngine.UI;

public class SaveLoadMenuUI : MonoBehaviour
{
    [SerializeField] private RectTransform panel;

    void Start()
    {
        if (panel == null)
            BuildPanel();
    }

    void BuildPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
            return;

        GameObject panelObject = new GameObject("Save Load Panel", typeof(RectTransform));
        panelObject.transform.SetParent(canvas.transform, false);
        panel = panelObject.transform as RectTransform;
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-18f, 104f);
        panel.sizeDelta = new Vector2(340f, 46f);

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.043f, 0.085f, 0.92f);

        HorizontalLayoutGroup layout = panelObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateButton("Save Game", () => SaveLoadManager.Instance?.TrySaveGame(SaveLoadManager.ManualSaveSlot));
        CreateButton("Load Game", () => SaveLoadManager.Instance?.TryLoadGame(SaveLoadManager.ManualSaveSlot));
        CreateButton("Autosave", () => SaveLoadManager.Instance?.TrySaveGame(SaveLoadManager.AutoSaveSlot));
    }

    void CreateButton(string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform));
        buttonObject.transform.SetParent(panel, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = UITheme.ButtonNormal;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        buttonObject.AddComponent<StyledButton>();

        Text text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = text.transform as RectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 12;
        text.color = UITheme.TextPrimary;
        text.raycastTarget = false;
    }
}
