// نقطة ظل ناعمة تحت اللاعب — دائرة مرسومة بالحساب لا بتكستشر، فلا تحتاج أي ملف صورة.
// يستخدمها PlayerGroundMarker على مربّع صغير ملصوق بالأرض.
//
// بلا كلمات مفتاحية (نسخة واحدة من الشيدر)، والماتيريال في مجلد Resources،
// فيدخل البلد دائمًا ويشتغل فيه مثل المحرر بالضبط.
// URP / Unity 6.
Shader "Osama/GroundMarker"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.55)
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.4
        _Ring ("Ring Width (0 = solid dot)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundMarker"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            // يسحب النقطة نحو الكاميرا قليلًا فلا تومض مع الأرض تحتها
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Softness;
                float  _Ring;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 0 في المركز، 1 على حافة المربّع الداخلية
                float d = length(input.uv - 0.5) * 2.0;
                float edge = saturate(1.0 - _Softness);
                float alpha = 1.0 - smoothstep(edge, 1.0, d);

                // حلقة: نقصّ المنتصف فتبقى حافة دائرية فقط
                if (_Ring > 0.001)
                {
                    float inner = saturate(1.0 - _Ring);
                    alpha *= smoothstep(inner - _Softness, inner, d);
                }

                return half4(_Color.rgb, _Color.a * alpha);
            }
            ENDHLSL
        }
    }
}
