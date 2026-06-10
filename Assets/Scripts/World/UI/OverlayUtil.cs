using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Institute.World.UI
{
    /// <summary>
    /// Helpers for full-screen UI Toolkit overlays (pause/event/settings). All overlays share the one
    /// themed PanelSettings (from <see cref="UIToolkitThemeUtility"/>) and are layered via each
    /// <see cref="UIDocument.sortingOrder"/> — HUD 0, Pause 200, Event 250, Settings 300 — so there is a
    /// single panel with a valid Theme Style Sheet rather than many competing untheme'd panels.
    /// Overlay layout is forced inline so a popup is always centered even if USS fails to load.
    /// </summary>
    public static class OverlayUtil
    {
        public static UIDocument CreateDocument(GameObject host, int sortingOrder)
        {
            UIToolkitThemeUtility.EnsureEventSystem();
            UIToolkitThemeUtility.EnsureCamera();
            UIDocument doc = host.GetComponent<UIDocument>();
            if (doc == null) doc = host.AddComponent<UIDocument>();
            if (doc.panelSettings == null) doc.panelSettings = UIToolkitThemeUtility.GetOrCreatePanelSettings();
            doc.sortingOrder = sortingOrder;
            return doc;
        }

        // UI assets live under Assets/Resources/UI so they are baked into the standalone build.
        // Loads from Resources first (works in editor AND player); editor AssetDatabase is only a
        // dev-time fallback. Accepts either a Resources-relative path or an "Assets/.../X.uxml" path.
        public static VisualTreeAsset LoadUxml(string assetPath)
        {
            string resourcePath = CleanResourcePath(assetPath);
            VisualTreeAsset asset = Resources.Load<VisualTreeAsset>(resourcePath);
#if UNITY_EDITOR
            if (asset == null) asset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
#endif
            return asset;
        }

        public static StyleSheet LoadStyle(string assetPath)
        {
            string resourcePath = CleanResourcePath(assetPath);
            StyleSheet sheet = Resources.Load<StyleSheet>(resourcePath);
#if UNITY_EDITOR
            if (sheet == null) sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
#endif
            return sheet;
        }

        public static void AddStyles(VisualElement root, params string[] ussPaths)
        {
            foreach (string p in ussPaths)
            {
                StyleSheet sheet = LoadStyle(p);
                if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            }
        }

        /// <summary>
        /// Normalizes an asset path to a Resources-relative, extensionless key:
        /// ".../Resources/UI/Styles/base.uss" or "Assets/UI/Styles/base.uss" -> "UI/Styles/base".
        /// </summary>
        public static string CleanResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string cleaned = path.Replace("\\", "/");
            int idx = cleaned.IndexOf("/Resources/");
            if (idx >= 0) cleaned = cleaned.Substring(idx + 11);
            else if (cleaned.StartsWith("Assets/")) cleaned = cleaned.Substring(7);
            int dot = cleaned.LastIndexOf('.');
            if (dot >= 0) cleaned = cleaned.Substring(0, dot);
            return cleaned;
        }

        public static Button Button(string text, string cls, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            if (!string.IsNullOrEmpty(cls)) b.AddToClassList(cls);
            b.style.marginTop = 6;
            return b;
        }

        /// <summary>
        /// Forces a full-screen, dimmed, centered overlay layout via inline styles so the dialog is
        /// always centered regardless of whether the USS classes loaded. Applies to the named overlay
        /// element if present, otherwise to the panel root.
        /// </summary>
        public static void ApplyOverlayLayout(VisualElement root, string overlayName)
        {
            VisualElement overlay = !string.IsNullOrEmpty(overlayName) ? root.Q<VisualElement>(overlayName) : null;
            if (overlay == null) overlay = root.childCount > 0 ? root[0] : root;

            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.alignItems = Align.Center;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);

            // Give the dialog a readable fallback background if USS didn't supply one.
            VisualElement dialog = overlay.Q(className: "dialog");
            if (dialog != null)
            {
                if (dialog.resolvedStyle.width <= 1f) dialog.style.minWidth = 420;
                dialog.style.paddingLeft = 24; dialog.style.paddingRight = 24;
                dialog.style.paddingTop = 24; dialog.style.paddingBottom = 24;
                dialog.style.backgroundColor = new Color(0.07f, 0.09f, 0.15f, 0.98f);
                dialog.style.borderTopLeftRadius = 14; dialog.style.borderTopRightRadius = 14;
                dialog.style.borderBottomLeftRadius = 14; dialog.style.borderBottomRightRadius = 14;
                dialog.style.borderLeftWidth = 1; dialog.style.borderRightWidth = 1;
                dialog.style.borderTopWidth = 1; dialog.style.borderBottomWidth = 1;
                Color border = new Color(0.55f, 0.36f, 0.96f, 1f);
                dialog.style.borderLeftColor = border; dialog.style.borderRightColor = border;
                dialog.style.borderTopColor = border; dialog.style.borderBottomColor = border;
            }
        }
    }
}
