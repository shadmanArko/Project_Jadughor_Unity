# Sheet Row Maps

Which row is which animation, and how many frames each one actually uses.

Every figure here is cross-checked two ways: the frame ranges, rates and loop modes come from the
Godot `.tscn` clips, and the frame **counts** are measured independently from the PNG alpha
(counting cells that contain pixels). Where the two disagree it is called out rather than averaged.

Frame index is row-major: `frame = row * columns + column`, frame 0 top-left — same as Godot's
`Sprite2D.frame`. Rows are padded on the right, so a 3-frame animation in a 16-column sheet
occupies frames `r*16` to `r*16+2` and leaves 13 cells empty. Those gaps are why the frame numbers
jump: `intrigue_front` ends at 66 and `intrigue_back` starts at 80.

## Guest — `GUEST_ANIMATION_ASSETS/*`, 16 x 23

All 23 layer sheets are exactly `368x1035` → 23x45 px cells, so every variant is interchangeable.
Every clip runs at **10 fps**. Sheet occupancy matches the Godot clip length on all 23 rows.

| Row | Animation | Frames | Count | Loop |
| --- | --- | --- | --- | --- |
| 0 | `walk_forward` | 0–7 | 8 | loop |
| 1 | `walk_backward` | 16–23 | 8 | loop |
| 2 | `idle_front_facing` | 32–37 | 6 | loop |
| 3 | `idle_back_facing` | 48–53 | 6 | loop |
| 4 | `intrigue_front` | 64–66 | 3 | once |
| 5 | `intrigue_back` | 80–82 | 3 | once |
| 6 | `intrigue_blink_front` | 96–98 | 3 | once |
| 7 | `sit_down_front` | 112–116 | 5 | ping-pong |
| 8 | `sit_down_back` | 128–132 | 5 | ping-pong |
| 9 | `stand_up_front` | 144–150 | 7 | once |
| 10 | `stand_up_back` | 160–166 | 7 | once |
| 11 | `use_front` | 176–181 | 6 | once |
| 12 | `use_back` | 192–197 | 6 | once |
| 13 | `consume_front` | 208–215 | 8 | loop |
| 14 | `consume_back` | 224–231 | 8 | loop |
| 15 | `sus_behaviour_front` | 240–254 | 15 | loop |
| 16 | `sus_behaviour_back` | 256–270 | 15 | loop |
| 17 | `dissapoint_front` | 272–287 | 16 | loop |
| 18 | `dissapoint_back` | 288–303 | 16 | loop |
| 19 | `disgusted_front` | 304–315 | 12 | loop |
| 20 | `disgusted_back` | 320–331 | 12 | loop |
| 21 | `excited_front` | 336–347 | 12 | loop |
| 22 | `excited_back` | 352–363 | 12 | loop |

Only rows 17 and 18 fill all 16 columns. Frames 364–367 (the tail of row 22) are empty.

## MainCharacter — `Male Main Character (2).png`, 8 x 4

`176x180` → 22x45 px cells. Also used by `Player_Uncontrolled`.

| Row | Animation | Frames | Count | Rate | Loop |
| --- | --- | --- | --- | --- | --- |
| 0 | `walk_forward` | 0–7 | 8 | 10 fps | loop |
| 1 | `walk_backward` | 8–15 | 8 | 10 fps | loop |
| 2 | `idle_front_facing` | 16–20 | 5 | 5 fps | loop |
| 3 | `idle_back_facing` | 24–28 | 5 | 5 fps | loop |

## Alex (Digging Buddy) — `Digging_Buddy_Animation.png`, 8 x 4

`176x180` → 22x45 px cells. Identical layout to MainCharacter; same clip list works.

| Row | Animation | Frames | Count | Rate | Loop |
| --- | --- | --- | --- | --- | --- |
| 0 | `walk_forward` | 0–7 | 8 | 10 fps | loop |
| 1 | `walk_backward` | 8–15 | 8 | 10 fps | loop |
| 2 | `idle_front_facing` | 16–20 | 5 | 5 fps | loop |
| 3 | `idle_back_facing` | 24–28 | 5 | 5 fps | loop |

## Professor — `Professor_Walk_Idle.png`, 8 x 4

`176x180` → 22x45 px cells.

| Row | Animation | Frames | Count | Rate | Loop |
| --- | --- | --- | --- | --- | --- |
| 0 | `walk_forward` | 0–7 | 8 | 10 fps | loop |
| 1 | `walk_backward` | 8–15 | 8 | 10 fps | loop |
| 2 | `idle_front_facing` | 16–20 | 5 of **6** | 5 fps | loop |
| 3 | `idle_back_facing` | 24–28 | 5 of **6** | 5 fps | loop |

⚠ Both idle rows have **6** drawn frames but the Godot clip only played 5 — frames 21 and 29 were
never shown. Probably an oversight when the clips were copied from the main character (whose idles
genuinely are 5 frames). Use `16-21` / `24-29` if the extra frame belongs in the cycle.

## Emily (Ticket Counter) — `Ticket Counter Girlll.png`, 10 x 5

`220x225` → 22x45 px cells. **Note the 10 x 5 grid** — this is the one sheet that isn't 8 x 4, and
assuming 8 x 4 gives 27.5 px cells and a garbage grid. The frame numbers therefore step by 10.

| Row | Animation | Frames | Count | Rate | Loop |
| --- | --- | --- | --- | --- | --- |
| 0 | `walk_forward` | 0–7 | 8 | 10 fps | loop |
| 1 | `walk_backward` | 10–17 | 8 | 10 fps | loop |
| 2 | `idle_front_facing` | 20–24 | 5 | 5 fps | loop |
| 3 | `idle_back_facing` | 30–34 | 5 | 5 fps | loop |
| 4 | *unnamed* | 40–49 | 10 | ? | ? |

⚠ Row 4 has 10 drawn frames and **no clip in the Godot scene** — an animation that was drawn but
never hooked up. Worth a look before porting; it may be the counter/ticket-handling action.

## Paste-ready clip lists

For `Tools ▸ Project Museum ▸ Frame Animation Clip Builder`. The guest list is in
[README.md](README.md); these are the rest.

### MainCharacter / Alex (8 x 4)

```
walk_forward: 0-7 @ 10 loop
walk_backward: 8-15 @ 10 loop
idle_front_facing: 16-20 @ 5 loop
idle_back_facing: 24-28 @ 5 loop
```

### Professor (8 x 4) — includes the unused sixth idle frame

```
walk_forward: 0-7 @ 10 loop
walk_backward: 8-15 @ 10 loop
idle_front_facing: 16-21 @ 5 loop
idle_back_facing: 24-29 @ 5 loop
```

### Emily (10 x 5)

```
walk_forward: 0-7 @ 10 loop
walk_backward: 10-17 @ 10 loop
idle_front_facing: 20-24 @ 5 loop
idle_back_facing: 30-34 @ 5 loop
row_4_unnamed: 40-49 @ 10 loop
```
