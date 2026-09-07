// Master hull shader for Solar Majesty modules and units.
//
// Replaces authored PBR texture sets, which a solo project cannot produce for 47 meshes. Panel
// lines, edge wear, and settled dust are generated in world space, so an untextured Blender export
// gains surface detail without a UV unwrap, a bake, or a single texture file.
//
// World-space projection means the detail scale stays constant across every module regardless of
// mesh scale, which is what keeps a campus of different-sized kits reading as one built thing.
Shader "SolarMajesty/Hull"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.92, 0.92, 0.90, 1)
        _Metallic("Metallic", Range(0,1)) = 0.1
        _Smoothness("Smoothness", Range(0,1)) = 0.45

        [Header(Panel Lines)]
        _PanelScale("Panel Size (m)", Range(0.15, 6)) = 1.1
        _PanelWidth("Line Width", Range(0.001, 0.2)) = 0.022
        _PanelDarken("Line Darken", Range(0,1)) = 0.45
        _PanelBevel("Bevel Highlight", Range(0,1)) = 0.35

        [Header(Wear)]
        _WearColor("Wear Color", Color) = (0.32, 0.31, 0.30, 1)
        _WearAmount("Wear Amount", Range(0,1)) = 0.18
        _WearScale("Wear Scale", Range(0.5, 20)) = 5.0

        [Header(Dust)]
        _DustColor("Dust Color", Color) = (0.55, 0.34, 0.20, 1)
        _DustAmount("Dust Amount", Range(0,1)) = 0.25
        _DustSharpness("Dust Sharpness", Range(1, 12)) = 3.5
        _SkirtHeight("Ground Dust Skirt Height (m)", Range(0, 3)) = 0.0
        _SkirtAmount("Ground Dust Skirt Amount", Range(0,1)) = 0.0

        [Header(Authored Detail Tiles)]
        [NoScaleOffset] _DetailAlbedo("Detail Albedo (triplanar)", 2D) = "white" {}
        [NoScaleOffset] _DetailNormal("Detail Normal (triplanar)", 2D) = "bump" {}
        _DetailScale("Detail Tile Size (m)", Range(0.25, 12)) = 2.4
        _DetailAmount("Detail Albedo Amount", Range(0,1)) = 0.0
        _DetailNormalAmount("Detail Normal Amount", Range(0,1)) = 0.0

        [Header(Emissive)]
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _EmissionBandCenter("Band Center (object Y)", Float) = 0.0
        _EmissionBandWidth("Band Width", Range(0, 2)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float  _Metallic;
            float  _Smoothness;
            float  _PanelScale;
            float  _PanelWidth;
            float  _PanelDarken;
            float  _PanelBevel;
            float4 _WearColor;
            float  _WearAmount;
            float  _WearScale;
            float4 _DustColor;
            float  _DustAmount;
            float  _DustSharpness;
            float  _SkirtHeight;
            float  _SkirtAmount;
            float4 _DetailAlbedo_ST;
            float4 _DetailNormal_ST;
            float  _DetailScale;
            float  _DetailAmount;
            float  _DetailNormalAmount;
            float4 _EmissionColor;
            float  _EmissionBandCenter;
            float  _EmissionBandWidth;
        CBUFFER_END

        TEXTURE2D(_DetailAlbedo);
        SAMPLER(sampler_DetailAlbedo);
        TEXTURE2D(_DetailNormal);
        SAMPLER(sampler_DetailNormal);

        // World-space triplanar sample of the authored grit tile. Kit meshes have no UVs worth
        // trusting (primitives + joined FBX), so the tile is projected along the dominant normal
        // axis and blended — the same trick the panel seams use, so both stay in register.
        void SM_TriplanarDetail(float3 worldPos, float3 worldNormal, out float3 albedoMul, out float3 normalTS)
        {
            float3 blend = abs(worldNormal);
            blend = pow(blend, 4.0);
            blend /= max(blend.x + blend.y + blend.z, 1e-4);

            float inv = 1.0 / max(_DetailScale, 0.05);
            float2 uvX = worldPos.zy * inv;
            float2 uvY = worldPos.xz * inv;
            float2 uvZ = worldPos.xy * inv;

            float3 aX = SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, uvX).rgb;
            float3 aY = SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, uvY).rgb;
            float3 aZ = SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, uvZ).rgb;
            albedoMul = aX * blend.x + aY * blend.y + aZ * blend.z;

            float3 nX = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, uvX));
            float3 nY = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, uvY));
            float3 nZ = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, uvZ));
            // Swizzle each projection's tangent-space XY into the world axes it perturbs.
            float3 pX = float3(0.0, nX.y, nX.x);
            float3 pY = float3(nY.x, 0.0, nY.y);
            float3 pZ = float3(nZ.x, nZ.y, 0.0);
            normalTS = pX * blend.x + pY * blend.y + pZ * blend.z;
        }

        // Cheap value noise. Enough to break up flat panels; not trying to be a texture.
        float SM_Hash(float3 p)
        {
            p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float SM_Noise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);

            float n000 = SM_Hash(i + float3(0, 0, 0));
            float n100 = SM_Hash(i + float3(1, 0, 0));
            float n010 = SM_Hash(i + float3(0, 1, 0));
            float n110 = SM_Hash(i + float3(1, 1, 0));
            float n001 = SM_Hash(i + float3(0, 0, 1));
            float n101 = SM_Hash(i + float3(1, 0, 1));
            float n011 = SM_Hash(i + float3(0, 1, 1));
            float n111 = SM_Hash(i + float3(1, 1, 1));

            float nx00 = lerp(n000, n100, f.x);
            float nx10 = lerp(n010, n110, f.x);
            float nx01 = lerp(n001, n101, f.x);
            float nx11 = lerp(n011, n111, f.x);
            return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
        }

        // Distance to the nearest panel seam on one axis, in cell units.
        float SM_SeamAxis(float coord, float scale)
        {
            float c = coord / max(scale, 0.01);
            return abs(frac(c) - 0.5) * 2.0;   // 1 at cell centre, 0 at the seam
        }

        // Triplanar seam mask: dark groove plus a lighter bevel lip on the panel side.
        void SM_Panels(float3 worldPos, float3 worldNormal, out float groove, out float bevel)
        {
            float3 blend = abs(worldNormal);
            blend /= max(blend.x + blend.y + blend.z, 1e-4);

            // Each projection uses the two axes perpendicular to its dominant normal.
            float sx = min(SM_SeamAxis(worldPos.y, _PanelScale), SM_SeamAxis(worldPos.z, _PanelScale));
            float sy = min(SM_SeamAxis(worldPos.x, _PanelScale), SM_SeamAxis(worldPos.z, _PanelScale));
            float sz = min(SM_SeamAxis(worldPos.x, _PanelScale), SM_SeamAxis(worldPos.y, _PanelScale));
            float seam = sx * blend.x + sy * blend.y + sz * blend.z;

            float w = max(_PanelWidth, 1e-4);
            groove = 1.0 - smoothstep(0.0, w, seam);
            bevel = smoothstep(w, w * 2.6, seam) * (1.0 - smoothstep(w * 2.6, w * 5.0, seam));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HullVertex
            #pragma fragment HullFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HullVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 HullFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 positionWS = input.positionWS;

                float3 albedo = _BaseColor.rgb;

                // Authored grit tile (Dream Loop SM_Mat_* bakes) — multiplies the base colour so the
                // body colour lock holds, and tilts the normal so panels catch light unevenly.
                if (_DetailAmount > 0.001 || _DetailNormalAmount > 0.001)
                {
                    float3 detailMul, detailN;
                    SM_TriplanarDetail(positionWS, normalWS, detailMul, detailN);
                    // Normalise the tile around mid-grey so it adds texture, not a global tint.
                    float lum = dot(detailMul, float3(0.299, 0.587, 0.114));
                    float3 neutral = detailMul / max(lum, 0.05);
                    neutral = lerp(1.0, neutral, 0.55) * lerp(1.0, lum * 1.15, 0.45);
                    albedo *= lerp(1.0, neutral, _DetailAmount);
                    normalWS = normalize(normalWS + detailN * _DetailNormalAmount);
                }

                // Panel seams: darken the groove, lift the bevel lip beside it.
                float groove, bevel;
                SM_Panels(positionWS, normalWS, groove, bevel);
                albedo *= 1.0 - groove * _PanelDarken;
                albedo += bevel * _PanelBevel * 0.06;

                // Blotchy wear so large flat hulls are not perfectly uniform.
                float wear = SM_Noise(positionWS * _WearScale);
                wear = smoothstep(0.55, 0.95, wear) * _WearAmount;
                albedo = lerp(albedo, _WearColor.rgb, wear);

                // Dust settles on up-facing surfaces. This is what visually plants a module on a
                // planet instead of leaving it looking like a clean render pasted onto dirt.
                float up = saturate(dot(normalWS, float3(0, 1, 0)));
                float dust = pow(up, _DustSharpness) * _DustAmount;
                dust *= 0.65 + 0.35 * SM_Noise(positionWS * 1.7);

                // Kicked-up dust skirt: the lowest metre or so of any hull takes the ground colour
                // (thrown regolith, boot scuff), fading out with height. Concept hulls are white on
                // top and dirty at the sill, which is what separates "landed" from "placed".
                if (_SkirtHeight > 0.001 && _SkirtAmount > 0.001)
                {
                    float sill = 1.0 - smoothstep(0.0, _SkirtHeight, positionWS.y);
                    sill *= sill;
                    sill *= 0.7 + 0.3 * SM_Noise(positionWS * float3(2.3, 6.0, 2.3));
                    dust = max(dust, sill * _SkirtAmount);
                }
                albedo = lerp(albedo, _DustColor.rgb, saturate(dust));

                float smoothness = _Smoothness * (1.0 - saturate(dust) * 0.7) * (1.0 - wear * 0.5);
                float metallic = _Metallic * (1.0 - saturate(dust) * 0.5);

                // Object-space horizontal band for lit window strips.
                float band = 0.0;
                if (_EmissionBandWidth > 0.0001)
                {
                    band = 1.0 - smoothstep(0.0, _EmissionBandWidth,
                                            abs(input.positionOS.y - _EmissionBandCenter));
                }
                float3 emission = _EmissionColor.rgb * max(band, _EmissionBandWidth > 0.0001 ? 0.0 : 1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.fogCoord = input.fogCoord;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = metallic;
                surfaceData.specular = 0;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.emission = emission;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // Required by SSAO and decals: both read the depth-normals prepass.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
