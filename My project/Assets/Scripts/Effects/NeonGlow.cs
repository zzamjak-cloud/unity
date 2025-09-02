using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// 네온 글로우 효과를 Sprite, UI Image, UI Raw Image 그래픽 컴포넌트에 적용하는 통합 컴포넌트입니다.
    /// RGBA 채널을 각각 분리해서 특수한 Main Texture를 만들어야 합니다.
    /// 깜빡임 효과를 넣으려면 별도의 Noise Texture를 적용해야 합니다.
    /// 이 컴포넌트는 NeonGlow 셰이더와 함께 작동합니다.
    /// </summary>
    [AddComponentMenu("CAT/Effects/NeonGlow")]
    public class NeonGlowEffect : MonoBehaviour
    {
        // 지원되는 그래픽 컴포넌트 타입 열거형
        private enum GraphicType
        {
            None,
            SpriteRenderer,
            Image,
            RawImage
            // 필요에 따라 확장 가능
        }

        #region 셰이더 프로퍼티 변수
        [Header("기본 설정")]
        [Tooltip("주요 텍스처 (RGBA 채널 사용)")]
        public Texture2D mainTexture;

        public Color mainColor = Color.white;
        public Color innerGlowColor = Color.green;
        public Color outerGlowColor = Color.blue;

        [Range(0f, 3f)]
        public float innerGlowIntensity = 1.0f;

        [Range(0f, 3f)]
        public float outerGlowIntensity = 1.0f;

        [Range(0f, 5f)]
        public float emissionIntensity = 1.0f;

        [Space(10)]
        [Header("깜빡임 효과")]
        [Tooltip("깜빡임 효과 사용 여부")]
        public bool useFlicker = true;

        [Tooltip("노이즈 텍스처 (R 채널 사용)")]
        public Texture2D noiseTexture;

        [Range(0.1f, 10f)]
        [Tooltip("깜빡임 속도")]
        public float flickerSpeed = 2.0f;

        [Range(0f, 1f)]
        [Tooltip("내부 깜빡임 최소값")]
        public float innerFlickerMin = 0.7f;

        [Range(1f, 3f)]
        [Tooltip("내부 깜빡임 최대값")]
        public float innerFlickerMax = 1.3f;

        [Range(0f, 1f)]
        [Tooltip("외부 깜빡임 최소값")]
        public float outerFlickerMin = 0.8f;

        [Range(1f, 3f)]
        [Tooltip("외부 깜빡임 최대값")]
        public float outerFlickerMax = 1.2f;

        [Range(0.1f, 5f)]
        [Tooltip("내부 노이즈 스케일")]
        public float noiseScaleInner = 1.0f;

        [Range(0.1f, 5f)]
        [Tooltip("외부 노이즈 스케일")]
        public float noiseScaleOuter = 0.7f;

        [Range(0f, 1f)]
        [Tooltip("외부 노이즈 오프셋")]
        public float noiseOffsetOuter = 0.5f;
        #endregion

        // 셰이더 프로퍼티 ID 캐싱 (메모리 및 퍼포먼스 최적화)
        private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");
        private static readonly int MainColorProp = Shader.PropertyToID("_MainColor");
        private static readonly int InnerGlowColorProp = Shader.PropertyToID("_InnerGlowColor");
        private static readonly int OuterGlowColorProp = Shader.PropertyToID("_OuterGlowColor");
        private static readonly int InnerGlowIntensityProp = Shader.PropertyToID("_InnerGlowIntensity");
        private static readonly int OuterGlowIntensityProp = Shader.PropertyToID("_OuterGlowIntensity");
        private static readonly int EmissionIntensityProp = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int UseFlickerProp = Shader.PropertyToID("_UseFlicker");
        private static readonly int NoiseTextureProp = Shader.PropertyToID("_NoiseTexture");
        private static readonly int FlickerSpeedProp = Shader.PropertyToID("_FlickerSpeed");
        private static readonly int InnerFlickerMinProp = Shader.PropertyToID("_InnerFlickerMin");
        private static readonly int InnerFlickerMaxProp = Shader.PropertyToID("_InnerFlickerMax");
        private static readonly int OuterFlickerMinProp = Shader.PropertyToID("_OuterFlickerMin");
        private static readonly int OuterFlickerMaxProp = Shader.PropertyToID("_OuterFlickerMax");
        private static readonly int NoiseScaleInnerProp = Shader.PropertyToID("_NoiseScaleInner");
        private static readonly int NoiseScaleOuterProp = Shader.PropertyToID("_NoiseScaleOuter");
        private static readonly int NoiseOffsetOuterProp = Shader.PropertyToID("_NoiseOffsetOuter");

        // 그래픽 컴포넌트 참조들
        private SpriteRenderer spriteRenderer;
        private Image uiImage;
        private RawImage rawImage;
        private GraphicType graphicType = GraphicType.None;

        // 네온 글로우 셰이더 머티리얼
        private Material neonGlowMaterial;

        private void Awake()
        {
            // 자동으로 그래픽 컴포넌트 탐지
            DetectGraphicComponent();

            if (graphicType == GraphicType.None)
            {
                Debug.LogError("지원되는 그래픽 컴포넌트(SpriteRenderer, Image, RawImage)를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            // 네온 글로우 셰이더 로드
            Shader neonGlowShader = Shader.Find("CAT/Effects/NeonGlow");

            if (neonGlowShader == null)
            {
                Debug.LogError("NeonGlow 셰이더를 찾을 수 없습니다. 셰이더가 프로젝트에 포함되어 있는지 확인하세요.");
                enabled = false;
                return;
            }

            // 새 머티리얼 생성 및 셰이더 적용
            neonGlowMaterial = new Material(neonGlowShader);

            // 발견된 그래픽 컴포넌트에 머티리얼 적용
            ApplyMaterialToGraphic();

            // 초기 셰이더 프로퍼티 설정
            UpdateShaderProperties();
        }

        /// <summary>
        /// 이 게임 오브젝트에서 사용 가능한 그래픽 컴포넌트를 감지합니다.
        /// </summary>
        private void DetectGraphicComponent()
        {
            // 우선순위: SpriteRenderer > Image > RawImage
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                graphicType = GraphicType.SpriteRenderer;
                return;
            }

            uiImage = GetComponent<Image>();
            if (uiImage != null)
            {
                graphicType = GraphicType.Image;
                return;
            }

            rawImage = GetComponent<RawImage>();
            if (rawImage != null)
            {
                graphicType = GraphicType.RawImage;
                return;
            }

            graphicType = GraphicType.None;
        }

        /// <summary>
        /// 현재 감지된 그래픽 컴포넌트에 네온 글로우 머티리얼을 적용합니다.
        /// </summary>
        private void ApplyMaterialToGraphic()
        {
            switch (graphicType)
            {
                case GraphicType.SpriteRenderer:
                    spriteRenderer.material = neonGlowMaterial;
                    break;
                case GraphicType.Image:
                    uiImage.material = neonGlowMaterial;
                    break;
                case GraphicType.RawImage:
                    rawImage.material = neonGlowMaterial;
                    break;
            }
        }

        /// <summary>
        /// 메인 텍스처를 현재 그래픽 컴포넌트의 텍스처로 설정합니다.
        /// </summary>
        private void TryUseGraphicTexture()
        {
            if (mainTexture != null)
                return;

            switch (graphicType)
            {
                case GraphicType.SpriteRenderer:
                    if (spriteRenderer.sprite != null)
                    {
                        mainTexture = spriteRenderer.sprite.texture;
                    }
                    break;
                case GraphicType.Image:
                    if (uiImage.sprite != null)
                    {
                        mainTexture = uiImage.sprite.texture;
                    }
                    break;
                case GraphicType.RawImage:
                    if (rawImage.texture != null && rawImage.texture is Texture2D)
                    {
                        mainTexture = (Texture2D)rawImage.texture;
                    }
                    break;
            }
        }

        private void OnValidate()
        {
            // 인스펙터에서 값이 변경될 때마다 셰이더 프로퍼티 업데이트
            if (neonGlowMaterial != null)
            {
                UpdateShaderProperties();
            }
        }

        /// <summary>
        /// 컴포넌트의 변수 값을 셰이더 프로퍼티에 적용합니다.
        /// </summary>
        public void UpdateShaderProperties()
        {
            // 기본 텍스처가 설정되어 있지 않으면 그래픽 컴포넌트의 텍스처 사용
            TryUseGraphicTexture();

            // 텍스처 설정
            if (mainTexture != null)
            {
                neonGlowMaterial.SetTexture(MainTexProp, mainTexture);
            }

            // 색상 및 강도 설정
            neonGlowMaterial.SetColor(MainColorProp, mainColor);
            neonGlowMaterial.SetColor(InnerGlowColorProp, innerGlowColor);
            neonGlowMaterial.SetColor(OuterGlowColorProp, outerGlowColor);
            neonGlowMaterial.SetFloat(InnerGlowIntensityProp, innerGlowIntensity);
            neonGlowMaterial.SetFloat(OuterGlowIntensityProp, outerGlowIntensity);
            neonGlowMaterial.SetFloat(EmissionIntensityProp, emissionIntensity);

            // 깜빡임 효과 설정
            neonGlowMaterial.SetFloat(UseFlickerProp, useFlicker ? 1.0f : 0.0f);

            if (noiseTexture != null)
            {
                neonGlowMaterial.SetTexture(NoiseTextureProp, noiseTexture);
            }

            neonGlowMaterial.SetFloat(FlickerSpeedProp, flickerSpeed);
            neonGlowMaterial.SetFloat(InnerFlickerMinProp, innerFlickerMin);
            neonGlowMaterial.SetFloat(InnerFlickerMaxProp, innerFlickerMax);
            neonGlowMaterial.SetFloat(OuterFlickerMinProp, outerFlickerMin);
            neonGlowMaterial.SetFloat(OuterFlickerMaxProp, outerFlickerMax);
            neonGlowMaterial.SetFloat(NoiseScaleInnerProp, noiseScaleInner);
            neonGlowMaterial.SetFloat(NoiseScaleOuterProp, noiseScaleOuter);
            neonGlowMaterial.SetFloat(NoiseOffsetOuterProp, noiseOffsetOuter);
        }

        #region 런타임 유틸리티 메서드
        /// <summary>
        /// 런타임에 내부 글로우 색상을 변경합니다.
        /// </summary>
        public void SetInnerGlowColor(Color color)
        {
            innerGlowColor = color;
            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetColor(InnerGlowColorProp, innerGlowColor);
            }
        }

        /// <summary>
        /// 런타임에 외부 글로우 색상을 변경합니다.
        /// </summary>
        public void SetOuterGlowColor(Color color)
        {
            outerGlowColor = color;
            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetColor(OuterGlowColorProp, outerGlowColor);
            }
        }

        /// <summary>
        /// 런타임에 메인 색상을 변경합니다.
        /// </summary>
        public void SetMainColor(Color color)
        {
            mainColor = color;
            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetColor(MainColorProp, mainColor);
            }
        }

        /// <summary>
        /// 런타임에 글로우 강도를 변경합니다.
        /// </summary>
        public void SetGlowIntensity(float inner, float outer)
        {
            innerGlowIntensity = inner;
            outerGlowIntensity = outer;

            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetFloat(InnerGlowIntensityProp, innerGlowIntensity);
                neonGlowMaterial.SetFloat(OuterGlowIntensityProp, outerGlowIntensity);
            }
        }

        /// <summary>
        /// 런타임에 발광 강도를 변경합니다.
        /// </summary>
        public void SetEmissionIntensity(float intensity)
        {
            emissionIntensity = intensity;
            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetFloat(EmissionIntensityProp, emissionIntensity);
            }
        }

        /// <summary>
        /// 런타임에 깜빡임 효과를 켜거나 끕니다.
        /// </summary>
        public void ToggleFlicker(bool enabled)
        {
            useFlicker = enabled;
            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetFloat(UseFlickerProp, useFlicker ? 1.0f : 0.0f);
            }
        }

        /// <summary>
        /// 런타임에 깜빡임 속도를 변경합니다.
        /// </summary>
        public void SetFlickerSpeed(float speed)
        {
            flickerSpeed = Mathf.Clamp(speed, 0.1f, 10f);
            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetFloat(FlickerSpeedProp, flickerSpeed);
            }
        }

        /// <summary>
        /// 런타임에 깜빡임 범위를 설정합니다.
        /// </summary>
        public void SetFlickerRange(float innerMin, float innerMax, float outerMin, float outerMax)
        {
            innerFlickerMin = Mathf.Clamp(innerMin, 0f, 1f);
            innerFlickerMax = Mathf.Clamp(innerMax, 1f, 3f);
            outerFlickerMin = Mathf.Clamp(outerMin, 0f, 1f);
            outerFlickerMax = Mathf.Clamp(outerMax, 1f, 3f);

            if (neonGlowMaterial != null)
            {
                neonGlowMaterial.SetFloat(InnerFlickerMinProp, innerFlickerMin);
                neonGlowMaterial.SetFloat(InnerFlickerMaxProp, innerFlickerMax);
                neonGlowMaterial.SetFloat(OuterFlickerMinProp, outerFlickerMin);
                neonGlowMaterial.SetFloat(OuterFlickerMaxProp, outerFlickerMax);
            }
        }
        #endregion

        /// <summary>
        /// GameObject가 비활성화될 때 인스턴스 머티리얼 정리
        /// </summary>
        private void OnDisable()
        {
            // 런타임에 생성된 인스턴스 머티리얼 정리
            if (Application.isPlaying && neonGlowMaterial != null)
            {
                Destroy(neonGlowMaterial);
            }
        }
    }
}