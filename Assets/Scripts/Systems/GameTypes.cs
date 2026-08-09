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
        DefendArea = 4
    }

    public enum SpecialistClass
    {
        EngineerBot = 0,
        ScoutDrone = 1,
        DefenseMech = 2
    }

    public enum BuildingCategory
    {
        LandingPad = 0,
        Habitat = 1,
        Power = 2,
        Mining = 3,
        Defense = 4,
        Utility = 5
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
