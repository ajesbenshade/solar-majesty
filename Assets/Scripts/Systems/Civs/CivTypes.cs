using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Playable civilizations. Saved by number (<see cref="SaveGame.civ"/>): never renumber or
    /// delete a value. <see cref="None"/> is the neutral civ: no modifiers, today's art and units.
    /// </summary>
    public enum CivId
    {
        None = 0,
        LumenConcord = 1,
        TetherExchange = 2,
        BastionMandate = 3,
        /// <summary>
        /// The fourth civ (expansion and mobility). "Pathfinder Accord" is a provisional name. Its
        /// perks are live data, but it stays off the new-game cards (<see cref="CivDefinition.playable"/>)
        /// until its castle (D1 Stilt Arcology) exists. The number is final.
        /// </summary>
        PathfinderAccord = 4
    }

    /// <summary>
    /// The numbers a civ may change. Each one maps to one existing constant or rule, and its call
    /// sites go through <see cref="CivRules"/> with the owner's <see cref="CivModifiers"/>.
    /// Stored in designer assets by number: never renumber.
    /// </summary>
    public enum CivStat
    {
        None = 0,
        /// <summary>What the treasury pays for a bounty; the hero still receives the full amount.</summary>
        BountyTreasuryCost = 1,
        /// <summary>Workshop hire fee (<c>MajestyEconomy.HireCost</c>). Revival is built on it.</summary>
        HireCost = 2,
        /// <summary>Gold a new hero starts with (<c>MajestyEconomy.HeroStartingPurse</c>).</summary>
        HeroStartingPurse = 3,
        /// <summary>Daily tax a building banks (<c>MajestyEconomy.DailyTax</c>).</summary>
        BuildingDailyTax = 4,
        /// <summary>Tax collectors each Commons fields (<c>MajestyEconomy.CollectorsPerCommons</c>).</summary>
        CollectorsPerCommons = 5,
        /// <summary>Mine / caravan loads and the Market's daily take.</summary>
        TradeIncome = 6,
        /// <summary>The world's starting stockpile of gold.</summary>
        StartingTreasury = 7,
        /// <summary>Commons hit points.</summary>
        CommonsHealth = 8,
        /// <summary>Laser Turret and armed Watchtower range, in metres.</summary>
        GunRange = 9,
        /// <summary>
        /// Laser Turret and armed Watchtower damage per second. Wired, but no built-in civ uses it
        /// since the Mandate's gun damage perk moved onto its Bulwark and Picket.
        /// </summary>
        GunDamage = 10,
        /// <summary>Price of arming a Watchtower with lasers.</summary>
        WatchtowerArmingCost = 11,
        /// <summary>
        /// Price of an outpost building (Watchtower, Ore Mine, Landing Pad; see
        /// <see cref="CivRules.IsOutpostBuilding"/>), before the duplicate surcharge.
        /// </summary>
        OutpostBuildingCost = 12,
        /// <summary>Hero walking speed (<c>SpecialistData.moveSpeed</c>, before gear and statuses).</summary>
        HeroMoveSpeed = 13,
        /// <summary>Seconds a player-placed building takes to go up (<c>BuildingData.buildTimeSeconds</c>).</summary>
        BuildTime = 14,
        /// <summary>
        /// Strike damage of a unit whose roster entry has <see cref="CivUnitDefinition.nearHomeBonus"/>,
        /// while it stands within <see cref="OverseerRules.CommonsShadeRadius"/> of a live Commons.
        /// Units without the flag, and fights away from home, are never changed.
        /// </summary>
        NearHomeUnitDamage = 15
    }

    public enum CivModifierOp
    {
        /// <summary><see cref="CivModifier.value"/> is added to the base value.</summary>
        Add = 0,
        /// <summary><see cref="CivModifier.value"/> is a whole percent: 85 = ×0.85, 120 = ×1.2.</summary>
        Multiply = 1
    }

    /// <summary>One number change. Multipliers are whole percents so every machine does the same integer math.</summary>
    [Serializable]
    public struct CivModifier
    {
        public CivStat stat;
        public CivModifierOp op;
        [Tooltip("Add: the amount. Multiply: whole percent (85 = x0.85).")]
        public int value;
        [Tooltip("True on the civ's drawback. Each playable civ has exactly one.")]
        public bool tradeOff;

        public static CivModifier Add(CivStat stat, int amount, bool tradeOff = false) =>
            new CivModifier { stat = stat, op = CivModifierOp.Add, value = amount, tradeOff = tradeOff };

        public static CivModifier Percent(CivStat stat, int percent, bool tradeOff = false) =>
            new CivModifier { stat = stat, op = CivModifierOp.Multiply, value = percent, tradeOff = tradeOff };
    }

    /// <summary>One art swap: this civ draws <see cref="building"/> with <see cref="artKey"/>.</summary>
    [Serializable]
    public struct CivArtEntry
    {
        public BuildingCategory building;
        public string artKey;

        public CivArtEntry(BuildingCategory building, string artKey)
        {
            this.building = building;
            this.artKey = artKey;
        }
    }

    /// <summary>
    /// One civilization as data. Anything a civ does not list falls back to today's game: no
    /// modifier means the base number, no art entry means the shared building, no roster entry
    /// means the base class. Gameplay never branches on <see cref="id"/>; it asks a
    /// <see cref="CivModifiers"/> built from this record.
    /// </summary>
    [Serializable]
    public sealed class CivDefinition
    {
        /// <summary>Art key meaning "the shared building every civ has today".</summary>
        public const string SharedArtKey = "";

        public CivId id;
        public string displayName = "";
        [Tooltip("One line for the new-game card.")]
        public string pitch = "";
        [TextArea(2, 4)]
        [Tooltip("Perks and the drawback in player words, for the new-game card.")]
        public string summary = "";
        [Tooltip("Which castle the Commons is drawn as. Empty = today's Commons.")]
        public string castleKey = SharedArtKey;
        [Tooltip("Offered on the new-game cards. Off for the neutral civ, and for a civ whose castle does not exist yet (it still plays if a save names it).")]
        public bool playable;
        public Color teamPrimary = Color.white;
        public Color teamSecondary = Color.white;
        public List<CivModifier> modifiers = new List<CivModifier>();
        public List<CivArtEntry> buildingArt = new List<CivArtEntry>();
        public List<CivUnitDefinition> roster = new List<CivUnitDefinition>();

        public string DisplayNameText => Loc.S(displayName);
        public string PitchText => Loc.S(pitch);
        public string SummaryText => Loc.S(summary);

        /// <summary>Deep copy, so a caller can edit a civ without touching the catalog's record.</summary>
        public CivDefinition Clone()
        {
            var copy = new CivDefinition
            {
                id = id,
                displayName = displayName,
                pitch = pitch,
                summary = summary,
                castleKey = castleKey,
                playable = playable,
                teamPrimary = teamPrimary,
                teamSecondary = teamSecondary,
                modifiers = modifiers != null ? new List<CivModifier>(modifiers) : new List<CivModifier>(),
                buildingArt = buildingArt != null ? new List<CivArtEntry>(buildingArt) : new List<CivArtEntry>(),
                roster = new List<CivUnitDefinition>(roster != null ? roster.Count : 0)
            };
            if (roster != null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    if (roster[i] != null)
                        copy.roster.Add(roster[i].Clone());
                }
            }
            return copy;
        }
    }

    /// <summary>
    /// Colours no civ may use as a team colour. Cyan is the Core motif and amber is the charger
    /// pod light; both must stay readable on every civ's buildings. The values mirror the private
    /// constants in <c>HeroBuildingKits</c> (a test keeps the two in step).
    /// </summary>
    public static class CivPalette
    {
        public static readonly Color CoreCyan = new Color(0.22f, 0.84f, 0.98f);
        public static readonly Color PodAmber = new Color(0.95f, 0.74f, 0.26f);

        /// <summary>Team colours closer than this (RGB distance, 0 to about 1.73) to a reserved colour are rejected.</summary>
        public const float ReservedDistance = 0.30f;

        public static float Distance(Color a, Color b)
        {
            float r = a.r - b.r, g = a.g - b.g, bl = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bl * bl);
        }

        /// <summary>True when <paramref name="c"/> is, or reads as, the Core cyan or the pod amber.</summary>
        public static bool IsReserved(Color c) =>
            Distance(c, CoreCyan) < ReservedDistance || Distance(c, PodAmber) < ReservedDistance;
    }

    /// <summary>
    /// Per-player accent colours, for telling apart two players on the same civ (Aaron,
    /// 2026-11-11: each player picks their own accent and duplicates are blocked). The accent
    /// belongs to the player, not the civ: it is carried by that player's
    /// <see cref="CivModifiers.Accent"/>. Pure checks, no state.
    /// TODO(multiplayer): there is no player list yet. The lobby calls <see cref="CanUse"/> with
    /// the accents the other players hold, and the save gains a per-player (civ, accent) list.
    /// </summary>
    public static class CivAccents
    {
        /// <summary>Two accents closer than this (RGB distance) count as the same colour.</summary>
        public const float MinSeparation = 0.25f;

        /// <summary>True when <paramref name="c"/> reads as cyan, amber or any civ's team colour.</summary>
        public static bool IsReservedOrTeamColour(Color c)
        {
            if (CivPalette.IsReserved(c)) return true;
            var all = CivCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || def.id == CivId.None) continue;
                if (CivPalette.Distance(c, def.teamPrimary) < MinSeparation ||
                    CivPalette.Distance(c, def.teamSecondary) < MinSeparation)
                    return true;
            }
            return false;
        }

        /// <summary>True when <paramref name="c"/> matches an accent in <paramref name="taken"/>.</summary>
        public static bool IsTaken(Color c, IReadOnlyList<Color> taken)
        {
            if (taken == null) return false;
            for (int i = 0; i < taken.Count; i++)
            {
                if (CivPalette.Distance(c, taken[i]) < MinSeparation)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True when a player may pick <paramref name="accent"/>: it is not cyan, amber or a team
        /// colour, and no other player already holds it (<paramref name="takenByOthers"/>).
        /// </summary>
        public static bool CanUse(Color accent, IReadOnlyList<Color> takenByOthers) =>
            !IsReservedOrTeamColour(accent) && !IsTaken(accent, takenByOthers);
    }
}
