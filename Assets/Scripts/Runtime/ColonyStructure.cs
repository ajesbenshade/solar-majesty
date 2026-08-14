using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    public enum StructureRole
    {
        Core = 0,
        VillageHab = 1,
        Inn = 2,
        Camp = 3,
        Workshop = 4
    }

    /// <summary>
    /// Selectable colony piece. Village HABs take raids first.
    /// Workshops (not guilds) are per-class hangouts — flags nearby pull matching specialists.
    /// Progression is via research / campaign gates, not per-building upgrade levels.
    /// </summary>
    public class ColonyStructure : MonoBehaviour
    {
        [SerializeField] private StructureRole role = StructureRole.Core;
        [SerializeField] private float maxHealth = 48f;

        private float _health;
        private VillageExpansion _village;
        private GameObject _selectRing;
        private bool _selected;
        private readonly List<SpecialistAgent> _workers = new List<SpecialistAgent>(4);

        public StructureRole Role => role;
        public BuildingCategory Category { get; private set; } = BuildingCategory.Utility;
        public BuildingData SourceData { get; private set; }
        public string DisplayName { get; private set; } = "Module";
        public SpecialistClass PreferredClass { get; private set; } = SpecialistClass.EngineerBot;
        public bool ClassLocked { get; private set; }
        public bool HasPreferredClass { get; private set; }

        public bool IsVillageHab => role == StructureRole.VillageHab;
        public bool IsWorkshop => role == StructureRole.Workshop;
        public bool IsResidential =>
            role == StructureRole.VillageHab || Category == BuildingCategory.Habitat;
        public int ResidentCapacity => IsResidential ? Settlement.HousingPerHab : 0;
        public int Residents { get; private set; }
        public bool HasVacancy => IsResidential && IsAlive && Residents < ResidentCapacity;
        public bool IsAlive => _health > 0f;
        public float Health01 => maxHealth > 0f ? Mathf.Clamp01(_health / maxHealth) : 0f;
        public Vector3 WorldPosition => transform.position;
        public bool IsSelected => _selected;
        public int WorkerSlots => IsWorkshop ? 2 : 1;
        public IReadOnlyList<SpecialistAgent> Workers => _workers;
        public int WorkerCount
        {
            get
            {
                PruneWorkers();
                return _workers.Count;
            }
        }

        public void Configure(
            StructureRole structureRole,
            VillageExpansion village,
            float hp = 48f,
            BuildingCategory category = BuildingCategory.Utility,
            BuildingData data = null,
            string displayName = null)
        {
            role = structureRole;
            _village = village;
            Category = data != null ? data.category : category;
            SourceData = data;
            DisplayName = !string.IsNullOrEmpty(displayName)
                ? displayName
                : (data != null ? data.displayName : DefaultName(Category, role));
            maxHealth = hp;
            _health = hp;
            ApplyDefaultClass();
            if (Category == BuildingCategory.Habitat)
            {
                HasPreferredClass = false;
                ClassLocked = false;
            }
            EnsureSelectProxy();
            EnsureSelectRing();
            SetSelected(false);
        }

        public void SetResidents(int count)
        {
            Residents = Mathf.Clamp(count, 0, ResidentCapacity);
            RefreshResidentPips();
        }

        public bool TryAddResident()
        {
            if (!HasVacancy) return false;
            Residents++;
            RefreshResidentPips();
            return true;
        }

        private void RefreshResidentPips()
        {
            Transform existing = transform.Find("ResidentPips");
            if (existing != null)
                Destroy(existing.gameObject);
            if (!IsResidential || Residents <= 0) return;

            var root = new GameObject("ResidentPips").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 2.1f, 0f);
            for (int i = 0; i < Residents; i++)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.name = $"Pip_{i}";
                pip.transform.SetParent(root, false);
                pip.transform.localPosition = new Vector3((i - (Residents - 1) * 0.5f) * 0.35f, 0f, 0f);
                pip.transform.localScale = Vector3.one * 0.18f;
                Object.Destroy(pip.GetComponent<Collider>());
                var rend = pip.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                           ?? Shader.Find("Sprites/Default"));
                    var c = new Color(0.96f, 0.42f, 0.08f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    else if (mat.HasProperty("_Color")) mat.color = c;
                    rend.sharedMaterial = mat;
                }
            }
        }

        public void SetPreferredClass(SpecialistClass cls)
        {
            if (ClassLocked) return;
            PreferredClass = cls;
            HasPreferredClass = true;
        }

        public bool HasOpenSlot()
        {
            PruneWorkers();
            return _workers.Count < WorkerSlots;
        }

        public bool TryClockIn(SpecialistAgent agent)
        {
            if (agent == null || !IsAlive) return false;
            PruneWorkers();
            if (_workers.Contains(agent)) return true;
            if (_workers.Count >= WorkerSlots) return false;
            if (HasPreferredClass && agent.Data != null && agent.Data.specialistClass != PreferredClass)
                return false;
            _workers.Add(agent);
            return true;
        }

        public void ClockOut(SpecialistAgent agent)
        {
            if (agent == null) return;
            _workers.Remove(agent);
        }

        public void ApplyRaidDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _health -= amount;
            RefreshDamageVisual();
            if (_health <= 0f)
                Collapse();
        }

        /// <summary>Engineer patch. Returns HP actually restored.</summary>
        public float Repair(float amount)
        {
            if (!IsAlive || amount <= 0f || !NeedsRepair) return 0f;
            float before = _health;
            _health = Mathf.Min(maxHealth, _health + amount);
            RefreshDamageVisual();
            return Mathf.Max(0f, _health - before);
        }

        public bool NeedsRepair => IsAlive && Health01 < 0.985f;

        private Vector3 _baseScale = Vector3.one;
        private bool _capturedBaseScale;

        private void CaptureBaseScale()
        {
            if (_capturedBaseScale) return;
            _baseScale = transform.localScale;
            if (_baseScale.sqrMagnitude < 0.0001f)
                _baseScale = Vector3.one;
            _capturedBaseScale = true;
        }

        private void RefreshDamageVisual()
        {
            CaptureBaseScale();
            // Footprint-fitted kits must stay at authored scale; damage only nicks the silhouette.
            float n = 0.94f + Health01 * 0.06f;
            transform.localScale = _baseScale * n;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_selectRing != null)
                _selectRing.SetActive(selected);
        }

        public static FlagType AttractFlagFor(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.EngineerBot: return FlagType.Build;
                case SpecialistClass.DefenseMech: return FlagType.DefendArea;
                case SpecialistClass.Medic: return FlagType.DefendArea;
                default: return FlagType.Explore;
            }
        }

        public static string ClassLabel(SpecialistClass cls)
        {
            switch (cls)
            {
                case SpecialistClass.EngineerBot: return "ENG";
                case SpecialistClass.DefenseMech: return "DEF";
                case SpecialistClass.Medic: return "MED";
                default: return "SCOUT";
            }
        }

        public static bool IsWorkshopCategory(BuildingCategory cat) =>
            cat == BuildingCategory.ScoutWorkshop ||
            cat == BuildingCategory.EngineerWorkshop ||
            cat == BuildingCategory.DefenseWorkshop ||
            cat == BuildingCategory.MedicWorkshop;

        /// <summary>True after this workshop has fabricated its outdoor robot.</summary>
        public bool RobotFabricated { get; private set; }

        public void MarkRobotFabricated() => RobotFabricated = true;

        public static SpecialistClass? RobotClassForWorkshop(BuildingCategory cat)
        {
            switch (cat)
            {
                case BuildingCategory.ScoutWorkshop: return SpecialistClass.ScoutDrone;
                case BuildingCategory.EngineerWorkshop: return SpecialistClass.EngineerBot;
                case BuildingCategory.DefenseWorkshop: return SpecialistClass.DefenseMech;
                case BuildingCategory.MedicWorkshop: return SpecialistClass.Medic;
                default: return null;
            }
        }

        private void ApplyDefaultClass()
        {
            if (SourceData != null && SourceData.preferredOccupants != null &&
                SourceData.preferredOccupants.Length > 0)
            {
                PreferredClass = SourceData.preferredOccupants[0];
                HasPreferredClass = true;
                ClassLocked = IsWorkshop;
                return;
            }

            switch (Category)
            {
                case BuildingCategory.ScoutWorkshop:
                    PreferredClass = SpecialistClass.ScoutDrone;
                    HasPreferredClass = true;
                    ClassLocked = true;
                    role = StructureRole.Workshop;
                    break;
                case BuildingCategory.EngineerWorkshop:
                    PreferredClass = SpecialistClass.EngineerBot;
                    HasPreferredClass = true;
                    ClassLocked = true;
                    role = StructureRole.Workshop;
                    break;
                case BuildingCategory.DefenseWorkshop:
                    PreferredClass = SpecialistClass.DefenseMech;
                    HasPreferredClass = true;
                    ClassLocked = true;
                    role = StructureRole.Workshop;
                    break;
                case BuildingCategory.MedicWorkshop:
                    PreferredClass = SpecialistClass.Medic;
                    HasPreferredClass = true;
                    ClassLocked = true;
                    role = StructureRole.Workshop;
                    break;
                case BuildingCategory.Farm:
                case BuildingCategory.Mine:
                case BuildingCategory.RegolithCamp:
                case BuildingCategory.Mining:
                    PreferredClass = SpecialistClass.EngineerBot;
                    HasPreferredClass = true;
                    break;
                case BuildingCategory.Defense:
                    PreferredClass = SpecialistClass.DefenseMech;
                    HasPreferredClass = true;
                    break;
                case BuildingCategory.Laboratory:
                case BuildingCategory.LandingPad:
                    PreferredClass = SpecialistClass.ScoutDrone;
                    HasPreferredClass = true;
                    break;
                case BuildingCategory.Habitat:
                    // Humans live indoors — no outdoor robot duty class.
                    HasPreferredClass = false;
                    break;
                default:
                    HasPreferredClass = false;
                    break;
            }
        }

        private static string DefaultName(BuildingCategory cat, StructureRole role)
        {
            if (role == StructureRole.VillageHab) return "Village HAB";
            if (role == StructureRole.Inn) return "Waystation Inn";
            switch (cat)
            {
                case BuildingCategory.Palace: return "Palace Keep";
                case BuildingCategory.Habitat: return "Habitat";
                case BuildingCategory.Farm: return "Greenhouse Farm";
                case BuildingCategory.Mine: return "Ore Mine";
                case BuildingCategory.RegolithCamp: return "Regolith Camp";
                case BuildingCategory.ScoutWorkshop: return "Scout Workshop";
                case BuildingCategory.EngineerWorkshop: return "Engineer Workshop";
                case BuildingCategory.DefenseWorkshop: return "Defense Workshop";
                case BuildingCategory.MedicWorkshop: return "Medic Workshop";
                case BuildingCategory.Power: return "Power Node";
                case BuildingCategory.Laboratory: return "Laboratory";
                case BuildingCategory.Defense: return "Command";
                case BuildingCategory.Mining: return "Ops";
                case BuildingCategory.LandingPad: return "Landing Pad";
                case BuildingCategory.Utility: return "Airlock";
                default: return "Module";
            }
        }

        private void EnsureSelectProxy()
        {
            Transform existing = transform.Find("SelectProxy");
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return;
            }

            var proxy = new GameObject("SelectProxy");
            proxy.transform.SetParent(transform, false);
            proxy.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            proxy.layer = gameObject.layer;
            var box = proxy.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(2.6f, 2.2f, 2.6f);
        }

        private void EnsureSelectRing()
        {
            if (_selectRing != null) return;
            _selectRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _selectRing.name = "SelectRing";
            _selectRing.transform.SetParent(transform, false);
            _selectRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            _selectRing.transform.localScale = new Vector3(3.2f, 0.025f, 3.2f);
            Object.Destroy(_selectRing.GetComponent<Collider>());
            var rend = _selectRing.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                       ?? Shader.Find("Sprites/Default"));
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.96f, 0.42f, 0.08f, 0.85f));
                rend.sharedMaterial = mat;
            }
            _selectRing.SetActive(false);
        }

        private void PruneWorkers()
        {
            for (int i = _workers.Count - 1; i >= 0; i--)
            {
                var w = _workers[i];
                if (w == null || !w.IsAlive)
                    _workers.RemoveAt(i);
            }
        }

        private void Collapse()
        {
            for (int i = 0; i < _workers.Count; i++)
                _workers[i]?.SetWorkplace(null);
            _workers.Clear();
            _village?.NotifyCollapsed(this);
            DemoVfx.DeathBurst(transform.position, new Color(0.95f, 0.42f, 0.08f));
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_village != null)
                _village.OnStructureDestroyed(this);
        }
    }
}
