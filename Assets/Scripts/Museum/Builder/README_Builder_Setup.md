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
└── MuseumData: Info(money) · DevelopedChunks · Tiles · PlacedObjects · Exhibits · Walls
        ▲ mutated ONLY by
MuseumDataModel (Zenject IInitializable/IDisposable, UniRx)
  · ReactiveProperty: Money, PlacedObjectCount, DevelopedChunkCount
  · API: CanPlace / PlaceObject / RemoveObject / SetFloorTile / RegisterExpandedChunk
  · Persistence: JSON at Application.persistentDataPath/museumData.json
        ▲ injected into
MuseumObjectPlacementSystem (MonoBehaviour)
  · card click → look up variation's prefab in PlaceablePrefabConfig → ghost copy
    (green/red = valid+affordable / not) follows the mouse, grid-snapped
  · LMB place (repeats for multiples) · RMB/Esc cancel · UI clicks ignored
  · Start(): respawns every saved object from its prefab
```

Hooks already wired: `ExpansionManager.TryExpand` → `OnMuseumChunkExpanded` (seeds
the chunk's tile records); `MuseumTilePlacementManager.PlaceRectangle` →
`OnFloorTilePainted` (records painted floors). New actions live in `BuilderActions`
(placement started/cancelled, object placed/removed, floor painted, chunk expanded,
wallpaper changed, data reloaded).

## Placeable-object prefabs (`PlaceablePrefabConfig`)

Ghost previews and placed objects are now **real prefabs**, keyed by **category +
footprint size** (not by individual variation) — this fixes both the missing-object
and wrong-size-preview issues, and matches the fact that many variations (e.g. every
plant color) share the same size and just need a different sprite.

You don't make one prefab per variation — you make one prefab per **(Type, Width ×
Length)** you need, e.g. Exhibit-1x1, Exhibit-2x1, Sanitation-2x2, DecorationOther-1x1.
At spawn time the system:
1. Looks up the clicked variation's real size + texture from `BuilderDatabase`.
2. Fetches the matching sized prefab from `PlaceablePrefabConfig`.
3. Instantiates it, then calls `view.ApplyVariationSprite(...)` to swap in that
   specific variation's cropped artwork (first frame of its sheet) — so the shared
   prefab actually shows the right object.

Each prefab: a root `GameObject` sized/anchored for its footprint, with a
`SpriteRenderer` and a **`PlaceableObjectView`** component (or a subclass —
`ExhibitObjectView`, `ShopObjectView`, `DecorationObjectView`, `SanitationObjectView`).
If a prefab has multiple renderers, set **Primary Renderer** on the view to the one
that should receive the swapped sprite (defaults to the first one otherwise). If a
prefab has no view component at all, the placement system adds a base
`PlaceableObjectView` automatically — but adding the right subclass yourself keeps the
door open for per-category logic (e.g. exhibit artifact slots) later.

> **Give that renderer a placeholder sprite sized correctly for the tile footprint**
> (any sprite — even a plain box — works). `ApplyVariationSprite` reads that
> placeholder's visual size and rescales the renderer so the swapped-in variation
> art fills the exact same footprint, regardless of the source texture's own pixel
> size or PPU. Skip this and the swapped art renders at whatever size its raw pixel
> dimensions happen to produce, which is usually NOT what you want.

Then create **Project Museum ▸ Placeable Prefab Config** and add one entry per prefab
(Type + Width In Tiles + Length In Tiles + Prefab), and assign it to `MuseumInstaller`.
Console warns (with the sizes you *have* configured for that type) if a variation asks
for a size you haven't built.

> **Which sizes are actually reachable right now** (checked against the current data):
> **1×1** — Exhibit `BasicExhibit1x1`, both DecorationShop, both DecorationOther.
> **2×1** — Exhibit `BasicExhibit4x4` (its `NumberOfTilesNeeded` is 2, derived as a
> 2-wide row; despite the "4x4" name it is NOT a real 2×2 today). **2×2** — Sanitation
> `Toilet1` (its data already carries real `WidthInTiles`/`LengthInTiles` = 2×2).
> **1×2 is not reachable by anything yet** — no current variation produces that
> combination. If you're building a 1×2 prefab, either a variation needs to actually
> use it, or the Exhibit/DecorationOther footprint derivation should read the data's
> `TilesExtendInDirection` field to pick the axis instead of always defaulting to
> width — tell me if you want that derivation fixed.

## Scene setup (one-time)

1. **Assets**: Create **Project Museum ▸ Museum Data** (`Assets/GameData/MuseumData.asset`)
   — set Chunk Size (20×18) to match ExpansionManager. Create
   **Project Museum ▸ Placeable Prefab Config** and assign your prefabs (see above).
   Create **Installers ▸ Museum Installer** (`Assets/GameData/MuseumInstaller.asset`)
   and assign MuseumData + BuilderDatabase + PlaceablePrefabConfig to it.
2. **SceneContext**: the Museum scene needs one (GameObject ▸ Zenject ▸ Scene Context).
   Add `MuseumInstaller` to its **Scriptable Object Installers** list.
3. **Placement system**: add `MuseumObjectPlacementSystem` to a manager object;
   assign the Grid and an "Objects" parent transform.

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
- **Nothing placing?** `MuseumObjectPlacementSystem` now logs *why* every blocked
  click failed (over UI / no space / can't afford) — check the Console. A very
  common cause: a full-screen background `Image` somewhere in the canvas with
  **Raycast Target** left ON, which makes Unity's EventSystem think the pointer is
  "over UI" everywhere, so clicks in the world are silently ignored. If you see
  "Click ignored — pointer is over UI" for clicks over the museum floor, find that
  background and turn its Raycast Target off.
- Missing-injection is now a loud `Debug.LogError` (not a silent no-op) if the scene
  has no SceneContext/MuseumInstaller, or the installer is missing a reference.

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
