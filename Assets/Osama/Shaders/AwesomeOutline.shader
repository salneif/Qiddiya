// Outline Shader رهيب بأسلوب Inverted Hull — URP / Unity 6.
// باصّان: (1) باص الأوت لاين يرسم قشرة خارجية موسّعة على المحاور، (2) باص الإضاءة العادي.
// مميزات: عرض ثابت على الشاشة (لا يتغيّر مع البُعد)، لون HDR يوهّج مع Bloom،
// نبض Pulse اختياري، وحافة Fresnel Rim.
// طريقة الاستخدام: أنشئ متيريال بهذا الشيدر وحطّه على أي كائن.
Shader "Osama/AwesomeOutline"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Outline)]
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 0.4, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 2
        _OutlineEmission ("Outline Glow", Range(1, 12)) = 2.5

        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3

        [Header(Fresnel Rim)]
        [HDR] _RimColor ("Rim Color", Color) = (0, 0, 0, 0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ================= باص الأوت لاين (Inverted Hull) =================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineEmission;
                float  _PulseSpeed;
                float  _PulseAmount;
                float4 _RimColor;
                float  _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float4 posCS  = TransformObjectToHClip(IN.positionOS.xyz);
                // إسقاط النرمال إلى فضاء القص لإزاحة الحواف للخارج
                float3 normCS = normalize(mul((float3x3)UNITY_MATRIX_MVP, IN.normalOS));
                float2 offset = normalize(normCS.xy);
                offset.x *= _ScreenParams.y / _ScreenParams.x; // تصحيح نسبة الشاشة

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                // الضرب في posCS.w يجعل السماكة ثابتة على الشاشة مهما بَعُد الكائن
                posCS.xy += offset * (_OutlineWidth * 0.01) * pulse * posCS.w;

                OUT.positionHCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                return half4(_OutlineColor.rgb * _OutlineEmission * pulse, 1.0);
            }
            ENDHLSL
        }

        // ================= باص الإضاءة الأساسي =================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineEmission;
                float  _PulseSpeed;
                float  _PulseAmount;
                float4 _RimColor;
                float  _RimPower;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo  = baseTex.rgb * _BaseColor.rgb;

                float3 normalWS   = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);

                float ndotl    = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (ndotl * mainLight.shadowAttenuation);
                half3 ambient  = SampleSH(normalWS);
                half3 col      = albedo * (lighting + ambient);

                // حافة Fresnel
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  fres    = pow(1.0 - saturate(dot(normalWS, viewDir)), _RimPower);
                col += _RimColor.rgb * fres;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
