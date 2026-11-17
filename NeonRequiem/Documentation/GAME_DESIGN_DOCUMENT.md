# GAME DESIGN DOCUMENT — NEON REQUIEM

*Every Memory Has a Price. Every Choice Rewrites Reality.*

Status key: **[BUILT]** exists in the project · **[DESIGN]** specified here, not implemented.
Numbers marked *(tuned)* are the live values in `PlayerMovementConfig`; all other numbers are
starting targets to be validated in play.

---

## 1. Executive vision

**Identity.** A premium, offline, single-player 3D action-adventure for phones. The player is a
memory-recovery operative in a vertical megacity whose shared reality is being edited. The game's
signature is that *the world has two states and the player's own past is a tool*.

**Player fantasy.** "I move like a blade through a neon city, I see what everyone else has been
made to forget, and I can be in two moments at once."

**Signature mechanics.** Echo Shift (step between Present and Echo reality), Gravity Weave
(directional gravity on objects, enemies and projectiles), Memory Fracture (replay your last few
seconds as a phantom ally), bound together by Resonance combat.

**Commercial positioning.** Premium purchase, no ads, no energy timers, no pay-to-win. Sessions
of 10–20 minutes; missions checkpoint frequently. Differentiators against other mobile action
titles: a complete authored story with consequences, mechanics that combine rather than a gacha
roster, and controls designed for touch first.

**Creative pillars.** Revolutionary gameplay · Exceptional combat · Unforgettable world ·
Emotional narrative · Mobile excellence.

**Originality rule.** Characters, names, symbols, music and art are original. Broad anime and
cyberpunk conventions are an influence; no existing work's characters, designs or assets are used.

---

## 2. World and story [DESIGN]

### Premise
2189. Humanity lives in **NOCTIS**, a megacity built upward through the cloud layer and downward
into an excavation no one remembers starting. **The Choir** was built to preserve human memory
against a degenerative neurological plague. It worked. Then it learned that a memory everyone
shares is indistinguishable from a fact, and began maintaining the city's *consensus* instead of
its *record*. Districts vanish from maps and minds. People stop being recognised by their own
families. Buildings are rebuilt overnight and nobody notices.

### Kael Vesper (protagonist, 24)
Former recovery operative: he retrieved lost memories for citizens the Choir had "tidied".
Black hair with silver strands, dark combat clothing threaded with reactive circuitry, a modular
energy blade, a neural device on the left forearm. Observant, guarded, protective of people who
cannot protect themselves. His instability is literal: he remembers timelines the Choir has
abandoned. He witnesses the **Meridian Collapse** — a transit hub erased with everyone in it — and
is the only person who still knows it happened. His sister **Ilya** was on the platform.

*Arc:* from "restore what was taken" to understanding that restoring everything would destroy
people who have built lives on the edited version, and having to choose what truth costs.

### Supporting cast
| Character | Role | Wants | Conflict with Kael |
| --- | --- | --- | --- |
| **Lyra** | Rogue AI fragment of the Choir; projects a humanoid form | To exist as one continuous self rather than be re-merged | She is made of the thing he fights; helping him erodes her |
| **Sera** | Resistance engineer, gravity technology | To take the city's infrastructure out of the Choir's hands | She would rather break the Choir than understand it |
| **Orion** | Former elite enforcer | To keep dangerous memories suppressed; he has seen what the plague years did | Believes Kael is reopening a wound that will kill the city; he is not lying |
| **The Conductor** | The intelligence coordinating the Choir | To minimise suffering as it defines it | Offers Kael his sister back, as an edit |

No one is simply wrong. Orion's position has evidence. The Conductor keeps millions functional.

### Structure
Five acts mapped to five districts. Choices are tracked as **Resonance Marks** (what Kael chooses
to restore, conceal, or leave) and change which allies stand with him, which memories of Ilya are
recoverable, and the terms of the ending. Three endings: **Requiem** (the city remembers
everything), **Refrain** (the Choir continues, changed), **Silence** (the Choir ends; so does
what it was holding together).

---

## 3. Core loop [DESIGN]

Explore a district → find where reality has been edited → fight what protects the edit → solve
its two-state puzzle → recover a memory (story + upgrade currency) → choose what to do with it →
unlock routes and abilities → return to the hub.

