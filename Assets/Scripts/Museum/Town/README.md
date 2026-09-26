# Town Map

Port of the Godot town map (`Scenes/Museum/Town/new town.tscn` + `TownController` / `TownUi` /
`SpritePanAndZoom` / `TownBuilding`). As in Godot, the town is a separate little world shown
inside a UI panel.

## Setup (once, in the editor)
1. Open `Museum.unity`.
2. **Tools ▸ Project Museum ▸ Town ▸ Build Town (world + map UI)**
   → `Assets/Prefabs/Town/Town.prefab`, placed at (500, 500) far from the museum.
3. **Tools ▸ Project Museum ▸ Town ▸ Build Right Side Canvas**
   → `Assets/Prefabs/Town/Right Side Canvas.prefab`.
4. Save the scene.

Both are ordinary prefabs from then on, so edit them freely. **Re-running a Build command replaces
that prefab and its scene copy (it asks first).** After moving buildings, run
**Re-sort Town by Y** to fix the draw order.

## Town prefab
```
Town                     TownMapController: open/close + popup rules (inspector)
├ World                  "Town" layer, real SpriteRenderers
│ ├ Ground               base map; its red border is part of the art
│ ├ trailer houses/…     TownBuilding + PolygonCollider2D (Godot click outline, editable)
│ ├ DiggingBuddyIndicator
│ └ Town Camera          renders World → TownMap.renderTexture (enabled only while open)
└ Town Map Canvas        overlay, sort 50
  └ Map Panel            Backdrop, Map (RawImage + TownMapView), Header, Close Button, Popup
```
- **sortingOrder** = Godot z × 1000 + back-to-front rank.
- **TownBuilding** flags: `livingHouse` shows the empty-house popup, `hasDiggingBuddy` completes
  the tutorial step.
- **Popup text and timing** are in the Rules section of `TownMapController`.

## Controls
| | Mouse / keyboard | Gamepad |
|---|---|---|
| Open | Town Map button, **M** | View |
| Close | X button, **Esc**, **M** | **B**, View |
| Pan / zoom | drag / wheel | left stick / RT, LT |

## Data
`Assets/2D/Museum/Town/town_layout.json` is only read by the Build Town tool. Regenerate it
from Godot with `GodotImport~/parse_town.py <out.json>`.
