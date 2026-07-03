# MineSystem Engineering Guide

This file defines the coding and delivery rules for everything under
`Assets/Systems/MineSystem`. It is written for both human contributors and
coding agents.

## Authority and interpretation

- The current user request is the highest authority. A task-specific instruction
  may override this guide explicitly.
- Rules under **Mandatory rules from project prompts** are direct, standing user
  instructions and must be followed.
- Rules under **Recurring project conventions** are inferred from repeated
  requests and existing architectural direction. Treat them as the default unless
  the current task establishes a better or more specific approach.
- Existing code that violates this guide is legacy code, not a precedent to copy.
- Do not refactor unrelated legacy code merely to make it comply. Bring new and
  directly touched code into compliance within the authorized task scope.

## Mandatory rules from project prompts

### Architecture

- Use Zenject, UniRx, UniTask, MVC, and SOLID appropriately. Do not introduce a
  pattern merely to claim compliance; each dependency and layer must have a clear
  responsibility.
- Keep systems decoupled. Communicate through focused interfaces, injected
  dependencies, reactive properties, or signals instead of reaching across
  unrelated systems.
- Keep controllers, models, views, and services within their proper roles. Do not
  move business logic into views or turn controllers into catch-all classes.
- Every class, interface, struct, and enum must have its own script. This includes
  private or nested helper types. The file name must match its single declared
  type.
- Place every script in the corresponding subsystem and architectural folder.
  Do not leave production scripts in a subsystem root or a temporary/test folder.
- Favor clear, focused scripts over coupled code and large multi-purpose classes.

### Controller and model disposal

- Every behavior-bearing `Controller` and every behavior-bearing `Model` must
  implement `System.IDisposable`.
- A model is exempt only when it is a genuine data holder: state plus simple
  constructors, properties, accessors, and value-oriented helpers, with no
  orchestration, subscriptions, event ownership, asynchronous work, or runtime
  lifecycle responsibility.
- A controller or non-data model remains required to implement `IDisposable` even
  when its current `Dispose()` is small. This keeps lifecycle ownership explicit
  and safe as the type evolves.
- `Dispose()` must release everything the object owns, including:
  - UniRx subscriptions, `CompositeDisposable`, `SerialDisposable`, and subjects;
  - C# and Unity event handlers;
  - cancellation token sources and pending UniTask operations;
  - timers, tweens, callbacks, registrations, and runtime references;
  - pooled or spawned resources owned by the object.
- Disposal must be idempotent where repeated cleanup is possible. Disposed
  callbacks must not mutate gameplay state later.
- Bind disposable controllers and models through Zenject interfaces, normally
  with `BindInterfacesAndSelfTo<T>()` or an equivalent binding that exposes
  `IDisposable`, so the owning container disposes them.
- Views and services that own subscriptions or runtime resources must also clean
  them up, even though the blanket `IDisposable` requirement specifically targets
  behavior-bearing controllers and models.

### Runtime flow and performance

- Do not use Unity `Update`, `FixedUpdate`, or `LateUpdate` methods.
- Do not use coroutines or `StartCoroutine`.
- Do not replace an update loop with disguised polling such as
  `Observable.EveryUpdate`, `Observable.EveryFixedUpdate`, or a perpetual UniTask
  loop.
- Prefer event-driven work, Input System callbacks, UniRx subscriptions, signals,
  reactive properties, tweens, and cancellable UniTask sequences.
- Use UniTask instead of coroutines for asynchronous sequences. Every operation
  that can outlive its owner must accept or derive a cancellation token and must
  stop safely during disposal, scene changes, despawning, or reuse.
- If true continuous ticking is unavoidable, use Zenject `ITickable` or
  `IFixedTickable` only in a controller-level script. Document why events,
  callbacks, physics events, or a tween cannot express the behavior.
- Make systems processor-conscious: avoid repeated scene searches, unnecessary
  allocations, repeated LINQ in hot paths, redundant state writes, and work for
  off-screen or inactive objects.
- Use `CompositeDisposable` or another explicit ownership mechanism whenever a
  type owns multiple subscriptions. Never leave zombie subscriptions, callbacks,
  or asynchronous work behind.

### Configuration and reactive state

- Put adjustable gameplay and presentation values in a focused config or
  ScriptableObject rather than scattering magic numbers through runtime code.
- Use reactive properties when a value must notify consumers or be adjustable at
  runtime. Do not make a value reactive when plain immutable configuration is
  sufficient.
- Validate configuration and serialized references at initialization. Fail with
  a useful error for required missing data; handle genuinely optional data
  deliberately.
- Preserve authored prefab and asset settings unless the task explicitly changes
  them. Do not silently overwrite designer-authored state at runtime.

### Input and interaction

- Define persistent Input Actions and action maps in the Input System asset. Do
  not construct ordinary gameplay or UI actions dynamically inside controllers.
- Enable only the action map appropriate to the current context. When a modal UI
  closes, restore the previously active gameplay map and state.
- Input handlers capture input and emit intent. They must not accumulate unrelated
  gameplay rules.
