using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RegionUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI")]
    public Text regionNameText;
    public Image regionImage;         // назначи в инспекторе
    public Color hoverColor = new Color(0.65f, 0.65f, 0.65f, 0.35f);

    [Header("Data")]
    private Region region;

    // Цвета, чтобы корректно восстанавливать внешний вид
    private Color _initialColor = Color.white;
    private Color _baseColor = Color.white;
    private bool _hasInitialColor;

    void Awake()
    {
        if (regionImage != null)
        {
            _initialColor = regionImage.color;
            _baseColor = _initialColor;
            _hasInitialColor = true;
        }
    }

    public void SetRegion(Region newRegion)
    {
        if (region == newRegion)
        {
            RefreshVisual();
            return;
        }

        if (region != null)
            region.StatsChanged -= OnRegionStatsChanged;

        region = newRegion;

        if (region != null)
            region.StatsChanged += OnRegionStatsChanged;

        if (LevelController.Instance != null)
            LevelController.Instance.RegisterRegionUI(this);

        RefreshVisual();
    }

    // Старый клик оставляю как “обычный выбор” региона.
    // Он нужен, когда нет таргетинга.
    public void OnClick()
    {
        if (region == null) return;
        Debug.Log("Region clicked: " + region.Name);
        if (RegionManager.Instance != null)
            RegionManager.Instance.SelectRegion(region);
        else if (LevelController.Instance != null)
            LevelController.Instance.SelectRegion(region);
    }

    // Hover
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHover(true, hoverColor);

        // Показ тултипа при наведении
        if (Tooltip.Instance != null && region != null)
            Tooltip.Instance.Show(region.GetTooltip());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false);

        if (Tooltip.Instance != null)
            Tooltip.Instance.Hide();
    }

    // Клик по UI-региону: если включен таргетинг, отдаём выбор менеджеру.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (region == null) return;

        OnClick();
    }

    // Удобный вызов для внешней подсветки (если когда-нибудь понадобится)
    public void SetHover(bool on)
    {
        SetHover(on, hoverColor);
    }

    public void SetHover(bool on, Color color)
    {
        if (regionImage == null) return;
        if (on)
        {
            // Лёгкая подсветка поверх базового цвета
            Color highlight = Color.Lerp(_baseColor, color, 0.5f);
            highlight.a = Mathf.Max(_baseColor.a, highlight.a);
            regionImage.color = highlight;
        }
        else
        {
            regionImage.color = _baseColor;
        }
    }

    public Region GetRegion()
    {
        return region;
    }

    public void RefreshVisual()
    {
        LevelController controller = LevelController.Instance;
        LevelController.MapViewMode mode = controller != null
            ? controller.CurrentMapViewMode
            : LevelController.MapViewMode.Standard;

        RefreshVisual(mode);
    }

    public void RefreshVisual(LevelController.MapViewMode mode)
    {
        if (regionImage != null)
        {
            _baseColor = CalculateRegionColor(mode);
            regionImage.color = _baseColor;
        }

        if (regionNameText != null)
            regionNameText.text = BuildRegionLabel(mode);
    }

    Color CalculateRegionColor(LevelController.MapViewMode mode)
    {
        if (region == null)
            return _hasInitialColor ? _initialColor : Color.white;

        Color baseColor;
        switch (mode)
        {
            case LevelController.MapViewMode.Influence:
                baseColor = GetGradientColor(region.Influence, new Color(0.22f, 0.26f, 0.42f), UITheme.BarInfluence);
                break;
            case LevelController.MapViewMode.Stability:
                baseColor = GetGradientColor(region.Stability, new Color(0.18f, 0.30f, 0.24f), UITheme.BarStability);
                break;
            case LevelController.MapViewMode.Development:
                baseColor = GetGradientColor(region.Development, new Color(0.34f, 0.24f, 0.10f), UITheme.BarDevelopment);
                break;
            default:
                baseColor = GetRegionTypeColor(region.Type);
                break;
        }

        Region selected = LevelController.Instance != null ? LevelController.Instance.SelectedRegion : null;
        if (selected == region)
            baseColor = Color.Lerp(baseColor, UITheme.AccentPrimary, 0.35f);

        return baseColor;
    }

    string BuildRegionLabel(LevelController.MapViewMode mode)
    {
        if (region == null)
            return string.Empty;

        switch (mode)
        {
            case LevelController.MapViewMode.Influence:
                return $"{region.Name}\nInfluence: {region.Influence}";
            case LevelController.MapViewMode.Stability:
                return $"{region.Name}\nStability: {region.Stability}";
            case LevelController.MapViewMode.Development:
                return $"{region.Name}\nDevelopment: {region.Development}";
            default:
                return region.Name;
        }
    }

    Color GetGradientColor(int value, Color low, Color high)
    {
        float t = Mathf.InverseLerp(0f, 20f, Mathf.Clamp(value, 0, 20));
        Color color = Color.Lerp(low, high, t);
        color.a = 0.95f;
        return color;
    }

    Color GetRegionTypeColor(RegionType type)
    {
        switch (type)
        {
            case RegionType.CoreSettlement: return new Color(0.25f, 0.36f, 0.56f, 0.96f);
            case RegionType.Frontier: return new Color(0.18f, 0.22f, 0.31f, 0.96f);
            case RegionType.Mountain: return new Color(0.30f, 0.30f, 0.34f, 0.96f);
            case RegionType.Forest: return new Color(0.13f, 0.32f, 0.22f, 0.96f);
            case RegionType.Coast: return new Color(0.12f, 0.30f, 0.42f, 0.96f);
            case RegionType.Ruins: return new Color(0.30f, 0.22f, 0.34f, 0.96f);
            case RegionType.ReligiousCenter: return new Color(0.34f, 0.26f, 0.48f, 0.96f);
            case RegionType.TradeHub: return new Color(0.38f, 0.28f, 0.12f, 0.96f);
            default: return new Color(0.18f, 0.22f, 0.34f, 0.96f);
        }
    }

    void OnRegionStatsChanged(Region changedRegion)
    {
        if (changedRegion != region)
            return;

        RefreshVisual();
    }

    void OnDestroy()
    {
        if (region != null)
            region.StatsChanged -= OnRegionStatsChanged;

        if (LevelController.Instance != null)
            LevelController.Instance.UnregisterRegionUI(this);
    }
}
