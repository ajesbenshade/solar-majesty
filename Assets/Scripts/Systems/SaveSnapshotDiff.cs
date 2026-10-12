using System;
using System.Collections.Generic;
using System.Globalization;

namespace SolarMajesty
{
    /// <summary>
    /// Field-by-field comparison of two <see cref="SaveGame"/> snapshots, for save→load→save checks.
    /// Lists are matched as multisets (sorted on stable keys) because restore may rebuild them in a
    /// different order. Positions and timers compare within a tolerance: restore snaps robots and
    /// fauna onto the nav mesh. Metadata (label, timestamps, play time, build stamp) is ignored.
    /// Pure C#: the PlayMode round-trip test uses it, EditMode tests cover it.
    /// </summary>
    public static class SaveSnapshotDiff
    {
        public sealed class Tolerances
        {
            /// <summary>World units on the ground plane.</summary>
            public float Position = 1.25f;
            /// <summary>Health, fatigue and other 0-1 fractions.</summary>
            public float Fraction = 0.01f;
            /// <summary>Timers and elapsed seconds.</summary>
            public float Seconds = 0.15f;
            /// <summary>Science, work and bounty amounts.</summary>
            public float Amount = 0.05f;
        }

        /// <summary>Human-readable differences; empty when the snapshots agree.</summary>
        public static List<string> Compare(SaveGame expected, SaveGame actual, Tolerances tol = null)
        {
            tol ??= new Tolerances();
            var d = new Differ(tol);
            if (expected == null || actual == null)
            {
                if (expected != actual) d.Add("snapshot", expected == null ? "null" : "set", actual == null ? "null" : "set");
                return d.Lines;
            }

            d.Eq("body", expected.body, actual.body);
            d.Eq("seed", expected.seed, actual.seed);
            d.Eq("campaignStamp", expected.campaignStamp ?? "", actual.campaignStamp ?? "");
            d.Eq("simSteps", expected.simSteps, actual.simSteps);
            d.Eq("tutorialDone", expected.tutorialDone, actual.tutorialDone);
            d.Eq("firstHourDemo", expected.firstHourDemo, actual.firstHourDemo);

            CompareStockpile(d, expected.stockpile, actual.stockpile);
            CompareSettlement(d, expected.settlement, actual.settlement);
            CompareResearch(d, expected.research, actual.research);
            CompareMission(d, expected.mission, actual.mission);
            CompareReplay(d, expected.replay, actual.replay);
            CompareVillage(d, expected.villageGrowth, actual.villageGrowth);
            CompareCollectors(d, expected.collectors, actual.collectors);
            CompareOrbital(d, expected.orbital, actual.orbital);
            CompareBuildings(d, expected.buildings, actual.buildings);
            CompareFlags(d, expected.flags, actual.flags);
            CompareAgents(d, expected.agents, actual.agents);
            CompareFauna(d, expected.fauna, actual.fauna);
            CompareNodes(d, expected.nodes, actual.nodes);
            CompareLairs(d, expected.lairs, actual.lairs);
            CompareParties(d, expected, actual);
            CompareRoster(d, expected.roster, actual.roster);
            return d.Lines;
        }

        private static void CompareStockpile(Differ d, SaveStockpile e, SaveStockpile a)
        {
            if (!d.BothOrNeither("stockpile", e, a)) return;
            d.Eq("stockpile.regolith", e.regolith, a.regolith);
            d.Eq("stockpile.waterIce", e.waterIce, a.waterIce);
            d.Eq("stockpile.metals", e.metals, a.metals);
            d.Eq("stockpile.power", e.power, a.power);
        }

        private static void CompareSettlement(Differ d, SaveSettlementState e, SaveSettlementState a)
        {
            if (!d.BothOrNeither("settlement", e, a)) return;
            d.Eq("settlement.population", e.population, a.population);
            d.Eq("settlement.populationGoal", e.populationGoal, a.populationGoal);
            d.Eq("settlement.villageHabs", e.villageHabs, a.villageHabs);
            d.Eq("settlement.bonusBeds", e.bonusBeds, a.bonusBeds);
            d.Eq("settlement.hasOutpost", e.hasOutpost, a.hasOutpost);
            d.Eq("settlement.everHadHab", e.everHadHab, a.everHadHab);
        }

