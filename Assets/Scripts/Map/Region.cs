using System;
using System.Collections.Generic;
using UnityEngine;

public enum RegionType
{
    CoreSettlement,
    Frontier,
    Mountain,
    Forest,
    Coast,
    Ruins,
    ReligiousCenter,
    TradeHub
}

[System.Serializable]
public class Region
{
    public string Id;
    public string Name;
    public int HexQ;
    public int HexR;
    public RegionType Type;
    public int Influence;
    public int Stability;
    public int Development;
    private readonly List<RegionModifier> _activeModifiers = new List<RegionModifier>();
    private readonly List<Region> _neighbors = new List<Region>();
    private readonly List<string> _neighborIds = new List<string>();
    private readonly List<string> _tags = new List<string>();
    public event Action<Region> StatsChanged;
    [NonSerialized] private Continent _continent;
    public Continent Continent
    {
        get => _continent;
        internal set => _continent = value;
    }

    public IReadOnlyList<RegionModifier> ActiveModifiers => _activeModifiers;
    public IReadOnlyList<Region> Neighbors => _neighbors;
    public IReadOnlyList<string> NeighborIds => _neighborIds;
    public IReadOnlyList<string> Tags => _tags;

    public Region(string name)
    {
        Id = Guid.NewGuid().ToString("N");
        Name = name;
        Influence = UnityEngine.Random.Range(1, 11);
        Stability = UnityEngine.Random.Range(1, 11);
        Development = UnityEngine.Random.Range(1, 11);
    }

    public Region(string id, string name, int hexQ, int hexR, RegionType type, int influence, int stability, int development)
    {
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
        Name = string.IsNullOrEmpty(name) ? Id : name;
        HexQ = hexQ;
        HexR = hexR;
        Type = type;
        Influence = Mathf.Clamp(influence, 0, 20);
        Stability = Mathf.Clamp(stability, 0, 20);
        Development = Mathf.Clamp(development, 0, 20);
        SyncTypeTag();
    }

    public void InitializeHexMetadata(string id, int hexQ, int hexR, RegionType type)
    {
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
        HexQ = hexQ;
        HexR = hexR;
        Type = type;
        SyncTypeTag();
    }

    public void SetStats(int influence, int stability, int development, bool notify = true)
    {
        Influence = Mathf.Clamp(influence, 0, 20);
        Stability = Mathf.Clamp(stability, 0, 20);
        Development = Mathf.Clamp(development, 0, 20);

        if (notify)
            NotifyStatsChanged();
    }

    public void AddModifier(RegionModifier modifier)
    {
        if (modifier == null) return;
        modifier.Restart();
        _activeModifiers.Add(modifier);
    }

    public void AddRestoredModifier(RegionModifier modifier)
    {
        if (modifier == null) return;
        _activeModifiers.Add(modifier);
    }

    public void ClearModifiers()
    {
        _activeModifiers.Clear();
    }

    internal void ClearNeighbors()
    {
        _neighbors.Clear();
        _neighborIds.Clear();
    }

    internal bool AddNeighbor(Region neighbor)
    {
        if (neighbor == null || neighbor == this)
            return false;

        if (_neighbors.Contains(neighbor))
            return false;

        _neighbors.Add(neighbor);
        AddNeighborId(neighbor.Id);
        return true;
    }

    internal bool AddNeighborId(string neighborId)
    {
        if (string.IsNullOrEmpty(neighborId) || neighborId == Id)
            return false;

        if (_neighborIds.Contains(neighborId))
            return false;

        _neighborIds.Add(neighborId);
        return true;
    }

    public void SetNeighborIds(IEnumerable<string> neighborIds)
    {
        _neighborIds.Clear();
        if (neighborIds == null)
            return;

        foreach (string neighborId in neighborIds)
            AddNeighborId(neighborId);
    }

    public bool IsNeighbor(Region region)
    {
        if (region == null)
            return false;

        return _neighbors.Contains(region);
    }

    public void UpdateModifiers(float deltaTime)
    {
        if (_activeModifiers.Count == 0) return;
        for (int i = _activeModifiers.Count - 1; i >= 0; i--)
        {
            RegionModifier modifier = _activeModifiers[i];
            if (modifier.Update(deltaTime, this))
                _activeModifiers.RemoveAt(i);
        }
    }

    public void ChangeInfluence(int delta)
    {
        Influence = Mathf.Clamp(Influence + delta, 0, 20);
        NotifyStatsChanged();
    }

    public void ChangeStability(int delta)
    {
        Stability = Mathf.Clamp(Stability + delta, 0, 20);
        NotifyStatsChanged();
    }

    public void ChangeDevelopment(int delta)
    {
        Development = Mathf.Clamp(Development + delta, 0, 20);
        NotifyStatsChanged();
    }

    // Удобный текст для тултипа
    public string GetTooltip()
    {
        string continentName = Continent != null ? Continent.Name : "Unknown";
        return $"Continent: {continentName}\n" +
               $"Region: {Name}\n" +
               $"Type: {FormatRegionType(Type)}\n" +
               $"Hex: {HexQ}, {HexR}\n" +
               $"Influence: {Influence}\n" +
               $"Stability: {Stability}\n" +
               $"Development: {Development}\n" +
               $"Neighbors: {_neighbors.Count}";
    }

    public void NotifyStatsChanged()
    {
        StatsChanged?.Invoke(this);
    }

    void SyncTypeTag()
    {
        _tags.Clear();
        _tags.Add(FormatRegionType(Type));
    }

    static string FormatRegionType(RegionType type)
    {
        switch (type)
        {
            case RegionType.CoreSettlement: return "Core Settlement";
            case RegionType.ReligiousCenter: return "Religious Center";
            case RegionType.TradeHub: return "Trade Hub";
            default: return type.ToString();
        }
    }
}
