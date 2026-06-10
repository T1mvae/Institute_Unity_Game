using UnityEngine;

public class RegionPanelController : MonoBehaviour
{
    public void Refresh()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.RefreshSelectedRegion();
    }
}
