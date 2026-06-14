using System.Text;
using Institute.World.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Institute.World.UI
{
    /// <summary>
    /// The clean, data-driven gameplay HUD, built with UI Toolkit. Structure comes from
    /// Assets/UI/UXML/GameplayHUD.uxml + USS; colors from theme.json. It binds to
    /// <see cref="WorldController"/> selection events and shows the selected REGION's political
    /// stats, or a wilderness/terrain panel when an unclaimed tile is selected.
    ///
    /// No hard-coded colors live here — visuals are owned by the USS/theme. The controller only
    /// wires data and behavior. Falls back to a programmatic tree if the UXML asset is absent.
    /// </summary>
    [RequireComponent(typeof(WorldController))]
    public class WorldHUDController : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset layoutAsset;
        [SerializeField] StyleSheet[] styleSheets;

        UIDocument _document;
        WorldController _world;
        VisualElement _root;

        Label _money, _artifacts, _sanity, _stability, _date;
        Label _title, _subtitle, _statInfluence, _statStability, _statDevelopment, _body, _characters;
        VisualElement _statBlock, _actionsList, _tooltip;
        Label _logText, _tooltipText;
        VisualElement[] _modeButtons;

        enum DirectiveTab { Decisions, Characters }
        DirectiveTab _directiveTab = DirectiveTab.Decisions;
        Button _tabDecisions, _tabCharacters;
        RegionData _ctxRegion;   // last selected region, for tab re-population
        HexTileData _ctxTile;    // last selected wilderness tile, for tab re-population

        bool _hoveringTile;
        float _cosmeticDay;

        void Awake() => _world = GetComponent<WorldController>();

        void Start()
        {
            ThemeLoader.LoadOrCreateDefault();
            LoadEditorAssetsIfNeeded();
            _document = UIToolkitThemeUtility.EnsureDocument(gameObject);
            BuildUI();
            Subscribe();
            RefreshWorldOverview();
        }

        void OnDestroy() => Unsubscribe();

        // Load UXML/USS from Resources so the HUD is styled in the standalone build (not just the
        // editor). OverlayUtil does Resources-first with an editor AssetDatabase fallback.
        void LoadEditorAssetsIfNeeded()
        {
            if (layoutAsset == null)
                layoutAsset = OverlayUtil.LoadUxml("UI/UXML/GameplayHUD");
            if (styleSheets == null || styleSheets.Length == 0)
            {
                styleSheets = new[]
                {
                    OverlayUtil.LoadStyle("UI/Styles/base"),
                    OverlayUtil.LoadStyle("UI/Styles/gameplay"),
                    OverlayUtil.LoadStyle("UI/Styles/popups"),
                };
            }
        }

        void BuildUI()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1f;
            AddStyleSheets(_root);

            if (layoutAsset != null) layoutAsset.CloneTree(_root);
            else BuildFallbackTree(_root);

            // Bind elements (null-safe; missing names are simply skipped).
            _money = _root.Q<Label>("res-money");
            _artifacts = _root.Q<Label>("res-artifacts");
            _sanity = _root.Q<Label>("res-sanity");
            _stability = _root.Q<Label>("res-stability");
            _date = _root.Q<Label>("lbl-date");

            _title = _root.Q<Label>("panel-title");
            _subtitle = _root.Q<Label>("panel-subtitle");
            _statBlock = _root.Q<VisualElement>("stat-block");
            _statInfluence = _root.Q<Label>("stat-influence");
            _statStability = _root.Q<Label>("stat-stability");
            _statDevelopment = _root.Q<Label>("stat-development");
            _body = _root.Q<Label>("panel-body");
            _characters = _root.Q<Label>("panel-characters");
            _actionsList = _root.Q<VisualElement>("actions-list");
            _logText = _root.Q<Label>("log-text");
            _tooltip = _root.Q<VisualElement>("tooltip");
            _tooltipText = _root.Q<Label>("tooltip-text");

            WirePause();
            WireModeButtons();
            WireDirectiveTabs();
            WirePointerGate();
        }

        void WirePause()
        {
            Button pause = _root.Q<Button>("btn-pause");
            if (pause != null) pause.clicked += OpenPauseMenu;
        }

        void OpenPauseMenu()
        {
            // Proper pause via the wired controller (no direct Time.timeScale toggling here).
            if (WorldPauseController.Instance != null) WorldPauseController.Instance.Open();
            else PlayerLog.Add("Pause menu is not available in this scene.");
        }

        void WireModeButtons()
        {
            (string name, MapMode mode)[] modes =
            {
                ("mode-terrain", MapMode.Terrain),
                ("mode-political", MapMode.Political),
                ("mode-influence", MapMode.Influence),
                ("mode-stability", MapMode.Stability),
                ("mode-development", MapMode.Development),
                ("mode-danger", MapMode.Danger),
            };
            _modeButtons = new VisualElement[modes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                Button b = _root.Q<Button>(modes[i].name);
                _modeButtons[i] = b;
                if (b == null) continue;
                MapMode m = modes[i].mode;
                b.clicked += () => { _world.SetMapMode(m); HighlightMode(m); };
            }
        }

        void HighlightMode(MapMode mode)
        {
            string active = "mode-" + mode.ToString().ToLowerInvariant();
            foreach (var b in _modeButtons)
            {
                if (b == null) continue;
                if (b.name == active) b.AddToClassList("mode-active");
                else b.RemoveFromClassList("mode-active");
            }
        }

        // ---------- directive tabs (Decisions | Characters) ----------
        void WireDirectiveTabs()
        {
            _tabDecisions = _root.Q<Button>("tab-decisions");
            _tabCharacters = _root.Q<Button>("tab-characters");
            if (_tabDecisions != null) _tabDecisions.clicked += () => SelectDirectiveTab(DirectiveTab.Decisions);
            if (_tabCharacters != null) _tabCharacters.clicked += () => SelectDirectiveTab(DirectiveTab.Characters);
            HighlightDirectiveTab();
        }

        void SelectDirectiveTab(DirectiveTab tab)
        {
            _directiveTab = tab;
            HighlightDirectiveTab();
            RepopulateActions();
        }

        void HighlightDirectiveTab()
        {
            SetActiveClass(_tabDecisions, _directiveTab == DirectiveTab.Decisions);
            SetActiveClass(_tabCharacters, _directiveTab == DirectiveTab.Characters);
        }

        static void SetActiveClass(VisualElement e, bool active)
        {
            if (e == null) return;
            if (active) e.AddToClassList("mode-active");
            else e.RemoveFromClassList("mode-active");
        }

        // Re-render the actions list for the current selection under the active tab.
        void RepopulateActions()
        {
            if (_ctxRegion != null) PopulateRegionActions(_ctxRegion);
            else if (_ctxTile != null) PopulateWildernessActions(_ctxTile);
            else RefreshWorldOverview();
        }

        void WirePointerGate()
        {
            foreach (string panel in new[] { "top-bar", "left-panel", "right-panel", "bottom-log", "mode-bar" })
            {
                VisualElement e = _root.Q<VisualElement>(panel);
                if (e == null) continue;
                e.RegisterCallback<PointerEnterEvent>(_ => MapInteractionGate.PointerOverUI = true);
                e.RegisterCallback<PointerLeaveEvent>(_ => MapInteractionGate.PointerOverUI = false);
            }
        }

        void Subscribe()
        {
            _world.RegionSelected += OnRegionSelected;
            _world.TileSelected += OnTileSelected;
            _world.SelectionCleared += RefreshWorldOverview;
            _world.TileHovered += OnTileHovered;
            _world.WorldBuilt += _ => RefreshWorldOverview();
            _world.RegionDataChanged += OnRegionDataChanged;
            if (GameManager.Instance != null) GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
        }

        void Unsubscribe()
        {
            if (_world == null) return;
            _world.RegionSelected -= OnRegionSelected;
            _world.TileSelected -= OnTileSelected;
            _world.SelectionCleared -= RefreshWorldOverview;
            _world.TileHovered -= OnTileHovered;
            _world.RegionDataChanged -= OnRegionDataChanged;
            if (GameManager.Instance != null) GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }

        // Part 4: show a centered VICTORY/DEFEAT banner when the game ends.
        void OnGameStateChanged(GameManager.GameState state)
        {
            if (state != GameManager.GameState.GameOver || _root == null) return;
            bool win = GameManager.Instance != null && GameManager.Instance.DidWin;

            var banner = new VisualElement { name = "gameover-banner" };
            banner.style.position = Position.Absolute;
            banner.style.left = 0; banner.style.right = 0; banner.style.top = 0; banner.style.bottom = 0;
            banner.style.justifyContent = Justify.Center;
            banner.style.alignItems = Align.Center;
            banner.style.backgroundColor = new Color(0f, 0f, 0f, 0.82f);

            var label = new Label(win ? "VICTORY" : "DEFEAT");
            label.style.fontSize = 64;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = win ? new Color(0.22f, 0.78f, 0.44f) : new Color(0.94f, 0.27f, 0.27f);
            banner.Add(label);
            banner.Add(new Label(win ? "The Institute's quiet hand has reshaped the world."
                                     : "The mission is over.") { style = { color = Color.white, fontSize = 16, marginTop = 8 } });
            _root.Add(banner);
        }

        // Re-render the dossier when the selected region's stats change (decision/event/character).
        void OnRegionDataChanged(RegionData region)
        {
            if (region != null && region == _world.SelectedRegion) OnRegionSelected(region);
        }

        void Update()
        {
            // Resources via the bridge (ResourceManager preferred, else LevelController).
            if (GameResources.Available)
            {
                SetText(_money, GameResources.Money.ToString());
                SetText(_artifacts, GameResources.Artifacts.ToString());
                SetText(_sanity, GameResources.Sanity.ToString());
            }

            // Part 2: rich tooltips showing the daily income/loss math (from the economy system).
            var eco = EconomySystem.Instance;
            if (eco != null)
            {
                if (_money != null) _money.tooltip = eco.MoneyTooltip;
                if (_artifacts != null) _artifacts.tooltip = eco.ArtifactTooltip;
                if (_sanity != null) _sanity.tooltip = eco.SanityTooltip;
            }

            if (_world.Map != null)
            {
                int stab = eco != null ? eco.GlobalStability(_world.Map) : AverageStability(_world.Map);
                int exp = eco != null ? eco.Exposure : 0;
                SetText(_stability, $"{stab}%  ·  Exp {exp}");
                if (_stability != null && eco != null) _stability.tooltip = eco.ExposureTooltip;
            }

            // Real in-game date when the clock is present, else a cosmetic counter.
            if (TimeManager.Instance != null)
                SetText(_date, TimeManager.Instance.FormattedDate.ToUpperInvariant());
            else
            {
                _cosmeticDay += Time.unscaledDeltaTime / 5f;
                SetText(_date, "DAY " + (1 + Mathf.FloorToInt(_cosmeticDay)).ToString("000"));
            }

            if (_hoveringTile && _tooltip != null && _tooltip.resolvedStyle.display == DisplayStyle.Flex)
                PositionTooltip();
        }

        // ---------- selection handlers ----------
        void OnRegionSelected(RegionData region)
        {
            _ctxRegion = region;
            _ctxTile = null;
            SetText(_title, region.displayName);
            SetText(_subtitle, $"{Prettify(region.regionType.ToString())} • {region.TileCount} tiles");
            ShowStats(true);
            SetText(_statInfluence, region.influence.ToString());
            SetText(_statStability, region.stability.ToString());
            SetText(_statDevelopment, region.development.ToString());

            var sb = new StringBuilder();
            StateData state = _world.Map.GetState(region.stateId);
            if (state != null)
                sb.AppendLine($"State: {state.displayName}  (Infl {state.influence} · Stab {state.stability} · Dev {state.development})");
            HexTileData cap = _world.Map.GetTile(region.capitalTileId);
            sb.AppendLine($"Capital: {(cap != null ? MapDefinitions.GetTerrain(cap.terrainType).displayName + " " + cap.coord : "n/a")}");
            sb.AppendLine($"Population: {region.population:n0}   Wealth: {region.wealth}");
            sb.AppendLine($"Neighbors: {region.neighborRegionIds.Count}   Border tiles: {region.borderTileIds.Count}");
            if (region.tags.Count > 0) sb.AppendLine("Tags: " + string.Join(", ", region.tags));
            if (region.modifiers.Count > 0) sb.AppendLine("Modifiers: " + region.modifiers.Count);
            SetText(_body, sb.ToString().TrimEnd());

            SetText(_characters, region.characterIds.Count > 0
                ? $"Local characters: {region.characterIds.Count}"
                : "No known characters in this region.");

            PopulateRegionActions(region);
        }

        void OnTileSelected(HexTileData tile)
        {
            _ctxRegion = null;
            _ctxTile = tile;
            TerrainDefinition def = MapDefinitions.GetTerrain(tile.terrainType);
            SetText(_title, def.displayName);
            SetText(_subtitle, "Unclaimed — No organized region");
            ShowStats(false);

            var sb = new StringBuilder();
            sb.AppendLine($"Coordinate: {tile.coord}");
            sb.AppendLine($"Biome: {Prettify(tile.biomeType.ToString())}");
            sb.AppendLine($"Elevation: {tile.elevation:0.00}   Moisture: {tile.moisture:0.00}");
            sb.AppendLine($"Movement cost: {tile.movementCost:0.0}   Danger: {tile.dangerLevel:0.00}");
            if (tile.specialFeatureTags.Count > 0) sb.AppendLine("Features: " + string.Join(", ", tile.specialFeatureTags));
            else sb.AppendLine("Features: none");
            SetText(_body, sb.ToString().TrimEnd());
            SetText(_characters, "Wilderness. A future expansion or exploration target.");

            PopulateWildernessActions(tile);
        }

        void RefreshWorldOverview()
        {
            _ctxRegion = null;
            _ctxTile = null;
            SetText(_title, "WORLD OVERVIEW");
            int regions = _world.Map != null ? _world.Map.RegionCount : 0;
            int tiles = _world.Map != null ? _world.Map.TileCount : 0;
            int unclaimed = _world.Map != null ? _world.Map.unclaimedTileIds.Count : 0;
            SetText(_subtitle, "Select a tile or region");
            ShowStats(false);
            SetText(_body, $"Tiles: {tiles}\nRegions: {regions}\nUnclaimed land: {unclaimed}\nSeed: {(_world.Map != null ? _world.Map.seed : 0)}");
            SetText(_characters, "No region selected.");
            if (_actionsList != null)
            {
                _actionsList.Clear();
                AddHint("Select a region to view decisions and characters.");
            }
        }

        void OnTileHovered(HexTileData tile)
        {
            if (_tooltip == null || _tooltipText == null) return;
            if (tile == null)
            {
                _hoveringTile = false;
                _tooltip.style.display = DisplayStyle.None;
                return;
            }
            _hoveringTile = true;
            var sb = new StringBuilder();
            sb.Append(MapDefinitions.GetTerrain(tile.terrainType).displayName).Append("  ").Append(tile.coord).Append('\n');
            RegionData region = _world.Map.GetRegionForTile(tile);
            sb.Append(region != null ? region.displayName : "Unclaimed wilderness");
            if (tile.specialFeatureTags.Count > 0) sb.Append('\n').Append(string.Join(", ", tile.specialFeatureTags));
            _tooltipText.text = sb.ToString();
            _tooltip.style.display = DisplayStyle.Flex;
            PositionTooltip();
        }

        void PositionTooltip()
        {
            if (_tooltip == null || _root.panel == null) return;
            Vector2 screen = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, screen);
            _tooltip.style.left = panelPos.x + 16f;
            _tooltip.style.top = panelPos.y + 16f;
        }

        // ---------- contextual actions: real decisions + character interactions on RegionData ----------
        void PopulateRegionActions(RegionData region)
        {
            if (_actionsList == null) return;
            _actionsList.Clear();

            if (_directiveTab == DirectiveTab.Decisions)
            {
                var decisions = RegionDecisionSystem.Instance;
                if (decisions != null)
                    foreach (var def in decisions.GetDecisionsFor(region))
                        AddDecisionCard(def, region);
            }
            else // Characters
            {
                var chars = RegionCharacterSystem.Instance;
                var local = chars != null ? chars.GetCharactersInRegion(region.regionId) : null;
                if (local != null && local.Count > 0)
                    foreach (var c in local) AddCharacterCard(c, region);
                else
                    AddHint("No known characters in this region.");
            }
        }

        void PopulateWildernessActions(HexTileData tile)
        {
            if (_actionsList == null) return;
            _actionsList.Clear();
            if (_directiveTab == DirectiveTab.Decisions)
            {
                // Only non-regional decisions are valid with no region selected.
                var decisions = RegionDecisionSystem.Instance;
                if (decisions != null)
                    foreach (var def in decisions.GetDecisionsFor(null))
                        AddDecisionCard(def, null);
                AddAction("Scout", "Reveal what lies on this tile.",
                    () => Log($"Scouting {tile.coord} ({MapDefinitions.GetTerrain(tile.terrainType).displayName})."));
            }
            else // Characters
            {
                AddHint("No characters in the wilderness.");
            }
        }

        // Muted one-line note inside the actions list (empty states / prompts).
        void AddHint(string text)
        {
            if (_actionsList == null) return;
            var hint = new Label(text); hint.AddToClassList("action-desc");
            hint.style.marginTop = 6;
            _actionsList.Add(hint);
        }

        // Simple clickable card (used for non-decision actions like wilderness scouting).
        void AddAction(string title, string desc, System.Action onClick)
        {
            var card = new VisualElement(); card.AddToClassList("action-card");
            var t = new Label(title); t.AddToClassList("action-title");
            var d = new Label(desc); d.AddToClassList("action-desc");
            card.Add(t); card.Add(d);
            card.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());
            _actionsList.Add(card);
        }

        void AddDecisionCard(DecisionDefinition def, RegionData region)
        {
            var system = RegionDecisionSystem.Instance;
            bool enabled = system != null && system.CanApplyDecision(def, region, out _);
            string reason = system != null ? system.GetDisabledReason(def, region) : "Unavailable";

            var card = new VisualElement(); card.AddToClassList("action-card");
            var t = new Label(def.displayName); t.AddToClassList("action-title");
            var d = new Label(BuildDecisionDetail(def, enabled ? null : reason)); d.AddToClassList("action-desc");
            card.Add(t); card.Add(d);
            card.SetEnabled(enabled);
            if (enabled)
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (RegionDecisionSystem.Instance.ApplyDecision(def, region))
                        OnRegionSelectedSafe(region);
                });
            _actionsList.Add(card);
        }

        static string BuildDecisionDetail(DecisionDefinition def, string disabledReason)
        {
            var parts = new System.Collections.Generic.List<string>();
            void Cost(string n, int v) { if (v > 0) parts.Add($"-{v} {n}"); }
            Cost("Money", def.moneyCost); Cost("Sanity", def.sanityCost); Cost("Artifacts", def.artifactsCost);
            void Fx(string n, int v) { if (v != 0) parts.Add($"{n} {(v > 0 ? "+" : "")}{v}"); }
            Fx("Influence", def.influenceDelta); Fx("Stability", def.stabilityDelta); Fx("Development", def.developmentDelta);
            Fx("State Infl", def.stateInfluenceDelta); Fx("State Stab", def.stateStabilityDelta); Fx("State Dev", def.stateDevelopmentDelta);
            if (def.exposureRisk != 0) parts.Add($"Exposure +{def.exposureRisk}");
            string s = parts.Count > 0 ? string.Join(", ", parts) : "No cost";
            if (def.isShadowInstrument) s = "⚠ Shadow  ·  " + s;
            if (!string.IsNullOrEmpty(disabledReason)) s += "  —  " + disabledReason;
            return s;
        }

        void AddCharacterCard(GameCharacter character, RegionData region)
        {
            var card = new VisualElement(); card.AddToClassList("action-card");
            var t = new Label($"{character.displayName} — {character.title}"); t.AddToClassList("action-title");
            var d = new Label($"Trust {character.trust}  Loyalty {character.loyalty}  Rel {character.relationshipWithPlayer}" +
                              (character.recruitedAsContact ? "  [contact]" : "")); d.AddToClassList("action-desc");
            card.Add(t); card.Add(d);

            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap;
            CharacterInteractionType[] shown =
            {
                CharacterInteractionType.Negotiate, CharacterInteractionType.Bribe,
                CharacterInteractionType.Support, CharacterInteractionType.RecruitAsContact,
                CharacterInteractionType.Investigate,
            };
            foreach (var type in shown)
            {
                var sys = RegionCharacterSystem.Instance;
                bool can = sys != null && sys.CanInteract(character, type, out _);
                var b = new Button { text = RegionCharacterSystem.Interactions[type].displayName };
                b.AddToClassList("btn"); b.AddToClassList("mode-btn");
                b.style.marginRight = 4; b.style.marginTop = 4;
                b.SetEnabled(can);
                CharacterInteractionType captured = type;
                if (can)
                    b.clicked += () =>
                    {
                        var result = RegionCharacterSystem.Instance.ApplyInteraction(character, captured);
                        Log(result.message);
                        OnRegionSelectedSafe(region);
                    };
                row.Add(b);
            }
            card.Add(row);
            _actionsList.Add(card);
        }

        // Re-render only if the region is still the selection (avoids stale refresh after load).
        void OnRegionSelectedSafe(RegionData region)
        {
            if (region != null && region == _world.SelectedRegion) OnRegionSelected(region);
        }

        // ---------- helpers ----------
        void ShowStats(bool show)
        {
            if (_statBlock != null) _statBlock.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Log(string message)
        {
            if (_logText != null) _logText.text = message;
            if (PlayerLogExists()) PlayerLog.Add(message);
        }

        static bool PlayerLogExists() => true;

        static void SetText(Label label, string text) { if (label != null) label.text = text; }

        // Fallback global stability as a 0..100 percentage (region.stability is 0..20, so ×5).
        // Mirrors EconomySystem.GlobalStability, used only when the economy system is absent.
        static int AverageStability(WorldMapData map)
        {
            if (map.RegionCount == 0) return 0;
            int sum = 0;
            foreach (var r in map.Regions) sum += r.stability;
            return sum * 5 / map.RegionCount;
        }

        static string Prettify(string pascal)
        {
            if (string.IsNullOrEmpty(pascal)) return pascal;
            var sb = new StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i])) sb.Append(' ');
                sb.Append(pascal[i]);
            }
            return sb.ToString();
        }

        void AddStyleSheets(VisualElement root)
        {
            if (styleSheets == null) return;
            foreach (var sheet in styleSheets)
                if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }

        // Minimal programmatic tree so binding works even without the UXML asset.
        void BuildFallbackTree(VisualElement root)
        {
            var hud = new VisualElement { name = "hud-root" };
            hud.AddToClassList("hud-root");

            var top = new VisualElement { name = "top-bar" };
            top.AddToClassList("top-bar");
            top.Add(MakeReadout("MONEY", "res-money"));
            top.Add(MakeReadout("ARTIFACTS", "res-artifacts"));
            top.Add(MakeReadout("SANITY", "res-sanity"));
            top.Add(MakeReadout("GLOBAL STABILITY", "res-stability"));
            var spacer = new VisualElement(); spacer.AddToClassList("spacer"); top.Add(spacer);
            var date = new Label("DAY 001") { name = "lbl-date" }; date.AddToClassList("date"); top.Add(date);
            var pause = new Button { name = "btn-pause", text = "PAUSE" }; pause.AddToClassList("btn"); pause.AddToClassList("btn-ghost"); top.Add(pause);
            hud.Add(top);

            var left = new VisualElement { name = "left-panel" };
            left.AddToClassList("side-panel"); left.AddToClassList("left-panel");
            left.Add(new Label("WORLD OVERVIEW") { name = "panel-title" });
            left.Add(new Label("") { name = "panel-subtitle" });
            var statBlock = new VisualElement { name = "stat-block" }; statBlock.AddToClassList("stat-block");
            statBlock.Add(MakeStat("Influence", "stat-influence"));
            statBlock.Add(MakeStat("Stability", "stat-stability"));
            statBlock.Add(MakeStat("Development", "stat-development"));
            left.Add(statBlock);
            left.Add(new Label("") { name = "panel-body" });
            left.Add(new Label("") { name = "panel-characters" });
            hud.Add(left);

            var right = new VisualElement { name = "right-panel" };
            right.AddToClassList("side-panel"); right.AddToClassList("right-panel");
            right.Add(new Label("DIRECTIVES") { name = "panel-title-right" });
            var tabs = new VisualElement { name = "directive-tabs" }; tabs.AddToClassList("tab-row");
            var tabDec = new Button { name = "tab-decisions", text = "DECISIONS" };
            tabDec.AddToClassList("btn"); tabDec.AddToClassList("mode-btn"); tabDec.AddToClassList("mode-active");
            var tabChar = new Button { name = "tab-characters", text = "CHARACTERS" };
            tabChar.AddToClassList("btn"); tabChar.AddToClassList("mode-btn");
            tabs.Add(tabDec); tabs.Add(tabChar);
            right.Add(tabs);
            var actions = new VisualElement { name = "actions-list" }; actions.AddToClassList("actions-list");
            right.Add(actions);
            hud.Add(right);

            var bottom = new VisualElement { name = "bottom-log" }; bottom.AddToClassList("bottom-log");
            bottom.Add(new Label("OPERATIONS LOG") { name = "log-title" });
            bottom.Add(new Label("World initialized.") { name = "log-text" });
            hud.Add(bottom);

            var modeBar = new VisualElement { name = "mode-bar" }; modeBar.AddToClassList("mode-bar");
            foreach (var (label, name) in new[]
            {
                ("Terrain", "mode-terrain"), ("Political", "mode-political"), ("Influence", "mode-influence"),
                ("Stability", "mode-stability"), ("Development", "mode-development"), ("Danger", "mode-danger"),
            })
            {
                var b = new Button { name = name, text = label }; b.AddToClassList("btn"); b.AddToClassList("mode-btn");
                modeBar.Add(b);
            }
            hud.Add(modeBar);

            var tooltip = new VisualElement { name = "tooltip" }; tooltip.AddToClassList("tooltip");
            tooltip.Add(new Label("") { name = "tooltip-text" });
            hud.Add(tooltip);

            root.Add(hud);
        }

        static VisualElement MakeReadout(string label, string valueName)
        {
            var wrap = new VisualElement(); wrap.AddToClassList("readout");
            var l = new Label(label); l.AddToClassList("readout-label");
            var v = new Label("--") { name = valueName }; v.AddToClassList("readout-value");
            wrap.Add(l); wrap.Add(v);
            return wrap;
        }

        static VisualElement MakeStat(string label, string valueName)
        {
            var row = new VisualElement(); row.AddToClassList("stat-row");
            var l = new Label(label); l.AddToClassList("stat-name");
            var v = new Label("--") { name = valueName }; v.AddToClassList("stat-val");
            row.Add(l); row.Add(v);
            return row;
        }
    }
}
