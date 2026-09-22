// Natural planetary ground (v2, 2026-09-22).
//
// The per-texel "what is here" comes from TerrainDataBake (512² over the map): splat weights
// (R dust / G rock / B vegetation-dark sand-lineae / A wet-mare-floor), world normals, and a mask
// (R horizon AO, G crater freshness / ejecta / rays, B regional albedo feature, A height).
// Per-pixel work adds what a 0.75 m bake cannot: anti-tiled grit from the authored detail tile,
// triplanar projection on cliffs (no smeared stretch marks), layered strata on rock faces, a
// correct tangent-frame detail normal, and multi-scale colour variation. The terrain casts and
// receives shadows, so crater walls and butte faces shade the ground around them.
//
// Property names from v1 are kept so PlanetaryMapDressing stays source-compatible.
Shader "SolarMajesty/PlanetGround"
{
    Properties
    {
        [MainColor] _BaseColor("Dust Color (splat R)", Color) = (0.82, 0.42, 0.18, 1)
        _DarkColor("Low / Feature Color", Color) = (0.46, 0.18, 0.08, 1)
        _RockColor("Rock Color (splat G)", Color) = (0.50, 0.24, 0.12, 1)
        _GrassColor("Layer B Color (vegetation / dark sand / lineae)", Color) = (0.22, 0.42, 0.16, 1)
        _WetColor("Layer A Color (wet / mare / floor)", Color) = (0.18, 0.22, 0.16, 1)
        _EjectaColor("Fresh Ejecta Color", Color) = (0.75, 0.73, 0.70, 1)
        _StrataColor("Strata Band Color", Color) = (0.56, 0.34, 0.19, 1)

        _MacroScale("Macro Variation Scale (m)", Range(4, 200)) = 60
        _MacroStrength("Macro Variation Strength", Range(0,1)) = 0.35
        _DetailScale("Speckle Scale (m)", Range(0.2, 12)) = 2.2
        _DetailStrength("Speckle Strength", Range(0,1)) = 0.22

        [Header(Authored Detail Tiles)]
        [NoScaleOffset] _DetailAlbedo("Detail Albedo", 2D) = "white" {}
        [NoScaleOffset] _DetailNormal("Detail Normal", 2D) = "bump" {}
        _DetailTexScale("Detail Tex Scale (m)", Range(0.5, 32)) = 8
        _DetailTexAmount("Detail Tex Amount", Range(0,1)) = 0
        _DetailMeanLuma("Detail Mean Luma", Range(0.05, 1)) = 0.4
        _DetailNormalStrength("Detail Normal Strength", Range(0, 2)) = 0.8

        _SlopeStart("Rock Slope Start", Range(0,1)) = 0.55
        _SlopeEnd("Rock Slope End", Range(0,1)) = 0.88

        [Header(Terrain Data Baker Maps)]
        [NoScaleOffset] _HeightMap("Height Map", 2D) = "gray" {}
        [NoScaleOffset] _NormalMap("World Normal (TDB)", 2D) = "bump" {}
        [NoScaleOffset] _SplatMap("Splat Map", 2D) = "red" {}
        [NoScaleOffset] _MaskMap("Mask Map", 2D) = "white" {}
        _BakeSize("Bake Size (m)", Vector) = (384, 384, 0, 0)
        _BakeNormalAmount("Bake Normal Amount", Range(0,1)) = 0
        _SplatAmount("Splat Amount", Range(0,1)) = 0
        _AOStrength("Baked AO Strength", Range(0,1)) = 0.6
        _FreshAmount("Fresh Ejecta Amount", Range(0,1)) = 0.6
        _FeatureAmount("Regional Feature Darkening", Range(0,1)) = 0.2
        _StrataStrength("Strata Strength", Range(0,1)) = 0
        _StrataScale("Strata Bands per Metre", Range(0.05, 4)) = 0.7
        _BlendContrast("Layer Blend Contrast", Range(0, 2)) = 0.8

        [Header(Splat Albedo Tiles)]
        [NoScaleOffset] _SplatAlbedo0("Splat Albedo 0 (Sand / Dust)", 2D) = "white" {}
        [NoScaleOffset] _SplatAlbedo1("Splat Albedo 1 (Grass / Rock)", 2D) = "white" {}
        [NoScaleOffset] _SplatAlbedo2("Splat Albedo 2 (Snow / High)", 2D) = "white" {}
        [NoScaleOffset] _SplatAlbedo3("Splat Albedo 3 (Wet / Floor)", 2D) = "white" {}
        _SplatTexScale("Splat Tex Scale (m)", Range(1, 48)) = 8
        _SplatAlbedoAmount("Splat Albedo Amount", Range(0,1)) = 0
        _SplatDesat("Splat Desat (Sand/Grass/Snow/Wet)", Vector) = (0, 0, 0, 0)

        _Smoothness("Smoothness", Range(0,1)) = 0.06
        _Metallic("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 250

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _DarkColor;
            float4 _RockColor;
            float4 _GrassColor;
            float4 _WetColor;
            float4 _EjectaColor;
            float4 _StrataColor;
            float  _MacroScale;
            float  _MacroStrength;
            float  _DetailScale;
            float  _DetailStrength;
            float4 _DetailAlbedo_ST;
            float4 _DetailNormal_ST;
            float  _DetailTexScale;
            float  _DetailTexAmount;
            float  _DetailMeanLuma;
            float  _DetailNormalStrength;
            float  _SlopeStart;
            float  _SlopeEnd;
            float4 _BakeSize;
            float  _BakeNormalAmount;
            float  _SplatAmount;
            float  _AOStrength;
            float  _FreshAmount;
            float  _FeatureAmount;
            float  _StrataStrength;
            float  _StrataScale;
            float  _BlendContrast;
            float4 _SplatAlbedo0_ST;
            float4 _SplatAlbedo1_ST;
            float4 _SplatAlbedo2_ST;
            float4 _SplatAlbedo3_ST;
            float  _SplatTexScale;
            float  _SplatAlbedoAmount;
            float4 _SplatDesat;
            float  _Smoothness;
            float  _Metallic;
        CBUFFER_END

        TEXTURE2D(_DetailAlbedo);   SAMPLER(sampler_DetailAlbedo);
        TEXTURE2D(_DetailNormal);   SAMPLER(sampler_DetailNormal);
        TEXTURE2D(_HeightMap);      SAMPLER(sampler_HeightMap);
        TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
        TEXTURE2D(_SplatMap);       SAMPLER(sampler_SplatMap);
        TEXTURE2D(_MaskMap);        SAMPLER(sampler_MaskMap);
        TEXTURE2D(_SplatAlbedo0);   SAMPLER(sampler_SplatAlbedo0);
        TEXTURE2D(_SplatAlbedo1);
        TEXTURE2D(_SplatAlbedo2);
        TEXTURE2D(_SplatAlbedo3);

        // Earth keeps authored chroma (desat 0). Mars desaturates then colorizes so
        // TDB Grass never reads as a lawn and Sand tufts do not stay olive.
        half3 SM_SplatLayer(half3 texRgb, half3 tint, float desat)
        {
            float d = saturate(desat);
            half luma = dot(texRgb, half3(0.299, 0.587, 0.114));
            half3 dehued = lerp(texRgb, luma.xxx, d);
            half3 colorized = tint * lerp(0.82, 1.08, saturate(luma));
            return lerp(dehued, colorized, d);
        }

        float SM_GHash(float2 p)
        {
            p = frac(p * float2(0.1031, 0.1030));
            p += dot(p, p.yx + 33.33);
            return frac((p.x + p.y) * p.x);
        }

        float SM_GNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = SM_GHash(i);
            float b = SM_GHash(i + float2(1, 0));
            float c = SM_GHash(i + float2(0, 1));
            float d = SM_GHash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float SM_Fbm(float2 p)
        {
            float v = 0.0;
            float a = 0.5;
            const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
            for (int i = 0; i < 4; i++)
            {
                v += a * SM_GNoise(p);
                p = mul(rot, p) * 2.03;
                a *= 0.5;
            }
            return v / 0.9375;   // 0..1, mean ~0.5
        }

        // Two rotated scales blended by noise: kills the visible grid of a repeating tile.
        float2 SM_Rot(float2 p, float a)
        {
            float c = cos(a), s = sin(a);
            return float2(c * p.x - s * p.y, s * p.x + c * p.y);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // URP's ComputeFogFactor(positionCS.z) collapses to ~0 under orthographic cameras;
            // linear view depth gives the same haze in perspective editor stills and the ortho Game tab.
            float SM_FogCoord(float3 positionWS)
            {
                float viewZ = -TransformWorldToView(positionWS).z;
                return ComputeFogFactorZ0ToFar(viewZ);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings GroundVertex(Attributes input)
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
                output.color = input.color;
                return output;
            }

            // Anti-tiled, triplanar-on-cliffs sample of the detail albedo.
            half3 SampleDetail(float3 pw, float3 n, float scale)
            {
                float2 uvA = pw.xz / scale;
                float2 uvB = SM_Rot(pw.xz, 0.9) / (scale * 2.7) + 0.37;
                float mixAB = smoothstep(0.35, 0.65, SM_Fbm(pw.xz / 23.0));
                half3 top = lerp(SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, uvA).rgb,
                                 SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, uvB).rgb, mixAB);
                float3 w = pow(abs(n), 4.0);
                w /= max(w.x + w.y + w.z, 1e-4);
                if (w.y > 0.97) return top;
                half3 sx = SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, pw.zy / scale).rgb;
                half3 sz = SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, pw.xy / scale).rgb;
                return top * w.y + sx * w.x + sz * w.z;
            }

            half3 SampleSplatTex(TEXTURE2D_PARAM(tex, smp), float2 p, float scale)
            {
                float2 uvA = p / scale;
                float2 uvB = SM_Rot(p, 1.3) / (scale * 2.3) + 0.61;
                float mixAB = smoothstep(0.35, 0.65, SM_Fbm(p / 31.0 + 7.0));
                return lerp(SAMPLE_TEXTURE2D(tex, smp, uvA).rgb, SAMPLE_TEXTURE2D(tex, smp, uvB).rgb, mixAB);
            }

            half4 GroundFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 pw = input.positionWS;
                float3 normalWS = normalize(input.normalWS);
                float2 p = pw.xz;
                float2 bakeUV = saturate(p / max(_BakeSize.xy, float2(1, 1)));

                float splatAmt = saturate(_SplatAmount);
                float4 splat = float4(1, 0, 0, 0);
                float4 mask = float4(1, 0, 0, 0.5);
                if (splatAmt > 0.001)
                {
                    half3 packedN = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, bakeUV).rgb;
                    float3 bakeN;
                    bakeN.x = packedN.r * 2.0 - 1.0;
                    bakeN.z = packedN.b * 2.0 - 1.0;
                    bakeN.y = sqrt(saturate(1.0 - bakeN.x * bakeN.x - bakeN.z * bakeN.z));
                    normalWS = normalize(lerp(normalWS, bakeN, saturate(_BakeNormalAmount)));
                    splat = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, bakeUV);
                    mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, bakeUV);
                }

                // --- detail grit (luminance, normalised around 1) -------------------------------
                float detailAmt = saturate(_DetailTexAmount);
                half3 detailRgb = half3(1, 1, 1);
                float grit = 1.0;
                if (detailAmt > 0.001)
                {
                    detailRgb = SampleDetail(pw, normalWS, max(_DetailTexScale, 0.25));
                    grit = dot(detailRgb, half3(0.299, 0.587, 0.114)) / max(_DetailMeanLuma, 0.05);
                }

                // --- layer colours ------------------------------------------------------------------
                float3 c0 = _BaseColor.rgb;
                float3 c1 = _RockColor.rgb;
                float3 c2 = _GrassColor.rgb;
                float3 c3 = _WetColor.rgb;

                // Layered strata on rock faces (sedimentary buttes, crater walls, Earth outcrops).
                if (_StrataStrength > 0.001)
                {
                    float warpY = pw.y + (SM_Fbm(p / 9.0) - 0.5) * 1.3 / max(_StrataScale, 0.05);
                    float band = 0.5 + 0.5 * sin(warpY * _StrataScale * 6.2831853);
                    band = smoothstep(0.15, 0.85, band);
                    c1 = lerp(c1, _StrataColor.rgb, band * _StrataStrength);
                }

                float texAmt = saturate(_SplatAlbedoAmount);
                if (texAmt > 0.001)
                {
                    float sc = max(_SplatTexScale, 0.25);
                    half3 l0 = SM_SplatLayer(SampleSplatTex(TEXTURE2D_ARGS(_SplatAlbedo0, sampler_SplatAlbedo0), p, sc), c0, _SplatDesat.x);
                    half3 l1 = SM_SplatLayer(SampleSplatTex(TEXTURE2D_ARGS(_SplatAlbedo1, sampler_SplatAlbedo0), p, sc), c1, _SplatDesat.y);
                    half3 l2 = SM_SplatLayer(SampleSplatTex(TEXTURE2D_ARGS(_SplatAlbedo2, sampler_SplatAlbedo0), p, sc), c2, _SplatDesat.z);
                    half3 l3 = SM_SplatLayer(SampleSplatTex(TEXTURE2D_ARGS(_SplatAlbedo3, sampler_SplatAlbedo0), p, sc), c3, _SplatDesat.w);
                    c0 = lerp(c0, l0, texAmt);
                    c1 = lerp(c1, l1, texAmt);
                    c2 = lerp(c2, l2, texAmt);
                    c3 = lerp(c3, l3, texAmt);
                }

                // Height-aware blend: rock pokes through dust where the grit is high, dust
                // settles in the grit's low spots — crisp, natural transitions instead of fades.
                float lift = (grit - 1.0) * _BlendContrast;
                float4 wts = splat * float4(1.0 - lift * 0.5, 1.0 + lift, 1.0, 1.0 - lift * 0.5);
                wts = max(wts, 0.0);
                wts /= max(dot(wts, float4(1, 1, 1, 1)), 1e-4);
                float3 layered = c0 * wts.r + c1 * wts.g + c2 * wts.b + c3 * wts.a;

                // Without bake maps (edit-mode previews) fall back to the v1 exposure grade.
                float exposure = saturate(input.color.r);
                float3 graded = lerp(_DarkColor.rgb, _BaseColor.rgb, saturate(exposure * 0.7 + 0.3));
                float3 albedo = lerp(graded, layered, splatAmt);

                // Steep faces lose their dust cover and show rock (v1 behaviour, softened with bakes).
                float slope = 1.0 - saturate(normalWS.y);
                float rockSlope = smoothstep(1.0 - _SlopeEnd, 1.0 - _SlopeStart, slope);
                albedo = lerp(albedo, c1, rockSlope * (1.0 - splatAmt * 0.7));

                // Regional albedo feature, fresh ejecta and rays.
                albedo *= 1.0 - _FeatureAmount * mask.b * 0.25;
                albedo = lerp(albedo, _EjectaColor.rgb, saturate(mask.g * _FreshAmount));

                // Multi-scale variation: broad patches, mid mottling, fine speckle.
                float macro = SM_Fbm(p / max(_MacroScale, 1.0));
                float mid = SM_Fbm(p / max(_MacroScale * 0.2, 1.0) + 13.7);
                float speckle = SM_GNoise(p / max(_DetailScale, 0.05));
                albedo *= 1.0 + (macro - 0.5) * _MacroStrength * 0.7 + (mid - 0.5) * _MacroStrength * 0.35
                              + (speckle - 0.5) * _DetailStrength * 0.35;

                // Grit texture multiplies the grade (keeps the body colour lock).
                albedo *= lerp(1.0, grit, detailAmt);

                // Baked horizon AO darkens crater floors, channel beds, the feet of cliffs.
                float ao = lerp(1.0, mask.r, _AOStrength * splatAmt);
                albedo *= lerp(1.0, ao, 0.6);

                // --- detail normal in a proper tangent frame (T = +X, B = +Z on the ground) ----
                if (detailAmt > 0.001 && _DetailNormalStrength > 0.001)
                {
                    float2 uvA = pw.xz / max(_DetailTexScale, 0.25);
                    float3 nTS = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, uvA));
                    nTS.xy *= _DetailNormalStrength * detailAmt * (1.0 - slope);
                    float3 T = normalize(float3(1, 0, 0) - normalWS * normalWS.x);
                    float3 B = cross(T, normalWS);
                    normalWS = normalize(T * nTS.x + B * nTS.y + normalWS * max(nTS.z, 0.2));
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = pw;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(pw);
                inputData.shadowCoord = TransformWorldToShadowCoord(pw);
                inputData.fogCoord = SM_FogCoord(pw);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = saturate(albedo);
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = saturate(_Smoothness + (wts.g + rockSlope) * 0.04);
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = ao;
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
