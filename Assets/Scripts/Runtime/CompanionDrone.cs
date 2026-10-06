using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Something fauna can be taunted onto: a robot or a companion.</summary>
    public interface ITauntTarget
    {
        Vector3 TauntPosition { get; }
        bool TauntAlive { get; }
        /// <summary>A taunted creature's bite, in robot hulls.</summary>
        void TakeTauntBite(float amount);
    }

    /// <summary>
    /// A robot's deployed companion (Majesty 2 summoned wolf / bear). Follows its robot, fights
    /// fauna within its leash, taunts if it is the heavy kind, and powers down when its lifetime
    /// ends, its hull is gone, or its robot is scrapped. Not saved: it is short-lived by design.
    /// </summary>
    public sealed class CompanionDrone : MonoBehaviour, ITauntTarget
    {
        private static readonly List<CompanionDrone> Live = new List<CompanionDrone>();

        private GameLoop _loop;
        private SpecialistAgent _owner;
        private CompanionDef _def;
        private float _hull;
        private float _life;
        private float _tauntTimer;
        private Renderer _rend;

        public string Id => _def.id;
        public SpecialistAgent Owner => _owner;

        public Vector3 TauntPosition => transform.position;
        public bool TauntAlive => this != null && _hull > 0f;

        public void TakeTauntBite(float amount)
        {
            if (amount <= 0f || _hull <= 0f) return;
            _hull -= amount;
            if (_hull <= 0f) PowerDown(true);
        }

        public static int CountFor(SpecialistAgent owner, string id)
        {
            int n = 0;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var c = Live[i];
                if (c == null) { Live.RemoveAt(i); continue; }
                if (c._owner == owner && c._def.id == id && c._hull > 0f) n++;
            }
            return n;
        }

        public static CompanionDrone Spawn(GameLoop loop, SpecialistAgent owner, CompanionDef def)
        {
            if (owner == null) return null;
            var go = GameObject.CreatePrimitive(def.taunts ? PrimitiveType.Cube : PrimitiveType.Capsule);
            go.name = "Companion_" + def.id;
            Object.Destroy(go.GetComponent<Collider>()); // never steals selection clicks
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, def.scale);
            Vector2 j = Random.insideUnitCircle.normalized * 1.6f;
            go.transform.position = owner.transform.position + new Vector3(j.x, 0.5f * def.scale, j.y);

            var c = go.AddComponent<CompanionDrone>();
            c._loop = loop;
            c._owner = owner;
            c._def = def;
            c._hull = Mathf.Max(0.1f, def.hull);
            c._life = Mathf.Max(1f, def.lifetime);
            c._rend = go.GetComponent<Renderer>();
            if (c._rend != null) c._rend.material.color = def.color;
            Live.Add(c);
            DemoVfx.ClaimRing(go.transform.position, def.color);
            return c;
        }

        private void OnDestroy() => Live.Remove(this);

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_owner == null || !_owner.gameObject.activeInHierarchy)
            {
                PowerDown(false);
                return;
            }

            _life -= dt;
            if (_life <= 0f)
            {
                PowerDown(false);
                return;
            }

            Vector3 home = _owner.transform.position;
            var target = NearestFauna(home, _def.leash);
            if (target != null)
            {
                Vector3 at = target.transform.position;
                if (Flat(transform.position, at) > 2f)
                    MoveToward(at, _def.moveSpeed * dt);
                else
                    target.ApplyCombatDamage(_def.dps * dt);

                if (_def.taunts)
                {
                    _tauntTimer -= dt;
                    if (_tauntTimer <= 0f)
                    {
                        _tauntTimer = Mathf.Max(1f, _def.tauntCooldown);
                        TauntAround();
                    }
                }
            }
            else if (Flat(transform.position, home) > 2.5f)
            {
                MoveToward(home, _def.moveSpeed * dt);
            }
        }

        private void TauntAround()
        {
            if (_loop == null) return;
            var all = _loop.Stalkers;
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || !s.IsAlive) continue;
                if (Flat(transform.position, s.transform.position) > _def.tauntRadius) continue;
                s.Taunt(this, _def.tauntDuration);
                n++;
            }
            if (n > 0)
            {
                DemoVfx.ClaimRing(transform.position, _def.color);
                _owner?.ShowRefusal("TAUNT");
            }
        }

        private DustStalkerAgent NearestFauna(Vector3 from, float range)
        {
            if (_loop == null) return null;
            DustStalkerAgent best = null;
            float bestD = range;
            var all = _loop.Stalkers;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || !s.IsAlive) continue;
                float d = Flat(from, s.transform.position);
                if (d > bestD) continue;
                bestD = d;
                best = s;
            }
            return best;
        }

        private void MoveToward(Vector3 dest, float step)
        {
            Vector3 p = transform.position;
            Vector3 flat = new Vector3(dest.x, p.y, dest.z);
            transform.position = Vector3.MoveTowards(p, flat, step);
            if (_owner != null)
            {
                // Ride at the robot's ground height (the map is not flat everywhere).
                var q = transform.position;
                q.y = _owner.transform.position.y + 0.5f * _def.scale;
                transform.position = q;
            }
        }

        private void PowerDown(bool destroyed)
        {
            if (destroyed) DemoVfx.DeathBurst(transform.position, _def.color);
            else DemoVfx.ClaimRing(transform.position, _def.color);
            _hull = 0f;
            Destroy(gameObject);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