        private static void CompareResearch(Differ d, SaveResearchState e, SaveResearchState a)
        {
            if (!d.BothOrNeither("research", e, a)) return;
            d.Eq("research.unlocked", SortedJoin(e.unlocked), SortedJoin(a.unlocked));
            d.Eq("research.activeTech", e.activeTech, a.activeTech);
            d.Near("research.activeProgress", e.activeProgress, a.activeProgress, d.Tol.Amount);
            d.Near("research.bankedScience", e.bankedScience, a.bankedScience, d.Tol.Amount);
            var ep = ByKey(e.progress, p => p.tech);
            var ap = ByKey(a.progress, p => p.tech);
            foreach (var kv in ep)
            {
                float have = ap.TryGetValue(kv.Key, out var got) ? got.science : 0f;
                d.Near($"research.progress[{kv.Key}]", kv.Value.science, have, d.Tol.Amount);
            }
            foreach (var kv in ap)
                if (!ep.ContainsKey(kv.Key) && Math.Abs(kv.Value.science) > d.Tol.Amount)
                    d.Add($"research.progress[{kv.Key}]", "absent", F(kv.Value.science));
        }

        private static void CompareMission(Differ d, SaveMissionState e, SaveMissionState a)
        {
            if (!d.BothOrNeither("mission", e, a)) return;
            d.Eq("mission.state", e.state, a.state);
            d.Near("mission.elapsed", e.elapsed, a.elapsed, d.Tol.Seconds);
            d.Near("mission.sustainHold", e.sustainHold, a.sustainHold, d.Tol.Seconds);
            d.Eq("mission.densCleared", e.densCleared, a.densCleared);
            d.Eq("mission.sustainMet", e.sustainMet, a.sustainMet);
            d.Eq("mission.launchReady", e.launchReady, a.launchReady);
        }

        private static void CompareReplay(Differ d, SaveReplayState e, SaveReplayState a)
        {
            if (!d.BothOrNeither("replay", e, a)) return;
            d.Eq("replay.mode", e.mode, a.mode);
            d.Eq("replay.challenge", e.challenge, a.challenge);
            d.Eq("replay.stance", e.stance, a.stance);
            d.Eq("replay.ironman", e.ironman, a.ironman);
        }

        private static void CompareVillage(Differ d, SaveVillageGrowth e, SaveVillageGrowth a)
        {
            if (!d.BothOrNeither("villageGrowth", e, a)) return;
            d.Eq("villageGrowth.hasProject", e.hasProject, a.hasProject);
            if (e.hasProject && a.hasProject)
            {
                d.Eq("villageGrowth.category", e.category, a.category);
                d.Eq("villageGrowth.cell", $"{e.x},{e.y}", $"{a.x},{a.y}");
                d.Near("villageGrowth.progress", e.progress, a.progress, d.Tol.Amount);
            }
            d.Near("villageGrowth.cooldown", e.cooldown, a.cooldown, d.Tol.Seconds);
            d.Eq("villageGrowth.plotSalt", e.plotSalt, a.plotSalt);
        }

        private static void CompareCollectors(Differ d, SaveCollectors e, SaveCollectors a)
        {
            if (!d.BothOrNeither("collectors", e, a)) return;
            var ea = Sorted(e.agents, c => $"{c.state:D2}|{c.carry:D8}|{c.health:F2}");
            var aa = Sorted(a.agents, c => $"{c.state:D2}|{c.carry:D8}|{c.health:F2}");
            if (!d.Eq("collectors.count", ea.Count, aa.Count)) return;
            for (int i = 0; i < ea.Count; i++)
            {
                string p = $"collectors[{i}]";
                d.Eq(p + ".carry", ea[i].carry, aa[i].carry);
                d.Eq(p + ".state", ea[i].state, aa[i].state);
                d.Near(p + ".health", ea[i].health, aa[i].health, d.Tol.Amount);
                d.Flat(p + ".pos", ea[i].px, ea[i].pz, aa[i].px, aa[i].pz);
            }
            d.Eq("collectors.replacements", Count(e.replacementSeconds), Count(a.replacementSeconds));
        }

