using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>Settings → TEXT SIZE: the math, the layouts that grow with it, and that it persists.</summary>
    public class HudTextScaleTests
    {
        private string _prefsDir;

        [TearDown]
        public void TearDown()
        {
            HudTextScale.Set(HudTextScale.Default);
            if (_prefsDir != null)
            {
                GamePrefs.DirectoryOverride = null;
                if (Directory.Exists(_prefsDir)) Directory.Delete(_prefsDir, true);
                _prefsDir = null;
            }
        }

        [Test]
        public void RoundToStep_SnapsToTenPercent_AndClampsTo90Through150()
        {
            Assert.AreEqual(1.2f, HudTextScale.RoundToStep(1.23f), 0.001f);
            Assert.AreEqual(0.9f, HudTextScale.RoundToStep(0.5f), 0.001f);
            Assert.AreEqual(1.5f, HudTextScale.RoundToStep(3f), 0.001f);
            Assert.AreEqual(1f, HudTextScale.RoundToStep(float.NaN), 0.001f);
            Assert.AreEqual(1.1f, HudTextScale.Stepped(1f, 1), 0.001f);
            Assert.AreEqual(1.5f, HudTextScale.Stepped(1.5f, 1), 0.001f);
            Assert.AreEqual(0.9f, HudTextScale.Stepped(0.9f, -1), 0.001f);
            Assert.AreEqual("130%", HudTextScale.Percent(1.3f));
        }

        [Test]
        public void Set_BumpsVersionOnlyWhenTheSizeChanges()
        {
            HudTextScale.Set(1f);
            int v = HudTextScale.Version;
            HudTextScale.Set(1.02f); // snaps back to 100 %
            Assert.AreEqual(v, HudTextScale.Version);
            HudTextScale.Set(1.3f);
            Assert.AreEqual(v + 1, HudTextScale.Version);
            Assert.AreEqual(1.3f, HudTextScale.Factor, 0.001f);
        }

        [Test]
        public void AtOneHundredPercent_EveryHelperIsTheIdentity()
        {
            HudTextScale.Set(1f);
            foreach (int px in new[] { 9, 10, 11, 12, 13, 17, 24, 25, 44 })
                Assert.AreEqual(px, HudTextScale.Font(px));
            foreach (float px in new[] { 7.5f, 13f, 26f, 300f })
            {
                Assert.AreEqual(px, HudTextScale.Line(px));
                Assert.AreEqual(px, HudTextScale.Width(px));
                Assert.AreEqual(0f, HudTextScale.Grow(px));
            }
        }

        [Test]
        public void Font_GrowsSmallTextFully_AndCapsDisplayType()
        {
            Assert.AreEqual(15, HudTextScale.Font(10, 1.5f));
            Assert.AreEqual(18, HudTextScale.Font(12, 1.5f));
            Assert.AreEqual(31, HudTextScale.Font(25, 1.5f), "display type grows by at most 6 px");
            Assert.AreEqual(50, HudTextScale.Font(44, 1.5f));
            Assert.AreEqual(9, HudTextScale.Font(10, 0.9f));
            Assert.Greater(HudTextScale.Font(9, 1.1f), 9 - 1);
            Assert.GreaterOrEqual(HudTextScale.Font(1, 0.9f), 1);
        }

        [Test]
        public void LinesGrowFully_WidthsByHalf()
        {
            Assert.AreEqual(39f, HudTextScale.Line(26f, 1.5f), 0.001f);
            Assert.AreEqual(375f, HudTextScale.Width(300f, 1.5f), 0.001f);
            Assert.AreEqual(270f, HudTextScale.Width(300f, 0.8f), 0.001f);
        }

        [Test]
        public void DockLayout_IsUnchangedAtDefaultSize()
        {
            HudTextScale.Set(1f);
            Assert.AreEqual(HudLayout.DockH, HudLayout.DockHeight, 0.001f);
            Assert.AreEqual(28f, HudLayout.ChipHeight, 0.001f);
            Assert.AreEqual(HudLayout.ObjectivesW, HudLayout.ObjectivesWidth, 0.001f);
            var chip = new Rect(0f, 0f, 119f, 28f);
            HudLayout.ChipText(chip, out var label, out var number);
            Assert.AreEqual(new Rect(79f, 0f, 36f, 28f), number);
            Assert.AreEqual(12f, label.height, 0.001f);
            Assert.AreEqual(13f, HudLayout.ChipRate(chip, label).y, 0.001f);
            HudLayout.DockColumns(HudLayout.DockW - HudLayout.Pad * 2f, out float stock, out float clock);
            Assert.AreEqual(HudLayout.StockpileW, stock, 0.001f);
            Assert.AreEqual(186f, clock, 0.001f);
        }

        [TestCase(1280, 800)]
        [TestCase(1920, 1080)]
        [TestCase(1024, 640)]
        public void AtLargestText_ChipLinesFitTheChip_AndTheDockHoldsTheStockpile(int w, int h)
        {
            HudTextScale.Set(HudTextScale.Max);
            float ch = HudLayout.ChipHeight;
            var chip = new Rect(0f, 0f, HudLayout.StockpileChipWidth(), ch);
            HudLayout.ChipText(chip, out var label, out var number);
            Rect rate = HudLayout.ChipRate(chip, label);
            Assert.LessOrEqual(label.yMax, rate.y + 0.5f, "label and rate lines must not overlap");
            Assert.LessOrEqual(rate.yMax, chip.yMax + 0.5f, "rate line stays inside the chip");
            Assert.LessOrEqual(label.xMax + HudLayout.TextGap, number.xMin + 0.01f);

            // Header + two chip rows + gap fit the dock's content height.
            float content = HudLayout.DockHeight - HudLayout.Pad * 2f;
            float stockpile = HudTextScale.Line(17f) + ch * 2f + 4f;
            Assert.LessOrEqual(stockpile, content + 0.5f);
            // The 15 px label font fits its line.
            Assert.LessOrEqual(HudTextScale.Font(10), label.height + 0.5f);

            // The catalog still sits left of the treasury plate and above it, on every screen.
            Rect crest = HudLayout.Crest(w, h);
            var plate = new Rect(crest.x - HudLayout.CrestPad, crest.y - HudLayout.CrestPad,
                crest.width + HudLayout.CrestPad * 2f, crest.height + HudLayout.CrestPad * 2f);
            Rect catalog = HudLayout.ToolPopup(w, h, HudTextScale.Width(320f), HudTextScale.Line(400f));
            Assert.LessOrEqual(catalog.xMax + HudLayout.PanelGap, plate.xMin + 0.01f);
            Assert.LessOrEqual(catalog.yMax + HudLayout.PanelGap, plate.yMin + 0.01f);
            Assert.GreaterOrEqual(catalog.y, HudLayout.Margin - 0.01f);
            Assert.LessOrEqual(HudLayout.DockLeft(w) + HudLayout.DockWidth(w), w - HudLayout.MapSize - HudLayout.Margin + 0.01f,
                "the dock never runs under the minimap");
        }

        [Test]
        public void ClockColumn_TagSitsUnderSol_AtLargestText()
        {
            HudTextScale.Set(HudTextScale.Max);
            HudLayout.ClockHeader(new Rect(0f, 0f, 220f, 114f), out var sol, out var tag, out var speed);
            Assert.LessOrEqual(sol.yMax, tag.y + 0.01f);
            Assert.LessOrEqual(sol.xMax + HudLayout.TextGap, speed.xMin + 0.01f);
            Assert.GreaterOrEqual(sol.height, HudTextScale.Font(11) - 0.5f);
        }

        [Test]
        public void DockColumns_GiveExtraWidthToTheTextColumns()
        {
            HudLayout.DockColumns(HudLayout.DockW - HudLayout.Pad * 2f + 100f, out float stock, out float clock);
            Assert.AreEqual(HudLayout.StockpileW + 55f, stock, 0.01f);
            Assert.AreEqual(186f + 45f, clock, 0.01f);
            HudLayout.DockColumns(500f, out stock, out clock); // narrow dock: columns keep their size
            Assert.AreEqual(HudLayout.StockpileW, stock, 0.01f);
        }

        [Test]
        public void VictoryPanel_GrowsWithText_AndStaysOnScreen()
        {
            HudTextScale.Set(1f);
            Rect normal = VictoryOffer.PanelRect(1920f, 1080f);
            Assert.AreEqual(520f, normal.width, 0.01f);
            HudTextScale.Set(1.5f);
            Rect big = VictoryOffer.PanelRect(1280f, 800f);
            Assert.Greater(big.height, normal.height);
            Assert.LessOrEqual(big.xMax, 1280f);
            Assert.LessOrEqual(big.yMax, 800f);
        }

        [Test]
        public void TextScaleArgument_ReadsFactorsAndPercents()
        {
            Assert.AreEqual(1.5f, DemoSettings.ParseTextScaleArgument(new[] { "game.exe", "-textScale", "1.5" }).Value, 0.001f);
            Assert.AreEqual(1.3f, DemoSettings.ParseTextScaleArgument(new[] { "-textscale=130%" }).Value, 0.001f);
            Assert.AreEqual(1.2f, DemoSettings.ParseTextScaleArgument(new[] { "-textScale", "120" }).Value, 0.001f);
            Assert.AreEqual(1.5f, DemoSettings.ParseTextScaleArgument(new[] { "-textScale", "2" }).Value, 0.001f, "clamped");
            Assert.IsNull(DemoSettings.ParseTextScaleArgument(new[] { "-textScale" }));
            Assert.IsNull(DemoSettings.ParseTextScaleArgument(new[] { "-textScale", "big" }));
            Assert.IsNull(DemoSettings.ParseTextScaleArgument(null));
        }

        [Test]
        public void TextSize_PersistsInGamePrefs_ApartFromHudScale()
        {
            _prefsDir = Path.Combine(Path.GetTempPath(), "sm-textscale-" + System.Guid.NewGuid().ToString("N"));
            GamePrefs.DirectoryOverride = _prefsDir;
            DemoSettings.SetTextScale(1.34f);
            Assert.AreEqual(1.3f, HudTextScale.Factor, 0.001f);
            Assert.IsTrue(File.Exists(GamePrefs.FilePath));

            GamePrefs.Reload();
            HudTextScale.Set(1f);
            HudTextScale.Set(GamePrefs.GetFloat(DemoSettings.TextScaleKey, 1f));
            Assert.AreEqual(1.3f, HudTextScale.Factor, 0.001f);
            Assert.AreNotEqual(DemoSettings.TextScaleKey, DemoSettings.HudKey);
            Assert.IsFalse(GamePrefs.IsMachineKey(DemoSettings.TextScaleKey), "text size roams with the profile");
        }
    }
}
