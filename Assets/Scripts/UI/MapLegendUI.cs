using UnityEngine;
using UnityEngine.UI;

public class MapLegendUI : MonoBehaviour
{
    [SerializeField] private RectTransform panel;

    void Start()
    {
        if (panel == null)
            BuildLegend();
    }

    void BuildLegend()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        GameObject panelObject = new GameObject("Map Legend", typeof(RectTransform));
        panelObject.transform.SetParent(canvas.transform, false);
        panel = panelObject.transform as RectTransform;
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 0f);
        panel.pivot = new Vector2(0f, 0f);
        panel.anchoredPosition = new Vector2(18f, 96f);
        panel.sizeDelta = new Vector2(280f, 138f);

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.043f, 0.085f, 0.9f);

        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 5;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddLabel("MAP LEGEND", UITheme.AccentPrimary, FontStyle.Bold);
        AddLabel("Default: region type", UITheme.TextSecondary, FontStyle.Normal);
        AddLabel("Influence: dark blue → bright blue", UITheme.BarInfluence, FontStyle.Normal);
        AddLabel("Stability: dark green → bright green", UITheme.BarStability, FontStyle.Normal);
        AddLabel("Development: brown → amber", UITheme.BarDevelopment, FontStyle.Normal);
    }

    void AddLabel(string content, Color color, FontStyle style)
    {
        GameObject labelObject = new GameObject("Legend Label", typeof(RectTransform));
        labelObject.transform.SetParent(panel, false);
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 20f;
        Text text = labelObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = 12;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
    }
}
