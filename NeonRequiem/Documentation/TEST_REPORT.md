# TEST REPORT

Milestone: Phase 0–1 · Report date: 2026-11-17

This report separates four categories and never mixes them:

1. **Executed** — actually run; results below are observed.
2. **Compiled only** — written and type-checked; never run.
3. **Static checks** — performed on files without running Unity.
4. **Not performed** — needs the Unity Editor or a device.

---

## 1. Executed

### 1.1 Compilation against Unity (Roslyn, C# 9)

Tool: `Tools/StaticValidation/validate.py`, .NET SDK 8.0.134.
References: Unity **6000.3.28f1** engine and editor assemblies, .NET Standard 2.1 reference
assemblies shipped with that editor, compiled Input System / uGUI / URP 17.3.0 / Test Runner
assemblies from the editor's URP template cache, Unity's bundled NUnit.

| Assembly | editor | android (no UnityEditor) | ios (no UnityEditor) |
| --- | --- | --- | --- |
| NeonRequiem.Core (15 files) | 0 errors, 0 warnings | 0 / 0 | 0 / 0 |
| NeonRequiem.Runtime (36 files) | 0 / 0 | 0 / 0 | 0 / 0 |
| NeonRequiem.Editor (2 files) | 0 / 0 | not built (editor-only) | not built |
| NeonRequiem.Tests.EditMode (6 files) | 0 / 0 | not built | not built |
| NeonRequiem.Tests.PlayMode (1 file) | 0 / 0 | not built (tests excluded from players) | not built |

What this proves: syntax and types are correct; every Unity, Input System, uGUI and URP API used
exists with that signature in the targeted versions and is not obsolete-as-error; assembly
references are complete; runtime code uses no editor-only API.

What it does not prove: that Unity's own import/compile pipeline agrees in every detail (it adds
analyzers and platform defines), or anything about runtime behaviour. The package assemblies used
as references were compiled in editor configuration.

### 1.2 Logic unit tests

Tool: `Tools/StaticValidation/run_logic_tests.py` — NUnit 3.14 on .NET 8, test sources from
`Assets/_NeonRequiem/Tests/EditMode/Logic`, running against the assemblies built in 1.1.

**Result: 171 test cases, 171 passed, 0 failed, 0 skipped** (0.9 s).

| Fixture | Cases | Covers |
| --- | --- | --- |
| PlayerLocomotionTests | 43 | Run/sprint speeds, camera-relative movement, stopping, turning; jump height at 30/60/120 FPS, short hop, events, terminal velocity, air jump limit and height, coyote time (inside/expired), walk-off, jump buffer (fresh/stale), head bump; dodge distance at 30/60/120 FPS, backstep, facing, invulnerability window, cooldown, buffered dodge, air dodge, dodge off a ledge, exit speed; wall contact, steep-slope slide, walkable slope; invalid/huge delta time, teleport, null arguments |
| NRMathTests | 20 | Damping (frame-rate independence, no overshoot), angle wrap, yaw/direction conversion, radial dead zone, MoveTowards |
| GameStateMachineTests | 16 | Full transition table, events, rejected transitions |
| CircularBufferTests | 12 | Order, overwrite, bounds, pop front/back, clear, copy |
| AdaptiveResolutionPolicyTests | 12 | Config validation, sample/interval gates, step down to floor, tracker reset, upward probe, back-off doubling and reset, cap, invalid inputs |
| LookMathTests | 10 | DPI vs screen-width scaling, same physical swipe on phone and tablet, stick curve, combination, sensitivity, invert, time scaling |
| OrbitCameraModelTests | 10 | Reset, forward vector convention, yaw wrap, pitch clamp, follow-assist rules and convergence, collision pull-in, ease-out, minimum distance |
| SettingsManagerTests | 9 | Defaults, custom defaults, persist + notify once, every field round-trips, sanitising on modify/load, newer schema, reset, null arguments |
| DeviceTierHeuristicTests | 7 | Preset suggestion table |
| FrameTimeTrackerTests | 7 | Rejection of invalid samples, average, percentile, window |
| SafePositionTrackerTests | 7 | Sampling, age filter, fallback, clear |
| VirtualJoystickMathTests | 5 | Dead zone, clamp, proportionality |
| SafeAreaMathTests | 4 | Notch/home-indicator anchors, clamping, unusable input |
| GameSettingsTests | 3 | Clamping, non-finite values, clone completeness |
| MovementTuningTests | 3 | Jump velocity, sanitising |
| LocalizationTests | 2 | Every key has text; missing-key behaviour |
| PlayerInputSampleTests | 1 | Strongest source wins |

