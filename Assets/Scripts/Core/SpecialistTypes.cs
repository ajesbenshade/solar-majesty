using System;
using UnityEngine;

namespace SolarMajesty
{
    public enum SpecialistAction
    {
        Idle,
        Rest,
        PursueFlag,
        Flee,
        Hunt,
        Wander,
        Repair
    }

    [Serializable]
    public struct SpecialistContext
    {
        public SpecialistData Data;
        public Vector3 Position;
        public float Fatigue;          // 0 = fresh, 1 = exhausted
        public float GreedHunger;      // how hungry for reward right now (0-1)
        public FlagHandle CurrentFlag; // null if none
        public float HealthNormalized; // 0-1
        public Vector3 SafetyPosition; // inn / campus
        public Vector3 VocationPosition;
        public Vector3 HuntPosition;
        public float HuntDistance;
        public bool HasHunt;
        public SpecialistAction CurrentAction;
        public Vector3 WorkshopPosition;
        public bool HasWorkshop;
        public float FlagWorkshopBonus;
        public bool HasPatient;
        public Vector3 PatientPosition;
        public bool HasRepair;
        public Vector3 RepairPosition;
        public float RepairDistance;
        public float RepairNeed;
        public float CourageEffective;
    }

    public struct BrainDecision
    {
        public SpecialistAction Action;
        public FlagHandle TargetFlag;   // only valid if Action == PursueFlag
        public Vector3 TargetPosition;
        public float Score;
        public string Reason;

        public static BrainDecision Idle(float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.Idle, Score = score, Reason = reason };

        public static BrainDecision Rest(float score, string reason, Vector3 inn) =>
            new BrainDecision { Action = SpecialistAction.Rest, TargetPosition = inn, Score = score, Reason = reason };

        public static BrainDecision Pursue(FlagHandle flag, float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.PursueFlag, TargetFlag = flag, Score = score, Reason = reason };

        public static BrainDecision Flee(Vector3 safety, float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.Flee, TargetPosition = safety, Score = score, Reason = reason };

        public static BrainDecision Hunt(Vector3 prey, float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.Hunt, TargetPosition = prey, Score = score, Reason = reason };

        public static BrainDecision Wander(Vector3 dest, float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.Wander, TargetPosition = dest, Score = score, Reason = reason };

        public static BrainDecision Repair(Vector3 site, float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.Repair, TargetPosition = site, Score = score, Reason = reason };
    }

    /// <summary>
    /// Lightweight handle so the brain never holds heavy scene references.
    /// Created by FlagManager; scored by SpecialistBrain.
    /// </summary>
    [Serializable]
    public class FlagHandle
    {
        public FlagData Data;
        public Vector3 WorldPosition;
        public float CurrentBounty;
        public float Risk;              // 0-1
        public int ClaimCount;          // how many specialists already chasing it
        [NonSerialized] public object RuntimeId; // FlagManager identity — not Unity-serializable
        /// <summary>Metals reserved from the stockpile when the flag was posted.</summary>
        public int EscrowMetals;
        /// <summary>Work required snapshot (may be scaled for scouted dens).</summary>
        public float PostedWork;
        /// <summary>How many living specialists would take this flag at the current bounty.</summary>
        public int InterestCount;
        /// <summary>Short class names currently tempted (empty if ignored).</summary>
        public string InterestLabel;
    }
}