- Name input signals by intent, such as `MovementInputSignal`, and keep each signal
  in its own script. Signals should be lightweight structs containing only the
  data consumers need.
- Validate the target and current state before applying animations, restrictions,
  inventory mutations, movement modes, or other side effects. A failed interaction
  must be a no-op and must not leave partial state behind.
- Preserve controller and keyboard/mouse support for navigable UI and repeated
  input behavior when that system supports both.

### Pooling and item data

- Use object pooling for repeatedly spawned transient world objects when practical.
- A pooled object must reset all mutable state before reuse: data, sprites,
  transforms as applicable, velocity, gravity/body mode, flags, timers,
  subscriptions, callbacks, cancellation, and collection/activation state.
- Despawning must cancel delayed work so an old lifetime cannot affect a reused
  object.
- Runtime items must preserve their full identity and conversion data so they can
  be restored to their concrete form without data loss.
- Keep item data separate from presentation assets. Resolve authoritative icons or
  sprites through the appropriate profile, catalog, resolver, or ScriptableObject
  instead of embedding presentation state into runtime item data.
- When an object must become a world collectable, do not bypass the required world
  spawn, physics, attraction, and collection flow by inserting it directly into
  inventory.

## Canonical code and folder structure

Organize code by feature/subsystem first, then by responsibility. Use the existing
namespace root `Systems.MineSystem` and mirror the folder path in the namespace.

