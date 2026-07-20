// شيدر عمود طاقة: لون متوهّج "يتعبّى" عموديًا (يرقى وينزل) داخل المجسم،
// وعند الإطفاء يتحوّل العمود كله إلى أسود.
// URP / Unity 6. يقوده EnergyPillarController عبر _Fill (مستوى التعبئة) و _On (يشتغل/مطفي).
Shader "Osama/EnergyPillar"
{
    Properties
    {
        [HDR] _FillColor ("Fill Color (HDR)", Color) = (0.2, 0.9, 1.0, 1)
        _DarkColor ("Empty Part Color", Color) = (0.05, 0.06, 0.08, 1)
        _OffColor ("Off Color (Black)", Color) = (0.0, 0.0, 0.0, 1)

        _Fill ("Fill Level", Range(0, 1)) = 0.5
        _On ("On (1) / Off (0)", Range(0, 1)) = 1

        _EdgeWidth ("Level Edge Glow Width", Range(0.001, 0.3)) = 0.05
        _EdgeBoost ("Level Edge Glow Boost", Range(0, 10)) = 2

        _MinY ("Mesh Bottom (local Y)", Float) = -0.5
        _MaxY ("Mesh Top (local Y)", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor;
                float4 _DarkColor;
                float4 _OffColor;
                float  _Fill;
                float  _On;
                float  _EdgeWidth;
                float  _EdgeBoost;
                float  _MinY;
                float  _MaxY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float  heightNorm  : TEXCOORD0; // 0 أسفل المجسم → 1 أعلاه
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.heightNorm = saturate((IN.positionOS.y - _MinY) / max(_MaxY - _MinY, 1e-4));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float h = IN.heightNorm;

                // المعبّى (تحت مستوى _Fill) ملوّن، وفوقه غامق
                float filled = step(h, _Fill);
                half3 col = lerp(_DarkColor.rgb, _FillColor.rgb, filled);

                // خط توهّج عند سطح التعبئة (يتحرك مع المستوى)
                float edge = 1.0 - saturate(abs(h - _Fill) / _EdgeWidth);
                col += _FillColor.rgb * (edge * edge) * _EdgeBoost * step(0.001, _Fill);

                // الإطفاء: كل شيء يذوب إلى الأسود
                col = lerp(_OffColor.rgb, col, saturate(_On));

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
