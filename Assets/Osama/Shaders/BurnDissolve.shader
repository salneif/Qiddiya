// شيدر تفتّت ناري بأسلوب Little Nightmares — يُستخدم لحظة موت اللاعب:
// تتفتّت الشخصية تدريجيًا مع توهّج ناري على حافة التفتّت، ثم تختفي، ثم تعود.
// يقوده مكوّن DeathDissolveEffect عبر تحريك _DissolveAmount من 0 (كامل) إلى 1 (متفتّت).
// URP / Unity 6.
Shader "Osama/BurnDissolve"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.02, 0.02, 1)
        [HDR] _EdgeColor ("Burn Edge Color", Color) = (1.0, 0.35, 0.05, 1)
        _EdgeEmission ("Edge Emission", Range(0, 20)) = 6
        _EdgeWidth ("Edge Width", Range(0.001, 0.4)) = 0.08
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Range(1, 60)) = 14
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 uv          : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float  _EdgeEmission;
                float  _EdgeWidth;
                float  _DissolveAmount;
                float  _NoiseScale;
            CBUFFER_END

            // ضوضاء قيمية إجرائية (بدون حاجة لتكستشر)
            float hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(i + float3(0, 0, 0));
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS  = positionInputs.positionWS;
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float n = valueNoise(IN.positionWS * _NoiseScale);

                // ما دون العتبة يتفتّت (يُقصّ فيختفي)
                if (n < _DissolveAmount)
                    discard;

                half3 col = _BaseColor.rgb;

                // شريط ناري متوهّج قرب حافة التفتّت
                float edge = 1.0 - saturate((n - _DissolveAmount) / max(_EdgeWidth, 1e-4));
                col += _EdgeColor.rgb * _EdgeEmission * edge;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
