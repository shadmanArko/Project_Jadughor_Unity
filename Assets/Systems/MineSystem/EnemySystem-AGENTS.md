# EnemySystem Engineering Guide

This file governs `Assets/Systems/MineSystem/EnemySystem`. It supplements the
master [`AGENTS.md`](AGENTS.md) one directory up — every mandatory rule there
(disposal, no `Update`/coroutines, Zenject lifecycle binding, one type per
file, etc.) applies here without exception. This document adds the patterns
specific to enemies, extracted from the three working reference
implementations: **GreenSlime** (grounded), **BlackBat** (flying), and
**RattleSnake** (crawling — grounded in practice today, see the movement-type
contract below).

`EnemyType` also declares `Skunk`, which has no implementation yet
(`Mob/Skunk/` is empty scaffolding). Use this guide to build it, or any new
enemy, the same way Slime, Bat, and Snake were built.

## Authority and interpretation

- The current user request is the highest authority; it may override this
  guide explicitly for a specific task.
- Existing code that deviates from this guide is legacy, not precedent.
  `IEnemySpawnRule` and `SlimeSpawnRule` are marked `[Obsolete]` — do not
  implement a new one; spawn validity now lives entirely in
  `EnemyConfigScriptable` fields, read by `EnemySpawnLocator`.
- Do not refactor Slime or Bat internals to "fix" an inconsistency between
  them unless the task asks for it. Where they intentionally diverge (see
  "Known intentional divergences" below), that divergence is a design
  choice per species, not a bug.

## How the shared system fits together

