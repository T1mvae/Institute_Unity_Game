using UnityEngine;
using UnityEngine.UI;

public class ReturnToMenu : MonoBehaviour
{
    [SerializeField] Button menuButton;

    void Awake()
    {
        if (menuButton == null)
            menuButton = GetComponent<Button>();

        if (menuButton != null)
            menuButton.onClick.AddListener(GoToMainMenu);
    }

    void OnDestroy()
    {
        if (menuButton != null)
            menuButton.onClick.RemoveListener(GoToMainMenu);
    }

    void GoToMainMenu()
    {
        SceneFlowManager.EnsureExists().ReturnToMainMenu();
    }
}