        private static void CompareOrbital(Differ d, SaveOrbital e, SaveOrbital a)
        {
            float[] ec = e?.cooldowns ?? Array.Empty<float>();
            float[] ac = a?.cooldowns ?? Array.Empty<float>();
            int n = Math.Max(ec.Length, ac.Length);
            for (int i = 0; i < n; i++)
            {
                float ev = i < ec.Length ? ec[i] : 0f;
                float av = i < ac.Length ? ac[i] : 0f;
                d.Near($"orbital.cooldowns[{i}]", ev, av, d.Tol.Seconds);
            }
        }

        private static void CompareBuildings(Differ d, List<SaveBuilding> e, List<SaveBuilding> a)
        {
            Func<SaveBuilding, string> key = b => $"{b.category:D3}@{b.x:D4},{b.y:D4} {b.w}x{b.h}{(b.villageHab ? " vhab" : "")}";
            var es = Sorted(e, key);
            var asd = Sorted(a, key);
            if (!d.Eq("buildings.count", es.Count, asd.Count))
            {
                d.SetDiff("buildings", es, asd, key);
                return;
            }
            for (int i = 0; i < es.Count; i++)
            {
                string p = $"buildings[{key(es[i])}]";
                if (!d.Eq(p, key(es[i]), key(asd[i]))) continue;
                d.Eq(p + ".progressMilli", es[i].progressMilli, asd[i].progressMilli);
                d.Near(p + ".health", es[i].health, asd[i].health, d.Tol.Fraction);
                d.Eq(p + ".levyPurse", es[i].levyPurse, asd[i].levyPurse);
                d.Eq(p + ".laserArmed", es[i].laserArmed, asd[i].laserArmed);
            }
        }

        private static void CompareFlags(Differ d, List<SaveFlag> e, List<SaveFlag> a)
        {
            Func<SaveFlag, string> key = f => $"{f.flagType:D2}@{Mathf0(f.px)},{Mathf0(f.pz)}";
            var es = Sorted(e, key);
            var asd = Sorted(a, key);
            if (!d.Eq("flags.count", es.Count, asd.Count)) return;
            for (int i = 0; i < es.Count; i++)
            {
                string p = $"flags[{key(es[i])}]";
                d.Eq(p + ".type", es[i].flagType, asd[i].flagType);
                d.Flat(p + ".pos", es[i].px, es[i].pz, asd[i].px, asd[i].pz);
                d.Near(p + ".bounty", es[i].bounty, asd[i].bounty, d.Tol.Amount);
                d.Eq(p + ".escrowMetals", es[i].escrowMetals, asd[i].escrowMetals);
                d.Near(p + ".workDone", es[i].workDone, asd[i].workDone, d.Tol.Amount);
                d.Near(p + ".postedWork", es[i].postedWork, asd[i].postedWork, d.Tol.Amount);
                d.Eq(p + ".claimCount", es[i].claimCount, asd[i].claimCount);
            }
        }

        private static string AgentKey(SaveAgent g) =>
            $"{g.specialistClass:D2}|L{g.level:D2}|X{g.xp:D6}|C{g.credits:D6}|H{g.health:F2}";

        private static void CompareAgents(Differ d, List<SaveAgent> e, List<SaveAgent> a)
        {
            var es = Sorted(e, AgentKey);
            var asd = Sorted(a, AgentKey);
            d.Eq("agents.byClass", ClassHistogram(es), ClassHistogram(asd));
            if (!d.Eq("agents.count", es.Count, asd.Count)) return;
            for (int i = 0; i < es.Count; i++)
            {
                var x = es[i];
                var y = asd[i];
                string p = $"agents[class {x.specialistClass} #{i}]";
                if (!d.Eq(p + ".class", x.specialistClass, y.specialistClass)) continue;
                d.Near(p + ".health", x.health, y.health, d.Tol.Fraction);
                d.Near(p + ".fatigue", x.fatigue, y.fatigue, d.Tol.Fraction);
                d.Eq(p + ".credits", x.credits, y.credits);
                d.Eq(p + ".downed", x.downed, y.downed);
                if (x.downed && y.downed)
                    d.Near(p + ".downedTimer", x.downedTimer, y.downedTimer, d.Tol.Seconds);
                d.Eq(p + ".level", x.level, y.level);
                d.Eq(p + ".xp", x.xp, y.xp);
                d.Eq(p + ".suit", x.suit, y.suit);
                d.Eq(p + ".reviveCount", x.reviveCount, y.reviveCount);
                d.Eq(p + ".levyCarry", x.levyCarry, y.levyCarry);
                d.Eq(p + ".dutyCarry", x.dutyCarry, y.dutyCarry);
                d.Eq(p + ".claimsFlag", x.claimedFlagIndex >= 0, y.claimedFlagIndex >= 0);
                d.Eq(p + ".inParty", x.partyId >= 0, y.partyId >= 0);
                d.Flat(p + ".pos", x.px, x.pz, y.px, y.pz);
            }
        }

