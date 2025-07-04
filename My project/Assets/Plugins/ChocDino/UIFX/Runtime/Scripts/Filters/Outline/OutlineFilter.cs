﻿//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	/// <summary></summary>
	public enum OutlineMethod
	{
		/// <summary></summary>
		DistanceMap,
		/// <summary></summary>
		Dilate,
	}

	/// <summary>The direction in which the outline grows from the edge</summary>
	public enum OutlineDirection
	{
		/// <summary>Grow the outline from the edge both inside and outside.</summary>
		Both,
		/// <summary>Grow the outline from the edge only inside.</summary>
		Inside,
		/// <summary>Grow the outline from the edge only outside.</summary>
		Outside,
	}

	/// <summary>
	/// A outline filter for uGUI components
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Outline Filter")]
	public class OutlineFilter : FilterBase
	{
		[Tooltip("The algorithm to use for generating the outline.")]
		[SerializeField] OutlineMethod _method = OutlineMethod.DistanceMap;

		[Tooltip("The radius of the outline in pixels.")]
		[Range(0f, 256f)]
		[SerializeField] float _size = 4f;

		/*[Tooltip("The maximum radius of the outline in pixels.")]
		[Range(0f, 256f)]
		[SerializeField] float _maxSize = 4f;*/

		[Tooltip("The shape that the outline grows in.")]
		[SerializeField] DistanceShape _distanceShape = DistanceShape.Circle;

		[Tooltip("The radius of the blur filter in pixels.")]
		[Range(0f, 8f)]
		[SerializeField] float _blur = 0f;

		[Tooltip("The DistanceMap softness falloff pixels.")]
		[Range(0f, 128f)]
		[SerializeField] float _softness = 2f;

		[Tooltip("The transparency of the source content. Set to zero to make only the outline show.")]
		[Range(0f, 1f)]
		[SerializeField] float _sourceAlpha = 1.0f;

		[Tooltip("The color of the outline.")]
		[SerializeField] Color _color = Color.black;

		//[SerializeReference] GradientShader _gradient = new GradientShader();

		[Tooltip("The texture of the outline.")]
		[SerializeField] Texture _texture;

		[SerializeField] Vector2 _textureOffset = Vector2.zero;
		[SerializeField] Vector2 _textureScale = Vector2.one;

		[Tooltip("The direction in which the outline grows from the edge.")]
		[SerializeField] OutlineDirection _direction = OutlineDirection.Outside;

		/// <summary>The direction in which the outline grows from the edge.</summary>
		public OutlineMethod Method { get { return _method; } set { ChangeProperty(ref _method, value); } }

		/// <summary>The radius of the outline in pixels.</summary>
		public float Size { get { return _size; } set { ChangeProperty(ref _size, value); } }

		/// <summary>The shape that the outline grows in.</summary>
		public DistanceShape DistanceShape { get { return _distanceShape; } set { ChangeProperty(ref _distanceShape, value); } }

		/// <summary>The radius of the blur filter in pixels.</summary>
		public float Blur { get { return _blur; } set { ChangeProperty(ref _blur, value); } }

		/// <summary>The DistanceMap softness falloff in pixels.</summary>
		public float Softness { get { return _softness; } set { ChangeProperty(ref _softness, value); } }

		/// <summary>The transparency of the source content. Set to zero to make only the outline show. Range is [0..1] Default is 1.0</summary>
		public float SourceAlpha { get { return _sourceAlpha; } set { ChangeProperty(ref _sourceAlpha, Mathf.Clamp01(value)); } }

		/// <summary>The color of the outline.</summary>
		public Color Color { get { return _color; } set { ChangeProperty(ref _color,value); } }

		/// <summary>The direction in which the outline grows from the edge.</summary>
		public OutlineDirection Direction { get { return _direction; } set { ChangeProperty(ref _direction, value); } }

		private ITextureBlur _blurfx = null;
		private ErodeDilate _erodeDilate = null;
		private DistanceMap _distanceMap = null;

		private const string DisplayShaderPath = "Hidden/ChocDino/UIFX/Blend-Outline";

		static new class ShaderProp
		{
			public readonly static int SourceAlpha = Shader.PropertyToID("_SourceAlpha");
			public readonly static int OutlineColor = Shader.PropertyToID("_OutlineColor");
			public readonly static int Size = Shader.PropertyToID("_Size");
			//public readonly static int FillTex = Shader.PropertyToID("_FillTex");
		}
		static class ShaderKeyword
		{
			public const string Both = "DIR_BOTH";
			public const string Inside = "DIR_INSIDE";
			public const string Outside = "DIR_OUTSIDE";
			public const string DistanceMap = "DISTANCEMAP";
		}

		protected override string GetDisplayShaderPath()
		{
			return DisplayShaderPath;
		}

		internal override bool CanApplyFilter()
		{
			if (_blurfx == null ) return false;
			if (_erodeDilate == null) return false;
			return base.CanApplyFilter();
		}

		protected override bool DoParametersModifySource()
		{
			if (base.DoParametersModifySource())
			{
				//if (_sourceAlpha < 1f) return true;
				if (_color.a <= 0f) return false;
				if (_method == OutlineMethod.DistanceMap && _size <= 0f) return false;
				if (_method == OutlineMethod.Dilate && _size <= 0f && _blur <= 0f) return false;
				return true;
			}
			return false;
		}

		protected override void OnEnable()
		{
			_blurfx = new BoxBlurReference();
			_erodeDilate = new ErodeDilate();
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
			if (_erodeDilate != null)
			{
				_erodeDilate.FreeResources();
				_erodeDilate = null;
			}
			if (_blurfx != null)
			{
				_blurfx.FreeResources();
				_blurfx = null;
			}
			base.OnDisable();
		}

		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			// OnValidate is called when the scene is saved, which causes the material (fields without properties) to lose their properties, so we force update them here.
			if (_blurfx != null)
			{
				_blurfx.ForceDirty();
			}
			if (_erodeDilate != null)
			{
				_erodeDilate.ForceDirty();
			}
			if (_distanceMap != null)
			{
				_distanceMap.ForceDirty();
			}
			
			base.OnValidate();
		}
		#endif
		
		protected override void GetFilterAdjustSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			if (_method == OutlineMethod.DistanceMap)
			{
				if (_distanceMap != null)
				{
					if (_direction != OutlineDirection.Inside)
					{
						int size = Mathf.CeilToInt(_size * _strength * ResolutionScalingFactor);
						leftDown += new Vector2Int(size, size);
						rightUp += new Vector2Int(size, size);
					}
				}
			}
			else if (_method == OutlineMethod.Dilate)
			{
				if (_blurfx != null && _erodeDilate != null)
				{
					SetupFilterParams();
					_blurfx.AdjustBoundsSize(ref leftDown, ref rightUp);
					_erodeDilate.AdjustBoundsSize(ref leftDown, ref rightUp);
				}
			}

			// NOTE: When the Graphic is a solid color, then we need to add padding otherwise it will keep sampling that texture when
			// the sampling wraps, which leads to incorrect rendering
			if (_direction == OutlineDirection.Inside)
			{
				leftDown += new Vector2Int(1, 1);
				rightUp += new Vector2Int(1, 1);
			}
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			if (this.Strength >= 1f)
			{
				_displayMaterial.SetFloat(ShaderProp.SourceAlpha, _sourceAlpha);
				_displayMaterial.SetColor(ShaderProp.OutlineColor, _color);
			}
			else
			{
				_displayMaterial.SetFloat(ShaderProp.SourceAlpha, Mathf.LerpUnclamped(1f, _sourceAlpha, this.Strength));
				_displayMaterial.SetColor(ShaderProp.OutlineColor, _color);
			}
			switch(_direction)
			{
				case OutlineDirection.Both:
					_displayMaterial.EnableKeyword(ShaderKeyword.Both);
					_displayMaterial.DisableKeyword(ShaderKeyword.Inside);
					_displayMaterial.DisableKeyword(ShaderKeyword.Outside);
					break;
				case OutlineDirection.Inside:
					_displayMaterial.DisableKeyword(ShaderKeyword.Both);
					_displayMaterial.EnableKeyword(ShaderKeyword.Inside);
					_displayMaterial.DisableKeyword(ShaderKeyword.Outside);
					break;
				case OutlineDirection.Outside:
					_displayMaterial.DisableKeyword(ShaderKeyword.Both);
					_displayMaterial.DisableKeyword(ShaderKeyword.Inside);
					_displayMaterial.EnableKeyword(ShaderKeyword.Outside);
					break;
			}

			//_gradient.SetupMaterial(_displayMaterial);
			//_displayMaterial.SetTexture(ShaderProp.FillTex, _texture);
			//_displayMaterial.SetTextureOffset(ShaderProp.FillTex, _textureOffset);
			//_displayMaterial.SetTextureScale(ShaderProp.FillTex, _textureScale);

			if (_method == OutlineMethod.DistanceMap)
			{
				float size = _size * _strength * ResolutionScalingFactor;
				float soft = _softness * _strength * ResolutionScalingFactor;
				_displayMaterial.SetVector(ShaderProp.Size, new Vector2(size, soft));
				_displayMaterial.EnableKeyword(ShaderKeyword.DistanceMap);
			}
			else
			{
				_displayMaterial.DisableKeyword(ShaderKeyword.DistanceMap);
			}
		
			base.SetupDisplayMaterial(source, result);
		}

		private void SetupFilterParams()
		{
			if (_method == OutlineMethod.DistanceMap)
			{
				_distanceMap.DistanceShape = _distanceShape;
				_distanceMap.MaxDistance = Mathf.CeilToInt(_size * _strength * ResolutionScalingFactor * 1.5f + 1f);
				if (_direction == OutlineDirection.Both)
				{
					_distanceMap.Result = DistanceMapResult.InOutMax;
				}
				else if (_direction == OutlineDirection.Inside)
				{
					_distanceMap.Result = DistanceMapResult.Inside;
				}
				else if (_direction == OutlineDirection.Outside)
				{
					_distanceMap.Result = DistanceMapResult.Outside;
				}
			}
			else if (_method == OutlineMethod.Dilate)
			{
				_blurfx.SetBlurSize(_blur * _strength * ResolutionScalingFactor);

				_erodeDilate.AlphaOnly = true;
				_erodeDilate.DistanceShape = _distanceShape;
				if (_direction == OutlineDirection.Both)
				{
					_erodeDilate.UseMultiPassOptimisation = false;
					_erodeDilate.ErodeSize = _size * _strength * ResolutionScalingFactor;
					_erodeDilate.DilateSize = _size * _strength * ResolutionScalingFactor;
				}
				else if (_direction == OutlineDirection.Inside)
				{
					_erodeDilate.UseMultiPassOptimisation = true;
					_erodeDilate.ErodeSize = _size * _strength * ResolutionScalingFactor;
					_erodeDilate.DilateSize = 0f;
				}
				else if (_direction == OutlineDirection.Outside)
				{
					_erodeDilate.UseMultiPassOptimisation = true;
					_erodeDilate.ErodeSize = 0f;
					_erodeDilate.DilateSize = _size * _strength * ResolutionScalingFactor;
				}
			}
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			SetupFilterParams();

			if (_method == OutlineMethod.DistanceMap)
			{
				var distance = _distanceMap.Process(source);
				return distance;
			}
			else if (_method == OutlineMethod.Dilate)
			{
				var dilate = _erodeDilate.Process(source);
				var blurred = _blurfx.Process(dilate);
				if (dilate != blurred)
				{
					_erodeDilate.FreeTextures(); 
				}
				return blurred;
			}
			return source;
		}
	}
}