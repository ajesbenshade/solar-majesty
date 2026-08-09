// Runtime bounty flag list. Player posts; SpecialistBrain only reads.
// Pure C#: no MonoBehaviour.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Thin registry of open FlagHandles for one outpost simulation.
    /// Never assigns specialists — they evaluate and soft-claim themselves.
    /// </summary>
    public sealed class FlagManager
    {
        private readonly List<FlagHandle> _flags = new List<FlagHandle>();
        private readonly Dictionary<object, float> _workRemaining = new Dictionary<object, float>();
        private int _nextId = 1;

        public IReadOnlyList<FlagHandle> Flags => _flags;

        /// <summary>
        /// Player posts a bounty flag. Bounty is clamped to FlagData min/max.
        /// </summary>
        public FlagHandle Post(FlagData data, Vector3 worldPosition, float bounty)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            float clamped = Mathf.Clamp(bounty, data.minBounty, data.maxBounty);
            int id = _nextId++;

            var flag = new FlagHandle
            {
                Data = data,
                WorldPosition = worldPosition,
                CurrentBounty = clamped,
                Risk = data.baseRisk,
                ClaimCount = 0,
                RuntimeId = id
            };

            _flags.Add(flag);
            _workRemaining[id] = data.workRequired;
            return flag;
        }

        public FlagHandle PostDefault(FlagData data, Vector3 worldPosition)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return Post(data, worldPosition, data.defaultBounty);
        }

        public bool TryGet(object runtimeId, out FlagHandle flag)
        {
            for (int i = 0; i < _flags.Count; i++)
            {
                if (Equals(_flags[i].RuntimeId, runtimeId))
                {
                    flag = _flags[i];
                    return true;
                }
            }

            flag = null;
            return false;
        }

        /// <summary>
        /// Apply autonomous work toward completing a flag.
        /// Returns true if the flag finished and was removed.
        /// </summary>
        public bool ApplyWork(FlagHandle flag, float amount)
        {
            if (flag == null || flag.RuntimeId == null || amount <= 0f)
                return false;

            if (!_workRemaining.TryGetValue(flag.RuntimeId, out float remaining))
                return false;

            remaining = Mathf.Max(0f, remaining - amount);
            if (remaining > 0f)
            {
                _workRemaining[flag.RuntimeId] = remaining;
                return false;
            }

            _workRemaining.Remove(flag.RuntimeId);
            _flags.Remove(flag);
            return true;
        }

        public float GetWorkRemaining(FlagHandle flag)
        {
            if (flag?.RuntimeId == null) return 0f;
            return _workRemaining.TryGetValue(flag.RuntimeId, out float w) ? w : 0f;
        }

        public void Cancel(FlagHandle flag)
        {
            if (flag == null) return;
            if (flag.RuntimeId != null)
                _workRemaining.Remove(flag.RuntimeId);
            _flags.Remove(flag);
        }

        /// <summary>Soft claim — raises ClaimCount for crowd penalty in the brain.</summary>
        public void AddClaim(FlagHandle flag)
        {
            if (flag == null) return;
            flag.ClaimCount = Mathf.Max(0, flag.ClaimCount) + 1;
        }

        public void RemoveClaim(FlagHandle flag)
        {
            if (flag == null) return;
            flag.ClaimCount = Mathf.Max(0, flag.ClaimCount - 1);
        }

        public void SetBounty(FlagHandle flag, float bounty)
        {
            if (flag == null || flag.Data == null) return;
            flag.CurrentBounty = Mathf.Clamp(bounty, flag.Data.minBounty, flag.Data.maxBounty);
        }

        public void ClearAll()
        {
            _flags.Clear();
            _workRemaining.Clear();
        }
    }
}
