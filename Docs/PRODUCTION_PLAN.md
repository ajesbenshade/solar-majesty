# Solar Majesty — plan to production

September 21, 2026. Based on the [production review](../Docs/PRODUCTION_REVIEW_2026-09-21.md). This is the proposed delivery plan; it does not declare existing roadmap phases complete.

## Product contract

**Promise:** govern autonomous robot guilds, fund the work they choose to do, keep human colonies alive, and establish a connected foothold from Earth to Europa.

**Confirmed first-release scope:** Earth → Luna → Mars → Belt → Europa. **Planning assumptions:** single-player, offline-capable, keyboard/mouse desktop game; Windows is the primary commercial target, macOS remains a development/playtest target until it passes a separate release matrix; Steam is the candidate storefront. These platform and staffing assumptions have not been confirmed by the owner.

The broader solar-system ambition remains the expansion direction. It should not hold up proving the current campaign. Do not market the five destinations as every planet. A later expansion can add the inner-system heat/solar economy and then outer-system moon/orbital logistics, each after establishing a distinct reason to play it.

### Preserve

- Indirect control: buildings, policies, research and bounties influence robots; the player does not issue movement or attack orders that bypass the brain.
- Memorable autonomous specialists: preferences, purse, gear, fear, work, rest and recovery have visible consequences.
- Humans remain colony residents; robots perform outdoor work.
- The current spaced-campus visual direction and smooth Commons dome.
- Small, understandable strategic choices that accumulate across a campaign.

### Scope boundaries for version 1.0

Keep the existing five bodies and ten robot classes only where their roles are distinct and understandable. Use existing guilds, shops, doctrines and challenges selectively. Target a first campaign of roughly **4–6 hours** as a design hypothesis to validate, with saves and useful stopping points; do not pad it to hit a duration.

Defer multiplayer, direct fleet combat, real-time orbital mechanics, a fully simulated economy on every inactive world, live LLM decision-making, new planet families, mod tools and console/controller commitments. Additional systems require evidence that they solve a tested player problem. Do not add them simply because the genre could support them.

## The loop to prove

1. See a colony need or opportunity: power deficit, unsafe deposit, wounded robot, new launch route.
2. Invest in the enabling building or research and offer a clear bounty.
3. See which specialist considers it, why they refuse/accept, and what competing need matters.
4. Adjust the incentive, placement, equipment or support network.
5. Watch autonomous action resolve a real problem and pay a legible reward.
6. Reinvest in a stronger colony and carry a deliberate advantage into the next world.

Every stage needs visible feedback. A floating “tempted” label alone is insufficient. Show intent → travel → work/combat → outcome → payment. Refusal should be understandable even when the player's best response is patience.

## A distinct purpose for each world

**Earth — learn persuasion and establish a launch program.** A prepared Commons/airlock/HAB reduces setup friction. The first workshop and real construction/defense job teach autonomy. Introduce survival and research after that payoff. Target: the player understands the loop within 10 minutes and can finish the opening without exact-bounty coaching.

**Luna — operate under constrained logistics.** Use existing yield, distance and resupply systems to make local harvesting versus imported support a meaningful choice. A forward outpost should solve a visible bottleneck. Carry research and a chosen campaign benefit forward, while local mission state remains local.

**Mars — establish a resilient settlement.** Combine ecology pressure, housing and power so expansion needs a defense/support plan. Make at least two approaches viable, such as protective infrastructure versus extraction and rapid launch. This is the main presentation benchmark after Earth.

**Belt — secure valuable but scattered production.** Emphasize ore richness, thin agriculture and travel/haul exposure. The mission objective should reward securing a supply corridor or selected deposits using the existing flag/outpost vocabulary. Do not invent a separate space-combat game for this chapter.

**Europa — master survival and finish the expedition.** Power and exposure constrain remote operations; protecting a supplied work route becomes the culmination. Build a specific final project or multi-stage operation from proven systems, followed by a clear conclusion showing the colonies and robots that got there.