Mapping to the directive's required automated tests (those applicable to this milestone):
memory replay buffer behaviour → `CircularBufferTests`; state transitions → `GameStateMachineTests`
and the locomotion mode tests; cooldowns → dodge cooldown/buffer tests; settings serialization →
`SettingsManagerTests`. Damage, energy, save migration, inventory, progression, missions and
Echo Shift tests belong to the phases that introduce those systems.

**Caveat.** These tests ran on the .NET runtime, not inside Unity. The code under test uses only
managed `UnityEngine` math types, so results are expected to be identical in the Unity Test
Runner, but that run has not happened.

One defect was found by this suite and fixed (steep-slope sliding; see IMPLEMENTATION_LOG).

---

## 2. Compiled only (never run)

| Suite | Tests | Needs |
| --- | --- | --- |
| `Tests/EditMode/Unity/ProjectSetupTests` | 7: project validator has no errors; Player layer; quality level per preset; Input Actions asset loads and is complete; keyboard and gamepad bindings exist; movement config loads; template materials load | Unity Editor (Test Runner, EditMode) |
| `Tests/PlayMode/GameplayPlayModeTests` | 11: scene boots to Playing with a grounded player; forward input runs the player; jump reaches height and lands; dodge covers distance with invulnerability; pause freezes time, shows the menu and blocks input; pause button toggles; fall recovery; settings re-lay out touch controls live; scene change keeps the runtime; unknown scene rejected; Bootstrap scene hands over | Unity Editor (Test Runner, PlayMode) |

These cover the directive's PlayMode list for this milestone: player spawning, scene transitions,
UI interaction (settings), pause/resume, death-equivalent (fall) and respawn. Enemy spawning, hit
detection, checkpoints and save/load do not exist yet.

---

## 3. Static checks performed

| Check | Result |
| --- | --- |
| All 37 authored/derived Unity YAML files (scenes, materials, config, URP assets, project settings) parse as YAML | Pass |
| Every `guid:` reference in scenes, Resources assets, build/graphics/quality settings resolves to a `.meta` in the project or to a known URP/built-in asset | Pass (32 references) |
| Local `fileID` references inside each scene resolve to an object in that scene | Pass (no dangling references) |
| No duplicate GUIDs across 112 `.meta` files; no orphan `.meta` files | Pass |
| `link.xml` type and assembly names exist in the Unity 6000.3 assemblies | Pass |
| URP script GUIDs used in scenes (camera/light additional data) match URP 17.3.0 package source | Pass |
| Serialized field names in scene/asset YAML match the C# fields | Pass (the movement config asset is generated from the C# defaults) |
| Input Actions JSON: 8 actions, 24 bindings, structure matches the Unity template's asset | Pass (not imported by Unity) |

---

## 4. Not performed

| Item | Why it matters |
| --- | --- |
| Opening the project in Unity 6000.3 (package resolution, asset import, shader compilation) | First-open errors are possible; scene/asset YAML has never been deserialized by Unity |
| Entering Play mode | CharacterController behaviour, uGUI touch dispatch, URP rendering, lighting look, camera feel are all unobserved |
| Unity Test Runner (EditMode + PlayMode) | Section 2 is unexecuted |
| Android / iOS build | IL2CPP and engine-code stripping (mitigated by `link.xml`, unproven), shader variant stripping, build settings |
| Any device test | Touch ergonomics, performance, thermals, safe areas, backgrounding |
| Shader compilation | No custom shaders exist; URP's own shaders are used |

