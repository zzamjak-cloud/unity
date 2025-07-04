//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	internal class Compositor
	{
		private const string CompositeShaderPath = "Hidden/ChocDino/UIFX/Composite";

		private static class ShaderPass
		{
			internal const int AlphaBlendedToPremultipliedAlpha = 0;
		}

		private RenderTexture _rtRawSource;
		private RenderTexture _composite;
		private Matrix4x4 _projectionMatrix;
		private RenderTexture _prevRT;
		private Material _compositeMaterial;
		private Camera _prevCamera;
		private Matrix4x4 _viewMatrix;

		public bool IsTextureTooLarge { get; private set; }

		public void FreeResources()
		{
			RenderTextureHelper.ReleaseTemporary(ref _composite);
			RenderTextureHelper.ReleaseTemporary(ref _rtRawSource);
			ObjectHelper.Destroy(ref _compositeMaterial);
		}

		public bool Start(Camera camera, RectInt textureRect, float canvasScale = 1f)
		{
			if (_compositeMaterial == null)
			{
				_compositeMaterial = new Material(Shader.Find(CompositeShaderPath));
				Debug.Assert(_compositeMaterial != null);
			}

			RectInt scaledTextureRect = textureRect;

			scaledTextureRect.xMin = Mathf.FloorToInt(textureRect.xMin * canvasScale);
			scaledTextureRect.yMin = Mathf.FloorToInt(textureRect.yMin * canvasScale);
			scaledTextureRect.xMax = Mathf.CeilToInt(textureRect.xMax * canvasScale);
			scaledTextureRect.yMax = Mathf.CeilToInt(textureRect.yMax * canvasScale);
		
			IsTextureTooLarge = false;
			if (scaledTextureRect.width > Filters.GetMaxiumumTextureSize() || scaledTextureRect.height > Filters.GetMaxiumumTextureSize())
			{
				IsTextureTooLarge = true;

				// Modify the texture rectangle so it fits within the maximum supported texture size.
				// NOTE: This will lead to lower image quality.
				{
					float aspect = (float)scaledTextureRect.width / (float)scaledTextureRect.height;
					float origWidth = scaledTextureRect.width;
					float origHeight = scaledTextureRect.height;
					scaledTextureRect.width = Mathf.Min(scaledTextureRect.width, Filters.GetMaxiumumTextureSize());
					scaledTextureRect.height = Mathf.Min(scaledTextureRect.height, Filters.GetMaxiumumTextureSize());

					if (aspect > 1f)
					{
						scaledTextureRect.height = Mathf.CeilToInt(scaledTextureRect.width / aspect);
					}
					else
					{
						scaledTextureRect.width = Mathf.CeilToInt(scaledTextureRect.height / aspect);
					}

					textureRect.width = Mathf.CeilToInt(textureRect.width * (scaledTextureRect.width / origWidth));
					textureRect.height = Mathf.CeilToInt(textureRect.height * (scaledTextureRect.width / origWidth));
				}
			}

			if (_composite != null && (_composite.width != scaledTextureRect.width || _composite.height != scaledTextureRect.height))
			{
				RenderTextureHelper.ReleaseTemporary(ref _composite);
			}

			if (scaledTextureRect.width <= 0 || scaledTextureRect.height <= 0)
			{
				return false;
			}

			if (_composite == null)
			{
				RenderTextureFormat format = RenderTextureFormat.ARGBHalf;
				if ((Filters.PerfHint & PerformanceHint.UseLessPrecision) != 0)
				{
					format = RenderTextureFormat.ARGB32;
				}
				_composite = RenderTexture.GetTemporary(scaledTextureRect.width, scaledTextureRect.height, 0, format, RenderTextureReadWrite.Linear);
				_composite.wrapMode = TextureWrapMode.Clamp;
				#if UNITY_EDITOR
				_composite.name = "RT-Composite" + Time.frameCount;
				#endif
			}

			// Calculate our projection matrix, but cropped to the render area
			{
				if (camera == null)
				{
					// Note: in Overlay canvas mode, the z clip planes seem to be hardcoded to range [-1000f * Canvas.scaleFactor, 1000f * Canvas.scaleFactor]
					_projectionMatrix = Matrix4x4.Ortho(textureRect.xMin, textureRect.xMax, textureRect.yMin, textureRect.yMax, -1000f * canvasScale, 1000f * canvasScale);
				}
				else
				{
					Rect rect = Rect.zero;
					rect.x = textureRect.x;
					rect.y = textureRect.y;
					rect.xMax = textureRect.xMax;
					rect.yMax = textureRect.yMax;
					rect.x -= camera.pixelRect.x;
					rect.y -= camera.pixelRect.y;
					rect.x /= camera.pixelWidth;
					rect.y /= camera.pixelHeight;
					rect.width /= camera.pixelWidth;
					rect.height /= camera.pixelHeight;

					float inverseWidth = 1f / rect.width;
					float inverseHeight = 1f / rect.height;
					Matrix4x4 matrix1 = Matrix4x4.Translate(new Vector3(-rect.x * 2f * inverseWidth, -rect.y * 2f * inverseHeight, 0f));
					Matrix4x4 matrix2 = Matrix4x4.Translate(new Vector3(inverseWidth - 1f, inverseHeight - 1f, 0f)) * Matrix4x4.Scale(new Vector3(inverseWidth, inverseHeight, 1f));

					_projectionMatrix = matrix1 * matrix2 * camera.projectionMatrix;

					//_projectionMatrix = Matrix4x4.Ortho(textureRect.xMin, textureRect.xMax, textureRect.yMin, textureRect.yMax, -1000f, 1000f);
				}
			}

			_prevRT = RenderTexture.active;

			_viewMatrix = Matrix4x4.identity;
			if (camera)
			{
				_viewMatrix = camera.worldToCameraMatrix;
			}
			else
			{
				_viewMatrix = Matrix4x4.TRS(new Vector3(0f, 0f, -10f), Quaternion.identity, Vector3.one);
			}

			// NOTE: Camera.current can be non-null for example when draging the Camera.Size property with the Scene view visible
			// because it is rendering to the scene view.  In this case flickering can occur unless we use the below logic.
			if (Camera.current != null)
			{
				_prevCamera = Camera.current;
				_prevCamera.worldToCameraMatrix = _viewMatrix;
			}

			
			RenderTexture.active = _composite;
			GL.Clear(false, true, Color.clear);

			return true;
		}

		public void End()
		{
			_composite.IncrementUpdateCount();
			
			if (_prevCamera)
			{
				_prevCamera.ResetWorldToCameraMatrix();
				_prevCamera = null;
			}
			RenderTexture.active = _prevRT;
		}

		public void AddMesh(Transform xform, Mesh mesh, Material material, bool materialOutputPremultipliedAlpha)
		{
			if (!mesh || !material) return;

			if (materialOutputPremultipliedAlpha)
			{
				RenderMeshDirectly(xform, mesh, material);
			}
			else
			{
				int pass = ShaderPass.AlphaBlendedToPremultipliedAlpha;
				RenderMeshWithAdjustment(xform, mesh, material, pass);
			}
		}

		private void RenderMeshDirectly(Transform xform, Mesh mesh, Material material)
		{
			RenderTexture.active = _composite;
			RenderMeshToActiveTarget(xform, mesh, material);
		}

		private void RenderMeshWithAdjustment(Transform xform, Mesh mesh, Material material, int pass)
		{
			// If the material doesn't output premultiplied-alpha then first render normally and then
			// blit to convert to premuliplied-alpha and composite it into the _composite buffer
			// NOTE: this assumed a standard alpha blend and doesn't support other blend modes

			Debug.Assert(_rtRawSource == null);
			_rtRawSource = RenderTexture.GetTemporary(_composite.width, _composite.height, 0, _composite.format, RenderTextureReadWrite.Linear);
			#if UNITY_EDITOR
			_rtRawSource.name = "RT-RawSource";
			#endif

			RenderTexture.active = _rtRawSource;
			GL.Clear(false, true, Color.clear);

			RenderMeshToActiveTarget(xform, mesh, material);

			_rtRawSource.IncrementUpdateCount();

			// Blit to fix the alpha channel and composite using pre-multiplied alpha, or another adjustment
			Graphics.Blit(_rtRawSource, _composite, _compositeMaterial, pass);

			RenderTextureHelper.ReleaseTemporary(ref _rtRawSource);
		}

		private void RenderMeshToActiveTarget(Transform xform, Mesh mesh, Material material)
		{
			GL.PushMatrix();
			GL.LoadIdentity();
			GL.modelview = _viewMatrix;
			GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(_projectionMatrix, false));
			for (int i = 0; i < material.passCount; i++)
			{
				if (material.SetPass(i))
				{
					if (xform)
					{
						Graphics.DrawMeshNow(mesh, xform.localToWorldMatrix);
					}
					else
					{
						Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
					}
				}
			}
			GL.PopMatrix();
		}

		public RenderTexture GetTexture()
		{
			return _composite;
		}
	}
}