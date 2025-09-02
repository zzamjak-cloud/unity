Shader "CAT/Effects/VerticalFlipSprite"
{
    Properties
    {
        _MainTex ("Texture 1", 2D) = "white" {}
        _SecondTex ("Texture 2", 2D) = "white" {}
        _FlipProgress ("Flip Progress", Range(0, 1)) = 0
        _SliceCount ("Slice Count", Range(1, 50)) = 6
        _FlipDuration ("Flip Duration", Range(0.1, 2.0)) = 0.2
        _FlipOffset ("Flip Offset", Range(0, 1)) = 0.1
        
        // 라인 관련 속성
        [Toggle] _ShowLines ("Show Column Lines", Float) = 1
        _LineColor ("Line Color", Color) = (0, 0, 0, 1)
        _LineWidth ("Line Width", Range(0.001, 0.05)) = 0.05
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" "PreviewType"="Plane" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 모바일 최적화를 위한 프라그마 지시문
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma fragmentoption ARB_precision_hint_fastest
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            sampler2D _SecondTex;
            float4 _MainTex_ST;
            float _FlipProgress;
            float _SliceCount;
            float _FlipDuration;
            float _FlipOffset;
            
            // 라인 관련 변수
            float _ShowLines;
            float4 _LineColor;
            float _LineWidth;
            
            // 최적화된 cos 테이블 (미리 계산된 값)
            static const float COS_TABLE[16] = {
                1.0, 0.9808, 0.9239, 0.8315, 0.7071, 0.5556, 
                0.3827, 0.1951, 0.0, -0.1951, -0.3827, -0.5556, 
                -0.7071, -0.8315, -0.9239, -0.9808
            };
            
            // 최적화된 cos 함수
            float fastCos(float x) {
                // 범위를 0~1로 정규화
                x = frac(x / 6.283185) * 16.0;
                int idx = (int)x;
                float t = frac(x);
                // 선형 보간
                return lerp(COS_TABLE[idx % 16], COS_TABLE[(idx + 1) % 16], t);
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 수직 슬라이스의 인덱스 계산
                float sliceWidth = 1.0 / _SliceCount;
                float sliceIndex = floor(i.uv.x / sliceWidth);
                
                // 각 슬라이스마다 약간의 시간차를 두고 플립 애니메이션 적용
                float delayedProgress = _FlipProgress - (sliceIndex * _FlipOffset);
                delayedProgress = saturate(delayedProgress / _FlipDuration);
                
                // 플립 애니메이션 진행 상태에 따라 UV 좌표 조정
                float2 uv = i.uv;
                float flipAngle = delayedProgress * 3.14159; // 0에서 π까지
                
                fixed4 col;
                
                if (delayedProgress <= 0.0)
                {
                    // 아직 플립되지 않은 상태 - 첫 번째 텍스처 표시
                    col = tex2D(_MainTex, uv);
                }
                else if (delayedProgress >= 1.0)
                {
                    // 플립이 완료된 상태 - 두 번째 텍스처 표시
                    col = tex2D(_SecondTex, uv);
                }
                else
                {
                    // 플립 중인 상태 - 수직 축을 중심으로 회전
                    if (flipAngle < 1.57079) // π/2보다 작으면
                    {
                        // 첫 번째 텍스처가 수직 축을 중심으로 회전하며 사라짐
                        float horizontalStretch = fastCos(flipAngle);
                        float2 adjustedUV = float2((uv.x - 0.5) / horizontalStretch + 0.5, uv.y);
                        
                        // UV 범위를 벗어나면 검은색 처리
                        if (adjustedUV.x >= 0.0 && adjustedUV.x <= 1.0)
                            col = tex2D(_MainTex, adjustedUV);
                        else
                            col = fixed4(0, 0, 0, 1);
                    }
                    else
                    {
                        // 두 번째 텍스처가 수직 축을 중심으로 회전하며 나타남
                        float horizontalStretch = fastCos(3.14159 - flipAngle);
                        float2 adjustedUV = float2((uv.x - 0.5) / horizontalStretch + 0.5, uv.y);
                        
                        // UV 범위를 벗어나면 검은색 처리
                        if (adjustedUV.x >= 0.0 && adjustedUV.x <= 1.0)
                            col = tex2D(_SecondTex, adjustedUV);
                        else
                            col = fixed4(0, 0, 0, 1);
                    }
                }
                
                // 라인 그리기 (컬럼 경계에만)
                if (_ShowLines > 0.5)
                {
                    // 현재 위치가 슬라이스 경계에 충분히 가까운지 확인
                    // frac(i.uv.x / sliceWidth)가 0에 가까우면 슬라이스 경계에 있는 것
                    float distToSliceBoundary = frac(i.uv.x / sliceWidth);
                    
                    // 경계에 가까울 때만 라인 그리기 (0에 가까울 때 또는 1에 가까울 때)
                    if (distToSliceBoundary < _LineWidth || (1.0 - distToSliceBoundary) < _LineWidth)
                    {
                        // 라인과의 거리 계산 (0에 가까울수록 라인에 가까움)
                        float lineDistance = min(distToSliceBoundary, 1.0 - distToSliceBoundary);
                        // 라인 가장자리에서 자연스럽게 블렌딩
                        float blend = saturate(lineDistance / _LineWidth);
                        // 라인 색상과 텍스처 색상 블렌딩
                        col = lerp(_LineColor, col, blend);
                    }
                }
                
                return col;
            }
            ENDCG
        }
    }
    
    // 모바일 장치를 위한 대체 셰이더 (더 단순화된 버전)
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 50
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            sampler2D _SecondTex;
            float _FlipProgress;
            float _SliceCount;
            float _FlipOffset;
            float _ShowLines;
            float4 _LineColor;
            float _LineWidth;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float sliceWidth = 1.0 / _SliceCount;
                float sliceIndex = floor(i.uv.x / sliceWidth);
                float delayedProgress = saturate((_FlipProgress - (sliceIndex * _FlipOffset)) * 2.0);
                
                fixed4 col;
                if (delayedProgress < 0.5)
                    col = tex2D(_MainTex, i.uv);
                else
                    col = tex2D(_SecondTex, i.uv);
                
                // 단순화된 라인 처리 - 경계에만 라인 그리기
                if (_ShowLines > 0.5) {
                    float distToSliceBoundary = frac(i.uv.x / sliceWidth);
                    if (distToSliceBoundary < _LineWidth || (1.0 - distToSliceBoundary) < _LineWidth) {
                        float lineDistance = min(distToSliceBoundary, 1.0 - distToSliceBoundary);
                        float blend = saturate(lineDistance / _LineWidth);
                        col = lerp(_LineColor, col, blend);
                    }
                }
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Unlit/Texture"
}