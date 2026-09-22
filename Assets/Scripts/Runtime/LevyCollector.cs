using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty 2 tax collectors. The Commons (Palace) houses two levy drones and every Watchtower
    /// (Guardhouse) one more. They walk from till to till, empty any building holding at least
    /// <see cref="MajestyEconomy.CollectMinimum"/>, and carry the bag home once it reaches
    /// <see cref="MajestyEconomy.ReturnAt"/> or nothing else is worth the walk. Pests that catch a
    /// drone snatch part of the bag. Not a hero class, never player-commanded.
    /// </summary>
    public sealed class LevyCollectorDirector
    {
        private readonly VillageExpansion _village;
        private readonly GameLoop _loop;
        private readonly List<LevyCollector> _collectors = new List<LevyCollector>(8);
        private readonly HashSet<ColonyStructure> _claimed = new HashSet<ColonyStructure>();
        private Transform _root;
        private float _rosterTimer;
        private int _pendingLog;
        private float _logTimer;

        public LevyCollectorDirector(VillageExpansion village, GameLoop loop)
        {
            _village = village;
            _loop = loop;
        }

        public int Count => _collectors.Count;
        public IReadOnlyList<LevyCollector> Collectors => _collectors;

        /// <summary>Gold currently riding in collector bags (not yet in the treasury).</summary>
        public int GoldInTransit
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _collectors.Count; i++)
                    if (_collectors[i] != null) n += _collectors[i].Carry;
                return n;
            }
        }

        public void Tick(float dt)
        {
            if (_village == null || _loop == null || dt <= 0f) return;

            FlushChestTills();

            _rosterTimer -= dt;
            if (_rosterTimer <= 0f)
            {
                _rosterTimer = 1f;
                SyncRoster();
            }

            for (int i = _collectors.Count - 1; i >= 0; i--)
            {
                var c = _collectors[i];
                if (c == null)
                {
                    _collectors.RemoveAt(i);
                    continue;
                }
                c.Tick(dt);
            }

            _logTimer -= dt;
            if (_logTimer <= 0f && _pendingLog > 0)
            {
                _logTimer = 45f;
                _loop.LogOverseer($"Tax collectors carried {_pendingLog} CRED home.");
                _pendingLog = 0;
            }
        }

        /// <summary>Commons and Watchtowers are the vault — gold dropped in their tills is already home.</summary>
        private void FlushChestTills()
        {
            var list = _village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsTreasuryChest || s.LevyPurse <= 0) continue;
                int n = s.CollectLevy();
                if (n <= 0) continue;
                _loop.Resources?.Add(ResourceId.Metals, n);
                _loop.NoteTreasuryIncome(n);
            }
        }

        private void SyncRoster()
        {
            int commons = _village.CountAlive(BuildingCategory.Commons);
            int towers = _village.CountAlive(BuildingCategory.Watchtower);
            int want = MajestyEconomy.CollectorsFor(commons, towers);

            while (_collectors.Count > want)
            {
                int last = _collectors.Count - 1;
                var c = _collectors[last];
                _collectors.RemoveAt(last);
                if (c != null)
                {
                    if (c.Carry > 0) _loop.Resources?.Add(ResourceId.Metals, c.Carry);
                    Release(c.Target);
                    Object.Destroy(c.gameObject);
                }
            }

            while (_collectors.Count < want)
            {
                var home = HomeFor(_collectors.Count, commons);
                if (home == null) break;
                if (_root == null)
                {
                    var go = new GameObject("TaxCollectors");
                    _root = go.transform;
                }
                var c = LevyCollector.Spawn(_root, this, home, _collectors.Count);
                if (c == null) break;
                _collectors.Add(c);
            }
        }

        /// <summary>First two collectors live at Commons, the rest one per Watchtower.</summary>
        private ColonyStructure HomeFor(int index, int commons)
        {
            if (index < commons * MajestyEconomy.CollectorsPerCommons || commons <= 0)
                return _village.CommonsHub();
            int towerIndex = index - commons * MajestyEconomy.CollectorsPerCommons;
            int seen = 0;
            var list = _village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive || !s.IsWatchtower) continue;
                if (seen++ == towerIndex) return s;
            }
            return _village.CommonsHub();
        }

        internal GameLoop Loop => _loop;
        internal VillageExpansion Village => _village;

        /// <summary>Best unclaimed till worth walking to: gold per metre, above the collect minimum.</summary>
        internal ColonyStructure ClaimNextTill(Vector3 from, ColonyStructure previous)
        {
            Release(previous);
            ColonyStructure best = null;
            float bestScore = 0f;
            var list = _village.Structures;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || !s.IsAlive || s.IsTreasuryChest) continue;
                if (s.LevyPurse < MajestyEconomy.CollectMinimum) continue;
                if (_claimed.Contains(s)) continue;
                float d = Flat(from, s.WorldPosition);
                float score = s.LevyPurse / (d + 12f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            if (best != null) _claimed.Add(best);
            return best;
        }

        internal void Release(ColonyStructure s)
        {
            if (s != null) _claimed.Remove(s);
        }

        internal ColonyStructure ChestFor(Vector3 from) => _village.NearestLevyChest(from);

        internal void Deposit(int amount, Vector3 at)
        {
            if (amount <= 0) return;
            _loop.NoteCollectorDeposit(amount, at);
            _pendingLog += amount;
        }

        internal DustStalkerAgent PestNear(Vector3 at, float radius)
        {
            var stalkers = _loop.Stalkers;
            if (stalkers == null) return null;
            float r2 = radius * radius;
            for (int i = 0; i < stalkers.Count; i++)
            {
                var s = stalkers[i];
                if (s == null || !s.IsAlive) continue;
                Vector3 d = s.transform.position - at;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) return s;
            }
            return null;
        }

        internal static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>One levy drone: a small hovering bag-carrier. Driven by <see cref="LevyCollectorDirector"/>.</summary>
    public sealed class LevyCollector : MonoBehaviour
    {
        private enum State { Resting, ToTill, ToChest }

        private LevyCollectorDirector _director;
        private ColonyStructure _home;
        private NavMeshAgent _agent;
        private Transform _visual;
        private Transform _bag;
        private State _state;
        private float _rest;
        private float _robCooldown;
        private float _pestCheck;
        private float _stuckTimer;
        private Vector3 _lastPos;
        private float _bob;

        public int Carry { get; private set; }
        public ColonyStructure Target { get; private set; }
        public string Status { get; private set; } = "resting";

        private static Material _bodyMat;
        private static Material _goldMat;
        private static Material _eyeMat;

        public static LevyCollector Spawn(Transform parent, LevyCollectorDirector director, ColonyStructure home, int index)
        {
            if (home == null) return null;
            var go = new GameObject($"TaxCollector_{index}");
            go.transform.SetParent(parent, false);
            float a = index * 2.39996f;
            Vector3 pos = home.WorldPosition + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.2f;
            pos.y = TerrainDataBake.GroundHeight(pos);
            go.transform.position = pos;

            var c = go.AddComponent<LevyCollector>();
            c._director = director;
            c._home = home;
            c._bob = index * 1.3f;
            c._rest = 1.5f + index * 0.7f;
            c.BuildVisual();
            c.BindNav();
            TerrainFollow.Attach(go);
            return c;
        }

        private void BuildVisual()
        {
            EnsureMaterials();
            _visual = new GameObject("Drone").transform;
            _visual.SetParent(transform, false);
            _visual.localPosition = new Vector3(0f, 1.15f, 0f);

            var body = Prim(PrimitiveType.Sphere, _visual, Vector3.zero, new Vector3(0.62f, 0.42f, 0.62f), _bodyMat);
            body.name = "Body";
            Prim(PrimitiveType.Cylinder, _visual, new Vector3(0f, -0.2f, 0f), new Vector3(0.7f, 0.03f, 0.7f), _goldMat).name = "Ring";
            Prim(PrimitiveType.Sphere, _visual, new Vector3(0f, 0.04f, 0.27f), new Vector3(0.16f, 0.1f, 0.08f), _eyeMat).name = "Eye";
            Prim(PrimitiveType.Cylinder, _visual, new Vector3(0f, 0.3f, 0f), new Vector3(0.03f, 0.14f, 0.03f), _bodyMat).name = "Mast";
            _bag = Prim(PrimitiveType.Cube, _visual, new Vector3(0f, -0.42f, 0f), Vector3.one * 0.18f, _goldMat).transform;
            _bag.name = "Bag";
            _bag.gameObject.SetActive(false);
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return g;
        }

        private static void EnsureMaterials()
        {
            if (_bodyMat != null) return;
            _bodyMat = Mat(new Color(0.22f, 0.24f, 0.27f), 0.55f, false);
            _goldMat = Mat(new Color(0.98f, 0.78f, 0.2f), 0.7f, false);
            _eyeMat = Mat(new Color(1f, 0.72f, 0.2f), 0.2f, true);
        }

        private static Material Mat(Color c, float smooth, bool emissive)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", emissive ? 0f : 0.6f);
            if (emissive && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 2.2f);
            }
            return m;
        }

        private void BindNav()
        {
            var nav = _director.Loop != null ? _director.Loop.CampusNav : null;
            if (nav == null || !nav.IsReady) return;
            if (!nav.SamplePosition(transform.position, out Vector3 onMesh)) return;
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = MajestyEconomy.CollectorSpeed;
            _agent.angularSpeed = 540f;
            _agent.acceleration = 14f;
            _agent.radius = 0.3f;
            _agent.height = 1.6f;
            _agent.stoppingDistance = MajestyEconomy.CollectorArrive * 0.7f;
            _agent.avoidancePriority = 90;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.updateRotation = true;
            _agent.Warp(onMesh);
        }

        /// <summary>Sim-step update from the director (respects pause and speed).</summary>
        public void Tick(float dt)
        {
            _bob += dt * 2.4f;
            if (_visual != null)
                _visual.localPosition = new Vector3(0f, 1.15f + Mathf.Sin(_bob) * 0.08f, 0f);
            _robCooldown = Mathf.Max(0f, _robCooldown - dt);

            if (_home == null || !_home.IsAlive)
                _home = _director.ChestFor(transform.position);

            if (Carry > 0)
            {
                _pestCheck -= dt;
                if (_pestCheck <= 0f)
                {
                    _pestCheck = 0.5f;
                    TryGetRobbed();
                }
            }

            switch (_state)
            {
                case State.Resting:
                    Status = "resting";
                    _rest -= dt;
                    if (_rest > 0f) return;
                    _rest = 2f;
                    PickNext();
                    break;

                case State.ToTill:
                    if (Target == null || !Target.IsAlive || Target.LevyPurse <= 0)
                    {
                        PickNext();
                        return;
                    }
                    Status = $"walking to {Target.DisplayName}";
                    if (MoveTo(Target.WorldPosition, dt))
                    {
                        int take = Target.CollectLevy();
                        Carry += take;
                        RefreshBag();
                        PickNext();
                    }
                    break;

                case State.ToChest:
                    var chest = _director.ChestFor(transform.position);
                    if (chest == null)
                    {
                        _state = State.Resting;
                        return;
                    }
                    Status = $"carrying {Carry} CRED home";
                    if (MoveTo(chest.WorldPosition, dt))
                    {
                        _director.Deposit(Carry, chest.WorldPosition);
                        Carry = 0;
                        RefreshBag();
                        _home = chest;
                        _state = State.Resting;
                        _rest = 2.5f;
                    }
                    break;
            }
        }

        private void PickNext()
        {
            if (Carry >= MajestyEconomy.ReturnAt)
            {
                GoHome();
                return;
            }

            var next = _director.ClaimNextTill(transform.position, Target);
            Target = next;
            if (next != null)
            {
                _state = State.ToTill;
                _stuckTimer = 0f;
                return;
            }

            if (Carry > 0) GoHome();
            else
            {
                _state = State.Resting;
                _rest = 3f;
            }
        }

        private void GoHome()
        {
            _director.Release(Target);
            Target = null;
            _state = State.ToChest;
            _stuckTimer = 0f;
        }

        private void TryGetRobbed()
        {
            if (_robCooldown > 0f || Carry <= 0) return;
            var pest = _director.PestNear(transform.position, 2.6f);
            if (pest == null) return;
            int stolen = Mathf.Max(1, Mathf.RoundToInt(Carry * MajestyEconomy.CollectorRobShare));
            Carry -= stolen;
            RefreshBag();
            _robCooldown = 8f;
            _director.Loop.NoteLevyStolen(stolen, transform.position, false);
            GoHome();
        }

        /// <summary>Walk toward a building; true when close enough to hand over gold.</summary>
        private bool MoveTo(Vector3 target, float dt)
        {
            Vector3 p = transform.position;
            if (LevyCollectorDirector.Flat(p, target) <= MajestyEconomy.CollectorArrive)
            {
                if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
                return true;
            }

            if (_agent != null && _agent.isOnNavMesh)
            {
                var nav = _director.Loop.CampusNav;
                Vector3 dest = target;
                if (nav != null && nav.SamplePosition(target, out Vector3 onMesh)) dest = onMesh;
                if (!_agent.pathPending && (_agent.destination - dest).sqrMagnitude > 0.5f)
                    _agent.SetDestination(dest);
            }
            else
            {
                Vector3 step = Vector3.MoveTowards(new Vector3(p.x, 0f, p.z), new Vector3(target.x, 0f, target.z),
                    MajestyEconomy.CollectorSpeed * dt);
                transform.position = new Vector3(step.x, p.y, step.z);
                Vector3 look = new Vector3(target.x - p.x, 0f, target.z - p.z);
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), dt * 6f);
            }

            // Stuck guard: a blocked path should not freeze gold forever.
            if ((p - _lastPos).sqrMagnitude < 0.0004f) _stuckTimer += dt;
            else _stuckTimer = 0f;
            _lastPos = p;
            if (_stuckTimer > 12f)
            {
                _stuckTimer = 0f;
                if (_agent != null) _agent.enabled = false;
                _agent = null;
            }
            return false;
        }

        private void RefreshBag()
        {
            if (_bag == null) return;
            bool show = Carry > 0;
            _bag.gameObject.SetActive(show);
            if (!show) return;
            float s = Mathf.Lerp(0.14f, 0.34f, Mathf.Clamp01(Carry / (float)(MajestyEconomy.ReturnAt * 2)));
            _bag.localScale = Vector3.one * s;
        }

        private void OnDestroy()
        {
            _director?.Release(Target);
        }
    }
}