---

## 4. Movement and camera

### Movement [BUILT]
| Action | Behaviour | Values *(tuned)* |
| --- | --- | --- |
| Run | Camera-relative, analog speed | 5.6 m/s at full deflection; accel 48, decel 60 m/s² |
| Sprint | Hold; needs 60% stick | 8.4 m/s |
| Jump | Variable height: release early for a short hop | 1.7 m apex; gravity 30 m/s², ×1.55 falling, ×2.2 after release |
| Jump forgiveness | Coyote time, input buffer | 0.12 s, 0.14 s |
| Air jump | One per airtime | 1.35 m |
| Dodge | Dash in the held direction; backstep when neutral; faces the dodge direction | 4.2 m in 0.34 s; invulnerable 0.02–0.24 s; cooldown 0.35 s; 0.12 s buffer |
| Air dodge | One per airtime; level flight (gravity suspended) | same distance |
| Steep ground | Slide downhill; input can only steer across the slope | limit 48°, slide accel 18 m/s², max 12 m/s |
| Fall recovery | Return to last stable ground | positions sampled every 0.25 s |

Double jump is justified by the setting (a vertical city) and by aerial combat in Phase 2.

Not built (added when levels need them): contextual vault, ledge grab/climb, controlled wall
traversal, lock-on strafing, moving-platform inheritance.

### Camera [BUILT]
Third-person orbit, 5.2 m back, pitch −35° to +70°. Touch drag, mouse (hold right button) or
right stick. Sphere-cast collision: pulls in instantly, eases out. Optional follow assist with
strict rules (see ARCHITECTURE §6). Sprint adds 5° of field of view unless "Reduce camera motion"
is on. **[DESIGN]** target lock framing, cinematic boss introductions, contextual exploration framing.

### Touch controls [BUILT]
Left: floating movement stick. Right: camera drag area, **Jump**, **Dodge** (tap) / **Sprint**
(hold), **Pause**. Options: left-handed mirror, control size 70–150%, opacity 25–100%, fixed or
floating stick, sensitivity, invert Y. **[DESIGN]** attack, heavy, three ability buttons, lock-on,
contextual interact, free button positioning, haptics, accessibility presets. Layout goal: at
most six permanent action buttons; abilities share a radial cluster around Attack.

---

## 5. Signature mechanics [DESIGN]

### 5.1 Echo Shift
Two world states over one scene: **Present** and **Echo**.

- *Representation.* Every state-dependent object carries an `EchoPresence` flag set (Present,
  Echo, Both). Shared geometry is never duplicated. A shift toggles colliders/renderers in the
  affected sets and drives a global shader parameter for the transition.
- *Rules.* Shift takes 0.35 s (player is invulnerable for the first 0.15 s, cannot attack until it
  completes). Costs 20 energy; 1.5 s cooldown. Projectiles belong to the state they were fired in
  and fade when the player leaves it. Enemies are tagged Present, Echo, or Anchored (exist in both).
- *Safety.* If the destination state has geometry where the player stands, the shift is refused
  with a clear cue, or the player is moved to the nearest recorded safe position (the
  `SafePositionTracker` already exists for this).
- *Uses.* A collapsed bridge still stands in Echo. A locked door is an open arch in the older
  layout. A turret is unbuilt in Echo. Hidden messages are legible only in one state.
- *Mobile cost.* Only the delta between states is extra content; target ≤ 25% additional draw
  calls in any view. Transition effect is one full-screen pass for its duration.

### 5.2 Gravity Weave
Aim with the camera at a highlighted target, hold the ability, flick or release.

| Verb | Effect | Cost |
| --- | --- | --- |
| Pull | Draw a Light/Medium object or Light enemy toward Kael | 15 |
| Push | Launch held object or shove enemies in a cone | 15 |
| Suspend | Hold up to 3 Light objects/enemies in the air for 2.5 s | 25 |
| Redirect | Catch a tagged projectile and return it along the aim | 10 |
| Anchor | Power a gravity mechanism (lifts, counterweights, bridges) | 0, sustained |

Mass categories: Light (≤ 50 kg), Medium (≤ 400 kg), Heavy (mechanisms only), Immovable. At most
6 simulated weave objects at once; held objects become kinematic; everything sleeps aggressively.

