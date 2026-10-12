using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class ErrorLogTests
    {
        private string _root;
        private bool _enabled;
        private int _keep;
        private int _maxBytes;
        private string _defaultDir;

        [SetUp]
        public void UseTempLogRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "sm-logs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _enabled = ErrorLog.Enabled;
            _keep = ErrorLog.KeepFiles;
            _maxBytes = ErrorLog.MaxFileBytes;
            _defaultDir = ErrorLog.DefaultDirectory;
            ErrorLog.Enabled = true;
            ErrorLog.DirectoryOverride = _root;
        }

        [TearDown]
        public void ClearTempLogRoot()
        {
            ErrorLog.DirectoryOverride = null;
            ErrorLog.Enabled = _enabled;
            ErrorLog.KeepFiles = _keep;
            ErrorLog.MaxFileBytes = _maxBytes;
            ErrorLog.DefaultDirectory = _defaultDir;
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Test]
        public void Buffer_RepeatedMessage_CountsInsteadOfFilling()
        {
            var b = new ErrorLogBuffer(8);
            for (int i = 0; i < 50; i++)
                b.Add(LogType.Warning, "nav path stalled", "", i);
            b.Add(LogType.Log, "booted", "", 60);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(51, b.TotalLines);
            Assert.AreEqual(50, b.TotalWarnings);
            var held = b.Snapshot();
            Assert.AreEqual(50, held[0].Count);
            Assert.AreEqual(0.0, held[0].FirstSeconds);
            Assert.AreEqual(49.0, held[0].LastSeconds);
        }

        [Test]
        public void Buffer_SameTextDifferentType_StaysSeparate()
        {
            var b = new ErrorLogBuffer();
            b.Add(LogType.Warning, "x", "", 0);
            b.Add(LogType.Error, "x", "", 1);
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(1, b.TotalErrors);
        }

        [Test]
        public void Buffer_RepeatMovesToNewestEnd()
        {
            var b = new ErrorLogBuffer();
            b.Add(LogType.Log, "a", "", 0);
            b.Add(LogType.Log, "b", "", 1);
            b.Add(LogType.Log, "a", "", 2);
            CollectionAssert.AreEqual(new[] { "b", "a" }, b.Snapshot().Select(e => e.Message).ToArray());
        }

        [Test]
        public void Buffer_WhenFull_DropsOldestNonErrorFirst()
        {
            var b = new ErrorLogBuffer(3);
            b.Add(LogType.Exception, "first crash", "at Foo()", 0);
            b.Add(LogType.Log, "info 1", "", 1);
            b.Add(LogType.Log, "info 2", "", 2);
            b.Add(LogType.Log, "info 3", "", 3);
            var msgs = b.Snapshot().Select(e => e.Message).ToArray();
            CollectionAssert.AreEqual(new[] { "first crash", "info 2", "info 3" }, msgs);
            Assert.AreEqual(1, b.Evicted);

            // Only errors left: the oldest error goes.
            var errors = new ErrorLogBuffer(2);
            errors.Add(LogType.Error, "e1", "", 0);
            errors.Add(LogType.Error, "e2", "", 1);
            errors.Add(LogType.Error, "e3", "", 2);
            CollectionAssert.AreEqual(new[] { "e2", "e3" }, errors.Snapshot().Select(e => e.Message).ToArray());
        }

        [Test]
        public void Buffer_EvictedMessage_CanComeBackAsNew()
        {
            var b = new ErrorLogBuffer(1);
            b.Add(LogType.Log, "a", "", 0);
            b.Add(LogType.Log, "b", "", 1);
            b.Add(LogType.Log, "a", "", 2);
            var held = b.Snapshot();
            Assert.AreEqual(1, held.Count);
            Assert.AreEqual("a", held[0].Message);
            Assert.AreEqual(1, held[0].Count);
        }

        [Test]
        public void Buffer_ClipsHugeMessagesAndStacks()
        {
            var b = new ErrorLogBuffer();
            b.Add(LogType.Error, new string('m', 10000), new string('s', 20000), 0);
            var e = b.Snapshot()[0];
            Assert.LessOrEqual(e.Message.Length, ErrorLogBuffer.MaxMessageChars + 20);
            Assert.LessOrEqual(e.StackTrace.Length, ErrorLogBuffer.MaxStackChars + 20);
            StringAssert.EndsWith("[truncated]", e.Message);
        }

        [Test]
        public void Header_CarriesVersionPlatformWorldSeedSol()
        {
            var s = new ErrorLogSession
            {
                GameVersion = "0.9.3", BuildGuid = "abc123", UnityVersion = "6000.5.10f1",
                Platform = "WindowsPlayer", OperatingSystem = "Windows 11", Device = "Desk",
                Graphics = "GPU", MemoryMb = 16384, StartedUtc = new DateTime(2026, 10, 11, 9, 30, 0, DateTimeKind.Utc),
                World = "Mars", Seed = 4242, Sol = 7, Screen = "Playing"
            };
            var b = new ErrorLogBuffer();
            b.Add(LogType.Error, "boom", "", 1);
            string h = ErrorLog.FormatHeader(s, 125.0, b);
            StringAssert.Contains("0.9.3", h);
            StringAssert.Contains("abc123", h);
            StringAssert.Contains("6000.5.10f1", h);
            StringAssert.Contains("WindowsPlayer", h);
            StringAssert.Contains("Mars", h);
            StringAssert.Contains("seed 4242", h);
            StringAssert.Contains("sol 7", h);
            StringAssert.Contains("2026-10-11 09:30:00", h);
            StringAssert.Contains("errors 1", h);
            StringAssert.Contains("up 125.0s", h);
        }

        [Test]
        public void Entry_ShowsRepeatCountAndStackOnlyForErrors()
        {
            var b = new ErrorLogBuffer();
            b.Add(LogType.Exception, "NullReferenceException: x", "GameLoop.Update ()\nUnityEngine.Foo ()\n", 2);
            b.Add(LogType.Exception, "NullReferenceException: x", "GameLoop.Update ()\n", 9);
            b.Add(LogType.Log, "hello", "Debug.Log ()\n", 3);
            var held = b.Snapshot();
            string err = ErrorLog.FormatEntry(held[0]);
            StringAssert.Contains("EXCEPTION", err);
            StringAssert.Contains("(x2, last 9.0s)", err);
            StringAssert.Contains("    GameLoop.Update ()", err);
            string info = ErrorLog.FormatEntry(held[1]);
            StringAssert.DoesNotContain("Debug.Log ()", info);
        }

        [Test]
        public void Compose_StaysUnderSizeCap_KeepingNewestLines()
        {
            var b = new ErrorLogBuffer(512);
            for (int i = 0; i < 400; i++)
                b.Add(LogType.Log, "line " + i.ToString("D3") + " " + new string('x', 200), "", i);
            string text = ErrorLog.Compose(new ErrorLogSession(), b, 400, 16 * 1024);
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(text), 16 * 1024);
            StringAssert.Contains("line 399", text);
            StringAssert.DoesNotContain("line 000", text);
            StringAssert.Contains("older lines omitted", text);
            Assert.Less(text.IndexOf("line 398", StringComparison.Ordinal), text.IndexOf("line 399", StringComparison.Ordinal),
                "lines stay oldest-first");
        }

        [Test]
        public void FileName_SortsByStartTime()
        {
            string a = ErrorLog.FileNameFor(new DateTime(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc));
            string b = ErrorLog.FileNameFor(new DateTime(2026, 11, 2, 3, 4, 5, 6, DateTimeKind.Utc));
            Assert.AreEqual("session-20260102-030405-006.log", a);
            Assert.Less(string.CompareOrdinal(a, b), 0);
            Assert.IsTrue(ErrorLog.IsSessionFile(a));
            Assert.IsFalse(ErrorLog.IsSessionFile("slot0.json"));
        }

        [Test]
        public void Prune_KeepsNewestTen_AndNeverTheCurrentFile()
        {
            for (int i = 0; i < 14; i++)
                File.WriteAllText(Path.Combine(_root, ErrorLog.FileNameFor(new DateTime(2026, 1, 1).AddMinutes(i))), "x");
            File.WriteAllText(Path.Combine(_root, "notes.txt"), "keep me");
            string current = Path.Combine(_root, ErrorLog.FileNameFor(new DateTime(2026, 1, 1)));
            int removed = ErrorLog.PruneOldLogs(_root, 10, current);
            Assert.AreEqual(4, removed);
            var left = Directory.GetFiles(_root).Select(Path.GetFileName).Where(ErrorLog.IsSessionFile).ToList();
            Assert.AreEqual(10, left.Count);
            Assert.IsTrue(File.Exists(current), "the live session file survives even when oldest");
            Assert.IsTrue(File.Exists(Path.Combine(_root, "notes.txt")));
            Assert.IsTrue(left.Contains(ErrorLog.FileNameFor(new DateTime(2026, 1, 1).AddMinutes(13))));
        }

        [Test]
        public void Record_ErrorWritesImmediately_UnderTheOverride()
        {
            ErrorLog.Begin(new ErrorLogSession { GameVersion = "t1", World = "Luna" }, DateTime.UtcNow);
            ErrorLog.Record(LogType.Log, "context line", "");
            Assert.IsNull(ErrorLog.LastWrittenPath, "ordinary lines wait for a flush");
            ErrorLog.Record(LogType.Exception, "InvalidOperationException: bad", "Foo.Bar ()");
            string path = ErrorLog.LastWrittenPath;
            Assert.IsNotNull(path);
            StringAssert.StartsWith(Path.GetFullPath(_root), Path.GetFullPath(path));
            string text = File.ReadAllText(path);
            StringAssert.Contains("context line", text);
            StringAssert.Contains("InvalidOperationException: bad", text);
            StringAssert.Contains("    Foo.Bar ()", text);
            StringAssert.Contains("Luna", text);
            Assert.AreEqual(path, ErrorLog.LatestLogPath());
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void FlushIfDirty_OnlyWritesWhenSomethingArrived()
        {
            ErrorLog.Begin(new ErrorLogSession(), DateTime.UtcNow);
            Assert.IsFalse(ErrorLog.FlushIfDirty(0));
            ErrorLog.Record(LogType.Warning, "w", "");
            Assert.IsTrue(ErrorLog.Dirty);
            Assert.IsTrue(ErrorLog.FlushIfDirty(0));
            Assert.IsFalse(ErrorLog.Dirty);
            Assert.IsFalse(ErrorLog.FlushIfDirty(0));
            ErrorLog.SetContext("Europa", 9, 3, "Paused");
            Assert.IsTrue(ErrorLog.Flush(), "explicit flush writes even when clean");
            StringAssert.Contains("sol 3", File.ReadAllText(ErrorLog.LastWrittenPath));
        }

        [Test]
        public void Disabled_RecordsNothing()
        {
            ErrorLog.Begin(new ErrorLogSession(), DateTime.UtcNow);
            ErrorLog.Enabled = false;
            ErrorLog.Record(LogType.Exception, "ignored", "");
            Assert.IsFalse(ErrorLog.Flush());
            Assert.AreEqual(0, Directory.GetFiles(_root).Length);
        }

        [Test]
        public void Record_IsThreadSafe()
        {
            ErrorLog.Begin(new ErrorLogSession(), DateTime.UtcNow, 64);
            var threads = new Thread[8];
            for (int t = 0; t < threads.Length; t++)
            {
                int id = t;
                threads[t] = new Thread(() =>
                {
                    for (int i = 0; i < 500; i++)
                        ErrorLog.Record(i % 50 == 0 ? LogType.Error : LogType.Log, "t" + id + " m" + (i % 20), "");
                });
            }
            foreach (var th in threads) th.Start();
            foreach (var th in threads) th.Join();
            Assert.AreEqual(8 * 10, ErrorLog.TotalErrors);
            Assert.IsTrue(ErrorLog.Flush());
            StringAssert.Contains("lines:     4000", File.ReadAllText(ErrorLog.LastWrittenPath));
        }

        [Test]
        public void FolderUrl_IsAFileUrl()
        {
            string url = ErrorLog.FolderUrl(_root);
            StringAssert.StartsWith("file:///", url);
            StringAssert.EndsWith("/", url);
        }
    }
}
