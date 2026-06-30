# Narrative System (Dialogue · Cutscene · Tutorial)

Unity port of the Godot `DialogueSystem`, `TutorialSystem`, and `TutorialController`.
Everything talks through the static `MuseumActions` event hub (namespace
`ProjectMuseum.Narrative`). No HTTP — data lives in ScriptableObjects.

## The flow

```
MuseumActions.PlayStoryScene(n)
        │
        ▼
   DialogueManager  ── types each line, shows portrait + cutscene art
        │  (last line read)
        ├── HasTutorial ? ─► MuseumActions.PlayTutorial(TutorialNumber)
        │                          │
        │                          ▼
        │                    TutorialManager ── waits for keybinds (WASD/scroll)
        │                          │             and/or named actions
        │                          │  (all steps done)
        │                          └── ContinuesStory ? ─► PlayStoryScene(StoryNumber)
        │
        └── else ─► MuseumActions.StorySceneEnded(n)
```

## One-time setup

1. **Import the data.** Menu **Tools ▸ Project Museum ▸ Import Narrative JSON**.
   This reads `Assets/GameData/Source/*.json` and creates
   `Assets/GameData/StoryDatabase.asset` and `TutorialDatabase.asset`.
   After importing you can edit those assets directly and ignore the JSON.

2. **Art is already in Resources.** Portraits → `Assets/Resources/Portraits`,
   cutscene art → `Assets/Resources/Illustrations`. Loaded by name at runtime
   (`{Speaker} {Emotion}` and `{IllustrationName}`).

## Scene wiring

| Component | Put it on | Assign |
|---|---|---|
| `PlayerInfoProvider` | a persistent manager object | player name; **Tutorials Enabled** toggle |
| `DialogueManager` | **Dialogue Panel** | StoryDatabase · root · the box RectTransform · Text (TMP) · a Next **Button** · Portrait Image · Cutscene Image (inside *Cutscene Panel*) |
| `TutorialManager` | a manager object | TutorialDatabase |
| `TutorialPanelController` | **TutorialPanel** | root · panel RectTransform · Body Text (TMP) |
| `NarrativeDebugTrigger` *(optional)* | any object | auto-plays scene 1; F1/F2/F3 to fire scene/tutorial/action |

Notes
- The **Next Button** advances dialogue; a first press while text is still typing
  finishes the line instantly (same as Godot).
- Slide in/out uses DOTween on `anchoredPosition`. Each panel captures its authored
  position as "shown" and `shown + Hidden Offset` as "hidden" — tune the offset so
  it slides off-screen the way you want.

## Driving tutorials from other systems

Keybind steps (WASD, scroll) are tracked automatically. **Action** steps complete
when another system reports the action by name:

```csharp
using ProjectMuseum.Narrative;
// e.g. when the player right-clicks an exhibit:
MuseumActions.OnPlayerPerformedTutorialRequiringAction?.Invoke("SelectItem");
```

Action names expected by the current tutorial data:
`SelectItem`, `PlaceArtifactOnDisplaySlot`, `ClickedTownMap`, `FoundDiggingBuddy`,
`SleepAndSave`, `ClickedDiggingPermitsButton`, `SelectedMineSite`, `PickaxeSelected`,
`OnDigFirstOrdinaryCell`, `OnDigFirstArtifactCell`, `MiniGamesWon`, `ToggleGrab`,
`TalkToProfessor`, `ClickOnZoningButton`, `ClickOnNewZoneButton`, `CreateZone`,
`PlaceTwoMatchingArtifactsInZone`.

New keybind-based steps: add a case to `TutorialManager.IsKeyBindPerformed`.
```
