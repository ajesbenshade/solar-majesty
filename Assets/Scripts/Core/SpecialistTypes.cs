using System;
using UnityEngine;

namespace SolarMajesty
{
    public enum SpecialistAction
    {
        Idle,
        Rest,
        PursueFlag
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
    }

    public struct BrainDecision
    {
        public SpecialistAction Action;
        public FlagHandle TargetFlag;   // only valid if Action == PursueFlag
        public float Score;
        public string Reason;

        public static BrainDecision Idle(float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.Idle, Score = score, Reason = reason };

        public static BrainDecision Rest(float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.Rest, Score = score, Reason = reason };

        public static BrainDecision Pursue(FlagHandle flag, float score, string reason) =>
            new BrainDecision { Action = SpecialistAction.PursueFlag, TargetFlag = flag, Score = score, Reason = reason };
    }

    /// <summary>
    /// Lightweight handle so the brain never holds heavy scene references.
    /// Created by FlagManager; scored by SpecialistBrain.
    /// </summary>
    public class FlagHandle
    {
        public FlagData Data;
        public Vector3 WorldPosition;
        public float CurrentBounty;
        public float Risk;              // 0-1
        public int ClaimCount;          // how many specialists already chasing it
        public object RuntimeId;        // whatever FlagManager uses as identity
    }
}
