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
        public void LabelScreenPoint_PicksThatFlag_AndNotTheEmptyMap()
        {
            var labels = new[]
            {
                new FlagLabelLayout.ScreenLabel
                {
                    Center = new Vector2(400f, 300f),
                    Width = 220f,
                    Height = 72f,
                    Priority = 1
                }
            };
            Assert.AreEqual(0, FlagLabelLayout.Pick(labels, new Vector2(420f, 310f)));
            Assert.AreEqual(-1, FlagLabelLayout.Pick(labels, new Vector2(12f, 12f)),
                "a click on empty map is not a label, so it must not be treated as this pole");
        }

        [Test]
        public void OverlappingLabels_OffsetTheLowerPriorityOne()
        {
            var labels = new[]
            {
                new FlagLabelLayout.ScreenLabel
                {
                    Center = new Vector2(960f, 540f), Width = 180f, Height = 64f, Priority = 2
                },
                new FlagLabelLayout.ScreenLabel
                {
                    Center = new Vector2(970f, 530f), Width = 180f, Height = 64f, Priority = 0
                }
            };
            FlagLabelLayout.Resolve(labels, 6f);
            Assert.AreEqual(0f, labels[0].OffsetY, 0.01f, "the selected flag keeps its place");
            Assert.Greater(labels[1].OffsetY, 0f);
            Assert.IsFalse(FlagLabelLayout.Bounds(labels[0]).Overlaps(FlagLabelLayout.Bounds(labels[1])),
                "1080p neighbours must not paint on top of each other");
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

        [Test]
        public void Panel_Flag_EscrowSum_AndInBounties_StayOneNumber()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 5000);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.ClearThreat, 50, 5000);

            // Playtest: the panel said 450, the pole said 550, and IN BOUNTIES said 600
            // for a flag the player had priced at 1000. The reserved metals are the bounty.
            Assert.IsTrue(eco.TryEscrowBounty(600f, out _));
            var flag = flags.Post(data, Vector3.zero, 1000f);
            flag.CurrentBounty = 1000f;
            flag.EscrowMetals = 600;
            eco.MatchReserved(FlagBountySync.Sum(flags.Flags));
            FlagBountySync.AdoptReserved(flag);

            float pending = 450f;
            Assert.AreEqual(600, FlagBountySync.PanelAmount(flag, pending, data));
            Assert.AreEqual(600f, flag.CurrentBounty);
            Assert.AreEqual(600, flag.EscrowMetals);
            Assert.AreEqual(600, FlagBountySync.Amount(flag));
            Assert.AreEqual(600, FlagBountySync.Sum(flags.Flags));
            Assert.AreEqual(600, eco.EscrowedMetals);

            // The panel's − / + and the keys both call Apply, so a +50 moves every readout together.
            Assert.IsTrue(FlagBountySync.Apply(
                flags, eco, flag, true, ref pending, data, MajestyEconomy.FlagBountyStep, out bool shortfall));
            Assert.IsFalse(shortfall);
            Assert.AreEqual(450f, pending, "a selected pole does not move the next-post number");
            AssertSynced(flags, eco, flag, pending, data, 650);
            Assert.AreEqual(5000 - 600 - 50, res.Get(ResourceId.Metals));

            res.Set(ResourceId.Metals, 10);
            Assert.IsFalse(FlagBountySync.Apply(
                flags, eco, flag, true, ref pending, data, MajestyEconomy.FlagBountyStep, out shortfall));
            Assert.IsTrue(shortfall);
            AssertSynced(flags, eco, flag, pending, data, 650);
            Assert.AreEqual(10, res.Get(ResourceId.Metals));
        }

        [Test]
        public void PlusMinus_WithNothingSelected_DoesNotTouchPostedFlags()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 2000);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.ClearThreat, 50, 5000);
            var flag = FlagBountySync.TryPost(flags, eco, data, Vector3.zero, 400f);
            int stock = res.Get(ResourceId.Metals);
            int reserved = eco.EscrowedMetals;
            float pending = 200f;

            Assert.AreEqual(FlagBountyEdit.None, FlagBountySync.EditFor(false, false));
            Assert.IsFalse(FlagBountySync.Apply(
                flags, eco, null, false, ref pending, data, MajestyEconomy.FlagBountyStep, out _));

            Assert.AreEqual(200f, pending);
            Assert.AreEqual(400f, flag.CurrentBounty);
            Assert.AreEqual(400, flag.EscrowMetals);
            Assert.AreEqual(stock, res.Get(ResourceId.Metals));
            Assert.AreEqual(reserved, eco.EscrowedMetals);
            Assert.AreEqual(400, FlagBountySync.Sum(flags.Flags));

            // The flag tool, with nothing selected, may still price the next post. It must say so,
            // and it must not spend EU on a pole that is already standing.
            Assert.AreEqual(FlagBountyEdit.NextPost, FlagBountySync.EditFor(false, true));
            Assert.IsTrue(FlagBountySync.Apply(
                flags, eco, null, true, ref pending, data, MajestyEconomy.FlagBountyStep, out _));
            Assert.AreEqual(250f, pending);
            Assert.AreEqual(250, FlagBountySync.PanelAmount(null, pending, data));
            Assert.AreEqual(400f, flag.CurrentBounty);
            Assert.AreEqual(400, flag.EscrowMetals);
            Assert.AreEqual(stock, res.Get(ResourceId.Metals));
            Assert.AreEqual(reserved, eco.EscrowedMetals);

            var second = FlagBountySync.TryPost(flags, eco, data, new Vector3(8f, 0f, 0f), 200f);
            Assert.AreEqual(400, FlagBountySync.PanelAmount(flag, pending, data), "the panel shows the selected pole");
            Assert.AreEqual(200, FlagBountySync.Amount(second));
            Assert.AreEqual(600, FlagBountySync.Sum(flags.Flags), "IN BOUNTIES is every pole, not just the one on the panel");
            Assert.AreEqual(600, eco.EscrowedMetals);

            Assert.IsTrue(FlagBountySync.ClearSelectionOnTool(false), "closing the flag panel clears the pole");
            Assert.IsFalse(FlagBountySync.ClearSelectionOnTool(true));
        }

        [Test]
        public void Post_EscrowsTheClampedBounty_NotTheUnclampedRequest()
        {
            var res = new ResourceManager();
            res.Set(ResourceId.Metals, 20000);
            var eco = new SimpleEconomy(res);
            var flags = new FlagManager();
            var data = MakeFlag(FlagType.ClearThreat, 50, 5000);

            var flag = FlagBountySync.TryPost(flags, eco, data, Vector3.zero, 10000f);

            AssertSynced(flags, eco, flag, 450f, data, 5000);
            Assert.AreEqual(15000, res.Get(ResourceId.Metals));

            float pending = 450f;
            Assert.IsTrue(FlagBountySync.Apply(
                flags, eco, flag, true, ref pending, data, MajestyEconomy.FlagBountyStep, out bool shortfall));
            Assert.IsFalse(shortfall);
            AssertSynced(flags, eco, flag, pending, data, 5000);
            Assert.AreEqual(15000, res.Get(ResourceId.Metals), "a step at the ceiling spends nothing");
        }

        private static void AssertSynced(
            FlagManager flags, SimpleEconomy eco, FlagHandle flag, float pending, FlagData armed, int expect)
        {
            Assert.AreEqual(expect, FlagBountySync.PanelAmount(flag, pending, armed), "panel");
            Assert.AreEqual(expect, FlagBountySync.Amount(flag), "pole");
            Assert.AreEqual((float)expect, flag.CurrentBounty, "flag bounty");
            Assert.AreEqual(expect, flag.EscrowMetals, "flag escrow");
            Assert.AreEqual(expect, FlagBountySync.Sum(flags.Flags), "escrow sum");
            Assert.AreEqual(expect, eco.EscrowedMetals, "IN BOUNTIES");
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
