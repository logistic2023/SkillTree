// Arcane shimmer for a node's "mystery" state — a slow diagonal sweep and drifting
// mist over a dark violet base, suggesting something unknown/unrevealed rather than
// a generic loading-placeholder sheen. Samples the existing overlay sprite's alpha
// as a shape mask (e.g. a "?" glyph or frame), so it works with whatever art is
// already assigned under SkillNodeButton.mysteryOverlay — animates color on top of
// it, no new art needed. Reuses the same procedural-noise approach as EmberBurn for
// a visually consistent shader family.
Shader "JollyLlama/UI/MysteryShimmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor    ("Base Color",    Color) = (0.16, 0.08, 0.22, 0.9)
        _ShimmerColor ("Shimmer Color", Color) = (0.55, 0.35, 0.85, 1)
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.35
        _ShimmerWidth ("Shimmer Width", Range(0.02, 1)) = 0.25
        _MistScale    ("Mist Noise Scale", Float) = 6
        _MistStrength ("Mist Strength", Range(0,1)) = 0.35

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
        Blend SrcAlpha OneMinusSrcAlpha
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

            sampler2D _MainTex;
            fixed4 _BaseColor;
            fixed4 _ShimmerColor;
            float  _ShimmerSpeed;
            float  _ShimmerWidth;
            float  _MistScale;
            float  _MistStrength;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color    = v.color;
                return o;
            }

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
                fixed4 tex = tex2D(_MainTex, i.texcoord);

                // Diagonal shimmer sweep, looping over time across the sprite's own UVs.
                float diag    = (i.texcoord.x + i.texcoord.y) * 0.5;
                float sweep   = frac(diag - _Time.y * _ShimmerSpeed);
                float shimmer = 1.0 - saturate(abs(sweep - 0.5) / _ShimmerWidth);
                shimmer = pow(shimmer, 2.0);

                // Slow-drifting mist noise for texture, on top of the sweep.
                float mist = valueNoise(i.texcoord * _MistScale + _Time.y * 0.05);

                fixed4 col = lerp(_BaseColor, _ShimmerColor, saturate(shimmer + mist * _MistStrength * 0.5));
                col.a = tex.a * _BaseColor.a;

                return col * i.color;
            }
            ENDCG
        }
    }
}
