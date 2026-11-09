# Phase 6 – Beyond Sol: Procedural Systems, Galaxy Layer, Multiplayer

**Status:** Proposed (2026-11-09). Not started. Does not block Phase 5 or the Early Access launch.
**Goal:** Turn a five-world campaign into an open-ended game: generated star systems to conquer,
a galaxy layer that ties colonies together, rival overseers, and multiplayer.

The non-negotiables in the [overview](00_ROADMAP_OVERVIEW.md) all still hold. In particular,
multiplayer never adds direct unit control: overseers compete and cooperate through buildings,
flags and bounties only.

Durations are rough estimates for one developer working with AI assistance. Treat them as
relative sizes, not dates.

---

## Where the code stands today

| Fact | Consequence |
|---|---|
| Fixed 20 Hz `SimClock`, versioned full-world `SaveGame` with `simSteps` | Good base for an authoritative host and for join-in-progress |
| `CelestialBodyProfile` is data-only; `BodySeed` gives same body + seed → same world | Procedural worlds fit the existing generator |
| Steamworks.NET is already a dependency | Lobbies, invites and Steam Networking Sockets need no new SDK |
| The player only places buildings, posts flags and sets bounties | The multiplayer command stream is tiny: no unit micro to replicate |
| `GameLoop.cs` is 7,892 lines and owns the run; `SimClock` is used nowhere else | The simulation cannot run without the scene, a second colony or a remote player |
| 84 static classes under `Systems/` | Much state is global: one colony, one overseer per process |
| `CelestialBodyId` is a five-value enum with 304 references in 46 non-test files; `PlanetArchitecture` has one hand-written file per world | A sixth world is a code change today, not a data change |
| Agents are MonoBehaviours on Unity NavMesh with float math; 25 `UnityEngine.Random` call sites in `Runtime/` | The sim is not bit-deterministic across machines, which rules out lockstep networking |
| No rival-overseer system exists | Needed for single-player depth and as the stand-in for absent human players |

---

## Order and reasoning

1. **6A Foundation** first: every later sub-phase depends on it.
2. **6B Procedural systems** next: the largest content gain for the least new architecture.
3. **6C Galaxy layer and rivals**: gives generated worlds a reason to exist, and builds the AI
   overseer that multiplayer reuses.
4. **6D Multiplayer** last: the most expensive and the riskiest, and it needs 6A and 6C.

Ship Early Access and run the external playtest before starting. 6A rewrites the core of
`GameLoop`, which is the wrong change to make during a release candidate.

---

## 6A – Foundation refactor (6–10 weeks)

No player-visible features. This is what makes the rest possible.

- **Command queue.** Every overseer action (place, cancel, post flag, set bounty, research, buy,
  orbital power, speed change) becomes a serializable `OverseerCommand` applied at a tick. The HUD
  emits commands and never mutates state. This one change serves replays, AI overseers and network play.
- **`ColonySim` out of `GameLoop`.** Move run state and the tick into a pure class under `Systems/`.
  `GameLoop` becomes the presenter that spawns views and feeds frame time.
- **Instance state, not statics.** A `ColonyState` owns resources, flags, research, threat, roster
  and stats. Audit the 84 static classes; convert the ones that hold mutable per-run state.
  Each state belongs to an `OverseerId`, so two overseers can exist in one world.
- **One seeded RNG.** A `SimRng` stream saved with the world replaces `UnityEngine.Random` in sim
  paths (`KingdomThreat`, the 25 runtime sites). Cosmetic randomness keeps its own stream.
- **Body ids become data.** Replace the `CelestialBodyId` enum with a string id plus a profile
  registry. This also closes the open checklist item on ScriptableObject catalogs.
  Saves migrate by mapping the old integers to the five Sol ids.
- **Architecture by trait.** `PlanetArchitecture.Style` and `Adapt` key on environment traits
  (atmosphere, gravity, temperature, radiation) instead of a world id. The five Sol sets become
  the first five trait presets, so Mars stays exactly as tuned.

