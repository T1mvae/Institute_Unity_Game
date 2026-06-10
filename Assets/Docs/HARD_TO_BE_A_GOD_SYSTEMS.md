# Hard to Be a God — Feudal States, Economy, Decisions/Events, Win/Loss

Adds the thematic strategy layer on top of the corrected `Institute.World` map. Verified to compile
(Roslyn, 0 errors) in player + editor configs.

## Part 1 — Feudal states / nations
- `StateData` (`Institute.World`): stateId, displayName, colorHex, capitalRegionId, regionIds,
  state-level stability/development/influence.
- `RegionData.stateId`, `WorldRegionSave.stateId`, `WorldMapSave.states`, `WorldMapData.statesById`
  (+ `GetState`, `GetStateForTile`, `States`, `StateCount`).
- `WorldMapGenerator.GenerateStates()` runs after region growth: picks 3–6 most-developed, spaced
  capital regions, then a **multi-source BFS over the region adjacency graph** partitions all regions
  into contiguous states. Names are themed by the seed region type (Kingdom / Duchy / Temple Domain /
  Free Cities / Highland Clans / Confederation); colors are violet/gold/teal/crimson/emerald/terracotta.
  Disconnected regions attach to the nearest seed. `WorldStateUtil.RecomputeAll` sets state aggregates.
- `WorldMapSerializer` persists `stateId` + the states list to `<slot>.world.json`.
- `MapColors` **Political** mode colors each region by its state's `colorHex`; unclaimed = `MapPalette.Unclaimed`.

## Part 2 — Dynamic economy & daily tick
- `EconomySystem` (`Institute.World.Gameplay`) subscribes to `TimeManager.OnNewDay` (the installer now
  adds `GameDateTracker` + `TimeManager` + `EconomySystem`).
- **Income** = `baseIncome (10)` + `Σ region.development × (influence/100) × 0.06`.
- **Artifacts**: regions on Ruins/RuinedZone with influence ≥ 50 have a 5%/day chance to yield an artifact.
- **Sanity**: drains under high Exposure and low global stability; recovers when traces are cold (Exposure < 20).
- **Exposure / Suspicion** (0..100): player-global meter; Shadow Instruments and risky decisions/events
  raise it; it decays ~1/day. Persisted in the save companion.
- A `PlayerLog` line is written every day. The HUD shows `res-money`/`res-artifacts`/`res-sanity`
  **tooltips** with the exact daily math, plus a live `Exposure` readout next to global stability.

## Part 3 — Decisions & events
- `DecisionDefinition` extended: `targetType` (Region/State/Self), `isShadowInstrument`, `exposureRisk`,
  `minStateStability`, `stateStability/Influence/DevelopmentDelta`.
- `RegionDecisionSystem`: Self/Shadow always available; State decisions require a region in a state and
  honor `minStateStability`; State deltas hit `StateData` and **propagate half to every member region**;
  Shadow Instruments apply to the selected region and add high Exposure. `decisions.json` gains Royal
  Audience + Foment Dissent (State), Safe House: Rest (sanity regen), and Shadow: Spy Drone Recon /
  Silent Removal / Covert Toxin.
- `RegionEventSystem` + `EventScope.State`: state events apply to the **entire kingdom** (all member
  regions); when no State-scoped JSON events exist it reuses Local events kingdom-wide. Character status
  flows to state influence (interactions + a daily pass where loyal/recruited rulers raise influence,
  hostile ones erode it). **Anton (mentor)** appears as a recurring personal event during sanity strain
  (< 70), offering cynical counsel (Sanity +6 / Exposure +4) or refusal (Sanity −4).

## Part 4 — Win / loss (evaluated daily in `EconomySystem`)
- **Loss**: Sanity ≤ 0, OR global average stability < 15%, OR Exposure ≥ 100.
- **Victory**: ≥ 80% Institute influence in **every** state, OR `EconomySystem.VictoryReformPassed`
  (a final reform flag a capital decision can set).
- Calls `GameManager.Instance.SetGameOver(isWin)` once; the HUD shows a centered VICTORY/DEFEAT banner.

## Testing
- `Tools / Institute Game / Validate Map Data` then play: **Political** mode shows 3–6 colored kingdoms;
  the region dossier shows the owning state + its stats.
- Watch the operations log for the daily economy line; hover Money/Sanity/Artifacts for the breakdown.
- Use a State decision (Royal Audience) and a Shadow Instrument (Spy Drone Recon) — watch Exposure rise.
- Press **F9** for the diagnostics panel (now includes state count, exposure, global stability, income).
