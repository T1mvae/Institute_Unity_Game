using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset layoutAsset;
    [SerializeField] private StyleSheet[] styleSheets;

    UIDocument document;
    Label statusLabel;

    IEnumerator Start()
    {
        ThemeLoader.LoadOrCreateDefault();
        SceneFlowManager.EnsureExists();
        LoadEditorAssetsIfNeeded();
        BuildUI();
        yield return null;

        string targetScene = GameSession.PendingSceneName;
        GameSession.ClearPendingScene();
        if (statusLabel != null)
            statusLabel.text = "Generating strategic console...";

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        while (operation != null && !operation.isDone)
        {
            if (statusLabel != null)
                statusLabel.text = $"Loading {targetScene}: {Mathf.RoundToInt(operation.progress * 100f)}%";
            yield return null;
        }
    }

    void LoadEditorAssetsIfNeeded()
    {
        // Resources-first so the loading screen is styled in the standalone build too.
        if (layoutAsset == null)
            layoutAsset = Institute.World.UI.OverlayUtil.LoadUxml("UI/UXML/Loading");
        if (styleSheets == null || styleSheets.Length == 0)
        {
            styleSheets = new[]
            {
                Institute.World.UI.OverlayUtil.LoadStyle("UI/Styles/base"),
                Institute.World.UI.OverlayUtil.LoadStyle("UI/Styles/main_menu")
            };
        }
    }

    void BuildUI()
    {
        document = UIToolkitThemeUtility.EnsureDocument(gameObject);
        VisualElement root = document.rootVisualElement;
        root.Clear();
        UIToolkitThemeUtility.ApplyRootTheme(root);
        AddStyleSheets(root);

        if (layoutAsset != null)
        {
            layoutAsset.CloneTree(root);
            statusLabel = root.Q<Label>("LoadingStatus");
            return;
        }

        root.AddToClassList("root-screen");
        root.AddToClassList("menu-root");
        VisualElement card = UIToolkitThemeUtility.Panel();
        card.Add(UIToolkitThemeUtility.Label("SYNCHRONIZING INSTITUTE CONSOLE", "section-title"));
        statusLabel = UIToolkitThemeUtility.Label("Loading strategic state...", "body-copy");
        card.Add(statusLabel);
        root.Add(card);
    }

    void AddStyleSheets(VisualElement root)
    {
        if (styleSheets == null)
            return;

        for (int i = 0; i < styleSheets.Length; i++)
        {
            if (styleSheets[i] != null && !root.styleSheets.Contains(styleSheets[i]))
                root.styleSheets.Add(styleSheets[i]);
        }
    }
}
