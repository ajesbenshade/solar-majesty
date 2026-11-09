// Recorded overseer commands on disk — pure C#, no MonoBehaviour.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// A session's applied commands with enough about the world to replay them: which body, which
    /// seed, how far the clock ran and what the scene-free state hashed to at the end.
    /// </summary>
    [Serializable]
    public sealed class CommandLog
    {
        /// <summary>Launch argument that turns recording on.</summary>
        public const string LaunchArgument = "-recordcommands";

        public int version = 1;
        public string gameVersion = "";
        public string body = "";
        /// <summary>The world's text key (<see cref="CelestialBodyProfile.Key"/>).</summary>
        public string bodyKey = "";
        public int seed;
        public RunRules rules;
        /// <summary>Simulation steps run when the log was written.</summary>
        public long steps;
        /// <summary><see cref="ColonySim.StateHash"/> at <see cref="steps"/>, as hex.</summary>
        public string stateHash = "";
        /// <summary>The scene's hash over the sim plus robots and fauna. Empty when written without a scene.</summary>
        public string worldHash = "";
        public List<OverseerCommand> commands = new List<OverseerCommand>();

        public static bool Requested(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], LaunchArgument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>Snapshot a sim's recorded commands. The list is copied, so the sim can keep running.</summary>
        public static CommandLog From(ColonySim sim, string body, int seed, string gameVersion)
        {
            var log = new CommandLog
            {
                gameVersion = gameVersion ?? "",
                body = body ?? "",
                bodyKey = sim.BodyKey ?? "",
                seed = seed,
                rules = sim.Rules,
                steps = sim.Clock.TotalSteps,
                stateHash = sim.StateHash().ToString("x16")
            };
            log.commands.AddRange(sim.Commands.Log);
            return log;
        }

        public static string FileName(string body, int seed, DateTime utc) =>
            $"commands-{(string.IsNullOrEmpty(body) ? "world" : body.ToLowerInvariant())}-{seed}-{utc:yyyyMMdd-HHmmss}.json";

        /// <summary>Write the log as JSON. Returns the path, or null when the disk refused.</summary>
        public string Write(string directory, string fileName)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, fileName);
                File.WriteAllText(path, JsonUtility.ToJson(this, true));
                return path;
            }
            catch (Exception e)
            {
                // Recording is a diagnostic. It must never take the game down with it.
                Debug.LogWarning("[CommandLog] Write failed: " + e.Message);
                return null;
            }
        }

        public static bool TryRead(string path, out CommandLog log)
        {
            log = null;
            try
            {
                if (!File.Exists(path)) return false;
                log = JsonUtility.FromJson<CommandLog>(File.ReadAllText(path));
                return log != null && log.commands != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CommandLog] Read failed: " + e.Message);
                log = null;
                return false;
            }
        }
    }
}
