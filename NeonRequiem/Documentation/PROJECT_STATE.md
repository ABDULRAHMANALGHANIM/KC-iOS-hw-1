# PROJECT STATE

Last updated: 2026-11-17 · Milestone: **Phase 0 (technical foundation) + Phase 1 (playable movement prototype)**

This file is the single source of truth for what exists. If it disagrees with any other
document, this file wins and the other document is out of date.

## 1. Status at a glance

| Phase | Scope | State |
| --- | --- | --- |
| 0 | Project, packages, bootstrap, input configuration, basic mobile UI, test environment, documentation | **Implemented. Compiled against Unity 6000.3.28f1. Not yet opened in the Unity Editor.** |
| 1 | Third-person controller, camera, virtual joystick, camera touch control, jump, dodge, basic environment, placeholder animation | **Implemented. Logic unit-tested (171 cases pass). Not yet played in Unity or on a device.** |
| 2 | Combat foundation (attacks, hit detection, combos, parry, enemy AI, combat HUD) | Not started |
| 3 | Signature mechanics (Echo Shift, Gravity Weave, Memory Fracture, Resonance) | Not started. Supporting pieces exist: ring buffer, safe-position tracker |
| 4 | Premium vertical slice | Not started |
| 5 | Content production | Not started |
| 6 | Polish and optimization | Not started |
| 7 | Release preparation | Not started |

### Acceptance criteria

