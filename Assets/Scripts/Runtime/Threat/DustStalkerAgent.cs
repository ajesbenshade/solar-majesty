using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Minimal non-sentient lunar fauna. Wanders, raises ThreatPressure when prey is near,
    /// and despawns when a claimed ClearThreat flag is worked down next to it.
    /// No full combat system — pressure + death only so personalities diverge under danger.
    /// </summary>
    public class DustStalkerAgent : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.35f;
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float wanderRetargetSeconds = 4f;

        [Header("Aggro / pressure")]
        [SerializeField] private float aggroRange = 11f;
        [Tooltip("Pressure added to ThreatPressure while aggro is active (stacked via peak).")]
        [SerializeField] [Range(0f, 1f)] private float aggroPressure = 0.55f;
        [SerializeField] [Range(0f, 1f)] private float idlePressure = 0.08f;

        [Header("Defeat (via ClearThreat flags)")]
        [SerializeField] private float defeatLinkRadius = 4.5f;
        [SerializeField] private float maxHealth = 28f;
        [SerializeField] private float damagePerSecondWhileCleared = 9f;

        [Header("Phase 2A bite")]
        [SerializeField] private float biteRange = 3.2f;
        [SerializeField] private float biteDamagePerSecond = 0.18f;

        [Header("Visual")]
        [SerializeField] private Color stalkerColor = new Color(0.45f, 0.08f, 0.1f);
        [SerializeField] private float bobAmp = 0.12f;
        [SerializeField] private float bobSpeed = 3.2f;

        private static int _nextSourceId = 1;

        private ThreatPressure _threat;
        private FlagManager _flags;
        private Vector3 _home;
        private Vector3 _wanderTarget;
        private float _wanderTimer;
        private float _health;
        private bool _aggro;
        private object _sourceId;
        private Vector3 _baseScale;
        private float _yBase;
        private TextMesh _label;
        private Renderer _rend;

        public bool IsAlive => _health > 0f;
        public bool IsAggro => _aggro;
        public float Health01 => maxHealth > 0f ? Mathf.Clamp01(_health / maxHealth) : 0f;
        public FaunaKind Kind { get; private set; } = FaunaKind.Stalker;
        public string RoleLabel => string.IsNullOrEmpty(_roleNoun) ? Kind.ToString() : _roleNoun;

        public static bool IsCampusPest(FaunaKind kind) =>
            kind == FaunaKind.Mite || kind == FaunaKind.Leech ||
            kind == FaunaKind.Wisp || kind == FaunaKind.Tick ||
            kind == FaunaKind.Creeper || kind == FaunaKind.Hopper;

        public static bool UsesDefendCounter(FaunaKind kind) =>
            kind == FaunaKind.Mite || kind == FaunaKind.Tick || kind == FaunaKind.Creeper;

        /// <summary>Opportunistic combat from a hunting specialist (no bounty flag required).</summary>
        public void ApplyCombatDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _health -= amount;
            transform.localScale = _baseScale * (1f + Mathf.Sin(Time.time * 18f) * 0.08f);
            if (_health <= 0f)
                Die();
        }

        /// <summary>Instant kill when a ClearThreat job clears the owning lair.</summary>
        public void ApplyClearThreatKill()
        {
            if (!IsAlive) return;
            Die();
        }

        private GameLoop _loop;
        private float _stealTimer;
        private bool _raiding;
        private bool _preferMines;
        private bool _preferFarms;
        private bool _retreating;
        private float _retreatTimer;
        private string _roleNoun;
        private string _roleVerb;

        public void Initialize(ThreatPressure threat, FlagManager flags, Vector3 home, GameLoop loop = null)
        {
            _threat = threat;
            _flags = flags;
            _loop = loop;
            _home = home;
            _sourceId = _nextSourceId++;
            _health = maxHealth;
            _baseScale = transform.localScale;
            transform.position = home;
            ColonyVisualUtility.SnapToGround(gameObject);
            _home = transform.position;
            _yBase = transform.position.y;
            PickWanderTarget();
            EnsureVisual();
            gameObject.name = "DustStalker";
        }

        /// <summary>Retarget this agent as campus fauna after Initialize. Stalker is the default.</summary>
        public void SetKind(FaunaKind kind)
        {
            Kind = kind;
            switch (kind)
            {
                case FaunaKind.Mite:
                    gameObject.name = "RegolithMite";
                    stalkerColor = UnitPlaceholderFactory.MiteTint;
                    maxHealth = 16f;
                    _health = maxHealth;
                    moveSpeed = 1.7f;
                    wanderRadius = 7f;
                    aggroPressure = 0.32f;
                    idlePressure = 0.05f;
                    defeatLinkRadius = 5f;
                    biteDamagePerSecond = 0.06f;
                    _roleNoun = "MITE";
                    _roleVerb = "MITE STEAL";
                    break;
                case FaunaKind.Leech:
                    gameObject.name = "WattLeech";
                    stalkerColor = UnitPlaceholderFactory.LeechTint;
                    maxHealth = 18f;
                    _health = maxHealth;
                    moveSpeed = 1.15f;
                    wanderRadius = 8f;
                    aggroPressure = 0.40f;
                    idlePressure = 0.06f;
                    defeatLinkRadius = 5f;
                    biteDamagePerSecond = 0.04f;
                    _roleNoun = "LEECH";
                    _roleVerb = "LEECH DRAIN";
                    break;
                case FaunaKind.Wisp:
                    gameObject.name = "IceWisp";
                    stalkerColor = UnitPlaceholderFactory.WispTint;
                    maxHealth = 14f;
                    _health = maxHealth;
                    moveSpeed = 1.4f;
                    wanderRadius = 10f;
                    aggroPressure = 0.36f;
                    idlePressure = 0.07f;
                    defeatLinkRadius = 5.5f;
                    biteDamagePerSecond = 0.03f;
                    bobAmp = 0.22f;
                    bobSpeed = 4.2f;
                    _roleNoun = "WISP";
                    _roleVerb = "WISP DRAIN";
                    break;
                case FaunaKind.Tick:
                    gameObject.name = "RockTick";
                    stalkerColor = UnitPlaceholderFactory.TickTint;
                    maxHealth = 12f;
                    _health = maxHealth;
                    moveSpeed = 1.95f;
                    wanderRadius = 6f;
                    aggroPressure = 0.28f;
                    idlePressure = 0.05f;
                    defeatLinkRadius = 4.5f;
                    biteDamagePerSecond = 0.05f;
                    _preferMines = true;
                    _roleNoun = "TICK";
                    _roleVerb = "TICK STEAL";
                    break;
                case FaunaKind.Creeper:
                    gameObject.name = "SoilCreeper";
                    stalkerColor = UnitPlaceholderFactory.CreeperTint;
                    maxHealth = 22f;
                    _health = maxHealth;
                    moveSpeed = 1.05f;
                    wanderRadius = 6f;
                    aggroPressure = 0.30f;
                    idlePressure = 0.05f;
                    defeatLinkRadius = 5.2f;
                    biteDamagePerSecond = 0.05f;
                    _preferFarms = true;
                    _roleNoun = "CREEPER";
                    _roleVerb = "CREEP STEAL";
                    break;
                case FaunaKind.Hopper:
                    gameObject.name = "AshHopper";
                    stalkerColor = UnitPlaceholderFactory.HopperTint;
                    maxHealth = 15f;
                    _health = maxHealth;
                    moveSpeed = 2.05f;
                    wanderRadius = 9f;
                    aggroPressure = 0.38f;
                    idlePressure = 0.06f;
                    defeatLinkRadius = 5f;
                    biteDamagePerSecond = 0.07f;
                    bobAmp = 0.18f;
                    bobSpeed = 5.1f;
                    _roleNoun = "HOPPER";
                    _roleVerb = "HAB RAID";
                    break;
                default:
                    gameObject.name = "DustStalker";
                    _roleNoun = "AGGRO";
                    _roleVerb = "AGGRO";
                    break;
            }
            _baseScale = transform.localScale;
            if (_rend != null && !IndustrialArtDressing.HasArt(gameObject))
                SetColor(_rend, stalkerColor);
            if (_label != null)
                _label.color = LabelColor(kind);
        }

        /// <summary>Body-native speed, aggro, and tints. Does not change SpecialistBrain.</summary>
        public void ApplyBodyTune(CelestialBodyProfile body)
        {
            if (body == null) return;
            moveSpeed *= Mathf.Clamp(body.FaunaSpeedScale, 0.5f, 1.6f);
            aggroRange *= Mathf.Clamp(body.FaunaAggroScale, 0.6f, 1.6f);
            aggroPressure = Mathf.Clamp01(aggroPressure * Mathf.Clamp(body.FaunaAggroScale, 0.6f, 1.5f));

            if (Kind == FaunaKind.Mite)
            {
                _preferMines = body.PreferMineMites;
                if (body.FaunaMiteTint.a > 0.05f)
                    stalkerColor = body.FaunaMiteTint;
                if (body.Id == CelestialBodyId.Belt)
                {
                    gameObject.name = "RockMite";
                    _roleNoun = "ROCK MITE";
                    _roleVerb = "ORE STEAL";
                    if (_label != null) _label.color = new Color(0.92f, 0.72f, 0.42f);
                }
            }
            else if (Kind == FaunaKind.Leech)
            {
                if (body.FaunaLeechTint.a > 0.05f)
                    stalkerColor = body.FaunaLeechTint;
                if (body.Id == CelestialBodyId.Europa)
                {
                    gameObject.name = "FissureLeech";
                    _roleNoun = "FISSURE LEECH";
                    _roleVerb = "FISSURE DRAIN";
                    if (_label != null) _label.color = new Color(0.55f, 0.95f, 1f);
                }
            }
            else if (Kind == FaunaKind.Wisp)
            {
                if (body.FaunaWispTint.a > 0.05f)
                    stalkerColor = body.FaunaWispTint;
                if (body.Id == CelestialBodyId.Mars)
                {
                    gameObject.name = "DustWisp";
                    stalkerColor = body.FaunaWispTint.a > 0.05f
                        ? body.FaunaWispTint
                        : new Color(0.82f, 0.52f, 0.28f);
                    _roleNoun = "DUST WISP";
                    _roleVerb = "DUST DRAIN";
                    if (_label != null) _label.color = new Color(1f, 0.72f, 0.42f);
                }
                else if (body.Id == CelestialBodyId.Europa)
                {
                    gameObject.name = "IceWisp";
                    _roleNoun = "ICE WISP";
                    _roleVerb = "ICE DRAIN";
                    if (_label != null) _label.color = new Color(0.65f, 0.95f, 1f);
                }
            }
            else if (Kind == FaunaKind.Tick)
            {
                _preferMines = true;
                if (body.FaunaTickTint.a > 0.05f)
                    stalkerColor = body.FaunaTickTint;
                if (body.Id == CelestialBodyId.Belt)
                {
                    gameObject.name = "RockTick";
                    _roleNoun = "ROCK TICK";
                    _roleVerb = "ORE STEAL";
                    if (_label != null) _label.color = new Color(0.88f, 0.68f, 0.38f);
                }
                else if (body.Id == CelestialBodyId.Luna || body.Id == CelestialBodyId.Earth)
                {
                    gameObject.name = "DustTick";
                    _roleNoun = "DUST TICK";
                    _roleVerb = "CAMP STEAL";
                    if (_label != null) _label.color = new Color(0.85f, 0.62f, 0.42f);
                }
            }
            else if (Kind == FaunaKind.Creeper)
            {
                _preferFarms = true;
                if (body.FaunaCreeperTint.a > 0.05f)
                    stalkerColor = body.FaunaCreeperTint;
                if (body.Id == CelestialBodyId.Mars)
                {
                    gameObject.name = "DustCreeper";
                    _roleNoun = "DUST CREEPER";
                    _roleVerb = "FARM STEAL";
                    if (_label != null) _label.color = new Color(1f, 0.62f, 0.32f);
                }
                else if (body.Id == CelestialBodyId.Europa)
                {
                    gameObject.name = "IceCreeper";
                    _roleNoun = "ICE CREEPER";
                    _roleVerb = "ICE STEAL";
                    if (_label != null) _label.color = new Color(0.72f, 0.92f, 0.95f);
                }
                else
                {
                    gameObject.name = "SoilCreeper";
                    _roleNoun = "SOIL CREEPER";
                    _roleVerb = "FARM STEAL";
                    if (_label != null) _label.color = new Color(0.62f, 0.82f, 0.32f);
                }
            }
            else if (Kind == FaunaKind.Hopper)
            {
                if (body.FaunaHopperTint.a > 0.05f)
                    stalkerColor = body.FaunaHopperTint;
                if (body.Id == CelestialBodyId.Mars)
                {
                    gameObject.name = "DustHopper";
                    _roleNoun = "DUST HOPPER";
                    _roleVerb = "HAB RAID";
                    if (_label != null) _label.color = new Color(1f, 0.68f, 0.38f);
                }
                else if (body.Id == CelestialBodyId.Belt)
                {
                    gameObject.name = "ShardHopper";
                    _roleNoun = "SHARD HOPPER";
                    _roleVerb = "HAB RAID";
                    if (_label != null) _label.color = new Color(0.82f, 0.78f, 0.62f);
                }
                else
                {
                    gameObject.name = "AshHopper";
                    _roleNoun = "ASH HOPPER";
                    _roleVerb = "HAB RAID";
                    if (_label != null) _label.color = new Color(0.85f, 0.82f, 0.72f);
                }
            }

            if (_rend != null && !IndustrialArtDressing.HasArt(gameObject))
                SetColor(_rend, stalkerColor);
        }

        /// <summary>Scatter after dens go quiet. Despawns off-campus without a death burst.</summary>
        public void BeginRetreat()
        {
            if (_retreating || !IsAlive) return;
            _retreating = true;
            _aggro = false;
            _raiding = false;
            Vector3 away = transform.position - ColonyLayout.CampusOrigin;
            away.y = 0f;
            if (away.sqrMagnitude < 1f) away = Vector3.forward;
            _wanderTarget = transform.position + away.normalized * 48f;
            _retreatTimer = 11f;
            _threat?.Clear(_sourceId);
        }

        private void Update()
        {
            if (!IsAlive || _threat == null) return;

            float dt = Time.deltaTime;
            if (_retreating)
            {
                TickRetreat(dt);
                TickPresentation(dt);
                return;
            }
            if (TickRole(dt))
            {
                TickDefeat(dt);
                TickPresentation(dt);
                return;
            }
            TickWander(dt);
            TickAggroAndPressure();
            TickBite(dt);
            TickDefeat(dt);
            TickPresentation(dt);
        }

        private bool TickRole(float dt)
        {
            _raiding = false;
            switch (Kind)
            {
                case FaunaKind.Mite:
                case FaunaKind.Tick:
                case FaunaKind.Creeper:
                    return TickRaidExtractor(dt);
                case FaunaKind.Leech:
                case FaunaKind.Wisp:
                    return TickRaidPower(dt);
                case FaunaKind.Hopper:
                    return TickRaidHabitat(dt);
                default: return TickRaidVillage(dt);
            }
        }

        /// <summary>Village HABs are the outer ring — raid those before the main campus.</summary>
        private bool TickRaidVillage(float dt)
        {
            if (_loop?.Village == null) return false;
            var hab = _loop.Village.NearestVillageHab(transform.position, 36f);
            if (hab == null || !hab.IsAlive) return false;

            Vector3 dest = hab.WorldPosition;
            dest.y = transform.position.y;
            float dist = Vector3.Distance(Flat(transform.position), Flat(dest));

            var specialists = _loop.Agents;
            if (specialists != null)
            {
                for (int i = 0; i < specialists.Count; i++)
                {
                    var s = specialists[i];
                    if (s == null || s.IsIncapacitated) continue;
                    if ((Flat(s.transform.position) - Flat(transform.position)).sqrMagnitude < 16f)
                        return false;
                }
            }

            _aggro = true;
            _threat?.Report(_sourceId, aggroPressure);
            if (dist > 2.4f)
            {
                transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * 1.15f * dt);
                return true;
            }

            hab.ApplyRaidDamage(7f * dt);
            _raiding = true;
            return true;
        }

        private bool TickRaidExtractor(float dt)
        {
            if (_loop?.Village == null) return false;
            ColonyStructure camp = null;
            if (_preferFarms)
                camp = _loop.Village.NearestByCategory(transform.position, 42f, BuildingCategory.Farm);
            if (camp == null && _preferMines)
                camp = _loop.Village.NearestByCategory(
                    transform.position, 42f, BuildingCategory.Mine, BuildingCategory.Mining);
            if (camp == null)
                camp = _loop.Village.NearestExtractor(transform.position, 42f);
            if (camp == null || !camp.IsAlive) return false;
            return TickRaidStructure(dt, camp, StealFromCamp);
        }

        private bool TickRaidHabitat(float dt)
        {
            if (_loop?.Village == null) return false;
            var hab = _loop.Village.NearestByCategory(transform.position, 42f, BuildingCategory.Habitat);
            if (hab == null || !hab.IsAlive)
                hab = _loop.Village.NearestVillageHab(transform.position, 42f);
            if (hab == null || !hab.IsAlive) return false;
            return TickRaidStructure(dt, hab, StealLifeSupport);
        }

        private void TickRetreat(float dt)
        {
            Vector3 dest = _wanderTarget;
            dest.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * 1.45f * dt);
            _retreatTimer -= dt;
            if (_retreatTimer <= 0f ||
                Vector3.Distance(Flat(transform.position), Flat(_wanderTarget)) < 1.2f)
                DespawnQuiet();
        }

        private bool TickRaidPower(float dt)
        {
            if (_loop?.Village == null) return false;
            var node = _loop.Village.NearestPower(transform.position, 42f);
            if (node == null || !node.IsAlive) return false;
            return TickRaidStructure(dt, node, DrainPower);
        }

        private bool TickRaidStructure(float dt, ColonyStructure target, System.Action steal)
        {
            Vector3 dest = target.WorldPosition;
            dest.y = transform.position.y;
            float dist = Vector3.Distance(Flat(transform.position), Flat(dest));

            _aggro = true;
            _raiding = true;
            _threat?.Report(_sourceId, aggroPressure);
            if (dist > 2.4f)
            {
                transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * 1.2f * dt);
                return true;
            }

            target.ApplyRaidDamage(4f * dt);
            _stealTimer += dt;
            if (_stealTimer >= 0.8f)
            {
                _stealTimer = 0f;
                steal?.Invoke();
            }
            return true;
        }

        private void StealFromCamp()
        {
            if (_loop?.Resources == null) return;
            var camp = _loop.Village?.NearestExtractor(transform.position, 4f);
            if (camp == null) return;
            if (camp.Category == BuildingCategory.Farm)
                _loop.Resources.SpendUpTo(ResourceId.WaterIce, 1);
            else if (camp.Category == BuildingCategory.Mine)
                _loop.Resources.SpendUpTo(ResourceId.Metals, 1);
            else
                _loop.Resources.SpendUpTo(ResourceId.Regolith, 1);
        }

        private void DrainPower()
        {
            _loop?.Resources?.SpendUpTo(ResourceId.Power, 1);
        }

        private void StealLifeSupport()
        {
            _loop?.Resources?.SpendUpTo(ResourceId.WaterIce, 1);
        }

        private void TickWander(float dt)
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f || Vector3.Distance(Flat(transform.position), Flat(_wanderTarget)) < 0.4f)
                PickWanderTarget();

            Vector3 pos = transform.position;
            Vector3 target = _wanderTarget;
            target.y = pos.y;

            // Slow, readable crawl; slightly faster when aggro (harassing).
            float speed = _aggro ? moveSpeed * 1.25f : moveSpeed;
            transform.position = Vector3.MoveTowards(pos, target, speed * dt);
        }

        private void TickAggroAndPressure()
        {
            _aggro = false;
            var specialists = FindObjectsByType<SpecialistAgent>();
            float aggroSq = aggroRange * aggroRange;
            Vector3 me = Flat(transform.position);

            for (int i = 0; i < specialists.Length; i++)
            {
                var s = specialists[i];
                if (s == null) continue;
                if ((Flat(s.transform.position) - me).sqrMagnitude <= aggroSq)
                {
                    _aggro = true;
                    break;
                }
            }

            // Optional: buildings also count as "prey" via Building root cubes — skip full system;
            // specialists alone are enough for the brain to feel danger.

            float pressure = _aggro ? aggroPressure : idlePressure;
            _threat.Report(_sourceId, pressure);
        }

        private void TickBite(float dt)
        {
            if (!_aggro) return;

            var specialists = FindObjectsByType<SpecialistAgent>();
            Vector3 me = Flat(transform.position);
            SpecialistAgent nearest = null;
            float best = biteRange * biteRange;

            for (int i = 0; i < specialists.Length; i++)
            {
                var s = specialists[i];
                if (s == null || s.IsIncapacitated) continue;
                float sq = (Flat(s.transform.position) - me).sqrMagnitude;
                if (sq <= best)
                {
                    best = sq;
                    nearest = s;
                }
            }

            if (nearest == null) return;

            // Crawl toward prey while biting.
            Vector3 prey = nearest.transform.position;
            prey.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, prey, moveSpeed * 0.85f * dt);
            nearest.ApplyDamage(biteDamagePerSecond * dt);
        }

        private void TickDefeat(float dt)
        {
            if (_flags == null) return;

            // Nearby claimed/worked counter-flag damages this fauna.
            FlagType counter = UsesDefendCounter(Kind)
                ? FlagType.DefendArea
                : FlagType.ClearThreat;
            var list = _flags.Flags;
            Vector3 me = Flat(transform.position);
            float linkSq = defeatLinkRadius * defeatLinkRadius;
            bool beingCleared = false;

            for (int i = 0; i < list.Count; i++)
            {
                FlagHandle f = list[i];
                if (f == null || f.Data == null) continue;
                if (f.Data.flagType != counter) continue;
                if ((Flat(f.WorldPosition) - me).sqrMagnitude > linkSq) continue;

                // Claimed or any remaining work in range = player posted a clear job here.
                if (f.ClaimCount > 0 || _flags.GetWorkRemaining(f) < f.Data.workRequired - 0.05f)
                {
                    beingCleared = true;
                    break;
                }
            }

            if (!beingCleared) return;

            _health -= damagePerSecondWhileCleared * dt;
            // Hurt feedback
            transform.localScale = _baseScale * (1f + Mathf.Sin(Time.time * 18f) * 0.08f);

            if (_health <= 0f)
                Die();
        }

        private void TickPresentation(float dt)
        {
            // Bob for silhouette readability on isometric view.
            Vector3 p = transform.position;
            p.y = _yBase + Mathf.Sin(Time.time * bobSpeed) * bobAmp;
            transform.position = p;

            if (_label != null)
            {
                bool show = _aggro || _retreating || Kind != FaunaKind.Stalker;
                _label.gameObject.SetActive(show);
                if (show)
                {
                    if (_retreating)
                        _label.text = "SCATTER";
                    else if (IsCampusPest(Kind))
                        _label.text = _raiding ? (_roleVerb ?? "RAID") : (_roleNoun ?? "PEST");
                    else
                        _label.text = "AGGRO";
                    if (Camera.main != null)
                    {
                        _label.transform.rotation = Quaternion.LookRotation(
                            _label.transform.position - Camera.main.transform.position);
                    }
                }
            }

            // Aggro = brighter overlay on placeholders only — authored art keeps hide/plate slots.
            if (_rend != null && !IndustrialArtDressing.HasArt(gameObject))
            {
                Color c = _aggro
                    ? Color.Lerp(stalkerColor, new Color(1f, 0.15f, 0.1f), 0.55f)
                    : stalkerColor;
                SetColor(_rend, c);
            }
            else if (IndustrialArtDressing.HasArt(gameObject))
            {
                IndustrialArtDressing.SetTintOverlay(
                    gameObject,
                    _aggro ? new Color(1.15f, 0.85f, 0.8f) : Color.white);
            }
        }

        private void Die()
        {
            _threat?.Clear(_sourceId);
            DemoAudio.PlayStalkerDeath();
            DemoVfx.DeathBurst(transform.position, stalkerColor);
            string who = Kind switch
            {
                FaunaKind.Mite => string.IsNullOrEmpty(_roleNoun) ? "Regolith Mite" : _roleNoun,
                FaunaKind.Leech => string.IsNullOrEmpty(_roleNoun) ? "Watt Leech" : _roleNoun,
                FaunaKind.Wisp => string.IsNullOrEmpty(_roleNoun) ? "Ice Wisp" : _roleNoun,
                FaunaKind.Tick => string.IsNullOrEmpty(_roleNoun) ? "Rock Tick" : _roleNoun,
                FaunaKind.Creeper => string.IsNullOrEmpty(_roleNoun) ? "Soil Creeper" : _roleNoun,
                FaunaKind.Hopper => string.IsNullOrEmpty(_roleNoun) ? "Ash Hopper" : _roleNoun,
                _ => "Dust Stalker"
            };
            Debug.Log($"[Threat] {who} defeated — pressure contribution removed.");
            Destroy(gameObject);
        }

        private void DespawnQuiet()
        {
            _threat?.Clear(_sourceId);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _threat?.Clear(_sourceId);
        }

        private void PickWanderTarget()
        {
            _wanderTimer = wanderRetargetSeconds * Random.Range(0.7f, 1.3f);
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            _wanderTarget = _home + new Vector3(r.x, 0f, r.y);
        }

        private void EnsureVisual()
        {
            _rend = GetComponentInChildren<Renderer>();
            if (_rend == null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(transform, false);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one;
                Object.Destroy(sphere.GetComponent<Collider>());
                _rend = sphere.GetComponent<Renderer>();
            }

            if (!IndustrialArtDressing.HasArt(gameObject))
                SetColor(_rend, stalkerColor);

            bool authored = IndustrialArtDressing.HasArt(gameObject);
            // Only flatten the anonymous single-sphere placeholder — never authored mesh visuals.
            bool placeholderSphere = !authored &&
                                     _rend != null &&
                                     _rend.transform.parent == transform &&
                                     _rend.GetComponent<MeshFilter>() != null &&
                                     _rend.name.Contains("Sphere");
            if (placeholderSphere && transform.localScale == Vector3.one)
                transform.localScale = new Vector3(1.1f, 0.55f, 1.3f);
            _baseScale = transform.localScale;

            if (_label == null)
            {
                var go = new GameObject("Label");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 1.6f;
                _label = go.AddComponent<TextMesh>();
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;
                _label.characterSize = 0.14f;
                _label.fontSize = 40;
                _label.fontStyle = FontStyle.Bold;
                _label.color = new Color(1f, 0.45f, 0.4f);
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Color LabelColor(FaunaKind kind)
        {
            switch (kind)
            {
                case FaunaKind.Mite: return new Color(1f, 0.72f, 0.35f);
                case FaunaKind.Leech: return new Color(0.45f, 1f, 1f);
                case FaunaKind.Wisp: return new Color(0.65f, 0.95f, 1f);
                case FaunaKind.Tick: return new Color(0.88f, 0.68f, 0.38f);
                case FaunaKind.Creeper: return new Color(0.62f, 0.82f, 0.32f);
                case FaunaKind.Hopper: return new Color(0.85f, 0.82f, 0.68f);
                default: return new Color(1f, 0.45f, 0.4f);
            }
        }

        private static void SetColor(Renderer rend, Color c)
        {
            if (rend == null) return;
            if (rend.material.HasProperty("_Color"))
                rend.material.color = c;
            else if (rend.material.HasProperty("_BaseColor"))
                rend.material.SetColor("_BaseColor", c);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, defeatLinkRadius);
        }
#endif
    }
}
