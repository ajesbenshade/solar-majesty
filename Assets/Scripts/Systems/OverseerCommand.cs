// Overseer commands — pure C#, no MonoBehaviour.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum OverseerCommandKind
    {
        None = 0,
        PlaceBuilding = 1,
        PostFlag = 2,
        CancelFlag = 3,
        StepBounty = 4,
        CastOrbital = 5,
        ActivateGuildBenefit = 6,
        PayFobotYard = 7,
        ArmWatchtower = 8,
        RefabRookie = 9,
        SetWorkplaceClass = 10,
        PostAttractFlag = 11,
        FormParty = 12,
        DisbandParty = 13,
        ChangeSpeed = 14
    }

    public enum SimSpeedChange
    {
        Set = 0,
        Faster = 1,
        Slower = 2,
        TogglePause = 3
    }

    /// <summary>
    /// One thing an overseer asked for, as plain data. Input and HUD code build these and queue
    /// them; only the executor changes the colony. Because a command holds ids and numbers and
    /// never an object reference, the same stream can be logged, replayed, sent to a host, or
    /// produced by an AI overseer.
    /// </summary>
    [Serializable]
    public struct OverseerCommand
    {
        /// <summary>The local player. Other overseers get their own ids when they exist.</summary>
        public const int LocalOverseer = 0;

        public OverseerCommandKind kind;
        public int overseer;
        /// <summary>Simulation step the command was applied on. Stamped by the queue.</summary>
        public long tick;
        /// <summary>Order of issue within a run. Stamped by the queue.</summary>
        public int sequence;
        /// <summary>
        /// Build catalog index (PlaceBuilding), <see cref="FlagType"/> (PostFlag),
        /// <see cref="OrbitalPowerId"/> (CastOrbital), <see cref="RobotGuildId"/>
        /// (ActivateGuildBenefit), <see cref="SpecialistClass"/> (SetWorkplaceClass) or
        /// <see cref="SimSpeedChange"/> (ChangeSpeed).
        /// </summary>
        public int content;
        public int cellX;
        public int cellY;
        public float x;
        public float y;
        public float z;
        /// <summary>Bounty (PostFlag, PostAttractFlag) or bounty change (StepBounty).</summary>
        public float amount;
        /// <summary>
        /// <see cref="FlagHandle.RuntimeId"/> (CancelFlag, StepBounty), party id (DisbandParty)
        /// or speed index (ChangeSpeed with <see cref="SimSpeedChange.Set"/>).
        /// </summary>
        public int target;
        /// <summary>Robot ids (FormParty). Empty means "whoever is at the rest beacon".</summary>
        public int[] ids;
        /// <summary>Free-text flag orders (PostFlag). Empty when there are none.</summary>
        public string text;

        public Vector2Int Cell => new Vector2Int(cellX, cellY);
        public Vector3 World => new Vector3(x, y, z);

        public static OverseerCommand PlaceBuilding(int catalogIndex, Vector2Int cell, Vector3 world) =>
            new OverseerCommand
            {
                kind = OverseerCommandKind.PlaceBuilding,
                content = catalogIndex,
                cellX = cell.x,
                cellY = cell.y,
                x = world.x,
                y = world.y,
                z = world.z
            };

        public static OverseerCommand PostFlag(FlagType type, Vector3 world, float bounty, string orders = null) =>
            new OverseerCommand
            {
                kind = OverseerCommandKind.PostFlag,
                content = (int)type,
                x = world.x,
                y = world.y,
                z = world.z,
                amount = bounty,
                text = orders ?? ""
            };

        public static OverseerCommand CancelFlag(int flagId) =>
            new OverseerCommand { kind = OverseerCommandKind.CancelFlag, target = flagId };

        public static OverseerCommand StepBounty(int flagId, float delta) =>
            new OverseerCommand { kind = OverseerCommandKind.StepBounty, target = flagId, amount = delta };

        public static OverseerCommand CastOrbital(OrbitalPowerId power, Vector3 world) =>
            At(OverseerCommandKind.CastOrbital, world, (int)power);

        public static OverseerCommand ActivateGuildBenefit(RobotGuildId guild) =>
            new OverseerCommand { kind = OverseerCommandKind.ActivateGuildBenefit, content = (int)guild };

        public static OverseerCommand PayFobotYard() =>
            new OverseerCommand { kind = OverseerCommandKind.PayFobotYard };

        // Structures have no id yet, so a command names one by where it stands.

        public static OverseerCommand ArmWatchtower(Vector3 structureAt) =>
            At(OverseerCommandKind.ArmWatchtower, structureAt);

        public static OverseerCommand RefabRookie(Vector3 structureAt) =>
            At(OverseerCommandKind.RefabRookie, structureAt);

        public static OverseerCommand SetWorkplaceClass(Vector3 structureAt, SpecialistClass cls) =>
            At(OverseerCommandKind.SetWorkplaceClass, structureAt, (int)cls);

        public static OverseerCommand PostAttractFlag(Vector3 structureAt, float bounty)
        {
            OverseerCommand command = At(OverseerCommandKind.PostAttractFlag, structureAt);
            command.amount = bounty;
            return command;
        }

        public static OverseerCommand FormParty(int[] robotIds) =>
            new OverseerCommand { kind = OverseerCommandKind.FormParty, ids = robotIds ?? new int[0] };

        public static OverseerCommand DisbandParty(int partyId) =>
            new OverseerCommand { kind = OverseerCommandKind.DisbandParty, target = partyId };

        public static OverseerCommand ChangeSpeed(SimSpeedChange change, int index = 0) =>
            new OverseerCommand { kind = OverseerCommandKind.ChangeSpeed, content = (int)change, target = index };

        private static OverseerCommand At(OverseerCommandKind kind, Vector3 world, int content = 0) =>
            new OverseerCommand { kind = kind, content = content, x = world.x, y = world.y, z = world.z };
    }

    public interface IOverseerCommandExecutor
    {
        void Execute(in OverseerCommand command);
    }

    /// <summary>
    /// Holds commands between the moment they are issued and the next drain. Draining at a fixed
    /// point in the frame, before the simulation steps, gives every command a definite tick.
    /// </summary>
    public sealed class OverseerCommandQueue
    {
        private readonly List<OverseerCommand> _pending = new List<OverseerCommand>();
        private readonly List<OverseerCommand> _log = new List<OverseerCommand>();
        private int _nextSequence;

        public int PendingCount => _pending.Count;

        /// <summary>Keep every applied command in <see cref="Log"/>. Off by default: the log only grows.</summary>
        public bool Record { get; set; }

        /// <summary>Applied commands in order, each with the tick it ran on.</summary>
        public IReadOnlyList<OverseerCommand> Log => _log;

        public void Enqueue(OverseerCommand command)
        {
            if (command.kind == OverseerCommandKind.None) return;
            command.sequence = _nextSequence++;
            _pending.Add(command);
        }

        /// <summary>
        /// Apply everything queued, in order, stamped with <paramref name="tick"/>. A command queued
        /// by another command's execution waits for the next drain. Returns how many ran.
        /// </summary>
        public int Drain(long tick, IOverseerCommandExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            int count = _pending.Count;
            for (int i = 0; i < count; i++)
            {
                OverseerCommand command = _pending[i];
                command.tick = tick;
                if (Record) _log.Add(command);
                try
                {
                    executor.Execute(in command);
                }
                catch (Exception e)
                {
                    // One bad command must not strand the ones behind it.
                    Debug.LogException(e);
                }
            }
            _pending.RemoveRange(0, count);
            return count;
        }

        public void Clear()
        {
            _pending.Clear();
            _log.Clear();
            _nextSequence = 0;
        }
    }
}
