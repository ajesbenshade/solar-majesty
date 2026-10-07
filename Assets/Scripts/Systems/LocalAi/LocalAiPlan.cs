using System;
using System.Collections.Generic;

namespace SolarMajesty
{
    public enum LocalAiRole
    {
        Lines,
        Chat,
        Router,
        Voice
    }

    /// <summary>One background process of the bundled AI pack.</summary>
    public sealed class LocalAiService
    {
        public string Name;
        public LocalAiRole Role;
        public string Exe;
        public string Arguments;
        public int Port;
        public string HealthPath = "/health";
        public string LogName;
        /// <summary>Lower priority so the game keeps its frame rate while a model thinks.</summary>
        public bool BelowNormalPriority = true;

        public string Url => "http://127.0.0.1:" + Port;
    }

    /// <summary>
    /// Decides which bundled models to run on this machine, and with what arguments. Pure data:
    /// <see cref="LocalAiHost"/> starts what the plan lists. The pack lives in an <c>ai</c> folder
    /// next to the game:
    /// <code>
    /// ai/python/python.exe              portable Python with Kokoro's packages installed
    /// ai/llama/llama-server.exe         llama.cpp (CPU build)
    /// ai/models/*.gguf, kokoro, voices  the weights
    /// ai/scripts/voice_server.py, model_router.py
    /// </code>
    /// Every piece is optional: whatever is missing, or too heavy for the machine's memory, is
    /// left out, and the game plays on with the baked lines and the built-in brain.
    /// </summary>
    public sealed class LocalAiPlan
    {
        public const string LinesModelFile = "Qwen3-1.7B-Q4_K_M.gguf";
        public const string ChatModelFile = "Qwen3-4B-Instruct-2507-Q4_K_M.gguf";
        public const string VoiceModelFile = "kokoro-v1.0.onnx";
        public const string VoicesFile = "voices-v1.0.bin";
        public const string LinesAlias = "qwen3:1.7b";
        public const string ChatAlias = "qwen3:4b-instruct-2507-q4_K_M";

        /// <summary>Below this much RAM no model runs at all (the game itself wants about 3 GB).</summary>
        public const int MinRamMb = 6000;
        /// <summary>The 4B chat model needs this much to sit next to the game and the other models.</summary>
        public const int ChatRamMb = 11000;

        public readonly List<LocalAiService> Services = new List<LocalAiService>(4);
        public string SkipReason;
        public bool HasLines;
        public bool HasChat;
        public bool HasVoice;
        /// <summary>The URL the narrator talks to (the router when chat is on, else the lines server).</summary>
        public string NarratorUrl;
        public string VoiceUrl;
        /// <summary>Notes about parts left out, for the log.</summary>
        public readonly List<string> Notes = new List<string>(4);

        public bool Any => Services.Count > 0;

        public static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

        public static string Join(string dir, params string[] parts)
        {
            string p = dir.TrimEnd('/', '\\');
            for (int i = 0; i < parts.Length; i++) p += "\\" + parts[i];
            return p;
        }

