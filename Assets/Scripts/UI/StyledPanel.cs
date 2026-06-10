using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any UI panel to auto-apply the Institute theme styling
/// and optionally animate it on/off-screen with a slide transition.
/// </summary>
[RequireComponent(typeof(Image))]
public class StyledPanel : MonoBehaviour
{
    public enum SlideDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }

    [Header("Theme")]
    [Tooltip("Apply the PanelBorder outline.")]
    public bool showBorder = true;

    [Header("Animation")]
    [Tooltip("Play a slide-in animation when the panel becomes active.")]
    public bool animateOnShow = true;

    [Tooltip("Direction the panel slides from / to.")]
    public SlideDirection slideDirection = SlideDirection.Left;

    [Tooltip("Default slide duration in seconds.")]
    public float defaultSlideDuration = 0.3f;

    RectTransform rectTransform;
    Coroutine activeSlide;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        UITheme.StylePanel(gameObject);

        if (!showBorder)
        {
            Outline outline = GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
        }

        if (animateOnShow)
            SlideIn(defaultSlideDuration);
    }

    // ───────────────────── Public API ─────────────────────

    /// <summary>
    /// Animates the panel sliding in from off-screen.
    /// </summary>
    public void SlideIn(float duration = 0.3f)
    {
        EnsureRect();
        StopActiveSlide();
        activeSlide = StartCoroutine(SlideCoroutine(GetOffscreenOffset(), Vector2.zero, duration));
    }

    /// <summary>
    /// Animates the panel sliding out off-screen.
    /// </summary>
    public void SlideOut(float duration = 0.3f)
    {
        EnsureRect();
        StopActiveSlide();
        activeSlide = StartCoroutine(SlideCoroutine(Vector2.zero, GetOffscreenOffset(), duration));
    }

    // ───────────────────── Internals ─────────────────────

    void EnsureRect()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
    }

    void StopActiveSlide()
    {
        if (activeSlide != null)
        {
            StopCoroutine(activeSlide);
            activeSlide = null;
        }
    }

    /// <summary>
    /// Returns an anchored-position offset large enough to place the panel
    /// fully off-screen in the configured <see cref="slideDirection"/>.
    /// </summary>
    Vector2 GetOffscreenOffset()
    {
        // Use the rect's own dimensions as the displacement distance.
        float width  = rectTransform.rect.width;
        float height = rectTransform.rect.height;

        switch (slideDirection)
        {
            case SlideDirection.Left:   return new Vector2(-width,  0f);
            case SlideDirection.Right:  return new Vector2(width,   0f);
            case SlideDirection.Top:    return new Vector2(0f,  height);
            case SlideDirection.Bottom: return new Vector2(0f, -height);
            default:                    return new Vector2(-width,  0f);
        }
    }

    IEnumerator SlideCoroutine(Vector2 from, Vector2 to, float duration)
    {
        if (duration <= 0f)
        {
            rectTransform.anchoredPosition = to;
            activeSlide = null;
            yield break;
        }

        float elapsed = 0f;
        rectTransform.anchoredPosition = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth-step easing for a polished feel.
            float eased = t * t * (3f - 2f * t);
            rectTransform.anchoredPosition = Vector2.Lerp(from, to, eased);
            yield return null;
        }

        rectTransform.anchoredPosition = to;
        activeSlide = null;
    }
}
