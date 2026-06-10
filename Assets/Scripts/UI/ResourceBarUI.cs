using UnityEngine;
using TMPro;

/// <summary>
/// Top-of-screen HUD bar that displays Money, Sanity and Artifacts
/// with smooth number-lerping animation.
/// Reads live values from <see cref="LevelController"/> each frame.
/// </summary>
public class ResourceBarUI : MonoBehaviour
{
    [Header("Labels")]
    [SerializeField] private TMP_Text moneyLabel;
    [SerializeField] private TMP_Text sanityLabel;
    [SerializeField] private TMP_Text artifactsLabel;

    [Header("Values")]
    [SerializeField] private TMP_Text moneyValueText;
    [SerializeField] private TMP_Text sanityValueText;
    [SerializeField] private TMP_Text artifactsValueText;

    [Header("Animation")]
    [Tooltip("Speed of the number-lerp (higher = snappier).")]
    [SerializeField] private float lerpSpeed = 8f;

    // Internal displayed (lerped) values – kept as floats for smooth animation.
    float displayedMoney;
    float displayedSanity;
    float displayedArtifacts;

    // Actual target values.
    int targetMoney;
    int targetSanity;
    int targetArtifacts;

    void OnEnable()
    {
        // Style the label texts as secondary colour / small size.
        StyleLabel(moneyLabel, "FUNDS");
        StyleLabel(sanityLabel, "SANITY");
        StyleLabel(artifactsLabel, "ARTIFACTS");

        // Style value texts.
        StyleValue(moneyValueText);
        StyleValue(sanityValueText);
        StyleValue(artifactsValueText);

        // Snap to current values on first enable.
        SyncFromController(snap: true);
    }

    void Update()
    {
        SyncFromController(snap: false);
        AnimateValues();
    }

    // ───────────────────── Public API ─────────────────────

    /// <summary>
    /// Update a single resource by name.
    /// Recognised names (case-insensitive): money, sanity, artifacts.
    /// </summary>
    public void UpdateResource(string name, int value)
    {
        if (string.IsNullOrEmpty(name))
            return;

        switch (name.ToLowerInvariant())
        {
            case "money":     targetMoney     = value; break;
            case "sanity":    targetSanity    = value; break;
            case "artifacts": targetArtifacts = value; break;
        }
    }

    /// <summary>
    /// Set all three resource targets at once.
    /// </summary>
    public void UpdateAll(int money, int sanity, int artifacts)
    {
        targetMoney     = money;
        targetSanity    = sanity;
        targetArtifacts = artifacts;
    }

    // ───────────────────── Internals ─────────────────────

    void SyncFromController(bool snap)
    {
        LevelController lc = LevelController.Instance;
        if (lc == null)
            return;

        targetMoney     = lc.Money;
        targetSanity    = lc.Sanity;
        targetArtifacts = lc.Artifacts;

        if (snap)
        {
            displayedMoney     = targetMoney;
            displayedSanity    = targetSanity;
            displayedArtifacts = targetArtifacts;
            RefreshTexts();
        }
    }

    void AnimateValues()
    {
        float dt = Time.unscaledDeltaTime * lerpSpeed;

        bool changed = false;

        changed |= LerpDisplay(ref displayedMoney,     targetMoney,     dt);
        changed |= LerpDisplay(ref displayedSanity,    targetSanity,    dt);
        changed |= LerpDisplay(ref displayedArtifacts, targetArtifacts, dt);

        if (changed)
            RefreshTexts();
    }

    /// <summary>
    /// Lerps <paramref name="displayed"/> toward <paramref name="target"/>.
    /// Returns true when the displayed value changed visually (rounded integer changed).
    /// </summary>
    bool LerpDisplay(ref float displayed, int target, float dt)
    {
        int before = Mathf.RoundToInt(displayed);

        if (Mathf.Abs(displayed - target) < 0.5f)
            displayed = target;
        else
            displayed = Mathf.Lerp(displayed, target, dt);

        return Mathf.RoundToInt(displayed) != before;
    }

    void RefreshTexts()
    {
        SetText(moneyValueText,     Mathf.RoundToInt(displayedMoney));
        SetText(sanityValueText,    Mathf.RoundToInt(displayedSanity));
        SetText(artifactsValueText, Mathf.RoundToInt(displayedArtifacts));
    }

    static void SetText(TMP_Text text, int value)
    {
        if (text != null)
            text.text = value.ToString();
    }

    void StyleLabel(TMP_Text text, string labelContent)
    {
        if (text == null)
            return;

        text.text     = labelContent;
        text.color    = UITheme.TextSecondary;
        text.fontSize = UITheme.Small;
    }

    void StyleValue(TMP_Text text)
    {
        if (text == null)
            return;

        text.color    = UITheme.AccentSecondary;
        text.fontSize = UITheme.HeaderMedium;
    }
}
