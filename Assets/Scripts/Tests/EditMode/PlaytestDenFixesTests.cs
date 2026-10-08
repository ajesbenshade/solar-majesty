using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Playtest fixes from the Sol 164 Earth save: den HP, the stuck watch,
    /// stacked bounties, recovery, kill credit, scrap names, purse batching,
    /// the scale slider, and stacked flag labels.
    /// </summary>
    public class PlaytestDenFixesTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
        }

        [Test]
        public void DenStructure_BreakingItClears_AndStragglersDoNotPay()
        {
            var lair = MakeLair();
            var stalker = new GameObject("straggler").AddComponent<DustStalkerAgent>();
            _created.Add(stalker.gameObject);
            stalker.SetHealthForTests(40f);
            lair.BindRestored(stalker);

            int threats = 0;
            int clears = 0;
            Application.LogCallback onLog = (cond, stack, type) =>
            {
                if (cond == null) return;
                if (cond.IndexOf("[Threat]", System.StringComparison.Ordinal) >= 0) threats++;
                if (cond.IndexOf("[Lair] Cleared", System.StringComparison.Ordinal) >= 0) clears++;
            };
            Application.logMessageReceived += onLog;
            try
            {
                Assert.IsFalse(lair.ApplyStructureDamage(10f, null));
                Assert.IsFalse(lair.IsCleared);
                Assert.AreEqual(OverseerRules.DenStructureHp - 10f, lair.StructureHp, 0.01f);
                lair.Tick();
                Assert.IsFalse(lair.IsCleared, "a living swarm is not the clear rule");

                Assert.IsTrue(lair.ApplyStructureDamage(OverseerRules.DenStructureHp, null));
                Assert.IsTrue(lair.IsCleared);
                Assert.AreEqual(1, clears);
                Assert.AreEqual(0, threats, "stragglers scatter without a kill bounty");
                Assert.IsTrue(stalker == null);
            }
            finally
            {
                Application.logMessageReceived -= onLog;
            }
        }

        [Test]
        public void FaunaRestoreCap_KeepsEightHealthiestPerDen()
        {
            var lair = new int[13];
            var health = new float[13];
            for (int i = 0; i < 10; i++)
            {
                lair[i] = 0;
                health[i] = i + 1f;
            }
            lair[10] = 1;
            health[10] = 0.2f;
            lair[11] = -1;
            health[11] = 0.9f;
            lair[12] = -1;
            health[12] = 0.1f;

            bool[] keep = FaunaRestoreCap.Keep(lair, health, OverseerRules.MaxFaunaPerDen);
            int den0 = 0;
            for (int i = 0; i < 10; i++)
            {
                if (!keep[i]) continue;
                den0++;
                Assert.Greater(health[i], 2f, "the two weakest on den 0 are dropped");
            }
            Assert.AreEqual(8, den0);
            Assert.IsTrue(keep[10]);
            Assert.IsTrue(keep[11]);
            Assert.IsTrue(keep[12]);
        }

        [Test]
        public void Watch_CombatOrMovement_DoesNotRelease_IdleDoes()
        {
            var moving = new ClearThreatStuckWatch();
            moving.Note(80f, 1f, false, false, out _, out _);
            for (int i = 0; i < 25; i++)
                moving.Note(80f, 1f, true, false, out _, out bool release);
            Assert.IsFalse(moving.Released);

            var fighting = new ClearThreatStuckWatch();
            fighting.Note(80f, 1f, false, false, out _, out _);
            for (int i = 0; i < 25; i++)
                fighting.Note(80f, 1f, false, true, out _, out _);
            Assert.IsFalse(fighting.Released);

            var stuck = new ClearThreatStuckWatch();
            stuck.Note(80f, 1f, false, false, out _, out _);
            bool repath = false;
            bool released = false;
            for (int i = 0; i < 10; i++)
                stuck.Note(80f, 1f, false, false, out repath, out released);
            Assert.IsTrue(repath);
            Assert.IsFalse(released);
            for (int i = 0; i < 9; i++)
                stuck.Note(80f, 1f, false, false, out repath, out released);
            Assert.IsFalse(released);
            stuck.Note(80f, 1f, false, false, out repath, out released);
            Assert.IsTrue(released);
        }

        [Test]
        public void FightingHero_DoesNotDropTheClaim()
        {
            var flags = new FlagManager();
            var hero = MakeHero(flags);
            var flag = PostClear(flags, new Vector3(48f, 0f, 0f));
            hero.SuppressTravelForTests = true;
            var mite = new GameObject("mite").AddComponent<DustStalkerAgent>();
            _created.Add(mite.gameObject);
            mite.SetHealthForTests(100000f);
            mite.transform.position = hero.transform.position;
            hero.ThreatsOverride = new List<DustStalkerAgent> { mite };

            int hits = 0;
            Application.LogCallback onLog = (cond, stack, type) =>
            {
                if (cond != null && cond.IndexOf("released the claim", System.StringComparison.Ordinal) >= 0)
                    hits++;
            };
            Application.logMessageReceived += onLog;
            try
            {
                Sim(hero, 30f);
                Assert.AreEqual(0, hits);
                Assert.AreSame(flag, hero.ActiveFlag);
                Assert.Less(mite.Health01, 1f);

                mite.transform.position = new Vector3(400f, 0f, 400f);
                Sim(hero, 28f);
                Assert.AreEqual(1, hits);
                Assert.IsNull(hero.ActiveFlag);
            }
            finally
            {
                Application.logMessageReceived -= onLog;
            }
        }

        [Test]
        public void ClearThreatMerge_SumsEscrow_AndPaysOnce()
        {
            var flags = new FlagManager();
            var data = ClearData();
            Vector3 den = new Vector3(12f, 0f, -4f);
            for (int i = 0; i < 3; i++)
            {
                var flag = flags.Post(data, den + new Vector3(i, 0f, 0f), 1600f);
                flag.CurrentBounty = 1600f;
                flag.EscrowMetals = 1600;
            }
            var far = flags.Post(data, den + new Vector3(100f, 0f, 0f), 1600f);
            far.EscrowMetals = 1600;

            Assert.AreEqual(2, ClearThreatMerge.CollapseAll(flags, OverseerRules.ClearThreatSameDenMeters));
            Assert.AreEqual(2, flags.Flags.Count);
            int nearEscrow = 0;
            for (int i = 0; i < flags.Flags.Count; i++)
            {
                float dx = flags.Flags[i].WorldPosition.x - den.x;
                if (dx * dx < 18f * 18f)
                    nearEscrow = flags.Flags[i].EscrowMetals;
            }
            Assert.AreEqual(4800, nearEscrow);

            int pay = ClearThreatMerge.Take(flags, den, OverseerRules.ClearThreatSameDenMeters, out int escrow);
            Assert.AreEqual(4800, pay);
            Assert.AreEqual(4800, escrow);
            Assert.AreEqual(1, flags.Flags.Count);
            Assert.AreSame(far, flags.Flags[0]);
        }

        [Test]
        public void TryPost_FoldsASecondPoleOntoTheSameDen()
        {
            var flags = new FlagManager();
            var data = ClearData();
            Vector3 den = new Vector3(3f, 0f, 3f);
            var first = FlagBountySync.TryPost(flags, null, data, den, 1600f);
            var second = FlagBountySync.TryPost(flags, null, data, den + new Vector3(4f, 0f, 1f), 1600f);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, flags.Flags.Count);
            Assert.AreEqual(3200, first.EscrowMetals);
            Assert.AreEqual(3200f, first.CurrentBounty, 0.01f);
        }

        [Test]
        public void RecoveredHero_IsInvulnerableThenRetreatsHome()
        {
            var flags = new FlagManager();
            var hero = MakeHero(flags);
            hero.transform.position = new Vector3(400f, 0f, 400f);
            hero.ApplyDamage(5f);
            Assert.IsTrue(hero.IsIncapacitated);

            Sim(hero, OverseerRules.RecoverSeconds);
            Assert.IsFalse(hero.IsIncapacitated);
            Assert.IsTrue(hero.IsUntargetable);
            float hp = hero.HealthNormalized;
            hero.ApplyDamage(0.5f);
            Assert.AreEqual(hp, hero.HealthNormalized, 0.001f, "the stand-up window ignores bites");

            Sim(hero, OverseerRules.RecoverInvulnSeconds + 0.25f);
            Assert.AreEqual("flee_to_inn", hero.LastReason);
            Assert.AreEqual(SpecialistAction.Flee, hero.CurrentAction);
            Assert.IsTrue(hero.IsUntargetable, "the walk home is not a new fight");
            float after = hero.HealthNormalized;
            hero.ApplyDamage(0.2f);
            Assert.Less(hero.HealthNormalized, after);
        }

        [Test]
        public void KillPayee_IsTheLastHeroHit_TurretOnlyStaysUnpaid()
        {
            var flags = new FlagManager();
            var heroA = MakeHero(flags);
            var heroB = MakeHero(flags);
            var stalker = new GameObject("prey").AddComponent<DustStalkerAgent>();
            _created.Add(stalker.gameObject);
            stalker.SetHealthForTests(100f);
            stalker.ApplyCombatDamage(10f, heroA);
            stalker.ApplyCombatDamage(40f, heroB);
            stalker.ApplyCombatDamage(5f, heroA);
            Assert.AreSame(heroA, stalker.KillPayeeForTests);

            stalker.ApplyCombatDamage(10f, null);
            Assert.AreSame(heroA, stalker.KillPayeeForTests, "a turret hit does not steal a hero's kill");

            var turret = new GameObject("turret-kill").AddComponent<DustStalkerAgent>();
            _created.Add(turret.gameObject);
            turret.SetHealthForTests(10f);
            turret.ApplyCombatDamage(1000f, null);
            Assert.IsNull(turret.KillPayeeForTests);
            Assert.AreEqual(1, turret.KillCreditsIssued);
            turret.ApplyCombatDamage(1000f, heroA);
            Assert.AreEqual(1, turret.KillCreditsIssued);
        }

        [Test]
        public void ScrapRoster_KeepsNamedWreckBesideALivingSibling()
        {
            var living = new List<SpecialistRecord>
            {
                new SpecialistRecord { Class = SpecialistClass.DefenseMech, Name = "Jute", Corpse = false }
            };
            var basalt = new SpecialistRecord
            {
                Class = SpecialistClass.DefenseMech, Name = "Basalt", Corpse = true
            };
            var unnamed = new SpecialistRecord { Class = SpecialistClass.DefenseMech, Corpse = true };
            Assert.IsTrue(ScrapRoster.ShouldKeepCorpse(living, basalt));
            Assert.IsFalse(ScrapRoster.ShouldKeepCorpse(living, unnamed));
            Assert.IsFalse(ScrapRoster.ShouldKeepCorpse(living, living[0]));
        }

        [Test]
        public void Roster_RoundTripsTheServiceName()
        {
            var saved = new List<SpecialistRecord>
            {
                new SpecialistRecord
                {
                    Class = SpecialistClass.DefenseMech,
                    Level = 4,
                    Xp = 12,
                    Credits = 80,
                    ReviveCount = 1,
                    Corpse = true,
                    Name = "Basalt"
                },
                new SpecialistRecord
                {
                    Class = SpecialistClass.DefenseMech,
                    Level = 2,
                    Name = "Jute"
                }
            };
            var back = new List<SpecialistRecord>();
            Assert.IsTrue(SpecialistRoster.TryDecode(SpecialistRoster.Encode(saved), back));
            Assert.AreEqual(2, back.Count);
            Assert.AreEqual("Basalt", back[0].Name);
            Assert.IsTrue(back[0].Corpse);
            Assert.AreEqual("Jute", back[1].Name);
            Assert.IsFalse(back[1].Corpse);

            var legacy = new List<SpecialistRecord>();
            Assert.IsTrue(SpecialistRoster.TryDecode("1|2:3:0:0:0:0:1", legacy));
            Assert.AreEqual(1, legacy.Count);
            Assert.IsTrue(string.IsNullOrEmpty(legacy[0].Name));
            Assert.IsTrue(legacy[0].Corpse);
        }

        [Test]
        public void PurseTheftWindow_OneLinePerMinute()
        {
            var window = new PurseTheftWindow(OverseerRules.PurseTheftWindowSeconds);
            Assert.IsFalse(window.Note(0f, 10, "Solar Farm", out string first));
            Assert.IsNull(first);
            window.Note(5f, 20, "HAB-1", out _);
            window.Note(12f, 15, "Solar Farm", out _);
            window.Note(20f, 15, "Lab", out _);
            Assert.AreEqual(60, window.Amount);
            Assert.AreEqual(3, window.Buildings);
            Assert.IsFalse(window.Flush(59f, false, out _));
            Assert.IsTrue(window.Flush(60f, false, out string line));
            Assert.AreEqual("Pests lifted 60 EU from 3 building purses.", line);
            Assert.AreEqual(
                "Pests lifted 10 EU from 1 building purse.",
                CompactGrok.PestsLifted(10, 1));
            StringAssert.Contains("20 EU walked off HAB-1", CompactGrok.LevyStolen(20, "HAB-1"));

            var manifest = Resources.Load<TextAsset>("Audio/Voices/voices");
            if (manifest != null)
            {
                var bank = VoiceBank.Parse(manifest.text);
                Assert.IsFalse(bank.TryGetLine(line, out _), "the batched purse line stays text-only");
            }
        }

        [Test]
        public void HudScaleGesture_CommitsOnRelease()
        {
            var gesture = new HudScaleGesture();
            gesture.SyncApplied(1f);
            gesture.Drag(1.4f);
            Assert.IsTrue(gesture.Dragging);
            Assert.AreEqual(1f, gesture.Applied, 0.001f);
            Assert.AreEqual(1.4f, gesture.Shown, 0.001f);
            Assert.IsFalse(gesture.TryCommit(false, out _));
            Assert.AreEqual(1f, gesture.Applied, 0.001f);
            Assert.IsTrue(gesture.TryCommit(true, out float commit));
            Assert.AreEqual(1.4f, commit, 0.001f);
            Assert.AreEqual(1.4f, gesture.Applied, 0.001f);
            Assert.IsFalse(gesture.Dragging);
        }

        [Test]
        public void FlagLabelStack_MergesSameTarget()
        {
            var items = new List<FlagLabelStack.Item>(15);
            for (int i = 0; i < 14; i++)
            {
                items.Add(new FlagLabelStack.Item
                {
                    World = new Vector3(8f, 0f, 8f),
                    Title = "Clear Threat",
                    Bounty = 1600,
                    Order = i,
                    Type = FlagType.ClearThreat
                });
            }
            items.Add(new FlagLabelStack.Item
            {
                World = new Vector3(200f, 0f, 0f),
                Title = "Clear Threat",
                Bounty = 1600,
                Order = 0,
                Type = FlagType.ClearThreat
            });

            int shown = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (FlagLabelStack.IsRepresentative(items, i, OverseerRules.ClearThreatSameDenMeters))
                    shown++;
            }
            Assert.AreEqual(2, shown);
            Assert.IsTrue(FlagLabelStack.IsRepresentative(items, 0, OverseerRules.ClearThreatSameDenMeters));
            Assert.IsFalse(FlagLabelStack.IsRepresentative(items, 13, OverseerRules.ClearThreatSameDenMeters));
            FlagLabelStack.Sum(items, 0, OverseerRules.ClearThreatSameDenMeters, out int count, out int bounty);
            Assert.AreEqual(14, count);
            Assert.AreEqual(22400, bounty);
            Assert.AreEqual(
                "Clear Threat ×14 — " + 22400.ToString("N0") + " EU",
                FlagLabelStack.Caption("Clear Threat", count, bounty));
        }

        private StalkerLair MakeLair()
        {
            var go = new GameObject("den");
            _created.Add(go);
            return go.AddComponent<StalkerLair>();
        }

        private FlagData ClearData()
        {
            var data = ScriptableObject.CreateInstance<FlagData>();
            _created.Add(data);
            data.flagType = FlagType.ClearThreat;
            data.displayName = "Clear Threat";
            data.minBounty = 0;
            data.maxBounty = 100000;
            data.defaultBounty = 1600;
            data.workRequired = 80f;
            data.baseRisk = 0.4f;
            data.stronglyAttracts = new[] { SpecialistClass.DefenseMech };
            return data;
        }

        private FlagHandle PostClear(FlagManager flags, Vector3 den)
        {
            return flags.Post(ClearData(), den, 1600f);
        }

        private SpecialistAgent MakeHero(FlagManager flags)
        {
            var data = ScriptableObject.CreateInstance<SpecialistData>();
            _created.Add(data);
            data.specialistClass = SpecialistClass.DefenseMech;
            SpecialistPersonality.Apply(data);
            var go = new GameObject("Hero");
            _created.Add(go);
            var hero = go.AddComponent<SpecialistAgent>();
            hero.BindForTravelTest(data, flags, new SpecialistBrain());
            return hero;
        }

        private static void Sim(SpecialistAgent hero, float seconds)
        {
            float left = seconds;
            while (left > 0.001f)
            {
                float step = left > 1f ? 1f : left;
                hero.Simulate(step);
                left -= step;
            }
        }
    }
}
