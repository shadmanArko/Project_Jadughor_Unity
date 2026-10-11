# Map Selection (Digging & Permits)

Port of Godot `DiggingAndPermitsMenu` + `SceneChanger`.

**Setup:** in `Museum.unity`, run **Tools ▸ Project Museum ▸ HUD ▸ Build Map Selection Panel**,
then save the scene. The command:
- builds `Assets/Prefabs/HUD/Map Selection Canvas.prefab` (editable; re-running replaces it, after asking),
- creates `Assets/GameData/MapSelectionData.asset` if it is missing (it never overwrites it),
- adds `Museum.unity` and `Mine.unity` to Build Settings,
- turns on the Digging & Permits button in `Right Side Canvas.prefab`.

## Flow
1. **Digging button:** raises the tutorial action "ClickedDiggingPermitsButton" and `OnClickDiggingPermitsButton`, which opens the panel.
2. **Pick a region:** `<` / `>` cycle the regions. The site list is cloned from the *Site Button Template*; restyle the template to restyle every site.
3. **Pick a site:**
   - raises the tutorial action "SelectedMineSite" and `OnPlayerSavedGame`,
   - saves the museum (the clock is part of the save),
   - writes the region and site into the mine's `MinePlayerScriptable` and `ArtifactGenerationConfig`,
   - shows *Loading...*, resets `Time.timeScale`, and loads the scene `Mine`.
4. **Close:** the X button, Esc, or gamepad B closes the panel.

## Data
Edit `MapSelectionData` to add regions or sites. Untick `available` to list a site that can't
be dug yet. It starts with Godot's single entry: NORTH AMERICA → Possible Battle Site
(America / BattleSite).

## Not ported yet
- Godot hid the Digging button until story entry "9d", and hid the site after story scene 10.
- Returning from the mine to the museum (the Godot mine used `SceneLoader`).
