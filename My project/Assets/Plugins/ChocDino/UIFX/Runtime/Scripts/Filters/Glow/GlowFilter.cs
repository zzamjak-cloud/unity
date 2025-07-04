﻿//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	/// <summary>Which side of an edge to use for applying effects</summary>
	public enum EdgeSide
	{
		/// <summary>Both inside and outside the edge.</summary>
		Both,
		/// <summary>Inside the edge.</summary>
		Inside,
		/// <summary>Outside the edge.</summary>
		Outside,
	}

	public enum GlowFalloffMode
	{
		Exponential,
		Curve,
	}

	public enum GlowFillMode
	{
		Color,
		Texture,
		Gradient,
	}

	/// <summary>
	/// A glow filter for uGUI components
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Glow Filter")]
	public class GlowFilter : FilterBase
	{
		[Tooltip("Which side of the edge to use for the glow.")]
		[SerializeField] EdgeSide _edgeSide = EdgeSide.Both;

		[Tooltip("The shape that the glow grows in.")]
		[SerializeField] DistanceShape _distanceShape = DistanceShape.Circle;

		[Tooltip("Optionally limit the distance the glow can fill. This improves performance as large glows are expensive. Value 0.0 means no limit.")]
		[Range(0f, 1024f)]
		[SerializeField] float _maxDistance = 128f;

		[Tooltip("The distance map try not to regenerate (expensive). This requires Blur == 0. Changing distancemap settings, or changing the size of the source graphic willl cause it to regenerte.")]
		[SerializeField] bool _reuseDistanceMap = false;

		[Tooltip("")]
		[SerializeField] GlowFalloffMode _falloffMode = GlowFalloffMode.Exponential;

		[SerializeField, Range(1f, 16f)] float _expFalloffEnergy = 4f;
		[SerializeField, Range(1f, 8f)] float _expFalloffPower = 2f;
		[SerializeField] float _expFalloffOffset = 0f;

		[Tooltip("")]
		[SerializeField] AnimationCurve _falloffCurve = new AnimationCurve(new Keyframe(0f, 1f, -1f, -1f), new Keyframe(1f, 0f, -1f, -1f));

		[SerializeField, Range(0.01f, 10f)] float _falloffCurveGamma = 2.2f;

		[Tooltip("")]
		[SerializeField] GlowFillMode _fillMode = GlowFillMode.Color;

		[Tooltip("The color of the glow.")]
		[SerializeField, ColorUsage(true, true)] Color _color = Color.white;

		[Tooltip("The texture to use as a gradient to color the distance from the edge.")]
		[SerializeField] Texture _gradientTexture = null;

		[Tooltip("The gradient to color the distance from the edge.")]
		[SerializeField	, GradientUsage(true)] Gradient _gradient = ColorUtils.GetBuiltInGradient(BuiltInGradient.SoftRainbow);

		[SerializeField, Range(-1f, 1f)] float _gradientOffset = 0f;
		[SerializeField, Range(0f, 10f)] float _gradientGamma = 1f;

		[Tooltip("Reverse the fill direction.")]
		[SerializeField] bool _gradientReverse = false;

		[Tooltip("The radius of the blur filter in pixels.")]
		[Range(0f, 28f)]
		[SerializeField] float _blur = 1f;

		[Tooltip("How additive to make the glow. Zero will apply an alpha blend, One will apply an additive blend.")]
		[SerializeField, Range(0f, 1f)] float _additive = 1f;

		[Tooltip("The transparency of the source content. Set to zero to make only the glow show.")]
		[Range(0f, 1f)]
		[SerializeField] float _sourceAlpha = 1.0f;

		/// <summary>Which side of the edge to use for the glow.</summary>
		public EdgeSide EdgeSide { get { return _edgeSide; } set { ChangeProperty(ref _edgeSide, value); } }

		/// <summary>The shape that the glow grows in.</summary>
		public DistanceShape DistanceShape { get { return _distanceShape; } set { ChangeProperty(ref _distanceShape, value); } }

		/// <summary></summary>
		public float MaxDistance { get { return _maxDistance; } set { ChangeProperty(ref _maxDistance, Mathf.Clamp(value, 0f, 1024f)); } }

		/// <summary>The distance map try not to regenerate (expensive). This requires Blur == 0. Changing distancemap settings, or changing the size of the source graphic willl cause it to regenerte.</summary>
		public bool ReuseDistanceMap { get { return _reuseDistanceMap; } set { ChangeProperty(ref _reuseDistanceMap, value); } }

		/// <summary></summary>
		public GlowFalloffMode FalloffMode { get { return _falloffMode; } set { ChangeProperty(ref _falloffMode, value); } }

		/// <summary></summary>
		public float ExpFalloffEnergy { get { return _expFalloffEnergy; } set { ChangeProperty(ref _expFalloffEnergy, Mathf.Clamp(value, 1f, 16f)); } }

		/// <summary></summary>
		public float ExpFalloffPower { get { return _expFalloffPower; } set { ChangeProperty(ref _expFalloffPower, Mathf.Clamp(value, 1f, 8f)); } }

		/// <summary></summary>
		public float ExpFalloffOffset { get { return _expFalloffOffset; } set { ChangeProperty(ref _expFalloffOffset, value); } }

		/// <summary></summary>
		public AnimationCurve FalloffCurve { get { return _falloffCurve; } set { ChangePropertyRef(ref _falloffCurve, value); } }

		/// <summary></summary>
		public float FalloffCurveGamma { get { return _falloffCurveGamma; } set { ChangeProperty(ref _falloffCurveGamma, Mathf.Clamp(value, 0.01f, 10f)); } }

		/// <summary></summary>
		public GlowFillMode FillMode { get { return _fillMode; } set { ChangeProperty(ref _fillMode, value); } }

		/// <summary>The color of the glow.</summary>
		public Color Color { get { return _color; } set { ChangeProperty(ref _color,value); } }

		/// <summary>The texture to use as a gradient to color the distance from the edge.</summary>
		public Texture GradientTexture { get { return _gradientTexture; } set { ChangePropertyRef(ref _gradientTexture, value); } }

		/// <summary>The gradient to color the distance from the edge.</summary>
		public Gradient Gradient { get { return _gradient; } set { ChangePropertyRef(ref _gradient, value); } }

		/// <summary></summary>
		public float GradientOffset { get { return _gradientOffset; } set { ChangeProperty(ref _gradientOffset, Mathf.Clamp(value, -1f, 1f)); } }

		/// <summary></summary>
		public float GradientGamma { get { return _gradientGamma; } set { ChangeProperty(ref _gradientGamma, Mathf.Clamp(value, 0f, 10f)); } }

		/// <summary></summary>
		public bool GradientReverse { get { return _gradientReverse; } set { ChangeProperty(ref _gradientReverse, value); } }

		/// <summary>The radius of the blur filter in pixels.</summary>
		public float Blur { get { return _blur; } set { ChangeProperty(ref _blur, Mathf.Clamp(value, 0f, 28f)); } }

		/// <summary>How additive to make the glow. Zero will apply an alpha blend, One will apply an additive blend.</summary>
		public float Additive { get { return _additive; } set { ChangeProperty(ref _additive, Mathf.Clamp01(value)); } }

		/// <summary>The transparency of the source content. Set to zero to make only the glow show. Range is [0..1] Default is 1.0</summary>
		public float SourceAlpha { get { return _sourceAlpha; } set { ChangeProperty(ref _sourceAlpha, Mathf.Clamp01(value)); } }

		private GradientTexture _textureFromGradient = new GradientTexture(128);
		private GradientTexture _textureFromCurve = new GradientTexture(128);
		private ITextureBlur _blurfx = null;
		private DistanceMap _distanceMap = null;
		private RenderTexture _cachedDistanceMap;
		private float _cachedBlurSize = -1f;
		private FilterRenderSpace _cachedRenderSpace;

		private const string BlendGlowShaderPath = "Hidden/ChocDino/UIFX/Blend-Glow";

		static new class ShaderProp
		{
			public readonly static int MaxDistance = Shader.PropertyToID("_MaxDistance");
			public readonly static int FalloffParams = Shader.PropertyToID("_FalloffParams");
			public readonly static int FalloffTex = Shader.PropertyToID("_FalloffTex");
			public readonly static int GlowColor = Shader.PropertyToID("_GlowColor");
			public readonly static int GradientTex = Shader.PropertyToID("_GradientTex");
			public readonly static int GradientParams = Shader.PropertyToID("_GradientParams");
			public readonly static int AdditiveFactor = Shader.PropertyToID("_AdditiveFactor");
			public readonly static int SourceAlpha = Shader.PropertyToID("_SourceAlpha");
		}
		static class ShaderKeyword
		{
			public const string Both = "DIR_BOTH";
			public const string Inside = "DIR_INSIDE";
			public const string Outside = "DIR_OUTSIDE";
			public const string UseGradientTexture = "USE_GRADIENT_TEXTURE";
			public const string UseCurveFalloff = "USE_CURVE_FALLOFF";
		}

		protected override string GetDisplayShaderPath()
		{
			return BlendGlowShaderPath;
		}

		internal override bool CanApplyFilter()
		{
			if (_blurfx == null ) return false;
			return base.CanApplyFilter();
		}

		protected override bool DoParametersModifySource()
		{
			if (base.DoParametersModifySource())
			{
				if (_fillMode == GlowFillMode.Color && _color.a <= 0f) return false;
				if (_maxDistance <= 0f) return false;
				return true;
			}
			return false;
		}

		protected override void OnEnable()
		{
			_rectAdjustOptions.clampToScreen = (_renderSpace == FilterRenderSpace.Screen);
			_textureFromGradient = new GradientTexture(128);
			_textureFromCurve = new GradientTexture(128);
			_blurfx = new BoxBlurReference();
			_distanceMap = new DistanceMap();
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			if (_distanceMap != null)
			{
				_distanceMap.FreeResources();
				_distanceMap = null;
			}
			if (_blurfx != null)
			{
				_blurfx.FreeResources();
				_blurfx = null;
			}
			if (_textureFromCurve != null)
			{
				_textureFromCurve.Dispose();
				_textureFromCurve = null;
			}
			if (_textureFromGradient != null)
			{
				_textureFromGradient.Dispose();
				_textureFromGradient = null;
			}

			_cachedDistanceMap = null;
			_cachedBlurSize = -1f;

			base.OnDisable();
		}

		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			// OnValidate is called when the scene is saved, which causes the material (fields without properties) to lose their properties, so we force update them here.
			if (_blurfx != null)
			{
				//_blurfx.ForceDirty();
			}
			if (_distanceMap != null)
			{
				//_distanceMap.ForceDirty();
			}

			base.OnValidate();
		}
		#endif
		
		protected override void GetFilterAdjustSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			_rectAdjustOptions.clampToScreen = (_renderSpace == FilterRenderSpace.Screen);

			if (_edgeSide != EdgeSide.Inside)
			{
				int s = Mathf.CeilToInt(GetMaxGlowDistance());
				leftDown += new Vector2Int(s, s);
				rightUp += new Vector2Int(s, s);

				if (_blurfx != null)
				{
					SetupFilterParams();
					_blurfx.AdjustBoundsSize(ref leftDown, ref rightUp);
				}
			}
		}

		private float GetMaxGlowDistance()
		{
			float maxDist = 4096f;

			float strength = _strength;
			if (_reuseDistanceMap)
			{
				strength = 1f;
			}
			float resolutionScale = ResolutionScalingFactor;//(_renderSpace == FilterRenderSpace.Canvas ? 1f : ResolutionScalingFactor);

			if (_falloffMode == GlowFalloffMode.Exponential)
			{
				float smallestVisibleLinearBrightness = 2048f;
				float glowStrength = Mathf.LerpUnclamped(0f, _expFalloffEnergy * resolutionScale, strength);
				float glowPower = _expFalloffPower;//Mathf.LerpUnclamped(1f, _glowPower, strength);
				float glowOffset = _expFalloffOffset * resolutionScale;
			
				maxDist = glowStrength / Mathf.Pow(1.0f / smallestVisibleLinearBrightness, 1f / glowPower);
				maxDist *= strength;
				maxDist += Mathf.Max(0f, glowOffset);
			}

			if (_maxDistance > 0f)
			{
				maxDist = Mathf.Min(maxDist, _maxDistance * resolutionScale);
				if (_falloffMode != GlowFalloffMode.Exponential)
				{
					maxDist *= strength;
				}
			}

			return maxDist;
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			if (!_displayMaterial) { return; }

			_displayMaterial.SetFloat(ShaderProp.SourceAlpha, Mathf.LerpUnclamped(1f, _sourceAlpha, _strength));
			_displayMaterial.SetFloat(ShaderProp.AdditiveFactor, _additive);

			// In Exponential mode the maxDistance is calculated, but for Curve mode we want to scale by the strength
			if (_falloffMode == GlowFalloffMode.Exponential)
			{
				_displayMaterial.SetFloat(ShaderProp.MaxDistance, Mathf.Max(0f, _maxDistance) * ResolutionScalingFactor);
			}
			else
			{
				_displayMaterial.SetFloat(ShaderProp.MaxDistance, Mathf.Max(0f, _maxDistance) * ResolutionScalingFactor * _strength);
			}

			switch(_edgeSide)
			{
				case EdgeSide.Both:
					_displayMaterial.EnableKeyword(ShaderKeyword.Both);
					_displayMaterial.DisableKeyword(ShaderKeyword.Inside);
					_displayMaterial.DisableKeyword(ShaderKeyword.Outside);
					break;
				case EdgeSide.Inside:
					_displayMaterial.DisableKeyword(ShaderKeyword.Both);
					_displayMaterial.EnableKeyword(ShaderKeyword.Inside);
					_displayMaterial.DisableKeyword(ShaderKeyword.Outside);
					break;
				case EdgeSide.Outside:
					_displayMaterial.DisableKeyword(ShaderKeyword.Both);
					_displayMaterial.DisableKeyword(ShaderKeyword.Inside);
					_displayMaterial.EnableKeyword(ShaderKeyword.Outside);
					break;
			}

			_textureFromCurve.Update(_falloffCurve);
			_displayMaterial.SetTexture(ShaderProp.FalloffTex, _textureFromCurve.Texture);

			switch (_falloffMode)
			{
				case GlowFalloffMode.Exponential:
				{
					_displayMaterial.DisableKeyword(ShaderKeyword.UseCurveFalloff);
					float expStrength = Mathf.LerpUnclamped(0f, _expFalloffEnergy * ResolutionScalingFactor, _strength);
					float expPower = _expFalloffPower;
					float expOffset = _expFalloffOffset * ResolutionScalingFactor;
					_displayMaterial.SetVector(ShaderProp.FalloffParams, new Vector4(expStrength, expPower, expOffset, 0f));
				}
				break;
				case GlowFalloffMode.Curve:
				_displayMaterial.EnableKeyword(ShaderKeyword.UseCurveFalloff);
				_displayMaterial.SetVector(ShaderProp.FalloffParams, new Vector4(_falloffCurveGamma, _falloffCurveGamma, _falloffCurveGamma, _falloffCurveGamma));
				break;
			}

			switch (_fillMode)
			{
				case GlowFillMode.Color:
				{
					_displayMaterial.DisableKeyword(ShaderKeyword.UseGradientTexture);
					Color premultiplied = _color;
					premultiplied.r *= premultiplied.a;
					premultiplied.g *= premultiplied.a;
					premultiplied.b *= premultiplied.a;
					_displayMaterial.SetColor(ShaderProp.GlowColor, Color.LerpUnclamped(Color.clear, premultiplied, _strength));
				}
				break;
				case GlowFillMode.Texture:
				_displayMaterial.EnableKeyword(ShaderKeyword.UseGradientTexture);
				_displayMaterial.SetTexture(ShaderProp.GradientTex, _gradientTexture);
				_displayMaterial.SetVector(ShaderProp.GradientParams, new Vector4(_gradientOffset, _gradientGamma, _gradientReverse ? 1f: 0f, 0f));
				
				break;
				case GlowFillMode.Gradient:
				_textureFromGradient.Update(_gradient);
				_displayMaterial.EnableKeyword(ShaderKeyword.UseGradientTexture);
				_displayMaterial.SetTexture(ShaderProp.GradientTex, _textureFromGradient.Texture);
				_displayMaterial.SetVector(ShaderProp.GradientParams, new Vector4(_gradientOffset, _gradientGamma, _gradientReverse ? 1f: 0f, 0f));
				break;
			}

			base.SetupDisplayMaterial(source, result);
		}

		private void SetupFilterParams()
		{
			_distanceMap.DistanceShape = _distanceShape;
			_distanceMap.MaxDistance = Mathf.CeilToInt(GetMaxGlowDistance() * 1.5f + 1f);
			if (_edgeSide == EdgeSide.Both)
			{
				_distanceMap.Result = DistanceMapResult.SDF;
			}
			else if (_edgeSide == EdgeSide.Inside)
			{
				_distanceMap.Result = DistanceMapResult.Inside;
			}
			else if (_edgeSide == EdgeSide.Outside)
			{
				_distanceMap.Result = DistanceMapResult.Outside;
			}
			_blurfx.SetBlurSize(_blur * _strength * ResolutionScalingFactor);
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			SetupFilterParams();

			float blurFactor = (_blur * _strength * ResolutionScalingFactor);
			if (!_reuseDistanceMap || _cachedDistanceMap == null || _distanceMap.IsMaterialDirty() || _cachedBlurSize != blurFactor || _cachedRenderSpace != _renderSpace)
			{
				_cachedBlurSize = blurFactor;
				_cachedRenderSpace = _renderSpace;
				{
					var distance = _distanceMap.Process(source);
					var blurred = _blurfx.Process(distance);
					if (distance != blurred)
					{
						_distanceMap.FreeTextures();
					}
					_cachedDistanceMap = blurred;
				}
			}

			return _cachedDistanceMap;
		}
	}
}