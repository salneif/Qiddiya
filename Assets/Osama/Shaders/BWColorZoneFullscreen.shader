// شيدر فولسكرين: العالم أبيض وأسود ما عدا "منطقة ملوّنة" كروية حول الآيتم
// (بأسلوب MinionsArt World Position Effect لكن على الشاشة كاملة بدون تعديل أي ماتيريال).
//
// يعيد بناء موقع كل بكسل بالعالم من الـ Depth ويقيس بعده عن _ZonePosition:
//  داخل _ZoneRadius → ألوان طبيعية، خارجه → أبيض وأسود، مع حافة ناعمة وخط توهّج اختياري.
//
// القيم العامة (Global) يرسلها ColorZoneInteractor و WorldBWController:
//  _ZonePosition, _ZoneRadius, _ZoneSoftness, _WorldBWAmount (0 ملوّن بالكامل → 1 أبيض/أسود).
//
// الإعداد: ماتيريال من هذا الشيدر + Full Screen Pass Renderer Feature (Requirements: Depth)
// و Injection Point = After Rendering Post Processing، وفعّل Depth Texture في أصل URP.
Shader "Osama/BWColorZoneFullscreen"
{
    Properties
    {
        [HDR] _EdgeColor ("Zone Edge Glow", Color) = (1.0, 0.55, 0.15, 0.6)
        _EdgeWidth ("Edge Width (meters)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "BWColorZone"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // قيم عامة تُرسل من السكربتات (Shader.SetGlobal...)
            float3 _ZonePosition;
            float  _ZoneRadius;
            float  _ZoneSoftness;
            float  _WorldBWAmount;

            // خصائص الماتيريال
            float4 _EdgeColor;
            float  _EdgeWidth;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float bwAmount = saturate(_WorldBWAmount);
                if (bwAmount <= 0.0001)
                    return col; // العالم ملوّن بالكامل — لا شيء يتغيّر

                // موقع البكسل بالعالم من عمق المشهد
                float rawDepth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

                float dist = distance(worldPos, _ZonePosition);

                // 1 داخل المنطقة الملوّنة، 0 خارجها (بحافة ناعمة بعرض _ZoneSoftness)
                float zone = 1.0 - smoothstep(_ZoneRadius,
                                              _ZoneRadius + max(_ZoneSoftness, 1e-4), dist);
                zone *= step(0.001, _ZoneRadius); // لا منطقة إذا كان نصف القطر صفرًا

                // التحويل للرمادي خارج المنطقة فقط
                float bw = bwAmount * (1.0 - zone);
                half gray = dot(col.rgb, half3(0.299, 0.587, 0.114));
                half3 outCol = lerp(col.rgb, gray.xxx, bw);

                // خط توهّج على حدود المنطقة (اختياري — لونه وشدته من الماتيريال)
                float edge = 1.0 - saturate(abs(dist - _ZoneRadius) / max(_EdgeWidth, 1e-4));
                outCol += _EdgeColor.rgb * (edge * edge) * _EdgeColor.a
                          * bwAmount * step(0.001, _ZoneRadius);

                return half4(outCol, col.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
