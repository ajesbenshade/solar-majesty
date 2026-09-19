using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Visible chassis waiting at the Fobot Yard. Inspect the yard to pay.</summary>
    public sealed class FobotWreck : MonoBehaviour
    {
        public SpecialistClass Class { get; private set; }
        public int Level { get; private set; }

        public void Bind(SpecialistRecord rec)
        {
            Class = rec.Class;
            Level = Mathf.Max(1, rec.Level);
            gameObject.name = $"Wreck_{ColonyStructure.ClassLabel(Class)}_L{Level}";
        }

        public static GameObject Spawn(SpecialistRecord rec, Vector3 at, Transform parent)
        {
            var go = new GameObject("Wreck");
            go.transform.SetParent(parent, false);
            go.transform.position = at + Vector3.up * 0.05f;
            var wreck = go.AddComponent<FobotWreck>();
            wreck.Bind(rec);
            BuildMesh(go.transform);
            return go;
        }

        private static void BuildMesh(Transform root)
        {
            HeroBuildingKits.BuildWreck(root);
        }
    }
}
