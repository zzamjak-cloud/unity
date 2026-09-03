using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CAT.Toon
{
    /// <summary>
    /// 카메라 Depth / DepthNormals 텍스처로 화면 전체 아웃라인을 그리는 URP 렌더러 피처입니다.
    /// 씬 컬러를 복사하지 않고 블렌딩만으로 합성하므로 추가 RT 없이 동작합니다.
    /// 런타임 값 변경은 <see cref="CATToonOutlineRuntime"/> 를 통해 처리합니다.
    /// </summary>
    [DisallowMultipleRendererFeature("CAT Toon Outline")]
    public class CATToonOutlineFeature : ScriptableRendererFeature
    {
        /// <summary>아웃라인 합성 방식</summary>
        public enum OutlineBlendMode
        {
            /// <summary>단색 라인을 알파 블렌드로 얹는다. 또렷한 캐주얼 룩.</summary>
            Solid = 0,
            /// <summary>씬 컬러에 곱해 잉크가 스며든 듯한 룩.</summary>
            Multiply = 1,
        }

        /// <summary>해상도에 따른 라인 두께 처리 방식</summary>
        public enum ThicknessScaling
        {
            /// <summary>해상도와 무관하게 항상 같은 픽셀 두께.</summary>
            FixedPixels = 0,
            /// <summary>기준 높이 대비로 두께를 환산해 화면 대비 굵기를 일정하게 유지.</summary>
            ScaleWithHeight = 1,
        }

        [Serializable]
        public class Settings
        {
            [Tooltip("아웃라인을 그릴 시점. 기본값은 스카이박스 직후(반투명 오브젝트 제외)입니다.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Tooltip("합성 방식")]
            public OutlineBlendMode blendMode = OutlineBlendMode.Solid;

            [ColorUsage(true, true)]
            [Tooltip("아웃라인 컬러. 알파가 진하기입니다.")]
            public Color outlineColor = new Color(0.07f, 0.06f, 0.11f, 1f);

            [Range(0.5f, 6f)]
            [Tooltip("라인 두께. Thickness Mode 가 Scale With Height 이면 Reference Height 기준 픽셀 값입니다.")]
            public float thickness = 1.8f;

            [Tooltip("Scale With Height: 해상도가 바뀌어도 화면 대비 같은 굵기를 유지합니다(권장).\n" +
                     "Fixed Pixels: 항상 같은 픽셀 두께. 뷰포트가 작아지면 화면을 덮을 수 있습니다.")]
            public ThicknessScaling thicknessMode = ThicknessScaling.ScaleWithHeight;

            [Tooltip("Scale With Height 의 기준 세로 해상도. 이 높이에서 Thickness 가 그대로 픽셀 두께가 됩니다.")]
            public float referenceHeight = 1080f;

            [Header("깊이 엣지")]
            [Range(0f, 1f)]
            [Tooltip("실루엣 검출 임계값. 낮을수록 라인이 많아집니다.")]
            public float depthThreshold = 0.06f;

            [Range(0.001f, 0.5f)]
            public float depthSmooth = 0.04f;

            [Range(0f, 1f)]
            [Tooltip("시선과 거의 평행한 바닥/벽에서 생기는 가짜 라인을 억제합니다.")]
            public float grazingSuppress = 0.7f;

            [Header("노멀 엣지 (내부 크리스)")]
            public bool useNormalEdge = true;

            [Range(0f, 2f)]
            public float normalThreshold = 0.35f;

            [Range(0.001f, 1f)]
            public float normalSmooth = 0.25f;

            [Header("거리 페이드")]
            [Tooltip("이 거리부터 아웃라인이 옅어지기 시작합니다.")]
            public float fadeStart = 40f;

            [Tooltip("이 거리에서 아웃라인이 완전히 사라집니다.")]
            public float fadeEnd = 90f;

            [Header("스케치 (손그림 흔들림)")]
            [Range(0f, 4f)]
            [Tooltip("0 이면 깔끔한 라인, 값을 올리면 연필로 그린 듯 라인이 떨립니다.")]
            public float sketchJitter = 0f;

            [Range(1f, 30f)]
            [Tooltip("라인이 새로 흔들리는 초당 횟수. 낮을수록 수작업 애니메이션 느낌이 납니다.")]
            public float sketchFrequency = 12f;

            [Header("기타")]
            public bool renderInSceneView = true;
        }

        public Settings settings = new Settings();

        [SerializeField, HideInInspector]
        private Shader m_OutlineShader;

        private Material m_Material;
        private CATToonOutlinePass m_Pass;

        private const string ShaderName = "CAT/Toon/ScreenSpaceOutline";

        public override void Create()
        {
            if (m_OutlineShader == null)
                m_OutlineShader = Shader.Find(ShaderName);

            if (m_OutlineShader == null)
            {
                Debug.LogWarning($"[CATToonOutline] '{ShaderName}' 셰이더를 찾지 못했습니다. 아웃라인이 비활성화됩니다.");
                return;
            }

            if (m_Material == null)
                m_Material = CoreUtils.CreateEngineMaterial(m_OutlineShader);

            m_Pass ??= new CATToonOutlinePass();
            m_Pass.Setup(m_Material, settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Material == null || m_Pass == null)
                return;

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;
            if (cameraType == CameraType.SceneView && !settings.renderInSceneView)
                return;
            if (!CATToonOutlineRuntime.ResolveEnabled(true))
                return;

            m_Pass.Setup(m_Material, settings);
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
            m_Material = null;
            m_Pass = null;
        }

        // -------------------------------------------------------------------
        private class CATToonOutlinePass : ScriptableRenderPass
        {
            private static readonly int OutlineColorId     = Shader.PropertyToID("_OutlineColor");
            private static readonly int OutlineTexelSizeId = Shader.PropertyToID("_OutlineTexelSize");
            private static readonly int ThicknessId        = Shader.PropertyToID("_OutlineThickness");
            private static readonly int DepthThresholdId   = Shader.PropertyToID("_DepthThreshold");
            private static readonly int DepthSmoothId      = Shader.PropertyToID("_DepthSmooth");
            private static readonly int NormalThresholdId  = Shader.PropertyToID("_NormalThreshold");
            private static readonly int NormalSmoothId     = Shader.PropertyToID("_NormalSmooth");
            private static readonly int GrazingSuppressId  = Shader.PropertyToID("_GrazingSuppress");
            private static readonly int FadeStartId        = Shader.PropertyToID("_FadeStart");
            private static readonly int FadeEndId          = Shader.PropertyToID("_FadeEnd");
            private static readonly int SketchJitterId     = Shader.PropertyToID("_SketchJitter");
            private static readonly int SketchFrequencyId  = Shader.PropertyToID("_SketchFrequency");
            private static readonly int UseNormalEdgeId    = Shader.PropertyToID("_UseNormalEdge");

            private Material m_Material;
            private Settings m_Settings;

            private class PassData
            {
                public Material material;
                public int      shaderPass;
            }

            public void Setup(Material material, Settings settings)
            {
                m_Material  = material;
                m_Settings  = settings;
                renderPassEvent = settings.injectionPoint;

                // URP 가 _CameraDepthTexture / _CameraNormalsTexture 를 준비하도록 요청한다.
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            /// <summary>
            /// 실제 샘플링에 쓸 픽셀 두께를 구한다.
            /// ScaleWithHeight 에서는 UV 오프셋이 (thickness / referenceHeight) 로 고정되므로
            /// 해상도가 바뀌어도 라인 굵기와 검출되는 엣지 양이 함께 유지된다.
            /// </summary>
            private float ResolveThicknessPixels(float targetHeight)
            {
                float thickness = CATToonOutlineRuntime.ResolveThickness(m_Settings.thickness);

                if (m_Settings.thicknessMode == ThicknessScaling.ScaleWithHeight)
                    thickness *= targetHeight / Mathf.Max(1f, m_Settings.referenceHeight);

                // 0.5px 미만이면 로버츠 크로스 4샘플이 같은 텍셀을 읽어 엣지가 사라진다.
                return Mathf.Max(thickness, 0.5f);
            }

            private void UpdateMaterial(UniversalCameraData cameraData)
            {
                var desc = cameraData.cameraTargetDescriptor;
                float w = Mathf.Max(1, desc.width);
                float h = Mathf.Max(1, desc.height);

                m_Material.SetVector(OutlineTexelSizeId, new Vector4(1f / w, 1f / h, w, h));

                m_Material.SetColor(OutlineColorId, CATToonOutlineRuntime.ResolveColor(m_Settings.outlineColor));
                m_Material.SetFloat(ThicknessId, ResolveThicknessPixels(h));
                m_Material.SetFloat(DepthThresholdId, m_Settings.depthThreshold);
                m_Material.SetFloat(DepthSmoothId, m_Settings.depthSmooth);
                m_Material.SetFloat(NormalThresholdId, m_Settings.normalThreshold);
                m_Material.SetFloat(NormalSmoothId, m_Settings.normalSmooth);
                m_Material.SetFloat(GrazingSuppressId, m_Settings.grazingSuppress);
                m_Material.SetFloat(FadeStartId, m_Settings.fadeStart);
                m_Material.SetFloat(FadeEndId, Mathf.Max(m_Settings.fadeEnd, m_Settings.fadeStart + 0.01f));
                m_Material.SetFloat(SketchJitterId, CATToonOutlineRuntime.ResolveSketchJitter(m_Settings.sketchJitter));
                m_Material.SetFloat(SketchFrequencyId, m_Settings.sketchFrequency);
                m_Material.SetFloat(UseNormalEdgeId, m_Settings.useNormalEdge ? 1f : 0f);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Material == null)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData   = frameData.Get<UniversalCameraData>();

                // 백버퍼에 직접 그리는 구성에서는 중간 텍스처가 없어 블렌딩 대상을 보장할 수 없다.
                if (resourceData.isActiveTargetBackBuffer)
                    return;
                if (!resourceData.cameraDepthTexture.IsValid() || !resourceData.cameraNormalsTexture.IsValid())
                    return;

                UpdateMaterial(cameraData);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("CAT Toon Outline", out var passData))
                {
                    passData.material   = m_Material;
                    passData.shaderPass = (int)CATToonOutlineRuntime.ResolveBlendMode(m_Settings.blendMode);

                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);

                    // 셰이더가 _CameraDepthTexture / _CameraNormalsTexture 전역 바인딩을 읽으므로
                    // RenderGraph 에 전역 텍스처 사용을 명시해야 실제로 바인딩된다.
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), data.material, data.shaderPass);
                    });
                }
            }
        }
    }
}