| Criterion (from the development directive) | Verdict | Evidence |
| --- | --- | --- |
| Phase 0: "The project opens and runs without compilation errors on the selected Unity version." | **Partially evidenced.** No compile errors; opening and running are unverified. | All five assemblies compile with Roslyn (C# 9) against the real Unity 6000.3.28f1 engine/editor assemblies and the compiled Input System, uGUI, URP and Test Framework assemblies, in editor, Android and iOS configurations: 0 errors, 0 warnings. |
| Phase 1: "The player can move, jump, dodge, and control the camera on a mobile device." | **Not yet verified.** | Movement, jump and dodge rules pass 43 locomotion unit tests against a simulated body. Behaviour with Unity's CharacterController, the uGUI touch pipeline and real devices has not been observed. |

**The next action is a human one:** open the project in Unity 6000.3, run
*Neon Requiem > Validate Project Setup*, run both Test Runner tabs, press Play, then build to a
phone. Record the results in TEST_REPORT.md. Phase 2 should not start before that.

## 2. What exists

### Code (60 C# files, about 9,700 lines including tests)

| Assembly | Files | Contents |
| --- | --- | --- |
| `NeonRequiem.Core` | 15 | Logging facade, game flow state machine, GameManager (pause contract), SceneFlowController, settings model + manager + stores, CircularBuffer, FrameTimeTracker, AdaptiveResolutionPolicy, DeviceTierHeuristic, NRMath, localization table, constants |
| `NeonRequiem.Runtime` | 36 | Input (router, Input Actions source, look math, joystick, look zone, touch button, touch layout), Player (locomotion, motor, controller, tuning, config asset, safe-position tracker, placeholder rig, factory), Camera (orbit model, controller), UI (HUD, pause/settings, overlay, diagnostics, factory, sprites, safe area), World (arena builder, materials, fall recovery), Optimization (PerformanceManager), Bootstrap (GameRuntime, GameBootstrap, GameplaySceneInstaller) |
| `NeonRequiem.Editor` | 2 | ProjectValidator, scene/menu helpers |
| `NeonRequiem.Tests.EditMode` | 6 | 171 engine-independent cases + 7 project-setup tests |
| `NeonRequiem.Tests.PlayMode` | 1 | 11 integration tests |

### Player-facing features

- Camera-relative running with analog speed, sprint (hold), fast turning
- Jump with variable height, coyote time (0.12 s), input buffering (0.14 s) and one air jump
- Dodge: 4.2 m in 0.34 s, invulnerable from 0.02 s to 0.24 s, 0.35 s cooldown, buffered input, one air dodge per airtime, backstep when no direction is held
- Sliding on ground steeper than 48 degrees; head-bump handling; no speed build-up against walls
- Third-person orbit camera: touch/mouse/stick look, pitch limits, sphere-cast collision, follow assist, sprint field-of-view kick
- Touch controls: floating joystick, camera drag zone, Jump / Dodge(+hold to sprint) / Pause, safe-area aware
- Keyboard + mouse and gamepad support through one Input Actions asset; touch controls hide during gamepad play
- Pause menu with live settings: sensitivity, invert Y, camera assist, reduce camera motion, left-handed layout, floating joystick, control size, control opacity, graphics preset, frame-rate target, adaptive resolution, performance overlay
- Three graphics presets (Performance / Balanced / Quality) as URP quality levels; frame-time driven adaptive render scale
- Fall recovery to the last stable ground; "return to spawn"
- Auto-pause when the app is backgrounded on mobile
- Two test scenes: a movement course (stairs, 20/35/58 degree slopes, jump heights, 4 m and 6 m gaps, dodge lane with metre markers, pillars, tunnel, low ceiling, open edge) and a controls lab with live input diagnostics

### Assets

- Scenes: `Bootstrap`, `MovementTest`, `MobileControlsTest` (derived from the official Unity 6000.3 URP template scene)
- URP: three pipeline assets, one renderer, post-processing volume profile, global settings
- `NeonRequiemControls.inputactions`, `PlayerMovementConfig.asset`, three template materials
- No models, textures, animation clips, audio or fonts. Everything visible is placeholder (see ASSET_REQUIREMENTS.md)

## 3. What does not exist (and is not claimed)

- Combat of any kind, enemies, AI, bosses
- Echo Shift, Gravity Weave, Memory Fracture, Resonance
- Story content, dialogue, missions, cinematics
- Progression, inventory, equipment, skill trees
- Save system (only settings persist, in PlayerPrefs, by design)
- Audio system and audio content
- Main menu and all screens other than the HUD and pause menu
- Haptics, lock-on, ledge/wall traversal, moving platforms
- Custom shaders (cel shading), character model and animation
- Addressables, Unity Localization, AI Navigation packages (deliberately not installed until first used)
- Any device performance data

## 4. Known limitations of this milestone

| # | Limitation | Impact | Planned resolution |
| --- | --- | --- | --- |
| L1 | Never run in Unity | Unknown runtime defects are likely to exist | Human verification pass (section 1) |
| L2 | Scenes, materials and settings were authored as YAML by script rather than saved by the editor | Unity may re-serialize them on first import (harmless diff); a malformed file would fail to load | Covered by `ProjectValidator` and setup tests; re-save from the editor after first open |
| L3 | Environment and character are built at runtime from primitives | No baked lighting, occlusion or LODs; not representative of final render cost | Phase 4 replaces them with authored content |
| L4 | Adaptive resolution reads CPU-side frame time only | Cannot see GPU headroom directly; it probes upward periodically instead | Phase 6: GPU timings via FrameTimingManager after device benchmarking |
| L5 | Device-tier heuristic thresholds are guesses | First-run preset may be wrong on some devices (player can change it) | Replace with a measured device table |
| L6 | Pause menu list does not auto-scroll for gamepad navigation | Off-screen rows need manual scrolling with touch/mouse | Phase 2 UI pass |
| L7 | Player does not inherit velocity from moving platforms; no ledge grab, vault or wall traversal | Traversal vocabulary is basic | Added when level design requires them |
| L8 | UI uses Unity's built-in legacy font and legacy `Text` | Not final typography; no Arabic shaping | TextMeshPro + licensed font with the localization pass |
| L9 | In the editor, the runtime changes the active quality level and render scale | The project's current quality level may differ after play mode | Render scale is restored on exit; quality level is an editor-wide setting |

## 5. Feasibility assessment

**Complexity.** The full design is a commercial 3D action RPG. The systems (combat, three
interacting signature mechanics, five enemy classes, four multi-phase bosses, five districts,
progression, save, localization) are individually well understood; the difficulty is their
interaction and the content volume.

**Principal technical risks**

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| Echo Shift doubling scene cost | Two world states can mean double draw calls, memory and physics | Shared geometry with per-state activation sets and shader blending; prototype on device before building content for it |
| Memory Fracture determinism | Physics replay is not deterministic | Replay recorded *state and action events*, not simulation; limit to one phantom; specify which actions affect gameplay |
| Combat feel on touch | Latency and occlusion by thumbs decide whether the game is fun | Press-on-down inputs, buffered actions (already in place), generous windows, device-first tuning |
| Mobile thermals | Sustained 60 FPS throttles phones within minutes | Presets, adaptive resolution (in place), 30 FPS mode, thermal API integration in Phase 6 |
| Gravity Weave physics cost | Unbounded rigidbody interaction is expensive | Mass categories, capped active object count, kinematic "held" state |
| Content volume | Districts, enemies, bosses, animation and audio dominate cost | Modular kits, data-driven encounters, vertical slice before scale-up |

**What cannot be produced by code generation alone:** character models and rigs, animation,
environment art, VFX textures, music, sound effects, voice, UI art, store assets, and device QA.
These are listed in ASSET_REQUIREMENTS.md and are the true critical path.

**Effort estimates** (order-of-magnitude, for planning only; they assume the listed disciplines exist):

| Deliverable | Team | Duration |
| --- | --- | --- |
| Phase 2 + 3 gameplay code with placeholder art | 1–2 programmers | 6–10 weeks |
| Phase 4 vertical slice (one polished 15–30 minute district) | 5–8 people: 2 programmers, character artist, environment artist, animator, technical artist, audio, designer | 4–6 months |
| Full game as specified | 15–25 people plus QA | 18–30 months |
| Solo developer, placeholder or purchased art | 1 | Vertical-slice-quality demo: 6–12 months; the full scope is not realistic solo |

**Recommendation.** Treat the vertical slice as the product goal. Decide after it whether the
full scope is funded.

## 6. Next milestone: Phase 2 — combat foundation

Entry condition: Phase 1 verified in the editor and on at least one phone.

Planned dependency order:
1. `Health`, `DamageInfo`, `DamageResolver`, damage types, hurtbox/hitbox components and layers
2. `AttackDefinition` (ScriptableObject: startup/active/recovery, damage, poise damage, cancel windows)
3. `CombatStateMachine` + `ComboResolver` on top of `PlayerLocomotion` (light chain, heavy, launcher)
4. `ParrySystem`, perfect dodge (uses the existing invulnerability window), stagger/poise
5. Enemy: `EnemyProfile`, perception, hierarchical FSM, NavMesh movement (adds the AI Navigation package), `EnemyDirector` attack tokens
6. Target lock (camera framing + strafing), combat HUD (health, energy, resonance), hit-stop, camera shake with accessibility scaling, haptics
7. Tests: damage maths, state transitions, cooldowns, token limits; PlayMode: defeat one Choir Sentinel

Acceptance: "The player can defeat a functioning enemy through responsive combat."