**No frame rate, memory, thermal or battery figure exists for this project.**

---

## 5. Verification procedure for the first Unity session

Record results in a new section of this file.

1. Open the project. Expected: no red errors in the Console after import. Unity may rewrite some
   `.asset`/`.unity`/`.meta` files into its own formatting; commit that diff.
2. **Neon Requiem > Validate Project Setup** → "Project setup is valid."
3. **Test Runner > EditMode > Run All** → 178 passing (171 logic + 7 setup).
4. **Test Runner > PlayMode > Run All** → 11 passing.
5. Press Play from the Bootstrap scene, then work through section 6.
6. Build to a phone and repeat section 6 with touch.

## 6. Manual test plan (movement course)

| # | Where | Action | Expected |
| --- | --- | --- | --- |
| M1 | Spawn | Push the stick slightly, then fully | Slow walk, then 5.6 m/s run; overlay shows speed |
| M2 | Anywhere | Hold Dodge/Sprint (touch) or Shift | 8.4 m/s; slight field-of-view widening; "sprint" in overlay |
| M3 | Stairs (west) | Run up and down | Smooth, no snagging, no hopping on the way down |
| M4 | 20° and 35° ramps | Run up and down | Walkable both ways, no sliding, stays grounded going down |
| M5 | 58° slab | Jump onto it | Slides off; cannot be climbed |
| M6 | 1.0 m and 1.6 m platforms | Single jump | Reachable (1.6 m needs a full, held jump) |
| M7 | 2.9 m platform | Jump, then jump again in the air | Reachable only with the air jump |
| M8 | Any | Tap jump vs hold jump | Short hop vs full height |
| M9 | Platform edge | Run off and press jump just after leaving | Jump still happens (coyote time) |
| M10 | 4 m gap | Sprint jump or double jump | Clears it |
| M11 | 6 m gap | Sprint jump + air jump, or jump + air dodge | Clears it |
| M12 | Dodge lane (east) | Dodge from a marker | Travels about 4.2 m (4 markers); suit circuitry flares during invulnerability |
| M13 | Dodge lane | Dodge with no direction held | Backstep, facing unchanged |
| M14 | Dodge lane | Tap dodge repeatedly | One dodge per ~0.7 s; no chain |
| M15 | Pillars | Circle with the camera | Camera never clips into pillars; pulls in and eases out |
| M16 | Tunnel | Run through, rotate camera | Camera stays inside the tunnel |
| M17 | Low ceiling | Jump underneath | Jump cut short; no sticking |
| M18 | North edge / balcony | Run off | Falls, then reappears on solid ground near where they left |
| M19 | Pause | Open, change each setting, resume | Each setting applies immediately and survives restarting the app |
| M20 | Pause > Left-handed | Toggle | Stick moves right, buttons move left |
| M21 | Any | Idle the camera while running | After ~1.2 s the camera gently swings behind the direction of travel; never when running toward it |
| M22 | Gamepad | Connect and use | Touch controls hide; a screen touch brings them back |

Device-only checks: two-thumb simultaneous move + look + jump; controls inside the safe area on
notched phones in both landscape orientations; app to background and back (game is paused, no
stuck stick); 15 minutes of continuous play watching the overlay for render-scale steps and heat.

## 7. Defect severity and regression policy

| Severity | Definition | Rule |
| --- | --- | --- |
| S1 Blocker | Crash, data loss, cannot start or cannot progress | Stops the milestone; fix immediately |
| S2 Critical | Core mechanic wrong (movement, input, camera), unfair damage, soft lock with workaround | Fix before the milestone is accepted |
| S3 Major | Clearly wrong but playable (visual glitch, tuning far off, UI overlap) | Fix within the next milestone |
| S4 Minor | Polish, wording, cosmetic | Backlog |

Every S1/S2 fix adds a test that fails without it. Regression gate before any phase is closed:
compile in all three configurations, logic tests, EditMode and PlayMode suites, the manual plan
above on one Android and one iOS device.
