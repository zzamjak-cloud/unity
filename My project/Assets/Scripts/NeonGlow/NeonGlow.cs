using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>깜빡임 연출 종류.</summary>
    public enum NeonFlickerMode
    {
        None = 0,
        Hum = 1,     // 가스관의 미세한 떨림
        Broken = 2,  // 스타터 불량. 불규칙 점멸
        Warmup = 3   // 점등 직후 더듬다가 안정
    }

    /// <summary>색상 프리셋.</summary>
    public enum NeonPreset
    {
        ClassicRed,
        IceBlue,
        HotPink,
        ToxicGreen,
        WarmWhite,
        AmberOrange
    }

    /// <summary>
    /// SDF 기반 네온 사인 효과.
    ///
    /// 사용자는 흑백 마스크 이미지 한 장만 준비하면 된다.
    /// Tools > CAT > Neon SDF Baker 로 마스크를 SDF 텍스처로 구운 뒤,
    /// 그 결과 스프라이트를 SpriteRenderer / Image 에 넣고 이 컴포넌트를 붙인다.
    ///
    /// 유리관, 발광 코어, 내외곽 글로우는 전부 거리장에서 절차적으로 생성되므로
    /// 채널을 나눠 칠하거나 노이즈 텍스처를 따로 만들 필요가 없다.
    ///
    /// 원거리 번짐은 URP Bloom 이 담당한다. 카메라 HDR 과 Volume 의 Bloom 을 켜고
    /// Exposure 를 1 이상으로 올리면 빛이 화면으로 퍼진다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("CAT/Effects/Neon Glow")]
    public class NeonGlow : MonoBehaviour
    {
        public const string SpriteShaderName = "CAT/Effects/NeonGlow Sprite";
        public const string UIShaderName = "CAT/Effects/NeonGlow UI";

        private enum GraphicKind { None, Sprite, UI }

        #region 인스펙터 프로퍼티
        [Header("유리관")]
        [ColorUsage(true, true)] public Color tubeColor = new Color(1f, 0.25f, 0.45f, 1f);
        [ColorUsage(true, true)] public Color coreColor = new Color(1f, 0.92f, 0.95f, 1f);

        [Range(0.01f, 1f), Tooltip("관 단면의 반지름. 마스크 획 두께의 절반에 맞추면 자연스럽다.")]
        public float tubeWidth = 0.2f;

        [Range(0f, 1f), Tooltip("관 가장자리를 얼마나 어둡게 떨어뜨릴지. 유리 두께감을 만든다.")]
        public float tubeShading = 0.6f;

        [Range(0f, 1f), Tooltip("관 한가운데 흰 심지의 폭.")]
        public float coreWidth = 0.55f;

        [Range(0.001f, 1f)] public float coreSoftness = 0.3f;
        [Range(0f, 4f)] public float tubeIntensity = 1f;

        [Range(0f, 1f), Tooltip("0 이면 관 몸통도 완전 가산 합성되어 배경이 비친다.")]
        public float tubeOpacity = 1f;

        [Header("내부 글로우 (관 주변 강한 발광)")]
        [ColorUsage(true, true)] public Color innerGlowColor = new Color(1f, 0.2f, 0.4f, 1f);
        [Range(0.01f, 1f)] public float innerGlowRadius = 0.2f;
        [Range(0.5f, 8f)] public float innerGlowFalloff = 2f;
        [Range(0f, 8f)] public float innerGlowIntensity = 1.6f;

        [Header("외부 글로우 (넓게 퍼지는 확산)")]
        [ColorUsage(true, true)] public Color outerGlowColor = new Color(0.9f, 0.1f, 0.5f, 1f);
        [Range(0.01f, 1f), Tooltip("1 이면 SDF 를 구울 때 준 패딩 전체를 사용한다.")]
        public float outerGlowRadius = 0.9f;
        [Range(0.5f, 8f)] public float outerGlowFalloff = 3f;
        [Range(0f, 8f)] public float outerGlowIntensity = 0.8f;

        [Header("전역")]
        [Range(0f, 8f), Tooltip("1 을 넘기면 URP Bloom 이 잡아 화면으로 번진다.")]
        public float exposure = 1.5f;

        [ColorUsage(true, true), Tooltip("전체 틴트. 런타임 페이드에 사용한다.")]
        public Color tint = Color.white;

        [Header("깜빡임")]
        public NeonFlickerMode flickerMode = NeonFlickerMode.None;
        [Range(0.1f, 10f)] public float flickerSpeed = 1f;
        [Range(0f, 1f)] public float flickerAmount = 0.5f;
        [Tooltip("켜면 관 몸통은 고정되고 글로우만 깜빡인다.")]
        public bool flickerGlowOnly = false;
        [Range(0.1f, 10f), Tooltip("Warmup 모드에서 안정될 때까지 걸리는 시간(초).")]
        public float warmupDuration = 2f;

        [Header("배칭 (모바일)")]
        [Tooltip("같은 설정의 간판이 여러 개면 켜세요. 머티리얼을 공유해 드로우콜이 합쳐집니다.\n" +
                 "켜면 위 파라미터 대신 아래 머티리얼 에셋의 값이 사용됩니다.")]
        public bool useSharedMaterial = false;

        [Tooltip("공유할 머티리얼 에셋. 인스펙터의 '현재 설정을 머티리얼로 저장' 으로 만들 수 있습니다.")]
        public Material sharedMaterialAsset;

        [Header("셰이더 참조")]
        [Tooltip("비워두면 이름으로 찾는다. 빌드 시 셰이더 스트리핑을 막으려면 직접 지정해 두는 편이 안전하다.")]
        [SerializeField] private Shader spriteShader;
        [SerializeField] private Shader uiShader;
        #endregion

        #region 셰이더 프로퍼티 ID
        private static readonly int TubeColorId = Shader.PropertyToID("_TubeColor");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int TubeWidthId = Shader.PropertyToID("_TubeWidth");
        private static readonly int TubeShadingId = Shader.PropertyToID("_TubeShading");
        private static readonly int CoreWidthId = Shader.PropertyToID("_CoreWidth");
        private static readonly int CoreSoftnessId = Shader.PropertyToID("_CoreSoftness");
        private static readonly int TubeIntensityId = Shader.PropertyToID("_TubeIntensity");
        private static readonly int TubeOpacityId = Shader.PropertyToID("_TubeOpacity");
        private static readonly int InnerGlowColorId = Shader.PropertyToID("_InnerGlowColor");
        private static readonly int InnerGlowRadiusId = Shader.PropertyToID("_InnerGlowRadius");
        private static readonly int InnerGlowFalloffId = Shader.PropertyToID("_InnerGlowFalloff");
        private static readonly int InnerGlowIntensityId = Shader.PropertyToID("_InnerGlowIntensity");
        private static readonly int OuterGlowColorId = Shader.PropertyToID("_OuterGlowColor");
        private static readonly int OuterGlowRadiusId = Shader.PropertyToID("_OuterGlowRadius");
        private static readonly int OuterGlowFalloffId = Shader.PropertyToID("_OuterGlowFalloff");
        private static readonly int OuterGlowIntensityId = Shader.PropertyToID("_OuterGlowIntensity");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int GlowCutoffId = Shader.PropertyToID("_GlowCutoff");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FlickerModeId = Shader.PropertyToID("_FlickerMode");
        private static readonly int FlickerSpeedId = Shader.PropertyToID("_FlickerSpeed");
        private static readonly int FlickerAmountId = Shader.PropertyToID("_FlickerAmount");
        private static readonly int FlickerGlowOnlyId = Shader.PropertyToID("_FlickerGlowOnly");
        private static readonly int WarmupDurationId = Shader.PropertyToID("_WarmupDuration");
        private static readonly int StartTimeId = Shader.PropertyToID("_StartTime");
        private static readonly int UsePreviewTimeId = Shader.PropertyToID("_UsePreviewTime");
        private static readonly int PreviewTimeId = Shader.PropertyToID("_PreviewTime");
        #endregion

        private SpriteRenderer spriteRenderer;
        private Graphic graphic;
        private GraphicKind kind = GraphicKind.None;

        private Material instanceMaterial;   // 이 컴포넌트가 소유하는 인스턴스. 공유 모드면 null.
        private Material appliedMaterial;    // 실제로 렌더러에 붙어 있는 머티리얼
        private Material originalMaterial;
        private bool originalCaptured;

        /// <summary>현재 그래픽에 적용된 머티리얼. 공유 모드면 공유 에셋을 가리킨다.</summary>
        public Material Material => appliedMaterial;

        /// <summary>공유 머티리얼을 쓰는 중인지. 이때는 인스펙터 값이 셰이더로 전달되지 않는다.</summary>
        public bool IsUsingSharedMaterial => useSharedMaterial && sharedMaterialAsset != null;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            EditorPreviewEnd();
            RestoreOriginalMaterial();
            ReleaseInstanceMaterial();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;

#if UNITY_EDITOR
            // OnValidate 안에서 머티리얼을 만들거나 렌더러에 할당하면 Unity 가 경고를 낸다.
            // 한 프레임 뒤로 미뤄 안전한 시점에 재구성한다.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                Rebuild();
            };
