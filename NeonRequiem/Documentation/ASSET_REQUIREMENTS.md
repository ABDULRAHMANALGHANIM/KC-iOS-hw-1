# ASSET REQUIREMENTS

What is placeholder today, and what must be produced or licensed to replace it. Code cannot
generate these; they are the project's critical path. Every asset must be original or properly
licensed, and must not reproduce any existing franchise's characters, designs, logos or music.

## 1. Placeholder inventory (what the project uses now)

| Placeholder | Where | Replaces |
| --- | --- | --- |
| Kael built from capsules, cubes and spheres, animated procedurally (stride, lean, squash/stretch, dodge tuck, glow) | `PlayerVisualRig`, `PlayerFactory` | Character model, rig, animation set |
| Greybox arenas from scaled cubes; procedural grid texture; distant box "towers" | `TestArenaBuilder`, `PrimitiveFactory`, `NeonMaterialLibrary` | Modular environment kit, props, skyline, textures |
| Flat-colour URP Lit / Unlit template materials | `Resources/NeonRequiem/Materials/NR_*.mat` | Authored materials and the stylised character shader |
| Disc / ring / rounded-rectangle sprites generated at runtime; text labels on buttons | `ProceduralSprites`, `TouchControlsView` | HUD icon set, panel art |
| Unity built-in legacy font | `UIFactory.DefaultFont` | Licensed UI typeface (Latin + Arabic) |
| No audio of any kind | — | Music, SFX, ambience, voice |
| Placeholder identifier `com.neonrequiem.game`, company `NeonRequiem`, no icon or splash | `ProjectSettings.asset` | Real bundle id, app icon, splash, store art |

## 2. Requirements by phase

Budgets below are starting targets for mid-range mobile hardware and **must be revised after
device benchmarking**; they are not measured limits.

### Phase 2 — combat foundation (can still ship with placeholders)
| Asset | Notes |
| --- | --- |
| Temporary humanoid with a standard rig | Any licensed stand-in, to drive real attack timing |
| Placeholder attack animations: 4 light, heavy, launcher, parry, hit reactions, death | Timing accuracy matters more than quality |
| Hit, parry and telegraph VFX (simple) | Pooled particle prefabs |
| Stand-in SFX: swing, hit, parry, footsteps, UI | Needed to judge combat feel |

### Phase 4 — vertical slice (first real art)

**Characters**
| Asset | Spec |
| --- | --- |
| Kael Vesper | Target 25–35k triangles LOD0 with 2 LODs; one 2k texture set; 60–75 bones; facial blend shapes; emissive circuitry mask; energy blade; forearm device |
| Choir Sentinel | 10–15k triangles; shares a rig family with other humanoid enemies |
| One mini-boss | 30–40k triangles |
| Lyra (holographic) | Reuses a humanoid rig; additive/emissive material |

**Animation (Kael)**: idle + 2 variations; walk/run/sprint blend set; start/stop/turn; jump up /
apex / fall / land (light and heavy); air jump; dodge in 4 directions + backstep; air dodge;
4-hit light chain; heavy + charge; launcher; 3 aerial attacks; parry + counter; 3 ability casts
(shift, weave, fracture); hit reactions ×4 directions; knockdown/get-up; death; interact;
dialogue idles. Gameplay drives movement; clips are in-place except specific finishers.

**Environment — District Zero slice**: modular street kit (walls, floors, stairs, railings,
doors, pipes), market props, signage and holograms, rooftop kit, one landmark, skybox and
skyline cards, rain and wet-surface treatment, Echo-state variants for a subset of pieces.
Baked lighting, LODs and occlusion volumes are part of the deliverable.

**Shaders and VFX**: stylised character shader (banded lighting, rim, emissive circuitry, Echo
dissolve) with shadow and depth passes; Echo Shift full-screen transition; gravity distortion;
phantom material; hit/parry/dodge effects; rain. All with Performance-preset fallbacks.

**UI**: icon set (actions, abilities, status), HUD frames, menu backgrounds, licensed typeface with
Latin and Arabic coverage (TextMeshPro font assets), button states.

**Audio**: exploration and combat music layers for one district; one boss theme with phase
stingers; footsteps on 4 materials; blade set; ability signatures; enemy vocalisations; ambience
beds (street, interior, rain); UI sounds; voice or text-only dialogue decision.

### Phase 5 — content production
Four further districts, four enemy classes, four bosses, remaining cast, cinematics, full score,
localisation (English, Arabic), store assets.

### Phase 7 — release
App icon set (iOS and Android adaptive), splash, store screenshots per device class, feature
graphic, trailer, privacy policy text, age-rating questionnaire answers.

## 3. Technical delivery standards

- Meshes: FBX, metres, +Z forward, +Y up, applied transforms, no n-gons; colliders authored separately.
- Textures: source as PNG/TGA; ASTC on device (6×6 for most colour maps, 4×4 for UI and normal-critical maps); power-of-two; atlas where it reduces materials.
- Rigs: humanoid-compatible where possible so enemy animation can be shared; consistent bone naming.
- Animation: 30 FPS source; events for footsteps and attack windows are added in Unity from `AttackDefinition` data, not baked into clips.
- Audio: 48 kHz WAV source; Vorbis (Android) / AAC or Vorbis (iOS) on device; music as stems for adaptive layering; loudness target −16 LUFS integrated for mobile.
- Naming: `NR_<Category>_<Name>_<Variant>`; one material per mesh where possible.

## 4. Roles this requires

Character artist, environment artist, animator, technical artist (shaders, VFX, optimisation),
UI artist, composer/sound designer, narrative writer, and QA with physical test devices.
None of this work is represented in the repository yet.
