using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>The HUD tooltip: placement beside the cursor, clamping, the hover delay, and its text catalog.</summary>
    public class HudTooltipTests
    {
        private static readonly Vector2 Size = new Vector2(240f, 80f);

        [Test]
        public void Place_SitsBelowRightOfTheCursor_WhenThereIsRoom()
        {
            var cursor = new Vector2(400f, 300f);
            Rect r = HudTooltipMath.Place(cursor, Size, 1920f, 1080f);
            Assert.AreEqual(cursor.x + HudTooltipMath.OffsetX, r.x, 0.01f);
            Assert.AreEqual(cursor.y + HudTooltipMath.OffsetY, r.y, 0.01f);
            Assert.IsFalse(r.Contains(cursor), "the tip never sits under the arrow");
        }

        [Test]
        public void Place_FlipsLeftAtTheRightEdge_AndAboveAtTheBottom()
        {
            var cursor = new Vector2(1900f, 1070f);
            Rect r = HudTooltipMath.Place(cursor, Size, 1920f, 1080f);
            Assert.LessOrEqual(r.xMax, cursor.x, "flipped to the left of the cursor");
            Assert.LessOrEqual(r.yMax, cursor.y, "flipped above the cursor");
            AssertInside(r, 1920f, 1080f);
        }

        [TestCase(0f, 0f)]
        [TestCase(1280f, 800f)]
        [TestCase(640f, 0f)]
        [TestCase(-50f, 900f)]
        [TestCase(3000f, -20f)]
        public void Place_StaysInsideTheView_WhereverTheCursorIs(float x, float y)
        {
            foreach (var view in new[] { new Vector2(1280f, 800f), new Vector2(1920f, 1080f), new Vector2(1536f, 864f) })
            {
                Rect r = HudTooltipMath.Place(new Vector2(x, y), Size, view.x, view.y);
                AssertInside(r, view.x, view.y);
            }
        }

        [Test]
        public void Place_InAViewSmallerThanTheTip_PinsToTheTopLeftMargin()
        {
            Rect r = HudTooltipMath.Place(new Vector2(50f, 50f), new Vector2(400f, 300f), 200f, 150f);
            Assert.AreEqual(HudTooltipMath.Margin, r.x, 0.01f);
            Assert.AreEqual(HudTooltipMath.Margin, r.y, 0.01f);
        }

        [Test]
        public void WrapWidth_KeepsShortTipsTight_AndCapsLongOnes()
        {
            Assert.AreEqual(120f, HudTooltipMath.WrapWidth(120f, 300f, 1920f), 0.01f);
            Assert.AreEqual(300f, HudTooltipMath.WrapWidth(900f, 300f, 1920f), 0.01f);
            Assert.AreEqual(HudTooltipMath.MinWidth, HudTooltipMath.WrapWidth(10f, 300f, 1920f), 0.01f);
            Assert.LessOrEqual(HudTooltipMath.WrapWidth(900f, 300f, 200f), 200f - HudTooltipMath.Margin * 2f + 0.01f);
        }

        [Test]
        public void Hover_WaitsForTheDelay_ThenShows()
        {
            var h = new HudTooltipMath.Hover();
            Assert.IsFalse(h.Update("a", 10f));
            Assert.IsFalse(h.Update("a", 10f + HudTooltipMath.Delay * 0.5f));
            Assert.IsTrue(h.Update("a", 10f + HudTooltipMath.Delay + 0.01f));
        }

        [Test]
        public void Hover_MovingToAnotherControl_RestartsTheDelay_UnlessATipWasJustShowing()
        {
            var h = new HudTooltipMath.Hover();
            h.Update("a", 0f);
            Assert.IsFalse(h.Update("b", 0.3f), "a new control before any tip showed waits its own delay");

            Assert.IsTrue(h.Update("b", 0.3f + HudTooltipMath.Delay + 0.01f));
            Assert.IsTrue(h.Update("c", 1.0f), "sweeping along the dock: the next tip shows at once");

            Assert.IsFalse(h.Update(null, 1.1f));
            Assert.IsTrue(h.Update("d", 1.1f + HudTooltipMath.Grace * 0.5f), "within the grace window");
            Assert.IsFalse(h.Update(null, 2f));
            Assert.IsFalse(h.Update("e", 2f + HudTooltipMath.Grace + 0.2f), "after the grace window the delay is back");
        }

        [Test]
        public void Hover_Reset_HidesAndForgetsTheGraceWindow()
        {
            var h = new HudTooltipMath.Hover();
            h.Update("a", 0f);
            Assert.IsTrue(h.Update("a", 1f));
            h.Reset();
            Assert.IsFalse(h.Visible);
            Assert.IsFalse(h.Update("b", 1.05f));
        }

        [Test]
        public void HudTip_FromString_IsATitleOnlyTip()
        {
            HudTip t = "BUILD";
            Assert.AreEqual("BUILD", t.Title);
            Assert.IsNull(t.Body);
            Assert.IsFalse(t.IsEmpty);
            Assert.IsTrue(new HudTip(null, "").IsEmpty);
            Assert.AreNotEqual(new HudTip("A", "x").Id, new HudTip("A", "y").Id);
        }

        /// <summary>Every parameterless tip in the catalog has a title and a body, and key tokens are expanded.</summary>
        [Test]
        public void Catalog_EveryTipHasWords_AndNoRawKeyTokens()
        {
            int checkedTips = 0;
            foreach (var m in typeof(HudTips).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.ReturnType != typeof(HudTip) || m.GetParameters().Length != 0) continue;
                var tip = (HudTip)m.Invoke(null, null);
                Assert.IsFalse(string.IsNullOrEmpty(tip.Title), m.Name + " has no title");
                if (m.Name != nameof(HudTips.Close) && m.Name != nameof(HudTips.Back))
                    Assert.IsFalse(string.IsNullOrEmpty(tip.Body), m.Name + " has no body");
                StringAssert.DoesNotContain("{key:", tip.Body ?? "", m.Name);
                checkedTips++;
            }
            Assert.Greater(checkedTips, 60);

            foreach (var id in new[] { "HEROES", "CHARGERS", "HAUL", "BOTS", "HALTED" })
                StringAssert.DoesNotContain("{key:", HudTips.Chip(id).Body);
            foreach (FlagType t in System.Enum.GetValues(typeof(FlagType)))
                Assert.IsFalse(string.IsNullOrEmpty(HudTips.FlagKind(t).Body), t.ToString());
            StringAssert.Contains(InputBindings.Hint(GameAction.ToolFlag), HudTips.Bounties().Body);
            Assert.AreEqual(InputBindings.Hint(GameAction.ToolBuild).ToUpperInvariant(), HudTips.Build().Key);
        }

        [Test]
        public void BuildEntry_SaysWhyItCannotBeBought()
        {
            StringAssert.Contains("120 EU", HudTips.BuildEntry("HAB", "Bots bunk here.", "120 EU", false, true, "2").Body);
            StringAssert.Contains("research", HudTips.BuildEntry("LAB", null, "300 EU", true, false, null).Body);
            Assert.AreNotEqual(
                HudTips.BuildEntry("HAB", null, "120 EU", false, true, null).Body,
                HudTips.BuildEntry("HAB", null, "120 EU", false, false, null).Body);
        }

        private static void AssertInside(Rect r, float viewW, float viewH)
        {
            Assert.GreaterOrEqual(r.x, HudTooltipMath.Margin - 0.01f);
            Assert.GreaterOrEqual(r.y, HudTooltipMath.Margin - 0.01f);
            Assert.LessOrEqual(r.xMax, viewW - HudTooltipMath.Margin + 0.01f);
            Assert.LessOrEqual(r.yMax, viewH - HudTooltipMath.Margin + 0.01f);
        }
    }
}
