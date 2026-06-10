using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    public static SceneFlowManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject flowObject = new GameObject("Scene Flow Manager");
        return flowObject.AddComponent<SceneFlowManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ServiceLocator.Register<SceneFlowManager>(this);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<SceneFlowManager>();
            Instance = null;
        }
    }

    public void LoadBoot() => LoadScene(InstituteSceneNames.Boot);
    public void GoToMainMenu() => LoadScene(InstituteSceneNames.MainMenu);
    public void GoToNewGameSetup() => LoadScene(InstituteSceneNames.NewGameSetup);

    public void StartNewGame(NewGameSettings settings)
    {
        GameSession.StartNewGame(settings);
        LoadGameplayThroughLoading();
    }

    public void ContinueGame()
    {
        LoadGame(SaveLoadManager.AutoSaveSlot);
    }

    public void LoadGame(string slot)
    {
        GameSession.RequestLoadGame(slot);
        LoadGameplayThroughLoading();
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        GameSession.ClearLoadRequest();
        LoadScene(InstituteSceneNames.MainMenu);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Quit requested from SceneFlowManager.");
#else
        Application.Quit();
#endif
    }

    void LoadGameplayThroughLoading()
    {
        GameSession.SetPendingScene(InstituteSceneNames.Gameplay);
        if (Application.CanStreamedLevelBeLoaded(InstituteSceneNames.Loading))
            LoadScene(InstituteSceneNames.Loading);
        else
            LoadScene(InstituteSceneNames.Gameplay);
    }

    void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName) && sceneName != SceneManager.GetActiveScene().name)
        {
            Debug.LogWarning($"Scene '{sceneName}' is not in Build Settings. Attempting direct load for editor workflow.");
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