These are proposed mission briefs, not claims that all these experiences already exist. Before content lock, each body needs one characteristic dilemma, a readable objective sequence, one memorable escalation and a clear completion reward.

## Milestones and evidence gates

Durations below are **provisional elapsed working-week ranges** for one experienced full-time Unity developer with part-time art/UI and recurring QA support. They are not a quote or a promised date. Sequential ranges total **20–31 weeks**; reserve roughly 25% for discoveries, yielding **25–39 weeks**. A solo part-time schedule must be replanned from actual available hours. Re-estimate after the first two milestones; there is no credible calendar commitment until then.

### M0 — stabilize the review baseline (2–3 weeks)

**Implementation underway:** [2026-09-22 stabilization checkpoint](STABILIZATION_PROGRESS_2026-09-22.md). Core repairs and automated campaign smoke coverage are implemented. [The second persistence pass](PERSISTENCE_PROGRESS_2026-09-22.md) extends coverage to developed colonies, per-robot veteran records, den ownership and migration/recovery. M0 remains open pending the remaining evidence gates.

**Owner:** engineering lead. **Depends on:** a captured snapshot of current work.

- Record baseline commit/content IDs and isolate tests from developer saves/preferences and uncommitted settings.
- Align local/CI Unity versions and produce a repeatable primary-platform build.
- Fix review F01–F05: campaign/save source of truth, research restoration, delayed-reader tutorial, Commons bootstrap and duplicate timers. Stage the deeper save refactor behind the smallest safe reproduction fixes.
- Repair all nine failing EditMode tests against the accepted art rules; distinguish obsolete expectations from real defects.
- Add PlayMode regressions for New Game, delayed tutorial input, travel, retry, Continue and correct per-body mission state.
- Correct telemetry JSON and attach exact build identity to logs.

**Exit:** all 279 existing tests pass or an intentionally retired expectation is documented with replacement coverage; the new critical regressions pass; two clean machines can boot the same tagged player; no known reproducible save-loss or campaign-state mixing remains in the tested paths. No art expansion during this milestone.

### M1 — prove the Earth experience (3–5 weeks)

**Owners:** design lead + engineering; QA conducts sessions. **Depends on:** M0.

- Teach a meaningful work outcome, not a forced 70-to-90 trick. Handle immediate, delayed, high-bounty, cancelled and resumed paths.
- Highlight valid docking and give clear placement/refusal reasons. Make pause, speed, flag cancellation, selection and bounty adjustment discoverable.
- Standardize CRED/ICE/PWR/REG language; distinguish public stockpile, personal purse and escrow.
- Define failure and recovery: a poor early purchase or one downed robot must have a visible route forward or a clear retry.
- Run an initial **5–8 unfamiliar-player** round with no spoken coaching, then fix the top observed blockers and repeat with fresh participants. Use the existing playtest protocol, revised to match the actual tutorial.

**Proposed exit thresholds:** at least 6 of 8 participants complete the core opening unassisted; at least 6 of 8 can explain why a robot accepts/refuses work; no repeated unexplained stall over two minutes; at least 5 of 8 elect to continue after the scheduled opening. These are small-sample decision gates, not population estimates or marketing claims. Record exact denominators and observed behavior.

If the gate fails, revise teaching or economy before adding a world. A full Earth completion should also be observed with at least three unassisted players before declaring the Earth chapter finished.

### M2 — make a trustworthy campaign (4–6 weeks)

**Owner:** engineering, with design defining persistence. **Depends on:** M0; content tuning uses M1 results.

