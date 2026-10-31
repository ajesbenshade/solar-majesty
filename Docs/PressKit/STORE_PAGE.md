# Store page copy (Steam / itch.io)

Draft copy for the Steam and itch.io pages, drawn from what is in the build today. Placeholders
are in `[brackets]`. Read "Before publishing" in [README.md](README.md) first: it covers
trademarks, the AI disclosure, and screenshots that need re-shooting.

## Short description

Steam caps this at 300 characters. This one is 260:

> Build the colony, post the bounties, and let your robots decide. Solar Majesty is colony strategy
> with indirect control: you never command a unit, you price the work. Greedy, timid and brave
> specialists take it on, from Earth's meadows to the red dust of Mars.

Shorter, for itch.io's tagline (78 characters):

> Indirect-control colony strategy. Post the bounty. The robots decide the rest.

## Long description

> **You run the colony. You never command a single robot.**
>
> Solar Majesty is a real-time colony strategy game about indirect control. You plan the
> settlement and set the prices. Ten kinds of autonomous robot specialists decide which jobs are
> worth their while.
>
> **Post a bounty, not an order.** Anything that needs doing out on the map goes up as a flag:
> *Explore*, *Clear Threat*, *Build*, *Extract*, *Defend*. Energy sits in escrow until the job
> is done. Every specialist weighs your offer against its own greed, courage and fatigue. A cheap
> Build order gets ignored, so raise the price. Wounded robots run for the rest beacon no matter
> what you're paying.
>
> **Build a colony that walks.** Charging stations keep the worker bots running, and couriers
> carry the levy home to the till on foot, where a pest can steal it on the way. Workshops fabricate the robots. Mines pay more the
> farther out they stand, and farther out is where the ambushes happen.
>
> **Each world fights back in its own way.** Soil creepers raid Earth's farms. Watt leeches drain
> your power nodes. Dust wisps and stalkers prowl Mars. Every pest needs the right kind of flag.
>
> **Three worlds, one campaign.** Earth, Luna and Mars, each procedurally generated from a seed. To win a world, clear its dens, sustain its population, and research and
> launch the craft that takes you to the next one.
>
> **Features**
> - Indirect control only: bounty flags with escrow, refunds and live "tempted / ignored" feedback
> - 10 specialist classes with personalities and service records; parties of up to four
> - Research labs, a tech tree with Secret Projects and wonders, doctrines and challenge modifiers
> - Endless mode and an end-of-run rating
> - Seeded procedural worlds with their own lighting, fauna and hazards
> - An adaptive soundtrack: four stems built around one theme that changes key and instruments
>   on each world
> - Optional robot commentary from a language model that runs entirely on your own PC (opt-in)
> - Colourblind palettes, reduce motion, HUD scale, rebindable keys
> - Versioned saves, achievements and an Ironman mode
>
> *[Early Access / demo note, e.g. "The demo covers Earth; the full campaign is in development."]*

## Tags

Steam (up to 20, most important first):

`Strategy` · `Colony Sim` · `Base Building` · `Management` · `Simulation` · `Space` · `Sci-fi` ·
`Real-Time` · `Isometric` · `God Game` · `Procedural Generation` · `Singleplayer` · `Indie` ·
`Economy` · `Resource Management` · `Building` · `Casual`

itch.io: `strategy`, `colony-sim`, `base-building`, `indirect-control`, `space`, `sci-fi`,
`management`, `procedural-generation`, `singleplayer`.

Genre: Strategy, Simulation. If the demo ships as Early Access, add the Early Access genre.

## System requirements

> **PLACEHOLDERS: not measured.** Nobody has profiled the game against a minimum spec yet (60 fps
> with 200 agents is still an open item in [RELEASE_CHECKLIST.md](../RELEASE_CHECKLIST.md)).
> Only the OS lines come from project settings; replace every `[TBD]` after testing on real low-end
> hardware.

