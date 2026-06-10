using System;
using System.IO;
using UnityEngine;

[Serializable]
public class UIThemeConfig
{
    public string themeName = "InstituteDark";
    public UIThemeColors colors = new UIThemeColors();
    public UIThemeSpacing spacing = new UIThemeSpacing();
    public UIThemeFontSizes fontSizes = new UIThemeFontSizes();
    public UIThemeRadii radii = new UIThemeRadii();
    public UIThemeOpacity opacity = new UIThemeOpacity();
    public UIThemeDurations durations = new UIThemeDurations();

    public Color ParseColor(string hex, Color fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
    }

    public Color Background => ParseColor(colors.background, new Color(0.031f, 0.043f, 0.071f, 1f));
    public Color BackgroundAlt => ParseColor(colors.backgroundAlt, new Color(0.051f, 0.071f, 0.125f, 1f));
    public Color Panel => ParseColor(colors.panel, new Color(0.071f, 0.094f, 0.149f, 0.90f));
    public Color PanelAlt => ParseColor(colors.panelAlt, new Color(0.094f, 0.125f, 0.20f, 0.90f));
    public Color PanelBorder => ParseColor(colors.panelBorder, new Color(0.165f, 0.212f, 0.333f, 1f));
    public Color Accent => ParseColor(colors.accent, new Color(0.545f, 0.361f, 0.965f, 1f));
    public Color AccentSoft => ParseColor(colors.accentSoft, new Color(0.427f, 0.294f, 0.847f, 1f));
    public Color AccentGlow => ParseColor(colors.accentGlow, new Color(0f, 0.831f, 0.667f, 1f));
    public Color TextPrimary => ParseColor(colors.textPrimary, Color.white);
    public Color TextSecondary => ParseColor(colors.textSecondary, new Color(0.667f, 0.698f, 0.773f, 1f));
    public Color Muted => ParseColor(colors.muted, new Color(0.42f, 0.447f, 0.502f, 1f));
    public Color Warning => ParseColor(colors.warning, new Color(0.961f, 0.773f, 0.259f, 1f));
    public Color Danger => ParseColor(colors.danger, new Color(0.937f, 0.267f, 0.267f, 1f));
    public Color Success => ParseColor(colors.success, new Color(0.133f, 0.773f, 0.369f, 1f));
    public Color Button => ParseColor(colors.button, new Color(0.102f, 0.141f, 0.251f, 1f));
    public Color ButtonHover => ParseColor(colors.buttonHover, new Color(0.149f, 0.212f, 0.373f, 1f));
    public Color ButtonPressed => ParseColor(colors.buttonPressed, new Color(0.192f, 0.275f, 0.49f, 1f));
    public Color ButtonDisabled => ParseColor(colors.buttonDisabled, new Color(0.082f, 0.106f, 0.169f, 1f));

    public static UIThemeConfig CreateDefault()
    {
        return new UIThemeConfig();
    }
}

[Serializable]
public class UIThemeColors
{
    public string background = "#080B12";
    public string backgroundAlt = "#0D1220";
    public string panel = "#121826E6";
    public string panelAlt = "#182033E6";
    public string panelBorder = "#2A3655";
    public string accent = "#8B5CF6";
    public string accentSoft = "#6D4BD8";
    public string accentGlow = "#00D4AA";
    public string textPrimary = "#F2F5FF";
    public string textSecondary = "#AAB2C5";
    public string muted = "#6B7280";
    public string warning = "#F5C542";
    public string danger = "#EF4444";
    public string success = "#22C55E";
    public string button = "#1A2440";
    public string buttonHover = "#26365F";
    public string buttonPressed = "#31467D";
    public string buttonDisabled = "#151B2B";
}

[Serializable]
public class UIThemeSpacing
{
    public int xs = 4;
    public int sm = 8;
    public int md = 16;
    public int lg = 24;
    public int xl = 32;
    public int xxl = 48;
}

[Serializable]
public class UIThemeFontSizes
{
    public int tiny = 10;
    public int small = 12;
    public int body = 16;
    public int label = 14;
    public int title = 28;
    public int header = 36;
    public int display = 52;
}

[Serializable]
public class UIThemeRadii
{
    public int sm = 4;
    public int md = 8;
    public int lg = 14;
    public int xl = 20;
}

[Serializable]
public class UIThemeOpacity
{
    public float panel = 0.9f;
    public float overlay = 0.72f;
    public float disabled = 0.42f;
    public float glow = 0.32f;
}

[Serializable]
public class UIThemeDurations
{
    public float fast = 0.08f;
    public float normal = 0.18f;
    public float slow = 0.32f;
}

public static class ThemeLoader
{
    public const string ThemeAssetPath = "Assets/Data/UI/theme.json";

    static UIThemeConfig current;
    public static UIThemeConfig Current => current ?? LoadOrCreateDefault();

    public static UIThemeConfig LoadOrCreateDefault()
    {
        string path = ResolveThemePath();
        try
        {
            if (!File.Exists(path))
            {
                current = UIThemeConfig.CreateDefault();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(current, true));
                return current;
            }

            string json = File.ReadAllText(path);
            UIThemeConfig parsed = JsonUtility.FromJson<UIThemeConfig>(json);
            current = parsed ?? UIThemeConfig.CreateDefault();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ThemeLoader: failed to load theme.json, using defaults. " + ex.Message);
            current = UIThemeConfig.CreateDefault();
        }

        return current;
    }

    public static bool ValidateTheme(out string message)
    {
        UIThemeConfig config = LoadOrCreateDefault();
        if (config == null)
        {
            message = "Theme failed to load.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.themeName))
        {
            message = "Theme has no themeName.";
            return false;
        }

        message = "Theme valid: " + config.themeName;
        return true;
    }

    static string ResolveThemePath()
    {
#if UNITY_EDITOR
        return Path.Combine(Directory.GetCurrentDirectory(), ThemeAssetPath);
#else
        return Path.Combine(Application.streamingAssetsPath, "theme.json");
#endif
    }
}
