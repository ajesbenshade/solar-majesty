using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    public class HudSafetyTests
    {
        [SetUp]
        public void SetUp() => HudSafety.Reset();

        [Test]
        public void LogOnce_LogsFirstTimeOnly()
        {
            var e = new InvalidOperationException("boom");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");

            Assert.IsTrue(HudSafety.LogOnce("a", e));
            Assert.IsFalse(HudSafety.LogOnce("a", e));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LogOnce_DistinctKeysLogSeparately()
        {
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: one");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: two");

            Assert.IsTrue(HudSafety.LogOnce("one", new InvalidOperationException("one")));
            Assert.IsTrue(HudSafety.LogOnce("two", new InvalidOperationException("two")));
        }
    }
}
