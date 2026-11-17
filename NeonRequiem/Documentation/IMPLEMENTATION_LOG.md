# IMPLEMENTATION LOG

Chronological record of what was built and why. Newest entry last.

---

## 2026-11-16 / 17 — Phase 0 and Phase 1

### Environment constraints (these shaped the approach)

- The work was done in a Linux container with **no Unity Editor licence**: the editor could not be
  launched, so nothing could be imported, played or built.
- Unity's own download hosts were not reachable. The public GameCI editor container image for
  **Unity 6000.3.28f1** was reachable, and was used *only as a source of files*: the engine and
  editor managed assemblies, the built-in package sources (uGUI 2.0.0, URP 17.3.0, Test Framework),
  the official "3D URP" project template, and the template's precompiled package assemblies
  (Input System, uGUI, URP, Test Runner). Nothing from it is committed to this repository.
- Input System source at tag 1.20.1 was read from its public GitHub repository to confirm behaviour.

Consequence: every claim in this project is one of three kinds, and the documents keep them apart —
**compiled** (Roslyn against real Unity assemblies), **executed** (unit tests on .NET 8),
or **unverified** (needs the editor or a device).

### Decisions

| # | Decision | Reason |
| --- | --- | --- |
| D1 | Target Unity **6000.3 LTS** rather than 6000.0 | 6000.3 is the supported LTS stream in November 2026; package versions were taken from that editor's own manifest |
| D2 | Start from the **official URP template's** project settings and pipeline assets, edited by script | The safest available way to get a correctly configured URP project without the editor; fields changed are listed below |
| D3 | Three quality levels with one URP asset each = the three graphics presets | Standard Unity mechanism; switching needs no custom rendering code |
| D4 | Build the environment, character and UI **from code** for this milestone | No authored prefabs can be verified without the editor; code can be compiled. All of it is flagged as placeholder |
| D5 | Scenes are minimal YAML derived from the template's sample scene: camera, light, one installer | Keeps hand-authored serialized data small; everything else is compiled code |
| D6 | Gameplay rules in engine-free classes (`PlayerLocomotion`, `OrbitCameraModel`, policies) behind small interfaces | Lets the rules be unit-tested and executed without Unity; also the right structure for combat later |
| D7 | Custom touch widgets on uGUI events instead of the Input System's on-screen controls | Needed floating stick, hold-to-sprint, per-pointer tracking, layout mirroring and scaling under our control |
| D8 | No service locator; one composition root (`GameRuntime`) with explicit `Initialize` calls | Predictable initialisation order and visible dependencies |
| D9 | Template materials in `Resources`, instantiated at runtime | `Shader.Find` on unreferenced shaders fails in device builds |
| D10 | Fog enabled in the scene files, not only from script | Unity strips fog shader variants that no built scene uses |
| D11 | Dodge and sprint share one touch button (tap / hold) | Fewer permanent buttons on a small screen |
| D12 | Adaptive resolution steps down on p95 over budget and probes upward with back-off | Frame pacing hides GPU headroom from delta time; avoids oscillation |
| D13 | Settings are the only PlayerPrefs data; progression will use versioned files | Directive requirement; PlayerPrefs is unsuitable for critical data |
| D14 | Addressables, Localization, AI Navigation, Adaptive Performance not installed yet | No unused dependencies; each arrives with its first real use |
| D15 | No custom cel-shading shader in this milestone | A hand-written shader cannot be compiled here; an unverified shader in the foundation risks magenta materials on first open. Scheduled for Phase 4 with the character art |
| D16 | No haptics, no audio system yet | Both need device verification (and a native iOS plugin for haptics); not required by the Phase 1 acceptance criterion |
| D17 | Committed `.meta` files with deterministic GUIDs | Scenes and assets reference scripts by GUID; stable GUIDs make those references valid on first import |

### Changes made to the Unity template's settings

`ProjectSettings.asset`: company and product name, application identifier
`com.neonrequiem.game` (placeholder), landscape-only auto-rotation, Android ARM64 + IL2CPP,
new product GUID. The template already had the new Input System backend and linear colour space.
`QualitySettings.asset`: levels Performance / Balanced / Quality (from the template's Mobile
level), per-platform defaults. `GraphicsSettings.asset`: default pipeline = Balanced.
`TagManager.asset`: layer 6 named `Player`. `EditorBuildSettings.asset`: the three scenes.
URP assets: render scale, MSAA, HDR, shadow resolution/distance/cascades, soft shadows,
per-object light limit per preset; volume profile bloom/vignette tuned.

### Build order (dependency order)

1. Project skeleton, package manifest, settings, URP assets
2. `NeonRequiem.Core`: logging → state machine → GameManager → SceneFlow → settings → ring buffer →
   frame-time tracker → adaptive policy → math → localization
3. Static validation harness (compile against Unity without the editor)
4. `NeonRequiem.Runtime`: input types → look math → actions source → router → UI factory/sprites →
   touch widgets → touch layout → movement tuning → locomotion → motor → controller → materials →
   placeholder rig → factory → camera model → camera controller → performance manager → HUD views →
   HUD controller → arena builder → fall recovery → GameRuntime → GameBootstrap → scene installer
5. Authored assets: input actions, materials, movement config, scenes, build settings, metas
6. Editor tools: project validator, menu
7. Tests: logic (executed), project setup and PlayMode (compiled)
8. Documentation

### Defects found and fixed during the milestone

| Defect | Found by | Fix |
| --- | --- | --- |
| On ground steeper than the slope limit the character crept at 0.3 m/s instead of sliding: the idle deceleration cancelled the slide acceleration every frame | Unit test `SteepGround_SlidesDownhill_AndRefusesUphillInput` | Velocity is split into a downhill component owned by gravity and an across-slope component steered by input |
| Fog would have been missing in device builds (enabled only from script) | Design review | Fog enabled in scene `RenderSettings` |
| Runtime-created materials would have had no shader in device builds | Design review | Template materials in `Resources` |
| Invisible touch zones might not be hit-testable if the canvas never registers a mesh-less graphic | Reading uGUI's `GraphicRaycaster` source (`depth == -1` is skipped) | Zones emit one transparent unit quad |
| Device builds strip engine classes nothing references; `GameObject.CreatePrimitive` needs `MeshFilter`, `MeshRenderer` and the collider types at runtime, so arena geometry could have lost its colliders on a phone | Design review (Unity's documented `CreatePrimitive` caveat) | Typed references in `PrimitiveFactory` plus `Assets/_NeonRequiem/link.xml`; type/assembly names checked against the 6000.3 assemblies |
| `Object.Destroy` is an error in edit mode (EditMode tests dispose an Input Actions copy) | Design review | `ObjectUtility.SafeDestroy` |
| PlayMode tests would have written the developer's real PlayerPrefs | Design review | `GameRuntime.SettingsStoreOverride` test seam |

### Verification performed

See TEST_REPORT.md. Summary: 0 compile errors and 0 warnings across 5 assemblies in 3
configurations; 171 of 171 logic test cases pass on .NET 8; all authored YAML parses and every
GUID and local file reference resolves. Nothing was run inside Unity.

### Exact continuation point

Human verification in Unity (PROJECT_STATE §1). Then Phase 2, step 1: `Health`, `DamageInfo`,
`DamageResolver`, hitbox/hurtbox layers (PROJECT_STATE §6).
