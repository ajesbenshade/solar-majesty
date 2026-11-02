using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class LocTests
    {
        [TearDown]
        public void TearDown() => Loc.SetLanguage(Loc.DefaultLanguage);

        [Test]
        public void English_IsTheSource_AndPassesThrough()
        {
            Loc.SetLanguage(Loc.DefaultLanguage);
            Assert.AreEqual("Settings", Loc.S("Settings"));
            Assert.AreEqual("1,250 EU a day", Loc.F("{0:N0} EU a day", 1250));
        }

        [Test]
        public void Format_UsesInvariantNumbers()
        {
            var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.AreEqual("1.5 m", Loc.F("{0:0.0} m", 1.5f), "a German PC must not turn 1.5 into 1,5 mid-sentence");
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = prev;
            }
        }

        [Test]
        public void Pseudo_AccentsPadsAndKeepsPlaceholders()
        {
            Loc.SetLanguage(Loc.PseudoLanguage);
            string p = Loc.S("Post {0} bounty with {key:ToolFlag}");
            StringAssert.StartsWith("[", p);
            StringAssert.EndsWith("]", p);
            StringAssert.Contains("{0}", p);
            StringAssert.Contains("{key:ToolFlag}", p, "key tokens survive so hints still expand");
            StringAssert.DoesNotContain("Post", p, "wrapped text must look different from English");
            Assert.Greater(p.Length, "Post {0} bounty with {key:ToolFlag}".Length);
            Assert.AreEqual(Loc.Pseudo("Raise {0}").Replace("{0}", "300"), Loc.F("Raise {0}", 300), "F formats the pseudo text too");
        }

        [Test]
        public void MissingTable_FallsBackToEnglish()
        {
            Loc.SetLanguage("zz");
            Assert.AreEqual(Loc.DefaultLanguage, Loc.Language);
            Assert.AreEqual("Resume", Loc.S("Resume"));
        }

        [Test]
        public void T_StillWorksForKeyedText()
        {
            Assert.AreEqual("Raise Colony Commons", Loc.T("tutorial.commons", "Raise Colony Commons"));
            Loc.SetLanguage(Loc.PseudoLanguage);
            StringAssert.StartsWith("[", Loc.T("tutorial.commons", "Raise Colony Commons"));
        }

        [Test]
        public void Available_AlwaysListsEnglishFirst()
        {
            Assert.AreEqual(Loc.DefaultLanguage, Loc.Available[0].Code);
            Assert.AreEqual("English", Loc.NameOf(Loc.DefaultLanguage));
        }
    }
}
