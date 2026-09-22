# Production stabilization — implementation checkpoint

This implements the first engineering slice of [the production plan](PRODUCTION_PLAN.md). The commercial scope remains Earth → Luna → Mars → Belt → Europa. This is a tested stabilization checkpoint, not a production-readiness or Phase 4 art sign-off.

## Changes delivered

### Campaign and persistence

- Autosaves now also retain a separate snapshot for each world. Travel restores that world's buildings, agents, flags, fauna, mission and depleted resources, while the campaign stockpile and research travel with the player.
- The saved terrain seed is restored before world generation. Applying a snapshot for another body or seed is refused before resources change.
- Current-format snapshots restore their own campus and specialist roster rather than borrowing the latest PlayerPrefs campus. Older roster-less snapshots retain a legacy migration fallback.
- Building damage and simulation step count survive restoration. New snapshots record demo/tutorial state and the player build GUID.
- Retry removes the current world's saved state and starts a funded colony; other world snapshots remain. Unlocked campaign research remains; ongoing research is not a retry entitlement.
- Atomic file replacement retains a previous `.bak` snapshot. Corrupt JSON can recover that previous snapshot. A newer-version primary is refused rather than silently rolled back.
- Fixed an additional issue found by the scene test: `OnDestroy` during scene teardown no longer reports a combat building loss and rewrites saves. Actual collapse still uses the explicit gameplay destruction path.

This is an incremental repair, **not the final atomic campaign-save architecture**. Player slots still contain individual-world snapshots, rather than a single versioned envelope containing every world and all campaign state. Cross-file crash consistency, save-slot loading UX, migrations across released builds, and complete transient-state fidelity remain in M2.

### Opening and simulation

- A Build flag that already attracts the Engineer now advances the tutorial. The player is no longer told to cancel and recreate a legitimate accepted job. No SpecialistBrain scoring changes were made.
- An additional regression uses the actual Engineer greed logic after hunger rises, rather than only testing tutorial booleans.
- A free Commons is placed on a destination even when the Earth-only demo shell does nothing.
- Flag interest, campus ecology and board updates run once through the fixed simulation clock. Research-effect refresh also uses simulation time instead of rendered frame count.

### Test and content baseline

- Generated colliders now use the existing play/edit-safe destruction helper in campus dressing, visual utility and modular building creation.
- Replaced obsolete sheet-white, orange-gasket and near-black material assertions with the current warm cream / graphite / SM_Hull charcoal contracts. Geometry assertions remain in place. The old dock test expected `(15,14)` to be free even though it intersects the Commons; replacement coverage verifies rejection and a valid fallback site.
- Detached the grass material from a missing parent asset found during the actual Earth scene boot. Its shader and texture reference are retained.
- Updated playtest instructions to allow an immediately accepted Build flag.
- Telemetry timestamps and float values use invariant decimal formatting; tabs are escaped. Session headers and saves record Unity's player build GUID.
- CI pins Unity 6000.5.10f1, runs EditMode and PlayMode, and builds the existing macOS/Windows/Linux matrix on pull requests as well as main/nightly runs. Remote CI has not been executed in this local session.

## Verification

Tests run in `/private/tmp/solar-stabilization-project`, copied from the working tree. Its company/product are changed to `SolarMajestyValidation / StabilizationTests`, isolating both saves and PlayerPrefs from the development game. The original project settings are not opened by Unity during these checks.

- Original review baseline: **279 EditMode tests, 270 passed, 9 failed**.
- Final results and player-build details: see [verification evidence](ReviewEvidence/2026-09-22-stabilization.md).
- Runtime scenarios cover mismatched planet/seed rejection, restored structure damage, Earth → Luna → Earth with shared stockpile, Continue without legacy campus preferences and with a stale seed preference, funded retry, and first arrival/revisit on all five worlds.
- Destructive campaign smoke tests refuse to run in an ordinary developer profile. They run only under the dedicated validation company name or ephemeral CI. This intentionally leaves them skipped in a normal editor Test Runner run.
- These automated scene tests verify initialization and persistence boundaries. They do not prove a human can complete the five mission gates, that the economy is balanced, or that a 60–90 minute session is fun or stable.

## Remaining before M0 can close

1. Run the same candidate on two clean machines, including native Windows launch/input/audio and save-directory permissions.
2. Extend persistence tests to a worked colony with ongoing construction, claimed flags, veteran gear/XP, dead/downed specialists, mission sustain, combat damage and depleted nodes; add migration fixtures and interrupted-write tests. Do not claim full save fidelity from the starter-colony smoke tests.
3. Verify the tutorial visually with both immediate and delayed input, including actual workshop placement and flag completion; conduct the uncoached external sessions from M1.
4. Recheck ecology/economy pacing after removing duplicate updates. Correct timing is verified structurally, but balance needs longer sessions.
5. Observe remote CI and resolve platform-specific failures. The local Windows build is a compilation/package check, not a Windows playtest.
6. Preserve the existing blocked Phase 4 visual exit. No art expansion or production-release declaration is included here.

The next implementation slice should deepen the worked-colony save round trip and campaign envelope before adding more content.
