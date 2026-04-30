Shader "MainEscape/FogOverlaySoft"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _FeatherRadius ("Feather Radius (Texels)", Range(0, 2)) = 0.85
        _FeatherStrength ("Feather Strength", Range(0, 1)) = 0.72
        _EdgeSensitivity ("Edge Sensitivity", Range(0.5, 12)) = 6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SoftFogFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            float _FeatherRadius;
            float _FeatherStrength;
            float _EdgeSensitivity;
            float4 _MainTex_TexelSize;

            fixed SoftFogSampleAlpha(float2 uv)
            {
                return SampleSpriteTexture(uv).a;
            }

            fixed4 SoftFogFrag(v2f IN) : SV_Target
            {
                fixed centerAlpha = SoftFogSampleAlpha(IN.texcoord);
                float2 texel = _MainTex_TexelSize.xy * _FeatherRadius;

                fixed alphaNorth = SoftFogSampleAlpha(IN.texcoord + float2(0.0, texel.y));
                fixed alphaSouth = SoftFogSampleAlpha(IN.texcoord - float2(0.0, texel.y));
                fixed alphaEast = SoftFogSampleAlpha(IN.texcoord + float2(texel.x, 0.0));
                fixed alphaWest = SoftFogSampleAlpha(IN.texcoord - float2(texel.x, 0.0));
                fixed alphaNorthEast = SoftFogSampleAlpha(IN.texcoord + texel);
                fixed alphaNorthWest = SoftFogSampleAlpha(IN.texcoord + float2(-texel.x, texel.y));
                fixed alphaSouthEast = SoftFogSampleAlpha(IN.texcoord + float2(texel.x, -texel.y));
                fixed alphaSouthWest = SoftFogSampleAlpha(IN.texcoord - texel);

                fixed weightedAverageAlpha =
                    ((centerAlpha * 4.0)
                    + ((alphaNorth + alphaSouth + alphaEast + alphaWest) * 2.0)
                    + alphaNorthEast + alphaNorthWest + alphaSouthEast + alphaSouthWest) / 16.0;

                fixed minAlpha = min(
                    min(centerAlpha, alphaNorth),
                    min(min(alphaSouth, alphaEast), min(alphaWest, min(min(alphaNorthEast, alphaNorthWest), min(alphaSouthEast, alphaSouthWest)))));
                fixed maxAlpha = max(
                    max(centerAlpha, alphaNorth),
                    max(max(alphaSouth, alphaEast), max(alphaWest, max(max(alphaNorthEast, alphaNorthWest), max(alphaSouthEast, alphaSouthWest)))));

                fixed edgeFactor = saturate((maxAlpha - minAlpha) * _EdgeSensitivity);
                fixed finalAlpha = lerp(centerAlpha, weightedAverageAlpha, edgeFactor * _FeatherStrength) * IN.color.a;
                fixed3 finalRgb = IN.color.rgb * finalAlpha;
                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