#else
            UpdateShaderProperties();
#endif
        }

        /// <summary>그래픽 컴포넌트를 다시 찾고 머티리얼을 재생성한 뒤 모든 값을 적용한다.</summary>
        public void Rebuild()
        {
            DetectGraphic();
            if (kind == GraphicKind.None)
            {
                instanceMaterial = null;
                appliedMaterial = null;
                return;
            }

            // 공유 모드: 인스턴스를 만들지 않으므로 간판 N 개가 드로우콜 1 개로 묶인다.
            // 대신 파라미터는 머티리얼 에셋이 단일 소스이므로 여기서 덮어쓰지 않는다.
            if (IsUsingSharedMaterial)
            {
                ReleaseInstanceMaterial();
                AssignMaterial(sharedMaterialAsset);
                return;
            }

            Shader shader = ResolveShader();
            if (shader == null)
            {
                Debug.LogError($"[NeonGlow] 셰이더를 찾을 수 없습니다. '{SpriteShaderName}' / '{UIShaderName}' 가 프로젝트에 있는지 확인하세요.", this);
                return;
            }

            if (instanceMaterial == null || instanceMaterial.shader != shader)
            {
                ReleaseInstanceMaterial();
                instanceMaterial = new Material(shader)
                {
                    name = "NeonGlow (Instance)",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            AssignMaterial(instanceMaterial);
            ResetFlickerTime();
            UpdateShaderProperties();
        }

        /// <summary>인스펙터 값을 셰이더에 밀어 넣는다. 공유 모드에서는 아무것도 하지 않는다.</summary>
        public void UpdateShaderProperties()
        {
            if (IsUsingSharedMaterial || instanceMaterial == null) return;
            ApplyTo(instanceMaterial);
            if (graphic != null) graphic.SetMaterialDirty();
        }

        /// <summary>
        /// 현재 인스펙터 값을 임의의 머티리얼에 기록한다.
        /// 공유용 머티리얼 에셋을 만들 때 에디터에서도 쓴다.
        /// </summary>
        public void ApplyTo(Material target)
        {
            if (target == null) return;
            target.SetColor(ColorId, tint);

            target.SetColor(TubeColorId, tubeColor);
            target.SetColor(CoreColorId, coreColor);
            target.SetFloat(TubeWidthId, tubeWidth);
            target.SetFloat(TubeShadingId, tubeShading);
            target.SetFloat(CoreWidthId, coreWidth);
            target.SetFloat(CoreSoftnessId, coreSoftness);
            target.SetFloat(TubeIntensityId, tubeIntensity);
            target.SetFloat(TubeOpacityId, tubeOpacity);

            target.SetColor(InnerGlowColorId, innerGlowColor);
            target.SetFloat(InnerGlowRadiusId, innerGlowRadius);
            target.SetFloat(InnerGlowFalloffId, innerGlowFalloff);
            target.SetFloat(InnerGlowIntensityId, innerGlowIntensity);

            target.SetColor(OuterGlowColorId, outerGlowColor);
            target.SetFloat(OuterGlowRadiusId, outerGlowRadius);
            target.SetFloat(OuterGlowFalloffId, outerGlowFalloff);
            target.SetFloat(OuterGlowIntensityId, outerGlowIntensity);

            target.SetFloat(ExposureId, exposure);

            target.SetFloat(FlickerModeId, (float)flickerMode);
            target.SetFloat(FlickerSpeedId, flickerSpeed);
            target.SetFloat(FlickerAmountId, flickerAmount);
            target.SetFloat(FlickerGlowOnlyId, flickerGlowOnly ? 1f : 0f);
            target.SetFloat(WarmupDurationId, warmupDuration);

            // 글로우가 사실상 보이지 않는 거리를 미리 구해 셰이더가 조기 폐기하도록 한다.
            // SDF 패딩 때문에 쿼드 면적의 상당 부분이 여기 해당해서 모바일 오버드로가 크게 준다.
            target.SetFloat(GlowCutoffId, ComputeGlowCutoff());

            // 프리뷰 중에 슬라이더를 만져도 재생이 끊기지 않도록 현재 상태를 그대로 다시 쓴다.
            // 공유 머티리얼 에셋을 만들 때는 둘 다 0 이 되어 프리뷰 상태가 저장되지 않는다.
            target.SetFloat(UsePreviewTimeId, IsEditorPreviewActive ? 1f : 0f);
            target.SetFloat(PreviewTimeId, previewTime);
        }

        /// <summary>
        /// 글로우 기여가 1/1024 미만이 되는 정규화 거리. Bloom 이 증폭해도 티가 안 나는 수준이다.
        /// 기여 = pow(saturate(1 - t/R), F) * I * exposure 이므로
        /// t > R * (1 - (eps / (I * exposure))^(1/F)) 부터 버려도 된다.
        /// </summary>
        private float ComputeGlowCutoff()
        {
            const float eps = 1f / 1024f;
            float gain = Mathf.Max(exposure, 0f) * Mathf.Max(tint.maxColorComponent, 0f);
            return Mathf.Max(
                CutoffFor(innerGlowRadius, innerGlowFalloff, innerGlowIntensity * gain, eps),
                CutoffFor(outerGlowRadius, outerGlowFalloff, outerGlowIntensity * gain, eps));
        }

        private static float CutoffFor(float radius, float falloff, float weight, float eps)
        {
            if (weight <= eps || radius <= 1e-4f) return 0f;
            float x = Mathf.Pow(eps / weight, 1f / Mathf.Max(falloff, 1e-4f));
            return Mathf.Clamp01(radius * (1f - x));
        }

        /// <summary>Warmup 모드의 기준 시각을 현재로 맞춘다. 간판을 다시 켜는 연출에 쓴다.</summary>
        public void ResetFlickerTime()
        {
            if (IsUsingSharedMaterial || instanceMaterial == null) return;
            instanceMaterial.SetFloat(StartTimeId, Application.isPlaying ? Time.time : 0f);
        }

        private void DetectGraphic()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                graphic = null;
                kind = GraphicKind.Sprite;
                return;
            }

            graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                kind = GraphicKind.UI;
                return;
            }

            kind = GraphicKind.None;
        }

        private Shader ResolveShader()
        {
            if (kind == GraphicKind.Sprite)
            {
                if (spriteShader == null) spriteShader = Shader.Find(SpriteShaderName);
                return spriteShader;
            }

            if (uiShader == null) uiShader = Shader.Find(UIShaderName);
            return uiShader;
        }

        private void AssignMaterial(Material target)
        {
            appliedMaterial = target;

            if (kind == GraphicKind.Sprite)
            {
                if (!originalCaptured)
                {
                    originalMaterial = spriteRenderer.sharedMaterial;
                    originalCaptured = true;
                }
                if (spriteRenderer.sharedMaterial != target)
                    spriteRenderer.sharedMaterial = target;
            }
            else if (kind == GraphicKind.UI)
            {
                if (!originalCaptured)
                {
                    originalMaterial = graphic.material;
                    originalCaptured = true;
                }
                if (graphic.material != target)
                    graphic.material = target;
            }
        }

        private void RestoreOriginalMaterial()
        {
            if (!originalCaptured) return;

            if (kind == GraphicKind.Sprite && spriteRenderer != null)
                spriteRenderer.sharedMaterial = originalMaterial;
            else if (kind == GraphicKind.UI && graphic != null)
                graphic.material = originalMaterial;

            originalCaptured = false;
            originalMaterial = null;
            appliedMaterial = null;
        }

        private void ReleaseInstanceMaterial()
        {
            if (instanceMaterial == null) return;

            if (Application.isPlaying) Destroy(instanceMaterial);
            else DestroyImmediate(instanceMaterial);

            instanceMaterial = null;
        }

        #region 에디터 씬 뷰 프리뷰
        private float previewTime;

        /// <summary>씬 뷰 프리뷰가 돌고 있는지.</summary>
        public bool IsEditorPreviewActive { get; private set; }

        /// <summary>
        /// 프리뷰를 시작한다. 시작할 수 없으면 false 를 돌려준다.
        /// 편집 모드에서는 _Time 이 흐르지 않으므로 셰이더가 _PreviewTime 을 보도록 전환한다.
        /// </summary>
        public bool EditorPreviewBegin()
        {
            if (flickerMode == NeonFlickerMode.None) return false;

            // 공유 머티리얼은 여러 오브젝트가 함께 쓰는 에셋이라 프리뷰로 건드리지 않는다.
            if (IsUsingSharedMaterial) return false;

            if (instanceMaterial == null) Rebuild();
            if (instanceMaterial == null) return false;

            previewTime = 0f;
            IsEditorPreviewActive = true;
            instanceMaterial.SetFloat(UsePreviewTimeId, 1f);
            instanceMaterial.SetFloat(PreviewTimeId, 0f);
            instanceMaterial.SetFloat(StartTimeId, 0f);   // Warmup 을 처음부터 재생
            return true;
        }

        /// <summary>프리뷰 시간을 delta 만큼 진행시킨다.</summary>
        public void EditorPreviewTick(float delta)
        {
            if (!IsEditorPreviewActive || instanceMaterial == null) return;

            previewTime += delta;
            instanceMaterial.SetFloat(PreviewTimeId, previewTime);
        }

        /// <summary>프리뷰를 끝내고 셰이더를 평소 시간축으로 되돌린다.</summary>
        public void EditorPreviewEnd()
        {
            IsEditorPreviewActive = false;
            previewTime = 0f;
            if (instanceMaterial != null) instanceMaterial.SetFloat(UsePreviewTimeId, 0f);
        }
        #endregion

        #region 런타임 유틸리티
        /// <summary>전체 밝기를 조절한다. 1 이하로 내리면 꺼진 느낌이 된다.</summary>
        public void SetExposure(float value)
        {
            exposure = Mathf.Max(0f, value);
            if (IsUsingSharedMaterial || instanceMaterial == null) return;
            instanceMaterial.SetFloat(ExposureId, exposure);
            // 밝기가 바뀌면 글로우가 보이는 거리도 바뀐다.
            instanceMaterial.SetFloat(GlowCutoffId, ComputeGlowCutoff());
        }

        /// <summary>틴트 색상을 바꾼다. 알파를 낮추면 전체가 페이드아웃된다.</summary>
        public void SetTint(Color value)
        {
            tint = value;
            if (IsUsingSharedMaterial || instanceMaterial == null) return;
            instanceMaterial.SetColor(ColorId, tint);
            instanceMaterial.SetFloat(GlowCutoffId, ComputeGlowCutoff());
        }

        /// <summary>관과 글로우 색을 한 번에 바꾼다.</summary>
        public void SetNeonColor(Color tube, Color inner, Color outer)
        {
            tubeColor = tube;
            innerGlowColor = inner;
            outerGlowColor = outer;
            if (IsUsingSharedMaterial || instanceMaterial == null) return;
            instanceMaterial.SetColor(TubeColorId, tubeColor);
            instanceMaterial.SetColor(InnerGlowColorId, innerGlowColor);
            instanceMaterial.SetColor(OuterGlowColorId, outerGlowColor);
        }

        /// <summary>깜빡임 모드를 바꾼다. Warmup 으로 바꾸면 기준 시각도 갱신한다.</summary>
        public void SetFlicker(NeonFlickerMode mode, float speed = -1f, float amount = -1f)
        {
            flickerMode = mode;
            if (speed >= 0f) flickerSpeed = Mathf.Clamp(speed, 0.1f, 10f);
            if (amount >= 0f) flickerAmount = Mathf.Clamp01(amount);

            if (IsUsingSharedMaterial || instanceMaterial == null) return;
            instanceMaterial.SetFloat(FlickerModeId, (float)flickerMode);
            instanceMaterial.SetFloat(FlickerSpeedId, flickerSpeed);
            instanceMaterial.SetFloat(FlickerAmountId, flickerAmount);
            if (mode == NeonFlickerMode.Warmup) ResetFlickerTime();
        }

        /// <summary>색상 프리셋을 적용한다.</summary>
        public void ApplyPreset(NeonPreset preset)
        {
            switch (preset)
            {
                case NeonPreset.ClassicRed:
                    tubeColor = new Color(1f, 0.12f, 0.10f);
                    coreColor = new Color(1f, 0.78f, 0.68f);
                    innerGlowColor = new Color(1f, 0.15f, 0.08f);
                    outerGlowColor = new Color(1f, 0.08f, 0.05f);
                    break;
                case NeonPreset.IceBlue:
                    tubeColor = new Color(0.25f, 0.75f, 1f);
                    coreColor = new Color(0.88f, 0.97f, 1f);
                    innerGlowColor = new Color(0.20f, 0.70f, 1f);
                    outerGlowColor = new Color(0.10f, 0.45f, 1f);
                    break;
                case NeonPreset.HotPink:
                    tubeColor = new Color(1f, 0.15f, 0.60f);
                    coreColor = new Color(1f, 0.85f, 0.95f);
                    innerGlowColor = new Color(1f, 0.10f, 0.55f);
                    outerGlowColor = new Color(0.85f, 0.05f, 0.60f);
                    break;
                case NeonPreset.ToxicGreen:
                    tubeColor = new Color(0.35f, 1f, 0.30f);
                    coreColor = new Color(0.90f, 1f, 0.85f);
                    innerGlowColor = new Color(0.30f, 1f, 0.25f);
                    outerGlowColor = new Color(0.15f, 0.90f, 0.20f);
                    break;
                case NeonPreset.WarmWhite:
                    tubeColor = new Color(1f, 0.92f, 0.80f);
                    coreColor = Color.white;
                    innerGlowColor = new Color(1f, 0.88f, 0.70f);
                    outerGlowColor = new Color(1f, 0.80f, 0.55f);
                    break;
                case NeonPreset.AmberOrange:
                    tubeColor = new Color(1f, 0.55f, 0.10f);
                    coreColor = new Color(1f, 0.90f, 0.70f);
                    innerGlowColor = new Color(1f, 0.50f, 0.08f);
                    outerGlowColor = new Color(1f, 0.35f, 0.05f);
                    break;
            }

            UpdateShaderProperties();
        }
        #endregion
    }
}
