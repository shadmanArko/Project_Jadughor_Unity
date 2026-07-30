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

## Flooring placement (tile painting)

`MuseumTilePlacementManager` is now **armed only by picking a Flooring card** in the
builder panel — it does nothing until then (no accidental painting from ordinary
clicks, no scroll-wheel tile cycling fighting the camera zoom, no number-key
selection; those bindings were removed, selection comes solely from the panel).

- Flooring card click → the manager finds the matching tile in **Available Tiles**
  by asset name, selects it, and enters placement mode.
- **Left-drag** paints a rectangle (unchanged); drags starting over UI are ignored.
- **Painting is clipped to the museum itself**: only cells with a record in
  `MuseumData.Tiles` (i.e. developed chunks) are paintable — the preview and the
  actual paint both skip anything outside (roads/forest untouched). Expanding a
  chunk automatically makes its cells paintable, since expansion seeds their tile
  records. (`MuseumDataModel` is `[Inject(Optional)]`-ed into the tile manager;
  without a Zenject context it falls back to unrestricted painting.)
- **Right-click / Esc cancels placement mode** (it no longer erases tiles — the
  erase code is kept unbound for a future bulldoze tool).
- Picking a card from any OTHER category disarms tile painting, and picking a
  Flooring card cancels a pending object ghost — only one placement mode can be
  active at a time.

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

### Sprite pivot — objects must be anchored at their BASE, not their center

An isometric object (a box with height) should sit with its **bottom** on the tile,
not have the tile pass through its visual middle. Two places control this:

1. **Runtime-swapped sprite** (what actually renders once placed in Play mode) — fixed
   in code: `BuilderSpriteUtil.FirstFrameSprite(..., pivot)` now takes an explicit pivot,
   and `MuseumObjectPlacementSystem` passes `BuilderSpriteUtil.BottomCenterPivot`
   (`(0.5, 0)`) for every ghost and placed object. You don't need to do anything for
   this part — it's already bottom-anchored.
