using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    public class OverseerCommandTests
    {
        private sealed class Recorder : IOverseerCommandExecutor
        {
            public readonly List<OverseerCommand> Seen = new List<OverseerCommand>();
            public Action<OverseerCommand> OnExecute;

            public void Execute(in OverseerCommand command)
            {
                Seen.Add(command);
                OnExecute?.Invoke(command);
            }
        }

        [Test]
        public void Drain_AppliesInIssueOrderAndStampsTheTick()
        {
            var queue = new OverseerCommandQueue();
            var exec = new Recorder();
            queue.Enqueue(OverseerCommand.PostFlag(FlagType.Explore, new Vector3(1f, 0f, 2f), 400f));
            queue.Enqueue(OverseerCommand.CancelFlag(7));

            Assert.AreEqual(2, queue.Drain(120, exec));

            Assert.AreEqual(OverseerCommandKind.PostFlag, exec.Seen[0].kind);
            Assert.AreEqual(OverseerCommandKind.CancelFlag, exec.Seen[1].kind);
            Assert.AreEqual(120, exec.Seen[0].tick);
            Assert.AreEqual(120, exec.Seen[1].tick);
            Assert.Less(exec.Seen[0].sequence, exec.Seen[1].sequence);
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void Drain_WithNothingQueued_RunsNothing()
        {
            var queue = new OverseerCommandQueue();
            var exec = new Recorder();

            Assert.AreEqual(0, queue.Drain(5, exec));
            Assert.IsEmpty(exec.Seen);
        }

        [Test]
        public void Enqueue_IgnoresAnEmptyCommand()
        {
            var queue = new OverseerCommandQueue();
            queue.Enqueue(default);

            Assert.AreEqual(0, queue.PendingCount);
        }

        /// <summary>
        /// A command that queues another must not run it in the same drain, or two machines that
        /// drain at different moments would disagree about the tick.
        /// </summary>
        [Test]
        public void Drain_LeavesCommandsQueuedDuringExecutionForTheNextDrain()
        {
            var queue = new OverseerCommandQueue();
            var exec = new Recorder();
            exec.OnExecute = c =>
            {
                if (c.kind == OverseerCommandKind.CancelFlag)
                    queue.Enqueue(OverseerCommand.StepBounty(3, 50f));
            };
            queue.Enqueue(OverseerCommand.CancelFlag(1));

            Assert.AreEqual(1, queue.Drain(10, exec));
            Assert.AreEqual(1, queue.PendingCount);
            Assert.AreEqual(1, queue.Drain(11, exec));
            Assert.AreEqual(11, exec.Seen[1].tick);
        }

        [Test]
        public void Drain_AThrowingCommandDoesNotStrandTheRest()
        {
            var queue = new OverseerCommandQueue();
            var exec = new Recorder();
            exec.OnExecute = c =>
            {
                if (c.target == 1) throw new InvalidOperationException("boom");
            };
            queue.Enqueue(OverseerCommand.CancelFlag(1));
            queue.Enqueue(OverseerCommand.CancelFlag(2));

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            Assert.AreEqual(2, queue.Drain(0, exec));

            Assert.AreEqual(2, exec.Seen.Count);
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void Log_IsEmptyUnlessRecording()
        {
            var queue = new OverseerCommandQueue();
            queue.Enqueue(OverseerCommand.CancelFlag(1));
            queue.Drain(1, new Recorder());
            Assert.IsEmpty(queue.Log);

            queue.Record = true;
            queue.Enqueue(OverseerCommand.CancelFlag(2));
            queue.Drain(2, new Recorder());

            Assert.AreEqual(1, queue.Log.Count);
            Assert.AreEqual(2, queue.Log[0].target);
            Assert.AreEqual(2, queue.Log[0].tick);
        }

        [Test]
        public void StructureCommands_CarryThePositionAndTheirOwnPayload()
        {
            var at = new Vector3(12f, 0.4f, -6f);

            OverseerCommand cls = OverseerCommand.SetWorkplaceClass(at, SpecialistClass.Medic);
            OverseerCommand attract = OverseerCommand.PostAttractFlag(at, 650f);
            OverseerCommand orbital = OverseerCommand.CastOrbital((OrbitalPowerId)2, at);

            Assert.AreEqual(OverseerCommandKind.SetWorkplaceClass, cls.kind);
            Assert.AreEqual(at, cls.World);
            Assert.AreEqual((int)SpecialistClass.Medic, cls.content);
            Assert.AreEqual(OverseerCommandKind.PostAttractFlag, attract.kind);
            Assert.AreEqual(650f, attract.amount);
            Assert.AreEqual(at, attract.World);
            Assert.AreEqual(2, orbital.content);
            Assert.AreEqual(at, OverseerCommand.ArmWatchtower(at).World);
            Assert.AreEqual(at, OverseerCommand.RefabRookie(at).World);
            Assert.AreEqual(OverseerCommandKind.PayFobotYard, OverseerCommand.PayFobotYard().kind);
        }

        /// <summary>The log is only useful for replay and network play if it survives serialisation.</summary>
        [Test]
        public void Command_RoundTripsThroughJson()
        {
            OverseerCommand placed = OverseerCommand.PlaceBuilding(4, new Vector2Int(-3, 9), new Vector3(1.5f, 0.25f, -7f));
            placed.tick = 4321;
            OverseerCommand posted = OverseerCommand.PostFlag(FlagType.ClearThreat, new Vector3(8f, 0f, 2f), 900f, "L9+ only");

            var backPlaced = JsonUtility.FromJson<OverseerCommand>(JsonUtility.ToJson(placed));
            var backPosted = JsonUtility.FromJson<OverseerCommand>(JsonUtility.ToJson(posted));

            Assert.AreEqual(OverseerCommandKind.PlaceBuilding, backPlaced.kind);
            Assert.AreEqual(4, backPlaced.content);
            Assert.AreEqual(new Vector2Int(-3, 9), backPlaced.Cell);
            Assert.AreEqual(new Vector3(1.5f, 0.25f, -7f), backPlaced.World);
            Assert.AreEqual(4321, backPlaced.tick);
            Assert.AreEqual((int)FlagType.ClearThreat, backPosted.content);
            Assert.AreEqual(900f, backPosted.amount);
            Assert.AreEqual("L9+ only", backPosted.text);
        }
    }
}
