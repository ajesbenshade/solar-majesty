using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Classic AC research: labs tick science into one active tech.
    /// Expensive tip techs may also spend stockpile on complete.
    /// </summary>
    public sealed class ResearchManager
    {
        private readonly ResourceManager _resources;
        private readonly HashSet<TechId> _unlocked = new HashSet<TechId>();
        private readonly Dictionary<TechId, float> _progress = new Dictionary<TechId, float>();

        public TechId ActiveTech { get; private set; } = TechId.None;
        public float ActiveProgress { get; private set; }
        public float ActiveCost { get; private set; }
        public float CurrentRate { get; private set; }
        public int LabCount { get; private set; }
        public string LastEvent { get; private set; } = "idle";

        public event Action<TechId> TechUnlocked;

        private const string PrefsKey = "SM_ResearchUnlocks";

        public ResearchManager(ResourceManager resources)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Load();
        }

        public static void WipeUnlocks()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string raw = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            var parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int v) && Enum.IsDefined(typeof(TechId), v))
                {
                    var id = (TechId)v;
                    if (id != TechId.None)
                        _unlocked.Add(id);
                }
            }
        }

        private void Save()
        {
            if (_unlocked.Count == 0)
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
                return;
            }
            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var id in _unlocked)
            {
                if (!first) sb.Append(',');
                sb.Append((int)id);
                first = false;
            }
            PlayerPrefs.SetString(PrefsKey, sb.ToString());
            PlayerPrefs.Save();
        }

        public bool IsUnlocked(TechId id) => id != TechId.None && _unlocked.Contains(id);

        public bool PrerequisitesMet(TechDef def)
        {
            if (def == null) return false;
            if (def.Prerequisites == null || def.Prerequisites.Length == 0) return true;
            for (int i = 0; i < def.Prerequisites.Length; i++)
            {
                if (!_unlocked.Contains(def.Prerequisites[i]))
                    return false;
            }
            return true;
        }

        public bool CanSelect(TechId id)
        {
            if (IsUnlocked(id)) return false;
            var def = TechCatalog.Get(id);
            return def != null && PrerequisitesMet(def);
        }

        public bool TrySelect(TechId id)
        {
            if (!CanSelect(id)) return false;
            ActiveTech = id;
            var def = TechCatalog.Get(id);
            ActiveCost = def != null ? def.ScienceCost : 1f;
            ActiveProgress = _progress.TryGetValue(id, out float p) ? p : 0f;
            LastEvent = $"researching_{id}";
            return true;
        }

        public float Progress01(TechId id)
        {
            var def = TechCatalog.Get(id);
            if (def == null) return 0f;
            if (IsUnlocked(id)) return 1f;
            float p = _progress.TryGetValue(id, out float v) ? v : 0f;
            return Mathf.Clamp01(p / Mathf.Max(1f, def.ScienceCost));
        }

        public TechId RecommendedNext()
        {
            var techs = TechCatalog.All;
            for (int i = 0; i < techs.Count; i++)
            {
                if (CanSelect(techs[i].Id))
                    return techs[i].Id;
            }
            return TechId.None;
        }

        public string LaunchTechLabel(CelestialBodyId body) =>
            body == CelestialBodyId.Earth ? "Lunar Rocket" : "Mars Ship";

        public void Tick(float dt, int labCount, int labWorkers, float rateMultiplier = 1f)
        {
            if (dt <= 0f) return;
            LabCount = Mathf.Max(0, labCount);

            float rate = 0.45f + LabCount * 0.85f + Mathf.Max(0, labWorkers) * 0.25f;
            var techs = TechCatalog.All;
            for (int i = 0; i < techs.Count; i++)
            {
                if (IsUnlocked(techs[i].Id) && techs[i].ResearchRateBonus > 0f)
                    rate += techs[i].ResearchRateBonus;
            }
            rate *= Mathf.Max(0.25f, rateMultiplier);
            CurrentRate = rate;

            if (ActiveTech == TechId.None) return;
            var def = TechCatalog.Get(ActiveTech);
            if (def == null || IsUnlocked(ActiveTech))
            {
                ActiveTech = TechId.None;
                return;
            }

            ActiveProgress += rate * dt;
            _progress[ActiveTech] = ActiveProgress;
            ActiveCost = def.ScienceCost;

            if (ActiveProgress < def.ScienceCost) return;

            if (def.CompleteCost != null && def.CompleteCost.Length > 0)
            {
                if (!_resources.CanAfford(def.CompleteCost))
                {
                    ActiveProgress = def.ScienceCost - 0.01f;
                    _progress[ActiveTech] = ActiveProgress;
                    LastEvent = "awaiting_stockpile";
                    return;
                }
                if (!_resources.TrySpend(def.CompleteCost))
                {
                    LastEvent = "spend_failed";
                    return;
                }
            }

            CompleteActive(def);
        }

        private void CompleteActive(TechDef def)
        {
            _unlocked.Add(def.Id);
            _progress.Remove(def.Id);
            ActiveTech = TechId.None;
            ActiveProgress = 0f;
            LastEvent = $"unlocked_{def.Id}";
            Save();
            TechUnlocked?.Invoke(def.Id);
            Debug.Log($"[Research] Unlocked {def.DisplayName}");

            // Keep the queue moving toward launch tech without forcing tip picks.
            var next = RecommendedNext();
            if (next != TechId.None)
                TrySelect(next);
        }

        public bool HasLaunchUnlockFor(CelestialBodyId body)
        {
            switch (body)
            {
                case CelestialBodyId.Earth:
                    return IsUnlocked(TechId.LunarRocket);
                case CelestialBodyId.Luna:
                    return IsUnlocked(TechId.MarsShip);
                default:
                    return IsUnlocked(TechId.MarsShip);
            }
        }
    }
}
