using UnityEngine;

[RequireComponent(typeof(DecisionPanelUI))]
public class DecisionPanelController : MonoBehaviour
{
    DecisionPanelUI decisionPanel;

    void Awake()
    {
        decisionPanel = GetComponent<DecisionPanelUI>();
    }

    public void Refresh()
    {
        if (decisionPanel != null)
        {
            decisionPanel.enabled = false;
            decisionPanel.enabled = true;
        }
    }
}