        private static void CompareFauna(Differ d, List<SaveFauna> e, List<SaveFauna> a)
        {
            Func<SaveFauna, string> key = f => $"{f.kind:D2}|{f.lairIndex:D3}|{f.health:F2}";
            var es = Sorted(e, key);
            var asd = Sorted(a, key);
            d.Eq("fauna.byKind", Histogram(es, f => f.kind), Histogram(asd, f => f.kind));
            if (!d.Eq("fauna.count", es.Count, asd.Count)) return;
            for (int i = 0; i < es.Count; i++)
            {
                string p = $"fauna[kind {es[i].kind} #{i}]";
                if (!d.Eq(p + ".kind", es[i].kind, asd[i].kind)) continue;
                d.Eq(p + ".lairIndex", es[i].lairIndex, asd[i].lairIndex);
                d.Near(p + ".health", es[i].health, asd[i].health, d.Tol.Fraction);
                d.Flat(p + ".pos", es[i].px, es[i].pz, asd[i].px, asd[i].pz);
            }
        }

        private static void CompareNodes(Differ d, List<SaveNode> e, List<SaveNode> a)
        {
            Func<SaveNode, string> key = n => $"{n.nodeType:D2}@{Mathf0(n.px)},{Mathf0(n.pz)}";
            var es = Sorted(e, key);
            var asd = Sorted(a, key);
            if (!d.Eq("nodes.count", es.Count, asd.Count)) return;
            for (int i = 0; i < es.Count; i++)
            {
                string p = $"nodes[{key(es[i])}]";
                d.Eq(p + ".type", es[i].nodeType, asd[i].nodeType);
                d.Eq(p + ".remaining", es[i].remaining, asd[i].remaining);
            }
        }

        private static void CompareLairs(Differ d, List<SaveLair> e, List<SaveLair> a)
        {
            // Lair order is the world-gen order and fauna refer to it by index, so it must hold.
            int ec = Count(e), ac = Count(a);
            if (!d.Eq("lairs.count", ec, ac)) return;
            for (int i = 0; i < ec; i++)
            {
                string p = $"lairs[{i}]";
                d.Flat(p + ".pos", e[i].px, e[i].pz, a[i].px, a[i].pz);
                d.Eq(p + ".cleared", e[i].cleared, a[i].cleared);
                d.Eq(p + ".scouted", e[i].scouted, a[i].scouted);
                d.Eq(p + ".expansionSpawned", e[i].expansionSpawned, a[i].expansionSpawned);
            }
        }

        private static void CompareParties(Differ d, SaveGame e, SaveGame a)
        {
            Func<SaveGame, SaveParty, string> shape = (g, p) =>
            {
                int leaderClass = p.leaderIndex >= 0 && g.agents != null && p.leaderIndex < g.agents.Count
                    ? g.agents[p.leaderIndex].specialistClass : -1;
                return $"leader {leaderClass} · {Count(p.memberIndices)} members";
            };
            var es = new List<string>();
            var asd = new List<string>();
            if (e.parties != null) foreach (var p in e.parties) es.Add(shape(e, p));
            if (a.parties != null) foreach (var p in a.parties) asd.Add(shape(a, p));
            es.Sort(StringComparer.Ordinal);
            asd.Sort(StringComparer.Ordinal);
            d.Eq("parties", string.Join("; ", es), string.Join("; ", asd));
        }

