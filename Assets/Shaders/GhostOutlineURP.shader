Shader "Game/GhostOutlineURP"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline HDR Color", Color) = (3, 0.06, 0.06, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.08)) = 0.012
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Name "GhostOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 expandedPositionOS = input.positionOS.xyz +
                    normalize(input.normalOS) * _OutlineWidth;
                output.positionHCS = TransformObjectToHClip(expandedPositionOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_OutlineColor);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
