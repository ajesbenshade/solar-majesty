using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Re-clicking a pole selects the flag already standing there. +/- edits that bounty
    /// and the escrow, and cannot spend EU the treasury does not have.
    /// </summary>
    public class FlagClickTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void ReclickingSamePole_DoesNotDuplicateOrRechargeEscrow()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 2000);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.ClearThreat, 50, 5000);
            var pole = new Vector3(12f, 0f, 8f);

            // Playtest: three Clear Threat clicks (400, then 500, then 700) on one pole.
            var first = Click(flags, eco, data, pole, 400f, null);
            int stock = res.Get(ResourceId.Metals);
            int escrowed = eco.EscrowedMetals;
            Assert.AreEqual(1600, stock);
            Assert.AreEqual(400, first.EscrowMetals);
            Assert.AreEqual(400, escrowed);

            var second = Click(flags, eco, data, pole, 500f, null);
            var third = Click(flags, eco, data, pole, 700f, first);

            Assert.AreSame(first, second);
            Assert.AreSame(first, third);
            Assert.AreEqual(1, flags.Flags.Count, "the pole keeps a single flag");
            Assert.AreEqual(stock, res.Get(ResourceId.Metals), "later clicks must not escrow again");
            Assert.AreEqual(escrowed, eco.EscrowedMetals);
            Assert.AreEqual(400f, first.CurrentBounty);
            Assert.AreEqual(400, first.EscrowMetals);
        }

        [Test]
        public void ClickOnNextCell_StillPostsANewFlag()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 2000);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.ClearThreat, 50, 5000);
            var pole = new Vector3(4f, 0f, 4f);

            Click(flags, eco, data, pole, 400f, null);
            var neighbor = pole + new Vector3(StillCampusDensity.DefaultCellSize, 0f, 0f);
            var second = Click(flags, eco, data, neighbor, 200f, null);

            Assert.AreEqual(2, flags.Flags.Count);
            Assert.AreNotSame(flags.Flags[0], second);
            Assert.AreEqual(1400, res.Get(ResourceId.Metals));
            Assert.AreEqual(600, eco.EscrowedMetals);
            Assert.GreaterOrEqual(StillCampusDensity.DefaultCellSize, FlagClick.SamePoleRadius + 0.5f,
                "the same-pole radius must stay inside one cell");
        }

        [Test]
        public void RayHit_SelectsThatFlag_EvenIfTheSnapIsElsewhere()
        {
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.Explore, 50, 5000);
            var a = flags.Post(data, Vector3.zero, 100f);
            var b = flags.Post(data, new Vector3(20f, 0f, 0f), 100f);

            var action = FlagClick.Resolve(flags.Flags, Vector3.zero, b, out var existing);

            Assert.AreEqual(FlagClickAction.SelectExisting, action);
            Assert.AreSame(b, existing);
            Assert.AreEqual(2, flags.Flags.Count);
        }

        [Test]
        public void PlusMinus_AdjustsSelectedBountyAndEscrow()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 1000);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.ClearThreat, 50, 5000);
            Assert.IsTrue(eco.TryEscrowBounty(400f, out int escrow));
            var flag = flags.Post(data, Vector3.zero, 400f);
            flag.EscrowMetals = escrow;

            Assert.IsTrue(FlagClick.TryStepBounty(flags, eco, flag, MajestyEconomy.FlagBountyStep));
            Assert.AreEqual(450f, flag.CurrentBounty);
            Assert.AreEqual(450, flag.EscrowMetals);
            Assert.AreEqual(550, res.Get(ResourceId.Metals));
            Assert.AreEqual(450, eco.EscrowedMetals);

            Assert.IsTrue(FlagClick.TryStepBounty(flags, eco, flag, -MajestyEconomy.FlagBountyStep));
            Assert.AreEqual(400f, flag.CurrentBounty);
            Assert.AreEqual(400, flag.EscrowMetals);
            Assert.AreEqual(600, res.Get(ResourceId.Metals), "lowering the bounty refunds the difference");
            Assert.AreEqual(400, eco.EscrowedMetals);
        }

        [Test]
        public void Plus_RefusesWhenTreasuryCannotCoverTheRaise()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 430);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.Build, 50, 5000);
            Assert.IsTrue(eco.TryEscrowBounty(400f, out int escrow));
            var flag = flags.Post(data, Vector3.zero, 400f);
            flag.EscrowMetals = escrow;
            Assert.AreEqual(30, res.Get(ResourceId.Metals));

            Assert.IsFalse(FlagClick.TryStepBounty(flags, eco, flag, MajestyEconomy.FlagBountyStep),
                "a +50 step costs 50 EU and the treasury only has 30");
            Assert.AreEqual(400f, flag.CurrentBounty);
            Assert.AreEqual(400, flag.EscrowMetals);
            Assert.AreEqual(30, res.Get(ResourceId.Metals));
            Assert.AreEqual(400, eco.EscrowedMetals);
        }

        [Test]
        public void Minus_StopsAtTheFlagMinimum_AndPlusAtTheMaximum()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 5000);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.DefendArea, 50, 500);
            Assert.IsTrue(eco.TryEscrowBounty(80f, out int escrow));
            var flag = flags.Post(data, Vector3.zero, 80f);
            flag.EscrowMetals = escrow;

            Assert.IsTrue(FlagClick.TryStepBounty(flags, eco, flag, -1000f));
            Assert.AreEqual(50f, flag.CurrentBounty, "floor is the flag's min bounty");
            Assert.AreEqual(50, flag.EscrowMetals);
            int stock = res.Get(ResourceId.Metals);
            int reserved = eco.EscrowedMetals;

            Assert.IsTrue(FlagClick.TryStepBounty(flags, eco, flag, -MajestyEconomy.FlagBountyStep));
            Assert.AreEqual(50f, flag.CurrentBounty);
            Assert.AreEqual(stock, res.Get(ResourceId.Metals), "a step below the floor refunds nothing");
            Assert.AreEqual(reserved, eco.EscrowedMetals);

            Assert.IsTrue(FlagClick.TryStepBounty(flags, eco, flag, 10000f));
            Assert.AreEqual(500f, flag.CurrentBounty, "ceiling is the flag's max bounty");
            Assert.AreEqual(500, flag.EscrowMetals);
            stock = res.Get(ResourceId.Metals);
            reserved = eco.EscrowedMetals;

            Assert.IsTrue(FlagClick.TryStepBounty(flags, eco, flag, MajestyEconomy.FlagBountyStep));
            Assert.AreEqual(500f, flag.CurrentBounty);
            Assert.AreEqual(stock, res.Get(ResourceId.Metals));
            Assert.AreEqual(reserved, eco.EscrowedMetals);
        }

        /// <summary>
        /// What the placement click does: select when <see cref="FlagClick.Resolve"/> says so,
        /// otherwise escrow once and post. Mirrors FlagPlacementInput.
        /// </summary>
        private static FlagHandle Click(
            FlagManager flags,
            SimpleEconomy eco,
            FlagData data,
            Vector3 snapped,
            float bounty,
            FlagHandle rayHit)
        {
            if (FlagClick.Resolve(flags.Flags, snapped, rayHit, out FlagHandle existing) ==
                FlagClickAction.SelectExisting)
                return existing;

            Assert.IsTrue(eco.TryEscrowBounty(bounty, out int metals));
            var posted = flags.Post(data, snapped, bounty);
            posted.EscrowMetals = metals;
            return posted;
        }

        private FlagData MakeFlag(FlagType type, int min, int max)
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = type;
            data.displayName = type.ToString();
            data.minBounty = min;
            data.maxBounty = max;
            data.defaultBounty = min;
            data.workRequired = 4f;
            return data;
        }
    }
}