```text
<Feature>System/
|-- Config/
|-- Controller/
|-- Enum/
|-- Handler/
|-- Installer/
|-- Interface/
|-- Model/
|-- Prefab/
|-- Profile/
|-- Scriptable/
|-- Service/
|-- Signal/
`-- View/
```

Create only the folders a feature needs. A substantial nested feature may be a
named subsystem with the same internal layering; do not create arbitrary folders
that hide ownership.

### Layer responsibilities

- **Controller**: coordinates a use case or feature lifecycle. It receives intent,
  invokes models/services, and translates results for views. Behavior-bearing
  controllers implement `IInitializable` when needed and always implement
  `IDisposable`.
- **Model**: owns feature state, invariants, and domain operations. A model with
  behavior or lifecycle implements `IDisposable`; a data-only DTO/value model is
  exempt.
- **View**: owns Unity components, serialized references, rendering, animation,
  layout, and physics-facing presentation. It exposes focused operations/events
  and contains no feature orchestration.
- **Service**: performs a focused reusable operation, integration, resolver, or
  domain capability that does not naturally belong to one model or view. Avoid
  generic manager classes with unrelated responsibilities.
- **Interface**: holds focused contracts used to invert dependencies or support
  multiple implementations. Do not create an interface with only one consumer
  unless it establishes a meaningful boundary or lifecycle contract.
- **Config**: contains tunable values and validation, without runtime orchestration.
- **Scriptable**: contains authored/shared Unity data or intentionally shared
  reactive state. Keep editor data distinct from per-instance runtime state.
- **Installer**: contains Zenject bindings and construction policy only. It must not
  perform gameplay work.
- **Signal**: contains one immutable or lightweight event payload per file.
- **Handler**: implements one strategy for a known action or interaction contract.
- **Profile**: contains authored behavior selection and presentation metadata used
  by resolvers/handlers rather than hard-coded type switches.
- **Enum**: contains one enum per file and no unrelated types.
- **Prefab**: contains authored Unity prefabs and their supporting asset structure;
  required components should be authored on the prefab rather than added as a
  hidden runtime repair.

## Dependency and data-flow rules

- Prefer constructor injection for non-MonoBehaviour classes. Use serialized
  references for authored view/prefab dependencies and inject the resulting view.
- Register lifecycle types through their Zenject interfaces. Use `NonLazy()` only
  when the system must initialize without being requested by another dependency.
- Use direct method calls inside a cohesive feature when ownership is clear. Use
  interfaces or signals across subsystem boundaries and for one-to-many events.
- Avoid service locators, repeated `Find*` calls, hidden singletons, and new global
  mutable state. Existing global infrastructure may be used consistently, but it
  must not become a shortcut around ownership.
- Establish one source of truth for state. Views render it; controllers coordinate
  it; services operate on it. Do not maintain competing copies without an explicit
  synchronization contract.
- Prefer `Try...` APIs or explicit result types when failure is expected. Validate
  first, then commit mutations atomically enough that failure cannot strand the
  system halfway through a transition.

## Planning and implementation workflow

### 1. Ground the task in the project

- Read the relevant controllers, models, views, services, interfaces, configs,
  installers, prefabs, scenes, and ScriptableObjects before proposing changes.
- Trace registrations, subscriptions, input flow, data ownership, spawn/despawn
  paths, and serialized references. Do not infer architecture from one file.
- Inspect the working tree and preserve unrelated user changes. Never overwrite or
  revert work merely because it complicates the task.
- For bugs, identify and explain the root cause before selecting the fix.

### 2. Plan nontrivial changes

- For nontrivial, architectural, or risky work, produce a decision-complete plan
  before implementation. If the user requested planning first, wait for explicit
  implementation approval.
- A complete plan states:
  - desired behavior and success criteria;
  - ownership by controller/model/view/service;
  - public interfaces, signals, configs, and serialized changes;
  - data flow and lifecycle/disposal behavior;
  - validation, edge cases, and failure behavior;
  - migration/removal of obsolete bindings, assets, or APIs;
  - compile, Edit Mode, and Play Mode verification.
- State assumptions that materially affect behavior. Ask only for decisions that
  cannot be established safely from the repository or prompt.

### 3. Implement the smallest coherent change

- Reuse the existing source of truth and extension points before creating a new
  system.
- Do not leave duplicate controllers, competing subscriptions, old installer
  bindings, dead APIs, or obsolete assets after a replacement is complete.
- Before deleting or renaming serialized assets/scripts, search scenes, prefabs,
  installers, configs, and code for references. Preserve Unity `.meta` identity
  when an asset is being modified in place.
- Keep public API changes focused and update every caller in the authorized scope.
- Do not mix opportunistic cleanup with the requested behavior. Note unrelated
  problems separately.

### 4. Verify before reporting completion

- Compile `Assembly-CSharp` using the current Unity-generated project/response
  files or let Unity recompile and inspect the Editor log.
- Confirm Unity completes domain reload without new errors.
- Check scenes, prefabs, installers, and ScriptableObjects for missing serialized
  references and stale bindings.
- Confirm every touched behavior-bearing controller/model implements and exposes
  `IDisposable`, and that all owned work is disposed or cancelled.
- Search touched code for prohibited update methods, coroutines, polling streams,
  multiple declared types, and unmanaged subscriptions.
- Exercise relevant happy paths, invalid inputs, boundaries, repeated input,
  interruption/disposal, scene transitions, and pooled reuse.
- Run focused Edit Mode or Play Mode tests when available and add tests when the
  behavior can be isolated usefully.
- Report exactly what was verified. Never claim a compile, Unity check, or runtime
  test that was not actually run.

## Recurring project conventions

The following are strongly supported by repeated project requests. They are
defaults rather than direct universal mandates when a task explicitly requires a
different design.

- Prefer event-driven recalculation on meaningful state changes over continuous
  observation.
- Preserve designer-authored prefab state and make runtime systems restore it when
  their temporary ownership ends.
- Centralize selection and resolution policy—for example target resolution,
  profile lookup, sprite selection, or handler priority—in one focused resolver.
- Keep generation/data creation separate from visualization and runtime spawning.
- Use services to extract focused behavior from overloaded controllers or models,
  while keeping orchestration in the controller.
- Use interfaces and priority-based handlers for extensible interactions instead
  of growing hard-coded conditional chains.
- Prefer configuration-driven thresholds, durations, offsets, speeds, capacities,
  and fallbacks.
- Make failure safe: invalid targets do nothing, full inventories leave world
  items intact, failed spawns do not destroy the source, and interrupted movement
  settles into a valid state.
- Preserve current behavior outside the requested change, including authored art,
  sprite style, data taxonomy, action priority, and input semantics.

## Quick DO / DON'T checklist

### DO

- Inspect the complete feature flow before changing it.
- Inject dependencies and bind lifecycle interfaces through Zenject.
- Use UniRx for discrete reactive events and UniTask for cancellable sequences.
- Implement and verify disposal for behavior-bearing controllers and models.
- Validate first and mutate only after the operation can succeed.
- Put tunable values in focused configs.
- Reset every piece of mutable state when reusing pooled objects.
- Keep one type per matching file in the correct subsystem/layer folder.
- Compile and check Unity serialization after changes.

### DON'T

- Add `Update`, `FixedUpdate`, `LateUpdate`, coroutines, polling, or
  `Observable.EveryUpdate`.
- Hide a polling loop inside UniTask.
- Leave subscriptions, events, tweens, timers, or cancellation sources alive.
- Put multiple or nested types in one script.
- Put business logic in views or unrelated responsibilities in controllers.
- Create runtime Input Actions that belong in the Input System asset.
- Bypass required world-item, validation, or interaction flows.
- Store presentation assets as runtime item identity.
- Copy a legacy violation because it already exists.
- Refactor, delete, or reformat unrelated user work.

## Pre-delivery checklist

- [ ] Requested behavior and edge cases are implemented.
- [ ] New and touched scripts follow the subsystem/layer folder structure.
- [ ] Every file declares exactly one matching type, with no nested helper types.
- [ ] Every behavior-bearing controller and model implements `IDisposable`.
- [ ] Zenject exposes disposable lifecycle interfaces.
- [ ] Subscriptions, callbacks, tasks, timers, tweens, and registrations are cleaned up.
- [ ] No prohibited update method, coroutine, polling stream, or perpetual task loop was added.
- [ ] Config and serialized references are valid.
- [ ] Pooled objects fully reset and cancel their previous lifetime.
- [ ] `Assembly-CSharp` compiles and Unity reports no new domain-reload errors.
- [ ] Relevant Edit Mode/Play Mode behavior was tested, or unrun checks are disclosed.
- [ ] Unrelated working-tree changes remain untouched.