| | Minimum | Recommended |
|---|---|---|
| **Windows OS** | Windows 10 64-bit | Windows 11 64-bit |
| **macOS** | macOS 12 Monterey (the project's `macOSTargetOSVersion`) | [TBD] |
| **Linux** | [TBD] 64-bit; test on Ubuntu 22.04 / 24.04 or SteamOS | [TBD] |
| **Processor** | [TBD] | [TBD] |
| **Memory** | [TBD] GB RAM | [TBD] GB RAM |
| **Graphics** | [TBD]; DirectX 11/12, Metal or Vulkan capable | [TBD] |
| **Storage** | [TBD] MB, from the size of the release zip | [TBD] |
| **Additional** | Optional local-AI narration downloads about 1.5 GB of models on first use and needs [TBD] GB RAM | |

## Steam Early Access questions

Steamworks asks these on the Early Access page. Answer them from what is in the build, and keep
them honest: an Early Access page that promises features the build lacks is the fastest route to
negative reviews. No third-party names (no SpaceX, Starship, xAI, Grok, or Majesty).

**Why Early Access?**
> The core loop works on Earth, the Moon and Mars. We want players to tell us where the guided start
> is unclear, which worlds feel the same, and how the economy holds up over a long campaign, before
> we call it finished.

**How long will this game be in Early Access?**
> [Fill in: a realistic estimate, for example "about 9–12 months".]

**How is the full version planned to differ from the Early Access version?**
> [Fill in from the roadmap. Candidates already tracked in the repository: more worlds past Mars,
> full controller support, localisation, deeper per-world objectives, more fauna and robot
> animation, UI polish, and balance from player feedback.]

**What is the current state of the Early Access version?**
> Three playable worlds (Earth, the Moon and Mars), a guided start on Earth, ten robot classes,
> research, guilds, doctrines, challenges, Endless and Ironman modes, full save and load with Steam
> Cloud, Steam achievements, and optional on-device AI voices and chat. English only. Keyboard and
> mouse, with partial controller support (Steam Deck included).

**Will the game be priced differently during and after Early Access?**
> [Fill in.]

**How are you planning on involving the Community in your development process?**
> [Fill in: Steam discussions, a Discord, regular patch notes.]

## Store and library art

Programmer art for every Steam capsule, the library hero and logo, and the client and community
icons is in [`Docs/StoreArt/`](../StoreArt/README.md), composed from in-game renders by
[`Tools/steam/make_store_art.py`](../../Tools/steam/make_store_art.py). That README lists which
file goes in which Steamworks slot.

| Asset | Size | Status |
|-------|------|--------|
| Steam header capsule | 920×430 | programmer art, [`Docs/StoreArt/`](../StoreArt/) |
| Steam small capsule | 462×174 | programmer art |
| Steam main capsule | 1232×706 | programmer art |
| Steam vertical capsule | 748×896 | programmer art |
| Library capsule / hero / logo | 600×900 · 3840×1240 · 1280×720 (transparent) | programmer art |
| Page background | 1438×810 | programmer art (a Europa render: swap for Earth, Moon or Mars before upload) |
| Steam client icon / community icon | .ico 16–256 px · 184×184 | done, same emblem as the app icon |
| Screenshots | at least 5, 1920×1080 or larger, real gameplay | captured in [`Docs/StoreScreenshots/`](../StoreScreenshots/README.md), but **predate the AI-core reshape and charging stations: recapture** |
| Trailer | 30–90 s gameplay | missing |
| itch.io cover image | 630×500 | missing |
| App icon | 1024 px down to 16 px | **done**, in [`Assets/Art/Icons/`](../../Assets/Art/Icons/) (the crowned, ringed planet; `.ico` beside it) |

Still to do by a person: an artist pass on the key art and the logo, recapturing the screenshots
and the key-art renders on the current build, and uploading everything in Steamworks.
Check Steamworks' current graphical-asset rules before uploading. Valve revises the sizes and
requires the game logo on capsules.
