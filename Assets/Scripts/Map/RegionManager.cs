using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Manages region selection, deselection, and provides a centralized access point
/// for all regions. Works alongside LevelController for backwards compatibility.
/// </summary>
public class RegionManager : MonoBehaviour
{
    public static RegionManager Instance { get; private set; }

    /// <summary>Fired when a region is selected. Passes the newly selected region.</summary>
    public event Action<Region> OnRegionSelected;

    /// <summary>Fired when the current region is deselected.</summary>
    public event Action OnRegionDeselected;

    /// <summary>Fired when any region's stats change.</summary>
    public event Action<Region> OnRegionStatsChanged;

    Region selectedRegion;

    /// <summary>Currently selected region, or null.</summary>
    public Region SelectedRegion
    {
        get => selectedRegion;
        private set => selectedRegion = value;
    }

    /// <summary>All regions in the game. Reads from LevelController for backwards compat.</summary>
    public List<Region> AllRegions
    {
        get
        {
            if (LevelController.Instance != null)
                return LevelController.Instance.AllRegions;
            return cachedRegions;
        }
    }

    /// <summary>All continents. Reads from LevelController for backwards compat.</summary>
    public List<Continent> Continents
    {
        get
        {
            if (LevelController.Instance != null)
                return LevelController.Instance.Continents;
            return cachedContinents;
        }
    }

    readonly List<Region> cachedRegions = new List<Region>();
    readonly List<Continent> cachedContinents = new List<Continent>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        ServiceLocator.Register<RegionManager>(this);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ServiceLocator.Unregister<RegionManager>();
            Instance = null;
        }
    }

    /// <summary>
    /// Select a region. Updates LevelController for backwards compatibility.
    /// </summary>
    public void SelectRegion(Region region)
    {
        if (region == null)
        {
            DeselectRegion();
            return;
        }

        selectedRegion = region;

        // Backwards compat: keep LevelController in sync without recursive reselection.
        if (LevelController.Instance != null && LevelController.Instance.SelectedRegion != region)
            LevelController.Instance.SelectRegion(region);

        OnRegionSelected?.Invoke(region);
    }

    /// <summary>
    /// Deselect the current region.
    /// </summary>
    public void DeselectRegion()
    {
        if (selectedRegion == null)
            return;

        selectedRegion = null;

        // Backwards compat
        if (LevelController.Instance != null && LevelController.Instance.SelectedRegion != null)
            LevelController.Instance.DeselectRegion();

        OnRegionDeselected?.Invoke();
    }

    /// <summary>
    /// Check if a pointer position is over any interactive UI element.
    /// Used to prevent map clicks from going through UI.
    /// </summary>
    public bool IsPointerOverUI()
    {
        EventSystem current = EventSystem.current;
        if (current == null)
            return false;

        return current.IsPointerOverGameObject();
    }

    /// <summary>
    /// Called when a region's stats change. Forwards to LevelController and fires event.
    /// </summary>
    public void NotifyRegionStatsChanged(Region region)
    {
        if (region == null)
            return;

        // Forward to LevelController for backwards compat
        if (LevelController.Instance != null)
            LevelController.Instance.OnRegionStatsChanged(region);

        OnRegionStatsChanged?.Invoke(region);
    }

    /// <summary>
    /// Get world-wide averages for all stats.
    /// </summary>
    public bool TryGetWorldAverages(out float influence, out float stability, out float development)
    {
        influence = 0f;
        stability = 0f;
        development = 0f;

        List<Region> regions = AllRegions;
        if (regions == null || regions.Count == 0)
            return false;

        for (int i = 0; i < regions.Count; i++)
        {
            Region r = regions[i];
            influence += r.Influence;
            stability += r.Stability;
            development += r.Development;
        }

        float inv = 1f / regions.Count;
        influence *= inv;
        stability *= inv;
        development *= inv;
        return true;
    }
}
