using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Authored stalker den (Resources/Dens/StalkerDen) and the primitive fallback.
    /// </summary>
    public class StalkerLairDenTests
    {
        private static readonly Color DefaultSoil = new Color(0.40f, 0.31f, 0.22f);
        private static readonly Color DefaultStone = new Color(0.52f, 0.48f, 0.43f);

        private readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp() => StalkerLair.ResetDenPrefabLoaderForTests();

        [TearDown]
        public void TearDown()
        {
            StalkerLair.ResetDenPrefabLoaderForTests();
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
        }

        [Test]
        public void PrefabPath_InstantiatesAndShowsActive()
        {
            Assert.IsNotNull(Resources.Load<GameObject>(StalkerLair.DenResourcePath), StalkerLair.DenResourcePath);
            Assert.AreEqual(1f, StalkerLair.AuthoredDenScale, 0.0001f);

            var lair = MakeLair();
            lair.Configure(null, 2, 8f);

            Transform body = lair.transform.Find("DenBody");
            Assert.IsNotNull(body);
            Assert.Less(Quaternion.Angle(body.localRotation, Quaternion.Euler(0f, 45f, 0f)), 0.5f);

            Transform den = FindNamed(body, "StalkerDen");
            Assert.IsNotNull(den);
            Assert.AreEqual(body, den.parent);
            Assert.AreEqual(StalkerLair.AuthoredDenScale, den.localScale.x, 0.0001f);
            Assert.AreEqual(den.localScale.x, den.localScale.y, 0.0001f);
            Assert.AreEqual(den.localScale.x, den.localScale.z, 0.0001f);
            Assert.Less(Quaternion.Angle(den.localRotation, Quaternion.identity), 0.5f);
            Assert.AreEqual(0, den.GetComponentsInChildren<Collider>(true).Length);
            Assert.IsNull(FindNamed(body, "Clearing"));

            Transform active = FindNamed(den, "Den_Active");
            Transform ruined = FindNamed(den, "Den_Ruined");
            Assert.IsNotNull(active);
            Assert.IsNotNull(ruined);
            Assert.IsTrue(active.gameObject.activeSelf);
            Assert.IsFalse(ruined.gameObject.activeSelf);

            AssertTint(den, "Den_Soil", DefaultSoil);
            AssertTint(den, "Den_Stone", DefaultStone);
            AssertTint(den, "Den_Hive", Color.white);
            AssertTint(den, "Den_Bones", Color.white);
            AssertTint(den, "Den_Glow", Color.white);
            AssertMaterialsUntouched(den);

            Renderer glow = RendererNamed(den, "Den_Glow");
            Assert.IsNotNull(glow);
            Color assetEmission = glow.sharedMaterial.GetColor("_EmissionColor");
            AssertWhiteEmission(assetEmission, "glow asset");
            AssertColor(BlockColor(glow, "_EmissionColor"), assetEmission, "live glow emission");

            lair.MarkScouted();
            Assert.IsTrue(active.gameObject.activeInHierarchy);
            Assert.IsFalse(ruined.gameObject.activeSelf);
            Bounds bounds = ActiveBounds(active);
            float height = bounds.size.y;
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            string size = "height " + height.ToString("0.00")
                + " footprint " + footprint.ToString("0.00")
                + " (x " + bounds.size.x.ToString("0.00")
                + " z " + bounds.size.z.ToString("0.00") + ")";
            Debug.Log("[Lair] Authored den size " + size);
            Assert.Greater(height, 4f, "den " + size);
            Assert.Less(height, 16f, "den " + size);
            Assert.Greater(footprint, 8f, "den " + size);
            Assert.Less(footprint, 24f, "den " + size);
        }

        [Test]
        public void ForceClear_SwapsToRuined_ZeroesEmission_WithoutSwappingMaterials()
        {
            var soil = new Color(0.21f, 0.44f, 0.13f, 1f);
            var stone = new Color(0.63f, 0.58f, 0.51f, 1f);
            var lair = MakeLair();
            lair.Configure(null, 2, 8f, null, null, soil, stone);

            Transform den = FindNamed(lair.transform, "StalkerDen");
            Assert.IsNotNull(den);
            AssertTint(den, "Den_Soil", soil);
            AssertTint(den, "Den_Stone", stone);

            Renderer[] renderers = den.GetComponentsInChildren<Renderer>(true);
            var materials = new Material[renderers.Length];
            var assetEmission = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].sharedMaterial;
                Assert.IsNotNull(materials[i], renderers[i].name);
                assetEmission[i] = materials[i].HasProperty("_EmissionColor")
                    ? materials[i].GetColor("_EmissionColor")
                    : Color.black;
            }

            int hits = 0;
            Application.LogCallback onLog = (cond, stack, type) =>
            {
                if (cond != null && cond.IndexOf("[Lair] Cleared", System.StringComparison.Ordinal) >= 0)
                    hits++;
            };
            Application.logMessageReceived += onLog;
            try
            {
                lair.ForceClear();
            }
            finally
            {
                Application.logMessageReceived -= onLog;
            }

            Assert.IsTrue(lair.IsCleared);
            Assert.AreEqual(1, hits);
            Assert.IsFalse(FindNamed(den, "Den_Active").gameObject.activeSelf);
            Assert.IsTrue(FindNamed(den, "Den_Ruined").gameObject.activeInHierarchy);

            for (int i = 0; i < renderers.Length; i++)
            {
                Assert.AreSame(materials[i], renderers[i].sharedMaterial, renderers[i].name);
                if (materials[i].HasProperty("_EmissionColor"))
                    AssertColor(materials[i].GetColor("_EmissionColor"), assetEmission[i], renderers[i].name + " asset emission");
                if (materials[i].HasProperty("_BaseColor"))
                    AssertColor(materials[i].GetColor("_BaseColor"), Color.white, renderers[i].name + " asset base");
                Assert.LessOrEqual(
                    BlockColor(renderers[i], "_EmissionColor").maxColorComponent,
                    0.001f,
                    renderers[i].name + " emission");
            }

            AssertTint(den, "Den_Soil", Dim(soil));
            AssertTint(den, "Den_Stone", Dim(stone));
            AssertTint(den, "Den_Hive", Dim(Color.white));
            AssertTint(den, "Den_Glow", Dim(Color.white));
        }

        [Test]
        public void RestoreChart_Cleared_ShowsRuinedWithoutLog()
        {
            var lair = MakeLair();
            lair.Configure(null, 2, 8f);
            Transform den = FindNamed(lair.transform, "StalkerDen");
            Assert.IsNotNull(den);

            int hits = 0;
            Application.LogCallback onLog = (cond, stack, type) =>
            {
                if (cond != null && cond.IndexOf("[Lair] Cleared", System.StringComparison.Ordinal) >= 0)
                    hits++;
            };
            Application.logMessageReceived += onLog;
            try
            {
                lair.RestoreChart(true, false);
            }
            finally
            {
                Application.logMessageReceived -= onLog;
            }

            Assert.AreEqual(0, hits);
            Assert.IsTrue(lair.IsCleared);
            Assert.AreEqual("StalkerLair_Cleared", lair.gameObject.name);
            Assert.IsFalse(FindNamed(den, "Den_Active").gameObject.activeSelf);
            Assert.IsTrue(FindNamed(den, "Den_Ruined").gameObject.activeInHierarchy);
            Assert.IsTrue(lair.transform.Find("DenBody").gameObject.activeSelf);
            Renderer glow = RendererNamed(den, "Den_Glow");
            Assert.LessOrEqual(BlockColor(glow, "_EmissionColor").maxColorComponent, 0.001f, "restored glow emission");
            var beacon = lair.transform.Find("SurveyBeacon");
            if (beacon != null)
                Assert.IsFalse(beacon.gameObject.activeSelf);
        }

        [Test]
        public void MissingPrefab_FallsBackToPrimitives()
        {
            StalkerLair.SetDenPrefabLoaderForTests(() => null);
            var lair = MakeLair();
            lair.Configure(null, 2, 8f);

            Transform body = lair.transform.Find("DenBody");
            Assert.IsNotNull(body);
            Assert.Less(Quaternion.Angle(body.localRotation, Quaternion.Euler(0f, 45f, 0f)), 0.5f);
            Assert.IsNull(FindNamed(lair.transform, "StalkerDen"));
            Assert.IsNull(FindNamed(lair.transform, "Den_Active"));
            Assert.IsNull(FindNamed(lair.transform, "Den_Ruined"));
            Transform clearing = FindNamed(body, "Clearing");
            Assert.IsNotNull(clearing);
            Assert.IsNotNull(FindNamed(body, "Hive_0"));
            Assert.AreEqual("SM_Den", clearing.GetComponent<Renderer>().sharedMaterial.name);
        }

        [Test]
        public void FoggedThenScouted_HidesAndShowsPrefab()
        {
            var lair = MakeLair();
            lair.Configure(null, 2, 8f);

            Transform body = lair.transform.Find("DenBody");
            Transform den = FindNamed(body, "StalkerDen");
            Transform hint = lair.transform.Find("FogHint");
            Assert.IsNotNull(den);
            Assert.IsNotNull(hint);
            Assert.IsFalse(lair.IsScouted);
            Assert.IsFalse(body.gameObject.activeSelf);
            Assert.IsFalse(den.gameObject.activeInHierarchy);
            Assert.IsTrue(hint.gameObject.activeSelf);
            Assert.IsTrue(FindNamed(den, "Den_Active").gameObject.activeSelf);
            Assert.IsFalse(FindNamed(den, "Den_Ruined").gameObject.activeSelf);
            Assert.IsNull(lair.transform.Find("SurveyBeacon"));

            lair.MarkScouted();

            Assert.IsTrue(lair.IsScouted);
            Assert.IsTrue(body.gameObject.activeSelf);
            Assert.IsTrue(den.gameObject.activeInHierarchy);
            Assert.IsFalse(hint.gameObject.activeSelf);
            Assert.IsTrue(FindNamed(den, "Den_Active").gameObject.activeInHierarchy);
            Assert.IsFalse(FindNamed(den, "Den_Ruined").gameObject.activeSelf);
            Transform beacon = lair.transform.Find("SurveyBeacon");
            Assert.IsNotNull(beacon);
            Assert.IsTrue(beacon.gameObject.activeSelf);
        }

        private StalkerLair MakeLair()
        {
            var go = new GameObject("lair-test");
            _created.Add(go);
            return go.AddComponent<StalkerLair>();
        }

        private static void AssertMaterialsUntouched(Transform den)
        {
            Renderer[] renderers = den.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i].sharedMaterial;
                Assert.IsNotNull(mat, renderers[i].name);
                Assert.IsTrue(mat.name.StartsWith("SM_StalkerDen"), renderers[i].name + " material " + mat.name);
                Assert.AreNotEqual("SM_Den", mat.name);
                Assert.AreNotEqual("SM_DenGlow", mat.name);
                if (mat.HasProperty("_BaseColor"))
                    AssertColor(mat.GetColor("_BaseColor"), Color.white, renderers[i].name + " asset");
            }
        }

        private static void AssertTint(Transform den, string rendererName, Color expected)
        {
            int found = 0;
            Renderer[] renderers = den.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject.name != rendererName) continue;
                found++;
                AssertColor(BlockColor(renderers[i], "_BaseColor"), expected, rendererName + " base");
                AssertColor(BlockColor(renderers[i], "_Color"), expected, rendererName);
            }
            Assert.Greater(found, 0, rendererName);
        }

        private static Renderer RendererNamed(Transform root, string name)
        {
            Transform t = FindNamed(root, name);
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        private static Color BlockColor(Renderer rend, string property)
        {
            var block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            return block.GetColor(property);
        }

        private static void AssertColor(Color actual, Color expected, string label)
        {
            Assert.AreEqual(expected.r, actual.r, 0.02f, label + " r");
            Assert.AreEqual(expected.g, actual.g, 0.02f, label + " g");
            Assert.AreEqual(expected.b, actual.b, 0.02f, label + " b");
            Assert.AreEqual(expected.a, actual.a, 0.02f, label + " a");
        }

        private static void AssertWhiteEmission(Color emission, string label)
        {
            Assert.Greater(emission.maxColorComponent, 0.5f, label);
            Assert.AreEqual(emission.r, emission.g, 0.05f, label + " not orange");
            Assert.AreEqual(emission.g, emission.b, 0.05f, label + " not orange");
        }

        private static Color Dim(Color c)
        {
            float grey = c.grayscale;
            return Color.Lerp(c, new Color(grey, grey, grey), 0.7f) * 0.7f;
        }

        private static Bounds ActiveBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, root.name);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
