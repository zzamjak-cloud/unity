// 픽셀 아트 다중 색상 변환 셰이더
Shader "CAT/Effects/PixelArtMultiColorReplacer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tolerance ("Color Tolerance", Range(0, 1)) = 0.01
        
        // 10개의 색상 쌍 정의
        _SourceColor1 ("Source Color 1", Color) = (1,1,1,1)
        _TargetColor1 ("Target Color 1", Color) = (1,0,0,1)
        _SourceColor2 ("Source Color 2", Color) = (1,1,1,1)
        _TargetColor2 ("Target Color 2", Color) = (1,0,0,1)
        _SourceColor3 ("Source Color 3", Color) = (1,1,1,1)
        _TargetColor3 ("Target Color 3", Color) = (1,0,0,1)
        _SourceColor4 ("Source Color 4", Color) = (1,1,1,1)
        _TargetColor4 ("Target Color 4", Color) = (1,0,0,1)
        _SourceColor5 ("Source Color 5", Color) = (1,1,1,1)
        _TargetColor5 ("Target Color 5", Color) = (1,0,0,1)
        _SourceColor6 ("Source Color 6", Color) = (1,1,1,1)
        _TargetColor6 ("Target Color 6", Color) = (1,0,0,1)
        _SourceColor7 ("Source Color 7", Color) = (1,1,1,1)
        _TargetColor7 ("Target Color 7", Color) = (1,0,0,1)
        _SourceColor8 ("Source Color 8", Color) = (1,1,1,1)
        _TargetColor8 ("Target Color 8", Color) = (1,0,0,1)
        _SourceColor9 ("Source Color 9", Color) = (1,1,1,1)
        _TargetColor9 ("Target Color 9", Color) = (1,0,0,1)
        _SourceColor10 ("Source Color 10", Color) = (1,1,1,1)
        _TargetColor10 ("Target Color 10", Color) = (1,0,0,1)
        
        // 색상 교체 활성화 여부 (0 = 비활성화, 1 = 활성화)
        _ColorEnabled1 ("Enable Color 1", Range(0, 1)) = 1
        _ColorEnabled2 ("Enable Color 2", Range(0, 1)) = 0
        _ColorEnabled3 ("Enable Color 3", Range(0, 1)) = 0
        _ColorEnabled4 ("Enable Color 4", Range(0, 1)) = 0
        _ColorEnabled5 ("Enable Color 5", Range(0, 1)) = 0
        _ColorEnabled6 ("Enable Color 6", Range(0, 1)) = 0
        _ColorEnabled7 ("Enable Color 7", Range(0, 1)) = 0
        _ColorEnabled8 ("Enable Color 8", Range(0, 1)) = 0
        _ColorEnabled9 ("Enable Color 9", Range(0, 1)) = 0
        _ColorEnabled10 ("Enable Color 10", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Tolerance;
            
            // 10개의 색상 쌍 변수 선언
            float4 _SourceColor1, _TargetColor1;
            float4 _SourceColor2, _TargetColor2;
            float4 _SourceColor3, _TargetColor3;
            float4 _SourceColor4, _TargetColor4;
            float4 _SourceColor5, _TargetColor5;
            float4 _SourceColor6, _TargetColor6;
            float4 _SourceColor7, _TargetColor7;
            float4 _SourceColor8, _TargetColor8;
            float4 _SourceColor9, _TargetColor9;
            float4 _SourceColor10, _TargetColor10;
            
            float _ColorEnabled1, _ColorEnabled2, _ColorEnabled3, _ColorEnabled4, _ColorEnabled5;
            float _ColorEnabled6, _ColorEnabled7, _ColorEnabled8, _ColorEnabled9, _ColorEnabled10;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            
            // 색상 교체 함수
            bool checkAndReplaceColor(inout fixed4 col, float4 sourceColor, float4 targetColor, float enabled)
            {
                if (enabled < 0.5) return false;
                
                float3 pixelRGB = col.rgb;
                float3 sourceRGB = sourceColor.rgb;
                float distance = length(sourceRGB - pixelRGB);
                
                if (distance < _Tolerance)
                {
                    col.rgb = targetColor.rgb;
                    return true;
                }
                return false;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 텍스쳐에서 색상 가져오기
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // 색상이 변경되었는지 추적
                bool colorChanged = false;
                
                // 모든 색상 쌍에 대해 교체 시도
                colorChanged = checkAndReplaceColor(col, _SourceColor1, _TargetColor1, _ColorEnabled1) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor2, _TargetColor2, _ColorEnabled2) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor3, _TargetColor3, _ColorEnabled3) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor4, _TargetColor4, _ColorEnabled4) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor5, _TargetColor5, _ColorEnabled5) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor6, _TargetColor6, _ColorEnabled6) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor7, _TargetColor7, _ColorEnabled7) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor8, _TargetColor8, _ColorEnabled8) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor9, _TargetColor9, _ColorEnabled9) || colorChanged;
                colorChanged = checkAndReplaceColor(col, _SourceColor10, _TargetColor10, _ColorEnabled10) || colorChanged;
                
                // SpriteRenderer의 색상 적용
                col *= i.color;
                
                return col;
            }
            ENDCG
        }
    }
    
    CustomEditor "PixelArtMultiColorReplacer"
}