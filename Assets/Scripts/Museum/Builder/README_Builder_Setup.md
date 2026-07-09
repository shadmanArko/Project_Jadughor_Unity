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

---

# Phase 2 — Museum Data + Object Placement (Zenject + UniRx)

## Architecture

```
MuseumDataAsset (SO, working copy — inspect live state in the editor)
└── MuseumData: Info(money) · DevelopedChunks · Tiles · PlacedObjects · Exhibits
        ▲ mutated ONLY by
MuseumDataModel (Zenject IInitializable/IDisposable, UniRx)
  · ReactiveProperty: Money, PlacedObjectCount, DevelopedChunkCount
  · API: CanPlace / PlaceObject / RemoveObject / SetFloorTile / RegisterExpandedChunk
  · Persistence: JSON at Application.persistentDataPath/museumData.json
        ▲ injected into
MuseumObjectPlacementSystem (MonoBehaviour)
  · card click → grid-snapped ghost (green/red = valid+affordable / not)
  · LMB place (repeats for multiples) · RMB/Esc cancel · UI clicks ignored
  · Start(): respawns every saved object from data
```

Hooks already wired: `ExpansionManager.TryExpand` → `OnMuseumChunkExpanded` (seeds
the chunk's tile records); `MuseumTilePlacementManager.PlaceRectangle` →
`OnFloorTilePainted` (records painted floors). New actions live in `BuilderActions`
(placement started/cancelled, object placed/removed, floor painted, chunk expanded).

## Scene setup (one-time)

1. **Assets**: Create **Project Museum ▸ Museum Data** (`Assets/GameData/MuseumData.asset`)
   — set Chunk Size (20×18) to match ExpansionManager. Create
   **Installers ▸ Museum Installer** (`Assets/GameData/MuseumInstaller.asset`) and
   assign MuseumData + BuilderDatabase to it.
2. **SceneContext**: the Museum scene needs one (GameObject ▸ Zenject ▸ Scene Context).
   Add `MuseumInstaller` to its **Scriptable Object Installers** list.
3. **Placement system**: add `MuseumObjectPlacementSystem` to a manager object;
   assign the Grid and an "Objects" parent transform. Tune `worldPixelsPerUnit` /
   `spriteYOffset` so sprites sit on tiles correctly.

## Notes
- Data model auto-loads the save on init, else seeds a new game (chunk 0,0).
  `MuseumDataModel.Save()` writes the JSON — call it from your save point
  (sleep/save spot) or a debug key; nothing auto-saves yet.
- The SO asset keeps play-mode changes **in the editor only** — handy for
  inspection; the JSON is the real save. New Game = `InitializeNewGame()`.
- Placing an exhibit also creates its `ExhibitData` row (artifact slots) —
  artifact placement is the next phase.
- Money starts at `StartingMoney` (asset); placements deduct their cost and are
  rejected when unaffordable.

## Testing save/load (MuseumSaveTester + friends)

Extra scene components for the full test loop:

| Component | Put it on | Assign |
|---|---|---|
| `MuseumSaveTester` | a "Museum Test" GameObject | nothing — right-click the component header for **Save Museum / Load Museum / New Game / Delete Save / Log State** (works in Play mode); F5/F9 hotkeys optional |
| `MuseumWallpaperSystem` | a manager object | **Wall Containers** = Left Walls, Right Walls, Bottom Left Walls, Bottom Right Walls |
| `MuseumFloorSync` | a manager object | Floor **Tilemap** + `MuseumTilePlacementManager` |

Test loop: place objects from cards, paint floors, click a wallpaper card (applies to
all registered walls for now) → **Save Museum** → move/remove stuff or restart Play →
**Load Museum** → objects respawn, floors repaint, wallpapers reapply.
All three systems refresh via `BuilderActions.OnMuseumDataReloaded`.

Wallpaper notes: every wall segment gets a `WallData` record (`container/childIndex`
id) in `MuseumData.Walls`. Card click = apply to ALL walls (per-wall click selection
comes later). "Clear All Wallpapers" (context menu on `MuseumWallpaperSystem`)
restores the original sprites and saves `""`.

⚠ On `MuseumData.asset`, keep **Default Tile Variation Name EMPTY** — floor records
then only fill in when the player actually paints, so loading never repaints your
hand-authored floor with a default tile.

## Still to come
Artifact-in-exhibit placement, per-wall wallpaper selection + real wall art,
object move/remove UI, rotation frames, folding MuseumData + PlayerInfo into one
master SaveData.