        private static void CompareRoster(Differ d, List<SaveRosterEntry> e, List<SaveRosterEntry> a)
        {
            Func<SaveRosterEntry, string> key = r =>
                $"{r.specialistClass:D2} L{r.level} X{r.xp} C{r.credits} R{r.reviveCount} S{r.suit}{(r.corpse ? " corpse" : "")}";
            var es = Sorted(e, key);
            var asd = Sorted(a, key);
            var el = new List<string>();
            var al = new List<string>();
            foreach (var r in es) el.Add(key(r));
            foreach (var r in asd) al.Add(key(r));
            d.Eq("roster", string.Join("; ", el), string.Join("; ", al));
        }

        private static string ClassHistogram(List<SaveAgent> list) => Histogram(list, g => g.specialistClass);

        private static string Histogram<T>(List<T> list, Func<T, int> pick)
        {
            var counts = new SortedDictionary<int, int>();
            foreach (var item in list)
            {
                int k = pick(item);
                counts.TryGetValue(k, out int c);
                counts[k] = c + 1;
            }
            var parts = new List<string>();
            foreach (var kv in counts) parts.Add($"{kv.Key}x{kv.Value}");
            return string.Join(" ", parts);
        }

        private static List<T> Sorted<T>(List<T> list, Func<T, string> key)
        {
            var copy = list != null ? new List<T>(list) : new List<T>();
            copy.Sort((x, y) => string.CompareOrdinal(key(x), key(y)));
            return copy;
        }

        private static Dictionary<int, T> ByKey<T>(List<T> list, Func<T, int> key)
        {
            var map = new Dictionary<int, T>();
            if (list == null) return map;
            foreach (var item in list) map[key(item)] = item;
            return map;
        }

        private static string SortedJoin(List<int> values)
        {
            var copy = values != null ? new List<int>(values) : new List<int>();
            copy.Sort();
            return string.Join(",", copy);
        }

        private static int Count<T>(List<T> list) => list != null ? list.Count : 0;

        private static string Mathf0(float v) => Math.Round(v).ToString("0", CultureInfo.InvariantCulture);

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private sealed class Differ
        {
            public readonly List<string> Lines = new List<string>();
            public readonly Tolerances Tol;

            public Differ(Tolerances tol)
            {
                Tol = tol;
            }

            public void Add(string path, string expected, string actual) =>
                Lines.Add($"{path}: expected {expected}, got {actual}");

            public bool Eq<T>(string path, T expected, T actual)
            {
                if (EqualityComparer<T>.Default.Equals(expected, actual)) return true;
                Add(path, Convert.ToString(expected, CultureInfo.InvariantCulture), Convert.ToString(actual, CultureInfo.InvariantCulture));
                return false;
            }

            public bool Near(string path, float expected, float actual, float tolerance)
            {
                if (float.IsNaN(actual) || float.IsInfinity(actual) || Math.Abs(expected - actual) > tolerance)
                {
                    Add(path, F(expected), F(actual));
                    return false;
                }
                return true;
            }

            public bool Flat(string path, float ex, float ez, float ax, float az)
            {
                float dx = ex - ax, dz = ez - az;
                if (float.IsNaN(ax) || float.IsNaN(az) || dx * dx + dz * dz > Tol.Position * Tol.Position)
                {
                    Add(path, $"({F(ex)}, {F(ez)})", $"({F(ax)}, {F(az)})");
                    return false;
                }
                return true;
            }

            public bool BothOrNeither(string path, object expected, object actual)
            {
                if (expected == null && actual == null) return false;
                if (expected != null && actual != null) return true;
                Add(path, expected == null ? "null" : "set", actual == null ? "null" : "set");
                return false;
            }

            public void SetDiff<T>(string path, List<T> expected, List<T> actual, Func<T, string> key)
            {
                var have = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var a in actual)
                {
                    string k = key(a);
                    have.TryGetValue(k, out int c);
                    have[k] = c + 1;
                }
                foreach (var e in expected)
                {
                    string k = key(e);
                    if (have.TryGetValue(k, out int c) && c > 0) have[k] = c - 1;
                    else Lines.Add($"{path}: missing {k}");
                }
                foreach (var kv in have)
                    for (int i = 0; i < kv.Value; i++)
                        Lines.Add($"{path}: unexpected {kv.Key}");
            }
        }
    }
}
