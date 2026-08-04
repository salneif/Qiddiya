// شبح طائر: جسد شبه شفاف يُغبّش ما خلفه فعليًا، بحافة متوهّجة ونصف سفلي
// يتلاشى في الهواء فلا تُرى له أرجل.
//
// الغبش حقيقي لا مزيّف: يأخذ عدة عيّنات من _CameraOpaqueTexture ويمزجها،
// فيبدو المشهد خلفه ضبابيًا كأنك تنظر عبر زجاج مثلج. ويشوّهه بضوضاء متحركة
// فيتموّج ببطء كأنه دخان متماسك.
//
// يتطلب: Opaque Texture مفعّلة في الـ URP Asset (مفعّلة في PC_RPAsset ✓).
// URP / Unity 6. حُطّ ماتيريالًا منه على مجسّم الشبح.
Shader "Osama/GhostSpirit"
{
    Properties
    {
        [Header(Body)]
        [HDR] _GhostColor ("Ghost Tint", Color) = (0.55, 0.8, 1.0, 1)
        _Opacity ("Body Opacity", Range(0, 1)) = 0.55

        [Header(Haze)]
        _BlurStrength ("Blur Strength", Range(0, 0.05)) = 0.014
        _Distortion ("Distortion", Range(0, 0.1)) = 0.018

        [Header(Rim)]
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 2.5
        [HDR] _RimColor ("Rim Glow", Color) = (0.7, 0.95, 1.0, 1)
        _RimStrength ("Rim Strength", Range(0, 6)) = 2.2

        [Header(Living Noise)]
        _NoiseScale ("Noise Scale", Range(1, 30)) = 7
        _NoiseSpeed ("Noise Speed", Range(0, 5)) = 0.9
        _NoiseStrength ("Noise Break Up", Range(0, 1)) = 0.35

        [Header(Bottom Fade)]
        _FadeBottom ("Fade Bottom (local Y)", Float) = -0.6
        _FadeTop ("Fade Top (local Y)", Float) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "GhostForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _GhostColor;
                float  _Opacity;
                float  _BlurStrength;
                float  _Distortion;
                float  _RimPower;
                float4 _RimColor;
                float  _RimStrength;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseStrength;
                float  _FadeBottom;
                float  _FadeTop;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
                float4 screenPos   : TEXCOORD3;
            };

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

                float n000 = hash13(i);
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
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            /// تسع عيّنات حول النقطة = غبش حقيقي لما خلف الشبح
            half3 SampleBlurred(float2 uv, float r)
            {
                half3 sum = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( r,  0)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-r,  0)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( 0,  r)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( 0, -r)).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( r,  r) * 0.7).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-r,  r) * 0.7).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2( r, -r) * 0.7).rgb;
                sum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(-r, -r) * 0.7).rgb;
                return sum / 9.0;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.screenPos   = ComputeScreenPos(p.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDir  = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // ضوضاء ملتصقة بالجسم تتموّج مع الزمن
                float noise = valueNoise(IN.positionOS * _NoiseScale +
                                         float3(0.0, _Time.y * _NoiseSpeed, 0.0));

                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);

                // تشويه المشهد خلفه بالضوضاء ثم تغبيشه
                float2 offset = (float2(noise, valueNoise(IN.positionOS * _NoiseScale + 17.3)) - 0.5)
                                * _Distortion;
                half3 behind = SampleBlurred(screenUV + offset, _BlurStrength);

                // حافة فرينل: الأطراف متوهّجة والوسط شبه فارغ — أساس شكل الشبح
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _RimPower);

                half3 col = behind * _GhostColor.rgb;
                col += _RimColor.rgb * fresnel * _RimStrength;

                // تلاشي النصف السفلي: لا أرجل، يذوب في الهواء
                float fade = saturate((IN.positionOS.y - _FadeBottom) /
                                      max(_FadeTop - _FadeBottom, 1e-4));

                float alpha = (_Opacity + fresnel * 0.6) * fade;
                alpha *= lerp(1.0, noise, _NoiseStrength);

                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