- Define one versioned `CampaignSave` with per-world snapshots and stable hero/building/node IDs. Store body, seed, roster/gear/XP, research, construction, health, bounty escrow, party membership, mission state and campaign mode together.
- Separate commands for new campaign, resume, travel, revisit, retry and new seeded run. Make their persistence behavior explicit.
- Migrate legacy saves once; validate ranges and references; refuse incompatible future versions safely. Use atomic writes plus a last-known-good backup and visible error handling.
- Expose manual slots and rotating autosaves in the actual UI. Do not present helper methods as a finished save feature.
- Preserve hero identity and a small, understandable campaign carryover. Prefer a static supply benefit or selected expedition package from secured colonies over simulating all offscreen worlds.
- Extract save/session/simulation orchestration from `GameLoop` in bounded steps. Keep existing brain scoring stable.

**Exit:** complete Earth → Europa → revisit → quit/relaunch → resume without state leakage; reload a prior manual slot after later construction and research; recover from a truncated file; migrate supported old saves; validate disk-full/write-failure handling without overwriting the last good slot. Demonstrate each on the primary shipped platform. Define and test a save compatibility policy before public release.

### M3 — make the five worlds worth playing (5–8 weeks)

**Owners:** design/content, art support, engineering. **Depends on:** M1–M2.

- Implement the five mission briefs above using existing mechanics wherever possible. Create a content worksheet with objective, dependencies, enemy composition, starting resources, expected pressure and recovery routes for each body.
- Introduce classes/guilds incrementally. Require a distinct purpose for every class and building; consolidate confusing duplicates before adding variants.
- Balance public currency sources/sinks, hero income/spending, escrow refunds, tax delivery, revive costs, production and research pacing. Record curves rather than tuning from a screenshot.
- Validate at least two successful approaches per body; measure idle waiting, failures, travel cost and choice usage across a documented seed set.
- Give each world one distinctive visual/audio identity using the approved asset language. Lock a small hero-asset list and a clear finale.
- Test whether players remember individual robots and can name the tradeoff that changed on the next world.

**Exit:** every world has two demonstrated viable approaches on the chosen test seeds; no known unavoidable progression wall; three complete internal campaigns and at least five external full-campaign completions, with documented difficulty and timing. These counts are an initial evidence floor, not proof of universal balance. Archive logs and recordings with build IDs.

### M4 — presentation, accessibility and performance (4–6 weeks)

**Owners:** UI/art, engineering and QA. **Depends on:** stable M3 content; asset work may prepare earlier, but acceptance remains sequential.

- Replace the remaining prototype HUD panels in bounded task-oriented passes. Establish typography, icons, safe areas, tooltips, input focus and scaling across supported resolutions.
- Route all exposed actions through bindings and update hints from those bindings. Test non-US keyboard layouts, focus loss, modal blocking and rebinding recovery.
- Verify reduced motion, non-color status cues, text scale, audio channels and subtitles/event equivalents. Translate only languages explicitly funded and tested; otherwise ship an honest English-only scope with extracted strings.
- Complete Earth/Mars visual acceptance against current locks, then run the same checklist on Luna/Belt/Europa. Judge moving gameplay, camera range and threat readability as well as stills.
- Profile a representative maximum colony and dense encounter in a player build. Define actual minimum hardware, quality tiers and entity caps from evidence. Treat the older “200 agents” aspiration as a separate stress fixture unless that count becomes supported gameplay.
- Fix measured allocation, draw-call, pathing and load-time hotspots. Audit authored asset imports, LODs, materials, clipping and missing-shader fallbacks.
- Listen through full sessions for repetitive audio, buried warnings and abrupt transitions.

**Proposed exit budgets:** 60 fps target at 1080p recommended settings; p95 frame time ≤16.7 ms and p99 ≤33.3 ms in a documented steady-play capture; no unexplained >100 ms gameplay hitch; no progressive memory growth across ten world transitions. These are goals, not current measurements. Minimum-tier fallback, loading thresholds and memory ceiling must be set after the first profile and recorded with exact hardware/settings.

The existing Phase 4 visual exit remains open until its actual criteria pass. Do not declare it complete just because this milestone's schedule expires.

### M5 — release candidate and launch preparation (2–3 weeks, plus platform review lead time)

**Owners:** release owner, QA, art/marketing. **Depends on:** M0–M4 exits and content freeze.

