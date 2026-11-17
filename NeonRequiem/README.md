# NEON REQUIEM

*Every Memory Has a Price. Every Choice Rewrites Reality.*

A 3D anime-inspired action-adventure for iOS and Android, built with Unity 6 (URP).

This repository folder is a complete Unity project. Its current state is the **Phase 0–1
milestone: a playable movement foundation** (run, sprint, jump, double jump, dodge, third-person
camera, touch controls, pause/settings, graphics presets). Combat, the three signature mechanics,
story content and final art are later phases. The authoritative status is in
[Documentation/PROJECT_STATE.md](Documentation/PROJECT_STATE.md).

> **Verification status.** The Unity Editor could not be run in the environment where this
> milestone was written. All C# was compiled against the real Unity 6000.3.28f1, Input System,
> uGUI and URP assemblies, and 171 engine-independent unit tests were executed and pass. The
> project has **not yet been opened, played or built in Unity**. See
> [Documentation/TEST_REPORT.md](Documentation/TEST_REPORT.md) for exactly what was and was not verified.

---

## Requirements

| Item | Version |
| --- | --- |
| Unity Editor | **6000.3.28f1** (Unity 6.3 LTS). Any 6000.3.x patch should work; Unity Hub will offer to upgrade. |
| Unity modules | Android Build Support (with OpenJDK, SDK and NDK) and/or iOS Build Support |
| iOS builds | macOS with Xcode |
| Packages | Resolved automatically from `Packages/manifest.json` on first open |

## Open and play (editor)

1. In Unity Hub choose **Add > Add project from disk** and select this `NeonRequiem` folder.
2. Open it with Unity 6000.3.x. The first import takes several minutes (package download, shader compilation).
3. The editor opens the `Bootstrap` scene automatically. If it does not, use the menu
   **Neon Requiem > Open Scene > Bootstrap**.
4. Run **Neon Requiem > Validate Project Setup**. It should report "Project setup is valid."
5. Press **Play**. The Bootstrap scene creates the runtime services and loads the `MovementTest` scene.

You can also open `MovementTest` or `MobileControlsTest` directly and press Play; gameplay scenes
create the runtime themselves.

### Controls

| Action | Touch | Keyboard / mouse | Gamepad |
| --- | --- | --- | --- |
| Move | Left side of the screen (floating stick) | W A S D or arrow keys | Left stick |
| Camera | Drag on the right side | Hold right mouse button and move | Right stick |
| Jump (hold for full height, press again in the air for a second jump) | JUMP | Space | South button (A / Cross) |
| Dodge (4.2 m, briefly invulnerable; backsteps with no direction held) | DODGE (tap) | Left Ctrl or C | East button (B / Circle) |
| Sprint | DODGE (keep holding) | Left Shift | Left stick click |
| Pause / settings | II (top corner) | Esc or P | Start |

In the editor the touch controls are shown and work with the mouse, so the mobile layout can be
exercised without a device. They hide while a gamepad is in use.

## Build for a device

1. **File > Build Profiles**, choose Android or iOS and **Switch Platform**.
2. The scene list is already configured (`Bootstrap`, `MovementTest`, `MobileControlsTest`).
3. Before distributing, replace the placeholder identifier `com.neonrequiem.game` and company name in
   **Project Settings > Player**.
4. Android is preconfigured for IL2CPP + ARM64 (required by Google Play). iOS always uses IL2CPP.
5. **Build And Run** with the device connected.

The `MobileControlsTest` scene shows a live readout of the input pipeline (move vector, look
rate, button states, touch count, DPI, safe area) for ergonomics testing on real hardware.

## Tests

| Suite | Where it runs | Count | Status |
| --- | --- | --- | --- |
| Logic tests (`Tests/EditMode/Logic`) | Unity Test Runner (EditMode) **and** plain .NET via `Tools/StaticValidation/run_logic_tests.py` | 171 cases | Executed on .NET 8: all pass |
| Project setup tests (`Tests/EditMode/Unity`) | Unity Test Runner (EditMode) | 7 | Written and compiled; not yet executed |
| Gameplay integration tests (`Tests/PlayMode`) | Unity Test Runner (PlayMode) | 11 | Written and compiled; not yet executed |

In Unity: **Window > General > Test Runner**, then run the EditMode and PlayMode tabs.

Without Unity (compile check and logic tests), see [Tools/StaticValidation/README.md](Tools/StaticValidation/README.md).

## Repository layout

```
NeonRequiem/
  Assets/_NeonRequiem/
    Scripts/Core/        NeonRequiem.Core assembly: state machine, settings, math, ring buffer, logging
    Scripts/             NeonRequiem.Runtime assembly: Input, Player, Camera, UI, World, Optimization, Bootstrap
    Editor/              Project validator and menu
    Tests/               EditMode (Logic, Unity) and PlayMode suites
    Scenes/              Bootstrap, TestScenes/MovementTest, TestScenes/MobileControlsTest
    Resources/NeonRequiem/  Assets loaded by path: input actions, movement config, template materials
    Settings/Rendering/  URP pipeline assets (three presets), renderer, volume profiles
  Documentation/         Project state, architecture, design, implementation log, test report, asset list
  Packages/              Package manifest
  ProjectSettings/       Unity project settings
  Tools/StaticValidation/  Compile and unit-test the project without launching Unity
```

## Documentation

- [PROJECT_STATE.md](Documentation/PROJECT_STATE.md) — what exists, what does not, feasibility and roadmap
- [ARCHITECTURE.md](Documentation/ARCHITECTURE.md) — assemblies, systems, data flow, rendering and performance strategy
- [GAME_DESIGN_DOCUMENT.md](Documentation/GAME_DESIGN_DOCUMENT.md) — vision, story, mechanics, combat, enemies, bosses, world
- [IMPLEMENTATION_LOG.md](Documentation/IMPLEMENTATION_LOG.md) — what was built, decisions and their reasons
- [TEST_REPORT.md](Documentation/TEST_REPORT.md) — executed tests, unexecuted tests, manual test plan, defect policy
- [ASSET_REQUIREMENTS.md](Documentation/ASSET_REQUIREMENTS.md) — placeholder inventory and the art/audio needed to replace it
