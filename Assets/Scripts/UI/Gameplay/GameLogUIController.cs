using UnityEngine;

[RequireComponent(typeof(PlayerLogUI))]
public class GameLogUIController : MonoBehaviour
{
    PlayerLogUI logUI;

    void Awake()
    {
        logUI = GetComponent<PlayerLogUI>();
    }

    public void Refresh()
    {
        if (logUI != null)
            logUI.Refresh();
    }
}
