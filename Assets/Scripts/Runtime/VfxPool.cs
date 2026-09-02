using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Reusable primitives for the VFX layer.
    ///
    /// Every effect used to create a GameObject and destroy it: a single death burst allocated nine
    /// of them, and a busy raid could churn hundreds per second. That is the largest avoidable
    /// source of GC pressure in the game and the first thing that would break a 200-agent target.
    /// Objects are now rented, reset, and returned.
    /// </summary>
    public static class VfxPool
    {
        private const int WarmPerType = 24;
        private const int HardCap = 400;

        private static readonly Dictionary<PrimitiveType, Stack<GameObject>> Free =
            new Dictionary<PrimitiveType, Stack<GameObject>>(4);

        private static readonly Dictionary<GameObject, PrimitiveType> Live =
            new Dictionary<GameObject, PrimitiveType>(64);

        private static Transform _root;
        private static int _created;

        private static Transform Root
        {
            get
            {
                if (_root != null) return _root;
                var go = GameObject.Find("SM_VfxPool");
                if (go == null)
                {
                    go = new GameObject("SM_VfxPool");
                    Object.DontDestroyOnLoad(go);
                }
                _root = go.transform;
                return _root;
            }
        }

        /// <summary>Rent a primitive. Returns null past the hard cap rather than spiralling.</summary>
        public static GameObject Rent(PrimitiveType type)
        {
            if (!Free.TryGetValue(type, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(WarmPerType);
                Free[type] = stack;
            }

            GameObject go = null;
            while (stack.Count > 0 && go == null)
                go = stack.Pop();

            if (go == null)
            {
                if (_created >= HardCap) return null;
                go = Create(type);
                _created++;
            }

            go.transform.SetParent(Root, false);
            go.transform.localScale = Vector3.one;
            go.SetActive(true);
            Live[go] = type;
            return go;
        }

        /// <summary>Return an object to the pool. Safe to call on something not from the pool.</summary>
        public static void Release(GameObject go)
        {
            if (go == null) return;

            if (!Live.TryGetValue(go, out PrimitiveType type))
            {
                Object.Destroy(go);
                return;
            }

            Live.Remove(go);
            go.SetActive(false);
            go.transform.SetParent(Root, false);

            if (!Free.TryGetValue(type, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(WarmPerType);
                Free[type] = stack;
            }
            stack.Push(go);
        }

        private static GameObject Create(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            go.transform.SetParent(Root, false);
            return go;
        }

        /// <summary>Live object count, for the debug overlay.</summary>
        public static int ActiveCount => Live.Count;

        public static int PooledCount
        {
            get
            {
                int n = 0;
                foreach (var kv in Free) n += kv.Value.Count;
                return n;
            }
        }
    }
}
