// Dark-fantasy "rune ignition" glow for a skill node's unlock moment: an expanding
// ring of procedural ember/crack noise, additive so it reads as a burning glow
// against a dark node background. No external texture required — the crack
// pattern is generated in-shader (a value-noise hash), so this drops straight
// into a UI Image with nothing else to import.
//
// Usage: create a Material using this shader, assign it to a child Image
// (SkillNodeUnlockBurst.burnOverlay) sized to roughly the node's bounds, sitting
// above the icon/background but below any "locked" overlay. Animate _Progress
// from 0 to 1 over the burst duration (SkillNodeUnlockBurst does this for you).
Shader "JollyLlama/UI/EmberBurn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Progress   ("Progress (0-1)", Range(0,1)) = 0
        _CoreColor  ("Core Color (hot)",  Color) = (1, 0.85, 0.4, 1)
        _EdgeColor  ("Edge Color (ember)", Color) = (0.55, 0.07, 0.02, 1)
        _NoiseScale ("Crack Noise Scale", Float) = 12
        _RingWidth  ("Ring Width", Range(0.01, 1)) = 0.35

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _CoreColor;
            fixed4 _EdgeColor;
            float  _Progress;
            float  _NoiseScale;
            float  _RingWidth;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color    = v.color;
                return o;
            }

            // Cheap 2D value-noise (no texture asset needed) for the ember/crack look.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv   = i.texcoord - 0.5;
                float  dist = length(uv) * 2.0; // 0 at center, ~1 at the inscribed circle edge

                // Expanding ignition ring driven by _Progress (slight overshoot so it
                // fully exits the node rather than stopping right at the edge).
                float ringCenter = _Progress * 1.3;
                float ring = 1.0 - saturate(abs(dist - ringCenter) / _RingWidth);
                ring = pow(ring, 2.0);

                // Procedural ember/crack texture riding along the ring.
                float crack = valueNoise(uv * _NoiseScale + _Progress * 3.0);
                crack = pow(crack, 2.0);

                // Overall fade as the burst completes.
                float fade = saturate(1.0 - _Progress);

                float intensity = ring * (0.5 + 0.5 * crack) * fade;

                fixed4 col = lerp(_EdgeColor, _CoreColor, saturate(crack + (1.0 - dist)));
                col.a    = intensity;
                col.rgb *= intensity; // premultiplied, matches the additive blend above

                return col * i.color;
            }
            ENDCG
        }
    }
}
