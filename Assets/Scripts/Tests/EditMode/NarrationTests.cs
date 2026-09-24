using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Hero narration: the request a local OpenAI-compatible server receives, how its reply is
    /// cleaned into one safe line, and which moments get the model's time. No server needed.
    /// </summary>
    public class NarrationTests
    {
        private static HeroMoment Claim(string orders = null) => new HeroMoment
        {
            Kind = NarrationKind.Claim,
            ClassName = "Defense Mech",
            Level = 7,
            Greed = 0.8f,
            Courage = 0.2f,
            Workaholic = 0.5f,
            Health01 = 0.64f,
            Fatigue01 = 0.3f,
            Credits = 120,
            World = "Luna",
            FlagType = "den",
            Bounty = 450,
            Orders = orders
        };

        [Test]
        public void Describe_GroundsTheModelInFacts()
        {
            string d = HeroNarration.Describe(Claim("stay away unless level 9+"));
            StringAssert.Contains("level 7 Defense Mech on Luna", d);
            StringAssert.Contains("greedy", d);
            StringAssert.Contains("cowardly", d);
            StringAssert.Contains("450 credits", d);
            StringAssert.Contains("stay away unless level 9+", d);

            var refused = Claim("mechs only");
            refused.Kind = NarrationKind.Refused;
            StringAssert.Contains("not allowed", HeroNarration.Describe(refused));
        }

        [Test]
        public void Request_IsValidChatJsonWithThinkingOff()
        {
            string req = HeroNarration.BuildRequest("qwen3:1.7b", Claim("say \"no\" to scouts\nplease"));
            var root = (Dictionary<string, object>)LocalJson.Parse(req);
            Assert.AreEqual("qwen3:1.7b", root["model"]);
            var messages = (List<object>)root["messages"];
            Assert.AreEqual("system", ((Dictionary<string, object>)messages[0])["role"]);
            Assert.AreEqual(0, messages.Count % 2, "system + example pairs + the real ask");
            var user = (Dictionary<string, object>)messages[messages.Count - 1];
            Assert.AreEqual("user", user["role"]);
            StringAssert.EndsWith("/no_think", (string)user["content"]);
            StringAssert.Contains("say \"no\" to scouts", (string)user["content"]);
            Assert.AreEqual(false, ((Dictionary<string, object>)root["chat_template_kwargs"])["enable_thinking"]);
        }

        [Test]
        public void Parse_ReadsOpenAIAndCompletionShapes()
        {
            const string chat = "{\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"Four hundred fifty? For a den? Fine.\"}}]}";
            Assert.IsTrue(HeroNarration.TryParseLine(chat, out string a));
            Assert.AreEqual("Four hundred fifty? For a den? Fine.", a);

            const string completion = "{\"choices\":[{\"text\":\"  Back to work.  \"}]}";
            Assert.IsTrue(HeroNarration.TryParseLine(completion, out string b));
            Assert.AreEqual("Back to work.", b);

            Assert.IsFalse(HeroNarration.TryParseLine("not json", out _));
            Assert.IsFalse(HeroNarration.TryParseLine("{\"choices\":[]}", out _));
            Assert.IsFalse(HeroNarration.TryParseLine(null, out _));
        }

        [TestCase("<think>hmm, greedy mech</think>\nPay me first.", "Pay me first.")]
        [TestCase("\"Not another den.\"", "Not another den.")]
        [TestCase("**Coward's pay** again", "Coward's pay again")]
        [TestCase("<color=red>Red alert</color>", "color=redRed alert/color")]
        [TestCase("Line one\nLine two", "Line one")]
        [TestCase("Onward \U0001F680", "Onward")]
        public void Sanitize_OneSafeLine(string raw, string expected)
        {
            Assert.AreEqual(expected, HeroNarration.Sanitize(raw));
        }

        [Test]
        public void Sanitize_RejectsJunkAndCapsLength()
        {
            Assert.IsNull(HeroNarration.Sanitize(
                "<think>\n\nOkay, the hero is a level 5 Medic on Luna, cowardly, with 25% health, and they are running back"));
            Assert.AreEqual("I've got a plan. Let's build a level 9.",
                HeroNarration.Sanitize("<think>\n\nI've got a plan. Let's build a level 9."), "line inside an unclosed think block");
            Assert.IsNull(HeroNarration.Sanitize("As an AI language model, I cannot"));
            Assert.IsNull(HeroNarration.Sanitize("  "));
            Assert.IsNull(HeroNarration.Sanitize("ok"));
            string longLine = HeroNarration.Sanitize(
                "I will march across the whole grey plain and claim every single den for a very modest fee indeed");
            Assert.LessOrEqual(longLine.Length, HeroNarration.MaxChars);
            StringAssert.EndsWith("…", longLine);
        }

        [Test]
        public void Scheduler_OneAtATimeBigMomentsAndSelectedFirst()
        {
            var s = new NarrationScheduler();
            Assert.IsFalse(s.TryStart(1, NarrationKind.Wander, selected: false, now: 0f), "routine lines only for the watched hero");
            Assert.IsTrue(s.TryStart(1, NarrationKind.Claim, selected: false, now: 0f));
            Assert.IsFalse(s.TryStart(2, NarrationKind.Claim, selected: false, now: 3f), "one request in flight");
            s.Finish();
            Assert.IsFalse(s.TryStart(2, NarrationKind.Claim, false, 0.5f), "global gap");
            Assert.IsTrue(s.TryStart(2, NarrationKind.Claim, false, 3f));
            s.Finish();
            Assert.IsFalse(s.TryStart(1, NarrationKind.Flee, false, 5f), "per-hero cooldown");
            Assert.IsTrue(s.TryStart(1, NarrationKind.Flee, false, 7f));
            s.Finish();
            Assert.IsTrue(s.TryStart(3, NarrationKind.Wander, selected: true, now: 10f), "the watched hero talks");
            // A request that never answers frees the slot after the timeout.
            Assert.IsFalse(s.TryStart(4, NarrationKind.Claim, false, 12f));
            Assert.IsTrue(s.TryStart(4, NarrationKind.Claim, false, 10f + s.Timeout + 0.1f));
        }
    }
}
