// شيدر احتراق/تفتّت للسبرايت (2D) بأسلوب Little Nightmares.
// يحترق على شكل ظل الشخصية نفسه (يحترم ألفا السبرايت) مع حافة نار متوهّجة.
// يقوده DeathDissolveEffect عبر تحريك _DissolveAmount من 0 (كامل) إلى 1 (متلاشي).
// URP 2D / Unity 6. حُط متيريالًا بهذا الشيدر في حقل Dissolve Material.
Shader "Osama/BurnDissolveSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _EdgeColor ("Burn Edge Color", Color) = (1, 0.35, 0.05, 1)
        _EdgeEmission ("Edge Emission", Range(0, 20)) = 7
        _EdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.12
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Range(1, 60)) = 12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _EdgeColor;
                float  _EdgeEmission;
                float  _EdgeWidth;
                float  _DissolveAmount;
                float  _NoiseScale;
            CBUFFER_END

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 x)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash12(i);
                float b = hash12(i + float2(1, 0));
                float c = hash12(i + float2(0, 1));
                float d = hash12(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                float baseAlpha = tex.a;

                float n = valueNoise(IN.uv * _NoiseScale);

                // keep = 1 حيث لم يتفتّت بعد، 0 حيث احترق
                float keep = step(_DissolveAmount, n);

                // شريط ناري متوهّج عند حافة التفتّت (داخل السبرايت فقط)
                float edge = saturate(1.0 - (n - _DissolveAmount) / max(_EdgeWidth, 1e-4));

                half3 col   = tex.rgb + _EdgeColor.rgb * _EdgeEmission * edge * baseAlpha * keep;
                float alpha = baseAlpha * keep;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
