Shader "CAT/UI/CornerRoundMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 모서리 반경 설정
        _RadiusTL ("Top-Left Radius", Range(0, 50)) = 10
        _RadiusTR ("Top-Right Radius", Range(0, 50)) = 10
        _RadiusBL ("Bottom-Left Radius", Range(0, 50)) = 10
        _RadiusBR ("Bottom-Right Radius", Range(0, 50)) = 10
        
        // 스텐실 마스크 지원을 위한 프로퍼티 추가
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            // 모서리 반경 설정
            float _RadiusTL;
            float _RadiusTR;
            float _RadiusBL;
            float _RadiusBR;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;
            
            // 각 꼭지점에 대한 반경을 가져오는 함수
            float GetCornerRadius(float2 uv)
            {
                // 각 사분면에 따라 다른 반경 값을 반환
                if (uv.x < 0.5 && uv.y >= 0.5) {
                    // (Top-Left)
                    return _RadiusTL;
                } else if (uv.x >= 0.5 && uv.y >= 0.5) {
                    // (Top-Right)
                    return _RadiusTR;
                } else if (uv.x < 0.5 && uv.y < 0.5) {
                    // (Bottom-Left)
                    return _RadiusBL;
                } else {
                    // (Bottom-Right)
                    return _RadiusBR;
                }
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                // UI 클리핑 영역 적용
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                float2 pos = IN.texcoord - 0.5;
                
                // 각 꼭지점에 대한 반경을 가져옴
                float radius = GetCornerRadius(IN.texcoord) * 0.01; // 0.01은 UV 좌표를 픽셀 단위로 변환하기 위한 스케일링
                
                if (radius > 0) {
                    float2 cornerPos;
                    
                    // 현재 픽셀이 속한 사분면에 따라 코너 위치 계산
                    if (pos.x < 0 && pos.y >= 0) { // 왼쪽 상단
                        cornerPos = float2(-0.5 + radius, 0.5 - radius);
                    } else if (pos.x >= 0 && pos.y >= 0) { // 오른쪽 상단
                        cornerPos = float2(0.5 - radius, 0.5 - radius);
                    } else if (pos.x < 0 && pos.y < 0) { // 왼쪽 하단
                        cornerPos = float2(-0.5 + radius, -0.5 + radius);
                    } else { // 오른쪽 하단
                        cornerPos = float2(0.5 - radius, -0.5 + radius);
                    }
                    
                    // 현재 픽셀이 코너 영역에 속하는지 확인
                    bool inCorner = (abs(pos.x) > 0.5 - radius) && (abs(pos.y) > 0.5 - radius);
                    
                    // 코너 영역에 속하는 경우
                    if (inCorner) {
                        // 현재 픽셀과 코너 위치 사이의 거리 계산
                        float dist = distance(pos, cornerPos);
                        
                        // 코너 영역의 외곽선 거리 계산
                        float outerEdge = radius;
                        
                        if (dist > outerEdge) {
                            // 코너 영역 외곽선 바깥에 있는 경우
                            color.a = 0;
                        }
                    } else if (abs(pos.x) > 0.5 || abs(pos.y) > 0.5) {
                        // 변 위에 있는 경우
                        color.a = 0;
                    }
                }
                
                // 알파 클리핑 적용
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                
                return color;
            }
            ENDCG
        }
    }
    
    CustomEditor "UnityEditor.UI.MaskableGraphicShaderGUI"
    Fallback "UI/Default"
}