        public static LocalAiPlan Build(string packDir, int ramMb, int cores, Func<int> pickPort,
            Func<string, bool> fileExists)
        {
            var plan = new LocalAiPlan();
            if (string.IsNullOrEmpty(packDir))
            {
                plan.SkipReason = "no AI pack folder";
                return plan;
            }
            if (ramMb > 0 && ramMb < MinRamMb)
            {
                plan.SkipReason = $"needs {MinRamMb / 1000} GB of RAM, this PC has {ramMb / 1000} GB";
                return plan;
            }

            string models = Join(packDir, "models");
            string llama = Join(packDir, "llama", "llama-server.exe");
            string python = Join(packDir, "python", "python.exe");
            string scripts = Join(packDir, "scripts");
            string linesGguf = Join(models, LinesModelFile);
            string chatGguf = Join(models, ChatModelFile);
            bool haveLlama = fileExists(llama);
            bool havePython = fileExists(python);
            cores = Math.Max(2, cores);

            LocalAiService lines = null, chat = null;
            if (haveLlama && fileExists(linesGguf))
            {
                lines = new LocalAiService
                {
                    Name = "Hero lines (Qwen3 1.7B)",
                    Role = LocalAiRole.Lines,
                    Exe = llama,
                    Port = pickPort(),
                    LogName = "lines"
                };
                lines.Arguments = LlamaArgs(linesGguf, LinesAlias, lines.Port, Math.Min(4, Math.Max(2, cores / 4)), 2, 4096);
                plan.HasLines = true;
            }
            else if (!haveLlama)
                plan.Notes.Add("llama-server.exe is not in the pack: no live hero lines or chat");
            else
                plan.Notes.Add(LinesModelFile + " is not in the pack: no live hero lines");

            // Chat needs the lines server too (the router falls back to it) and a Python for the router.
            if (lines != null && havePython && fileExists(chatGguf) && fileExists(Join(scripts, "model_router.py")))
            {
                if (ramMb > 0 && ramMb < ChatRamMb)
                    plan.Notes.Add($"chat model skipped: needs {ChatRamMb / 1000} GB of RAM, this PC has {ramMb / 1000} GB");
                else
                {
                    chat = new LocalAiService
                    {
                        Name = "Hero chat (Qwen3 4B)",
                        Role = LocalAiRole.Chat,
                        Exe = llama,
                        Port = pickPort(),
                        LogName = "chat"
                    };
                    chat.Arguments = LlamaArgs(chatGguf, ChatAlias, chat.Port, Math.Min(6, Math.Max(2, cores / 3)), 1, 4096);
                    plan.HasChat = true;
                }
            }

            LocalAiService router = null;
            if (chat != null)
            {
                router = new LocalAiService
                {
                    Name = "Model router",
                    Role = LocalAiRole.Router,
                    Exe = python,
                    Port = pickPort(),
                    LogName = "router",
                    HealthPath = "/v1/models",
                    BelowNormalPriority = false
                };
                router.Arguments = string.Join(" ", new[]
                {
                    "-I", Quote(Join(scripts, "model_router.py")),
                    "--port", router.Port.ToString(),
                    "--lines", lines.Url, "--chat", chat.Url,
                    "--lines-model", Quote(LinesAlias), "--chat-model", Quote(ChatAlias)
                });
            }

            LocalAiService voice = null;
            if (havePython && fileExists(Join(models, VoiceModelFile)) && fileExists(Join(models, VoicesFile)) &&
                fileExists(Join(scripts, "voice_server.py")))
            {
                voice = new LocalAiService
                {
                    Name = "Hero voices (Kokoro)",
                    Role = LocalAiRole.Voice,
                    Exe = python,
                    Port = pickPort(),
                    LogName = "voice"
                };
                voice.Arguments = string.Join(" ", new[]
                {
                    "-I", Quote(Join(scripts, "voice_server.py")),
                    "--model", Quote(Join(models, VoiceModelFile)),
                    "--voices", Quote(Join(models, VoicesFile)),
                    "--port", voice.Port.ToString()
                });
                plan.HasVoice = true;
                plan.VoiceUrl = voice.Url;
            }
            else if (!havePython)
                plan.Notes.Add("python is not in the pack: no live speech");

            if (lines != null) plan.Services.Add(lines);
            if (chat != null) plan.Services.Add(chat);
            if (router != null) plan.Services.Add(router);
            if (voice != null) plan.Services.Add(voice);
            plan.NarratorUrl = router != null ? router.Url : lines?.Url;
            if (plan.Services.Count == 0) plan.SkipReason = "the pack has nothing this PC can run";
            return plan;
        }

        /// <summary>Environment variables the game's narrator, chat and speaker already read.</summary>
        public Dictionary<string, string> Environment()
        {
            var env = new Dictionary<string, string>();
            if (NarratorUrl != null)
            {
                env["SOLAR_NARRATOR_URL"] = NarratorUrl;
                env["SOLAR_NARRATOR_MODEL"] = LinesAlias;
                if (HasChat) env["SOLAR_CHAT_MODEL"] = ChatAlias;
            }
            if (VoiceUrl != null) env["SOLAR_VOICE_URL"] = VoiceUrl;
            return env;
        }

        private static string LlamaArgs(string gguf, string alias, int port, int threads, int parallel, int ctx)
        {
            return string.Join(" ", new[]
            {
                "-m", Quote(gguf),
                "--alias", Quote(alias),
                "--host", "127.0.0.1",
                "--port", port.ToString(),
                "-c", ctx.ToString(),
                "-np", parallel.ToString(),
                "-t", threads.ToString(),
                "--jinja", "--reasoning-budget", "0"
            });
        }
    }
}
