# Sheet Frame Animation

> Looking for which row is which animation? See [SheetRowMaps.md](SheetRowMaps.md) — every
> character's row map, frame counts and paste-ready clip lists.

Unity port of the Godot character animation setup: one sheet texture + a grid, and animation
clips that key an integer frame index. Frame numbers from the Godot scenes carry over unchanged.

## Mapping from Godot

| Godot | Unity |
| --- | --- |
| `Sprite2D.texture` | `SheetSpriteRenderer._sheet` |
| `Sprite2D.hframes` / `vframes` | `Columns (hFrames)` / `Rows (vFrames)` |
| `Sprite2D.frame` (int, row-major) | `SheetSpriteRenderer.Frame` — same indices |
| `Sprite2D.offset` + centring | `Pivot` (default `(0.5, 0)` = bottom-centre) |
| `Sprite2D.scale.x = -1` | `Flip X` |
| `AnimationPlayer` track `Sprite2D:frame`, `update = 1` | `AnimationClip` discrete curve on `Frame` |
| Guest's 8 parts all keying the same frames | one `SheetSpriteGroup` broadcasting to layers |
| `AnimationPlayer.Play("walk_forward")` | `Animator.Play("walk_forward")` |

## Single-sprite character (main character, professor)

1. GameObject with `SpriteRenderer` + **Sheet Sprite Renderer**.
2. Assign the sheet, set Columns/Rows (the male main character sheet is `8 x 4`, same as Godot),
   and set Pixels Per Unit to match the texture's own import setting — `32` for the existing
   character sheets.
3. Scrub the **Frame** slider or click a cell in the sheet grid drawn in the inspector to find
   the index of a pose. The Scene view updates immediately.
4. Add an `Animator`, then generate the clips (below).

## Layered character (guests)

The Godot guest keyed `Shadow / Skin / Eye / Hair / Shoe / Pant / Cloth / OverCloth` with
identical frame values. Do the same here with **Sheet Sprite Group**:

1. Parent GameObject with `SheetSpriteGroup`, one child per part with a `SheetSpriteRenderer`.
2. All parts share the grid layout, so the same index is the same pose in every sheet.
3. `Collect Child Layers` fills the layer list in hierarchy order; list order becomes sorting
   order (index 0 renders furthest back).
4. Animate `SheetSpriteGroup.Frame` — one track drives the whole stack, so adding a layer later
   does not mean re-keying every clip.
5. Swap variants at runtime with `SetLayerSheet(index, texture)`; the current pose is preserved.

## Generating clips

`Tools ▸ Project Museum ▸ Frame Animation Clip Builder`

Set the Animator Root and the Frame Target, pick an output folder, then either hit a **preset**
button or paste your own list.

### Presets

One button per existing character sheet — **Guest**, **Player / Alex**, **Professor**, **Emily** —
each filling in that character's established clip list (23 clips for the guest, 4–5 for the rest).
The button for the preset matching the target's grid is marked `✔`; picking one written for a
different grid asks for confirmation first, because frame indices only mean anything on the grid
they were authored for. Presets live in
[Editor/FrameClipPresets.cs](Editor/FrameClipPresets.cs) — add a character by adding an entry.

Loading a preset fills the editable clip list, so it's a starting point, not a commitment.

### Writing your own

Syntax is `name: frames @ fps loop` — fps and the loop keyword are optional (default 10,
looping); `once` turns looping off. Ranges may descend, and singles mix with ranges (`8-15,0`).

`Add One Clip Per Row` seeds one clip per sheet row when you don't know the ranges yet. Assigning
an Animator Controller also creates a state per clip. Re-running overwrites clips of the same name
in place, so tweaking a rate or range and regenerating keeps existing transitions intact.

### Main character — `Male Main Character (2).png`, 8 x 4

```
walk_forward: 0-7 @ 10 loop
walk_backward: 8-15 @ 10 loop
idle_front_facing: 16-20 @ 5 loop
idle_back_facing: 24-28 @ 5 loop
```

### Guest — the `GUEST_ANIMATION_ASSETS` layers, 16 x 23

