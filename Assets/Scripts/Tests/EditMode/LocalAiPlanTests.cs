using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class LocalAiPlanTests
    {
        private const string Pack = @"C:\Games\Solar Majesty\ai";

        private static readonly string[] FullPack =
        {
            @"python\python.exe",
            @"llama\llama-server.exe",
            @"models\Qwen3-1.7B-Q4_K_M.gguf",
            @"models\Qwen3-4B-Instruct-2507-Q4_K_M.gguf",
            @"models\kokoro-v1.0.onnx",
            @"models\voices-v1.0.bin",
            @"scripts\voice_server.py",
            @"scripts\model_router.py"
        };

        private static LocalAiPlan Plan(int ramMb, params string[] without)
        {
            var have = new HashSet<string>(
                FullPack.Where(f => !without.Contains(f)).Select(f => LocalAiPlan.Join(Pack, f.Split('\\'))));
            int port = 40000;
            return LocalAiPlan.Build(Pack, ramMb, 8, () => port++, have.Contains);
        }

        [Test]
        public void FullPackOnA16GbPc_RunsEverything()
        {
            var plan = Plan(16000);
            Assert.IsNull(plan.SkipReason);
            CollectionAssert.AreEqual(
                new[] { LocalAiRole.Lines, LocalAiRole.Chat, LocalAiRole.Router, LocalAiRole.Voice },
                plan.Services.Select(s => s.Role).ToArray());
            Assert.AreEqual(4, plan.Services.Select(s => s.Port).Distinct().Count(), "every service gets its own port");

            var env = plan.Environment();
            Assert.AreEqual(plan.Services.Single(s => s.Role == LocalAiRole.Router).Url, env["SOLAR_NARRATOR_URL"]);
            Assert.AreEqual(plan.Services.Single(s => s.Role == LocalAiRole.Voice).Url, env["SOLAR_VOICE_URL"]);
            Assert.AreEqual(LocalAiPlan.LinesAlias, env["SOLAR_NARRATOR_MODEL"]);
            Assert.AreEqual(LocalAiPlan.ChatAlias, env["SOLAR_CHAT_MODEL"]);
        }

        [Test]
        public void Arguments_QuotePathsWithSpacesAndBindToLoopbackOnly()
        {
            var plan = Plan(16000);
            var lines = plan.Services.Single(s => s.Role == LocalAiRole.Lines);
            StringAssert.Contains("\"C:\\Games\\Solar Majesty\\ai\\models\\Qwen3-1.7B-Q4_K_M.gguf\"", lines.Arguments);
            StringAssert.Contains("--host 127.0.0.1", lines.Arguments);
            StringAssert.Contains("--port " + lines.Port, lines.Arguments);
            StringAssert.Contains("--alias \"qwen3:1.7b\"", lines.Arguments);

            var voice = plan.Services.Single(s => s.Role == LocalAiRole.Voice);
            StringAssert.StartsWith("-I ", voice.Arguments, "bundled Python runs isolated from the tester's Python");
            StringAssert.Contains("\"C:\\Games\\Solar Majesty\\ai\\scripts\\voice_server.py\"", voice.Arguments);
        }

        [Test]
        public void EightGbPc_DropsTheChatModelButKeepsLinesAndVoices()
        {
            var plan = Plan(8000);
            CollectionAssert.AreEqual(new[] { LocalAiRole.Lines, LocalAiRole.Voice }, plan.Services.Select(s => s.Role).ToArray());
            Assert.IsFalse(plan.HasChat);
            Assert.AreEqual(plan.Services[0].Url, plan.Environment()["SOLAR_NARRATOR_URL"], "no router: the narrator talks to the lines server");
            Assert.IsFalse(plan.Environment().ContainsKey("SOLAR_CHAT_MODEL"));
            Assert.IsTrue(plan.Notes.Any(n => n.Contains("chat model skipped")));
        }

        [Test]
        public void TinyPc_RunsNothing()
        {
            var plan = Plan(4000);
            Assert.IsFalse(plan.Any);
            StringAssert.Contains("RAM", plan.SkipReason);
            Assert.AreEqual(0, plan.Environment().Count);
        }

        [Test]
        public void MissingLlamaServer_LeavesOnlyVoices()
        {
            var plan = Plan(16000, @"llama\llama-server.exe");
            CollectionAssert.AreEqual(new[] { LocalAiRole.Voice }, plan.Services.Select(s => s.Role).ToArray());
            Assert.IsFalse(plan.Environment().ContainsKey("SOLAR_NARRATOR_URL"));
            Assert.IsTrue(plan.Notes.Any(n => n.Contains("llama-server.exe")));
        }

        [Test]
        public void MissingPython_LeavesOnlyTheLinesServer()
        {
            var plan = Plan(16000, @"python\python.exe");
            CollectionAssert.AreEqual(new[] { LocalAiRole.Lines }, plan.Services.Select(s => s.Role).ToArray());
            Assert.IsFalse(plan.HasChat, "the router needs Python");
            Assert.IsFalse(plan.HasVoice);
        }

        [Test]
        public void EmptyPack_PlansNothing()
        {
            var plan = LocalAiPlan.Build(Pack, 16000, 8, () => 1, _ => false);
            Assert.IsFalse(plan.Any);
            Assert.IsNotNull(plan.SkipReason);
            Assert.IsNull(LocalAiPlan.Build(null, 16000, 8, () => 1, _ => true).NarratorUrl);
        }

        [Test]
        public void ModelServersLeaveCoresForTheGame()
        {
            var plan = LocalAiPlan.Build(Pack, 32000, 16, () => 1, new HashSet<string>(
                FullPack.Select(f => LocalAiPlan.Join(Pack, f.Split('\\')))).Contains);
            var lines = plan.Services.Single(s => s.Role == LocalAiRole.Lines);
            var chat = plan.Services.Single(s => s.Role == LocalAiRole.Chat);
            StringAssert.Contains("-t 4", lines.Arguments);
            StringAssert.Contains("-t 5", chat.Arguments);
            Assert.IsTrue(lines.BelowNormalPriority && chat.BelowNormalPriority);
        }
    }
}
