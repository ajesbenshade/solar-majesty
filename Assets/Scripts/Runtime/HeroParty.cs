using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Majesty-style party formed at the waystation inn. Members follow the leader.</summary>
    public sealed class HeroParty
    {
        public const int MaxSize = 4;

        public int Id { get; }
        public SpecialistAgent Leader { get; private set; }
        public readonly List<SpecialistAgent> Members = new List<SpecialistAgent>(MaxSize);

        public int Count => Members.Count;
        public bool IsAlive => Leader != null && Leader.IsAlive;

        public HeroParty(int id, SpecialistAgent leader)
        {
            Id = id;
            Leader = leader;
        }

        public bool Contains(SpecialistAgent a) => a != null && Members.Contains(a);

        public bool IsLeader(SpecialistAgent a) => a != null && a == Leader;

        public void PromoteIfNeeded()
        {
            if (Leader != null && Leader.IsAlive) return;
            Leader = null;
            for (int i = 0; i < Members.Count; i++)
            {
                var m = Members[i];
                if (m != null && m.IsAlive)
                {
                    Leader = m;
                    return;
                }
            }
        }

        public void Disband()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i] != null)
                    Members[i].SetParty(null);
            }
            Members.Clear();
            Leader = null;
        }
    }
}
