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
        Hopper = 6
    }

    public enum BuildingCategory
    {
        LandingPad = 0,
        Habitat = 1,
        Power = 2,
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
        /// <summary>Majesty-style keep — must be built first; campus docks from its airlocks.</summary>
        Palace = 15,
        GuildHall = 16,
        HarvesterWorkshop = 17,
        SurveyorWorkshop = 18,
        TerraformerWorkshop = 19,
        CourierWorkshop = 20,
        GeologistWorkshop = 21,
        SentinelWorkshop = 22,
        ClimateLoom = 23,
        AegisSpire = 24,
        DeepArchive = 25
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

    /// <summary>Queued construction after a successful BuildingPlacer.TryPlace.</summary>
    public sealed class ConstructionOrder
    {
        public int Id;
        public BuildingData Data;
        public Vector3 WorldPosition;
        public Vector2Int GridCell;
        public float ProgressSeconds;
        public float RequiredSeconds;
        public bool IsComplete => ProgressSeconds >= RequiredSeconds;
    }
}