2. **Your prefab's own placeholder sprite** (what you see in the Scene view when you
   drag the prefab in directly to check alignment, as in your screenshot — this runs
   BEFORE any runtime swap, so it uses whatever pivot the source texture was imported
   with). If that pivot is "Center" (Unity's default), the object will look
   misaligned in the editor even though the runtime version is fine. Fix it per
   texture: select the sprite in the Project window → **Sprite Editor** (or the
   Inspector's Pivot dropdown for Single-sprite-mode textures) → set **Pivot: Bottom**
   (or Custom `X=0.5, Y=0`) → **Apply**. Do this for every sprite/frame in
   `Assets/2D/Museum/{Exhibits,DecorationShops,DecorationOthers,Sanitations}` that you
   use as a prefab placeholder, so editor preview matches what Play mode will show.

> Note for multi-tile objects (2×2, etc.): bottom-center assumes the art is
> horizontally symmetric around its own footprint — true for the boxes shown. If a
> future asset's visual "front" isn't centered on its own bounding box, its pivot may
> need a custom X offset instead of 0.5.

### Anchor point — tile CORNER, not CENTER

Even with a bottom-pivoted sprite, the ghost/placed object still needs to be
positioned at the right point on the tile. This was using
`Grid.GetCellCenterWorld` (the tile's visual middle) — for an **Isometric** grid,
center and corner differ **diagonally** in world space (not just vertically), so
the object rendered offset down-and-right of the tile you were actually hovering.

Fixed: `MuseumObjectPlacementSystem.CellToWorld` now uses `Grid.CellToWorld` (the
cell's corner, which for an isometric cell is its front/bottom vertex) instead of
`GetCellCenterWorld` — combined with the bottom-pivoted sprite, the object's base
now lands exactly on that vertex.

If it's still a few pixels off after this (e.g. from cell gap or padding baked into
your art), nudge it with the new **Anchor Offset** field (world units) on
`MuseumObjectPlacementSystem` — tune it live in Play mode until the ghost sits
exactly on the tile.

### Museum Sorting (`MuseumSortingSystem`) — depth sorting for placed objects

Placed museum objects (and the ghost) are **no longer sorted by `YSortable`** — they
use a dedicated **`MuseumSortingSystem`** (Placement/MuseumSortingSystem.cs), the
Unity port of the Godot project's `ManualSorting.cs`.

**Why:** with mixed footprint sizes (1×1 next to 2×2, wide 2×1 rows…), NO single
scalar depth value — pivot Y, bounds Y, or any "corner sum" of cell coordinates —
can order every configuration correctly (e.g. a wide front row vs a 1×1 directly
behind its left tile inverts any such formula). Several single-key attempts each
fixed some layouts and broke others, which matched the in-game symptom exactly
("working for some not for all"). The Godot solution was **pairwise + dynamic**:
compare items two at a time using tile coordinates and footprint, re-running on
every item update. This port keeps that model but replaces Godot's fragile
±0.1-Y-position nudging with deterministic explicit sort orders.

**How it works:**
- The placement system registers every spawned object (and the ghost) with its
  anchor cell + footprint; the ghost re-registers whenever it moves to a new cell
  (the dynamic part — mirrors Godot's `OnItemUpdated` re-sort trigger).
- On every change it derives pairwise constraints — using the axis convention
  already validated by `ExpansionManager` (+X/+Y cell directions = the BACK edges):
  **A draws in front of B if every tile of A is at lower X than all of B, or at
  lower Y than all of B.** Diagonal neighbours (front on one axis, behind on the
  other) can't visually overlap and get no constraint.
- Constraints are flattened by topological sort (back-most first; exact integer
  `x+y` of the far corner only breaks ties between unrelated objects), and the
  resulting order is written to **every `SpriteRenderer`** under each object, so
  multi-part prefabs always sort as one unit.

**Wiring:** none needed — `MuseumObjectPlacementSystem` auto-adds the component to
its own GameObject if the serialized field is empty. It also **removes any
`YSortable`** from spawned instances/ghosts so the two systems never fight over
`sortingOrder`. `YSortable` remains the simple Y-based sorter for non-grid things
(characters, walls, one-off scene sprites).

> If your 2×2 prefab genuinely has separate sub-pieces (not just one sprite drawn
> to *look* like several boxes), note that `PlaceableObjectView.ApplyVariationSprite`
> still only swaps ONE renderer's sprite (`Primary Renderer`, or the first one
> found) — the other pieces keep whatever placeholder art the prefab was built
> with. That's separate from sorting; flag it if each sub-piece needs its own
> per-variation artwork.

### Rotation (Q/E) — Godot parity

Matches the Godot behavior exactly: rotating is a **sprite-sheet frame swap**, not a
transform rotation, and it only affects the **pending ghost** — there's no
rotate-in-place interaction for already-placed objects yet (Godot didn't have one
either; that would ride on a future "select/move a placed object" system).

- **Q** steps the rotation frame forward, **E** steps it backward (configurable —
  `Rotate Forward Key` / `Rotate Backward Key` on `MuseumObjectPlacementSystem`),
  wrapping at the variation's `NumberOfFrames` (typically 4 = one sprite per facing).
  A variation with only 1 frame simply can't be rotated (no-op).
- The ghost's sprite re-crops to the new frame immediately (`BuilderSpriteUtil.FrameSprite`,
  a generalization of the old always-frame-0 crop). `BuilderActions.OnPlacementRotated(int)`
  fires each step (mirrors Godot's `OnItemRotated`) if you want to hook a sound effect etc.
- Rotation **persists across placing several objects in one open session** (right-click/Esc
  to stop) — rotate once, then lay down a whole row facing the same way — and only
  resets to frame 0 when you pick a **new** card. This is a small, deliberate departure
  from Godot (which always started a fresh drag instance per item); flag it if you'd
  rather it reset after every placement instead.
- Footprint (`WidthInTiles`/`LengthInTiles`) never changes with rotation — matches
  Godot exactly (no width/length swap on rotate, even for non-square footprints).
- **Persisted correctly**: `PlacedObjectData.RotationFrame` (this field already existed
  in the data model, just unused until now) is set from the pending frame at placement
  time, and `SpawnVisual` — the SAME method used both for a fresh placement and for
  respawning from a loaded save — reads it back to re-crop the correct frame. No
  special-casing needed between "just placed" and "loaded from disk."

Then create **Project Museum ▸ Placeable Prefab Config** and add one entry per prefab
(Type + Width In Tiles + Length In Tiles + Prefab), and assign it to `MuseumInstaller`.
Console warns (with the sizes you *have* configured for that type) if a variation asks
for a size you haven't built.

### Data flow (how a size is chosen)

```
BuilderCard click → BuilderActions.OnClickBuilderCard(type, cardName)
  → MuseumObjectPlacementSystem.OnClickBuilderCard
      → BuilderDatabase.TryGetPlacementInfo(type, cardName)
            finds the variation by name, returns PlacementInfo
            { WidthInTiles, LengthInTiles, Texture, NumberOfFrames, Cost }
      → PlaceablePrefabConfig.GetPrefab(type, info.WidthInTiles, info.LengthInTiles)
            EXACT match only — no match → warns (with configured sizes) → no ghost
```

**Where WidthInTiles/LengthInTiles actually come from, per category:**
- **DecorationShop / Sanitation** — explicit `WidthInTiles`/`LengthInTiles` fields
  already in their JSON (ground truth, no guessing).
- **Exhibit / DecorationOther** — the Godot source data only ever had
  `NumberOfTilesNeeded` (a single ambiguous number), so these two models now carry
  their own explicit `WidthInTiles`/`LengthInTiles` fields too (added on our side,
  not from Godot). **If left at `0` (unset), `BuilderDatabase` falls back to
  `NumberOfTilesNeeded × 1`** — which is almost never the footprint you actually want
  for anything wider than 1 tile, and is the exact bug that made `BasicExhibit4x4`
  fail to preview/place (it derived to 2×1, not the 2×2 you'd configured a prefab
  for). Both exhibit variations and both decoration-others now have explicit
  values set in `exhibitVariations.json` / `decorationOtherVariations.json`
  (`BasicExhibit1x1`→1×1, `BasicExhibit4x4`→**2×2** — my best guess from the "4x4"
  name; change it directly in the JSON if that's wrong for your art).

**⚠ Re-run Tools ▸ Project Museum ▸ Import Builder JSON after this change** so
`BuilderDatabase.asset` picks up the new fields — otherwise it's still serving the
old (wrong) footprints.

> **Reachable sizes today:** 1×1 (Exhibit small, both Shop, both DecorationOther),
> 2×2 (Exhibit "4x4", Sanitation Toilet1). Nothing currently requests 1×2 or plain
> 2×1 — add a variation with those explicit values (or a prefab config entry with
> a matching size) if you need one.

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

### Wallpaper placement (drag-select on back walls)

Every wall segment gets a `WallData` record (`container/childIndex` id) in
`MuseumData.Walls`. Placement flow:

- A **Wallpaper card** arms wallpaper mode (and disarms tile/object placement —
  one mode at a time, in both directions).
- **Left-drag** along the wall line selects segments: each wall is mapped to its
  grid cell once at startup, and the drag's start→current cell rectangle decides
  what's selected — same rectangle model as floor painting. **Only segments in
  the `Back Wall Containers` are selectable** (assign Left Walls + Right Walls
  there); front walls never react.
- **A wall sprite under the pointer wins**: the drag cell comes from hit-testing
  the back-wall sprites' renderer bounds directly — the pointed-at wall
  contributes its own edge-tile cell. (Cell math on a wall's upper area drifts
  diagonally with height on an isometric grid, shifting the selection ALONG the
  wall the further you are from the back corner — hit-testing avoids that
  entirely.) Pointers over no wall fall back to WorldToCell + nearest-museum-tile
  snapping.
- **A single click applies one wall** — click-release without moving makes a
  1-cell selection, so one-by-one painting works with no separate mode.
- **A floor overlay marks the selected walls' edge tiles** (only those) with the
  `Selection Overlay Tile`, tinted the same green/red as the walls.
- Selected walls **preview live**: the wallpaper sprite is swapped in with a
  **green tint**, turning **red** when the total (price × selected count) exceeds
  your money. Walls dropped from the selection mid-drag revert to their saved look.
- **Release** applies: writes each wall's `WallpaperName` to data and deducts the
  total cost — or just restores the preview if unaffordable. The mode stays armed
  for more drags; **right-click / Esc exits**.
- Saved wallpapers reapply on load; "Clear All Wallpapers" (context menu on
  `MuseumWallpaperSystem`) restores original sprites and saves `""` (free).
- **Expansion walls register automatically.** Expanding the museum spawns new wall
  containers at runtime; `ExpansionManager` raises `OnMuseumWallBuilt` (back walls
  flagged selectable) / `OnMuseumWallRemoved` (seam teardown), and the wallpaper
  system registers/unregisters them live — so newly expanded back walls are
  immediately wallpaper-able, and walls torn down where two chunks join stop being
  selectable. Only the initial walls need manual assignment in the Inspector.

**New Inspector fields on `MuseumWallpaperSystem`:** `Back Wall Containers`
(assign **Left Walls** and **Right Walls**), `Grid` (auto-found if empty), the
green/red tints, **`Selection Overlay Tile`** (assign a highlight tile — e.g. the
"tile selection" tile in `Assets/2D/Museum/Floorings`; without it the floor overlay
is simply skipped) and **`Floor Tilemap`** (the overlay copies its sorting so it
draws above the floor). `Wall Containers` still lists ALL four containers (fronts
are registered/saved, just not selectable).

⚠ On `MuseumData.asset`, keep **Default Tile Variation Name EMPTY** — floor records
then only fill in when the player actually paints, so loading never repaints your
hand-authored floor with a default tile.

---

# Phase 3 — Interaction + Exhibit Artifact Placement

## Clicking placed objects (`IInteractable`)

`PlaceableObjectView` now implements **`IInteractable`** (`void Interact()`) — every
placed object is clickable. `MuseumInteractionSystem` (put on a manager object) turns a
left-click into `Interact()` on the **front-most** placed object under the cursor
(sprite-bounds hit-test, highest `sortingOrder` wins — no colliders needed). It ignores
clicks over UI and while a build/paint mode is active.

`ExhibitObjectView.Interact()` raises `BuilderActions.OnExhibitClicked(exhibitId)`.
**Exhibit prefabs must have the `ExhibitObjectView` component** (not the base
`PlaceableObjectView`) for click-to-open to work — add it to your exhibit prefab roots.
Other categories inherit the base no-op `Interact()` (hooks for their own UIs later).

## Artifact data (`MuseumArtifactDatabase` + importer)

Same pattern as `BuilderDatabase`: run **Tools ▸ Project Museum ▸ Import Artifact Data**
→ reads `Assets/Resources/ArtifactCatalogue/RawArtifactDescriptiveDataEnglish.json` +
`RawArtifactFunctionalData.json`, merges by `Id`, and resolves **two actual `Sprite`s
per artifact** (the DB stores real sprite references, not the stale `res://` path
strings from the JSON):
- **`Icon`** — the UI card/slot icon, by **ArtifactName** in `Assets/2D/UI/MineUi/Artifacts`.
- **`IsometricSprite`** — the in-game sprite shown on the physical exhibit (used when
  placed artifacts render in the museum — next phase), by **Id** in
  `Assets/2D/Museum/Isometric View Artifacts`.

Writes `Assets/GameData/MuseumArtifactDatabase.asset`; the console reports how many of
each sprite it couldn't resolve. Assign that asset to `MuseumInstaller` (new
**Artifact Database** field).

Tags shown on cards come from the functional data, in order: **Era, Region, Object,
ObjectSize, then each Material**.

## Owned artifacts + slots (data)

- `MuseumData.Artifacts` — `OwnedArtifactData { InstanceId, RawArtifactId }`: what the
  player owns (from mining later; a debug seed fills it for now).
- `ExhibitData.Slots` — `ArtifactSlotAssignment { SlotIndex, ArtifactInstanceId }`:
  which artifact sits in which slot (replaces the old flat `ArtifactIds`).
- Model API: `GetUnplacedArtifacts()`, `AssignArtifactToSlot(exhibitId, slot, instance)`
  (an instance lives in at most one slot — reassigning frees the old spot),
  `RemoveArtifactFromSlot(...)`, `AddOwnedArtifact(rawId)`. Changes raise
  `OnExhibitArtifactsChanged`.

## The exhibit editor UI

`ExhibitEditorUI` (on an initially-hidden editor panel; injected). On
`OnExhibitClicked`: shows the panel, builds the slot grid (**2×2 slots per exhibit tile**
— a 1×1 exhibit = 4 slots, a 2×2 = 16, columns = width×2), fills the **left list** with
draggable `ArtifactCard`s for owned-and-unplaced artifacts, and marks filled slots.
Drag a card onto a slot to place; **left-click a filled slot to remove**. Close button
hides it. `debugFillStorageFromCatalog` seeds one of every artifact on first open so
there's something to test with — turn OFF for real play.

### ⚡ Quick build (recommended): the generator
Run **Tools ▸ Project Museum ▸ Build Exhibit Editor UI**. It creates the whole
"Exhibit Editor Canvas" (dimmer bg → ~70% window → left scroll list + right centred
grid + bottom bar + close button + drag layer) in the open scene, generates the
`ArtifactCard` + `ArtifactSlot` prefabs under `Assets/Prefabs/Exhibit Editor/`, wires
every `ExhibitEditorUI` field, and adds an EventSystem if missing. Then just recolour /
resize to taste. (Re-running adds a fresh canvas — delete the old one first.)

You still need to: run **Import Artifact Data**, assign the artifact DB to
`MuseumInstaller`, put `ExhibitObjectView` on exhibit prefabs, and add
`MuseumInteractionSystem` to a manager object.

### Building the prefabs by hand instead
- **Artifact card** (`ArtifactCard`): root with a horizontal/whatever layout → `Image`
  (icon) + `TMP_Text` (name) + tags. For tags, either assign a **tag-chip prefab**
  (a small `TMP_Text`) + a **tag container** (a HorizontalLayoutGroup) — one chip per
  tag — or just a single **tags label** as a fallback. It's a drag source (no extra
  setup; the script implements the drag handlers).
- **Artifact slot** (`ArtifactSlot`): a square with a child `Image` (assigned to its
  `icon` field) that shows the placed artifact.
- **Editor panel**: a full-screen (or windowed) panel, hidden by default, with a left
  ScrollView (assign its Content to `storageContent`, the card prefab), a right grid
  (a `GridLayoutGroup`; assign it + its Content, the slot prefab), a **Close** button,
  and a top-level **Drag Layer** RectTransform (usually the editor's canvas root) for
  the drag ghost. It needs a Canvas + GraphicRaycaster and the scene an EventSystem
  (already present).

### Scene wiring
Add `MuseumInteractionSystem` (any manager object) and `ExhibitEditorUI` (on the editor
panel) — both live under the scene with the Zenject SceneContext + `MuseumInstaller`.
Put `ExhibitObjectView` on exhibit prefabs.

## Still to come
- Artifacts **rendered on the exhibit in the world** (the editor UI places them in
  data + slots; showing the iso artifact sprites on the physical exhibit is next —
  the iso sprites are already resolvable by Id).
- **Size-based slot rules** (Godot's Tiny=1 slot / Small=column / Medium=whole 2×2):
  right now it's one artifact per slot regardless of `ObjectSize` (still shown as a tag).
- Real artifact acquisition from mining (replaces the debug seed), condition/rarity,
  per-wall wallpaper art, object move/remove UI, master SaveData merge.
