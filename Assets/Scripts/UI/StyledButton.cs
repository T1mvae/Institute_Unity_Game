using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Themed button that applies UITheme styling on enable,
/// shows a cyan glow outline on hover, and supports a
/// cooldown-fill overlay and disabled visual state.
/// </summary>
[RequireComponent(typeof(Button), typeof(Image))]
public class StyledButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("Optional label child – located automatically if left empty.")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Text legacyLabel;

    [Header("Cooldown Overlay")]
    [Tooltip("Optional Image used as a fill overlay to indicate cooldown progress. " +
             "Should be set to Image.Type.Filled with Fill Method = Vertical.")]
    [SerializeField] private Image cooldownOverlay;

    Button button;
    Outline hoverOutline;
    bool isDisabledVisual;

    void Awake()
    {
        button = GetComponent<Button>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>();
        if (legacyLabel == null)
            legacyLabel = GetComponentInChildren<Text>();
    }

    void OnEnable()
    {
        UITheme.StyleButton(button);

        if (label != null)
            UITheme.StyleText(label, false);
        if (legacyLabel != null)
            UITheme.StyleText(legacyLabel, false);

        // Make sure a previous hover outline is cleaned up.
        RemoveHoverGlow();
    }

    void OnDisable()
    {
        RemoveHoverGlow();
    }

    // ───────────────────── Pointer Events ─────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDisabledVisual)
            return;

        ApplyHoverGlow();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RemoveHoverGlow();
    }

    // ───────────────────── Public API ─────────────────────

    /// <summary>
    /// Updates the cooldown overlay fill amount.
    /// <paramref name="normalizedCooldown"/> should be 0 (no cooldown) to 1 (full cooldown).
    /// </summary>
    public void SetCooldownOverlay(float normalizedCooldown)
    {
        if (cooldownOverlay == null)
            return;

        normalizedCooldown = Mathf.Clamp01(normalizedCooldown);

        if (normalizedCooldown <= 0f)
        {
            cooldownOverlay.gameObject.SetActive(false);
            return;
        }

        if (!cooldownOverlay.gameObject.activeSelf)
            cooldownOverlay.gameObject.SetActive(true);

        cooldownOverlay.fillAmount = normalizedCooldown;
    }

    /// <summary>
    /// Dims or restores the button and its label to indicate a disabled state
    /// that is separate from <see cref="Button.interactable"/>.
    /// </summary>
    public void SetDisabledVisual(bool disabled)
    {
        isDisabledVisual = disabled;

        Image image = GetComponent<Image>();
        if (image != null)
            image.color = disabled ? UITheme.ButtonDisabled : Color.white;

        if (label != null)
            label.color = disabled ? UITheme.TextSecondary : UITheme.TextPrimary;
        if (legacyLabel != null)
            legacyLabel.color = disabled ? UITheme.TextSecondary : UITheme.TextPrimary;

        if (disabled)
            RemoveHoverGlow();
    }

    /// <summary>
    /// Returns the button's label text component (may be null).
    /// </summary>
    public TMP_Text Label => label;
    public Text LegacyLabel => legacyLabel;

    // ───────────────────── Internals ─────────────────────

    void ApplyHoverGlow()
    {
        if (hoverOutline == null)
        {
            hoverOutline = gameObject.AddComponent<Outline>();
        }

        hoverOutline.effectColor    = UITheme.AccentPrimary;
        hoverOutline.effectDistance  = new Vector2(2f, -2f);
        hoverOutline.useGraphicAlpha = false;
        hoverOutline.enabled = true;
    }

    void RemoveHoverGlow()
    {
        if (hoverOutline != null)
        {
            hoverOutline.enabled = false;
        }
    }
}