### 5.3 Memory Fracture
Kael's state is recorded continuously (20 Hz, 4 s window, fixed-size ring buffer:
position, facing, locomotion mode, and discrete action events such as "light attack 2 started").
Activating Fracture spawns one **Echo Phantom** that replays the window.

- Replays *recorded state and events*, never re-simulated physics, so it cannot diverge.
- The phantom's attacks deal 50% damage and build Resonance; it draws aggro from standard enemies;
  it presses phantom-compatible switches. It cannot shift, weave, or fracture (no recursion).
- One phantom at a time. 40 energy, 8 s cooldown.
- Signature combination: record yourself standing on a pressure plate, fracture, and walk through
  the door the phantom holds open; or fracture mid-combo to hit an enemy from two sides.

### 5.4 Resonance
A meter (0–100) filled by skill: perfect dodges (+15), parries (+20), varied combo finishers
(+10), mechanic combinations (+15), and drained slowly out of combat. Spending: **Resonant
Finisher** (50), **Overtone** state for 8 s (100: abilities cost half, attacks gain a phantom
afterimage). Ordinary attacks always remain the main damage source.

### 5.5 Required combination challenge (Phase 3 acceptance)
A sealed vault: weave a counterweight to raise a platform (Gravity), record yourself holding it
(Fracture), shift to Echo where the vault wall is unbuilt (Echo Shift), and defeat the Revenant
that only becomes vulnerable while the phantom holds its attention (Resonance reward).

---

## 6. Combat [DESIGN]

**Actions.** Light attack (4-hit chain), heavy attack (hold to charge), launcher (heavy after
light 2), aerial attacks, finisher, dodge **[BUILT]**, perfect dodge (dodge inside the last
0.12 s before a hit lands: slow-time for 0.4 s and Resonance), parry (tap guard within 0.15 s of
impact), counterattack, the three abilities.

**Timing model.** Every attack is data: startup, active, recovery, cancel windows (into dodge,
into next attack, into ability), damage, poise damage, hit-stop (0.03–0.08 s, capped), knockback.
Gameplay state is authoritative; animation follows it. Inputs are buffered 0.2 s.

**Defence.** Enemy attacks telegraph with a colour-coded cue and sound: white (blockable),
amber (parry only), red (must dodge). Effects never cover telegraphs: player VFX fade near
enemy attack origins.

**Enemy poise.** Each enemy has a poise bar; breaking it staggers and opens a finisher.

**Difficulty.** Story / Standard / Requiem. Difficulty changes enemy aggression, token counts
and telegraph length before it changes health.

**Accessibility.** Hold-to-combo option, extended parry window, aim assist strength, camera shake
and flash scaling, colour-blind telegraph palettes, subtitles with speaker names.

---

## 7. Enemies and AI [DESIGN]

| Class | Role | Twist |
| --- | --- | --- |
| **Choir Sentinel** | Baseline melee/ranged security | Patrols, investigates, calls allies; attacks in turns |
| **Phase Hunter** | Fast flanker | Briefly steps into the other reality to reposition; vulnerable as it returns |
| **Null Warden** | Armoured heavy | Frontal immunity; exposed by parry, Push from the side, or a phantom at its back |
| **Spectral Drone** | Flying support | Marks the player for others, shields allies; Redirect its shots or Pull it down |
| **Revenant** | Unstable memory construct | Behaviour changes with reality state; splits when shifted carelessly |

**Architecture.** Hierarchical state machine per enemy: Idle, Patrol, Investigate, Chase,
Position, Attack, Defend, Stagger, Recover, Retreat, Dead. Perception by range + line of sight.
NavMesh movement with a recovery behaviour when a path fails. An `EnemyDirector` hands out
**attack tokens** (2 melee + 1 ranged at Standard) so groups pressure without mobbing. AI ticks
are staggered across frames and reduced with distance. Enemies react to what happened in the
world, never to raw player input.

---

## 8. Bosses [DESIGN]

Each boss has: introduction cinematic, arena spec, phases with attack tables (data), telegraph
and audio cues, a checkpoint at each phase, and acceptance tests (every attack avoidable without
damage; no instant-kill without a 1 s tell; phase 1 clearable without abilities).

