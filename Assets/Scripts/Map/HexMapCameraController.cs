using UnityEngine;
using UnityEngine.EventSystems;

public class HexMapCameraController : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private float panSpeed = 1f;
    [SerializeField] private float zoomStep = 0.12f;
    [SerializeField] private float minZoom = 0.65f;
    [SerializeField] private float maxZoom = 2.2f;
    [SerializeField] private Vector2 panClamp = new Vector2(900f, 650f);

    Vector2 lastMousePosition;
    bool isPanning;

    void Awake()
    {
        if (target == null)
            target = transform as RectTransform;
        if (viewport == null && target != null)
            viewport = target.parent as RectTransform;
    }

    void Update()
    {
        if (target == null)
            return;

        HandleZoom();
        HandlePan();
    }

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        if (IsPointerOverBlockingUI())
            return;

        float nextScale = Mathf.Clamp(target.localScale.x + scroll * zoomStep, minZoom, maxZoom);
        target.localScale = Vector3.one * nextScale;
        ClampPan();
    }

    void HandlePan()
    {
        bool panButtonDown = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        bool panButtonHeld = Input.GetMouseButton(1) || Input.GetMouseButton(2);

        if (panButtonDown && !IsPointerOverBlockingUI())
        {
            isPanning = true;
            lastMousePosition = Input.mousePosition;
        }

        if (!panButtonHeld)
        {
            isPanning = false;
            return;
        }

        if (!isPanning)
            return;

        Vector2 currentMousePosition = Input.mousePosition;
        Vector2 delta = (currentMousePosition - lastMousePosition) * panSpeed;
        lastMousePosition = currentMousePosition;

        target.anchoredPosition += delta;
        ClampPan();
    }

    void ClampPan()
    {
        Vector2 clamp = panClamp * Mathf.Max(1f, target.localScale.x);
        Vector2 position = target.anchoredPosition;
        position.x = Mathf.Clamp(position.x, -clamp.x, clamp.x);
        position.y = Mathf.Clamp(position.y, -clamp.y, clamp.y);
        target.anchoredPosition = position;
    }

    bool IsPointerOverBlockingUI()
    {
        EventSystem current = EventSystem.current;
        if (current == null || !current.IsPointerOverGameObject())
            return false;

        if (viewport == null)
            return true;

        return !RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, null);
    }
}
