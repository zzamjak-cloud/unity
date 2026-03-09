// [OptionalShader] SoftMaskLight: CAT/Effects/Windable
// CAT/Effects/Windable 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
Shader "Hidden/CAT/Effects/Windable (SoftMaskLight)"
{
    Properties
    {
        [HideInInspector]
        _MainTex ("Main Texture", 2D) = "white" {}
        _RotateUV ("Rotate UV", Range(0, 360)) = 0
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _WindSpeed ("Wind Speed", Float) = 0.2

        _WindStrength ("Wind Strength", Float) = 1.0
        _WindFrequency ("Wind Frequency", Float) = 0.2
        _WindDirection ("Wind Direction", Vector) = (1, 1, 0, 0)
        _ClipRect ("Clip Rect", Vector) = (-2147.0, -2147.0, 2147.0, 2147.0)
        _WindScale ("Noise Scale", Float) = 1.0
        _ImageOffsetX ("Image Offset X", Float) = 0.0

        _ImageOffsetY ("Image Offset Y", Float) = 0.0
        _ImageScale ("Image Scale", Float) = 1

        [HideInInspector] _CustomTime ("Custom Time", Float) = 0

        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SpritePivot ("Sprite Pivot", Vector) = (0.5, 0.5, 0, 0)
        [HideInInspector] _NormalizedWindDir ("Normalized Wind Dir", Vector) = (1, 0, 0, 0)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // SoftMaskLight 프로퍼티
        [HideInInspector] _MaskTex ("Mask Texture", 2D) = "white" {}
        [HideInInspector] _Softness ("Softness", Range(0, 1)) = 0.1
        [HideInInspector] _InvertMask ("Invert Mask", Float) = 0
        [HideInInspector] _MaskUVRect ("Mask UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder ("Mask Slice Border", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV ("Mask Slice Inner UV", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskTex2 ("Mask Texture 2", 2D) = "white" {}
        [HideInInspector] _Softness2 ("Softness 2", Range(0, 1)) = 0.1
        [HideInInspector] _InvertMask2 ("Invert Mask 2", Float) = 0
        [HideInInspector] _MaskUVRect2 ("Mask UV Rect 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder2 ("Mask Slice Border 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV2 ("Mask Slice Inner UV 2", Vector) = (0, 0, 1, 1)
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
        LOD 100

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            // _CAT_SOFTMASK multi_compile 없음 (항상 활성)
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE

            // SoftMaskLight 항상 활성화
            #define _CAT_SOFTMASK 1
            #include "../../SoftMaskLight/Shader/SoftMaskLight_Core.cginc"

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                CAT_SOFTMASK_COORDS(2, 3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float _RotateUV;
            float _WindSpeed;
            float _WindStrength;
            float _WindFrequency;
            float2 _WindDirection;
            float4 _ClipRect;
            float _WindScale;
            float _ImageOffsetX;
            float _ImageOffsetY;
            float _ImageScale;
            float4 _SpriteUVRect;
            float4 _SpritePivot;
            float _CustomTime;
            float2 _NormalizedWindDir; // C#에서 사전 계산된 정규화 바람 방향

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                // SoftMaskLight 버텍스 처리 (항상 활성)
                CAT_SOFTMASK_VERT(v.vertex.xyz, o)

                return o;
            }

            float2 RotateUV(float2 uv, float angle, float2 pivot)
            {
                float rad = angle * UNITY_PI / 180.0;
                float cosA = cos(rad);
                float sinA = sin(rad);
                float2x2 rotationMatrix = float2x2(cosA, - sinA, sinA, cosA);
                return mul(rotationMatrix, uv - pivot) + pivot;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 clipTestPoint = i.worldPosition.xy;
                float2 rectMin = _ClipRect.xy;
                float2 rectMax = _ClipRect.zw;
                clip(clipTestPoint.x - rectMin.x);
                clip(rectMax.x - clipTestPoint.x);
                clip(clipTestPoint.y - rectMin.y);
                clip(rectMax.y - clipTestPoint.y);

                float2 spriteCenter = (_SpriteUVRect.xy + _SpriteUVRect.zw) * 0.5;
                float2 rotatedUV = RotateUV(i.uv, _RotateUV, spriteCenter);

                float timeOffset = _CustomTime * _WindSpeed * _WindFrequency;
                float2 noiseSampleUV = (rotatedUV + _WindDirection * timeOffset) * _WindScale;
                float noiseValue = tex2D(_NoiseTex, noiseSampleUV).r;
                float windEffect = noiseValue * _WindStrength * 0.1;
                float2 windOffset = _NormalizedWindDir * windEffect;

                float2 pivot = _SpritePivot.xy;
                float2 centeredUV = rotatedUV - pivot;

                centeredUV += windOffset;
                centeredUV = centeredUV * (1.0 - windEffect * 0.5);
                centeredUV *= _ImageScale;
                centeredUV += pivot;

                centeredUV.x += _ImageOffsetX * - 0.1;
                centeredUV.y += _ImageOffsetY * - 0.1;

                float2 finalUV = centeredUV;

                float2 spriteMinUV = _SpriteUVRect.xy;
                float2 spriteMaxUV = _SpriteUVRect.zw;
                clip(finalUV.x - spriteMinUV.x);
                clip(spriteMaxUV.x - finalUV.x);
                clip(finalUV.y - spriteMinUV.y);
                clip(spriteMaxUV.y - finalUV.y);

                fixed4 col = tex2D(_MainTex, finalUV);
                col *= i.color;

                // SoftMaskLight 적용 (항상 활성)
                col.a *= CAT_SOFTMASK_FRAG(i);

                return col;
            }
            ENDCG
        }
    }
}
