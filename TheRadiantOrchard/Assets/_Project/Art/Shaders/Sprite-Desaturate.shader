// Grey <-> colour for sprites and tilemaps (ENGINEERING_GUIDE 8; ART_TRACKER 2: only the colour art is painted).
// URP 2D unlit sprite shader, based on URP's Sprite-Unlit-Default, plus:
//   _Saturation   0 = grey (linear-light luminance), 1 = the sprite's own colour
//   _Darken       dims the grey part only ("withered"), 0..1
//   sweep         pixels within _SweepHeight of _SweepOrigin along _SweepDirection (world space) are in full colour,
//                 with a _SweepSoftness edge; one sweep can cross several sprites (a visitor's body and head, a zone).
// Values are set per renderer with a MaterialPropertyBlock (Presentation/Desaturation), never by copying the material.
Shader "Radiant Orchard/Sprite Desaturate"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Darken ("Darken (grey part)", Range(0, 1)) = 0
        _SweepOrigin ("Sweep Origin (world xy)", Vector) = (0, 0, 0, 0)
        _SweepDirection ("Sweep Direction (world xy)", Vector) = (0, 1, 0, 0)
        _SweepHeight ("Sweep Height (world units, <0 = none)", Float) = -1
        _SweepSoftness ("Sweep Softness (world units)", Float) = 0.15

        // Legacy properties, as in URP's sprite shaders.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex DesaturateVertex
            #pragma fragment DesaturateFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 sweepWS : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // Same layout for every variant so the SRP batcher can use it.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Saturation;
                half _Darken;
                float4 _SweepOrigin;
                float4 _SweepDirection;
                float _SweepHeight;
                float _SweepSoftness;
            CBUFFER_END

            Varyings DesaturateVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.sweepWS = TransformObjectToWorld(input.positionOS).xy;
                return o;
            }

            half4 DesaturateFragment(Varyings input) : SV_Target
            {
                half4 colour = CommonUnlitFragment(input, input.color);

                // How far this pixel is into the sweep: 1 behind the front, 0 ahead of it.
                float along = dot(input.sweepWS - _SweepOrigin.xy, _SweepDirection.xy);
                float edge = max(_SweepSoftness, 0.0001);
                half swept = _SweepHeight < 0 ? 0 : 1 - smoothstep(_SweepHeight - edge, _SweepHeight, along);
                half saturation = max(_Saturation, swept);

                half grey = dot(colour.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 greyColour = grey * (1 - _Darken);
                colour.rgb = lerp(greyColour, colour.rgb, saturation);
                return colour;
            }
            ENDHLSL
        }
    }
}
