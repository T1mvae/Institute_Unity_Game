using System;
using UnityEngine;

/// <summary>
/// Tracks elapsed game time and derives day number / formatted date.
/// Uses GameDateTracker for date formatting when it exists (backwards compat).
/// Fires an event every frame so UI elements can update without polling.
/// </summary>
public class TimeManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────────────
    public static TimeManager Instance { get; private set; }

    // ─── Events ─────────────────────────────────────────────────────────
    /// <summary>
    /// Fires each frame with the total elapsed game time (in seconds).
    /// </summary>
    public event Action<float> OnTimeUpdated;

    /// <summary>
    /// Fires once when the day number increments.
    /// Parameter: new day number.
    /// </summary>
    public event Action<int> OnNewDay;

    // ─── Configuration ──────────────────────────────────────────────────
    [Header("Fallback Settings (used when GameDateTracker is absent)")]
    [Tooltip("How many real-time seconds equal one in-game day.")]
    [SerializeField] private float secondsPerDay = 3f;

    // ─── Runtime State ──────────────────────────────────────────────────
    private float elapsedTime;
    private int lastDayNumber;

    // ─── Public Properties ──────────────────────────────────────────────

    /// <summary>Total elapsed game time in seconds (respects Time.timeScale).</summary>
    public float ElapsedTime => elapsedTime;

    /// <summary>Current in-game day number (1-based).</summary>
    public int CurrentDay
    {
        get
        {
            // Prefer GameDateTracker if available — it has configurable calendar settings.
            if (GameDateTracker.Instance != null)
            {
                return GameDateTracker.Instance.GetCurrentDayNumber(elapsedTime);
            }

            // Fallback: simple division.
            return Mathf.FloorToInt(elapsedTime / Mathf.Max(0.001f, secondsPerDay)) + 1;
        }
    }

    /// <summary>
    /// Human-readable date string. Delegates to GameDateTracker when available.
    /// </summary>
    public string FormattedDate
    {
        get
        {
            if (GameDateTracker.Instance != null)
            {
                return GameDateTracker.Instance.FormatDateLabel(elapsedTime);
            }

            // Fallback format when GameDateTracker is not in the scene.
            return $"Day {CurrentDay:000}";
        }
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TimeManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceLocator.Register<TimeManager>(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<TimeManager>();
            Instance = null;
        }
    }

    private void Update()
    {
        // Accumulate time (automatically paused when Time.timeScale == 0).
        elapsedTime += Time.deltaTime;

        // Notify listeners every frame.
        OnTimeUpdated?.Invoke(elapsedTime);

        // Detect day transitions.
        int day = CurrentDay;
        if (day != lastDayNumber)
        {
            lastDayNumber = day;
            OnNewDay?.Invoke(day);
        }
    }

    // ─── Public Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Manually set elapsed time (e.g. when loading a save).
    /// </summary>
    public void SetElapsedTime(float time)
    {
        elapsedTime = Mathf.Max(0f, time);
        lastDayNumber = CurrentDay;
    }

    /// <summary>
    /// Reset the clock back to zero.
    /// </summary>
    public void ResetTime()
    {
        elapsedTime = 0f;
        lastDayNumber = 0;
    }
}
