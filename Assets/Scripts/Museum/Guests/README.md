# Guests

Port of the Godot `GuestsController` / `Guest.cs`. Guests spawn at the road ends, walk the
sidewalks, sometimes (5%) decide to visit when they pass the door, tour random exhibits, then
leave and walk off at another road end.

| File | Role |
| --- | --- |
| `GuestController.cs` | Spawning, pooling, appearance pools, walk graph, spawn/exit points, door, exhibit viewing spots, population counts + events |
| `GuestAgent.cs` | Per-guest state machine, tile-by-tile movement, animation, sorting (added to clones automatically) |
| `GuestData.cs` | Rolled per-guest data + `GuestState` |
| `GuestPathfinder.cs` | 4-way A* over cells (binary heap, reused buffers, no allocations per search) |
| `Editor/GuestControllerEditor.cs` | Auto-fill button, draggable cell handles, live stats |

## Setup (once)

1. In `Museum.unity`, create an empty GameObject **Guest Controller** and add **Project Museum ▸ Guest Controller**.
   It has to sit in the scene under the SceneContext so `MuseumDataModel` is injected.
2. The **Guest Template** can stay empty. The existing scene object `Guest` is used, hidden on Play,
   and cloned. To make it a prefab, drag it into `Assets/Prefabs` and assign the prefab here.
3. Adding the component auto-fills **Appearance Pools** from `GUEST_ANIMATION_ASSETS`. If that
   didn't happen, press **Auto-fill From Guest Sheets**. Each pool maps a part folder to a layer
   of the guest's `SheetSpriteGroup`, and each guest picks one sheet per pool. Set **Hidden
   Chance** on a pool (e.g. OVER_CLOTH 0.5) so only some guests wear that layer.
4. Select the controller and check the Scene view. Use the World/Cell overlay to read cells.
   Drag an area's dot to move it; set width/height in the lists to resize it.
   - **Red zones** are where guests spawn *and* disappear. Each zone is a row of cells across a
     road end. A guest spawns on a random cell of a zone and vanishes on the first cell of its
     exit zone it reaches. Make each zone span the full width of its sidewalk or crosswalk end.
   - **Blue** areas are **Sidewalks**. Wander stops and prewarmed guests only use these.
   - **White** areas are **Crosswalks**. Guests step on or off anywhere a crosswalk touches a
     sidewalk, and never pause on one.
   - **Yellow / green** mark the door: outside on the sidewalk, inside on the museum floor. They
     must be orthogonal neighbours. Defaults are `(5,-1)` / `(5,0)`.
   - Museum cells are excluded from walk areas automatically, and the door pair is the only link.
     For shapes rectangles can't cover, paint an **Outside Walk Tilemap** (renderer disabled).
5. Press Play. Startup errors or warnings in the Console name the specific misconfiguration:
   a door cell that isn't floor or sidewalk, a missing animator state, or a missing sorting layer.

## Behaviour

```
spawn in a zone ─► WalkingOutside ─(within Approach Radius of door, Enter Chance, once)─► HeadingToMuseum
     │ optional wander stops (PausingOutside)                                                 │ through door
     ▼                                                                                        ▼
vanish on exit zone ◄─ HeadingToExit ◄─ LeavingMuseum ◄─ WalkingToExhibit ⇄ ViewingExhibit
```

- **Exit choice:** a random zone at least **Min Travel Distance** cells away, or the farthest if
  none qualify, never the zone the guest came in by. Guests aim at a random cell of that zone,
  so they cross the road at different lanes, then vanish on the first zone cell they step on.
- **Visit:** the guest wants *Exhibits To Visit* exhibits, capped by how many exist. It shuffles
  them and walks to a free spot facing each exhibit. The two camera-facing sides (low X / low Y)
  are preferred. It watches for *View Time*, sometimes playing a reaction (`intrigue_*`,
  `excited_*`), then leaves. An empty museum gets a short look-around instead.
- **Reservations:** viewing spots are reserved, so two guests don't stack on one cell unless
  every spot is taken.
- **Dynamic museum:** guests re-path if an exhibit is placed on their route. They skip exhibits
  deleted mid-visit and use newly expanded floor right away. A guest that can't reach any exit
  is removed with a warning. In Godot, guests froze forever in that case.
- **Capacity:** **Max Guests Inside** counts guests inside plus those walking to the door.
  **Museum Open = false** stops new visitors.

## Sorting

- **Outside:** sorting layer `Guest`, which is last, so guests draw over the street, forest and
  front walls. Guests Y-sort among themselves.
- **Inside:** layer `PlacedMuseumObject` + `MuseumSortingSystem`, exactly like the player. Each
  body layer gets a `MuseumSortOffset` of 0–7, so the 8 parts stay stacked within the guest's
  sort band.
- **Crossing:** the swap happens halfway across the door step.
- **Batching:** guests use `MuseumSortingSystem.UpdateObjectFootprintDeferred`, which resorts
  once per frame instead of once per guest step.

## Code hooks

```csharp
controller.GuestEnteredMuseum += g => model.AddMoney(ticketPrice); // tickets — not wired yet
controller.GuestsInMuseum;   // for the top-bar Guest counter
controller.MuseumOpen = false;
```

## Not ported (yet)

- Godot's needs (hunger, thirst, bladder), shop buying and washrooms. They depend on the shop
  and sanitation systems, and `GuestData` is where those fields go.
- Interest tags. Godot rolled them but never used them.
- Guest click-selection UI.
