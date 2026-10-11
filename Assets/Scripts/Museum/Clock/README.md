# Museum Clock & Time UI

Port of Godot `MuseumTimeSystem` + `DateTimeUi` + gate sign (`Museum ui.tscn → Top Panel/Date time panel`).

**Setup:** in `Museum.unity`, run **Tools ▸ Project Museum ▸ HUD ▸ Build Time UI**, then save
the scene. This creates `Assets/Prefabs/HUD/Time Canvas.prefab`, which you can edit freely.
Re-running the command replaces it (it asks first).

## Rules (`MuseumTimeSystem`, on the canvas root)
- **Ticks:** 10 game minutes every 5 real seconds at 1x. Speeds are 1x, 2x and 4x.
- **Speed:** changes only the clock rate, as in Godot. `speedScalesGameplay` also speeds up the guests.
- **Pause:** stops the clock and, by default, sets `Time.timeScale = 0` so guests and the player freeze. UI, camera and dialogue keep running.
- **Calendar:** 60 min, 24 h, 30 days per month, 4 seasons per year. A new game starts at 09:50 on day 1, season 1, year 1.
- **Saving:** the clock is stored in the museum save (`MuseumData.Clock`).
- **Shortcuts:** Space pauses/plays; 1, 2, 3 set 1x, 2x, 4x.
- **Events (`MuseumActions`):** `OnTimeUpdated`, `OnTimePauseValueUpdated`, `OnTimeSpeedChanged`.
  The buttons raise `OnClickPausePlayButton` / `OnClickTimeSpeedButton`.

## View (`MuseumTimeUI`)
- **Shows:** HH:MM, day name, DD, a season icon and "Yr NN".
- **Highlights:** the active speed button and the pause button (while paused) are tinted #FDD053. Godot showed neither.

## Gate sign (`MuseumGateSign`)
- **Click:** flips OPEN/CLOSE with the 6-frame spin.
- **Event:** raises `OnClickMuseumGateToggle(bool)`. `GuestController` stops guests entering while it's closed.

## Changes from Godot
- **Day names:** day 1 is "Mo" (Godot showed "Tu" because of an off-by-one). The names are an editable array.
- **Season rollover:** consistent, after season 4.
- **Gate sign:** announces its starting state.