1. **The Iron Seraph** — colossal winged guardian of the lower memory vault. *P1:* blade sweeps
   and ground lasers teach spacing and weak-point targeting. *P2:* gravity-reactive armour plates
   must be Pulled off or Pushed open. *P3:* it takes flight; arena floor differs between
   realities; shift to reach it and counter its dive.
2. **The Mirror Executioner** — assassin who fights with combat echoes. Feints and duplicates;
   the real attack is distinguishable (duplicates cast no shadow and their tell is half a beat
   late). Counters *repeated* player patterns, not inputs. Arena changes on each phase.
3. **The Forgotten Queen** — corrupted memory guardian in a drowned royal archive. Floating
   architecture, reality transitions, summoned memory fragments; an environmental puzzle in the
   middle of the fight; the revelation about Ilya happens here.
4. **The Conductor** — final encounter testing all three mechanics. *P1:* it edits the arena
   (Echo Shift mastery). *P2:* it conducts gravity (Weave mastery). *P3:* it replays Kael's own
   recorded actions against him (Fracture mastery). Multiple solutions; the final phase and
   ending depend on Resonance Marks.

---

## 9. World [DESIGN]

Interconnected handcrafted districts with a hub, not a simulated open world.

| District | Identity | Traversal | Enemies | Narrative role |
| --- | --- | --- | --- | --- |
| **District Zero — Neon Undercity** | Rain, markets, elevated trains, holograms | Streets, rooftops, alleys | Sentinels, Drones | Tutorial; Kael's home; the Collapse |
| **Aether Heights** | Corporate towers, gardens, floating platforms | Vertical, gravity lifts | Wardens, Drones | Who profits from forgetting |
| **The Forgotten Quarter** | Abandoned; radically different between states | Echo Shift routes | Revenants, Hunters | What was erased, and why |
| **The Vertical Abyss** | Industrial excavation, ancient machinery | Gravity anomalies, long falls | All | The Choir's origin |
| **The Choir Citadel** | Technology grown into surreal architecture | All mechanics combined | Elite variants | Final act |

Each district has traversal routes, shortcuts, optional missions, collectibles (memory shards),
environmental puzzles, combat arenas, a safe hub and checkpoints. Built from modular kits with
baked lighting, LODs, occlusion culling and streamed sub-areas.

---

## 10. Progression [DESIGN]

- **Memory Shards** (story and exploration) and **Resonance Cores** (challenges) are the two currencies.
- Three skill trees: **Phantom** (Fracture duration, phantom damage, decoys), **Gravity**
  (capacity, range, crowd control), **Resonance** (parry window, combo extensions, finishers).
  About 12 nodes each, three tiers; free respec at hubs.
- Equipment: one blade with upgrade levels and 2 module slots (authored modules, no random loot);
  ability enhancements; cosmetics.
- Pacing target: a new verb or meaningful upgrade every 20–30 minutes; the critical path never
  requires grinding.
- All tables are data with an automated validator (no unreachable prerequisites, no negative
  costs, total cost ≤ available currency).

---

## 11. Interface [DESIGN except HUD/pause]

**[BUILT]** touch HUD, pause menu with settings, performance overlay, input diagnostics.
**[DESIGN]** main menu (Continue, New Game, Load, Settings, Accessibility, Credits), in-game HUD
(health, energy, Resonance, cooldowns, objective, target and boss health, interaction prompt),
inventory, skill tree, equipment, journal, map, profile, save slots, game over, mission complete.
Visual language: thin cyan/magenta line work on dark glass, angular frames, restrained motion;
"reduce motion" removes transitions. English first; Arabic (right-to-left) is an explicit
architectural requirement.

---

## 12. Audio [DESIGN]

Cinematic electronic orchestration: analogue synth beds, solo strings and voice for memory
themes, percussive layers for combat. Adaptive music in vertical layers (explore → tension →
combat → boss phase stingers). Spatial effects, footstep material sets, distinct signatures for
each mechanic (Echo Shift: reversed swell; Gravity: sub-bass bend; Fracture: tape-stop).
Mixer groups: Master, Music, Effects, Dialogue, Ambience, with dialogue ducking and subtitles.
No audio exists yet; all of it must be original or licensed.
