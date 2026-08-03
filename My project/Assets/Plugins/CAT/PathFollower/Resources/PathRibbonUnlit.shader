// PathRibbon 전용 Unlit 셰이더 (Sprite 모드 폴백)
//
// URP 의 기본 스프라이트 셰이더(Sprite-Unlit-Default / Sprite-Lit-Default)는
// SpriteRenderer 가 draw 시점에 주입하는 unity_SpriteProps / unity_SpriteColor 내장값에
// 의존하기 때문에 MeshRenderer 로 그리면 정점이 붕괴(×0)되고 알파가 0이 되어 보이지 않는다.
// PathRibbon 은 MeshRenderer 로 리본 메시를 그리므로, 자식 SpriteRenderer 가
// URP 기본 스프라이트 material 을 쓰는 경우 이 셰이더로 자동 대체된다.
//
// - 텍스처: [PerRendererData] _MainTex — MaterialPropertyBlock 으로 주입 (material 공유 유지)
// - 컬러: 정점 컬러 × _Color (리본 정점 컬러에 자식 Color 가 반영되어 있음)
// - 모바일 최적화: half precision, 분기 없음, 단일 텍스처 샘플링
Shader "CAT/PathFollower/Ribbon-Unlit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }
        LOD 100

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            // 정점: 위치 변환 + UV 전달
            // UV 는 메시에 타일링/스크롤 값이 이미 계산되어 있으므로 TRANSFORM_TEX 불필요
            // (스크롤 오프셋으로 0~1 범위를 벗어나므로 float 유지, Wrap=Repeat 텍스처 전제)
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // 프래그먼트: 텍스처 × 정점 컬러 (분기 없음)
            half4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