**Exit:** a headless test runs seed + command log for 10 minutes of sim time twice and gets the
same state hash; a second `ColonyState` can run in the same process; all existing tests pass and
old saves load.

### 6A status (2026-11-09, branch `claude/phase-6a-foundation`)

Done, with 906 EditMode tests and the isolated PlayMode suite passing:

- `OverseerCommandQueue`: all fourteen colony-changing player actions are commands, drained before the simulation steps. `-recordcommands` writes the log to the logs folder.
- `ColonySim`: owns the clock, the seeded `SimRng`, robot ids, the queue and the pure systems. `GameLoop` supplies the scene through `IColonySimHost`. A sim runs headless, replays a log and hashes its state.
- `OverseerSession`: one `ColonySim` per overseer in one process, commands routed by overseer id.
- Worlds as data: every world has a key; a registered `CelestialBodyProfile` boots, plays and saves under its own key and seed, borrowing authored content from the Sol world its `Id` names. `WorldDatabase` registers worlds from an asset.
- Architecture is chosen from `EnvironmentTraits` through `ArchPreset`.
- `TechCatalog` reads a `TechDatabase` asset when one ships.
- Robots decide on the fixed step (`SpecialistAgent.SimTick`); simulation rolls draw from `SimRng`.

Still open:

- Robot and fauna movement and work run per frame, so a replay reproduces the scene-free state and robot decisions' order, not positions or work totals. Closing this needs fixed-step movement with render interpolation.
- The 200-odd branches on `CelestialBodyId` remain. A registered world takes its dressing, sky, music and narrative from the Sol world it borrows; those become data in 6B as the generator needs them.
- A registered world shares the borrowed world's travel snapshot file (`world<N>.json`) and campus and roster prefs. Key those by world key before the two can be played in one campaign.
- Headless command execution covers six of fourteen kinds; the rest act on robots or structures.
- `ReplayRules`, the robot roster and `BodySeed` are still process-wide statics. The sim carries a `RunRules` snapshot, but the game reads the statics.
- `ShopCatalog` and the Sol world profiles are still code. No database assets are committed.
- A command log from a session that began with Continue starts from loaded state and cannot be replayed from the seed alone.

## 6B – Procedural star systems (8–12 weeks)

- **`StarSystemGenerator`** (pure, seeded): star class → 3–9 bodies with orbit, size and type.
  Star class drives sun colour and intensity; orbit drives temperature and power yield.
- **World archetypes** generate a full `CelestialBodyProfile`: rocky desert, ice shell, asteroid
  cluster, volcanic, ocean archipelago, tidally locked, gas-giant moon, garden world. Each sets
  palette, sky, fog, resource weights, yields, gravity (move speed), radiation and threat.
- **New terrain kits** beside `Standard`, `AsteroidField` and `IceCrust`: volcanic (lava
  channels as hazards), archipelago (water limits building), dune sea.
- **World modifiers** as the replay driver: dust storms, flares, quakes, rich veins, ancient
  ruins, hostile hives. Two or three per world, shown in the briefing.
- **Fauna from parts.** `ProceduralLegs` already builds walkers; add a seeded fauna assembler
  (body plan, size, behaviour weights) so each system has its own threats.
- **Names and briefings** from localizable templates. The local AI pack may add flavour text
  when it is on, never rules text.
- **Solvability testing.** A scripted bot overseer (built on the command queue and `-benchmark`
  mode) plays 1,000 generated worlds in CI. Any seed it cannot win, or that throws, fails the build.
- **Placement.** Sol stays the hand-authored campaign. Conquering Europa unlocks the interstellar
  launch and a "Beyond Sol" mode; skirmish on a typed seed is available from the title screen.

**Exit:** 1,000-seed soak with zero exceptions and a bot win rate inside the tuning band; ten
generated worlds reviewed by eye for readability; shareable seed strings reproduce a system exactly.

## 6C – Galaxy layer and rival overseers (8–12 weeks)

- **Star map.** A generated sector of 20–60 systems with jump lanes, extending the existing
  solar-system title view. Scouting reveals a system's bodies and modifiers before you commit.
