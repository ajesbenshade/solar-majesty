using System.Collections.Generic;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Hero conversation: the streaming request, how server-sent deltas are read, and how replies
    /// are tidied for the chat panel and speech bubble. No server needed.
    /// </summary>
    public class ConversationTests
    {
        private static HeroPersona Rivet() => new HeroPersona
        {
            Name = "Rivet",
            Designation = "Nightshift",
            ClassName = "Engineer Bot",
            Level = 3,
            Rank = "Serving",
            Greed = 0.8f,
            Courage = 0.5f,
            Workaholic = 0.9f,
            Health01 = 0.82f,
            Fatigue01 = 0.35f,
            Credits = 140,
            World = "Mars",
            Situation = "working a shift at the guild workshop",
            Recent = new[] { "took the build bounty for 300 credits", "reached level 3" }
        };

        [Test]
        public void SystemPrompt_GroundsThePersona()
        {
            string p = HeroConversation.SystemPrompt(Rivet());
            StringAssert.Contains("Rivet \"Nightshift\"", p);
            StringAssert.Contains("level 3 Engineer Bot", p);
            StringAssert.Contains("on Mars", p);
            StringAssert.Contains("greedy", p);
            StringAssert.Contains("working a shift at the guild workshop", p);
            StringAssert.Contains("took the build bounty for 300 credits; reached level 3", p);
            StringAssert.EndsWith("/no_think", p);
        }

        [Test]
        public void Request_StreamsWithThinkingOffAndTrimsHistory()
        {
            var history = new List<ChatLine>();
            for (int i = 0; i < 12; i++) history.Add(new ChatLine(i % 2 == 0, "line " + i));
            history.Add(new ChatLine(true, "say \"hi\"\nplease"));
            string req = HeroConversation.BuildRequest("qwen3:1.7b", Rivet(), history);

            var root = (Dictionary<string, object>)LocalJson.Parse(req);
            Assert.AreEqual(true, root["stream"]);
            Assert.AreEqual("none", root["reasoning_effort"], "Ollama only turns thinking off this way");
            Assert.AreEqual(false, ((Dictionary<string, object>)root["chat_template_kwargs"])["enable_thinking"]);
            var messages = (List<object>)root["messages"];
            Assert.AreEqual("system", ((Dictionary<string, object>)messages[0])["role"]);
            Assert.LessOrEqual(messages.Count - 1, HeroConversation.HistoryLines);
            Assert.AreEqual("user", ((Dictionary<string, object>)messages[1])["role"], "a reply never opens the window");
            var last = (Dictionary<string, object>)messages[messages.Count - 1];
            Assert.AreEqual("user", last["role"]);
            Assert.AreEqual("say \"hi\"\nplease", last["content"]);
        }

        [Test]
        public void Ambient_RequestAlsoTurnsThinkingOffForOllama()
        {
            var m = new HeroMoment { Kind = NarrationKind.Rest, ClassName = "Medic", Level = 2 };
            var root = (Dictionary<string, object>)LocalJson.Parse(HeroNarration.BuildRequest("qwen3:1.7b", m));
            Assert.AreEqual("none", root["reasoning_effort"]);
        }

        [Test]
        public void Delta_ReadsPiecesAndTheEnd()
        {
            Assert.IsTrue(HeroConversation.TryParseDelta(
                "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Three \"},\"finish_reason\":null}]}",
                out string piece, out bool done));
            Assert.AreEqual("Three ", piece);
            Assert.IsFalse(done);

            Assert.IsTrue(HeroConversation.TryParseDelta(
                "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}", out piece, out done));
            Assert.AreEqual("", piece);
            Assert.IsTrue(done);

            Assert.IsTrue(HeroConversation.TryParseDelta("data: [DONE]", out _, out done));
            Assert.IsTrue(done);

            Assert.IsFalse(HeroConversation.TryParseDelta("", out _, out _));
            Assert.IsFalse(HeroConversation.TryParseDelta(": keep-alive", out _, out _));
            Assert.IsFalse(HeroConversation.TryParseDelta("data: {not json", out _, out _));
        }

        [Test]
        public void StreamingView_HidesThinkingUntilItCloses()
        {
            Assert.AreEqual("", HeroConversation.StreamingView("<think>\nhmm the user"));
            Assert.AreEqual("Pay me", HeroConversation.StreamingView("<think>\n\n</think>\n\nPay me"));
            Assert.AreEqual("Pay me first.", HeroConversation.StreamingView("</think> Pay  me\nfirst."));
        }

        [Test]
        public void CleanReply_TidiesForTheHud()
        {
            Assert.AreEqual("Pay me first. I don't do charity.",
                HeroConversation.CleanReply("<think></think>\n*wipes oil off hands* \"Pay me first. I don't do charity.\""));
            Assert.AreEqual("Sure thing.", HeroConversation.CleanReply("Rivet: Sure thing.", "Rivet"));
            Assert.AreEqual("Look: I need credits.", HeroConversation.CleanReply("Look: I need credits.", "Rivet"),
                "only the hero's own name is stripped as a speaker label");
            Assert.AreEqual("Workshop's loud. Pay's fair.",
                HeroConversation.CleanReply("Workshop's loud. Pay's fair. And the coffee is", "Rivet"),
                "a reply cut off by the token cap ends on its last whole sentence");
            Assert.IsNull(HeroConversation.CleanReply("As an AI language model, I cannot."));
            Assert.IsNull(HeroConversation.CleanReply("   "));

            string longReply = new string('a', 200) + ". " + new string('b', 200) + ".";
            Assert.LessOrEqual(HeroConversation.CleanReply(longReply).Length, HeroConversation.MaxReplyChars);
        }

        [Test]
        public void CleanReply_DropsHelpdeskTailsAndEchoes()
        {
            var history = new List<ChatLine>
            {
                new ChatLine(true, "How's work?"),
                new ChatLine(false, "Frontier's rough. Let me know when you want me to pull a shift.")
            };
            Assert.AreEqual("Thirty credits and the den's gone.",
                HeroConversation.CleanReply("Thirty credits and the den's gone. Let me know when you want me to pull a shift.", "Jute", history));
            Assert.AreEqual("Pay first.",
                HeroConversation.CleanReply("Frontier's rough. Pay first.", "Jute", history), "no echo of an earlier line");
            Assert.AreEqual("Let me know.", HeroConversation.CleanReply("Let me know.", "Jute", history),
                "never drops the whole reply");
        }

        [Test]
        public void PlayerLine_IsOneCappedLine()
        {
            Assert.AreEqual("hello there", HeroConversation.CleanPlayerLine("  hello\u0007 there  "));
            Assert.IsNull(HeroConversation.CleanPlayerLine("   "));
            Assert.AreEqual(HeroConversation.MaxPlayerChars, HeroConversation.CleanPlayerLine(new string('x', 400)).Length);
        }

        [Test]
        public void PickModel_PrefersKnownSmallModels()
        {
            string ollama = "{\"object\":\"list\",\"data\":[{\"id\":\"big-27b:Q5\"},{\"id\":\"qwen3:1.7b\"},{\"id\":\"qwen3:4b\"}]}";
            Assert.AreEqual("qwen3:1.7b", HeroConversation.PickModel(ollama, new[] { "qwen3:1.7b", "qwen3:0.6b" }));
            string llama = "{\"data\":[{\"id\":\"unsloth/Qwen3-1.7B-GGUF\"}]}";
            Assert.IsNull(HeroConversation.PickModel(llama, new[] { "qwen3:1.7b" }), "keep the configured name");
            Assert.IsNull(HeroConversation.PickModel("nope", new[] { "qwen3:1.7b" }));
        }

        [Test]
        public void ChatLog_KeepsRecentLinesAndDistinctEvents()
        {
            var log = new HeroChatLog();
            for (int i = 0; i < HeroChatLog.MaxLines + 5; i++) log.Add(i % 2 == 0, "l" + i);
            Assert.AreEqual(HeroChatLog.MaxLines, log.Lines.Count);
            log.Remember("reached level 2");
            log.Remember("reached level 2");
            log.Remember(null);
            Assert.AreEqual(1, log.Events.Count);

            var claim = new HeroMoment { Kind = NarrationKind.Claim, FlagType = "den", Bounty = 450 };
            Assert.AreEqual("took the den bounty for 450 credits", HeroConversation.EventLine(claim));
            Assert.IsNull(HeroConversation.EventLine(new HeroMoment { Kind = NarrationKind.Wander }));
        }

        [Test]
        public void ErrorMessage_ReadsBothShapes()
        {
            Assert.AreEqual("model \"local\" not found",
                HeroConversation.ErrorMessage("{\"error\":{\"message\":\"model \\\"local\\\" not found\",\"type\":\"api_error\"}}"));
            Assert.AreEqual("boom", HeroConversation.ErrorMessage("{\"error\":\"boom\"}"));
            Assert.IsNull(HeroConversation.ErrorMessage("<html>"));
        }
    }
}
