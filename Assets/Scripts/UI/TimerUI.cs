using UnityEngine;

public class TimerUI : MonoBehaviour
{
    public static TimerUI Instance;
    public float timeElapsed = 0f;
    private bool timerIsRunning = true;

    private void Awake()
    {
        Instance = this;
    }
    void Update()
    {
        if (timerIsRunning)
        {
            timeElapsed += Time.deltaTime;
            // Просто передаём время менеджеру UI!
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateTimer(timeElapsed);
        }
    }

    public void SetElapsedTime(float elapsedTime)
    {
        timeElapsed = Mathf.Max(0f, elapsedTime);
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateTimer(timeElapsed);
    }
}
