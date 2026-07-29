// كائن من ظلّ خالص — كتلة سوداء تبتلع الضوء، سطحها يتلوّى وحوافها تتكسّر بضوضاء
// متحركة فتبدو حيّة غير مستقرّة. مخصص لسرب RatSwarm (كرات صغيرة).
//
// أهم ما فيه أنه <b>يزيح الرؤوس</b> فيكسر الصورة الظلّية للكرة — الكرة المثالية
// تُقرأ دائمًا ككرة مهما كان لونها، وإزاحة السطح هي ما يحوّلها إلى كائن.
//
// ويقرأ نفس مصفوفة مناطق الضوء التي يرسلها InteractorManager، فيشتعل ويتشقّق
// كلما لامسه ضوء — يعزّز قانون اللعبة بصريًا: الضوء يؤذي كائنات الظلام.
//
// Unlit عمدًا: لا إضاءة تفتح لونه، فيبقى أسود مهما كان المشهد منوّرًا.
// URP / Unity 6.
Shader "Osama/VoidCreature"
{
    Properties
    {
        [Header(Core)]
        _CoreColor ("Void Core", Color) = (0, 0, 0, 1)

        [Header(Silhouette Wobble)]
        _WobbleAmount ("Wobble Amount", Range(0, 0.5)) = 0.14
        _WobbleScale ("Wobble Scale", Range(1, 20)) = 4
        _WobbleSpeed ("Wobble Speed", Range(0, 6)) = 1.4

        [Header(Rim)]
        [HDR] _RimColor ("Rim Glow", Color) = (0.22, 0.04, 0.35, 1)
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 5)) = 1.1

        [Header(Living Noise)]
        _NoiseScale ("Noise Scale", Range(1, 40)) = 14
        _NoiseSpeed ("Noise Speed", Range(0, 5)) = 1.6
        _NoiseStrength ("Noise Break Up", Range(0, 1)) = 0.55

        [Header(Reaction To Light Zones)]
        [Toggle] _ReactToZones ("React To Light Zones", Float) = 1
        [HDR] _BurnColor ("Burn In Light", Color) = (1, 0.35, 0.08, 1)
        _BurnStrength ("Burn Strength", Range(0, 10)) = 3.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off // الإزاحة قد تقلب أجزاء رفيعة، فنرسم الوجهين

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // نفس المصفوفة التي يرسلها InteractorManager (عالمية، خارج CBUFFER)
            #define MAX_INTERACTORS 100
            float4 _InteractorData[MAX_INTERACTORS]; // xyz = الموقع، w = نصف القطر
            int    _InteractorCount;

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float  _WobbleAmount;
                float  _WobbleScale;
                float  _WobbleSpeed;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseStrength;
                float  _ReactToZones;
                float4 _BurnColor;
                float  _BurnStrength;
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
                float3 positionOS  : TEXCOORD2; // للضوضاء الملتصقة بالجسم
            };

            // ضوضاء قيمية إجرائية — بلا تكستشر
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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // الضوضاء في الفضاء المحلي فتلتصق بالجسم بدل أن ينزلق تحتها وهو يتحرك
                float wobble = valueNoise(IN.positionOS.xyz * _WobbleScale +
                                          float3(0.0, _Time.y * _WobbleSpeed, 0.0)) - 0.5;

                // إزاحة على اتجاه العمودي: تنتفخ وتنكمش أجزاء من السطح فيضيع شكل الكرة
                float3 displacedOS = IN.positionOS.xyz + IN.normalOS * wobble * _WobbleAmount;

                VertexPositionInputs p = GetVertexPositionInputs(displacedOS);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.positionOS  = IN.positionOS.xyz;
                return OUT;
            }

            /// شدة الضوء الساقط على هذه النقطة من أقرب منطقة آمنة (0 خارجها → 1 في قلبها)
            float LightZoneAmount(float3 worldPos)
            {
                float amount = 0.0;
                [loop]
                for (int i = 0; i < _InteractorCount; i++)
                {
                    float radius = _InteractorData[i].w;
                    if (radius <= 0.0001) continue;

                    float3 diff = worldPos - _InteractorData[i].xyz;
                    diff.y = 0.0; // أسطوانة — يطابق Flat On Ground في شيدر الفولسكرين
                    amount = max(amount, 1.0 - saturate(length(diff) / radius));
                }
                return amount;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDir  = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // ضوضاء سطحية ملتصقة بالجسم — يبدو كدخان متماسك لا كرة صلبة
                float noise = valueNoise(IN.positionOS * _NoiseScale +
                                         float3(0.0, _Time.y * _NoiseSpeed, 0.0));

                // حافة فرينل مكسّرة بالضوضاء — الحد الوحيد الذي يفصله عن السواد
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _RimPower);
                fresnel *= lerp(1.0, noise, _NoiseStrength);

                half3 col = _CoreColor.rgb;
                col += _RimColor.rgb * fresnel * _RimStrength;

                // الضوء يحرقه: كلما اقترب من قلب المنطقة اشتعل وتشقّق أكثر
                if (_ReactToZones > 0.5)
                {
                    float inLight = LightZoneAmount(IN.positionWS);
                    float burn = inLight * inLight * (0.4 + noise);
                    col += _BurnColor.rgb * burn * _BurnStrength;
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
