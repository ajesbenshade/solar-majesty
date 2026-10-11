# Solar Majesty: press kit

> **Status: draft.** Everything below describes the game as it is in this repository today.
> Fields in `[brackets]` are placeholders the team must fill in before this goes to press. Read
> [Before publishing](#before-publishing) first: it lists the claims and names that need a
> decision.

## Fact sheet

| | |
|---|---|
| **Title** | Solar Majesty |
| **Developer** | [studio / developer name] |
| **Publisher** | [self-published / publisher] |
| **Genre** | Indirect-control colony strategy |
| **Platforms** | Windows, macOS, Linux (PC). Builds for all three come from CI; macOS is the one verified by hand so far |
| **Engine** | Unity 6 (URP) |
| **Release date** | [TBD]. Current state: playable early demo (Earth), full campaign in development |
| **Price** | [TBD] |
| **Languages** | English. A localization lookup exists; other languages are not translated yet |
| **Players** | Single-player |
| **Website** | [URL] |
| **Store pages** | [Steam URL] · [itch.io URL] |
| **Press contact** | [email] |
| **Press assets** | this folder; app icons in [`Assets/Art/Icons/`](../../Assets/Art/Icons/) (1024 px master) |

## One-line pitch

You run the colony. You never command a single robot. You post a price on a job, and the ten kinds
of robot specialists decide whether it's worth their while.

## Short description (about 50 words)

Solar Majesty is a colony strategy game about indirect control. Build habitats, workshops and mines,
then post bounty flags (*explore here*, *clear that den*, *build this*, *defend the farm*) and watch
autonomous robot specialists take the work, ignore it, haggle with your price, or flee when the
local fauna bites back.

## Long description

Every colony in Solar Majesty starts on open ground with a Colony Commons and an empty treasury
of energy. From there you plan and pay, and the specialists do the rest.

You place buildings. Habitats give colonists beds, births and taxes. Workshops fabricate the
robots. Farms, mines and power nodes keep the lights on. Anything that needs doing out on the map,
you post as a **bounty flag**: Explore, Clear Threat, Build, Extract or Defend, with energy held in
escrow. Each robot weighs the job against its own personality, greed, fatigue and fear. A greedy
Engineer ignores a cheap Build order until you raise the price. A hurt Defender flees to the rest
beacon whatever you offer.

The planet pushes back. Each world has its own fauna, from soil creepers on Earth's farms to
power-draining watt leeches and dust wisps on Mars, and each one needs the right kind of flag.
Tax collectors walk your income home on foot, and an ambush on the way costs you the purse. Mines
pay more the farther out they sit, and farther out is also more dangerous.

Clear the dens, sustain the population and research the launch technology to win a world. The
campaign then moves on, from Earth to Luna, Mars, the Asteroid Belt and Europa. Each world is
procedurally generated from a seed and has its own lighting, hazards and goals.

## Features

These are in the build today. The current playtest demo is Earth only; the other worlds and
the deeper systems are switched on with **Settings → Full campaign**.

- **Indirect control:** no click-to-move, ever. Bounty flags with escrowed rewards are your only lever
  on the units, and you can raise, lower or cancel them (with a refund).
- **Ten robot specialist classes**, each fabricated by its own workshop: Scout, Engineer, Defense,
  Medic, Harvester, Surveyor, Terraformer, Courier, Geologist and Sentinel. Each has a
  personality and a service record, and they form parties of up to four.
- **A five-world campaign:** Earth → Luna → Mars → Asteroid Belt → Europa. To win a world you
  clear its dens, sustain its population, and research a launch technology plus place a landing
  pad.
- **Fauna native to each world** (soil creepers, regolith mites, watt leeches, ash hoppers, dust
  wisps, dust stalkers). Some need Defend, others Clear Threat.
- **An economy that walks:** taxes are carried home on foot, and mines pay by distance and risk.
  Research labs, a tech tree with Secret Projects and wonders, doctrines, challenge modifiers, an
  Endless mode and an end-of-run Overseer rating.
- **Seeded procedural worlds:** the same world and seed give the same map.
- **A generated adaptive score:** four stems that react to play, with one leitmotif restated in
  each world's own key and instrumentation. All music, sound effects and character voices are
  produced by the project's own scripts.
- **Optional local-AI hero lines:** heroes can comment, as text or speech, through a language
  model and voice model that run on the player's own machine. Opt-in, off by default, nothing
  leaves the computer.
- **Accessibility:** deuteranopia, protanopia and tritanopia palettes, a reduce-motion switch, HUD
  scale, rebindable keys, a frame cap, and severity shown by icon and text as well as colour.
- **Versioned full-world saves**, achievements, and an Ironman mode.
- **Game speed** from 0.25× to 4×, and a hold key to stop the clock.

## Screenshots

Images already in the repository that work as screenshots. All are real in-engine captures.
**All of them predate 2026-09-25**, when airlocks and connecting tubes were removed from the game,
and every one of them still shows them. Use them for early press and pitch decks only. Before a
store page goes live, re-shoot fresh captures with **Solar Majesty → Render → Capture Mars Still**.

| File | Size | What it shows | Use |
|------|------|---------------|-----|
| [`Docs/Roadmap/SM_Capture.png`](../Roadmap/SM_Capture.png) | 3440×1191 | Mars, Sol 1: geodesic Commons, habitat, solar array, landing pad with rocket, full HUD | **Best.** Clean game capture with no editor chrome. Ultra-wide, so crop to 16:9 for stores |
| [`Docs/Roadmap/SM_Capture_Luna.png`](../Roadmap/SM_Capture_Luna.png) | 1721×1191 | Luna: crater rim, Commons dome, rocket pad, suited colonists, full HUD | **Good.** Clean capture; crop to 16:9 |
| [`Docs/Roadmap/SM_MarsCampaign_PlayModeCampusStill9.png`](../Roadmap/SM_MarsCampaign_PlayModeCampusStill9.png) | 2575×1407 | Earth, Sol 2: built campus, "FIGHT" and "DUTY" unit tags, soil creeper | Usable after cropping off the Unity editor toolbar (top ~150 px) |
| [`Docs/Roadmap/SM_MarsCampaign_PlayModeCampusStill7.png`](../Roadmap/SM_MarsCampaign_PlayModeCampusStill7.png) | 2575×1407 | Earth, Sol 18: Commons with habitats and workshops, Overseer log | Same: crop the editor chrome |
| [`Docs/Roadmap/SM_MarsCampaign_PlayModeCampusStill8.png`](../Roadmap/SM_MarsCampaign_PlayModeCampusStill8.png) | 2575×1407 | Earth, Sol 1: fresh drop, river and woods, Build catalog open | Same: crop the editor chrome |
| [`Docs/Roadmap/SM_MarsCampaign_PlayModeCampusStill6.png`](../Roadmap/SM_MarsCampaign_PlayModeCampusStill6.png) | 2575×1407 | Mars, Sol 1: empty start with HUD and arrival log | Mood shot. Little to see in it |
| [`Docs/Roadmap/SM_MarsCampaign_EditorStill.png`](../Roadmap/SM_MarsCampaign_EditorStill.png) | 1920×1080 | Mars campus, no HUD | Editor render (`Camera.Render`), not gameplay; dark hull tint. Background or banner only |
| [`Docs/Roadmap/SM_MarsCampaign_PackedCampusStill.png`](../Roadmap/SM_MarsCampaign_PackedCampusStill.png) | 1920×1080 | Packed Mars campus, no HUD | Editor render; dense layout the art direction has since retired |

Do not use these as screenshots:

- `SM_MarsCampus_SpacedOverseer_Concept.png` is **concept art**, not the game. Stores require
  screenshots to show real gameplay.
- `SM_MarsCampaign_PlayModeCampusStill.png` to `…Still4.png` are low-resolution JPEGs with a
  `.png` name (1024 px wide) showing the retired tube-web campus.
- `SM_MarsCampaign_PlayModeStill.png` and `…Still5.png` are under 1000 px wide.

The **app icon** (dark metallic tile, gold HUD bevel, rust planet with an orbit ring and the Colony
Commons dome) is at [`Assets/Art/Icons/SM_AppIcon_1024.png`](../../Assets/Art/Icons/SM_AppIcon_1024.png),
with every smaller size beside it. Still missing: logo lockup, capsule art, key art and a trailer.
See [STORE_PAGE.md](STORE_PAGE.md) for the store image sizes.

## Credits

[Team names and roles]

Third-party content (fonts, Unity Asset Store packages, voice model) and its licenses are listed
in [CREDITS.md](../../CREDITS.md).

## Before publishing

Decide on each of these before anything here goes public:

1. **Trademarks in the copy.** The repository's tagline ("SpaceXAI Colonization Protocol"), the art
   briefs ("SpaceX industrial aesthetic", "Starship") and the design docs ("Majesty 2-style") name
   other companies' trademarks. This press kit and STORE_PAGE.md leave those names out on purpose.
   Use them in public material only after a legal check. "A love letter to indirect-control
   strategy" says the same thing without naming anyone.
2. **AI disclosure.** Steam's content survey asks about AI-generated content. Several parts of the
   project qualify:
   - character voice lines are pre-rendered with a neural text-to-speech model (Kokoro-82M);
   - the optional hero narration is generated live by a local language model;
   - concept sheets that guided the models were made with an image generator.

   Disclose these, and describe the live narration as opt-in and on-device.
3. **Asset Store content.** [CREDITS.md](../../CREDITS.md) flags Unity Asset Store packages
   that must not be redistributed. The press kit does not include them, but check before making
   the repository public.
4. **Fill the placeholders:** developer, contact, price, date, URLs and credits.
5. **Fresh screenshots** (see above).
