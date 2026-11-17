# ARCHITECTURE

Describes the code as it exists at the Phase 0–1 milestone, and the decisions that shape later
phases. Systems that are planned but not built are marked *(planned)*.

## 1. Platform and package strategy

| Choice | Value | Reason |
| --- | --- | --- |
| Engine | Unity **6000.3.28f1** (Unity 6.3 LTS) | Current long-term-support stream at the time of writing; code avoids APIs that differ from 6000.0 LTS |
| Language | C# 9.0, .NET Standard 2.1 profile | Unity 6000.3 defaults |
| Render pipeline | URP 17.3.0 (Forward) | Mobile target; SRP Batcher; per-quality-level pipeline assets |
| Input | Input System 1.20.1, "Input System Package (New)" only | Touch, gamepad and keyboard behind one action asset; legacy input disabled |
| UI | uGUI 2.0.0 | Mature touch event pipeline, per-pointer multi-touch. UI Toolkit can be evaluated for menu-heavy screens later |
| Tests | Test Framework 1.6.0 (NUnit) | EditMode + PlayMode |
| Scripting backend | IL2CPP, ARM64 (Android); IL2CPP (iOS) | Store requirements and performance |

Package versions are the ones Unity 6000.3.28f1 itself declares as verified (read from the
editor's package manifest), not guesses.

**Deliberately not installed yet** — each is added in the phase that first uses it, so the
project never carries an unused dependency:

| Package | Verified version | Arrives with |
| --- | --- | --- |
| AI Navigation | 2.0.16 | Phase 2 (enemy movement) |
| Addressables | 2.10.3 | Phase 4 (district streaming, replaces `Resources`) |
| Localization | 1.5.8 | Phase 4 (string tables; Arabic/RTL pass) |
| Adaptive Performance | 6.0.0 | Phase 6 (thermal state feeding the quality policy) |

## 2. Assemblies and dependency direction

```
NeonRequiem.Tests.PlayMode ─┐
NeonRequiem.Tests.EditMode ─┼─► NeonRequiem.Editor ─► NeonRequiem.Runtime ─► NeonRequiem.Core
                            └──────────────────────────────┘
NeonRequiem.Runtime ─► Unity.InputSystem, UnityEngine.UI, URP runtime
NeonRequiem.Core    ─► UnityEngine only
```

- **Core** has no gameplay knowledge and no package dependencies. Most of it is engine-free logic.
- **Runtime** holds every gameplay folder (`Input`, `Player`, `Camera`, `UI`, `World`,
  `Optimization`, `Bootstrap`). It will be split further only when compile time or layering
  problems justify it.
- Dependencies point one way. Nothing in Core references Runtime; nothing in Runtime references Editor.

Namespaces: `NeonRequiem.Core`, `.Controls` (input; not `.Input`, which would shadow engine types),
`.Player`, `.Cameras`, `.UI`, `.World`, `.Optimization`, `.App`, `.EditorTools`, `.Tests`.

## 3. Lifetime and composition

```
Bootstrap scene                       Gameplay scene (MovementTest, ...)
  GameBootstrap ──creates──► GameRuntime (DontDestroyOnLoad)          GameplaySceneInstaller
                               ├─ SettingsManager                       ├─ TestArenaBuilder.Build(...)
                               ├─ GameManager ── GameStateMachine       ├─ PlayerFactory.Create(...)
                               ├─ SceneFlowController                   ├─ ThirdPersonCameraController
                               ├─ PlayerInputRouter ◄─ sources          ├─ FallRecovery
                               ├─ PerformanceManager                    └─ Hud.BindPlayer, GameManager.BeginPlaying
                               ├─ NeonMaterialLibrary
                               └─ HudController (canvas, EventSystem)
```

- **`GameRuntime` is the only composition root for long-lived services.** It constructs them in
  dependency order and passes references explicitly (`Initialize(...)`). There is no service
  locator and no `FindObjectOfType`. The single static is `GameRuntime.Instance`, reset on
  `SubsystemRegistration` so play mode without domain reload works.
- **`GameplaySceneInstaller` is the scene-level composition root.** Gameplay scenes can be started
  directly in the editor: the installer calls `GameRuntime.EnsureExists()`.
- **Game flow**: `Boot → Loading → Playing ⇄ Paused`, `Playing/Paused → Loading`. Transitions are
  validated by `GameStateMachine` (pure, table-tested). `GameManager` owns the pause contract
  (time scale 0; input router gated; HUD swaps to the pause menu) and auto-pauses on mobile when
  the app loses focus.
- **Scene changes** go through `SceneFlowController.LoadScene`, which rejects unknown scenes,
  concurrent loads and illegal states with an explicit error instead of failing silently.

Script execution order: `GameBootstrap` −1000, `GameRuntime` −900, uGUI `EventSystem` 0,
`PlayerInputRouter` 10, `PlayerController` 20, `HudController` 30, camera in `LateUpdate`. Input
is therefore sampled *after* this frame's touches were dispatched and consumed in the same frame.

## 4. Input pipeline

```
Touch widgets (uGUI events) ─┐
                             ├─► IPlayerInputSource.Sample(ref PlayerInputSample)
Input Actions (kbd/mouse/pad)┘            │  raw units: stick, pixels, held/pressed flags
                                          ▼
                                 PlayerInputRouter  ── LookMath (pixels/stick → degrees, sensitivity, invert)
                                          ▼
                                 PlayerInputFrame   ── the only input type gameplay reads
```

- `PlayerInputFrame` is device-independent: `Move`, `LookDegrees`, `JumpPressed/Held`,
  `DodgePressed`, `SprintHeld`, `PausePressed`. Gameplay code has no reference to the Input System.
- Touch look is converted by **physical distance** (degrees per inch from `Screen.dpi`) so the
  same thumb movement turns the camera equally on a phone and a tablet; when the reported DPI is
  implausible it falls back to a fraction of screen width.
- Touch widgets: `VirtualJoystick` (floating, radial dead zone with rescaling, drag threshold
  disabled), `TouchLookZone` (accumulates pixel deltas), `TouchButton` (fires on pointer **down**;
  exposes hold time). Each tracks exactly one pointer id, releases on disable and on focus loss.
- One button serves dodge (tap) and sprint (hold > 0.28 s), keeping the action cluster small.
- `TouchControlsView` is laid out from data and re-applied on every settings change: scale,
  opacity, floating stick, and a full left-handed mirror. Minimum touch target is enforced.
- Scheme tracking (`Touch`, `KeyboardMouse`, `Gamepad`) hides touch controls during gamepad play.
- The Input Actions asset is loaded from `Resources` and **instantiated**, so runtime changes
  (future rebinding) never dirty the shared asset. If it is missing, touch input still works.

## 5. Player

| Type | Kind | Responsibility |
| --- | --- | --- |
| `MovementTuning` | serializable data | Every movement number; `Sanitize()` guards against bad values |
| `PlayerMovementConfig` | ScriptableObject | Designer-facing asset wrapping the tuning |
| `ICharacterMotor` | interface | "Move by this displacement and tell me what happened" |
| `PlayerMotor` | MonoBehaviour | CharacterController wrapper; ground normal by ray |
| `PlayerLocomotion` | plain class | **Authoritative movement rules and state** (Grounded / Airborne / Dodging), events |
| `PlayerController` | MonoBehaviour | Wires input + camera heading → locomotion → transform → visual rig |
| `SafePositionTracker` | plain class | Ring buffer of recent stable ground positions |
| `PlayerVisualRig` | MonoBehaviour | Placeholder body and procedural animation; reads state only |
| `PlayerFactory` | static | The one place that knows the player object's structure |

Key decisions:

- **Rules are separate from physics.** `PlayerLocomotion` contains no engine calls beyond vector
  math, so it is unit-tested against a simulated motor. This is where combat cancel windows and
  ability states will attach in Phase 2.
- **Frame-rate independence by construction.** Vertical motion uses the trapezoid rule (exact for
  constant acceleration); the dodge integrates its speed profile analytically. Tests assert jump
  height and dodge distance at 30, 60 and 120 FPS.
- **Presentation never feeds gameplay.** The rig listens to `Jumped`, `Landed`, `DodgeStarted`...
  and reads velocity; replacing it with an Animator-driven character changes no gameplay code.
  Root motion policy: gameplay-authoritative movement; root motion will be limited to
  cinematics and specific finishers.
- **Invulnerability is state, not animation.** `IsInvulnerable` is derived from dodge timing data
  and is what combat will query.

## 6. Camera

`OrbitCameraModel` (pure) holds yaw, pitch and distance and implements pitch limits, follow
assist and collision-distance behaviour. `ThirdPersonCameraController` adds the engine parts:
pivot smoothing (vertical softer than horizontal), a sphere cast from the pivot to keep the view
out of geometry (pull in immediately, ease back out), sprint field-of-view kick and the URP
post-processing switch.

Rules that keep the camera predictable: the follow assist acts only while moving, only after
1.2 s without manual look, never when the character runs toward the camera, and it can be turned
off. "Reduce camera motion" disables the assist-independent motion effects (field-of-view kick,
floaty follow). No camera shake exists yet; when added it will scale with an accessibility setting.

## 7. Interface

- One screen-space overlay canvas (reference 1920×1080, scaled by height), a safe-area container,
  and a full-screen pause layer above it.
- Phase 0–1 UI is built from code through `UIFactory` so the project needs no authored prefabs.
  Screens with real art will be prefabs loaded through Addressables.
- Large touch zones use `RaycastZone` (one transparent unit quad) instead of full-screen
  transparent images, avoiding full-screen overdraw.
- All display text goes through `Loc.Get(LocKeys.X)`. Moving to Unity Localization and adding
  Arabic is a table swap plus layout mirroring (the left-handed mirror already proves the layout
  code can flip), not a UI rewrite.
- Settings rows are 112 reference units tall with 92-unit controls (about 6 mm on a phone) and
  scroll when they do not fit.

## 8. Rendering and performance

### Presets

The three presets are Unity quality levels, each with its own URP asset
(`Settings/Rendering/NR_URP_*.asset`). Switching presets is `QualitySettings.SetQualityLevel`.

| Setting | Performance | Balanced | Quality |
| --- | --- | --- | --- |
| Render scale | 0.70 | 0.85 | 1.00 |
| MSAA | off | 2x | 4x |
| HDR | off | on | on |
| Post-processing (bloom, tonemapping, vignette) | off | on | on |
| Main light shadow map | 512 | 1024 | 2048 |
| Shadow distance / cascades | 25 m / 1 | 40 m / 1 | 60 m / 2 |
| Soft shadows | off | off | on |
| Per-object additional lights | 2 | 4 | 4 |
| Default on | weak phones (heuristic) | phones | desktop |

### Cost of the major visual features (qualitative; no device has been measured yet)

| Feature | GPU cost | Memory cost | Notes |
| --- | --- | --- | --- |
| Render scale | Fragment cost scales with scale² (0.7 → about half the pixels) | Colour/depth targets scale the same way | Cheapest large lever; drives adaptive resolution |
| MSAA | Low on tile-based mobile GPUs at 2x, noticeable at 4x | Multiplies colour/depth target size by sample count until resolve | Off on Performance |
| HDR target | Similar fill cost; wider format | Larger colour target on some devices | Required for bloom on emissive neon to read correctly |
| Bloom | Several full-screen down/up-sample passes: usually the most expensive post effect on phones | A mip chain of temporary targets | High-quality filtering disabled; skipped entirely on Performance |
| Real-time shadows | A depth pass over shadow casters plus sampling per lit pixel; soft shadows add taps | Shadow atlas (512² to 2048²) | Only the main light casts shadows |
| Additional lights | Per-pixel cost per light per object in Forward | none | Three unshadowed point lights in the test arena |
| Fog | A few instructions per vertex/pixel | none | Exponential-squared; enabled in the scene files so its shader variants survive build stripping |
| Transparent UI | Overdraw | none | Kept small; zones draw nothing |

### Adaptive resolution

`PerformanceManager` records frame times in a `FrameTimeTracker` (240-frame window, percentile
queries, allocation-free) and runs `AdaptiveResolutionPolicy` while the game is in the Playing
state. The policy multiplies the preset's render scale by a factor in [0.7, 1.0]: it steps down
when the 95th-percentile frame time exceeds 120% of the target, and probes one step up after a
sustained in-budget period, doubling the wait after a failed probe to avoid oscillation. The
original render scale is restored on shutdown.

### Budgets

Frame-time targets are 16.67 ms (60 FPS) and 33.33 ms (30 FPS). CPU, GPU, memory, draw-call,
geometry and texture budgets are **intentionally undefined** until representative devices have
been benchmarked; numbers invented before that would be arbitrary.

### Practices already in place

No per-frame allocations in input, locomotion, camera or performance code; cached property IDs
and materials; pure-logic hot paths; SRP Batcher-compatible URP materials; overlay text rebuilt
4 times per second; `Application.targetFrameRate` pacing; screen sleep disabled during play.
Planned: pooling for VFX/projectiles (Phase 2), LODs/occlusion/baked lighting (Phase 4),
shader-variant stripping review and thermal integration (Phase 6).

### Build-inclusion rule for runtime materials

Procedural content instantiates materials from **template assets** in
`Resources/NeonRequiem/Materials`. `Shader.Find` alone would work in the editor and fail on
devices, because Unity only ships shaders (and keyword variants) that a built asset references.

## 9. Settings and persistence

`GameSettings` (plain data, `Sanitize()`), `SettingsManager` (load, modify, persist, notify),
`ISettingsStore` (PlayerPrefs in the game, in-memory in tests). Settings are the only data in
PlayerPrefs; they carry a schema version for migration.

Progression saves *(planned, Phase 4)*: versioned JSON documents in `persistentDataPath`, written
to a temporary file and atomically renamed, with a checksum and a rotating backup; migration
functions per schema version; no binary deserialization.

## 10. Conventions

- Private fields `m_Name`, statics `s_Name`, constants/readonly `k_Name` or PascalCase constants.
- Every MonoBehaviour that needs dependencies has `Initialize(...)`; null arguments throw.
- Event subscriptions are removed in `OnDestroy`; statics are reset on `SubsystemRegistration`.
- Logging through `NRLog` with a category; verbose logs compile out of release builds.
- Placeholder content is labelled `PLACEHOLDER` in code comments and listed in ASSET_REQUIREMENTS.md.
- Logic that can be engine-free must be, and gets a test in `Tests/EditMode/Logic`.

## 11. Planned systems and where they attach *(planned)*

| System | Attaches to |
| --- | --- |
| CombatStateMachine, ComboResolver, AttackDefinition, Hitbox/Hurtbox, DamageResolver, Stagger, Parry | `PlayerLocomotion` (movement locks, cancels), `PlayerInputFrame` (new actions), `PlayerVisualRig` events |
| TargetLockSystem | `OrbitCameraModel` (framing), locomotion (strafe) |
| EnemyDirector, enemy FSM | `GameRuntime` (service), AI Navigation package |
| Echo Shift | World-state service in `GameRuntime`; `SafePositionTracker` for safe placement |
| Memory Fracture | `CircularBuffer<PlayerSnapshot>` recorded in `PlayerController` |
| Gravity Weave | Physics layers and mass categories; targeting through the camera |
| SaveManager, MissionManager, ProgressionManager, InventoryManager, AudioManager | `GameRuntime`, each with an explicit `Initialize` |
