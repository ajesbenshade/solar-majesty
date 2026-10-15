using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class SaveDirArgumentTests
    {
        [Test]
        public void SaveDir_SpaceAndEquals_SetTheOverride_AbsentLeavesItAlone()
        {
            string previous = SaveSystem.DirectoryOverride;
            try
            {
                SaveSystem.DirectoryOverride = null;
                Assert.IsFalse(SaveSystem.TryParseSaveDirArgument(null, out _));
                Assert.IsFalse(SaveSystem.TryParseSaveDirArgument(new[] { "SolarMajesty" }, out _));
                Assert.IsFalse(SaveSystem.TryParseSaveDirArgument(new[] { "SolarMajesty", "-batchmode" }, out _));

                SaveSystem.ApplySaveDirArgument(new[] { "SolarMajesty", "-batchmode" });
                Assert.IsNull(SaveSystem.DirectoryOverride, "no -saveDir keeps the build default");

                Assert.IsTrue(SaveSystem.TryParseSaveDirArgument(
                    new[] { "SolarMajesty", "-saveDir", "/tmp/sm-saves" }, out string spaced));
                Assert.AreEqual("/tmp/sm-saves", spaced);
                Assert.IsTrue(SaveSystem.TryParseSaveDirArgument(
                    new[] { "SolarMajesty", "-saveDir=/tmp/sm-eq" }, out string equals));
                Assert.AreEqual("/tmp/sm-eq", equals);
                Assert.IsTrue(SaveSystem.TryParseSaveDirArgument(
                    new[] { "SolarMajesty", "-SAVEDIR=/tmp/sm-case" }, out string ignoreCase));
                Assert.AreEqual("/tmp/sm-case", ignoreCase);

                SaveSystem.ApplySaveDirArgument(new[] { "SolarMajesty", "-saveDir", "/tmp/from-space" });
                Assert.AreEqual("/tmp/from-space", SaveSystem.DirectoryOverride);

                SaveSystem.ApplySaveDirArgument(new[] { "SolarMajesty", "-other" });
                Assert.AreEqual("/tmp/from-space", SaveSystem.DirectoryOverride,
                    "a later launch line with no -saveDir must not clear the directory");

                SaveSystem.ApplySaveDirArgument(new[] { "SolarMajesty", "-saveDir" });
                SaveSystem.ApplySaveDirArgument(new[] { "SolarMajesty", "-saveDir=" });
                SaveSystem.ApplySaveDirArgument(new[] { "SolarMajesty", "-saveDir", "-batchmode" });
                Assert.AreEqual("/tmp/from-space", SaveSystem.DirectoryOverride,
                    "an empty or flag-like value is not a directory");

                SaveSystem.DirectoryOverride = null;
                SaveSystem.ApplySaveDirArgument(new[] { "SolarMajesty", "-saveDir=/tmp/from-equals" });
                Assert.AreEqual("/tmp/from-equals", SaveSystem.DirectoryOverride);
            }
            finally
            {
                SaveSystem.DirectoryOverride = previous;
            }
        }

        [Test]
        public void PlayerBuild_AppliesSaveDirBeforeAnyScene_AndLogsOnce()
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Systems/SaveSystem.cs"));
            int guard = text.IndexOf("#if !UNITY_EDITOR");
            int hook = text.IndexOf("SubsystemRegistration");
            int apply = text.IndexOf("ApplySaveDirArgument(System.Environment.GetCommandLineArgs())");
            int log = text.IndexOf("LogSaveDirectoryOnce();");
            Assert.GreaterOrEqual(guard, 0, "the command-line hook is player-only so EditMode keeps its own override");
            Assert.Greater(hook, guard);
            Assert.Greater(apply, hook);
            Assert.Greater(log, apply);
        }
    }
}
