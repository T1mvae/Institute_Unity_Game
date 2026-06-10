using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates a pointy-top axial-coordinate hex map as UI region tiles.
/// Axial coordinates use q/r axes with neighbor deltas:
/// (+1,0), (+1,-1), (0,-1), (-1,0), (-1,+1), (0,+1).
/// </summary>
public class HexMapGenerator : MonoBehaviour
{
    static readonly Vector2Int[] AxialNeighborDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1)
    };

    static readonly string[] RegionNamePool =
    {
        "Aurel", "Bastion", "Cindervale", "Dawnmere", "Ebon Reach", "Fallow Crown",
        "Greyfen", "Hearthspire", "Ironwood", "Jadewick", "Karth", "Lowspire",
        "Mourn Coast", "Nacre Fields", "Old Vey", "Pale Orchard", "Quietus",
        "Redmarsh", "Sable Gate", "Tarnhold", "Umberfall", "Vigil", "Westwatch",
        "Yarrow", "Zeal Point", "Ashcourt", "Brinewall", "Cairnmarket"
    };

    [Header("References")]
    [SerializeField] private LevelController levelController;
    [SerializeField] private RectTransform regionsContainer;
    [SerializeField] private GameObject regionUIPrefab;

    [Header("Generation")]
    [SerializeField] private int mapWidth = 8;
    [SerializeField] private int mapHeight = 6;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool useDifficultySettings = true;

    [Header("Visuals")]
    [SerializeField] private float hexRadius = 54f;
    [SerializeField] private Vector2 mapPadding = new Vector2(80f, 60f);
    [SerializeField] private int hexTextureSize = 128;
    [SerializeField] private Color defaultLowColor = new Color(0.12f, 0.16f, 0.28f, 0.96f);
    [SerializeField] private Color defaultHighColor = new Color(0.25f, 0.34f, 0.52f, 0.96f);

    Sprite cachedHexSprite;
    readonly Dictionary<string, RegionUI> regionUiById = new Dictionary<string, RegionUI>();

    public int MapWidth => mapWidth;
    public int MapHeight => mapHeight;
    public int RandomSeed => randomSeed;

    void Awake()
    {
        ResolveReferences();
    }

    public void GenerateRegions()
    {
        ResolveReferences();
        ApplyDifficultySettings();

        if (levelController == null || regionsContainer == null)
        {
            Debug.LogError("HexMapGenerator: missing LevelController or regions container.");
            return;
        }

        System.Random rng = new System.Random(randomSeed);
        ClearGeneratedTiles();
        levelController.ClearRegionUIRegistry();
        levelController.AllRegions.Clear();
        levelController.Continents.Clear();

        Continent world = new Continent("Generated Hex World", Vector2.zero);
        levelController.Continents.Add(world);

        Dictionary<Vector2Int, Region> byCoord = new Dictionary<Vector2Int, Region>();
        DifficultyConfig difficulty = GameSession.ActiveDifficulty;

        for (int row = 0; row < mapHeight; row++)
        {
            for (int column = 0; column < mapWidth; column++)
            {
                int q = column - Mathf.FloorToInt(row * 0.5f);
                int r = row;
                RegionType type = PickRegionType(rng);
                string id = BuildRegionId(q, r);
                string name = PickRegionName(rng, levelController.AllRegions.Count, type);

                RollStartingStats(rng, type, difficulty, out int influence, out int stability, out int development);
                Region region = new Region(id, name, q, r, type, influence, stability, development);

                byCoord[new Vector2Int(q, r)] = region;
                levelController.AllRegions.Add(region);
                world.AddRegion(region);
                region.StatsChanged += levelController.OnRegionStatsChanged;
            }
        }

        BuildNeighbors(byCoord);
        SpawnRegionTiles(byCoord);
        levelController.RefreshAllRegionVisuals();
    }

    public void LoadRegions(List<RegionSaveData> savedRegions)
    {
        ResolveReferences();

        if (savedRegions == null || levelController == null || regionsContainer == null)
            return;

        ClearGeneratedTiles();
        levelController.ClearRegionUIRegistry();
        levelController.AllRegions.Clear();
        levelController.Continents.Clear();

        Continent world = new Continent("Loaded Hex World", Vector2.zero);
        levelController.Continents.Add(world);

        Dictionary<Vector2Int, Region> byCoord = new Dictionary<Vector2Int, Region>();
        Dictionary<string, Region> byId = new Dictionary<string, Region>();

        for (int i = 0; i < savedRegions.Count; i++)
        {
            RegionSaveData saved = savedRegions[i];
            if (saved == null)
                continue;

            RegionType parsedType = ParseRegionType(saved.regionType);
            Region region = new Region(
                saved.id,
                saved.name,
                saved.hexQ,
                saved.hexR,
                parsedType,
                saved.influence,
                saved.stability,
                saved.development);

            region.SetNeighborIds(saved.neighborIds);
            RestoreModifiers(region, saved.modifiers);

            byCoord[new Vector2Int(region.HexQ, region.HexR)] = region;
            byId[region.Id] = region;
            levelController.AllRegions.Add(region);
            world.AddRegion(region);
            region.StatsChanged += levelController.OnRegionStatsChanged;
        }

        LinkSavedNeighbors(byId);
        SpawnRegionTiles(byCoord);
        levelController.RefreshAllRegionVisuals();
    }

    public RegionUI GetRegionUI(string regionId)
    {
        if (string.IsNullOrEmpty(regionId))
            return null;

        regionUiById.TryGetValue(regionId, out RegionUI regionUI);
        return regionUI;
    }

    void ResolveReferences()
    {
        if (levelController == null)
            levelController = GetComponent<LevelController>() ?? LevelController.Instance;

        if (regionsContainer == null && levelController != null && levelController.regionsContainer != null)
            regionsContainer = levelController.regionsContainer as RectTransform;

        if (regionUIPrefab == null && levelController != null)
            regionUIPrefab = levelController.regionUIPrefab;
    }

    void ApplyDifficultySettings()
    {
        if (!useDifficultySettings)
            return;

        DifficultyConfig difficulty = GameSession.ActiveDifficulty;
        mapWidth = difficulty.mapWidth;
        mapHeight = difficulty.mapHeight;
        randomSeed = difficulty.randomSeed;
    }

    void BuildNeighbors(Dictionary<Vector2Int, Region> byCoord)
    {
        foreach (KeyValuePair<Vector2Int, Region> entry in byCoord)
        {
            Region region = entry.Value;
            region.ClearNeighbors();

            for (int i = 0; i < AxialNeighborDirections.Length; i++)
            {
                Vector2Int neighborCoord = entry.Key + AxialNeighborDirections[i];
                if (byCoord.TryGetValue(neighborCoord, out Region neighbor))
                    region.AddNeighbor(neighbor);
            }
        }
    }

    void LinkSavedNeighbors(Dictionary<string, Region> byId)
    {
        foreach (Region region in byId.Values)
        {
            List<string> neighborIds = new List<string>(region.NeighborIds);
            region.ClearNeighbors();
            region.SetNeighborIds(neighborIds);

            for (int i = 0; i < neighborIds.Count; i++)
            {
                if (byId.TryGetValue(neighborIds[i], out Region neighbor))
                    region.AddNeighbor(neighbor);
            }
        }
    }

    void SpawnRegionTiles(Dictionary<Vector2Int, Region> byCoord)
    {
        regionUiById.Clear();
        EnsureHexSprite();

        Dictionary<Region, Vector2> positions = CalculateCenteredPositions(byCoord);
        foreach (KeyValuePair<Vector2Int, Region> entry in byCoord)
        {
            Region region = entry.Value;
            GameObject tile = regionUIPrefab != null
                ? Instantiate(regionUIPrefab, regionsContainer)
                : CreateFallbackTile(regionsContainer);

            tile.name = "Hex_" + region.Id;

            RectTransform rect = tile.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(hexRadius * 2f, hexRadius * 2f);
                rect.anchoredPosition = positions[region];
            }

            RegionUI regionUI = tile.GetComponent<RegionUI>();
            if (regionUI == null)
                regionUI = tile.AddComponent<RegionUI>();

            Image image = regionUI.regionImage != null ? regionUI.regionImage : tile.GetComponent<Image>();
            Text label = regionUI.regionNameText != null ? regionUI.regionNameText : tile.GetComponentInChildren<Text>();
            ConfigureTileImage(image);
            regionUI.regionImage = image;
            regionUI.regionNameText = label;
            regionUI.SetRegion(region);
            regionUiById[region.Id] = regionUI;

            Button button = tile.GetComponent<Button>();
            if (button == null)
                button = tile.AddComponent<Button>();

            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(regionUI.OnClick);
        }
    }

    Dictionary<Region, Vector2> CalculateCenteredPositions(Dictionary<Vector2Int, Region> byCoord)
    {
        Dictionary<Region, Vector2> positions = new Dictionary<Region, Vector2>();
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Region region in byCoord.Values)
        {
            Vector2 position = AxialToUIPosition(region.HexQ, region.HexR);
            positions[region] = position;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxY = Mathf.Max(maxY, position.y);
        }

        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        List<Region> keys = new List<Region>(positions.Keys);
        for (int i = 0; i < keys.Count; i++)
            positions[keys[i]] -= center;

        return positions;
    }

    Vector2 AxialToUIPosition(int q, int r)
    {
        float x = hexRadius * Mathf.Sqrt(3f) * (q + r * 0.5f);
        float y = -hexRadius * 1.5f * r;
        return new Vector2(x, y);
    }

    void RollStartingStats(System.Random rng, RegionType type, DifficultyConfig difficulty, out int influence, out int stability, out int development)
    {
        influence = rng.Next(4, 12) + difficulty.startingInfluenceModifier;
        stability = rng.Next(4, 12) + difficulty.startingStabilityModifier;
        development = rng.Next(3, 11) + difficulty.startingDevelopmentModifier;

        ApplyTypeModifiers(type, ref influence, ref stability, ref development);

        influence = Mathf.Clamp(influence, 0, 20);
        stability = Mathf.Clamp(stability, 0, 20);
        development = Mathf.Clamp(development, 0, 20);
    }

    void ApplyTypeModifiers(RegionType type, ref int influence, ref int stability, ref int development)
    {
        switch (type)
        {
            case RegionType.CoreSettlement:
                influence += 1;
                stability += 2;
                development += 2;
                break;
            case RegionType.Frontier:
                influence -= 1;
                stability -= 1;
                development -= 1;
                break;
            case RegionType.Mountain:
                stability += 1;
                development -= 2;
                break;
            case RegionType.Forest:
                stability += 1;
                development -= 1;
                break;
            case RegionType.Coast:
                influence += 1;
                development += 1;
                break;
            case RegionType.Ruins:
                influence -= 1;
                stability -= 1;
                development += 1;
                break;
            case RegionType.ReligiousCenter:
                influence += 2;
                stability += 1;
                break;
            case RegionType.TradeHub:
                influence += 1;
                development += 2;
                break;
        }
    }

    RegionType PickRegionType(System.Random rng)
    {
        Array values = Enum.GetValues(typeof(RegionType));
        return (RegionType)values.GetValue(rng.Next(values.Length));
    }

    string PickRegionName(System.Random rng, int index, RegionType type)
    {
        string baseName = RegionNamePool[index % RegionNamePool.Length];
        if (index >= RegionNamePool.Length)
            baseName += " " + (index / RegionNamePool.Length + 1);

        switch (type)
        {
            case RegionType.Ruins: return "Ruins of " + baseName;
            case RegionType.TradeHub: return baseName + " Exchange";
            case RegionType.ReligiousCenter: return baseName + " Sanctum";
            default: return baseName;
        }
    }

    RegionType ParseRegionType(string rawType)
    {
        if (Enum.TryParse(rawType, out RegionType parsed))
            return parsed;

        return RegionType.Frontier;
    }

    string BuildRegionId(int q, int r)
    {
        return $"hex_{q}_{r}";
    }

    void RestoreModifiers(Region region, List<ModifierSaveData> modifiers)
    {
        region.ClearModifiers();
        if (modifiers == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            ModifierSaveData saved = modifiers[i];
            if (saved == null)
                continue;

            region.AddRestoredModifier(new RegionModifier(
                saved.name,
                saved.durationSeconds,
                saved.tickIntervalSeconds,
                saved.influencePerTick,
                saved.stabilityPerTick,
                saved.developmentPerTick,
                saved.remainingDurationSeconds));
        }
    }

    void ConfigureTileImage(Image image)
    {
        if (image == null)
            return;

        image.sprite = cachedHexSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = true;
        image.alphaHitTestMinimumThreshold = 0.1f;
        image.color = Color.white;

        Outline outline = image.GetComponent<Outline>();
        if (outline == null)
            outline = image.gameObject.AddComponent<Outline>();

        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = false;
    }

    void EnsureHexSprite()
    {
        if (cachedHexSprite != null)
            return;

        int size = Mathf.Clamp(hexTextureSize, 64, 512);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Vector2[] vertices = BuildHexVertices(center, size * 0.47f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                bool inside = IsPointInPolygon(point, vertices);
                float edgeFade = Mathf.Clamp01((DistanceToPolygonEdge(point, vertices) - 1f) / 4f);
                Color color = inside ? Color.Lerp(defaultLowColor, defaultHighColor, edgeFade) : Color.clear;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        cachedHexSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    Vector2[] BuildHexVertices(Vector2 center, float radius)
    {
        Vector2[] vertices = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i - 30f);
            vertices[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return vertices;
    }

    bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    float DistanceToPolygonEdge(Vector2 point, Vector2[] polygon)
    {
        float minDistance = float.PositiveInfinity;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Length];
            minDistance = Mathf.Min(minDistance, DistanceToSegment(point, a, b));
        }

        return minDistance;
    }

    float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(point - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
        t = Mathf.Clamp01(t);
        return Vector2.Distance(point, a + ab * t);
    }

    GameObject CreateFallbackTile(RectTransform parent)
    {
        GameObject tile = new GameObject("HexRegion", typeof(RectTransform));
        tile.transform.SetParent(parent, false);
        Image image = tile.AddComponent<Image>();
        Button button = tile.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(tile.transform, false);
        RectTransform labelRect = labelObject.transform as RectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 12;
        label.color = UITheme.TextPrimary;
        label.raycastTarget = false;

        RegionUI regionUI = tile.AddComponent<RegionUI>();
        regionUI.regionImage = image;
        regionUI.regionNameText = label;
        return tile;
    }

    void ClearGeneratedTiles()
    {
        regionUiById.Clear();
        if (regionsContainer == null)
            return;

        for (int i = regionsContainer.childCount - 1; i >= 0; i--)
            Destroy(regionsContainer.GetChild(i).gameObject);
    }
}
