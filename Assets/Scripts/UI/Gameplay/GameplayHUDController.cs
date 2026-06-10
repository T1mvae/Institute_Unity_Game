using UnityEngine;
using UnityEngine.UI;

public class GameplayHUDController : MonoBehaviour
{
    [SerializeField] private Text moneyText;
    [SerializeField] private Text artifactsText;
    [SerializeField] private Text sanityText;
    [SerializeField] private Text dateText;

    int lastMoney = int.MinValue;
    int lastArtifacts = int.MinValue;
    int lastSanity = int.MinValue;
    float lastElapsed = -1f;

    public void Bind(Text money, Text artifacts, Text sanity, Text date)
    {
        moneyText = money;
        artifactsText = artifacts;
        sanityText = sanity;
        dateText = date;
        Refresh(force: true);
    }

    void Update()
    {
        Refresh(force: false);
    }

    void Refresh(bool force)
    {
        LevelController controller = LevelController.Instance;
        if (controller != null)
        {
            if (force || controller.Money != lastMoney)
            {
                lastMoney = controller.Money;
                if (moneyText != null)
                    moneyText.text = controller.Money.ToString();
            }

            if (force || controller.Artifacts != lastArtifacts)
            {
                lastArtifacts = controller.Artifacts;
                if (artifactsText != null)
                    artifactsText.text = controller.Artifacts.ToString();
            }

            if (force || controller.Sanity != lastSanity)
            {
                lastSanity = controller.Sanity;
                if (sanityText != null)
                    sanityText.text = controller.Sanity.ToString();
            }
        }

        float elapsed = TimeManager.Instance != null ? TimeManager.Instance.ElapsedTime : Time.time;
        if (force || Mathf.Abs(elapsed - lastElapsed) > 0.5f)
        {
            lastElapsed = elapsed;
            if (dateText != null)
            {
                if (GameDateTracker.Instance != null)
                    dateText.text = GameDateTracker.Instance.FormatDateLabel(elapsed);
                else
                    dateText.text = $"T+{Mathf.FloorToInt(elapsed)}";
            }
        }
    }
}
