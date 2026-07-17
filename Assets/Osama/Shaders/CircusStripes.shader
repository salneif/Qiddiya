// شيدر سيرك/ملاهي — خطوط حلزونية ملوّنة متحركة (candy stripes) بلمعة احتفالية.
// يعطي العدو مظهرًا مبهجًا مخادعًا في حالته الطبيعية (الصلبة).
// URP / Unity 6. حُط ماتيريالًا منه على الشكل الصلب للعدو.
Shader "Osama/CircusStripes"
{
    Properties
    {
        _ColorA ("Stripe Color A", Color) = (0.9, 0.1, 0.15, 1)
        _ColorB ("Stripe Color B", Color) = (1.0, 0.95, 0.85, 1)
        _StripeCount ("Stripe Count", Float) = 14
        _Twist ("Spiral Twist", Float) = 3
        _ScrollSpeed ("Scroll Speed", Float) = 0.6
        [HDR] _Emission ("Festive Glow", Color) = (0.6, 0.2, 0.25, 1)
        _EmissionStrength ("Glow Strength", Range(0, 5)) = 0.6
        _RimColor ("Rim Color", Color) = (1, 0.85, 0.4, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float  _StripeCount;
                float  _Twist;
                float  _ScrollSpeed;
                float4 _Emission;
                float  _EmissionStrength;
                float4 _RimColor;
                float  _RimPower;
            CBUFFER_END

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
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // خطوط حلزونية: مزج إحداثي U مع V (اللف) وتحريكها بالزمن
                float coord = IN.uv.x + IN.uv.y * _Twist + _Time.y * _ScrollSpeed;
                float stripe = frac(coord * _StripeCount);
                float band = smoothstep(0.45, 0.55, stripe); // حافة ناعمة بين اللونين

                half3 albedo = lerp(_ColorA.rgb, _ColorB.rgb, band);

                // إضاءة أساسية بسيطة
                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (ndotl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(normalWS);
                half3 col = albedo * (lighting + ambient);

                // توهّج احتفالي على الخطوط الداكنة (اللون A)
                col += _Emission.rgb * _EmissionStrength * (1.0 - band);

                // حافة Fresnel احتفالية
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fres = pow(1.0 - saturate(dot(normalWS, viewDir)), _RimPower);
                col += _RimColor.rgb * fres;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
