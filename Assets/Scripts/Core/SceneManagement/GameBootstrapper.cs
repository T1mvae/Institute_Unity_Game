using UnityEngine;

public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private bool loadMainMenuOnStart = true;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (GetComponent<ConfigLoader>() == null)
            gameObject.AddComponent<ConfigLoader>();
        if (SceneFlowManager.Instance == null)
            gameObject.AddComponent<SceneFlowManager>();
        if (SaveLoadManager.Instance == null)
            gameObject.AddComponent<SaveLoadManager>();

        // Ensure Boot has a camera so Unity doesn't report "Display 1 No cameras rendering"
        // before the transition to MainMenu.
        UIToolkitThemeUtility.EnsureCamera();
    }

    void Start()
    {
        if (loadMainMenuOnStart && SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.GoToMainMenu();
    }
}
