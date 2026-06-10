using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Central compatibility facade for legacy uGUI screens.
/// Values come from Assets/Data/UI/theme.json through ThemeLoader so old Canvas UI and new UI Toolkit screens share one design source.
/// </summary>
public static class UITheme
{
    static UIThemeConfig Theme => ThemeLoader.Current;

    public static Color PanelBackground => Theme.Panel;
    public static Color PanelAlt => Theme.PanelAlt;
    public static Color PanelBorder => Theme.PanelBorder;

    public static Color AccentPrimary => Theme.AccentGlow;
    public static Color AccentSecondary => Theme.Warning;
    public static Color AccentDanger => Theme.Danger;

    public static Color TextPrimary => Theme.TextPrimary;
    public static Color TextSecondary => Theme.TextSecondary;
    public static Color TextHeader => Theme.Accent;

    public static Color ButtonNormal => Theme.Button;
    public static Color ButtonHover => Theme.ButtonHover;
    public static Color ButtonDisabled => Theme.ButtonDisabled;

    public static Color BackgroundDark => Theme.Background;

    public static Color BarInfluence => new Color(0.267f, 0.533f, 1f, 1f);
    public static Color BarStability => Theme.Success;
    public static Color BarDevelopment => Theme.Warning;

    public static Color MapHighlight => new Color(Theme.AccentGlow.r, Theme.AccentGlow.g, Theme.AccentGlow.b, 0.42f);

    public static float HeaderLarge => Theme.fontSizes.title;
    public static float HeaderMedium => Theme.fontSizes.body + 2;
    public static float Body => Theme.fontSizes.label;
    public static float Small => Theme.fontSizes.small;
    public static float Tiny => Theme.fontSizes.tiny;

    public static float PanelPadding => Theme.spacing.md;
    public static float ElementSpacing => Theme.spacing.sm;
    public static float BorderWidth => 1f;

    public static void StylePanel(GameObject panel)
    {
        if (panel == null)
            return;

        Image image = panel.GetComponent<Image>();
        if (image == null)
            image = panel.AddComponent<Image>();

        image.color = PanelBackground;
        image.raycastTarget = true;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
            outline = panel.AddComponent<Outline>();

        outline.effectColor = PanelBorder;
        outline.effectDistance = new Vector2(BorderWidth, -BorderWidth);
        outline.useGraphicAlpha = false;
    }

    public static void StyleButton(Button button)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = Theme.ButtonPressed;
        colors.selectedColor = ButtonHover;
        colors.disabledColor = ButtonDisabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = Theme.durations.fast;
        button.colors = colors;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = Color.white;
            image.type = Image.Type.Sliced;
        }
    }

    public static void StyleText(TMP_Text text, bool isHeader = false)
    {
        if (text == null)
            return;

        text.color = isHeader ? TextHeader : TextPrimary;
        text.fontSize = isHeader ? HeaderMedium : Body;
    }

    public static void StyleText(Text text, bool isHeader = false)
    {
        if (text == null)
            return;

        text.color = isHeader ? TextHeader : TextPrimary;
        text.fontSize = Mathf.RoundToInt(isHeader ? HeaderMedium : Body);
    }

    public static void CreateBorderEffect(RectTransform target)
    {
        if (target == null)
            return;

        Transform existing = target.Find("__ThemeBorder");
        if (existing != null)
            return;

        GameObject borderObject = new GameObject("__ThemeBorder");
        borderObject.transform.SetParent(target, false);

        RectTransform borderRect = borderObject.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;

        Image borderImage = borderObject.AddComponent<Image>();
        borderImage.color = new Color(0f, 0f, 0f, 0f);
        borderImage.raycastTarget = false;

        Outline borderOutline = borderObject.AddComponent<Outline>();
        borderOutline.effectColor = PanelBorder;
        borderOutline.effectDistance = new Vector2(BorderWidth, -BorderWidth);
        borderOutline.useGraphicAlpha = false;
    }

    public static Color GetStatBarColor(string statName)
    {
        if (string.IsNullOrEmpty(statName))
            return TextPrimary;

        switch (statName.ToLowerInvariant())
        {
            case "influence": return BarInfluence;
            case "stability": return BarStability;
            case "development": return BarDevelopment;
            default: return TextPrimary;
        }
    }
}
