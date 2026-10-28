// Cono de luz falso para focos (URP / Unity 6).
// Aditivo, sin coste de iluminación: Fresnel para bordes suaves, caída a lo largo del cono,
// intersección suave con la geometría (necesita Depth Texture en el URP Asset)
// y fade cuando la cámara se acerca o entra en el cono.
Shader "Custom/FakeLightCone"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.92, 0.75, 1)
        _Intensity ("Intensity", Range(0, 10)) = 1
        _EdgeSoftness ("Edge Softness (Fresnel)", Range(0.1, 8)) = 2
        _LengthFalloff ("Length Falloff", Range(0.1, 8)) = 1.5
        _SoftDistance ("Soft Intersection Distance", Float) = 0.5
        _CameraFadeStart ("Camera Fade Start", Float) = 0.3
        _CameraFadeDistance ("Camera Fade Distance", Float) = 1
        _NoiseTex ("Dust Noise (opcional)", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0
        _NoiseSpeed ("Noise Scroll Speed (XY)", Vector) = (0.02, -0.05, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FakeLightCone"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One      // aditivo
            ZWrite Off
            ZTest LEqual
            Cull Off           // visible también desde dentro del cono

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half   _Intensity;
                half   _EdgeSoftness;
                half   _LengthFalloff;
                float  _SoftDistance;
                float  _CameraFadeStart;
                float  _CameraFadeDistance;
                float4 _NoiseTex_ST;
                half   _NoiseStrength;
                float4 _NoiseSpeed;
            CBUFFER_END

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
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float2 uv         : TEXCOORD2;   // x = vuelta, y = 0 en el foco, 1 al final
                float4 screenPos  : TEXCOORD3;
                float  eyeDepth   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS  = GetWorldSpaceViewDir(p.positionWS);
                o.uv         = v.uv;
                o.screenPos  = ComputeScreenPos(p.positionCS);
                o.eyeDepth   = -p.positionVS.z;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Bordes suaves: la silueta del cono se desvanece
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewDirWS);
                half edge = pow(saturate(abs(dot(n, v))), _EdgeSoftness);

                // Más intenso junto al foco, se apaga hacia el final
                half lengthFade = pow(saturate(1.0 - i.uv.y), _LengthFalloff);

                // Intersección suave con suelo, instrumentos, personajes...
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = saturate((sceneDepth - i.eyeDepth) / max(_SoftDistance, 1e-4));

                // Desvanecer si la cámara está muy cerca
                half camFade = saturate((i.eyeDepth - _CameraFadeStart) / max(_CameraFadeDistance, 1e-4));

                // Polvo en suspensión (opcional)
                float2 nUV = TRANSFORM_TEX(i.uv, _NoiseTex) + _NoiseSpeed.xy * _Time.y;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nUV).r;
                noise = lerp(1.0h, noise, _NoiseStrength);

                half a = edge * lengthFade * soft * camFade * noise * _Intensity;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
