// Stand-ins for package/Unity-6-only types missing from the 2021.3 module reference assemblies.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct EntityId : IEquatable<EntityId> { public int v; public bool Equals(EntityId o) => v == o.v; public override int GetHashCode() => v; public override string ToString() => v.ToString(); }
    public static class EntityIdExt { public static EntityId GetEntityId(this Object o) => default; }
}
namespace UnityEngine.Rendering
{
    public class VolumeParameter<T> { public T value; public bool overrideState; public void Override(T x) { value = x; overrideState = true; } }
    public class ClampedFloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : VolumeParameter<float> { }
    public class FloatParameter : VolumeParameter<float> { }
    public class ColorParameter : VolumeParameter<Color> { }
    public class BoolParameter : VolumeParameter<bool> { }
    public class Vector2Parameter : VolumeParameter<Vector2> { }
    public class VolumeComponent : ScriptableObject { public bool active; }
    public class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public bool TryGet<T>(out T c) where T : VolumeComponent { c = null; return false; }
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public bool Has<T>() where T : VolumeComponent => false;
        public void Remove<T>() where T : VolumeComponent { }
    }
    public class Volume : MonoBehaviour { public bool isGlobal; public float priority, weight, blendDistance; public VolumeProfile profile, sharedProfile; }
}
namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }
    public enum DepthOfFieldMode { Off, Gaussian, Bokeh }
    public class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }
    public class DepthOfFieldModeParameter : VolumeParameter<DepthOfFieldMode> { }
    public class Tonemapping : VolumeComponent { public TonemappingModeParameter mode = new TonemappingModeParameter(); }
    public class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature = new ClampedFloatParameter(), tint = new ClampedFloatParameter(); }
    public class ColorAdjustments : VolumeComponent { public FloatParameter postExposure = new FloatParameter(); public ClampedFloatParameter contrast = new ClampedFloatParameter(), hueShift = new ClampedFloatParameter(), saturation = new ClampedFloatParameter(); public ColorParameter colorFilter = new ColorParameter(); }
    public class Bloom : VolumeComponent { public MinFloatParameter threshold = new MinFloatParameter(), intensity = new MinFloatParameter(); public ClampedFloatParameter scatter = new ClampedFloatParameter(); public ColorParameter tint = new ColorParameter(); public BoolParameter highQualityFiltering = new BoolParameter(); }
    public class Vignette : VolumeComponent { public ColorParameter color = new ColorParameter(); public Vector2Parameter center = new Vector2Parameter(); public ClampedFloatParameter intensity = new ClampedFloatParameter(), smoothness = new ClampedFloatParameter(); public BoolParameter rounded = new BoolParameter(); }
    public class DepthOfField : VolumeComponent { public DepthOfFieldModeParameter mode = new DepthOfFieldModeParameter(); public MinFloatParameter gaussianStart = new MinFloatParameter(), gaussianEnd = new MinFloatParameter(), focusDistance = new MinFloatParameter(); public ClampedFloatParameter gaussianMaxRadius = new ClampedFloatParameter(), focalLength = new ClampedFloatParameter(), aperture = new ClampedFloatParameter(); public BoolParameter highQualitySampling = new BoolParameter(); }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public enum AntialiasingQuality { Low, Medium, High }
    public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing, renderShadows, stopNaN, dithering; public AntialiasingMode antialiasing; public AntialiasingQuality antialiasingQuality; public LayerMask volumeLayerMask; public Transform volumeTrigger; public bool requiresDepthTexture, requiresColorTexture; public void SetRenderer(int i) { } }
    public class UniversalAdditionalLightData : MonoBehaviour { public bool usePipelineSettings; public int additionalLightsShadowResolutionTier; public Texture lightCookieTexture; public Vector2 lightCookieSize, lightCookieOffset; }
}
namespace Unity.AI.Navigation
{
    public enum CollectObjects { All, Volume, Children, MarkedWithModifier }
    public class NavMeshSurface : UnityEngine.MonoBehaviour { public int agentTypeID, defaultArea; public CollectObjects collectObjects; public UnityEngine.LayerMask layerMask; public UnityEngine.AI.NavMeshCollectGeometry useGeometry; public bool overrideVoxelSize, overrideTileSize, buildHeightMesh; public float voxelSize; public int tileSize; public UnityEngine.AI.NavMeshData navMeshData; public void BuildNavMesh() { } public void RemoveData() { } public void AddData() { } public UnityEngine.AsyncOperation UpdateNavMesh(UnityEngine.AI.NavMeshData d) => null; }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.MonoBehaviour { public static EventSystem current; public bool IsPointerOverGameObject() => false; public bool IsPointerOverGameObject(int id) => false; }
}
