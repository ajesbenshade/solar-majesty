using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// New-campaign civ chips. The four official names have to read in full on the chip at
    /// 1920×1080 and 1280×720. A cut name is the only reason a chip grows a name tooltip; the
    /// pitch stays in the paragraph under the cards. Neutral Commons stays off the cards.
    /// </summary>
    public class CivPickerLayoutTests
    {
        private const string Ellipsis = "\u2026";

        /// <summary>
        /// Wider than DejaVu Sans Bold caps (about 0.70em). Padding is included, as GUIStyle.CalcSize does.
        /// </summary>
        private static float WideCaps(string line, int size) =>
            (line == null ? 0 : line.Length) * size * 0.85f + HudLayout.CivCardPadX * 2f;

        [SetUp]
        public void SetUp()
        {
            HudTextScale.Set(HudTextScale.Default);
            CivCatalog.UseDatabase(null);
        }

        [TearDown]
        public void TearDown()
        {
            HudTextScale.Set(HudTextScale.Default);
            CivCatalog.ReloadDatabase();
        }

        [Test]
        public void OfficialNames_StayTheFourWords_AndWrapOnTheSpace()
        {
            var playable = CivCatalog.Playable();
            CollectionAssert.AreEqual(
                new[] { "Lumen Concord", "Tether Exchange", "Bastion Mandate", "Pathfinder Accord" },
                Names(playable));

            foreach (var def in playable)
            {
                string label = HudLayout.CivCardLabel(def.displayName);
                Assert.AreEqual(def.displayName.ToUpperInvariant(), label.Replace("\n", " "), def.displayName);
                StringAssert.Contains("\n", label, def.displayName);
                StringAssert.DoesNotContain(Ellipsis, label, def.displayName);
                StringAssert.DoesNotContain(def.pitch, label);
                StringAssert.DoesNotContain(def.summary, label);
            }
        }

        [Test]
        public void Cards_ShowEveryWord_At1080pAnd720p()
        {
            foreach (var (sw, sh) in new[] { (1920, 1080), (1280, 720) })
            {
                float card = CardWidth(sw, sh);
                foreach (var def in CivCatalog.Playable())
                {
                    var face = HudLayout.LayoutCivCard(def.displayName, card, WideCaps, HudLayout.CivCardFont);
                    Assert.AreEqual(HudLayout.CivCardFont, face.FontSize, $"{def.displayName} at {sw}x{sh}");
                    Assert.IsFalse(face.HasNameTooltip, $"{def.displayName} at {sw}x{sh} needs no name tip");
                    Assert.IsNull(face.NameTooltip);
                    Assert.AreEqual(def.displayName.ToUpperInvariant(), face.Shown.Replace("\n", " "));
                    StringAssert.DoesNotContain(Ellipsis, face.Shown);
                    Assert.LessOrEqual(WideCaps(LongestLine(face.Shown), face.FontSize), card);
                }
            }
        }

        [Test]
        public void Cards_MeasureInTheDisplayFace_At1080pAnd720p()
        {
            var style = HudSkin.Text(HudSkin.Display, HudLayout.CivCardFont, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            style.padding = new RectOffset(
                HudLayout.CivCardPadX, HudLayout.CivCardPadX,
                HudLayout.CivCardPadTop, HudLayout.CivCardPadBottom);
            style.wordWrap = true;
            style.alignment = TextAnchor.MiddleCenter;

            float Measure(string line, int size)
            {
                style.fontSize = size;
                return style.CalcSize(new GUIContent(line ?? "")).x;
            }

            Assume.That(Measure("PATHFINDER", HudLayout.CivCardFont) > 20f, "fonts measure in this Unity mode");

            foreach (var (sw, sh) in new[] { (1920, 1080), (1280, 720) })
            {
                float card = CardWidth(sw, sh);
                float cardH = HudTextScale.Line(HudLayout.CivCardHeight);
                foreach (var def in CivCatalog.Playable())
                {
                    var face = HudLayout.LayoutCivCard(def.displayName, card, Measure, HudLayout.CivCardFont);
                    Assert.GreaterOrEqual(face.FontSize, 9, $"{def.displayName} stays readable at {sw}x{sh}");
                    Assert.IsFalse(face.HasNameTooltip, def.displayName);
                    Assert.AreEqual(def.displayName.ToUpperInvariant(), face.Shown.Replace("\n", " "));
                    StringAssert.DoesNotContain(Ellipsis, face.Shown);
                    Assert.LessOrEqual(Measure(LongestLine(face.Shown), face.FontSize), card,
                        $"{def.displayName} at {face.FontSize}px fits a {card:0.0}px chip at {sw}x{sh}");

                    style.fontSize = face.FontSize;
                    float h = style.CalcHeight(new GUIContent(face.Shown), card);
                    Assert.Greater(h, HudLayout.CivCardFont, "both lines have height");
                    Assert.LessOrEqual(h, cardH, $"{def.displayName} fits the chip height at {sw}x{sh}");
                }
            }
        }

        [Test]
        public void CutName_KeepsANameTooltip_AndDoesNotRestoreThePitchTip()
        {
            var face = HudLayout.LayoutCivCard("Pathfinder Accord", 24f, (line, size) => line.Length * size * 2f, HudLayout.CivCardFont);
            Assert.IsTrue(face.HasNameTooltip);
            Assert.AreEqual("PATHFINDER ACCORD", face.NameTooltip);
            StringAssert.Contains(Ellipsis, face.Shown);
            StringAssert.DoesNotContain("PATHFINDER ACCORD", face.Shown);
            StringAssert.DoesNotContain("health", face.NameTooltip);
            StringAssert.DoesNotContain("Expansion", face.NameTooltip);
        }

        [Test]
        public void NeutralCommons_StaysOffTheCards()
        {
            var neutral = CivCatalog.Get(CivId.None);
            Assert.AreEqual("Standard", neutral.displayName);
            Assert.IsFalse(neutral.playable);
            Assert.AreEqual("STANDARD", HudLayout.CivCardLabel(neutral.displayName));
            Assert.IsFalse(CivCatalog.Playable().Exists(c => c.id == CivId.None));
            Assert.AreEqual(CivId.None, CivCatalog.FromSaved(0), "a save from before civs load as the neutral civ");
            Assert.IsTrue(CivChoice.IsGuidedStart(CelestialBodyId.Earth, guidedStart: true));
            Assert.AreEqual(CivId.None, CivChoice.ForNewGame(CivId.PathfinderAccord, CelestialBodyId.Earth, guidedStart: true),
                "the guided Earth start still plays without a civilization");
        }

        [Test]
        public void Dialog_FitsTheView_At1080pAnd720p()
        {
            foreach (var (sw, sh) in new[] { (1920, 1080), (1280, 720) })
            {
                float scale = Scale(sw, sh);
                float viewW = sw / scale;
                float viewH = sh / scale;
                Assert.AreEqual(HudLayout.CampaignDialogDesignW, HudLayout.CampaignDialogWidth(viewW), 0.01f);
                Assert.LessOrEqual(HudLayout.NewCampaignDialogMinH, viewH - 20f, $"{sw}x{sh} can hold the dialog");
                Assert.Greater(CardWidth(sw, sh), 90f);
            }
        }

        private static float Scale(int sw, int sh) =>
            HudScaleMath.ClampToFit(HudScaleMath.AutoForHeight(sh, handheld: false), sw, sh);

        private static float CardWidth(int sw, int sh)
        {
            float viewW = sw / Scale(sw, sh);
            float dialog = HudLayout.CampaignDialogWidth(viewW);
            return HudLayout.CivCardWidth(HudLayout.DialogContentWidth(dialog), CivCatalog.Playable().Count);
        }

        private static List<string> Names(List<CivDefinition> civs)
        {
            var names = new List<string>(civs.Count);
            for (int i = 0; i < civs.Count; i++) names.Add(civs[i].displayName);
            return names;
        }

        private static string LongestLine(string label)
        {
            string longest = "";
            int start = 0;
            string text = label ?? "";
            for (int i = 0; i <= text.Length; i++)
            {
                if (i != text.Length && text[i] != '\n') continue;
                string line = text.Substring(start, i - start);
                if (line.Length > longest.Length) longest = line;
                start = i + 1;
            }
            return longest;
        }
    }
}
