Shader "Custom/GhostGlitchV2"
{
    Properties
    {
        _Color ("Base Color", Color) = (0,0,0,1)

        [Header(Vertex Glitch)]
        _GlitchAmount  ("Vertex Glitch Strength",   Range(0,1))    = 0.12
        _GlitchSpeed   ("Glitch Change Speed",       Range(0.1,60)) = 20
        _GlitchChance  ("Glitch Jump Chance",        Range(0,1))    = 0.35

        [Header(Flicker Cutout)]
        _FlickerSpeed  ("Flicker Speed",             Range(0.1,60)) = 24
        _FlickerChance ("Instant Disappear Chance",  Range(0,1))    = 0.18

        [Header(Color Glitch Tint)]
        _TintAmount  ("Tint Strength During Glitch", Range(0,1))   = 0.35
        _TintColorA  ("Tint Color A",                Color) = (1,0,0.1,1)
        _TintColorB  ("Tint Color B",                Color) = (0,0.6,1,1)

        [Header(Scanlines)]
        _ScanlineIntensity ("Scanline Intensity",    Range(0,1))    = 0.80
        _ScanlineCount     ("Scanline Count",        Range(20,600)) = 180
        _ScanlineSpeed     ("Scanline Scroll Speed", Range(0,10))   = 2.5

        [Header(Pixel Block Corruption)]
        _BlockSize      ("Block Size",               Range(4,64))  = 20
        _BlockCorrupt   ("Block Shift Strength",     Range(0,1))   = 0.65
        _BlockFreq      ("Corruption Frequency",     Range(0,1))   = 0.55

        [Header(Horizontal Tear)]
        _TearIntensity  ("Tear Strength",            Range(0,1))   = 0.55
        _TearCount      ("Tear Zone Count",          Range(1,30))  = 12

        [Header(Black Dominance)]
        _BlackDominance ("Black Dominance",          Range(0,1))   = 0.80
        _BlackThreshold ("Darkness Threshold",       Range(0,1))   = 0.45

        [Header(Master Control)]
        _GlitchIntensity ("Glitch Intensity (Script)", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 uv          : TEXCOORD1;
                float  glitchSeed  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _GlitchAmount;
                float  _GlitchSpeed;
                float  _GlitchChance;
                float  _FlickerSpeed;
                float  _FlickerChance;
                float  _TintAmount;
                float4 _TintColorA;
                float4 _TintColorB;
                float  _ScanlineIntensity;
                float  _ScanlineCount;
                float  _ScanlineSpeed;
                float  _BlockSize;
                float  _BlockCorrupt;
                float  _BlockFreq;
                float  _TearIntensity;
                float  _TearCount;
                float  _BlackDominance;
                float  _BlackThreshold;
                float  _GlitchIntensity;
            CBUFFER_END

            float Hash1(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float Hash2(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            float3 HashOffset(float3 seedPos, float t)
            {
                float n = dot(seedPos, float3(12.9898, 78.233, 37.719)) + t;
                return float3(Hash1(n)-0.5, Hash1(n*1.37+1.7)-0.5, Hash1(n*2.13+3.1)-0.5);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float steppedTime = floor(_Time.y * _GlitchSpeed);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                float trigger  = Hash1(steppedTime * 7.13);
                float glitchOn = step(1.0 - (_GlitchChance * max(_GlitchIntensity, 0.05)), trigger);
                float3 jitter  = HashOffset(posWS, steppedTime) * _GlitchAmount * glitchOn * (0.5 + _GlitchIntensity);

                float tearLine  = floor(IN.uv.y * _TearCount) / _TearCount;
                float tearRnd   = Hash1(tearLine + floor(_Time.y * _GlitchSpeed * 0.35));
                float tearRnd2  = Hash1(tearLine * 3.7 + floor(_Time.y * _GlitchSpeed * 0.7));
                float tearShift = (tearRnd < _TearIntensity) ? (tearRnd2 - 0.5) * _TearIntensity * 0.18 : 0.0;
                jitter.x       += tearShift * _GlitchIntensity;

                posWS          += jitter;
                OUT.positionWS  = posWS;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv          = IN.uv;
                OUT.glitchSeed  = steppedTime;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv           = IN.uv;
                float  t            = _Time.y;
                float  steppedFlick = floor(t * _FlickerSpeed);

                // 1. Flicker Cutout
                float flickerNoise  = Hash1(steppedFlick * 3.71 + IN.glitchSeed * 0.01);
                float flickerActive = step(1.0 - (_FlickerChance * max(_GlitchIntensity, 0.05)), flickerNoise);
                clip(flickerActive > 0.5 ? -1 : 1);

                // 2. Pixel Block Corruption
                float2 blockUV    = floor(uv * _BlockSize) / _BlockSize;
                float  blockRnd   = Hash2(blockUV + floor(t * _GlitchSpeed * 0.5));
                float  blockRnd2  = Hash2(blockUV * 2.3 + floor(t * _GlitchSpeed));
                float2 blockShift = 0;
                if (blockRnd < _BlockFreq * _GlitchIntensity)
                {
                    blockShift.x = (Hash1(blockRnd  + t) - 0.5) * _BlockCorrupt * 0.25;
                    blockShift.y = (Hash1(blockRnd2 + t) - 0.5) * _BlockCorrupt * 0.08;
                }
                uv += blockShift;

                // 3. Scanlines
                float scan     = sin((uv.y + t * _ScanlineSpeed * 0.04) * _ScanlineCount * PI);
                scan           = pow(saturate(scan * 0.5 + 0.5), 1.4);
                float scanDark = lerp(1.0, scan, _ScanlineIntensity);

                // 4. Color Tint
                float  tintNoise  = Hash1(steppedFlick * 5.21);
                float  tintActive = step(1.0 - (_TintAmount * max(_GlitchIntensity, 0.05)), tintNoise);
                half4  tintColor  = (frac(steppedFlick * 0.5) < 0.5) ? _TintColorA : _TintColorB;
                half4  baseColor  = lerp(_Color, tintColor, tintActive * 0.6);

                // 5. Apply Scanlines
                baseColor.rgb *= scanDark;

                // 6. Black Corruption Blocks
                float2 bigBlock = floor(uv * (_BlockSize * 0.4)) / (_BlockSize * 0.4);
                float  blackRnd = Hash2(bigBlock + floor(t * _GlitchSpeed * 0.45) * 0.37);
                if (blackRnd < _BlockFreq * _GlitchIntensity * 0.5)
                    baseColor.rgb = half3(0, 0, 0);

                // 7. Black Dominance
                float brightness = dot(baseColor.rgb, half3(0.299, 0.587, 0.114));
                float blackMask  = 1.0 - smoothstep(0.0, _BlackThreshold, brightness);
                baseColor.rgb    = lerp(baseColor.rgb, half3(0,0,0), blackMask * _BlackDominance * _GlitchIntensity);

                // 8. Global Noise Flicker
                float gFlicker = Hash2(uv * 80.0 + t * _GlitchSpeed);
                float gMask    = step(1.0 - _GlitchIntensity * 0.25, gFlicker);
                baseColor.rgb  = lerp(baseColor.rgb, half3(0,0,0), gMask);

                return baseColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
