// Lit (PBR, metallic) para URP / Unity 6 con mapa de opacidad independiente.
// Modos: Opaque, Cutout (alpha clipping) y Transparent, elegibles en el material
// (el selector lo dibuja Editor/LitOpacityShaderGUI.cs).
//
// Opacidad final = Opacity Map (R) [invertible] * Opacity * Base Map.a * Base Color.a
// Opacity Map y Normal Map usan las mismas UV y tiling que el Base Map.
Shader "Custom/LitOpacity"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [NoScaleOffset] _OpacityMap ("Opacity Map (R)", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
        [MaterialToggle] _InvertOpacity ("Invert Opacity", Float) = 0
        _Cutoff ("Alpha Cutoff (Cutout y sombras)", Range(0, 1)) = 0.5

        [NoScaleOffset][Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1

        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Back = normal, Off = doble cara)", Float) = 2

        // Gestionados por el ShaderGUI
        [HideInInspector] _Mode ("__mode", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
        TEXTURE2D(_OpacityMap); SAMPLER(sampler_OpacityMap);
        TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Opacity;
            half   _InvertOpacity;
            half   _Cutoff;
            half   _BumpScale;
            half   _Metallic;
            half   _Smoothness;
            half4  _EmissionColor;
        CBUFFER_END

        half SampleOpacity(float2 uv)
        {
            half o = SAMPLE_TEXTURE2D(_OpacityMap, sampler_OpacityMap, uv).r;
            o = lerp(o, 1.0h - o, _InvertOpacity);
            return o * _Opacity;
        }

        half GetAlpha(float2 uv)
        {
            half baseA = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
            return baseA * SampleOpacity(uv);
        }

        // Recorte en Cutout; en Transparent también se usa para que las sombras salgan recortadas
        void ClipAlpha(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }
        ENDHLSL

        // ---------------------------------------------------------------- Forward
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            // Material
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT

            // URP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            // Forward+ / Deferred+ (Unity 6.1+)
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // Unity
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;   // w = signo de la bitangente
                float2 lightmapUV : TEXCOORD4;
                half   fogFactor  : TEXCOORD5;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3  vertexLight : TEXCOORD6;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = n.normalWS;
                o.tangentWS  = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv         = TRANSFORM_TEX(v.uv, _BaseMap);
                o.lightmapUV = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                o.fogFactor  = ComputeFogFactor(p.positionCS.z);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                o.vertexLight = VertexLighting(p.positionWS, n.normalWS);
            #endif
                return o;
            }

            half4 frag (Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half alpha = baseSample.a * SampleOpacity(i.uv);
                ClipAlpha(alpha);

                // Normal (con normal map opcional) y corrección para la cara trasera
            #ifdef _NORMALMAP
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                float3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                float3x3 tbn = float3x3(i.tangentWS.xyz, bitangent, i.normalWS);
                float3 normalWS = TransformTangentToWorld(normalTS, tbn);
            #else
                half3 normalTS = half3(0, 0, 1);
                float3 normalWS = i.normalWS;
            #endif
                normalWS = NormalizeNormalPerPixel(normalWS) * IS_FRONT_VFACE(facing, 1.0, -1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.vertexLighting = i.vertexLight;
            #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
            #if defined(LIGHTMAP_ON)
                inputData.bakedGI = SampleLightmap(i.lightmapUV, normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);
            #else
                inputData.bakedGI = SampleSH(normalWS);
                inputData.shadowMask = half4(1, 1, 1, 1);
            #endif

                SurfaceData s = (SurfaceData)0;
                s.albedo = baseSample.rgb;
                s.metallic = _Metallic;
                s.specular = half3(0, 0, 0);
                s.smoothness = _Smoothness;
                s.normalTS = normalTS;
                s.emission = _EmissionColor.rgb;
                s.occlusion = 1;
                s.alpha = alpha;

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);

            #if defined(_SURFACE_TYPE_TRANSPARENT)
                color.a = alpha;
            #else
                color.a = 1;
            #endif
                return color;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- Shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
            #if defined(_ALPHATEST_ON) || defined(_SURFACE_TYPE_TRANSPARENT)
                clip(GetAlpha(i.uv) - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- Depth
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                ClipAlpha(GetAlpha(i.uv));
                return i.positionCS.z;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- Depth + Normals (SSAO, etc.)
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                return o;
            }

            half4 frag (Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                ClipAlpha(GetAlpha(i.uv));

            #ifdef _NORMALMAP
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                float3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                float3 normalWS = TransformTangentToWorld(normalTS, float3x3(i.tangentWS.xyz, bitangent, i.normalWS));
            #else
                float3 normalWS = i.normalWS;
            #endif
                normalWS = NormalizeNormalPerPixel(normalWS) * IS_FRONT_VFACE(facing, 1.0, -1.0);

            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "LitOpacityShaderGUI"
}
