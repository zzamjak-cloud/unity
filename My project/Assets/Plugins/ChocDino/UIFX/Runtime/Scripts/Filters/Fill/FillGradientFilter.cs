//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum FillGradientBlendMode
	{
		// Normal
		Replace = 0,
		AlphaBlend,

		// Darken
		//[InspectorName(null)]
		//SectionDarken = 1000,

		Multiply =  10,
		Darken,

		// Lighten
		//[InspectorName(null)]
		//SectionLighten = 2000,

		Lighten = 20,

		// Contrast
		// Inversion
		// Cancelation

		// Component
		//[InspectorName(null)]
		//SectionComponent = 6000,

		[InspectorName("Replace (Alpha)")]
		ReplaceAlpha = 60,
		ReplaceBackground = 61,
	}

	/// <summary>
	/// A visual filter that fills a uGUI component with a gradient.
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Fill Gradient Filter")]
	public class FillGradientFilter : FilterBase
	{
		[Tooltip("The shape of the gradient.")]
		[SerializeField] GradientShape _gradientShape = GradientShape.Horizontal;

		[Tooltip("Flip the diagonal gradient shape vertically.")]
		[SerializeField] bool _diagonalFlip = false;

		[Range(0f, 360f)]
		[Tooltip("The angle for GradientShape.Linear in degrees.")]
		[SerializeField] float _gradientLinearAngle = 0f;

		[Range(-2f, 2f)]
		[Tooltip("The central position GradientShape.Radial and GradientShape.Conic.")]
		[SerializeField] float _gradientCenterX = 0f;

		[Range(-2f, 2f)]
		[Tooltip("The central position GradientShape.Radial and GradientShape.Conic.")]
		[SerializeField] float _gradientCenterY = 0f;

		[Range(0f, 16f)]
		[Tooltip("The radius for GradientShape.Radial.")]
		[SerializeField] float _gradientRadius = 0.5f;

		[Tooltip("The gradient to use")]
		[SerializeField] Gradient _gradient = ColorUtils.GetBuiltInGradient(BuiltInGradient.SoftRainbow);

		[Tooltip("The interpolation mode for the Gradient.")]
		[SerializeField] GradientLerp _gradientLerp = GradientLerp.Smooth;

		[Tooltip("The color-space used when interpolating the Gradient.")]
		[SerializeField] GradientColorSpace _gradientColorSpace = GradientColorSpace.Linear;

		[Tooltip("The wrap mode for sampling the Gradient past the [0..1] range.")]
		[SerializeField] GradientWrap _gradientWrap = GradientWrap.Clamp;

		[Range(0f, 1f)]
		[Tooltip("Optionally dither the gradient interpolation to reduce banding. Default 0.0, Range [0.0..1.0]")]
		[SerializeField] float _gradientDither = 0f;

		[Range(0f, 100f)]
		[Tooltip("Scale the gradient so it repeats multiple times.")]
		[SerializeField] float _gradientScale = 1f;

		[Range(0f, 1f)]
		[Tooltip("The pivot point in gradient where repeat/scaling starts. Default 0.5, Range [0.0..1.0].")]
		[SerializeField] float _gradientScaleCenter = 0.5f;

		[Range(-1f, 1f)]
		[Tooltip("The translation for sampling the gradient. Default 0.0, Range [-1.0..1.0].")]
		[SerializeField] float _gradientOffset = 0f;

		[Tooltip("The speed to scroll the gradient.")]
		[SerializeField] float _scrollSpeed = 0f;

		[Tooltip("How to composite the fill with the source graphic.")]
		[SerializeField] FillGradientBlendMode _blendMode = FillGradientBlendMode.AlphaBlend;

		/// <summary>The shape of the gradient.</summary>
		public GradientShape Shape { get { return _gradientShape; } set { ChangeProperty(ref _gradientShape, value); } }

		/// <summary>Flip the diagonal gradient shape vertically.</summary>
		public bool DiagonalFlip { get { return _diagonalFlip; } set { ChangeProperty(ref _diagonalFlip, value); } }

		/// <summary>The angle for GradientShape.Linear in degrees.</summary>
		public float LinearAngle { get { return _gradientLinearAngle; } set { ChangeProperty(ref _gradientLinearAngle, value); } }

		/// <summary>The central position GradientShape.Radial and GradientShape.Conic.</summary>
		public float CenterX { get { return _gradientCenterX; } set { ChangeProperty(ref _gradientCenterX, value); } }

		/// <summary>The central position GradientShape.Radial and GradientShape.Conic.</summary>
		public float CenterY { get { return _gradientCenterY; } set { ChangeProperty(ref _gradientCenterY, value); } }

		/// <summary>The radius for GradientShape.Radial.</summary>
		public float Radius { get { return _gradientRadius; } set { ChangeProperty(ref _gradientRadius, value); } }

		/// <summary>The gradient to use.</summary>
		public Gradient Gradient { get { return _gradient; } set { ChangePropertyRef(ref _gradient, value); } }

		/// <summary>The interpolation mode for the Gradient.</summary>
		public GradientLerp LerpMode { get { return _gradientLerp; } set { ChangeProperty(ref _gradientLerp, value); } }

		/// <summary>The color-space used when interpolating the Gradient.</summary>
		public GradientColorSpace ColorSpace { get { return _gradientColorSpace; } set { ChangeProperty(ref _gradientColorSpace, value); } }

		/// <summary>The wrap mode for sampling the Gradient past the [0..1] range.</summary>
		public GradientWrap WrapMode { get { return _gradientWrap; } set { ChangeProperty(ref _gradientWrap, value); } }

		/// <summary>Optionally dither the gradient interpolation to reduce banding. Default 0.0, Range [0.0..1.0]</summary>
		public float Dither { get { return _gradientDither; } set { ChangeProperty(ref _gradientDither, value); } }

		/// <summary>Scale the gradient so it repeats multiple times.</summary>
		public float Repeat { get { return _gradientScale; } set { ChangeProperty(ref _gradientScale, value); } }

		/// <summary>The pivot point in gradient where repeat/scaling starts. Default 0.5, Range [0.0..1.0].</summary>
		public float RepeatCenter { get { return _gradientScaleCenter; } set { ChangeProperty(ref _gradientScaleCenter, value); } }

		/// <summary>The translation for sampling the gradient. Default 0.0, Range [-1.0..1.0].</summary>
		public float Offset { get { return _gradientOffset; } set { ChangeProperty(ref _gradientOffset, value); } }

		/// <summary>The speed to scroll the gradient.</summary>
		public float ScrollSpeed { get { return _scrollSpeed; } set { ChangeProperty(ref _scrollSpeed, value); } }

		/// <summary>How to composite the fill with the source graphic.</summary>
		public FillGradientBlendMode BlendMode { get { return _blendMode; } set { ChangeProperty(ref _blendMode, value); } }

		internal bool IsPreviewScroll { get; set; }

		private const int MaxUnityGradientStops = 8;

		private float _scroll = 0f;
		private Vector4[] _colorKeys = new Vector4[MaxUnityGradientStops];
		private Vector4[] _alphaKeys = new Vector4[MaxUnityGradientStops];

		static new class ShaderProp
		{
			public readonly static int GradientColorCount = Shader.PropertyToID("_GradientColorCount");
			public readonly static int GradientAlphaCount = Shader.PropertyToID("_GradientAlphaCount");
			public readonly static int GradientColors = Shader.PropertyToID("_GradientColors");
			public readonly static int GradientAlphas = Shader.PropertyToID("_GradientAlphas");
			public readonly static int GradientTransform = Shader.PropertyToID("_GradientTransform");
			public readonly static int GradientRadial = Shader.PropertyToID("_GradientRadial");
			public readonly static int GradientDither = Shader.PropertyToID("_GradientDither");
			public readonly static int GradientLinearStartLine = Shader.PropertyToID("_GradientLinearStartLine");
			public readonly static int GradientLinearParams = Shader.PropertyToID("_GradientLinearParams");
			public readonly static int GradientAxisParams = Shader.PropertyToID("_GradientAxisParams");
		}
		static class ShaderKeyword
		{
			public const string Dither = "DITHER";

			public const string LerpSmooth = "GRADIENT_LERP_SMOOTH";
			public const string LerpLinear = "GRADIENT_LERP_LINEAR";
			public const string LerpStep = "GRADIENT_LERP_STEP";
			public const string LerpStepAA = "GRADIENT_LERP_STEPAA";

			public const string ColorSpacePerceptual = "GRADIENT_COLORSPACE_PERCEPTUAL";

			public const string ShapeAxis = "GRADIENT_SHAPE_AXIS";
			public const string ShapeQuad = "GRADIENT_SHAPE_QUAD";
			public const string ShapeLinear = "GRADIENT_SHAPE_LINEAR";
			public const string ShapeRadial = "GRADIENT_SHAPE_RADIAL";
			public const string ShapeConic = "GRADIENT_SHAPE_CONIC";

			public const string BlendAlphaBlend = "BLEND_ALPHABLEND";
			public const string BlendMultiply = "BLEND_MULTIPLY";
			public const string BlendDarken = "BLEND_DARKEN";
			public const string BlendLighten = "BLEND_LIGHTEN";
			public const string BlendReplaceAlpha = "BLEND_REPLACE_ALPHA";
			public const string BlendBackground = "BLEND_BACKGROUND";
		}

		private const string BlendShaderPath = "Hidden/ChocDino/UIFX/Blend-Fill-Gradient";

		protected override string GetDisplayShaderPath()
		{
			return BlendShaderPath;
		}

		protected override bool DoParametersModifySource()
		{
			if (!base.DoParametersModifySource())
			{
				return false;
			}
			return true;
		}

		internal bool HasScrollSpeed()
		{
			return _scrollSpeed != 0f;
		}

		public void ResetScroll()
		{
			if (_scroll != 0f)
			{
				_scroll = 0f;
				ForceUpdate();
			}
		}

		protected override void OnEnable()
		{
			_expand = FilterExpand.None;
			ResetScroll();
			base.OnEnable();
		}

		protected override void Update()
		{
			if (HasScrollSpeed()
			#if UNITY_EDITOR
				&& (Application.isPlaying || IsPreviewScroll)
			#endif
			)
			{
				_scroll += _scrollSpeed * Time.deltaTime;
				ForceUpdate();
			}
			base.Update();
		}

		private void GradientToArrays()
		{
			int colorKeyCount = _gradient.colorKeys.Length;
			for (int i = 0; i < colorKeyCount; i++)
			{
				Color c = _gradient.colorKeys[i].color;

				bool isLinear = (_gradientLerp == GradientLerp.Step || _gradientColorSpace != GradientColorSpace.Perceptual);

				if (isLinear)
				{
					if (QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear)
					{ 
						c = c.linear;
					}
					if (_blendMode == FillGradientBlendMode.ReplaceAlpha)
					{
						// When we're replacing only alpha values, the color can be set to a single white key as the blend mode will be set to Multiply
						c = Color.white;
					}
					_colorKeys[i] = new Vector4(c.r, c.g, c.b, _gradient.colorKeys[i].time);
				}
				else if (_gradientColorSpace == GradientColorSpace.Perceptual)
				{
					if (_blendMode == FillGradientBlendMode.ReplaceAlpha)
					{
						// When we're replacing only alpha values, the color can be set to a single white key as the blend mode will be set to Multiply
						c = Color.white;
					}
					Vector3 oklab = ColorUtils.LinearToOklab(c.linear);
					_colorKeys[i] = new Vector4(oklab.x, oklab.y, oklab.z, _gradient.colorKeys[i].time);
				}
				else
				{
					Debug.Assert(false, "No such case..");
				}
			}
				
			int alphaKeyCount = _gradient.alphaKeys.Length;
			for (int i = 0; i < alphaKeyCount; i++)
			{
				_alphaKeys[i] = new Vector4(_gradient.alphaKeys[i].alpha, 0f, 0f, _gradient.alphaKeys[i].time);
			}
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			GradientToArrays();
			_displayMaterial.SetInt(ShaderProp.GradientColorCount, _gradient.colorKeys.Length);
			_displayMaterial.SetInt(ShaderProp.GradientAlphaCount, _gradient.alphaKeys.Length);
			_displayMaterial.SetVectorArray(ShaderProp.GradientColors, _colorKeys);
			_displayMaterial.SetVectorArray(ShaderProp.GradientAlphas, _alphaKeys);

			_displayMaterial.SetVector(ShaderProp.GradientTransform, new Vector4(_gradientScale, _gradientScaleCenter, _gradientOffset + _scroll, (float)_gradientWrap));

			if (_gradientDither > 0f)
			{
				_displayMaterial.EnableKeyword(ShaderKeyword.Dither);
				_displayMaterial.SetFloat(ShaderProp.GradientDither, Mathf.Lerp(0f, 0.05f, _gradientDither));
			}
			else
			{
				_displayMaterial.DisableKeyword(ShaderKeyword.Dither);
			}
			
			_displayMaterial.DisableKeyword(ShaderKeyword.ShapeAxis);
			_displayMaterial.DisableKeyword(ShaderKeyword.ShapeQuad);
			_displayMaterial.DisableKeyword(ShaderKeyword.ShapeLinear);
			_displayMaterial.DisableKeyword(ShaderKeyword.ShapeRadial);
			_displayMaterial.DisableKeyword(ShaderKeyword.ShapeConic);
			switch (_gradientShape)
			{
				case GradientShape.None:
				_displayMaterial.EnableKeyword(ShaderKeyword.ShapeAxis);
				_displayMaterial.SetVector(ShaderProp.GradientAxisParams, Vector4.zero);
				break;
				case GradientShape.Horizontal:
				_displayMaterial.EnableKeyword(ShaderKeyword.ShapeAxis);
				_displayMaterial.SetVector(ShaderProp.GradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
				break;
				case GradientShape.Vertical:
				_displayMaterial.EnableKeyword(ShaderKeyword.ShapeAxis);
				_displayMaterial.SetVector(ShaderProp.GradientAxisParams, new Vector4(0f, -1f, 0f, 1f));
				break;
				case GradientShape.Diagonal:
				_displayMaterial.EnableKeyword(ShaderKeyword.ShapeAxis);
				if (_diagonalFlip)
				{
					_displayMaterial.SetVector(ShaderProp.GradientAxisParams, new Vector4(0.5f, 0.5f, 0f, 0f));
				}
				else
				{
					_displayMaterial.SetVector(ShaderProp.GradientAxisParams, new Vector4(0.5f, -0.5f, 0f, 0.5f));
				}
				break;
				case GradientShape.Linear:
				_displayMaterial.EnableKeyword(ShaderKeyword.ShapeLinear);
				{
					Vector2 uvPointOnStartLine;
					Vector2 uvStartLineDirection;
					float uvGradientLength;
					float uvRectRatio;
					GradientUtils.GetCssLinearGradientShaderParams(_gradientLinearAngle, _screenRect.GetRect(), out uvPointOnStartLine, out uvStartLineDirection, out uvGradientLength, out uvRectRatio);
					_displayMaterial.SetVector(ShaderProp.GradientLinearStartLine, new Vector4(uvPointOnStartLine.x, uvPointOnStartLine.y, uvStartLineDirection.x, uvStartLineDirection.y));
					_displayMaterial.SetVector(ShaderProp.GradientLinearParams, new Vector4(uvGradientLength, uvRectRatio, 0f, 0f));
				}
				break;
				case GradientShape.Radial:
				_displayMaterial.EnableKeyword(ShaderKeyword.ShapeRadial);
				_displayMaterial.SetVector(ShaderProp.GradientRadial, new Vector4(_gradientCenterX, _gradientCenterY, _gradientRadius, 0f));
				break;
				case GradientShape.Conic:
				_displayMaterial.EnableKeyword(ShaderKeyword.ShapeConic);
				_displayMaterial.SetVector(ShaderProp.GradientRadial, new Vector4(_gradientCenterX, _gradientCenterY, _gradientRadius, 0f));
				break;
			}

			switch (_gradientLerp)
			{
				default:
				case GradientLerp.Smooth:
				_displayMaterial.EnableKeyword(ShaderKeyword.LerpSmooth);
				_displayMaterial.DisableKeyword(ShaderKeyword.LerpLinear);
				_displayMaterial.DisableKeyword(ShaderKeyword.LerpStep);
				break;
				case GradientLerp.Linear:
				_displayMaterial.DisableKeyword(ShaderKeyword.LerpSmooth);
				_displayMaterial.EnableKeyword(ShaderKeyword.LerpLinear);
				_displayMaterial.DisableKeyword(ShaderKeyword.LerpStep);
				break;
				case GradientLerp.Step:
				_displayMaterial.DisableKeyword(ShaderKeyword.LerpSmooth);
				_displayMaterial.DisableKeyword(ShaderKeyword.LerpLinear);
				_displayMaterial.EnableKeyword(ShaderKeyword.LerpStep);
				break;
			}

			if (_gradientLerp == GradientLerp.Step)
			{
				_displayMaterial.DisableKeyword(ShaderKeyword.ColorSpacePerceptual);
			}
			else
			{
				switch (_gradientColorSpace)
				{
					default:
					case GradientColorSpace.Linear:
					_displayMaterial.DisableKeyword(ShaderKeyword.ColorSpacePerceptual);
					break;
					case GradientColorSpace.Perceptual:
					_displayMaterial.EnableKeyword(ShaderKeyword.ColorSpacePerceptual);
					break;
				}
			}
			_displayMaterial.DisableKeyword(ShaderKeyword.BlendAlphaBlend);
			_displayMaterial.DisableKeyword(ShaderKeyword.BlendMultiply);
			_displayMaterial.DisableKeyword(ShaderKeyword.BlendDarken);
			_displayMaterial.DisableKeyword(ShaderKeyword.BlendLighten);
			_displayMaterial.DisableKeyword(ShaderKeyword.BlendReplaceAlpha);
			_displayMaterial.DisableKeyword(ShaderKeyword.BlendBackground);
			switch (_blendMode)
			{
				case FillGradientBlendMode.Replace:
				break;
				case FillGradientBlendMode.AlphaBlend:
				_displayMaterial.EnableKeyword(ShaderKeyword.BlendAlphaBlend);
				break;
				case FillGradientBlendMode.Multiply:
				_displayMaterial.EnableKeyword(ShaderKeyword.BlendMultiply);
				break;
				case FillGradientBlendMode.Darken:
				_displayMaterial.EnableKeyword(ShaderKeyword.BlendDarken);
				break;
				case FillGradientBlendMode.Lighten:
				_displayMaterial.EnableKeyword(ShaderKeyword.BlendLighten);
				break;
				case FillGradientBlendMode.ReplaceAlpha:
				_displayMaterial.EnableKeyword(ShaderKeyword.BlendReplaceAlpha);
				break;
				case FillGradientBlendMode.ReplaceBackground:
				_displayMaterial.EnableKeyword(ShaderKeyword.BlendBackground);
				break;
			}

			_displayMaterial.SetFloat(FilterBase.ShaderProp.Strength, _strength);

			base.SetupDisplayMaterial(source, result);
		}
	}
}