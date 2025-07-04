Shader "Hidden/ChocDino/UIFX/Blend-Fill-Color"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_FillColor    ("Fill Color", Color) = (1, 1, 1, 1)
		_FillColorA  ("Fill Color A", Color) = (1, 1, 1, 1)
		_FillColorB  ("Fill Color B", Color) = (0, 0, 0, 1)
		_FillColorTL  ("Fill Color Top Left", Color) = (1, 1, 1, 1)
		_FillColorTR  ("Fill Color Top Right", Color) = (1, 1, 1, 1)
		_FillColorBL  ("Fill Color Bottom Left", Color) = (0, 0, 0, 1)
		_FillColorBR  ("Fill Color Bottom Right", Color) = (0, 0, 0, 1)
		_ColorScaleBias ("", Vector) = (1, 0, 0, 0)
		_Strength     ("Strength", Float) = 1.0

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local COMP_FG_REPLACE COMP_FG_MULTIPLY COMP_BG_REPLACE COMP_CUTOUT
	#pragma multi_compile_local _ MODE_HORIZONTAL MODE_VERTICAL MODE_CORNERS

	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	uniform half4 _FillColor;
	uniform half4 _FillColorA;
	uniform half4 _FillColorB;
	uniform half4 _FillColorTL;
	uniform half4 _FillColorTR;
	uniform half4 _FillColorBL;
	uniform half4 _FillColorBR;
	uniform half2 _ColorScaleBias;
	uniform half _Strength;

	fixed4 frag(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		fixed4 result = tex2D(_ResultTex, i.uv.xy);

		#if MODE_CORNERS
		fixed4 fill = CornerGradient(_FillColorTL, _FillColorTR, _FillColorBL, _FillColorBR, i.uv.xy, _ColorScaleBias.x);
		#elif MODE_HORIZONTAL
		fixed4 fill = EdgeGradient(_FillColorA, _FillColorB, i.uv.x, _ColorScaleBias.x, _ColorScaleBias.y);
		#elif MODE_VERTICAL
		fixed4 fill = EdgeGradient(_FillColorB, _FillColorA, i.uv.y, _ColorScaleBias.x, 1.0-_ColorScaleBias.y);
		#else
		fixed4 fill = _FillColor;
		#endif

		fill = ToPremultiplied(fill);

		fixed4 color = result;
		#if COMP_CUTOUT
		color = AlphaComp_Out(fill, color);   // background replace, foreground cutout
		#elif COMP_BG_REPLACE
		color = AlphaComp_Over(color, fill);  // background replace
		#elif COMP_FG_REPLACE
		color = AlphaComp_In(fill, color);    // foreground replace
		#elif COMP_FG_MULTIPLY
		color = result * fill;                // foreground multiply
		#endif

		color = lerp(result, color, _Strength);

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		color = ApplyClipRect(color, i.mask);
		#endif

		// Alpha clipping
		#ifdef UNITY_UI_ALPHACLIP
		clip (color.a - 0.001);
		#endif

		color.rgb *= i.color.a;
		color *= i.color;

		return color;
	}

	ENDCG

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
			"OutputsPremultipliedAlpha"="True"
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
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend One OneMinusSrcAlpha // Premultiplied transparency
		ColorMask [_ColorMask]

		Pass
		{
			Name "Blend-Fill-Color"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}