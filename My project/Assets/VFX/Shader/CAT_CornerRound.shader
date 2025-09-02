Shader "CAT/UI/CornerRound" {
	Properties {
		_MainTex("Texture", 2D) = "white" { }
		_RadiusTL("Top-Left Radius px", Float) = 10
		_RadiusTR("Top-Right Radius px", Float) = 10
		_RadiusBL("Bottom-Left Radius px", Float) = 10
		_RadiusBR("Bottom-Right Radius px", Float) = 10
		_Size("Size px", Float) = 100
		
		// 스텐실 마스크 지원을 위한 프로퍼티 추가
		[HideInInspector] _Stencil ("Stencil ID", Float) = 0
		[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
		[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
		[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
		[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
		[HideInInspector] _ColorMask ("Color Mask", Float) = 15
	}
	SubShader {
		Tags {
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
		}
		Cull Off
		Lighting Off
		// Alpha blending.
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		
		// 마스크와 호환되도록 스텐실 설정 추가
		Stencil {
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}
		Pass {
			// 컬러 마스크 설정 추가
			ColorMask [_ColorMask]
			
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
			float _RadiusTL; // Top-Left
			float _RadiusTR; // Top-Right
			float _RadiusBL; // Bottom-Left
			float _RadiusBR; // Bottom-Right
			float _Size;
			
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				return o;
			}
			
			// 각 꼭지점에 대한 반경을 가져오는 함수
			float GetCornerRadius(float2 pixel)
			{
				// 각 사분면에 따라 다른 반경 값을 반환
				if (pixel.x < 0 && pixel.y < 0) {
					// 왼쪽 하단 (Bottom-Left)
					return _RadiusBL;
				} else if (pixel.x >= 0 && pixel.y < 0) {
					// 오른쪽 하단 (Bottom-Right)
					return _RadiusBR;
				} else if (pixel.x < 0 && pixel.y >= 0) {
					// 왼쪽 상단 (Top-Left)
					return _RadiusTL;
				} else {
					// 오른쪽 상단 (Top-Right)
					return _RadiusTR;
				}
			}
			
			// 하드 엣지용 거리 계산 함수
			float CalculateDistance(float2 pixel, float radius)
			{
				float2 halfSize = float2(_Size, _Size) * 0.5;
				float2 cornerPos;
				
				// 현재 픽셀이 속한 사분면에 따라 코너 위치 계산
				if (pixel.x < 0 && pixel.y < 0) {
					// 왼쪽 하단
					cornerPos = float2(-halfSize.x + radius, -halfSize.y + radius);
				} else if (pixel.x >= 0 && pixel.y < 0) {
					// 오른쪽 하단
					cornerPos = float2(halfSize.x - radius, -halfSize.y + radius);
				} else if (pixel.x < 0 && pixel.y >= 0) {
					// 왼쪽 상단
					cornerPos = float2(-halfSize.x + radius, halfSize.y - radius);
				} else {
					// 오른쪽 상단
					cornerPos = float2(halfSize.x - radius, halfSize.y - radius);
				}
				
				// 레디우스 범위 안에 있는지 확인
				bool inCornerX = abs(pixel.x) > halfSize.x - radius;
				bool inCornerY = abs(pixel.y) > halfSize.y - radius;
				
				// 모서리 영역에 있는 경우
				if (inCornerX && inCornerY) {
					return length(pixel - cornerPos);
				}
				
				// 변 위에 있는 경우
				return 0;
			}
			
			// 하드 엣지 알파 계산
			float CalculateHardAlpha(float2 pixel)
			{
				float2 halfSize = float2(_Size, _Size) * 0.5;
				
				// 사각형 바깥인 경우
				if (abs(pixel.x) > halfSize.x || abs(pixel.y) > halfSize.y) {
					return 0;
				}
				
				float radius = GetCornerRadius(pixel);
				
				// 반경이 0인 경우 직각 처리
				if (radius <= 0) {
					return 1.0;
				}
				
				float dist = CalculateDistance(pixel, radius);
				
				// 모서리 반경보다 거리가 크면 투명 처리
				return dist <= radius ? 1.0 : 0.0;
			}
			

			
			fixed4 frag(v2f i) : SV_Target
			{
				float2 uvInPixel = (i.uv - 0.5) * float2(_Size, _Size);
				
				// 하드 엣지로 라운딩된 알파 계산
				float roundAlpha = CalculateHardAlpha(uvInPixel);
				
				// 텍스처 색상 가져오기
				fixed4 col = tex2D(_MainTex, i.uv) * i.color;
				
				// 원본 알파값과 라운딩 알파값 결합
				float finalAlpha = col.a * roundAlpha;
				col.a = finalAlpha;
				
				return col;
			}
			ENDCG
		}
	}
}