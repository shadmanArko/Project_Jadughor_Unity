# Staff

Museum staff (namespace `ProjectMuseum.Staff`). There's a shared base for every role, plus the first role: the janitor.

| File | Role |
| --- | --- |
| `StaffAgent.cs` | **Base class.** Tile-by-tile A* movement, depth sorting, facing/animation, hunger + eating, home tile + idle fidget, task runner, dismissal |
| `JanitorAgent.cs` | Janitor decisions: sweep trash → sometimes clean an exhibit → go home |
| `StaffTask.cs` | `StaffTask` (walk → face → animate N s → callback) and `StaffTaskBoard` (job claims) |
| `StaffManager.cs` | Hire (cost, spawn at entrance), fire (walks out), daily wages. Optional |
| `TrashSystem.cs` | Shows litter (assign your sprites to **Trash Sprites**), guests drop litter, debug spawn (key **T**) |
| `StaffActions.cs` | Events: trash added/removed, staff hired/fired, exhibit cleaned. Also the `StaffRole` enum |

Trash is museum state: `MuseumData.Trash` (saved with the museum), `MuseumDataModel.AddTrash /
RemoveTrash / FindTrash / TryGetTrashAt`. Placing an object on a littered tile removes the litter.

## Setup (Museum.unity, under the SceneContext)

1. **Janitor**: add **Project Museum ▸ Staff ▸ Janitor** to the `Janitor` root object. On
   `Janitor/Body`, set the Animator's controller to `JanitorAnimatorController`.
   The tile he stands on at Play is his **home**. Untick *Home Is Start Position* to type one in
   instead. Select him to see the home tile (green) and break tile (orange) gizmos.
2. **Trash System**: add an empty GameObject with **TrashSystem** and assign your trash art to **Trash Sprites**. Until then litter is invisible, though janitors still sweep it. Guests now litter. Press **T**
   in Play mode to drop trash on a random tile.
3. *(optional)* **Staff Manager**: add an empty GameObject with **Project Museum ▸ Staff ▸ Staff
   Manager**. Set *Entrance Cell* to the GuestController's *Door Inside Cell*. Use the context menu
   **Hire Janitor (random home tile)** to test hiring. With no template assigned, it clones the
   scene janitor.

## Janitor behaviour

```
           ┌──────────── every Think Interval (1.5 s) while free, or instantly on new trash ─────────┐
           ▼                                                                                         │
 hungry (≥70)? ──yes──► walk to break tile / home ─► eat_front|back (6 s) ─► hunger = 0 ────────────┤
     │no                                                                                             │
 trash on floor? ─yes─► nearest unclaimed piece ─► sweeping_front|back (2.4 s) ─► trash removed ─────┤
     │no                                                                                             │
 exhibit roll? (cooldown 15–35 s, 40 %) ─yes─► random exhibit, a side tile ─► cleaning_glass (4 s) ──┤
     │no                                                                                             │
 walk home ─► idle_front|back (+ thinking fidget every 8–18 s) ──────────────────────────────────────┘
```

- **Hunger** rises 1.5 per 10 game minutes, so it pauses and speeds up with the museum clock.
- **Claims**: two janitors never chase the same trash or clean the same exhibit.
- **Robustness**: he re-paths if an exhibit is placed on his route, and drops a task if its
  target disappears. New litter can pull him away while he's walking home. After a save reload
  he snaps to the nearest floor tile.
- **Dirty exhibits**: random for now. Override `JanitorAgent.PickExhibitsToClean` (or give
  `ExhibitData` a dirt value and sort by it there) when exhibits get dirty for real.

## Adding a role

1. Add the role to `StaffRole`.
2. Subclass `StaffAgent` and implement `TryStartWork()`: build a `StaffTask` (stand cell, face
   cell, front/back states, duration, claim key, callbacks) and call `TryAssign(task)`.
3. Add a `RoleConfig` to the StaffManager.
4. Override `AnimatorStates()` so missing animator states are reported at startup.

## Tuning notes

All numbers are first-pass and need playtesting. The ones that matter most are litter chance
(TrashSystem, 4 % per guest per 10 game minutes) against sweep speed, and wages against ticket
income.
