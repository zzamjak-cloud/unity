Shader "CAT/Effects/Windable"
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
        _ImageScale ("Image Scale", Float) = 1 // 이미지 스케일

        [HideInInspector] _CustomTime ("Custom Time", Float) = 0

        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SpritePivot ("Sprite Pivot", Vector) = (0.5, 0.5, 0, 0)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        ZTest [unity_GUIZTestMode]

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {

                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
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
            float _CustomTime; // _CustomTime 변수 선언

            // ** * 에러의 원인이었던 누락된 vert 함수를 여기에 다시 추가합니다. ** *
            v2f vert (appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
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
                float2 windOffset = normalize(_WindDirection) * windEffect;

                float2 pivot = _SpritePivot.xy;
                float2 centeredUV = rotatedUV - pivot;

                centeredUV += windOffset;
                centeredUV = centeredUV * (1.0 - windEffect * 0.5);
                centeredUV *= _ImageScale;
                centeredUV += pivot;

                // 오프셋 추가
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
                return col * i.color;
            }
            ENDCG
        }
    }
}