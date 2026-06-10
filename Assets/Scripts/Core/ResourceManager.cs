using System;
using UnityEngine;

/// <summary>
/// Manages player resources (Sanity, Money, Artifacts).
/// Syncs bidirectionally with LevelController for backwards compatibility:
///   - On Start, reads current values from LevelController.
///   - On change, forwards deltas to LevelController so legacy UI/logic stays in sync.
/// </summary>
public class ResourceManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────────────
    public static ResourceManager Instance { get; private set; }

    // ─── Events ─────────────────────────────────────────────────────────
    /// <summary>
    /// Fired whenever a resource changes.
    /// Parameters: resourceName ("Sanity"/"Money"/"Artifacts"), oldValue, newValue.
    /// </summary>
    public event Action<string, int, int> OnResourceChanged;

    // ─── Backing Fields ─────────────────────────────────────────────────
    [SerializeField] private int sanity = 100;
    [SerializeField] private int money = 100;
    [SerializeField] private int artifacts = 5;

    // ─── Public Properties ──────────────────────────────────────────────

    public int Sanity => sanity;
    public int Money => money;
    public int Artifacts => artifacts;

    // ─── Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ResourceManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceLocator.Register<ResourceManager>(this);
    }

    private void Start()
    {
        // Sync initial values from LevelController if it exists (backwards compat).
        SyncFromLevelController();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<ResourceManager>();
            Instance = null;
        }
    }

    // ─── Resource Modification ──────────────────────────────────────────

    /// <summary>
    /// Change the player's Sanity by the given amount. Clamped to [0, 100].
    /// Also forwards the change to LevelController for legacy sync.
    /// </summary>
    public void ChangeSanity(int amount)
    {
        int oldValue = sanity;
        sanity = Mathf.Clamp(sanity + amount, 0, 100);

        if (sanity != oldValue)
        {
            OnResourceChanged?.Invoke("Sanity", oldValue, sanity);
        }

        // Forward to legacy system so its UI updates & game-over check runs.
        if (LevelController.Instance != null)
        {
            LevelController.Instance.ChangeSanity(amount);
            // Re-sync in case LevelController applies different clamping.
            sanity = LevelController.Instance.Sanity;
        }
    }

    /// <summary>
    /// Change the player's Money by the given amount. Clamped to [0, ∞).
    /// Also forwards the change to LevelController for legacy sync.
    /// </summary>
    public void ChangeMoney(int amount)
    {
        int oldValue = money;
        money = Mathf.Max(0, money + amount);

        if (money != oldValue)
        {
            OnResourceChanged?.Invoke("Money", oldValue, money);
        }

        // Forward to legacy system.
        if (LevelController.Instance != null)
        {
            LevelController.Instance.ChangeMoney(amount);
            // Re-sync in case LevelController applies different clamping.
            money = LevelController.Instance.Money;
        }
    }

    /// <summary>
    /// Change the player's Artifacts by the given amount. Clamped to [0, ∞).
    /// Also forwards the change to LevelController for legacy sync.
    /// </summary>
    public void ChangeArtifacts(int amount)
    {
        int oldValue = artifacts;
        artifacts = Mathf.Max(0, artifacts + amount);

        if (artifacts != oldValue)
        {
            OnResourceChanged?.Invoke("Artifacts", oldValue, artifacts);
        }

        // Forward to legacy system.
        if (LevelController.Instance != null)
        {
            LevelController.Instance.ChangeArtifacts(amount);
            // Re-sync in case LevelController applies different clamping.
            artifacts = LevelController.Instance.Artifacts;
        }
    }

    // ─── Affordability Checks ───────────────────────────────────────────

    /// <summary>
    /// Returns true if the player can afford the specified costs.
    /// A cost of 0 (or negative) is always affordable for that resource.
    /// </summary>
    public bool CanAfford(int sanityCost, int moneyCost, int artifactsCost)
    {
        if (sanityCost > 0 && sanity < sanityCost) return false;
        if (moneyCost > 0 && money < moneyCost) return false;
        if (artifactsCost > 0 && artifacts < artifactsCost) return false;
        return true;
    }

    /// <summary>
    /// Spend the given amounts of each resource. Does NOT check affordability —
    /// call CanAfford first if you need a guard.
    /// </summary>
    public void SpendResources(int sanityCost, int moneyCost, int artifactsCost)
    {
        if (sanityCost != 0) ChangeSanity(-sanityCost);
        if (moneyCost != 0) ChangeMoney(-moneyCost);
        if (artifactsCost != 0) ChangeArtifacts(-artifactsCost);
    }

    // ─── Internal ───────────────────────────────────────────────────────

    /// <summary>
    /// Pull current resource values from LevelController so we start in sync.
    /// </summary>
    public void SyncFromLevelController()
    {
        if (LevelController.Instance == null) return;

        sanity = LevelController.Instance.Sanity;
        money = LevelController.Instance.Money;
        artifacts = LevelController.Instance.Artifacts;

        Debug.Log($"[ResourceManager] Synced from LevelController — Sanity:{sanity}  Money:{money}  Artifacts:{artifacts}");
    }
}
