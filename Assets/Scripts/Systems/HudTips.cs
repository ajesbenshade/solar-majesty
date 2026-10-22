namespace SolarMajesty
{
    /// <summary>
    /// Every HUD tooltip's words, in one place. Titles are short upper-case names; bodies say what
    /// the thing is and how to raise or use it. Key hints come from <see cref="InputBindings"/> so
    /// they follow a rebind: either the tip's Key field or a <c>{key:Action}</c> token in a Loc.S body
    /// (never inside a Loc.F format; see Docs/LOCALIZATION.md).
    /// </summary>
    public static class HudTips
    {
        private static string K(string text) => InputBindings.ExpandKeyTokens(text);
        private static string Key(GameAction action) => InputBindings.Hint(action).ToUpperInvariant();

        // ---- stockpile, treasury, clock ----------------------------------------------------------

        public static HudTip Colony() => new HudTip(Loc.S("COLONY"),
            Loc.S("Your robots, charging stations, haulers and builder bots at a glance. Hover a chip to see what raises it."));

        /// <summary>Stockpile chip by its fixed id (HEROES, CHARGERS, HAUL, BOTS, HALTED).</summary>
        public static HudTip Chip(string id)
        {
            switch (id)
            {
                case "HEROES":
                    return new HudTip(Loc.S("HEROES"),
                        Loc.S("Contract robots that take your bounties. Each workshop fabricates one; build more workshops for more heroes."));
                case "CHARGERS":
                    return new HudTip(Loc.S("CHARGING STATIONS"),
                        Loc.S("Where worker bots recharge. Each station pays EU into its till. Bots raise new ones while the yard is safe; the small line counts solar farms."));
                case "HAUL":
                    return new HudTip(Loc.S("HAUL"),
                        Loc.S("Collectors who walk building tills home to the Commons. The small line is EU in their bags. Red while one is being replaced."));
                case "HALTED":
                    return new HudTip(Loc.S("HALTED"),
                        K(Loc.S("Enemies are near the build site, so the bots stopped. Post a Clear Threat bounty ({key:ToolFlag}) and they carry on.")));
                default:
                    return new HudTip(Loc.S("WORKER BOTS"),
                        Loc.S("Builder bots grow the village on their own. The number is the current building's progress."));
            }
        }

        public static HudTip Treasury() => new HudTip(Loc.S("TREASURY"),
            Loc.S("EU in the Commons. Bounties, buildings and orbital powers are paid from it. Below: income per minute and EU locked in posted bounties."));

        public static HudTip Levy() => new HudTip(Loc.S("LEVY"),
            Loc.S("EU on its way home: sitting in building tills, carried by collectors, and the tax the buildings pay each day."));

        public static HudTip Sol() => new HudTip(Loc.S("SOL"),
            Loc.S("Colony days since landing. The tag underneath is this run's mode and challenge."));

        public static HudTip Bounties() => new HudTip(Loc.S("BOUNTIES"),
            K(Loc.S("Flags you have posted. Their EU is held until a robot finishes the job. Post one with {key:ToolFlag}.")));

        public static HudTip Threat() => new HudTip(Loc.S("THREAT"),
            Loc.S("Fauna pressure around the yard in view. High threat halts building and wears robots down. Clearing dens lowers it."));

        public static HudTip Rating(string summary) => new HudTip(Loc.S("CORE RATING"), summary);

        // ---- speed ------------------------------------------------------------------------------

        public static HudTip Hold(bool paused) => paused
            ? new HudTip(Loc.S("RESUME"), Loc.S("Start the clock again."), Key(GameAction.TogglePause))
            : new HudTip(Loc.S("HOLD"), Loc.S("Freeze the colony. You can still look around, build and post bounties."), Key(GameAction.TogglePause));

        public static HudTip Slower() => new HudTip(Loc.S("SLOWER"), Loc.S("One speed step down."), Key(GameAction.SpeedDown));

        public static HudTip Faster() => new HudTip(Loc.S("FASTER"), Loc.S("One speed step up."), Key(GameAction.SpeedUp));

        public static HudTip SpeedReadout() => new HudTip(Loc.S("GAME SPEED"), Loc.S("1× is the colony's normal pace."));

        public static HudTip SpeedPip(string label) => new HudTip(label, Loc.S("Click to run at this speed."));

        // ---- dock verbs -------------------------------------------------------------------------

        public static HudTip Build() => new HudTip(Loc.S("BUILD"),
            Loc.S("Open the build catalog. Pick a building, then click open ground to place it."), Key(GameAction.ToolBuild));

        public static HudTip Flag() => new HudTip(Loc.S("BOUNTY FLAG"),
            Loc.S("Post a bounty. Robots decide whether it is worth the trip; a bigger bounty tempts more of them."), Key(GameAction.ToolFlag));

        public static HudTip Orbital() => new HudTip(Loc.S("ORBITAL SUPPORT"),
            Loc.S("Satellite powers paid in EU. Cheap next to an uplink, up to 15× the price far from one."), Key(GameAction.ToolOrbital));

        public static HudTip Research() => new HudTip(Loc.S("RESEARCH"),
            Loc.S("Choose what your labs study next. More labs, faster science."), Key(GameAction.ToolTech));

        public static HudTip SwitchYard() => new HudTip(Loc.S("SWITCH YARD"),
            Loc.S("Swing the camera between the home yard and the forward camp."),
            $"{Key(GameAction.FocusCampusA)}/{Key(GameAction.FocusCampusB)}");

        public static HudTip Party() => new HudTip(Loc.S("FORM PARTY"),
            Loc.S("Group the selected robots (or those resting at the beacon) into a party that rests and hunts together."), Key(GameAction.ToolParty));

        public static HudTip Menu() => new HudTip(Loc.S("MENU"), Loc.S("Pause: save, load, settings, quit."), "ESC");

        // ---- catalogs ---------------------------------------------------------------------------

        public static HudTip BuildCatalog() => new HudTip(Loc.S("BUILD CATALOG"),
            Loc.S("Click a building, then click open ground to place it. Each copy of a building costs more than the last."));

        /// <summary>One catalog row: what it does, what it costs, and why it cannot be bought yet.</summary>
        public static HudTip BuildEntry(string name, string what, string cost, bool locked, bool canAfford, string hotkey)
        {
            string state = locked
                ? Loc.S("Locked: research opens it.")
                : !canAfford ? Loc.F("Cost {0}. Not enough yet.", cost)
                : Loc.F("Cost {0}.", cost);
            string body = string.IsNullOrEmpty(what) ? state : what + "\n" + state;
            return new HudTip(name, body, hotkey);
        }

        public static HudTip FlagCatalog() => new HudTip(Loc.S("FLAG ORDERS"),
            Loc.S("Pick a flag, then click the map to post it. The bounty is held from the treasury until a robot finishes the job."));

        public static HudTip FlagKind(FlagType type)
        {
            switch (type)
            {
                case SolarMajesty.FlagType.Explore:
                    return new HudTip(Loc.S("EXPLORE"), Loc.S("A robot surveys 22 m around the pole for 90 s and charts the dens inside. Extract and Research Site flags pay extra in a survey."));
                case SolarMajesty.FlagType.ClearThreat:
                    return new HudTip(Loc.S("CLEAR THREAT"), Loc.S("Pays the robot that kills the fauna or den at the pole. Defense mechs like these."));
                case SolarMajesty.FlagType.Build:
                    return new HudTip(Loc.S("BUILD HERE"), Loc.S("Pays a robot to work the build site at the pole. Engineers like these."));
                case SolarMajesty.FlagType.Extract:
                    return new HudTip(Loc.S("EXTRACT"), Loc.S("Pays a robot to work the resource node at the pole and carry the yield to a drop-off."));
                case SolarMajesty.FlagType.DefendArea:
                    return new HudTip(Loc.S("DEFEND"), Loc.S("A robot stands watch at the pole: 50 s of cover after it arrives."));
                case SolarMajesty.FlagType.ResearchSite:
                    return new HudTip(Loc.S("RESEARCH SITE"), Loc.S("A robot studies the pole: science for the active tech, more inside a survey."));
                case SolarMajesty.FlagType.EstablishOutpost:
                    return new HudTip(Loc.S("OUTPOST"), Loc.S("Claims the forward outpost near the second yard. More power draw; a Mine on site pays it back."));
                case SolarMajesty.FlagType.Terraform:
                    return new HudTip(Loc.S("TERRAFORM"), Loc.S("A terraform pulse: farms on this world grow greener."));
                default:
                    return new HudTip(Loc.S("FLAG"));
            }
        }

        public static HudTip BountyDown(int step) => new HudTip(Loc.S("LOWER BOUNTY"),
            Loc.F("{0} EU less. Fewer robots will find the job worth it.", step), Key(GameAction.BountyDown));

        public static HudTip BountyUp(int step) => new HudTip(Loc.S("RAISE BOUNTY"),
            Loc.F("{0} EU more. More robots, and braver ones, will take the job.", step), Key(GameAction.BountyUp));

        public static HudTip BountyAmount() => new HudTip(Loc.S("BOUNTY"),
            Loc.S("What the next flag pays, or the selected pole's bounty. Robots keep their share; the till takes its cut."));

        public static HudTip Orders() => new HudTip(Loc.S("WRITTEN ORDERS"),
            Loc.S("Optional rules for the next flag in plain words, like \"stay away unless level 9+\" or \"no scouts\". The line below shows what the robots understood."));

        public static HudTip OrbitalCatalog() => new HudTip(Loc.S("ORBITAL SUPPORT"),
            Loc.S("Click a power, then click the map. The price grows with distance from your nearest uplink."));

        public static HudTip OpenList(string key) => new HudTip(Loc.S("OPEN THE LIST"),
            Loc.S("The armed pick stays ready; click the map to use it, or here to choose another."), key);

        // ---- right column -----------------------------------------------------------------------

        public static HudTip Objectives() => new HudTip(Loc.S("OBJECTIVES"),
            Loc.S("What clears this world. Tick all three and the next world opens."));

        public static HudTip FlagLog() => new HudTip(Loc.S("FLAG LOG"),
            Loc.S("Your newest flags, their bounties, and whether a robot has claimed them."));

        public static HudTip ResearchPanel() => new HudTip(Loc.S("RESEARCH"),
            Loc.S("Click a tech to study it. Science comes from labs and Research Site flags; the launch tech is pinned on top."));

        public static HudTip TechEntry(string name, string description, string state) =>
            new HudTip(name, string.IsNullOrEmpty(state) ? description : description + "\n" + state);

        public static string TechState(bool done, bool active, bool can, string need, float cost, float progress01)
        {
            if (done) return Loc.S("Researched.");
            if (active) return Loc.F("Studying: {0:F0}% of {1:F0} science.", progress01 * 100f, cost);
            if (!string.IsNullOrEmpty(need)) return Loc.F("Needs {0} first.", need);
            return can ? Loc.F("Click to study. {0:F0} science.", cost) : Loc.S("Not available yet.");
        }

        public static HudTip Close() => new HudTip(Loc.S("CLOSE"), null, "ESC");

        public static HudTip Construction() => new HudTip(Loc.S("CONSTRUCTION"),
            Loc.S("Buildings going up. Bots work faster while the yard is safe."));

        public static HudTip Roster() => new HudTip(Loc.S("ROSTER"),
            Loc.S("Every robot and what it is doing. Click a row to select that robot."));

        public static HudTip Minimap() => new HudTip(Loc.S("MAP"),
            Loc.S("Click to look there. Gold: buildings. Blue: robots and charted dens. Red: fauna. Flags in their colours."));

        public static HudTip DropManifest() => new HudTip(Loc.S("DROP MANIFEST"),
            Loc.S("What to build first and what it costs."));

        public static HudTip Overlay(string title) => new HudTip(title,
            K(Loc.S("Map overlay. {key:CycleOverlay} shows the next one or turns it off.")));

        public static HudTip Alert() => new HudTip(Loc.S("ALERT"),
            K(Loc.S("Click to look at it; that also clears it. {key:JumpToAlert} jumps to the newest.")));

        // ---- inspect cards ----------------------------------------------------------------------

        public static HudTip FlagHere() => new HudTip(Loc.S("FLAG HERE"),
            Loc.S("Post a bounty on this building so robots come to work or defend it."));

        public static HudTip ClassChip(string cls) => new HudTip(cls,
            Loc.S("Which robot class this building serves. Flags near it pull that class."));

        public static HudTip PayYard() => new HudTip(Loc.S("PAY THE YARD"),
            Loc.S("Pay EU to stand a wrecked robot back up. The bill grows with its level; it takes 120 s."), Key(GameAction.PayYard));

        public static HudTip ArmLasers() => new HudTip(Loc.S("ARM LASERS"),
            Loc.S("A one-time payment: the tower fires on fauna within 18 m."));

        public static HudTip GuildBenefit() => new HudTip(Loc.S("GUILD BENEFIT"),
            Loc.S("A short boost for this guild's robots once its tech is researched. It costs EU and then cools down."));

        public static HudTip Refab() => new HudTip(Loc.S("RE-FAB"),
            Loc.S("Build a fresh level 1 robot from the wreck. The old chassis is gone for good."));

        public static HudTip Talk() => new HudTip(Loc.S("TALK"),
            Loc.S("Open a conversation with this robot. It runs on a small model on this computer."), Key(GameAction.Confirm));

        public static HudTip Health() => new HudTip(Loc.S("HP"),
            Loc.S("Hull integrity. A robot at zero is a wreck until the yard stands it up."));

        public static HudTip Fatigue() => new HudTip(Loc.S("FATIGUE"),
            Loc.S("Tired robots work slower and head home to rest and shop."));

        // ---- title, pause, tutorial -------------------------------------------------------------

        public static HudTip Continue() => new HudTip(Loc.S("CONTINUE"), Loc.S("Back to your last colony, where autosave left it."));
        public static HudTip NewCampaign() => new HudTip(Loc.S("NEW CAMPAIGN"), Loc.S("Start over in a save slot of your choice. Other slots stay."));
        public static HudTip LoadGame() => new HudTip(Loc.S("LOAD"), Loc.S("Open a colony from a save slot."));
        public static HudTip OpenSettings() => new HudTip(Loc.S("SETTINGS"), Loc.S("Sound, display, text size, accessibility and controls."));
        public static HudTip Quit() => new HudTip(Loc.S("QUIT"), Loc.S("Close the game. Autosave keeps your colony."));
        public static HudTip Resume() => new HudTip(Loc.S("RESUME"), Loc.S("Back to the colony."), "ESC");
        public static HudTip SaveGame() => new HudTip(Loc.S("SAVE"), Loc.S("Keep a copy of this colony in a numbered slot."));
        public static HudTip ToTitle() => new HudTip(Loc.S("TITLE"), Loc.S("Leave to the title screen. Autosave keeps this colony."));
        public static HudTip Abandon() => new HudTip(Loc.S("ABANDON BODY"), Loc.S("Throw this world away and land again on a new map."));
        public static HudTip SkipTutorial() => new HudTip(Loc.S("SKIP"), Loc.S("Hide the advisor's lessons. Settings can replay them."));
        public static HudTip Back() => new HudTip(Loc.S("BACK"), null, "ESC");

        // ---- settings ---------------------------------------------------------------------------

        public static HudTip Volume(string label) => new HudTip(label, Loc.S("Drag to set the volume."));
        public static HudTip UiScale() => new HudTip(Loc.S("UI SCALE"),
            Loc.S("Size of the whole HUD: panels, icons and text. AUTO follows the screen height."));
        public static HudTip UiScaleAuto() => new HudTip(Loc.S("AUTO"), Loc.S("Follow the screen height: 100% at 1080p, more on bigger screens."));
        public static HudTip TextSize() => new HudTip(Loc.S("TEXT SIZE"),
            Loc.S("Size of the HUD's words only, from 90% to 150%. Panels grow to fit; icons and the map keep their size."));
        public static HudTip InvertPan() => new HudTip(Loc.S("INVERT CAMERA PAN"), Loc.S("Drag and edge-pan move the camera the other way."));
        public static HudTip Diorama() => new HudTip(Loc.S("DIORAMA CAMERA"), Loc.S("Perspective camera: zoom out to see the horizon and the sky."));
        public static HudTip Fullscreen() => new HudTip(Loc.S("FULLSCREEN"), Loc.S("Switch between fullscreen and a window."));
        public static HudTip DayCycle() => new HudTip(Loc.S("DAY / NIGHT CYCLE"), Loc.S("The sun moves through golden hour and a blue-hour night. Off keeps the still look."));
        public static HudTip Resolution() => new HudTip(Loc.S("RESOLUTION"), Loc.S("Click to step through your monitor's modes."));
        public static HudTip Quality() => new HudTip(Loc.S("QUALITY"), Loc.S("Graphics preset. Lower it if the frame rate drops."));
        public static HudTip FrameCap() => new HudTip(Loc.S("FRAME CAP"), Loc.S("Limit frames per second to save power and heat."));
        public static HudTip TiltShift() => new HudTip(Loc.S("TILT-SHIFT"), Loc.S("Miniature-style depth of field when zoomed in."));
        public static HudTip CloudShadows() => new HudTip(Loc.S("CLOUD SHADOWS"), Loc.S("Drifting cloud shadows on worlds with weather."));
        public static HudTip CharacterVoices() => new HudTip(Loc.S("CHARACTER VOICES"), Loc.S("Recorded robot barks and the Core's voice. Nothing to install."));
        public static HudTip HeroLines() => new HudTip(Loc.S("HERO LINES"), Loc.S("Robots write their own lines with a small local language model. Needs the local server; nothing leaves this computer."));
        public static HudTip Spoken() => new HudTip(Loc.S("SPOKEN"), Loc.S("Read hero lines aloud through a local text-to-speech server."));
        public static HudTip EdgeScroll() => new HudTip(Loc.S("EDGE SCROLL"), Loc.S("Pan the camera when the cursor touches the screen edge."));
        public static HudTip ReduceMotion() => new HudTip(Loc.S("REDUCE MOTION"), Loc.S("No camera shake or pulsing; the launch and victory beats become short still shots."));
        public static HudTip ColourVision() => new HudTip(Loc.S("COLOUR VISION"), Loc.S("Palettes for deuteranopia, protanopia and tritanopia."));
        public static HudTip Language() => new HudTip(Loc.S("LANGUAGE"), Loc.S("Click to switch language. Voices stay in English."));
        public static HudTip Tutorial() => new HudTip(Loc.S("TUTORIAL"), Loc.S("Replay the advisor's lessons from the start."));
        public static HudTip Logs() => new HudTip(Loc.S("OPEN LOGS FOLDER"), Loc.S("Error logs to attach to a bug report. The number counts errors this session."));
        public static HudTip MarsLessons() => new HudTip(Loc.S("MARS JEX LESSONS"), Loc.S("Jex keeps teaching on Mars instead of only commenting on mistakes."));
        public static HudTip Rebind(string action) => new HudTip(action, Loc.S("Click, then press the new key. Esc cancels."));
        public static HudTip ResetControls() => new HudTip(Loc.S("RESET CONTROLS"), Loc.S("Every key back to its default."));
        public static HudTip CursorSpeed() => new HudTip(Loc.S("CURSOR SPEED"), Loc.S("How fast the controller stick moves the cursor."));
        public static HudTip GuidedStart(bool skip) => skip
            ? new HudTip(Loc.S("SKIP GUIDED START"), Loc.S("Open the full campaign: guilds, research and every world."))
            : new HudTip(Loc.S("REPLAY GUIDED START"), Loc.S("Go back to the Earth lesson."));
        public static HudTip Mode() => new HudTip(Loc.S("MODE"), Loc.S("Campaign moves on after a win; Endless keeps the colony running."));
        public static HudTip Challenge() => new HudTip(Loc.S("CHALLENGE"), Loc.S("Optional handicaps for a harder run."));
        public static HudTip Stance() => new HudTip(Loc.S("STANCE"), Loc.S("How your robots lean: braver, greedier or more careful."));
        public static HudTip Ironman() => new HudTip(Loc.S("IRONMAN"), Loc.S("One autosave, no manual save or load. Set before New Game."));
    }
}
