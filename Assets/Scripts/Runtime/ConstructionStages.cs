using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Construction stages a building visibly passes through while it is being built:
    /// site prep (stakes, string lines, material drops, tower crane), foundation (plinths, pads,
    /// decks), frame (legs and a temporary steel skeleton under scaffolding), shell (hull, dome,
    /// cladding, rising bottom-up) and fit-out (windows, lights, antennas, solar, trim).
    ///
    /// The finished building is spawned at placement as before; <see cref="Show"/> hides what is
    /// not built yet and adds construction-only parts under a <c>Dress_BuildOnly</c> child, and
    /// <see cref="Finish"/> puts the building back exactly as it was. Kit parts are sorted by an
    /// explicit <c>_S{n}_</c> name token (DetailBatch meshes carry one), otherwise by name and shape.
    /// </summary>
    public static class ConstructionStages
    {
        public const int SitePrep = 0;
        public const int Foundation = 1;
        public const int Frame = 2;
        public const int Shell = 3;
        public const int FitOut = 4;
        public const int Count = 5;

        /// <summary>Build progress at which each stage starts (last entry is completion).</summary>
        private static readonly float[] StageStart = { 0f, 0.08f, 0.28f, 0.55f, 0.80f, 1f };

        public static readonly string[] StageNames = { "Site prep", "Foundation", "Frame", "Shell", "Fit-out" };

        private static readonly Color Steel = new Color(0.30f, 0.31f, 0.34f);
        private static readonly Color Primer = new Color(0.86f, 0.36f, 0.10f);
        private static readonly Color ScaffoldYellow = new Color(0.95f, 0.78f, 0.14f);
        private static readonly Color Plank = new Color(0.46f, 0.38f, 0.28f);
        private static readonly Color Cream = new Color(0.88f, 0.82f, 0.74f);
        private static readonly Color Carbon = new Color(0.20f, 0.19f, 0.18f);
        private static readonly Color Soil = new Color(0.36f, 0.20f, 0.12f);
        private static readonly Color Orange = new Color(0.96f, 0.42f, 0.08f);
        private static readonly Color LampEmit = new Color(1.6f, 1.25f, 0.8f);
        private static readonly Color RedEmit = new Color(1.6f, 0.22f, 0.08f);

        private struct Entry
        {
            public Renderer Renderer;
            public int Stage;
            public float Rank;      // 0..1 bottom-up order within the stage
            public float HideAt;    // progress at which a construction-only part comes down
            public bool WasEnabled;
            public bool Pinned;     // up from the first moment of its stage (the crane)
        }

        private sealed class State
        {
            public GameObject Root;
            public GameObject BuildOnly;
            public readonly List<Entry> Entries = new List<Entry>();
            public readonly List<Light> Lights = new List<Light>();
            public readonly List<bool> LightsWere = new List<bool>();
            public float LastP = -1f;
            public Bounds Envelope;
            public float WorkHeight;
        }

        private static readonly Dictionary<EntityId, State> States = new Dictionary<EntityId, State>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => States.Clear();

        /// <summary>
        /// Progress that shows stage <paramref name="stage"/> nearly done (the last stage: finished),
        /// for stills of the build sequence.
        /// </summary>
        public static float Snapshot(int stage)
        {
            stage = Mathf.Clamp(stage, 0, Count - 1);
            if (stage == Count - 1) return 1f;
            return Mathf.Lerp(StageStart[stage], StageStart[stage + 1], 0.85f);
        }

        /// <summary>Which stage <paramref name="progress01"/> falls in.</summary>
        public static int StageAt(float progress01)
        {
            for (int s = Count - 1; s >= 0; s--)
                if (progress01 >= StageStart[s]) return s;
            return 0;
        }

        /// <summary>Show <paramref name="building"/> as it looks at <paramref name="progress01"/> of its build.</summary>
        public static void Show(GameObject building, float progress01)
        {
            if (building == null) return;
            if (progress01 >= 1f)
            {
                Finish(building);
                return;
            }
            var st = Ensure(building);
            float p = Mathf.Clamp01(progress01);
            if (st.LastP >= 0f && Mathf.Abs(p - st.LastP) < 0.002f) return;
            st.LastP = p;

            int stage = StageAt(p);
            float f = Mathf.InverseLerp(StageStart[stage], StageStart[stage + 1], p);
            float work = 0f;
            for (int i = 0; i < st.Entries.Count; i++)
            {
                var e = st.Entries[i];
                if (e.Renderer == null) continue;
                bool built = e.Stage < stage || (e.Stage == stage && e.Rank <= f);
                bool on = built && p < e.HideAt && e.WasEnabled;
                if (e.Renderer.enabled != on) e.Renderer.enabled = on;
                if (on && e.Stage == stage && e.HideAt > 1f)
                    work = Mathf.Max(work, e.Renderer.bounds.max.y - building.transform.position.y);
            }
            st.WorkHeight = work > 0.05f ? work : 0.3f;
            bool lit = stage == FitOut && f > 0.5f;
            for (int i = 0; i < st.Lights.Count; i++)
                if (st.Lights[i] != null) st.Lights[i].enabled = lit && st.LightsWere[i];
        }

        /// <summary>Put a staged building back to its finished look and drop the site kit.</summary>
        public static void Finish(GameObject building)
        {
            if (building == null) return;
            EntityId id = building.GetEntityId();
            if (!States.TryGetValue(id, out var st)) return;
            States.Remove(id);
            for (int i = 0; i < st.Entries.Count; i++)
            {
                var e = st.Entries[i];
                if (e.Renderer != null) e.Renderer.enabled = e.WasEnabled;
            }
            for (int i = 0; i < st.Lights.Count; i++)
                if (st.Lights[i] != null) st.Lights[i].enabled = st.LightsWere[i];
            if (st.BuildOnly != null)
            {
                // Site-kit meshes are built per site (not cached); free them with the kit.
                foreach (var mf in st.BuildOnly.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) ColonyVisualUtility.DestroyNow(mf.sharedMesh);
                ColonyVisualUtility.DestroyNow(st.BuildOnly);
            }
            Prune();
        }

        public static bool IsStaged(GameObject building) =>
            building != null && States.ContainsKey(building.GetEntityId());

        /// <summary>World point where work is happening now (top of the newest built part).</summary>
        public static Vector3 WorkPoint(GameObject building)
        {
            if (building == null) return Vector3.zero;
            Vector3 at = building.transform.position;
            if (!States.TryGetValue(building.GetEntityId(), out var st)) return at + Vector3.up * 0.6f;
            return new Vector3(at.x + st.Envelope.center.x, at.y + st.WorkHeight, at.z + st.Envelope.center.z);
        }

        /// <summary>Height of the finished building's main body above its origin.</summary>
        public static float Height(GameObject building)
        {
            if (building == null) return 3f;
            if (States.TryGetValue(building.GetEntityId(), out var st)) return st.Envelope.max.y;
            return 3f;
        }

        private static void Prune()
        {
            if (States.Count == 0) return;
            List<EntityId> dead = null;
            foreach (var kv in States)
                if (kv.Value.Root == null) (dead ??= new List<EntityId>()).Add(kv.Key);
            if (dead != null)
                for (int i = 0; i < dead.Count; i++) States.Remove(dead[i]);
        }

        private static State Ensure(GameObject building)
        {
            EntityId id = building.GetEntityId();
            if (States.TryGetValue(id, out var st)) return st;
            Prune();
            st = new State { Root = building };
            States[id] = st;
            Collect(st);
            BuildSiteKit(st);
            return st;
        }

        // ---------------------------------------------------------------- classification

        private static void Collect(State st)
        {
            Transform root = st.Root.transform;
            Vector3 origin = root.position;
            var rends = st.Root.GetComponentsInChildren<Renderer>(true);
            var perStage = new List<(Renderer r, float key)>[Count];
            for (int s = 0; s < Count; s++) perStage[s] = new List<(Renderer, float)>();

            bool anyShell = false;
            Bounds env = new Bounds(Vector3.up, Vector3.one);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null) continue;
                int stage = Classify(r, root, origin, out bool envelopePart);
                if (stage < 0) continue;
                Bounds b = r.bounds;
                b.center -= origin;
                perStage[stage].Add((r, b.min.y + b.center.y * 0.001f));
                if (envelopePart)
                {
                    if (!anyShell) { env = b; anyShell = true; }
                    else env.Encapsulate(b);
                }
            }
            if (!anyShell)
            {
                for (int s = 0; s < Count; s++)
                    foreach (var (r, _) in perStage[s])
                    {
                        Bounds b = r.bounds;
                        b.center -= origin;
                        if (!anyShell) { env = b; anyShell = true; }
                        else env.Encapsulate(b);
                    }
            }
            st.Envelope = env;

            for (int s = 0; s < Count; s++)
            {
                var list = perStage[s];
                list.Sort((a, b) => a.key.CompareTo(b.key));
                for (int i = 0; i < list.Count; i++)
                {
                    st.Entries.Add(new Entry
                    {
                        Renderer = list[i].r,
                        Stage = s,
                        Rank = list.Count > 1 ? (i + 0.5f) / list.Count : 0.5f,
                        HideAt = 2f,
                        WasEnabled = list[i].r.enabled
                    });
                }
            }

            foreach (var l in st.Root.GetComponentsInChildren<Light>(true))
            {
                st.Lights.Add(l);
                st.LightsWere.Add(l.enabled);
            }
        }

        private static readonly string[] Excluded =
            { "SelectRing", "SelectProxy", "StatusOrb", "StatusPip", "Label", "Footprint", "Ghost_" };
        private static readonly string[] FitOutWords =
        {
            "light", "lamp", "beacon", "window", "porthole", "antenna", "dish", "solar", "cell",
            "hatch", "door", "visor", "laser", "turret", "gun", "barrel", "flag", "banner", "sign",
            "stripe", "chevron", "eye", "crate", "lantern", "awning", "stack", "vent", "bench",
            "shield", "bubble", "tick", "cupola", "band", "rail"
        };
        private static readonly string[] FrameWords =
            { "leg", "strut", "truss", "pylon", "pillar", "column", "post", "brace", "mast" };

        /// <summary>
        /// Stage for one renderer, or -1 to leave it alone. <paramref name="envelopePart"/> marks
        /// the pieces whose union is the main body the steel frame and scaffold wrap.
        /// </summary>
        private static int Classify(Renderer r, Transform root, Vector3 origin, out bool envelopePart)
        {
            envelopePart = false;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return FitOut;

            // Explicit tag or exclusion anywhere between the renderer and the building root.
            bool arch = false, ship = false;
            for (Transform t = r.transform; t != null && t != root; t = t.parent)
            {
                string n = t.name;
                for (int i = 0; i < Excluded.Length; i++)
                    if (n.Contains(Excluded[i])) return -1;
                int tag = StageTag(n);
                if (tag >= 0)
                {
                    envelopePart = tag == Shell && !n.Contains("Detail");
                    return tag;
                }
                if (n.StartsWith("Dress_Arch")) arch = true;
                if (n.Contains("Starship") || n.Contains("ParkedShip") || n.Contains("Rocket")) ship = true;
            }
            if (ship) return FitOut;
            if (arch) return Shell;

            string low = r.name.ToLowerInvariant();
            Bounds b = r.bounds;
            float minY = b.min.y - origin.y;
            float h = b.size.y;
            float span = Mathf.Max(b.size.x, b.size.z);

            // Shield bubbles are huge but go up last, and the scaffold must not wrap them.
            if (low.Contains("shield") || low.Contains("bubble")) return FitOut;
            if (IsEmissive(r) && span < 1.5f) return FitOut;
            for (int i = 0; i < FitOutWords.Length; i++)
                if (low.Contains(FitOutWords[i]) && span < 3.5f) return FitOut;
            if (minY < 0.16f && h < 0.7f) return Foundation;
            for (int i = 0; i < FrameWords.Length; i++)
                if (low.Contains(FrameWords[i])) return Frame;
            if (h > span * 3.5f && span < 0.5f && minY < 0.6f) return Frame;

            envelopePart = span > 0.8f;
            return Shell;
        }

        /// <summary>"_S{n}_" in a name → stage n.</summary>
        private static int StageTag(string n)
        {
            int at = n.IndexOf("_S", System.StringComparison.Ordinal);
            while (at >= 0 && at + 3 < n.Length)
            {
                char c = n[at + 2];
                if (c >= '0' && c <= '4' && n[at + 3] == '_') return c - '0';
                at = n.IndexOf("_S", at + 2, System.StringComparison.Ordinal);
            }
            return -1;
        }

        private static bool IsEmissive(Renderer r)
        {
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("_EmissionColor")) return false;
            if (!m.IsKeywordEnabled("_EMISSION")) return false;
            return m.GetColor("_EmissionColor").maxColorComponent > 0.05f;
        }

        // ---------------------------------------------------------------- construction-only kit

        private static void BuildSiteKit(State st)
        {
            var holder = new GameObject("Dress_BuildOnly");
            holder.transform.SetParent(st.Root.transform, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            st.BuildOnly = holder;
            Transform t = holder.transform;

            Bounds e = st.Envelope;
            float top = Mathf.Max(0.4f, e.max.y);
            // Footprint the site occupies: the envelope, at least a little wider than the body.
            Vector3 c = new Vector3(e.center.x, 0f, e.center.z);
            float hx = Mathf.Max(1.0f, e.extents.x), hz = Mathf.Max(1.0f, e.extents.z);
            bool round = LooksRound(st, e);

            var site = new DetailBatch(t, null) { Stage = SitePrep };
            SitePrepKit(site, c, hx, hz, top);
            site.Build();
            Tag(st, t, SitePrep, 0.9f, "Site");

            if (top > 1.0f)
            {
                var frame = new DetailBatch(t, null) { Stage = Frame };
                if (round) RoundFrame(frame, c, Mathf.Min(hx, hz), e.min.y, top);
                else BoxFrame(frame, c, hx, hz, Mathf.Max(0.15f, e.min.y), top);
                frame.Build();
                Tag(st, t, Frame, StageStart[FitOut] + 0.02f, "Frame");

                var scaffold = new DetailBatch(t, null) { Stage = Frame };
                Scaffold(scaffold, c, hx + 0.38f, hz + 0.38f, top);
                scaffold.Build();
                Tag(st, t, Frame, 0.93f, "Scaffold");
            }

            var crane = new DetailBatch(t, null) { Stage = SitePrep };
            TowerCrane(crane, c, hx, hz, top);
            crane.Build();
            Tag(st, t, SitePrep, 0.96f, "Crane", pinned: true);

            // Construction parts sort in with the building's own parts of the same stage.
            ResortStages(st);
        }

        /// <summary>Register the DetailBatch renderers just built under <paramref name="t"/>.</summary>
        private static void Tag(State st, Transform t, int stage, float hideAt, string label = "Site", bool pinned = false)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child.name.StartsWith("Kit_")) continue;
                child.name = "Kit_" + label + "_" + child.name;
                var r = child.GetComponent<Renderer>();
                if (r == null) continue;
                st.Entries.Add(new Entry
                {
                    Renderer = r,
                    Stage = stage,
                    HideAt = hideAt,
                    WasEnabled = true,
                    Pinned = pinned
                });
                r.enabled = false;
            }
        }

        /// <summary>Rank every stage's parts (building and site kit together) bottom-up.</summary>
        private static void ResortStages(State st)
        {
            Vector3 origin = st.Root.transform.position;
            var idx = new List<int>();
            var keys = new Dictionary<int, float>();
            for (int s = 0; s < Count; s++)
            {
                idx.Clear();
                keys.Clear();
                for (int i = 0; i < st.Entries.Count; i++)
                {
                    var e = st.Entries[i];
                    if (e.Stage != s || e.Renderer == null) continue;
                    if (e.Pinned)
                    {
                        e.Rank = 0f;
                        st.Entries[i] = e;
                        continue;
                    }
                    Bounds b = e.Renderer.bounds;
                    keys[i] = b.min.y - origin.y + b.center.y * 0.001f;
                    idx.Add(i);
                }
                idx.Sort((a, b) => keys[a].CompareTo(keys[b]));
                for (int k = 0; k < idx.Count; k++)
                {
                    var e = st.Entries[idx[k]];
                    e.Rank = idx.Count > 1 ? (k + 0.5f) / idx.Count : 0.5f;
                    st.Entries[idx[k]] = e;
                }
            }
        }

        private static bool LooksRound(State st, Bounds env)
        {
            float best = 0f;
            string mesh = null;
            foreach (var e in st.Entries)
            {
                if (e.Stage != Shell || e.Renderer == null) continue;
                var b = e.Renderer.bounds;
                float v = b.size.x * b.size.y * b.size.z;
                if (v <= best) continue;
                var mf = e.Renderer.GetComponent<MeshFilter>();
                best = v;
                mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : null;
            }
            return mesh != null && (mesh.StartsWith("Sphere") || mesh.StartsWith("Cylinder")) &&
                   Mathf.Abs(env.size.x - env.size.z) < 0.4f;
        }

        private static void SitePrepKit(DetailBatch k, Vector3 c, float hx, float hz, float top)
        {
            float ex = hx + 0.55f, ez = hz + 0.55f;
            // Graded pad, a shade darker than the regolith.
            k.Box(c + new Vector3(0f, 0.012f, 0f), new Vector3(ex * 2f + 0.3f, 0.02f, ez * 2f + 0.3f), Soil);
            // Survey stakes with orange flags and string lines between them.
            Vector3[] corners =
            {
                c + new Vector3(-ex, 0f, -ez), c + new Vector3(ex, 0f, -ez),
                c + new Vector3(ex, 0f, ez), c + new Vector3(-ex, 0f, ez)
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = corners[i];
                k.Box(p + new Vector3(0f, 0.28f, 0f), new Vector3(0.05f, 0.56f, 0.05f), Plank);
                k.Box(p + new Vector3(0.08f, 0.50f, 0f), new Vector3(0.14f, 0.09f, 0.01f), Orange);
                Vector3 q = corners[(i + 1) % 4];
                k.Beam(p + Vector3.up * 0.22f, q + Vector3.up * 0.22f, 0.012f, ScaffoldYellow);
            }
            // Layout marks painted on the pad.
            for (int i = -1; i <= 1; i++)
            {
                k.Box(c + new Vector3(i * hx * 0.66f, 0.026f, 0f), new Vector3(0.04f, 0.01f, hz * 1.9f), Cream);
                k.Box(c + new Vector3(0f, 0.026f, i * hz * 0.66f), new Vector3(hx * 1.9f, 0.01f, 0.04f), Cream);
            }

            // Material drop along the -x side: panel stack, beam bundle, cable drum, container.
            Vector3 yard = c + new Vector3(-ex - 0.2f, 0f, -hz * 0.35f);
            k.Box(yard + new Vector3(0f, 0.06f, 0f), new Vector3(0.9f, 0.1f, 0.7f), Plank);
            for (int i = 0; i < 5; i++)
                k.Box(yard + new Vector3(0f, 0.14f + i * 0.07f, 0f), new Vector3(0.82f, 0.05f, 0.62f), i % 2 == 0 ? Cream : new Color(0.8f, 0.74f, 0.66f));
            k.Box(yard + new Vector3(0f, 0.34f, 0.33f), new Vector3(0.9f, 0.03f, 0.02f), Orange);

            Vector3 beams = yard + new Vector3(0f, 0f, 0.95f);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2 + (i == 0 ? 1 : 0); j++)
                    k.Box(beams + new Vector3(0f, 0.08f + j * 0.12f, (i - 1) * 0.14f), new Vector3(1.3f, 0.1f, 0.1f), Primer);
            k.Box(beams + new Vector3(-0.45f, 0.2f, 0f), new Vector3(0.04f, 0.4f, 0.5f), Carbon);
            k.Box(beams + new Vector3(0.45f, 0.2f, 0f), new Vector3(0.04f, 0.4f, 0.5f), Carbon);

            Vector3 drum = yard + new Vector3(0.1f, 0f, -0.85f);
            k.Cyl(drum + new Vector3(0f, 0.28f, 0f), 0.56f, 0.06f, Quaternion.Euler(90f, 0f, 0f), Primer);
            k.Cyl(drum + new Vector3(0f, 0.28f, 0.3f), 0.56f, 0.06f, Quaternion.Euler(90f, 0f, 0f), Primer);
            k.Cyl(drum + new Vector3(0f, 0.28f, 0.15f), 0.38f, 0.28f, Quaternion.Euler(90f, 0f, 0f), Carbon);

            // Site light tower at the front corner.
            Vector3 lt = c + new Vector3(-ex + 0.1f, 0f, -ez - 0.35f);
            k.Box(lt + new Vector3(0f, 0.18f, 0f), new Vector3(0.6f, 0.3f, 0.4f), ScaffoldYellow);
            k.Box(lt + new Vector3(0f, 0.34f, 0f), new Vector3(0.5f, 0.04f, 0.3f), Carbon);
            k.Box(lt + new Vector3(0f, 1.25f, 0f), new Vector3(0.06f, 1.9f, 0.06f), Steel);
            k.Box(lt + new Vector3(0f, 2.2f, 0f), new Vector3(0.62f, 0.05f, 0.08f), Steel);
            for (int i = 0; i < 4; i++)
            {
                Vector3 lamp = lt + new Vector3(-0.24f + i * 0.16f, 2.3f, 0.03f);
                k.Box(lamp, new Vector3(0.13f, 0.12f, 0.08f), Carbon);
                k.Box(lamp + new Vector3(0f, 0f, 0.045f), new Vector3(0.1f, 0.09f, 0.01f), Cream, LampEmit);
            }

            // Site cabin at the back-left.
            Vector3 cab = c + new Vector3(-ex + 0.2f, 0f, ez + 0.5f);
            k.Box(cab + new Vector3(0.3f, 0.36f, 0f), new Vector3(1.3f, 0.62f, 0.55f), Cream);
            k.Box(cab + new Vector3(0.3f, 0.69f, 0f), new Vector3(1.36f, 0.05f, 0.6f), Carbon);
            k.Box(cab + new Vector3(0.3f, 0.52f, -0.28f), new Vector3(1.3f, 0.06f, 0.01f), Orange);
            k.Box(cab + new Vector3(-0.05f, 0.42f, -0.28f), new Vector3(0.3f, 0.16f, 0.01f), Cream, new Color(0.9f, 0.7f, 0.4f));
            k.Box(cab + new Vector3(0.62f, 0.3f, -0.28f), new Vector3(0.2f, 0.42f, 0.01f), Carbon);
        }

        private static void BoxFrame(DetailBatch k, Vector3 c, float hx, float hz, float y0, float top)
        {
            float ix = hx * 0.94f, iz = hz * 0.94f;
            int nx = Mathf.Max(2, Mathf.RoundToInt(ix * 2f / 1.3f));
            int nz = Mathf.Max(2, Mathf.RoundToInt(iz * 2f / 1.3f));
            float lift = 1.05f;
            int levels = Mathf.Max(1, Mathf.CeilToInt((top - y0) / lift));
            float dy = (top - y0) / levels;

            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float fixedC = side % 2 == 0 ? -1f : 1f;
                int n = alongX ? nx : nz;
                for (int i = 0; i <= n; i++)
                {
                    float u = -1f + 2f * i / n;
                    Vector3 basePt = alongX
                        ? c + new Vector3(u * ix, y0, fixedC * iz)
                        : c + new Vector3(fixedC * ix, y0, u * iz);
                    k.Box(basePt + Vector3.up * (top - y0) * 0.5f, new Vector3(0.1f, top - y0, 0.1f), Steel);
                    k.Box(basePt + Vector3.up * 0.05f, new Vector3(0.2f, 0.06f, 0.2f), Carbon);
                    if (i == n) continue;
                    float u2 = -1f + 2f * (i + 1) / n;
                    Vector3 next = alongX
                        ? c + new Vector3(u2 * ix, y0, fixedC * iz)
                        : c + new Vector3(fixedC * ix, y0, u2 * iz);
                    for (int l = 1; l <= levels; l++)
                        k.Beam(basePt + Vector3.up * (dy * l), next + Vector3.up * (dy * l), 0.07f, Primer);
                    // X-bracing in alternate bays.
                    if ((i + side) % 2 == 0)
                    {
                        k.Beam(basePt + Vector3.up * 0.1f, next + Vector3.up * dy, 0.035f, Steel);
                        k.Beam(next + Vector3.up * 0.1f, basePt + Vector3.up * dy, 0.035f, Steel);
                    }
                }
            }
            // Roof joists.
            for (int i = 0; i <= nx; i++)
            {
                float x = -ix + 2f * ix * i / nx;
                k.Beam(c + new Vector3(x, top, -iz), c + new Vector3(x, top, iz), 0.07f, Primer);
            }
            k.Beam(c + new Vector3(-ix, top, 0f), c + new Vector3(ix, top, 0f), 0.08f, Steel);
        }

        private static void RoundFrame(DetailBatch k, Vector3 c, float r, float y0, float top)
        {
            const int ribs = 16;
            float ri = r * 0.95f;
            float wall = Mathf.Lerp(y0, top, 0.42f);
            for (int i = 0; i < ribs; i++)
            {
                float a = i * Mathf.PI * 2f / ribs;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 foot = c + dir * ri + Vector3.up * y0;
                k.Box(foot + Vector3.up * (wall - y0) * 0.5f, new Vector3(0.1f, wall - y0, 0.1f), Steel);
                k.Box(foot + Vector3.up * 0.05f, new Vector3(0.2f, 0.06f, 0.2f), Carbon);
                // Dome rib from the wall top to the crown, as an elliptical arc.
                Vector3 prev = c + dir * ri + Vector3.up * wall;
                for (int s = 1; s <= 6; s++)
                {
                    float t = s / 6f * Mathf.PI * 0.5f;
                    Vector3 pt = c + dir * (ri * Mathf.Cos(t)) + Vector3.up * (wall + (top - wall) * Mathf.Sin(t));
                    k.Beam(prev, pt, 0.07f, Primer);
                    prev = pt;
                }
            }
            // Hoops.
            float[] hoopH = { Mathf.Lerp(y0, wall, 0.5f), wall, 0f, 0f };
            for (int h = 0; h < 2; h++)
                Hoop(k, c + Vector3.up * hoopH[h], ri, 0.07f, Primer);
            for (int s = 1; s <= 3; s++)
            {
                float t = s / 4f * Mathf.PI * 0.5f;
                Hoop(k, c + Vector3.up * (wall + (top - wall) * Mathf.Sin(t)), ri * Mathf.Cos(t), 0.06f, Steel);
            }
        }

        private static void Hoop(DetailBatch k, Vector3 center, float r, float thick, Color col)
        {
            const int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                k.Beam(center + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * r,
                    center + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * r, thick, col);
            }
        }

        private static void Scaffold(DetailBatch k, Vector3 c, float hx, float hz, float top)
        {
            const float bay = 1.25f, lift = 1.0f, depth = 0.55f;
            int lifts = Mathf.Max(1, Mathf.CeilToInt(top / lift));
            float height = lifts * lift + 0.9f;
            // Scaffold the two back faces and the right-hand face; the camera-facing front stays open.
            for (int side = 0; side < 3; side++)
            {
                Vector3 a, b, outN;
                switch (side)
                {
                    case 0: a = c + new Vector3(-hx, 0f, hz); b = c + new Vector3(hx, 0f, hz); outN = Vector3.forward; break;
                    case 1: a = c + new Vector3(hx, 0f, hz); b = c + new Vector3(hx, 0f, -hz); outN = Vector3.right; break;
                    default: a = c + new Vector3(-hx, 0f, -hz); b = c + new Vector3(-hx, 0f, hz); outN = Vector3.left; break;
                }
                float len = Vector3.Distance(a, b);
                int bays = Mathf.Max(1, Mathf.RoundToInt(len / bay));
                Vector3 along = (b - a) / bays;
                for (int i = 0; i <= bays; i++)
                {
                    Vector3 p = a + along * i;
                    Vector3 q = p + outN * depth;
                    // Standards go up one lift at a time, so the scaffold climbs with the build.
                    for (int l = 0; l <= lifts; l++)
                    {
                        float y0 = l * lift, y1 = Mathf.Min(height, (l + 1) * lift);
                        if (y1 <= y0) continue;
                        k.Box(p + Vector3.up * (y0 + y1) * 0.5f, new Vector3(0.045f, y1 - y0, 0.045f), ScaffoldYellow);
                        k.Box(q + Vector3.up * (y0 + y1) * 0.5f, new Vector3(0.045f, y1 - y0, 0.045f), ScaffoldYellow);
                    }
                    k.Box(p + Vector3.up * 0.02f, new Vector3(0.12f, 0.03f, 0.12f), Carbon);
                    k.Box(q + Vector3.up * 0.02f, new Vector3(0.12f, 0.03f, 0.12f), Carbon);
                    for (int l = 1; l <= lifts; l++)
                        k.Beam(p + Vector3.up * (l * lift), q + Vector3.up * (l * lift), 0.04f, ScaffoldYellow);
                    if (i == bays) continue;
                    Vector3 p2 = p + along, q2 = q + along;
                    for (int l = 1; l <= lifts; l++)
                    {
                        float y = l * lift;
                        k.Beam(p + Vector3.up * y, p2 + Vector3.up * y, 0.035f, ScaffoldYellow);
                        k.Beam(q + Vector3.up * y, q2 + Vector3.up * y, 0.035f, ScaffoldYellow);
                        k.Beam(q + Vector3.up * (y + 0.5f), q2 + Vector3.up * (y + 0.5f), 0.03f, ScaffoldYellow);
                        Vector3 mid = (p + q2) * 0.5f;
                        Quaternion rot = Quaternion.LookRotation(along.normalized);
                        k.Box(mid + Vector3.up * (y + 0.035f), new Vector3(depth * 0.92f, 0.035f, along.magnitude * 0.96f), rot, Plank);
                        k.Box(q + (along * 0.5f) + Vector3.up * (y + 0.1f), new Vector3(0.02f, 0.12f, along.magnitude), rot, Orange);
                    }
                    for (int l = 0; l < lifts; l++)
                        if ((i + l) % 2 == 0)
                            k.Beam(q + Vector3.up * (l * lift + 0.05f), q2 + Vector3.up * ((l + 1) * lift), 0.03f, ScaffoldYellow);
                }
            }
        }

        private static void TowerCrane(DetailBatch k, Vector3 c, float hx, float hz, float top)
        {
            Vector3 foot = c + new Vector3(hx + 0.85f, 0f, hz + 0.85f);
            float mastH = top + 2.4f;
            const float m = 0.34f;
            k.Box(foot + Vector3.up * 0.1f, new Vector3(1.0f, 0.2f, 1.0f), new Color(0.4f, 0.41f, 0.43f));
            Vector3[] legs =
            {
                new Vector3(-m, 0f, -m) * 0.5f, new Vector3(m, 0f, -m) * 0.5f,
                new Vector3(m, 0f, m) * 0.5f, new Vector3(-m, 0f, m) * 0.5f
            };
            for (int i = 0; i < 4; i++)
                k.Box(foot + legs[i] + Vector3.up * mastH * 0.5f, new Vector3(0.05f, mastH, 0.05f), ScaffoldYellow);
            int sections = Mathf.CeilToInt(mastH / 0.5f);
            for (int s = 0; s < sections; s++)
            {
                float y0 = s * mastH / sections, y1 = (s + 1) * mastH / sections;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = foot + legs[i], b = foot + legs[(i + 1) % 4];
                    k.Beam(a + Vector3.up * y0, b + Vector3.up * y1, 0.022f, ScaffoldYellow);
                    k.Beam(a + Vector3.up * y1, b + Vector3.up * y1, 0.022f, ScaffoldYellow);
                }
            }
            // Slewing unit, cab, jib over the site toward the camera, counter-jib and weights.
            Vector3 head = foot + Vector3.up * mastH;
            k.Box(head + Vector3.up * 0.08f, new Vector3(0.5f, 0.16f, 0.5f), Carbon);
            Vector3 jibDir = new Vector3(-1f, 0f, -1f).normalized;
            Quaternion jibRot = Quaternion.LookRotation(jibDir);
            k.Box(head + Vector3.up * 0.3f + Vector3.Cross(jibDir, Vector3.up) * 0.3f, new Vector3(0.34f, 0.3f, 0.36f), jibRot, Cream);
            k.Box(head + Vector3.up * 0.3f + Vector3.Cross(jibDir, Vector3.up) * 0.3f + jibDir * 0.18f, new Vector3(0.28f, 0.16f, 0.02f), jibRot, new Color(0.4f, 0.6f, 0.7f), new Color(0.2f, 0.45f, 0.55f));
            float jibLen = Mathf.Sqrt(hx * hx + hz * hz) * 2f + 1.2f;
            Vector3 j0 = head + Vector3.up * 0.2f;
            Vector3 j1 = j0 + jibDir * jibLen;
            Vector3 side = Vector3.Cross(jibDir, Vector3.up) * 0.14f;
            k.Beam(j0 + side, j1 + side, 0.04f, ScaffoldYellow);
            k.Beam(j0 - side, j1 - side, 0.04f, ScaffoldYellow);
            k.Beam(j0 + Vector3.up * 0.26f, j1 + Vector3.up * 0.26f, 0.04f, ScaffoldYellow);
            int panels = Mathf.CeilToInt(jibLen / 0.45f);
            for (int i = 0; i < panels; i++)
            {
                Vector3 a = Vector3.Lerp(j0, j1, (float)i / panels), b = Vector3.Lerp(j0, j1, (float)(i + 1) / panels);
                k.Beam(a + side, b + Vector3.up * 0.26f, 0.018f, ScaffoldYellow);
                k.Beam(a - side, b + Vector3.up * 0.26f, 0.018f, ScaffoldYellow);
            }
            Vector3 cj = j0 - jibDir * 1.6f;
            k.Beam(j0, cj, 0.09f, ScaffoldYellow);
            k.Box(cj + jibDir * 0.3f + Vector3.down * 0.05f, new Vector3(0.36f, 0.34f, 0.5f), jibRot, new Color(0.40f, 0.41f, 0.43f));
            // A-frame and pendant lines.
            Vector3 apex = head + Vector3.up * 1.0f;
            k.Beam(head + Vector3.up * 0.2f, apex, 0.05f, ScaffoldYellow);
            k.Beam(apex, j0 + jibDir * jibLen * 0.7f + Vector3.up * 0.26f, 0.012f, Carbon);
            k.Beam(apex, cj, 0.012f, Carbon);
            k.Ball(apex + Vector3.up * 0.06f, 0.08f, Orange, RedEmit);
            k.Ball(j1 + Vector3.up * 0.3f, 0.07f, Orange, RedEmit);
            // Trolley, hoist line and hook block over the middle of the site.
            Vector3 trolley = j0 + jibDir * (Vector3.Distance(new Vector3(foot.x, 0f, foot.z), c) + 0.2f);
            k.Box(trolley - Vector3.up * 0.04f, new Vector3(0.26f, 0.1f, 0.2f), jibRot, Carbon);
            float hookY = top + 0.6f;
            k.Beam(trolley - Vector3.up * 0.06f, new Vector3(trolley.x, hookY, trolley.z), 0.012f, Carbon);
            k.Box(new Vector3(trolley.x, hookY - 0.09f, trolley.z), new Vector3(0.16f, 0.18f, 0.1f), Orange);
            k.Box(new Vector3(trolley.x, hookY - 0.34f, trolley.z), new Vector3(0.9f, 0.05f, 0.06f), Primer);
        }
    }
}
