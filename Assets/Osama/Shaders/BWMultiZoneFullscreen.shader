// ==== BWMultiZoneFullscreen  v3 (color-reveal hard mask) ====
// فولسكرين: العالم أبيض وأسود ما عدا دائرة ملوّنة حول كل WorldInteractor.
// نموذج صارم: اللون يملأ داخل نصف القطر بحدّ قاطع، والخط المتوهّج شريط رفيع
// عند الحافة الخارجية تمامًا — فاللون لا يتجاوز الخط الأبيض أبدًا.
//
// الإعداد: ماتيريال من هذا الشيدر في Full Screen Pass (After Rendering Post Processing، Depth) + Depth Texture.
Shader "Osama/BWMultiZoneFullscreen"
{
    Properties
    {
        [HDR] _EdgeColor ("Zone Edge Glow", Color) = (1.0, 1.0, 1.0, 1.0)
        _EdgeWidth ("Edge Line Width (meters)", Range(0.01, 1.0)) = 0.15
        _EdgeSoftness ("Boundary Softness (meters)", Range(0.0, 0.5)) = 0.03
        [Toggle] _FlatOnGround ("Flat On Ground (ignore height)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "BWMultiZone"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_INTERACTORS 100

            float4 _InteractorData[MAX_INTERACTORS]; // xyz = الموقع، w = نصف القطر
            int    _InteractorCount;

            float  _WorldBWAmount;  // 0 ملوّن بالكامل → 1 أبيض/أسود
            float4 _EdgeColor;
            float  _EdgeWidth;
            float  _EdgeSoftness;
            float  _FlatOnGround;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float bwAmount = saturate(_WorldBWAmount);
                if (bwAmount <= 0.0001)
                    return col;

                float rawDepth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

                float inside = 0.0; // 1 = داخل الدائرة (ملوّن)
                float line01 = 0.0; // 1 = على الخط المتوهّج
                [loop]
                for (int i = 0; i < _InteractorCount; i++)
                {
                    float radius = _InteractorData[i].w;
                    if (radius <= 0.0001) continue;

                    float3 diff = worldPos - _InteractorData[i].xyz;
                    diff.y *= (1.0 - _FlatOnGround);
                    float dist = length(diff);

                    // داخل نصف القطر = ملوّن، خارجه = أبيض/أسود (حدّ قاطع بنعومة بسيطة)
                    float ins = 1.0 - smoothstep(radius, radius + max(_EdgeSoftness, 1e-4), dist);
                    inside = max(inside, ins);

                    // الخط: شريط رفيع ينتهي عند نصف القطر بالضبط (داخل الحدّ، لا يتجاوزه)
                    float ln = 1.0 - smoothstep(0.0, _EdgeWidth, radius - dist);
                    ln *= ins; // يظهر داخل الدائرة فقط فلا يمتد للأبيض/الأسود
                    line01 = max(line01, ln);
                }

                // خارج الدائرة → رمادي بالكامل، داخلها → ألوان طبيعية
                float bw = bwAmount * (1.0 - inside);
                half gray = dot(col.rgb, half3(0.299, 0.587, 0.114));
                half3 outCol = lerp(col.rgb, gray.xxx, bw);

                // الخط المتوهّج فوق الكل
                outCol = lerp(outCol, _EdgeColor.rgb, saturate(line01 * _EdgeColor.a) * bwAmount);

                return half4(outCol, col.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
