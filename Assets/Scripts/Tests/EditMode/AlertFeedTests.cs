using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class AlertFeedTests
    {
        private AlertFeed _feed;
        private readonly List<Alert> _sorted = new List<Alert>();

        [SetUp]
        public void SetUp() => _feed = new AlertFeed();

        [Test]
        public void Push_AddsARow()
        {
            _feed.Push("a", "Power short", AlertSeverity.Warning, 0f);

            Assert.AreEqual(1, _feed.Alerts.Count);
            Assert.AreEqual("Power short", _feed.Alerts[0].Message);
        }

        /// <summary>The whole point: forty wisp drains must not become forty rows.</summary>
        [Test]
        public void Push_SameKeyCollapsesIntoACount()
        {
            for (int i = 0; i < 40; i++)
                _feed.Push("wisp", "Wisp draining power", AlertSeverity.Warning, i * 0.1f);

            Assert.AreEqual(1, _feed.Alerts.Count);
            Assert.AreEqual(40, _feed.Alerts[0].Count);
            StringAssert.Contains("x40", _feed.Alerts[0].DisplayMessage);
        }

        [Test]
        public void Push_SameKeyAfterTheWindow_StartsFresh()
        {
            _feed.Push("wisp", "Wisp draining power", AlertSeverity.Warning, 0f);
            _feed.Push("wisp", "Wisp draining power", AlertSeverity.Warning, 500f);

            Assert.AreEqual(2, _feed.Alerts.Count);
        }

        [Test]
        public void Push_EscalatingSeverity_UpgradesAndUnacknowledges()
        {
            _feed.Push("power", "Power tight", AlertSeverity.Info, 0f);
            _feed.Acknowledge(_feed.Alerts[0]);

            _feed.Push("power", "Power failing", AlertSeverity.Critical, 1f);

            Assert.AreEqual(AlertSeverity.Critical, _feed.Alerts[0].Severity);
            Assert.IsFalse(_feed.Alerts[0].Acknowledged, "an escalation must re-raise");
        }

        [Test]
        public void Push_EmptyMessage_IsIgnored()
        {
            _feed.Push("a", "", AlertSeverity.Info, 0f);
            _feed.Push("b", null, AlertSeverity.Info, 0f);

            Assert.AreEqual(0, _feed.Alerts.Count);
        }

        [Test]
        public void Tick_FadesNonCriticalAlerts()
        {
            _feed.Push("a", "Flag posted", AlertSeverity.Info, 0f);

            _feed.Tick(_feed.FadeSeconds + 1f);

            Assert.AreEqual(0, _feed.Alerts.Count);
        }

        /// <summary>A colony that is actively dying should not quietly stop saying so.</summary>
        [Test]
        public void Tick_KeepsUnacknowledgedCriticalAlerts()
        {
            _feed.Push("a", "Life support failing", AlertSeverity.Critical, 0f);

            _feed.Tick(10000f);

            Assert.AreEqual(1, _feed.Alerts.Count);
        }

        [Test]
        public void Tick_FadesAcknowledgedCriticalAlerts()
        {
            _feed.Push("a", "Life support failing", AlertSeverity.Critical, 0f);
            _feed.Acknowledge(_feed.Alerts[0]);

            _feed.Tick(_feed.FadeSeconds + 1f);

            Assert.AreEqual(0, _feed.Alerts.Count);
        }

        [Test]
        public void Capacity_DropsTheLeastImportantEntry()
        {
            _feed.Capacity = 3;
            _feed.Push("crit", "Module lost", AlertSeverity.Critical, 0f);
            _feed.Push("i1", "Info one", AlertSeverity.Info, 1f);
            _feed.Push("i2", "Info two", AlertSeverity.Info, 2f);
            _feed.Push("i3", "Info three", AlertSeverity.Info, 3f);

            Assert.AreEqual(3, _feed.Alerts.Count);

            bool keptCritical = false;
            for (int i = 0; i < _feed.Alerts.Count; i++)
            {
                if (_feed.Alerts[i].Key == "crit") keptCritical = true;
            }
            Assert.IsTrue(keptCritical, "the critical alert must outlive routine info");
        }

        [Test]
        public void Sorted_RanksBySeverityThenRecency()
        {
            _feed.Push("i", "Info", AlertSeverity.Info, 10f);
            _feed.Push("c", "Critical", AlertSeverity.Critical, 0f);
            _feed.Push("w", "Warning", AlertSeverity.Warning, 5f);

            _feed.Sorted(_sorted);

            Assert.AreEqual("c", _sorted[0].Key);
            Assert.AreEqual("w", _sorted[1].Key);
            Assert.AreEqual("i", _sorted[2].Key);
        }

        [Test]
        public void MostUrgentWithPosition_PicksTheWorstLocatableAlert()
        {
            _feed.Push("no_pos", "Critical but nowhere", AlertSeverity.Critical, 0f);
            _feed.Push("warn", "Warning here", AlertSeverity.Warning, 1f, new Vector3(5f, 0f, 5f));

            Alert urgent = _feed.MostUrgentWithPosition();

            Assert.IsNotNull(urgent);
            Assert.AreEqual("warn", urgent.Key, "a jump target must actually have somewhere to jump to");
        }

        [Test]
        public void MostUrgentWithPosition_SkipsAcknowledged()
        {
            _feed.Push("a", "Handled", AlertSeverity.Critical, 0f, Vector3.one);
            _feed.Acknowledge(_feed.Alerts[0]);

            Assert.IsNull(_feed.MostUrgentWithPosition());
        }

        [Test]
        public void CriticalCount_IgnoresAcknowledged()
        {
            _feed.Push("a", "One", AlertSeverity.Critical, 0f);
            _feed.Push("b", "Two", AlertSeverity.Critical, 0f);
            Assert.AreEqual(2, _feed.CriticalCount);

            _feed.Acknowledge(_feed.Alerts[0]);
            Assert.AreEqual(1, _feed.CriticalCount);
        }
    }
}
