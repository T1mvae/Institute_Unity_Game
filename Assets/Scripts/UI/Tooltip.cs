using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class Tooltip : MonoBehaviour
{
    public static Tooltip Instance;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text tooltipText;
    [SerializeField] Text legacyTooltipText;
    [SerializeField] Vector2 screenOffset = new Vector2(18f, -18f);
    [SerializeField] float hideDelay = 0.08f;

    RectTransform panelRect;
    RectTransform canvasRect;
    Coroutine hideRoutine;

    void Awake()
    {
        Instance = this;
        if (panel != null)
            panelRect = panel.transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvasRect = canvas.transform as RectTransform;
        if (panel != null)
            panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (panel != null && panel.activeSelf)
            PositionTooltip();
    }

    public void Show(string message)
    {
        if (panel == null || tooltipText == null)
        {
            if (panel == null || legacyTooltipText == null)
                return;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (tooltipText != null)
            tooltipText.text = message;
        if (legacyTooltipText != null)
            legacyTooltipText.text = message;

        PositionTooltip();
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel == null || !panel.activeSelf)
            return;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hideDelay));
        if (panel != null)
            panel.SetActive(false);
        hideRoutine = null;
    }

    void PositionTooltip()
    {
        if (panel == null)
            return;

        if (panelRect == null)
            panelRect = panel.transform as RectTransform;

        Vector2 screenPosition = (Vector2)Input.mousePosition + screenOffset;

        if (panelRect == null || canvasRect == null)
        {
            panel.transform.position = screenPosition;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint);
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 tooltipSize = panelRect.rect.size;

        float minX = -canvasSize.x * 0.5f + tooltipSize.x * panelRect.pivot.x;
        float maxX = canvasSize.x * 0.5f - tooltipSize.x * (1f - panelRect.pivot.x);
        float minY = -canvasSize.y * 0.5f + tooltipSize.y * panelRect.pivot.y;
        float maxY = canvasSize.y * 0.5f - tooltipSize.y * (1f - panelRect.pivot.y);

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);
        panelRect.anchoredPosition = localPoint;
    }
}
