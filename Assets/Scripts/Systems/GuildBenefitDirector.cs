using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Player-activated guild charters. Research unlocks them; Metals pay the pulse.
    /// Does not rewrite SpecialistBrain.
    /// </summary>
    public sealed class GuildBenefitDirector
    {
        private readonly float[] _remain = new float[4];
        private readonly float[] _cool = new float[4];

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < 4; i++)
            {
                if (_remain[i] > 0f)
                {
                    _remain[i] -= dt;
                    if (_remain[i] <= 0f)
                    {
                        _remain[i] = 0f;
                        _cool[i] = RobotGuildCatalog.Get((RobotGuildId)i).CooldownSeconds;
                    }
                }
                else if (_cool[i] > 0f)
                    _cool[i] = Mathf.Max(0f, _cool[i] - dt);
            }
        }

        public bool IsActive(RobotGuildId id) => Slot(id, _remain) > 0f;

        public float Remaining(RobotGuildId id) => Slot(id, _remain);

        public float CooldownLeft(RobotGuildId id) => Slot(id, _cool);

        public bool CanActivate(
            RobotGuildId id,
            ResearchManager research,
            ResourceManager resources,
            bool hallStanding)
        {
            var g = RobotGuildCatalog.Get(id);
            if (g == null || !hallStanding) return false;
            if (research == null || !research.IsUnlocked(g.BenefitTech)) return false;
            if (Slot(id, _remain) > 0f || Slot(id, _cool) > 0f) return false;
            return resources != null && resources.Get(ResourceId.Metals) >= g.ActivateCost;
        }

        public bool TryActivate(
            RobotGuildId id,
            ResearchManager research,
            ResourceManager resources,
            bool hallStanding,
            out string line)
        {
            line = null;
            var g = RobotGuildCatalog.Get(id);
            if (g == null)
            {
                line = "No such lodge.";
                return false;
            }

            if (!hallStanding)
            {
                line = $"Dock {g.HallName} first.";
                return false;
            }

            if (research == null || !research.IsUnlocked(g.BenefitTech))
            {
                line = $"Research {g.BenefitName} at the lab.";
                return false;
            }

            if (Slot(id, _remain) > 0f)
            {
                line = $"{g.BenefitName} is already running.";
                return false;
            }

            if (Slot(id, _cool) > 0f)
            {
                line = $"{g.BenefitName} cooling down ({Slot(id, _cool):F0}s).";
                return false;
            }

            if (resources == null || !resources.TrySpend(ResourceId.Metals, g.ActivateCost))
            {
                line = $"{g.BenefitName} needs {g.ActivateCost} CRED.";
                return false;
            }

            SetSlot(id, _remain, g.DurationSeconds);
            SetSlot(id, _cool, 0f);
            line = $"{g.ShortName}: {g.BenefitName} — {g.ActivateCost} CRED.";
            return true;
        }

        private static float Slot(RobotGuildId id, float[] arr)
        {
            int i = (int)id;
            if (i < 0 || i >= arr.Length) return 0f;
            return arr[i];
        }

        private static void SetSlot(RobotGuildId id, float[] arr, float value)
        {
            int i = (int)id;
            if (i < 0 || i >= arr.Length) return;
            arr[i] = value;
        }
    }
}
