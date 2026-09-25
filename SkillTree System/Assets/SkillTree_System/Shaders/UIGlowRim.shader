// Pulsing ember rim-glow for a node's "unlockable" state — draws the eye to nodes
// the player can afford/rank up right now. Additive, so it reads as an emissive
// glow against a dark background, consistent with the EmberBurn unlock effect.
// Samples the existing rim sprite's alpha as a shape mask, so it works with
// whatever ring/frame art is already assigned to SkillNodeButton.rimHighlight —
// no new art needed, just animates color/intensity on top of it.
Shader "JollyLlama/UI/GlowRim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor  ("Base Color (dim)",  Color) = (0.9, 0.55, 0.2, 0.6)
        _PulseColor ("Pulse Color (hot)", Color) = (1, 0.92, 0.6, 1)
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _PulseMin   ("Pulse Min", Range(0,1)) = 0.55
        _PulseMax   ("Pulse Max", Range(0,1)) = 1.0

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
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _BaseColor;
            fixed4 _PulseColor;
            float  _PulseSpeed;
            float  _PulseMin;
            float  _PulseMax;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color    = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texcoord);

                // Per-instance phase offset derived from world position, so
                // multiple unlockable nodes on screen don't pulse in lockstep.
                float phase = frac(i.worldPos.x * 0.13 + i.worldPos.y * 0.071) * 6.2831853;
                float pulse = lerp(_PulseMin, _PulseMax, 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + phase));

                fixed4 col = lerp(_BaseColor, _PulseColor, pulse);
                float  alpha = tex.a * pulse;
                col.rgb *= alpha; // premultiplied, matches the additive blend above
                col.a    = alpha;

                return col * i.color;
            }
            ENDCG
        }
    }
}
