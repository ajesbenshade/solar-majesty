namespace SolarMajesty
{
    /// <summary>
    /// Every HUD tooltip's words, in one place. Titles are short upper-case names; bodies say what
    /// the thing is and how to raise or use it. Key hints are read from <see cref="InputBindings"/>
    /// each time a tip is built, so they follow a rebind.
    ///
    /// Player copy: the canon scan (PlayerFacingCanonTests) reads this file like any other.
    /// </summary>
    public static class HudTips
    {
        /// <summary>The action's current key in HUD caps ("SPACE", "B", "F1").</summary>
        public static string Key(GameAction action) => InputBindings.Label(action).ToUpperInvariant();

        // ---- stockpile, till, clock -------------------------------------------------------------

        public static HudTip Colony() => new HudTip("COLONY",
            "Your bots, charging stations, collectors and worker bots at a glance. Hover a chip to see what raises it.");

        /// <summary>Stockpile chip by its fixed id: BOTS, CHARGERS, HAUL, WORKERS or HALTED.</summary>
        public static HudTip Chip(string id)
        {
            switch (id)
            {
                case "BOTS":
                    return new HudTip("BOTS",
                        "Contract mechs that take your bounties. Each workshop fabricates one; build more workshops for more bots.");
                case "CHARGERS":
                    return new HudTip("CHARGING STATIONS",
                        "Where worker bots recharge. Each station pays EU into its till. Bots raise new ones while the yard is safe; the small line counts solar farms.");
                case "HAUL":
                    return new HudTip("HAUL",
                        "Collectors who walk building tills home to the Commons. The small line is EU in their bags. Red while one is being replaced.");
                case "HALTED":
                    return new HudTip("HALTED",
                        $"Fauna are near the build site, so the worker bots stopped. Post a Clear Threat bounty ({Key(GameAction.ToolFlag)}) and they carry on.");
                default:
                    return new HudTip("WORKER BOTS",
                        "Worker bots grow the colony on their own. The number is the current building's progress.");
            }
        }

        public static HudTip Till() => new HudTip("THE TILL",
            "EU in the Commons. Bounties, buildings and orbital powers are paid from it. Below: income per minute and EU held in posted bounties.");

        public static HudTip Tills() => new HudTip("TILLS",
            "EU on its way home: sitting in building tills, carried by collectors, and the tax the buildings pay each day.");

        public static HudTip Sol() => new HudTip("SOL",
            "Colony days since landing. The tag underneath is this run's mode and challenge.");

        public static HudTip Bounties() => new HudTip("BOUNTIES",
            $"Flags you have posted. Their EU is held until a bot finishes the job. Post one with {Key(GameAction.ToolFlag)}.");

        public static HudTip Threat() => new HudTip("THREAT",
            "Fauna pressure around the yard in view. High threat halts building and wears bots down. Clearing dens lowers it.");

        public static HudTip Rating(string summary) => new HudTip("COLONY RATING", summary);

        // ---- speed ------------------------------------------------------------------------------

        public static HudTip Hold(bool paused) => paused
            ? new HudTip("RESUME", "Start the clock again.", Key(GameAction.TogglePause))
            : new HudTip("HOLD", "Freeze the colony. You can still look around, build and post bounties.", Key(GameAction.TogglePause));

        public static HudTip Slower() => new HudTip("SLOWER", "One speed step down.", Key(GameAction.SpeedDown));

        public static HudTip Faster() => new HudTip("FASTER", "One speed step up.", Key(GameAction.SpeedUp));

        public static HudTip SpeedReadout() => new HudTip("GAME SPEED", "1× is the colony's normal pace.");

        public static HudTip SpeedPip(string label) => new HudTip(label, "Click to run at this speed.");

        // ---- dock verbs -------------------------------------------------------------------------

        public static HudTip Build() => new HudTip("BUILD",
            "Open the build list. Pick a building, then click open ground to place it.", Key(GameAction.ToolBuild));

        public static HudTip Flag() => new HudTip("BOUNTY FLAG",
            "Post a bounty. Bots decide whether it is worth the trip; a bigger bounty tempts more of them.", Key(GameAction.ToolFlag));

        public static HudTip Orbital() => new HudTip("ORBITAL SUPPORT",
            "Satellite powers paid in EU. Cheap next to an uplink, up to 15× the price far from one.", Key(GameAction.ToolOrbital));

        public static HudTip Research() => new HudTip("RESEARCH",
            "Choose what your labs study next. More labs, faster science.", Key(GameAction.ToolTech));

        public static HudTip SwitchYard() => new HudTip("SWITCH YARD",
            "Swing the camera between the home yard and the forward camp.",
            $"{Key(GameAction.FocusCampusA)}/{Key(GameAction.FocusCampusB)}");

        public static HudTip Squad() => new HudTip("FORM SQUAD",
            "Group the selected bots (or those resting at the beacon) into a squad that rests and hunts together.", Key(GameAction.ToolParty));

        public static HudTip Menu() => new HudTip("MENU", "Pause: save, load, settings, quit.", "ESC");

        // ---- catalogs ---------------------------------------------------------------------------

        public static HudTip BuildCatalog() => new HudTip("BUILD LIST",
            "Click a building, then click open ground to place it. Each copy of a building costs more than the last.");

        /// <summary>One build-list row: what it does, what it costs, and why it cannot be bought yet.</summary>
        public static HudTip BuildEntry(string name, string what, string cost, bool locked, bool canAfford, string hotkey)
        {
            string state = locked
                ? "Locked: research opens it."
                : !canAfford ? $"Cost {cost}. Not enough EU yet."
                : $"Cost {cost}.";
            string body = string.IsNullOrEmpty(what) ? state : what + "\n" + state;
            return new HudTip(name, body, hotkey);
        }

        public static HudTip FlagCatalog() => new HudTip("FLAG ORDERS",
            "Pick a flag, then click the map to post it. The bounty is held from the till until a bot finishes the job.");

        public static HudTip FlagKind(FlagType type)
        {
            switch (type)
            {
                case FlagType.Explore:
                    return new HudTip("EXPLORE", "A bot surveys 22 m around the pole for 90 s and charts the dens inside. Extract and Research Site flags pay extra in a survey.");
                case FlagType.ClearThreat:
                    return new HudTip("CLEAR THREAT", "Pays the bot that kills the fauna or den at the pole. Defense mechs like these.");
                case FlagType.Build:
                    return new HudTip("BUILD HERE", "Pays a bot to work the build site at the pole. Engineers like these.");
                case FlagType.Extract:
                    return new HudTip("EXTRACT", "Pays a bot to work the resource node at the pole and carry the yield to a drop-off.");
                case FlagType.DefendArea:
                    return new HudTip("DEFEND", "A bot stands watch at the pole: 50 s of cover after it arrives.");
                case FlagType.ResearchSite:
                    return new HudTip("RESEARCH SITE", "A bot studies the pole: science for the active tech, more inside a survey.");
                case FlagType.EstablishOutpost:
                    return new HudTip("OUTPOST", "Claims the forward outpost near the second yard. A Mine on site pays it back.");
                case FlagType.Terraform:
                    return new HudTip("TERRAFORM", "A terraform pulse: farms on this world grow greener.");
                default:
                    return new HudTip("FLAG", "Click the map to post it.");
            }
        }

        public static HudTip BountyDown(int step) => new HudTip("LOWER BOUNTY",
            $"{step} EU less. Fewer bots will find the job worth it.", Key(GameAction.BountyDown));

        public static HudTip BountyUp(int step) => new HudTip("RAISE BOUNTY",
            $"{step} EU more. More bots, and braver ones, will take the job.", Key(GameAction.BountyUp));

        public static HudTip BountyAmount() => new HudTip("BOUNTY",
            "What the next flag pays, or the selected pole's bounty. It is held from the till until the job is done.");

        public static HudTip Orders() => new HudTip("WRITTEN ORDERS",
            "Optional rules for the next flag in plain words, like \"stay away unless level 9+\" or \"no scouts\". The line below shows what the bots understood.");

        public static HudTip OrbitalCatalog() => new HudTip("ORBITAL SUPPORT",
            "Click a power, then click the map. The price grows with distance from your nearest uplink.");

        public static HudTip OpenList(string key) => new HudTip("OPEN THE LIST",
            "The armed pick stays ready; click the map to use it, or here to choose another.", key);

        // ---- right column -----------------------------------------------------------------------

        public static HudTip Objectives() => new HudTip("OBJECTIVES",
            "What clears this world. Tick all three and the next world opens.");

        public static HudTip FlagLog() => new HudTip("FLAG LOG",
            "Your newest flags, their bounties, and whether a bot has claimed them.");

        public static HudTip ResearchPanel() => new HudTip("RESEARCH",
            "Click a tech to study it. Science comes from labs and Research Site flags; the launch tech is pinned on top.");

        public static HudTip TechEntry(string name, string description, string state) =>
            new HudTip(name, string.IsNullOrEmpty(state) ? description : description + "\n" + state);

        public static string TechState(bool done, bool active, bool can, string need, float cost, float progress01)
        {
            if (done) return "Researched.";
            if (active) return $"Studying: {progress01 * 100f:F0}% of {cost:F0} science.";
            if (!string.IsNullOrEmpty(need)) return $"Needs {need} first.";
            return can ? $"Click to study. {cost:F0} science." : "Not available yet.";
        }

        public static HudTip Close() => new HudTip("CLOSE", null, "ESC");

        public static HudTip Construction() => new HudTip("CONSTRUCTION",
            "Buildings going up. Worker bots build faster while the yard is safe.");

        public static HudTip Roster() => new HudTip("ROSTER",
            "Every bot and what it is doing. Click a row to select that bot.");

        public static HudTip Minimap() => new HudTip("MAP",
            "Click to look there. Gold: buildings. Blue: bots and charted dens. Red: fauna. Flags in their colours.");

        public static HudTip DropManifest() => new HudTip("DROP MANIFEST",
            "What to build first and what it costs.");

        public static HudTip Overlay(string title) => new HudTip(title,
            $"Map overlay. {Key(GameAction.CycleOverlay)} shows the next one or turns it off.");

        public static HudTip Alert() => new HudTip("ALERT",
            $"Click to look at it; that also clears it. {Key(GameAction.JumpToAlert)} jumps to the newest.");

        // ---- inspect cards ----------------------------------------------------------------------

        public static HudTip FlagHere() => new HudTip("FLAG HERE",
            "Post a bounty on this building so bots come to work or defend it.");

        public static HudTip ClassChip(string cls) => new HudTip(cls,
            "Which bot class this building serves. Flags near it pull that class.");

        public static HudTip PayYard() => new HudTip("PAY THE YARD",
            "Pay EU to stand a wrecked bot back up. The bill grows with its level; it takes 120 s.", Key(GameAction.PromptYes));

        public static HudTip ArmLasers() => new HudTip("ARM LASERS",
            "A one-time payment: the tower fires on fauna within 18 m.");

        public static HudTip GuildBenefit() => new HudTip("GUILD BENEFIT",
            "A short boost for this guild's bots once its tech is researched. It costs EU and then cools down.");

        public static HudTip Refab() => new HudTip("RE-FAB",
            "Build a fresh level 1 bot from the wreck. The old chassis is gone for good.");

        public static HudTip Talk() => new HudTip("TALK",
            "Open a conversation with this bot. It runs on a small model on this computer.", Key(GameAction.Confirm));

        public static HudTip Health() => new HudTip("HP",
            "Hull integrity. A bot at zero is a wreck until the yard stands it up.");

        public static HudTip Fatigue() => new HudTip("FATIGUE",
            "Tired bots work slower and head to a charging station to recharge.");

        // ---- title, pause, tutorial -------------------------------------------------------------

        public static HudTip Continue() => new HudTip("CONTINUE", "Back to your last colony, where autosave left it.");
        public static HudTip NewCampaign() => new HudTip("NEW CAMPAIGN", "Start over in a save slot of your choice. Other slots stay.");
        public static HudTip LoadGame() => new HudTip("LOAD", "Open a colony from a save slot.");
        public static HudTip OpenSettings() => new HudTip("SETTINGS", "Sound, display, text size, accessibility and controls.");
        public static HudTip Quit() => new HudTip("QUIT", "Close the game. Autosave keeps your colony.");
        public static HudTip Resume() => new HudTip("RESUME", "Back to the colony.", "ESC");
        public static HudTip SaveGame() => new HudTip("SAVE", "Keep a copy of this colony in a numbered slot.");
        public static HudTip ToTitle() => new HudTip("TITLE", "Leave to the title screen. Autosave keeps this colony.");
        public static HudTip Abandon() => new HudTip("ABANDON BODY", "Throw this world away and land again on a new map.");
        public static HudTip ReportBug() => new HudTip("REPORT A BUG", "Opens the logs folder. Attach the newest log to your report.");
        public static HudTip SkipTutorial() => new HudTip("SKIP", "Hide Jex's lessons. Settings can replay them.");
        public static HudTip Back() => new HudTip("BACK", null, "ESC");

        // ---- settings ---------------------------------------------------------------------------

        public static HudTip Volume(string label) => new HudTip(label, "Drag to set the volume.");
        public static HudTip UiScale() => new HudTip("UI SCALE",
            "Size of the whole HUD: panels, icons and text. AUTO follows the screen height.");
        public static HudTip UiScaleAuto() => new HudTip("AUTO", "Follow the screen height: 100% at 1080p, more on bigger screens.");
        public static HudTip TextSize() => new HudTip("TEXT SIZE",
            "Size of the HUD's words only, from 90% to 150%. Panels grow to fit; icons and the map keep their size.");
        public static HudTip InvertPan() => new HudTip("INVERT CAMERA PAN", "Drag and edge-pan move the camera the other way.");
        public static HudTip Diorama() => new HudTip("DIORAMA CAMERA", "Perspective camera: zoom out to see the horizon and the sky.");
        public static HudTip Fullscreen() => new HudTip("FULLSCREEN", "Switch between fullscreen and a window.");
        public static HudTip DayCycle() => new HudTip("DAY / NIGHT CYCLE", "The sun moves through golden hour and a blue-hour night. Off keeps the still look.");
        public static HudTip Resolution() => new HudTip("RESOLUTION", "Click to step through your monitor's modes.");
        public static HudTip Quality() => new HudTip("QUALITY", "Graphics preset. Lower it if the frame rate drops.");
        public static HudTip FrameCap() => new HudTip("FRAME CAP", "Limit frames per second to save power and heat.");
        public static HudTip TiltShift() => new HudTip("TILT-SHIFT", "Miniature-style depth of field when zoomed in.");
        public static HudTip CloudShadows() => new HudTip("CLOUD SHADOWS", "Drifting cloud shadows on worlds with weather.");
        public static HudTip CharacterVoices() => new HudTip("CHARACTER VOICES", "Recorded bot barks and the Core's voice. Nothing to install.");
        public static HudTip BotLines() => new HudTip("BOT LINES", "Bots write their own lines with a small local language model. Needs the local server; nothing leaves this computer.");
        public static HudTip Spoken() => new HudTip("SPOKEN", "Read bot lines aloud through a local text-to-speech server.");
        public static HudTip EdgeScroll() => new HudTip("EDGE SCROLL", "Pan the camera when the cursor touches the screen edge.");
        public static HudTip ReduceMotion() => new HudTip("REDUCE MOTION", "No camera shake or pulsing; the launch and victory beats become short still shots.");
        public static HudTip Controller() => new HudTip("CONTROLLER", "Use a gamepad for the camera and speed. HUD buttons and placing still need the mouse.");
        public static HudTip ColourVision() => new HudTip("COLOUR VISION", "Palettes for deuteranopia, protanopia and tritanopia.");
        public static HudTip Tutorial() => new HudTip("TUTORIAL", "Replay Jex's lessons from the start.");
        public static HudTip Controls() => new HudTip("CONTROLS", "See every key and change the ones you want.");
        public static HudTip IntroOnLaunch() => new HudTip("PLAY INTRO ON LAUNCH", "Show the opening every time the game starts, not just the first time.");
        public static HudTip ReplayIntro() => new HudTip("REPLAY INTRO", "Watch the opening again from the start.");
        public static HudTip MarsLessons() => new HudTip("MARS JEX LESSONS", "Jex keeps teaching on Mars instead of only commenting on mistakes.");
        public static HudTip Rebind(string action) => new HudTip(action, "Click, then press the new key. Esc cancels.");
        public static HudTip ResetControls() => new HudTip("RESET TO DEFAULTS", "Every key back to its default.");
        public static HudTip GuidedStart(bool skip) => skip
            ? new HudTip("SKIP GUIDED START", "Open the full campaign: guilds, research and every world.")
            : new HudTip("REPLAY GUIDED START", "Go back to the Earth lesson.");
        public static HudTip Mode() => new HudTip("MODE", "Campaign moves on after a win; Endless keeps the colony running.");
        public static HudTip Challenge() => new HudTip("CHALLENGE", "Optional handicaps for a harder run.");
        public static HudTip Stance() => new HudTip("STANCE", "How your bots lean: braver, greedier or more careful.");
        public static HudTip Ironman() => new HudTip("IRONMAN", "One autosave, no manual save or load. Set before New Game.");
    }
}
