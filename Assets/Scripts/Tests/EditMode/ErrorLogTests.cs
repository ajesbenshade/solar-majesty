using System.IO;
using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class ErrorLogTests
    {
        [Test]
        public void Prune_KeepsTheNewestSessions()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sm-errorlog-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                for (int i = 0; i < 7; i++)
                {
                    string f = Path.Combine(dir, $"errors-2026010{i}-000000.log");
                    File.WriteAllText(f, "x");
                    File.SetLastWriteTimeUtc(f, new System.DateTime(2026, 1, 1 + i));
                }
                File.WriteAllText(Path.Combine(dir, "keep-me.txt"), "not a session log");

                ErrorLog.Prune(dir, 4);

                Assert.That(Directory.GetFiles(dir, "errors-*.log").Length, Is.EqualTo(4));
                Assert.That(File.Exists(Path.Combine(dir, "errors-20260106-000000.log")), Is.True, "newest kept");
                Assert.That(File.Exists(Path.Combine(dir, "errors-20260100-000000.log")), Is.False, "oldest pruned");
                Assert.That(File.Exists(Path.Combine(dir, "keep-me.txt")), Is.True);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Prune_MissingFolder_DoesNothing()
        {
            Assert.DoesNotThrow(() => ErrorLog.Prune(Path.Combine(Path.GetTempPath(), "sm-nope-" + System.Guid.NewGuid()), 3));
        }
    }
}
