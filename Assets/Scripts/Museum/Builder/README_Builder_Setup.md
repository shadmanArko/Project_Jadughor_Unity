# Builder Panel (Phase 1 — Show Placeable Objects)

Unity port of the Godot builder panel. A Bottom-Bar category button opens the panel
and lists that category's placeable objects as clickable cards. **Placement is a later
phase** — clicking a card only raises an event for now.

Namespace `ProjectMuseum.Builder` (+ `.EditorTools`), under
`Assets/Scripts/Museum/Builder/`. Follows the narrative system's conventions.

## Flow

```
BottomBarBuilderController  (auto-spawns 6 category buttons in the Bottom Bar)
        │  click → BuilderActions.OnBottomPanelBuilderCardToggleClicked(type)
        ▼
BuilderPanelController      (shows panel, fills scroll Content with cards)
        │  Flooring → tileManager.AvailableTiles ; else → BuilderDatabase.GetCards(type)
        ▼
BuilderCard (per object)   click → BuilderActions.OnClickBuilderCard(type, cardName)
                                    └─ placement systems subscribe here (next phase)
```

## Categories & data
6 categories (`BuilderCardType`): Exhibit, DecorationShop, DecorationOther, Sanitation,
Flooring, Wallpaper. Five are JSON-driven (`Assets/GameData/Source/*Variations.json`
→ `BuilderDatabase.asset`); **Flooring** comes from the live tileset on
`MuseumTilePlacementManager` (icon = the tile's sprite).

## One-time setup

1. **Import the data.** Menu **Tools ▸ Project Museum ▸ Import Builder JSON** → creates
   `Assets/GameData/BuilderDatabase.asset` with icons resolved from
   `Assets/2D/Museum/{Exhibits,DecorationShops,DecorationOthers,Sanitations,Wallpapers}`.
   Check the Console for any "No icon …" warnings.

2. **Card prefab.** A UI object with `Button` (root) + child `Image` (icon) + `TMP_Text`
   (name) + optional `TMP_Text` (price). Add the **BuilderCard** component and assign
   its icon / name / price / button fields.

3. **Category-button prefab.** A `Button` with a `TMP_Text` child (its text is set
   automatically to the category name).

## Scene wiring

| Component | Put it on | Assign |
|---|---|---|
| `BottomBarBuilderController` | **Bottom Bar** | Category Button Prefab (Button Parent defaults to itself) |
| `BuilderPanelController` | **BuilderPanel** (keep it active) | Database · Tile Manager · **Panel Visual** (a child to show/hide) · **Content** (ScrollView Content) · Card Prefab · Placeholder Icon |

**Important:** `BuilderPanelController` must sit on an always-active object and toggle a
*child* `Panel Visual` — if its own GameObject were disabled it would stop receiving the
open event. Assign `Panel Visual` = the visuals you want to hide (e.g. Builder Body +
background wrapped together, or a content wrapper).

## ScrollView layout (on the existing BuilderPanel)
- **Content**: add `GridLayoutGroup` (Constraint = Fixed Row Count = 1, Start Axis =
  Horizontal, cell ≈ 160×180) + `ContentSizeFitter` (Horizontal = Preferred, Vertical =
  Unconstrained). Content left-anchored so it grows rightward.
- **ScrollRect**: Horizontal = true, Vertical = false, Movement = Clamped; Viewport has
  a `RectMask2D`; assign Content to the ScrollRect's Content field.

## Notes
- Card icons show only the **first frame** of each multi-frame sheet (via
  `NumberOfFrames`), mirroring Godot's AtlasTexture. Built at runtime by
  `BuilderSpriteUtil` (no Read/Write flag needed).
- Clicking the **already-open** category closes the panel (toggle).
- Adding more objects later: edit the JSON (or the `BuilderDatabase.asset` directly) and
  re-run the importer. Lots of extra art already exists in `Assets/2D/Museum/` to draw from.
- The two vending machines were renamed in `decorationShopVariations.json`
  (`vendingmachine1/2`) to match the sprite files.

## Next phase (not built yet)
Placement (ghost/preview, tile footprint via `NumberOfTilesNeeded`, Y-sort via
`YSortable`, cost checks), wallpaper application to walls, a pointer-over-UI guard so
card clicks don't trigger the tile-placement drag, and wiring Flooring card clicks to
`MuseumTilePlacementManager.SelectTile`.
