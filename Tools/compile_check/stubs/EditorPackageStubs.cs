namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererFeature : UnityEngine.ScriptableObject { public bool isActive; public void SetActive(bool a) { } }
    public class ScreenSpaceAmbientOcclusion : ScriptableRendererFeature { }
    public class DecalRendererFeature : ScriptableRendererFeature { }
    public class ScriptableRendererData : UnityEngine.ScriptableObject { public System.Collections.Generic.List<ScriptableRendererFeature> rendererFeatures = new System.Collections.Generic.List<ScriptableRendererFeature>(); public void SetDirty() { } }
    public class UniversalRendererData : ScriptableRendererData { }
    public class PostProcessData : UnityEngine.ScriptableObject { }
}