- **Persistent colonies.** Conquered worlds keep producing while you are elsewhere, run by a
  cheap abstract model (income, upkeep, threat level), not the full sim. Neglected colonies
  degrade and call for help.
- **Interstellar logistics.** Freight routes between colonies build on the trade-route design
  (`W2_TRADE_ROUTES_AND_FINDS.md`). Some resources exist only on some archetypes, so expansion has a purpose.
- **Deeper tech.** Per-system discoveries and secret projects that need resources from several worlds.
- **Carried heroes.** Named specialists and their service records travel with you; losing a
  veteran matters across the whole run.
- **Rival overseers.** AI colonies on the same body that post their own bounties and bid for the
  same free-agent heroes, the Majesty "rival kingdom" idea. Personalities come from the existing doctrines.
- **Run structure.** Sandbox galaxy, plus a seeded roguelite run (fixed sector, escalating
  threat, Ironman) with a score and leaderboard.

**Exit:** a 6–10 hour galaxy run is completable; rivals win a fair share of bot-vs-bot matches;
the external playtest protocol is re-run on this mode.

## 6D – Multiplayer (12–20 weeks, after a 2-week spike)

**Model: host-authoritative, not lockstep.** The host runs the one true `ColonySim`. Clients send
`OverseerCommand`s and receive state. Lockstep would need deterministic pathfinding and fixed-point
math in place of NavMesh and floats, which is a rewrite of the agents for no gameplay gain.

- **Transport:** Steam lobbies and invites plus Steam Networking Sockets through the
  Steamworks.NET package already in the project. Relay handles NAT; no dedicated servers.
- **Replication:** economy, flags and buildings as reliable events; agent and fauna transforms
  as ~10 Hz snapshots, interpolated on clients. Clients run views only, no brains.
- **Join and rejoin:** the host serializes a `SaveGame` and sends it; the client loads it and
  starts receiving deltas. The same path gives host-migration-by-reload and saved co-op sessions.
- **Speed and pause:** game speed is shared. Any player may hold; raising speed needs all players.
- **Local AI:** hero lines are generated on the host and sent as text through `AiContentGuard`.
  Player-to-player chat uses Steam's own chat filtering.

**Stages**

1. **Spike (2 weeks):** two instances, one colony, placing buildings and posting flags over Steam
   relay. Decide go or no-go from the measured bandwidth and feel.
2. **Co-op (2–4 players):** shared world, separate campuses and purses, shared threat. Lowest
   design risk and the best fit for the audience.
3. **Competitive (2–4 players):** rival campuses bidding for the same heroes, sabotage through
   bounties on enemy structures, race to the population goal. AI rivals from 6C fill empty seats.
4. **Stretch, decide later:** asynchronous shared galaxy (each player's conquered systems appear
   on friends' star maps). Needs a backend service, so it is out of scope until the rest has shipped.

**Exit:** a 90-minute four-player session over the internet with no desync reports, a mid-game
rejoin, and under 50 kB/s per client; Steam Deck as a client.

---

## Risks

| Risk | Mitigation |
|---|---|
| 6A regresses a game that is about to ship | Start after EA launch; land it in slices behind the existing 775+ tests; add the replay-hash test first |
| Generated worlds feel samey | Archetypes × modifiers × fauna assembly; eyeball review is part of the 6B exit |
| Generated worlds are unwinnable or broken | The 1,000-seed bot soak in CI |
| Multiplayer cost balloons | The 2-week spike is a hard gate; co-op ships before competitive is started |
| Indirect control is dull to watch with two players | The bidding war for heroes is the multiplayer hook; prototype it in the spike |
| Solo scope: 34–54 weeks in total | Each sub-phase ships as its own Early Access update and is useful without the next |

## Decisions needed

1. Does Phase 6 start after the Early Access launch (recommended) or before it?
2. Is co-op or competitive the first multiplayer target? (Recommended: co-op.)
3. Does "Beyond Sol" replace the endless mode after Europa, or sit beside it?