- Build from a release tag; embed version/commit/content IDs; verify packaged install, launch, pause, save/load, offline play, uninstall/reinstall and upgrades on supported clean machines.
- Remove or gate cheats, capture controls and developer diagnostics from normal release input. Retain diagnostic logs with useful build IDs.
- Finish credits and asset provenance records, original public branding, required content surveys and any generated-content disclosures. Review shipped content rather than assuming every concept image ships.
- Prepare an accurate store description, capsule art, gameplay screenshots, trailer, system requirements and support/contact information. Do not use concept renders as evidence of gameplay quality.
- Complete the storefront build/configuration checklists; leave review and resubmission time. Steam currently requires store/build approval and a Coming Soon page live for at least two weeks before release. [Steam release process](https://partner.steamgames.com/doc/store/releasing?l=english).
- Use a controlled external beta. Steam Playtest offers a separate linked app for this purpose; account setup remains outside this review. [Steam Playtest](https://partner.steamgames.com/doc/features/playtest?l=english).
- Validate rollback to the previous build without silently breaking supported saves. Assign launch monitoring, crash triage, backup and hotfix ownership; rehearse one update.

**Exit:** no open blocker, crash, data-loss, progression-stopper or misleading feature claim; all committed platform/quality checks pass; 20+ aggregate hours of release-candidate testing spanning the whole campaign and save/upgrade paths; remaining minor issues have explicit owner and disposition. Store requirements must also pass. The hours are a minimum exposure target, not a guarantee of quality.

Early Access is optional after M3 plus essential M4 reliability/usability work, only if the sold content stands on its own and its limits are explicit. It should not be used to finance fixing known save corruption or an incomprehensible opening.

## First four weeks: concrete work order

**Week 1:** capture the dirty-tree baseline; align engine pins; reproduce F01–F05; fix destructive restoration and duplicate ticking first; write focused transition/tutorial tests; repair forbidden EditMode destruction and triage the remaining visual assertions. Deliver a testable build and exact known-issue list.

**Week 2:** finish snapshot/world identity and retry/Continue fixes, then the missing Commons and delayed-reader lesson. Fix telemetry formatting and build identification. Run clean-machine Earth boot, tutorial, save/quit/resume and first Luna arrival. Ship the private test package only once those paths pass.

**Week 3:** observe 5–8 unfamiliar players, gather local logs and the exact moments they hesitate/stop. Correct repeated placement, bounty, resource or objective confusion. Use recordings and behavior counts, not general impressions.

**Week 4:** retest with fresh players; document results against M1 thresholds; choose the smallest remaining Earth fixes. Re-estimate campaign/persistence/UI work using actual throughput. If M0 ran long, shift the sessions accordingly; do not bypass the safety gate to preserve a calendar.

## Prioritized backlog

Each item is a deliverable with its own acceptance evidence. Owner labels are roles, not assumed hired people. Sizes are rough focused-person effort bands: S ≤2 days, M 3–5 days, L 1–2 weeks, XL split before scheduling. They are not additive to the milestone estimates.

- **P01 / now / engineering / L:** unify save world identity and transition routing (F01). Pass travel/revisit/old-slot matrix. Dependency: captured baseline.
- **P02 / now / engineering / S:** restore hidden research independently of demo visibility (F02). Pass upgrade and Continue regression.
- **P03 / now / design + engineering / M:** make tutorial resilient to timing and genuine acceptance (F03). Pass delayed-reader and cancellation cases.
- **P04 / now / engineering / S:** repair starting Commons fallback (F04). Pass fresh destination and revisit cases.
- **P05 / now / engineering / M:** give timers one simulation owner (F05). Compare outcomes at multiple frame rates/speeds.
- **P06 / now / engineering + art / M:** repair nine test failures and reconcile accepted art expectations. No blanket ignore/suppression.
- **P07 / now / build owner / M:** align editor pins and add player/PlayMode gates. Produce traceable artifacts from one revision.
- **P08 / now / engineering / S:** invariant telemetry serialization and build IDs (F07). Parse logs in three locales.
- **P09 / next / design + UI / M:** simplify placement, bounty feedback and resource terms. Unassisted task observation.
- **P10 / next / QA + design / M:** two Earth playtest rounds with raw observations and ranked friction. Dependency: stable player.
- **P11 / campaign / engineering / XL:** self-contained campaign snapshots, stable IDs, migration and recovery. Dependency: P01.
- **P12 / campaign / UI + engineering / M:** real save/load menu, rotating autosaves and error states. Dependency: P11.
- **P13 / campaign / design / M:** write travel/retry/death/hero continuity contract. Validate with concrete examples.
- **P14 / campaign / design + engineering / L:** implement visible hero identity/progression and bounded carryover. Dependency: P11/P13.
- **P15 / content / design / XL:** implement and tune the five destination briefs. Dependency: Earth learning gate.
- **P16 / content / design + QA / L:** source/sink and strategy balance review across fixed seeds. Two demonstrated approaches per body.
- **P17 / content / narrative + audio / M:** campaign payoffs, arrival escalation and Europa ending. Dependency: mission briefs.
- **P18 / quality / UI + engineering / XL:** migrate HUD task panels and validate resolution/scaling/input focus.
- **P19 / quality / engineering + QA / M:** wire remapping and accessibility behavior. Real runtime actions and hints must change.
- **P20 / quality / art + engineering / XL:** finish asset/look checklist and existing Phase 4 exit in moving gameplay.
- **P21 / quality / engineering / L:** hardware profiling and measured optimization. Dependency: representative complete colony.
- **P22 / release / release owner / M:** provenance, credits, public naming and content survey. Inventory required before claims.
- **P23 / release / QA + build owner / L:** platform install/upgrade/soak matrix and rollback rehearsal.
- **P24 / release / owner + marketing / L:** honest store assets, private beta, release/support plan. Dependency: representative approved gameplay.

## Verification matrix

**Session paths:** fresh profile; legacy profile; New Game confirmation; every body first arrival and revisit; pause/settings/title; quit while building; Continue after defeat/victory; retry versus reseed; demo/full-campaign toggle.

**Save integrity:** active research; queued/partial construction; downed/dead/equipped heroes; multiple flags and escrow cancellation; depleted deposits; cleared/scouted dens; parties; levy purses; armed defenses; oldest supported version; future-version rejection; corrupt/missing/truncated file; write failure; manual-slot restore after later autosaves.

**Gameplay:** player spends poorly; no eligible specialist; low/high/far/dangerous bounty; unreachable destination; navigation after placement/destruction; no money; no ICE; no power; loss of a key workshop; freight shortage; all victory gates and final ending.

**Presentation:** all five worlds; small and ultrawide screens; maximum text scale; color modes; reduced motion; non-US keys; modal click-through; focus loss; notification pileups; mixed audio warnings; shader/material failures.

**Performance/platform:** exact hardware and settings recorded; cold/warm boot; largest supported colony; mass combat; repeated saves; ten travel cycles; multi-hour session; clean install; upgrade from previous release candidate; offline launch; backup recovery.

## Decisions still required

Confirm primary launch OS/store, weekly staffing capacity, budget for UI/art/audio/QA, and the acceptable first-campaign duration after testing. Decide the precise hero/colony carryover contract and whether native macOS or controller support belongs in 1.0. These decisions can be made after M0 begins; none justifies adding new systems now.

For Steam, generated player-consumed art/audio/text should be reviewed against the current Content Survey; the documentation distinguishes pre-generated shipped content from live-generated content. This is a release task, not an assumption that use of a coding assistant requires a particular disclosure. [Steam Content Survey](https://partner.steamgames.com/doc/gettingstarted/contentsurvey?l=english).

**The next actionable milestone is M0, followed by an observed Earth playtest.** Progress is earned by passing the gates, not by counting completed systems or screenshot iterations.
