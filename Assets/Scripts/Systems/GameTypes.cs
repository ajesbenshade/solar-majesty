// Shared enums and economy/building records.
// Brain/flag decision types live in Core/SpecialistTypes.cs.

using System;
using UnityEngine;

namespace SolarMajesty
{
    public enum ResourceId
    {
        Regolith = 0,
        WaterIce = 1,
        Metals = 2,
        Power = 3
    }

    /// <summary>
    /// Mechanical bounty kinds posted through <see cref="FlagManager"/>.
    /// Narrative decree titles map onto these via <see cref="FlagDecreeIds"/> —
    /// see Docs/FLAG_TYPE_MAP_EARTH_LUNA_MARS.md. Do not add FlagType values
    /// for story beats; add a decree id instead.
    /// </summary>
    public enum FlagType
    {
        Explore = 0,
        ClearThreat = 1,
        Build = 2,
        Extract = 3,
        DefendArea = 4,
        ResearchSite = 5,
        EstablishOutpost = 6,
        Terraform = 7
    }

    public enum SpecialistClass
    {
        EngineerBot = 0,
        ScoutDrone = 1,
        DefenseMech = 2,
        Medic = 3,
        HarvesterBot = 4,
        SurveyorBot = 5,
        TerraformerBot = 6,
        CourierBot = 7,
        GeologistBot = 8,
        SentinelMech = 9
    }

    /// <summary>
    /// Fauna kinds. Stalkers hunt from lairs; mites/ticks/creepers steal (Defend);
    /// leeches/wisps drain power and hoppers raid HABs (Clear Threat).
    /// </summary>
    public enum FaunaKind
    {
        Stalker = 0,
        Mite = 1,
        Leech = 2,
        Wisp = 3,
        Tick = 4,
        Creeper = 5,
        Hopper = 6,
        /// <summary>Scrapyard ghost. Nibbles mechs / steals MET. Clear Threat, not HAB raid.</summary>
        JunkBot = 7
    }

    public enum BuildingCategory
    {
        LandingPad = 0,
        Habitat = 1,
        Power = 2,
        /// <summary>OPS-1 ops annex (legacy id). Not Colony Commons; metals drop-off like Mine.</summary>
        Mining = 3,
        Defense = 4,
        Utility = 5,
        Laboratory = 6,
        Farm = 7,
        Mine = 8,
        RegolithCamp = 9,
        Inn = 10,
        ScoutWorkshop = 11,
        EngineerWorkshop = 12,
        DefenseWorkshop = 13,
        MedicWorkshop = 14,
        /// <summary>
        /// Colony Commons — first 6×6 civic landmark; campus docks from its airlocks.
        /// Saved as int 15 (CampusSnapshot). Do not change the numeric id.
        /// </summary>
        Commons = 15,
        GuildHall = 16,
        HarvesterWorkshop = 17,
        SurveyorWorkshop = 18,
        TerraformerWorkshop = 19,
        CourierWorkshop = 20,
        GeologistWorkshop = 21,
        SentinelWorkshop = 22,
        ClimateLoom = 23,
        AegisSpire = 24,
        DeepArchive = 25,
        /// <summary>Majesty market stall — potions and the regen necklace. Saved as int 26.</summary>
        Market = 26,
        /// <summary>Guild arms and armor. Saved as int 27.</summary>
        Blacksmith = 27,
        /// <summary>Player-facing Fobot Yard — paid revive. Saved as int 28.</summary>
        FobotYard = 28,
        /// <summary>Rim guard post + levy chest. Lasers after CRED upgrade. Saved as int 29.</summary>
        Watchtower = 29,
        /// <summary>Paid heal kiosk. Triage clocks in. Saved as int 30.</summary>
        AidStation = 30
    }

    [Serializable]
    public struct ResourceAmount
    {
        public ResourceId resource;
        public int amount;

        public ResourceAmount(ResourceId resource, int amount)
        {
            this.resource = resource;
            this.amount = amount;
        }
    }

    /// <summary>
    /// Majesty gold. Flags, buildings, techs, and heroes spend Metals only.
    /// ICE and Power stay life-support / grid constraints, never shop currency.
    /// </summary>
    public static class Wallet
    {
        public static ResourceAmount[] Credits(int metals)
        {
            if (metals <= 0) return Array.Empty<ResourceAmount>();
            return new[] { new ResourceAmount(ResourceId.Metals, metals) };
        }

        public static ResourceAmount[] MetalsOnly(ResourceAmount[] costs)
        {
            if (costs == null || costs.Length == 0) return costs;
            int met = 0;
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].amount <= 0) continue;
                if (costs[i].resource == ResourceId.Metals || costs[i].resource == ResourceId.WaterIce)
                    met += costs[i].amount;
            }

            return Credits(met);
        }

        public static bool IsCreditsOnly(ResourceAmount[] costs)
        {
            if (costs == null) return true;
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].amount > 0 && costs[i].resource != ResourceId.Metals)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Queued construction after a successful BuildingPlacer.TryPlace.</summary>
        public sealed class ConstructionOrder
        {
            public int Id;
            public BuildingData Data;
            public Vector3 WorldPosition;
            public Vector2Int GridCell;
            public float ProgressSeconds;
            public float RequiredSeconds;
            public SpecialistClass? RefabClass;
            public bool IsComplete => ProgressSeconds >= RequiredSeconds;
            public bool IsRefab => RefabClass.HasValue;
        }
}