```text
EnemySystem/
|-- Config/            EnemyConfigScriptable (abstract base), EnemyWaveConfig
|-- Controller/         EnemyManager (the only IFixedTickable driver)
|-- Enum/               EnemyType, EnemyMovementType, PlaceableCollisionBehavior, ...
|-- Interface/          IEnemyController, IEnemyFactory, IEnemyPathfindingService, ...
|-- Model/              Structs/records shared across all enemies
|-- Service/            EnemySpawnService, EnemySpawnLocator, EnemyPathfindingService, ...
|-- Signal/             EnemyDiedSignal, EnemyDespawnedSignal, wave request/resolved
|-- Animation/          EnemyAnimationController (MonoBehaviour) + profile Scriptable
|-- Installer/          EnemyInstaller (Zenject bindings for every enemy type)
`-- Mob/<Name>/         Per-enemy Config/Controller/Enum/Model/Service/View/...
```

**Spawn flow:** `EnemyManager.SpawnAsync` -> `EnemySpawnService.Spawn` looks
up the `IEnemyFactory` for `request.Config.EnemyType` via
`EnemyFactoryRegistry`, then asks `EnemySpawnLocator` to find/validate a
`GridPosition` (occupancy, broken-cell state, distance from player,
visibility rule, solid ground below, path validation, placement/collider
fit — all driven by fields on `EnemyConfigScriptable`). The factory then
`Acquire`s a pooled instance and calls `Initialize`. `EnemyManager` tracks
the resulting `IEnemyController` in `_activeEnemies` and calls
`OnFixedTick` on all of them from its own single `FixedTick()`.

**Despawn/death:** enemies fire `EnemyDiedSignal` or `EnemyDespawnedSignal`
on the global event bus when their death/despawn animation completes.
`EnemyManager` subscribes to both and removes+releases the enemy back to
its pool. Never remove an enemy from `_activeEnemies` any other way.

**Relocation (the leash):** enemies are otherwise never culled — without
this they tick full AI for the whole mine session no matter where the
player is. `EnemyRelocationService` accumulates, per enemy, how long the
player has stayed beyond `RelocationDistanceInTiles`; once that exceeds
`RelocationDelaySeconds`, `EnemyManager.RelocateAsync` despawns the enemy
through the normal `DespawnAsync` path and spawns a replacement near the
player with `EnemySpawnVisibilityRule.OutsideCameraViewport`. A state
machine that has exhausted its own stuck recovery can request the same
treatment by firing `EnemyRelocationRequestedSignal`; `_relocatingEnemies`
guards against a leash tick and a stuck request racing on one enemy.
All of it is opt-in through the `[Header("Relocation")]` fields on
`EnemyConfigScriptable` and is currently enabled only on RattleSnake.
Relocation *is* a despawn + respawn — never bypass the signal path with a
direct pool release, and note the enemy's `MaximumSpawnDistanceInTiles`
must be set (and closer than the relocation distance) or the "respawn near
the player" lands anywhere in the mine and immediately relocates again.
`EnemyConfigScriptable.Validate()` enforces both.

**Wave spawning:** `EnemyWaveService` evaluates `EnemyWaveConfig` entries
against elapsed mine time and broken-cell count, fires
`EnemyWaveSpawnRequestedSignal`, and `EnemyManager` resolves it and fires
`EnemyWaveSpawnResolvedSignal` back. For a bespoke spawn trigger that isn't
time/wall-break driven (see `BatCaveSpawnController` reacting to
`CaveRevealedSignal`/`MineGeneratedSignal`), write a small
`IInitializable, IDisposable` controller that calls
`EnemyManager.SpawnAsync` directly instead of going through the wave
system.

**Shared per-tick services every enemy consumes:**

- `IEnemyTargetProvider` — is the player alive/spawned/climbing, its grid
  and world position. Combat logic must check `IsCombatTargetAvailable`
  (excludes climbing), general logic `IsTargetAvailable`.
- `IEnemyPathfindingService` — maintains a `WalkableCells`/`OpenCells`
  snapshot rebuilt on `MineData` changes and cell modifications, exposes
  `IsWalkable`/`IsFlyable`, `TryFind*Near`/`TryFindAny*`, `TryFindFallLanding`,
  an A* `FindPathToAnyAsync` against multiple candidate destinations, and
  `NavigationChanged` (`IObservable<GridPosition>`) + `NavigationRevision`
  so enemies can react to terrain changes instead of polling. Placeables
  implementing `IEnemyNavigationBlocker` (e.g. stalactites/stalagmites)
  mark cells impassable without changing terrain state.
- `IEnemyPlacementValidator` — grid<->world conversion and "does this
  enemy's collider actually fit here" checks. Always validate placement
  before committing a teleport or trusting a movement destination.
- `IEnemyChaseTargetResolver` — given a target grid cell and attack range,
  builds the ring of placement-valid candidate cells around it and asks
  `IEnemyPathfindingService` for the best reachable one. Both grounded and
  flying chase logic route through this rather than pathing straight to
  the target's cell (which may not be enterable).
- `IEnemyAttackService` — the only way to damage the player; wraps
  `IPlayerDamageService` + `IEnemyStatusEffectApplier` behind one
  `TryAttack(damage, statusEffect)` call.

## Per-enemy composition (the pattern to copy)

Every enemy under `Mob/<Name>/` follows the same eight-piece shape. Use
`SlimeX`/`BatX` as the literal template — file-for-file — when adding a new
enemy.

| Piece | Role |
|---|---|
| `<Name>ConfigScriptable : EnemyConfigScriptable` | Tunables + `Validate()`. One `[CreateAssetMenu]` asset per variant family. |
| `<Name>Model : IDisposable` | Pure data + mutation methods. No `UnityEngine` object references beyond simple structs (`Vector2`, `LayerMask`). `Dispose()` just calls `ResetRuntime()`. |
| `<Name>View : MonoBehaviour, IDamageable` | Owns `Rigidbody2D`, a non-trigger terrain `Collider2D`, a trigger hurtbox `Collider2D`, `SpriteRenderer`, and an `EnemyAnimationController`. Exposes movement primitives and `IObservable` streams (`DamageRequested`, collision/contact, animation markers/completed). `ValidateReferences()` must fail loudly if wiring is incomplete. |
| `<Name>StateMachine : IDisposable` | **All AI logic lives here.** Owns nothing Unity-specific directly; receives `Model`, `View`, and the shared services via constructor. Exposes `Initialize`, `SpawnAsync`, `DespawnAsync`, `OnFixedTick`, `HandleNavigationChanged`, `HandleAnimationMarker`, `HandleAnimationCompleted`, `EnterHurt`, `Pause`/`Resume`, `Release`, `Dispose`. |
| `<Name>Controller : IEnemyController` | Thin facade. Wires `View` observables to the `StateMachine` in `Initialize`, snapshots/restores physics+animator+damage state in `OnPause`/`OnUnpause`, forwards everything else to the `StateMachine`. This is the only piece Zenject/`EnemyFactory` touches directly. |
| `<Name>PoolEntry` | Immutable `(Prefab, View, Controller)` tuple. |
| `<Name>Pool : IInitializable, IDisposable` | Per-prefab `Stack<PoolEntry>`, pre-warms `Config.InitialPoolSize` instances under a dedicated root `Transform`, disposes every instantiated controller/view on teardown. |
| `<Name>Factory : IEnemyFactory` | `Create` = `pool.Acquire` + `controller.Initialize` in a try/catch that releases back to the pool on failure. `Release` forwards to the pool. |

Supporting single-purpose files: `<Name>AnimationId` (static class of
`"Name.State"` string constants matching the animation profile),
`<Name>PauseStateData` (plain snapshot holder: velocity, angular velocity,
animator speed, damage-enabled flag), and enums for `State`,
a movement-mode/path-purpose enum, and `Variant`.

## Contracts a new enemy must satisfy

1. Add a value to `EnemySystem/Enum/EnemyType.cs`.
2. Decide `EnemyMovementType` (`Grounded`, `Flying`, or `Crawling`) — this
   determines which pathfinding queries the state machine must use
   (`IsWalkable`/`TryFindWalkableNear` vs `IsFlyable`/`TryFindFlyableNear`,
   and `EnemyMovementType` passed into `EnemyMultiTargetPathRequest`). Every
   real branch on this enum (`EnemyChaseTargetResolver.IsCandidateValid`,
   `EnemySpawnLocator.IsNavigationValid`, `EnemyPathfindingService.FindPath`'s
   cell-set selection, and `EnemyPathfindingService.AddNeighbours`) is a
   ternary keyed on `== Flying`, so **anything that isn't `Flying` gets
   identical walk/fall behavior to `Grounded`** — this is why `Crawling`
   (RattleSnake) needed zero pathfinding changes to behave exactly like a
   grounded enemy. `Crawling` exists as its own value only so a *future*
   vertical-crawling variant can be special-cased later without touching
   `Grounded`/Slime. That future variant would need: a `ClimbableCells`
   snapshot set built in `EnemyPathfindingService.Rebuild` alongside
   `WalkableCells`/`OpenCells`, an `IsClimbable`/`TryFindClimbableNear` pair
   mirroring the existing Walkable/Flyable API, and a vertical-stepping
   branch in `AddNeighbours` gated on `movementType == Crawling`. None of
   this exists yet — do not add it speculatively before an enemy actually
   needs vertical movement.
3. Subclass `EnemyConfigScriptable`, override `VariantId`, and override
   `Validate()` — call `base.Validate()` first, then add every
   enemy-specific cross-field check (ranges that must exceed other ranges,
   required animation ids present, non-negative durations, etc.). Group
   serialized fields under `[Header]`s the same way Slime/Bat do
   (Identity/Core Stats/Combat Ranges/Spawn Rules come from the base;
   add your own for detection, movement, attack, status effect, pooling).
4. Implement the eight pieces above.
5. Author the prefab: `Rigidbody2D`, non-trigger terrain `Collider2D`,
   trigger hurtbox `Collider2D`, `SpriteRenderer`, `EnemyAnimationController`
   — exactly what `View.ValidateReferences()` checks for. Author an
   `EnemyAnimationProfileScriptable` with one `EnemyAnimationData` entry per
   string id the state machine plays, wired to an `Animator` whose
   `AnimationEvent_AdvanceFrame`/`AnimationEvent_Marker`/
   `AnimationEvent_Complete` calls exist on every clip that needs them.
6. Register in `EnemyInstaller.InstallBindings()`:
   - `Container.Bind<FooConfigScriptable>().FromScriptableObject(fooConfig).AsSingle()`
     with a null-check `throw` (matches the existing pattern for every
     other config).
   - `Container.BindInterfacesAndSelfTo<FooPool>().AsSingle()`.
   - `Container.Bind<IEnemyFactory>().To<FooFactory>().AsSingle()`
     (`EnemyFactoryRegistry` throws at construction if two factories claim
     the same `EnemyType` — keep exactly one factory per type).
7. Add `EnemyWaveSpawnData` entries to the shared `EnemyWaveConfig` asset
   if the enemy should spawn via time/wall-break waves, or write a
   dedicated `IInitializable`/`IDisposable` controller (mirroring
   `BatCaveSpawnController`) for a bespoke spawn trigger.

## Behavioral conventions shared by Slime, Bat, and Snake

- **Pause.** The state machine owns a `PauseGate`
  (`Systems.MineSystem.PauseSystem.Service.PauseGate`); `OnFixedTick`,
  `HandleNavigationChanged`, etc. bail out early when `_pauseGate.IsPaused`.
  The `Controller.OnPause` snapshots `Rigidbody2D.simulated`/velocity/
  angular velocity, animator speed, and damage-enabled state into
  `<Name>PauseStateData`, zeroes velocity, sets `body.simulated = false`,
  and calls `_stateMachine.Pause()`. `OnUnpause` restores the snapshot and
  calls `Resume()`. Copy this exactly — do not invent a different pause
  mechanism per enemy.
- **Damage.** `View.ApplyDamage` only forwards to `DamageRequested` when
  `_damageEnabled` is true (set false during spawn/death animations so an
  enemy can't be hurt before it's "alive" or after it's already dying).
  `Controller` subscribes `DamageRequested` -> `Model.ApplyDamage` ->
  tells the state machine to react (`EnterHurt`/death). Slime resolves
  death immediately when health hits zero; Bat sets `PendingDeath` on the
  model and only transitions to `Death` after the current `Hurt` animation
  finishes — pick whichever fits the new enemy's animation set, but always
  gate damage acceptance through `SetDamageEnabled`.
- **Animation-driven transitions.** `ChangeState` sets the model's state
  enum, resolves an animation id, plays it via
  `config.AnimationProfile.TryGet`, and stores an `_animationGeneration`
  counter. `HandleAnimationCompleted`/`HandleAnimationMarker` ignore events
  whose `Generation` doesn't match the current one (stale events from a
  just-superseded animation). Every state that must eventually leave on
  its own (spawn, aggro, attack, hurt, teleport, despawn, death) does so
  from `HandleAnimationCompleted`, not from a timer. Always implement a
  `HandleMissingAnimation` fallback so an incomplete animation profile
  can't stall the enemy forever — treat hitting that fallback as a content
  bug to fix, not a normal path.
- **Engagement thresholds are distinct and configurable.** Aggro range
  (start engaging) < chase-exit range (give up chasing) — validate this
  ordering in `Validate()`. Attack validity checks *both* a world-space
  contact distance (`IsWithinWorldDistance`) *and* a grid-range check
  (`GridDistance <= AttackRangeInTiles`); both must pass. Re-evaluate
  decisions only when something relevant actually changed (target grid
  cell changed, combat availability changed, navigation changed) — do not
  re-decide every fixed tick, or you'll thrash pathing.
- **Damage timing follows the animation marker, not the state entry.**
  Attacks apply damage in `HandleAnimationMarker` when
  `EnemyAnimationMarker.AttackImpact` fires, gated by an `_attackApplied`
  flag reset on `ChangeState`. Never apply attack damage the instant the
  `Attack` state is entered.
- **Movement failure always has an escape hatch.** Every movement/path
  attempt carries a timeout (`StartMovementTimeout`/`TickMovementTimeout`)
  computed from distance and speed, plus a minimum floor. Grounded
  movement also tracks placement validity every tick
  (`IsCurrentPlacementClear`) and falls back to something safe if the enemy
  is no longer sitting in a valid cell (e.g. terrain changed under it) —
  Slime's fallback is `StartEmergencyTeleport`, RattleSnake's is a quiet
  reposition or passive idle-retry (see "Known intentional divergences"
  below for why these differ). Flying movement additionally tracks stall
  (`TickMovementStall` — no measurable progress for
  `MovementStallTimeoutSeconds`) and falls back to `Explore`. A new enemy
  must have an equivalent "I'm stuck, do something safe" path — never leave
  a state with no exit condition, and pick the fallback appropriate to the
  animations it actually has.
- **Reachability-failure caching.** `RecordReachabilityFailure`/
  `IsReachabilityFailureCurrent` remember "path to X failed under
  navigation revision N" so the enemy doesn't retry an unreachable target
  every tick; the cache is only trusted while
  `IEnemyPathfindingService.NavigationRevision` hasn't changed.
- **Path requests are always cancellable and generation-checked.** Path
  results carry the `PathGeneration` they were requested under; handlers
  discard results whose generation doesn't match the model's current one
  (a newer request superseded it) and always cancel the previous
  `CancellationTokenSource` before starting a new path search.

## Known intentional divergences (Grounded vs Flying vs Crawling)

These are deliberate per-species differences, not inconsistencies to fix.
RattleSnake (Crawling) deliberately mirrors Slime's grounded movement almost
exactly, which is itself useful evidence: comparing Slime and Snake shows
which parts of "how Slime behaves" are really tied to being grounded
(patrol corridor, fall recovery, physics-driven movement — copy these) versus
which parts are just Slime's own personality (Aggro telegraph, teleport —
these are per-species animation-availability choices, not part of the
grounded-movement pattern itself, so a new grounded enemy should not assume
it needs them).

- **Movement execution.** Slime drives a `Rigidbody2D` via
  `SetVelocity`/horizontal-only movement and relies on physics + ground
  probing (`IsGrounded` boxcast) each tick. Bat interpolates directly with
  `MovePosition` across a start/target segment (`BeginSegment`/
  `AdvanceSegment`), layering a sinusoidal wobble as a **visual-only**
  offset (`View.SetFlightVisualOffset`, applied by
  `EnemyAnimationController` to the sprite's local transform, never to the
  physics body). Keep wobble/purely-visual offsets out of the
  `Rigidbody2D` position — mixing them caused a prior collider-stuck bug.
- **Patrol vs. explore-and-perch.** Slime precomputes a bounded
  "patrol corridor" (a line of walkable cells around its spawn point) and
  walks back and forth along it, occasionally falling to a lower corridor
  or teleporting when stuck. Bat instead repeatedly requests a random
  reachable flyable destination within `ExploreRangeInTiles`
  (`StartExploreRoute`), and can probabilistically choose to fly to a
  ceiling "perch" (`BatNavigationService.TryFindPerch`) and play an
  idle-hover animation for a while before resuming exploration.
- **Aggro telegraph.** Slime plays a dedicated `Aggro` animation the first
  time it engages a target before it starts chasing (`aggroProbe` path in
  `RequestChaseRoute`/`EvaluateDecision`). Bat has no telegraph state — it
  transitions straight into `Chase`. RattleSnake also has no telegraph
  (no `Aggro` animation exists for it) and mirrors Bat's simpler
  straight-into-chase flow despite being grounded like Slime — proof this is
  purely a per-species animation choice, not something tied to
  `EnemyMovementType`. Add a telegraph only if the design calls for one; it
  is not a required part of the pattern.
- **Movement-failure escape hatches differ by available animation set, not
  by movement type.** Slime has a full two-state teleport animation
  (`TeleportDespawn`/`TeleportSpawn`) and uses `StartEmergencyTeleport()` as
  its last resort when stuck or encased. Bat has no ground/fall concept at
  all and falls back to `Explore`. RattleSnake has neither a teleport
  animation nor Bat's explore state, so its ladder is: a silent, unanimated
  reposition (`SnakeStateMachine.TryQuietReposition` — calls
  `SnakeView.Teleport(Vector2)` directly, used only from `Fall`'s give-up
  paths), then `EnemyRelocationRequestedSignal` once either the failure
  count or the *success* count passes `DestinationRetries`, then
  `DespawnWithoutAnimation()` when relocation is disabled. Both counters
  matter: an enemy that repositions successfully over and over is just as
  stuck as one whose repositions fail, and only the second counter catches
  it. `CompleteQuietReposition` must leave `Fall` (it calls
  `EnterFallRecoveryIdle`) — a reposition lands on a walkable cell, i.e.
  already grounded, so `TickFall`'s landing latch could never fire again and
  the snake would teleport on every timeout forever. Match the new enemy's
  actual animation set — don't copy Slime's teleport sequence onto an enemy
  that has no teleport animation.
- **Fall is entered only when actually airborne.** `EnterFall()` resyncs and
  routes back to normal AI (`ResolveGroundedFallEntry`) if the ground probe
  still hits, because the landing latch needs an ungrounded frame *before* a
  grounded one and can never be satisfied otherwise. This matters at a ledge
  lip, where `WorldToGrid` resolves to the open cell past the edge while the
  collider is still supported. A directed drop that stalls on the lip latches
  a commit direction (`_fallCommitting`) instead of steering by `deltaX` —
  steering reverses the instant it overshoots, which reads as edge jitter —
  and `TickGroundedFallGuard` abandons the fall on a short budget so a stalled
  drop cannot ride out the full `MaxFallDistanceInTiles` movement timeout
  (~7s at current tuning). The budget differs by case: an undirected grounded
  fall can never move at all, so it bails after
  `MinimumMovementTimeoutSeconds`; the commit push gets a collider-width
  distance budget, because clearing a lip means travelling roughly half a
  collider width past the edge before the ground probe stops hitting it, and a
  flat minimum is too short at low move speeds. Any new grounded enemy copying
  the fall pattern needs all three: the grounded-entry guard, the commit latch,
  and the bail-out.
- **Multi-instance coordination.** `BatFormationService` hands each bat a
  stable slot index used to offset explore-destination search order,
  wobble phase (so multiple bats don't move in visual lock-step), and
  contact-approach direction (so bats surround rather than stack on the
  player). Slimes have no equivalent — `EnemyManager.GetOccupiedPositions`
  only prevents two enemies from *spawning* on the same cell, it does not
  arbitrate runtime movement conflicts. If a new grounded enemy needs
  multi-instance coordination, use `BatFormationService` as the template
  rather than inventing a new mechanism.
- **Placeable contact.** `PlaceableCollisionBehavior`
  (`ContinueMovement` vs `StopAndAttack`, shared from `EnemySystem/Enum/`
  — promoted out of Slime's own folder once Snake needed the same concept)
  driven by `View.HorizontalCollision` lets a patrolling grounded enemy stop
  and attack a damageable placeable it walks into; both Slime and RattleSnake
  use it. Bats don't touch placeables this way since they fly around
  obstacles rather than into them; a new grounded enemy that should ignore
  placeables can omit this entirely, but if it should interact with them,
  copy Slime's/Snake's `HandleHorizontalCollision` pattern rather than
  adding trigger-based placeable detection.

## Debugging stuck states

`EnemyDiagnosticsLog` (`Service/EnemyDiagnosticsLog.cs`) traces state
transitions, grounded/latch flags, repositions and relocations. Every method
is `[Conditional("ENEMY_DIAGNOSTICS")]`, so both the calls and their argument
expressions are compiled out unless that symbol is in
*Project Settings > Player > Scripting Define Symbols* — add it while
reproducing an AI stall and remove it before shipping. Prefer extending this
over sprinkling raw `Debug.Log`, which would allocate on the FixedUpdate path
in real builds.

## Pre-delivery checklist (enemy-specific, in addition to the master AGENTS.md checklist)

- [ ] New `EnemyType` value added; no two `IEnemyFactory` implementations
      claim the same type.
- [ ] `<Name>ConfigScriptable.Validate()` checks every new field, including
      cross-field ordering (aggro < chase-exit, min <= max distances, etc.).
- [ ] Prefab has exactly the components `View.ValidateReferences()`
      requires; terrain collider is non-trigger, hurtbox is a trigger.
- [ ] Every animation id the state machine references exists in the
      assigned `EnemyAnimationProfileScriptable`, or the
      `HandleMissingAnimation` fallback path is deliberately acceptable.
- [ ] `Config.MovementType` matches the pathfinding calls actually used
      (`Grounded`/`Crawling` -> walkable queries, `Flying` -> flyable
      queries).
- [ ] If relocation is enabled, `MaximumSpawnDistanceInTiles` is non-zero and
      smaller than `RelocationDistanceInTiles`, and the enemy has despawn/spawn
      presentation good enough to be seen doing it.
- [ ] Every state reachable from `OnFixedTick` has an exit that cannot depend
      on a condition the state itself makes unreachable (the Fall landing-latch
      class of bug). Walk each state and name what ends it.
- [ ] Pool prewarms `InitialPoolSize`; `Release()` fully resets
      Model+View+PauseState+subscriptions; `Dispose()` releases every
      pool-held instance and destroys their GameObjects.
- [ ] If animation clips were authored as placeholder stubs (empty sprite
      lists, assumed frame counts), confirm each clip's
      `AnimationEvent_AdvanceFrame` count/timing was revisited once real
      frames were dropped in, and that any `AttackImpact` marker still
      lands on the intended frame.
- [ ] No `Update`/`FixedUpdate`/coroutines were added — all per-frame logic
      runs through `EnemyManager.FixedTick` -> `IEnemyController.OnFixedTick`.
- [ ] `Controller`, `StateMachine`, `Model`, and `Pool` all implement and
      correctly cascade `IDisposable`.
- [ ] Manually spawned the enemy (via wave config or a debug spawn call)
      and exercised spawn -> idle/explore -> aggro/chase -> attack -> hurt
      -> death/despawn, plus pause/unpause mid-action, and confirmed no
      stuck states or leaked subscriptions.
