Shader "CAT/Effects/WindEffectUI"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _GradientTex ("Gradient Map (R: X axis influence, G: Y axis influence)", 2D) = "white" {}
        
        _WindStrength ("Wind Strength", Range(0, 0.1)) = 0.02
        _WindSpeed ("Wind Speed", Range(0, 10)) = 1
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1

        // UI용 정렬 속성 추가
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]

        // UI 마스크 지원을 위한 Stencil 설정
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
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"  // UI용 추가
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 rectCoords : TEXCOORD1;  // UI용 추가
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD1;  // UI용 추가
                half4 mask : TEXCOORD2;  // UI용 추가
            };
            
            sampler2D _MainTex;
            sampler2D _NoiseTex;
            //sampler2D _NoiseTex2;
            sampler2D _GradientTex;
            
            float4 _MainTex_ST;
            float _WindStrength;
            float _WindSpeed;
            float _NoiseScale;
            float _SecondaryNoiseScale;
            float _SecondaryStrength;
            
            // UI용 추가
            float4 _ClipRect;
            float4 _MainTex_TexelSize;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;  // UI용 세계 좌표 저장
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                
                // UI 클리핑을 위한 마스크 계산
                o.mask = float4(v.rectCoords.xy, 0, 0);
                
                return o;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // UI 클리핑 적용
                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.worldPosition.xy - (_ClipRect.xy + _ClipRect.zw) * 0.5)) * 10000);
                i.color.a *= m.x * m.y;
                #endif
                
                // 알파값이 너무 낮으면 픽셀 버리기(최적화)
                if (i.color.a < 0.01)
                    return float4(0, 0, 0, 0);
                
                // Time variables for animation
                float time = _Time.y * _WindSpeed;
                
                // Sample the gradient map to control wind influence based on position
                float4 gradientInfluence = tex2D(_GradientTex, i.uv);
                
                // Sample the noise textures for wind movement
                float2 noiseUV = float2(i.uv.x * _NoiseScale + time * 0.1, i.uv.y * _NoiseScale);
                float2 noise = (tex2D(_NoiseTex, noiseUV).rg * 2 - 1) * _WindStrength;
                
                // Apply gradient influence to control wind effect based on position
                noise.x *= gradientInfluence.r;
                noise.y *= gradientInfluence.g;
                
                // Sample the main texture with the offset UV coordinates
                float4 col = tex2D(_MainTex, i.uv + noise);
                
                // Apply vertex color
                col *= i.color;
                
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                
                return col;
            }
            ENDCG
        }
    }
    
    // Fallback for older devices
    Fallback "UI/Default"
}