Every animation starts on a row boundary, so the row index is the clip.

```
walk_forward: 0-7 @ 10 loop
walk_backward: 16-23 @ 10 loop
idle_front_facing: 32-37 @ 10 loop
idle_back_facing: 48-53 @ 10 loop
intrigue_front: 64-66 @ 10 once
intrigue_back: 80-82 @ 10 once
intrigue_blink_front: 96-98 @ 10 once
sit_down_front: 112-116,115-113 @ 10 loop
sit_down_back: 128-132,131-129 @ 10 loop
stand_up_front: 144-150 @ 10 once
stand_up_back: 160-166 @ 10 once
use_front: 176-181 @ 10 once
use_back: 192-197 @ 10 once
consume_front: 208-215 @ 10 loop
consume_back: 224-231 @ 10 loop
sus_behaviour_front: 240-254 @ 10 loop
sus_behaviour_back: 256-270 @ 10 loop
dissapoint_front: 272-287 @ 10 loop
dissapoint_back: 288-303 @ 10 loop
disgusted_front: 304-315 @ 10 loop
disgusted_back: 320-331 @ 10 loop
excited_front: 336-347 @ 10 loop
excited_back: 352-363 @ 10 loop
```

Ranges, rates and loop flags are all read from the Godot clips, not guessed: every guest clip runs
at 10 fps (0.1s per key), and `once` marks the ones Godot had on `loop_mode = 0`.

The `sit_down` clips were `loop_mode = 2` (ping-pong), which Unity clips have no flag for — the
descending second range replays the middle frames backwards, which is the same motion. Main
character idles are the exception on rate: 5 fps, matching their 0.2s keys.

## Editor preview

Both components are `[ExecuteAlways]`, so scrubbing the Animation window timeline shows the real
sub-image in the Scene view. `AnimationPreviewFrameTicker` pushes the value through on each
editor tick during preview, which covers the case where the Animator writes a frame without a
`LateUpdate` landing after it.

## Notes

- Frames are sliced on demand and **shared** through `SheetFrameCache`, keyed by
  (texture, grid, pivot, PPU). An `8 x 4` sheet costs 32 `Sprite` objects no matter how many
  guests point at it. No per-frame allocation: `Apply()` early-outs unless the index changed, so
  a held pose costs one int compare.
- Sprites are built with `SpriteMeshType.FullRect`. A tight mesh would give each cell a different
  vertex count from alpha analysis, which reads as sub-pixel jitter on pixel art.
- Slicing reads the source texture directly, so these sprites do not batch with atlased sprites.
  If crowd draw calls become a problem, that is the thing to measure first.
## Sheet import settings

**No slicing needed.** The grid comes from pixel dimensions, so Sprite Editor slices are ignored
— Sprite Mode can stay `Single`. What does matter:

| Setting | Value | Why |
| --- | --- | --- |
| Filter Mode | `Point (no filter)` | Bilinear bleeds neighbouring cells across the seam |
| Compression | `None` | BC/DXT artifacts wreck pixel art, and block compression wants dimensions divisible by 4 (the guest sheets are 1035 tall) |
| Texture Type | `Sprite (2D and UI)` | Forces NPOT scaling off. As `Default`, a non-power-of-two sheet can be resized to POT, which destroys the grid maths |
| Max Size | above the sheet's real size | A downscale changes width/height, and the cell division can then floor and drift |
| Generate Mip Maps | off | Sampling a lower mip pulls in adjacent cells |
| Read/Write Enabled | off | Slicing needs no CPU access; leaving it on doubles memory |

Pixels Per Unit is read from the **component**, not the importer, so the importer's value only
matters for other sprites made from the same texture. Keep the component values consistent between
characters or they render at different scales — the main character sheet is 22x45 px cells and the
guest sheets are 23x45, so both want the same PPU (32).
- Out-of-range frames **clamp** rather than wrap — a bad key shows as a stuck pose instead of
  quietly rendering a different valid frame.
- Frame offsets on the group layer and on the renderer are additive; normally only one is needed.
