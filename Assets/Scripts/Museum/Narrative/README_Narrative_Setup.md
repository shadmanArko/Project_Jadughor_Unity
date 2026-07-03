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
        │  (last line read → slide out → StorySceneEnded(n))
        ├── HasTutorial ? ─► MuseumActions.PlayTutorial(TutorialNumber)
        │                          │
        │                          ▼
        │                    TutorialManager ── waits for keybinds (WASD/scroll)
        │                          │             and/or named actions
        │                          │  (all steps done)
        │                          └── ContinuesStory ? ─► PlayStoryScene(StoryNumber)
        │
        ├── else ContinuesStory ? ─► PlayStoryScene(NextStoryNumber)   ← scene auto-chain
        │
        └── else ─► (stops; StorySceneEnded already fired)
```

### Auto-continuing the story

A scene with **no tutorial** plays the next scene automatically if you tick
**Continues Story** on that `StoryScene` (in the StoryDatabase asset). **Next Story
Number** picks the target; leave it `0` to default to `SceneNo + 1`. Scenes *with*
a tutorial chain through the tutorial's own `ContinuesStory`/`StoryNumber` instead.

`StorySceneEnded(n)` always fires when a scene's dialogue finishes — handy for the
"player left for another scene" case. To resume later, use **StoryController**:
- `PlayStory(int)` — play a specific scene.
- `ResumeStory()` — play `PlayerInfo.CompletedStoryScene + 1`.
- or tick **Play On Start** (+ optionally **Resume From Progress**) on the component.

> **Continues Story / Next Story Number** live in the source JSON (`StoryScene.json`)
> and are the source of truth — re-running the importer rebuilds the asset from them.
> Currently scenes **1, 2, 3** continue to the next; everything else stops or is
> driven by its tutorial. Edit the JSON (or the asset, then keep the JSON in sync) to
> change this.

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
| `DialogueManager` | **Dialogue Panel** | StoryDatabase · **Root** = Dialogue Panel · **Slide Root** = *Panel Bg* · Text (TMP) · a Next **Button** · Portrait Image · **Cutscene Panel Root** = *Cutscene Panel* · **Cutscene Art** = *Cutscene Image* |
| `TutorialManager` | a manager object | TutorialDatabase |
| `TutorialPanelController` | **TutorialPanel** | **Root** = TutorialPanel · **Panel** = *Panel Bg* · Body Text (TMP) |
| `StoryController` *(optional)* | a manager object | StoryDatabase; for manual start / resume (Play On Start, Resume From Progress) |
| `NarrativeDebugTrigger` *(optional)* | any object | auto-plays scene 1; F1 replay scene · F2 play tutorial · F3 step actions |

Notes
- **Slide Root / Panel** must be the **Panel Bg** (the container holding *both* the
  dialogue box + portrait, or header + body) so everything slides together — never
  assign just the inner box, or the portrait gets left behind.
- The separate **Cutscene Panel** is enabled/disabled automatically per line
  (`HasCutscene`); assign it to **Cutscene Panel Root**.
- The **Next Button** advances dialogue; a first press while text is still typing
  finishes the line instantly (same as Godot).
- Slide in/out uses DOTween on `anchoredPosition`. Each panel captures its authored
  position as "shown" and `shown + Hidden Offset` as "hidden" — tune the offset so
  it slides off-screen the way you want.
- **F3 walks through the current tutorial step's required actions one at a time** —
  press it once per required action; no need to type action names into any field.

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
