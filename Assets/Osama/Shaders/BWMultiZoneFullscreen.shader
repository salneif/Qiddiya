// شيدر فولسكرين: العالم أبيض وأسود ما عدا "مناطق ملوّنة" كروية حول كل WorldInteractor.
// يعيد بناء موقع كل بكسل بالعالم من الـ Depth، ويمر على مصفوفة الـ Interactors
// (نفس المصفوفة التي يرسلها InteractorManager)، فأي بكسل داخل أي منطقة يبقى ملوّنًا.
//
// لا يتطلب تعديل أي ماتيريال، ولا Shader Graph — يشتغل على الشاشة كاملة.
// الإعداد: ماتيريال من هذا الشيدر + Full Screen Pass Renderer Feature
// (Injection: After Rendering Post Processing، Requirements: Depth) + Depth Texture مفعّلة.
Shader "Osama/BWMultiZoneFullscreen"
{
    Properties
    {
        [HDR] _EdgeColor ("Zone Edge Glow", Color) = (1.0, 1.0, 1.0, 0.6)
        _EdgeWidth ("Edge Width (meters)", Float) = 0.35
        _Softness ("Zone Softness (meters)", Float) = 1.5
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

            // نفس المصفوفات التي يرسلها InteractorManager
            float4 _InteractorData[MAX_INTERACTORS]; // xyz = الموقع، w = نصف القطر
            int    _InteractorCount;

            float _WorldBWAmount; // 0 ملوّن بالكامل → 1 أبيض/أسود
            float4 _EdgeColor;
            float  _EdgeWidth;
            float  _Softness;
            float  _FlatOnGround; // 1 = دائرة مسطّحة على الأرض تتجاهل ارتفاع الكائن

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

                // أقرب حافة منطقة (لأصغر مسافة موقّعة عبر كل الكائنات)
                float zone = 0.0;   // 1 داخل أي منطقة
                float edge = 0.0;   // توهّج على الحدود
                [loop]
                for (int i = 0; i < _InteractorCount; i++)
                {
                    float radius = _InteractorData[i].w;
                    if (radius <= 0.0001) continue;

                    // الفرق بين البكسل والكائن؛ عند التفعيل نتجاهل المحور الرأسي (Y)
                    // فتصير الدائرة مسطّحة على الأرض حول الكائن مهما كان ارتفاعه
                    float3 diff = worldPos - _InteractorData[i].xyz;
                    diff.y *= (1.0 - _FlatOnGround);
                    float dist = length(diff);

                    // المنطقة الملوّنة تنتهي عند radius (الحافة الصلبة)، والنعومة للداخل فقط
                    float z = 1.0 - smoothstep(radius - max(_Softness, 1e-4), radius, dist);
                    zone = max(zone, z);

                    // الخط المتوهّج عند نفس نصف القطر تمامًا → ينطبق على حافة اللون
                    float e = 1.0 - saturate(abs(dist - radius) / max(_EdgeWidth, 1e-4));
                    edge = max(edge, e);
                }

                // تحويل للرمادي خارج المناطق فقط
                float bw = bwAmount * (1.0 - zone);
                half gray = dot(col.rgb, half3(0.299, 0.587, 0.114));
                half3 outCol = lerp(col.rgb, gray.xxx, bw);

                // خط توهّج على حدود المناطق
                outCol += _EdgeColor.rgb * (edge * edge) * _EdgeColor.a * bwAmount;

                return half4(outCol, col.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
