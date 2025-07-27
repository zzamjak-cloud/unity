using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    [AddComponentMenu("CAT/Effects/WindEffect")]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class WindEffect : MonoBehaviour
    {
        public enum TargetType
        {
            SpriteRenderer,
            UIImage,
            UIRawImage
        }

        [Header("Target Settings")]
        [SerializeField] private TargetType targetType = TargetType.SpriteRenderer;
        private TargetType previousTargetType;

        [Header("Wind Settings")]
        [Range(0, 0.1f)]
        [SerializeField] private float windStrength = 0.02f;
        [Range(0, 10f)]
        [SerializeField] private float windSpeed = 1.0f;
        [Range(0.1f, 10f)]
        [SerializeField] private float noiseScale = 1.0f;

        [Header("Textures")]
        [SerializeField] private Texture2D noiseTexture = null;
        [SerializeField] private Texture2D gradientTexture = null;

        // 컴포넌트 캐싱
        private SpriteRenderer spriteRenderer;
        private Image uiImage;
        private RawImage uiRawImage;
        private Material instanceMaterial;

        // 원본 머티리얼 저장용
        private Material originalMaterial;

        // 셰이더 프로퍼티 ID 캐싱
        private static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
        private static readonly int NoiseTex2Id = Shader.PropertyToID("_NoiseTex2");
        private static readonly int GradientTexId = Shader.PropertyToID("_GradientTex");

        private void Awake()
        {
            // 초기화 시 현재 타입 기억
            previousTargetType = targetType;
        }

        private void OnEnable()
        {
            // 대상 컴포넌트 찾기
            InitializeComponents();

            // 적절한 셰이더 적용
            ApplyShader();
        }

        private void OnDisable()
        {
            // 원래 머티리얼로 복원
            RestoreOriginalMaterial();

            // 인스턴스 머티리얼 정리
            CleanupMaterial();
        }

        private void OnValidate()
        {
            // 대상 컴포넌트 찾기
            InitializeComponents();

            // 타입이 변경되었는지 확인
            if (previousTargetType != targetType)
            {
                // 원래 머티리얼로 복원
                RestoreOriginalMaterial();

                // 인스턴스 머티리얼 정리
                CleanupMaterial();

                // 새 셰이더 적용
                ApplyShader();

                // 현재 타입 기억
                previousTargetType = targetType;
            }

            // 셰이더 속성 업데이트
            if (instanceMaterial != null)
            {
                UpdateShaderProperties();
            }
        }

        private void Update()
        {
            if (instanceMaterial != null)
            {
                UpdateShaderProperties();
            }
        }

        private void InitializeComponents()
        {
            // 컴포넌트 캐싱
            spriteRenderer = GetComponent<SpriteRenderer>();
            uiImage = GetComponent<Image>();
            uiRawImage = GetComponent<RawImage>();

            // 현재 게임오브젝트에 있는 컴포넌트에 따라 자동 타입 설정
            if (targetType == TargetType.SpriteRenderer && spriteRenderer == null && uiImage != null)
            {
                targetType = TargetType.UIImage;
                previousTargetType = targetType;
            }
            else if (targetType == TargetType.SpriteRenderer && spriteRenderer == null && uiRawImage != null)
            {
                targetType = TargetType.UIRawImage;
                previousTargetType = targetType;
            }
            else if (targetType == TargetType.UIImage && uiImage == null && spriteRenderer != null)
            {
                targetType = TargetType.SpriteRenderer;
                previousTargetType = targetType;
            }
            else if (targetType == TargetType.UIRawImage && uiRawImage == null && spriteRenderer != null)
            {
                targetType = TargetType.SpriteRenderer;
                previousTargetType = targetType;
            }
        }

        private void ApplyShader()
        {
            // 대상 컴포넌트에 따라 적절한 셰이더 적용
            switch (targetType)
            {
                case TargetType.SpriteRenderer:
                    if (spriteRenderer != null)
                    {
                        // 원본 머티리얼 저장 (sharedMaterial 사용)
                        originalMaterial = spriteRenderer.sharedMaterial;

                        // 새로운 머티리얼 생성
                        instanceMaterial = new Material(Shader.Find("CAT/Effects/WindEffectSprite"));

                        // 원본 속성 복사 (null 체크)
                        if (originalMaterial != null)
                        {
                            instanceMaterial.CopyPropertiesFromMaterial(originalMaterial);
                        }

                        // 에디터 모드에서는 sharedMaterial 사용
                        spriteRenderer.sharedMaterial = instanceMaterial;
                    }
                    break;

                case TargetType.UIImage:
                    if (uiImage != null)
                    {
                        // 원본 머티리얼 저장
                        originalMaterial = uiImage.material;

                        // 새로운 머티리얼 생성
                        instanceMaterial = new Material(Shader.Find("CAT/Effects/WindEffectUI"));

                        // 원본 속성 복사 (null 체크)
                        if (originalMaterial != null)
                        {
                            instanceMaterial.CopyPropertiesFromMaterial(originalMaterial);
                        }

                        // UI 컴포넌트에 적용
                        uiImage.material = instanceMaterial;
                    }
                    break;

                case TargetType.UIRawImage:
                    if (uiRawImage != null)
                    {
                        // 원본 머티리얼 저장
                        originalMaterial = uiRawImage.material;

                        // 새로운 머티리얼 생성
                        instanceMaterial = new Material(Shader.Find("CAT/Effects/WindEffectUI"));

                        // 원본 속성 복사 (null 체크)
                        if (originalMaterial != null)
                        {
                            instanceMaterial.CopyPropertiesFromMaterial(originalMaterial);
                        }

                        // UI 컴포넌트에 적용
                        uiRawImage.material = instanceMaterial;
                    }
                    break;
            }

            // 셰이더 속성 초기화
            if (instanceMaterial != null)
            {
                UpdateShaderProperties();
            }
        }

        private void UpdateShaderProperties()
        {
            // NULL 체크
            if (instanceMaterial == null) return;

            // 셰이더 속성 업데이트
            instanceMaterial.SetFloat(WindStrengthId, windStrength);
            instanceMaterial.SetFloat(WindSpeedId, windSpeed);
            instanceMaterial.SetFloat(NoiseScaleId, noiseScale);

            // 텍스처 업데이트 (NULL 체크)
            if (noiseTexture != null)
            {
                instanceMaterial.SetTexture(NoiseTexId, noiseTexture);
            }

            if (gradientTexture != null)
            {
                instanceMaterial.SetTexture(GradientTexId, gradientTexture);
            }
        }

        private void RestoreOriginalMaterial()
        {
            // 대상 컴포넌트 원래 머티리얼로 복원
            switch (previousTargetType)
            {
                case TargetType.SpriteRenderer:
                    if (spriteRenderer != null)
                    {
                        // 에디터 모드에서는 sharedMaterial 사용
                        spriteRenderer.sharedMaterial = originalMaterial;
                    }
                    break;

                case TargetType.UIImage:
                    if (uiImage != null)
                    {
                        uiImage.material = originalMaterial;
                    }
                    break;

                case TargetType.UIRawImage:
                    if (uiRawImage != null)
                    {
                        uiRawImage.material = originalMaterial;
                    }
                    break;
            }

            // 원본 머티리얼 참조 초기화
            originalMaterial = null;
        }

        private void CleanupMaterial()
        {
            // 인스턴스 머티리얼 정리
            if (instanceMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(instanceMaterial);
                }
                else
                {
                    DestroyImmediate(instanceMaterial);
                }
                instanceMaterial = null;
            }
        }

        #region Public Methods

        public void SetWindStrength(float strength)
        {
            windStrength = Mathf.Clamp(strength, 0f, 0.1f);
            if (instanceMaterial != null)
            {
                instanceMaterial.SetFloat(WindStrengthId, windStrength);
            }
        }

        public void SetWindSpeed(float speed)
        {
            windSpeed = Mathf.Clamp(speed, 0f, 10f);
            if (instanceMaterial != null)
            {
                instanceMaterial.SetFloat(WindSpeedId, windSpeed);
            }
        }

        public void SetNoiseScale(float scale)
        {
            noiseScale = Mathf.Clamp(scale, 0.1f, 10f);
            if (instanceMaterial != null)
            {
                instanceMaterial.SetFloat(NoiseScaleId, noiseScale);
            }
        }

        public void SetNoiseTexture(Texture2D texture)
        {
            noiseTexture = texture;
            if (instanceMaterial != null && noiseTexture != null)
            {
                instanceMaterial.SetTexture(NoiseTexId, noiseTexture);
            }
        }

        public void SetGradientTexture(Texture2D texture)
        {
            gradientTexture = texture;
            if (instanceMaterial != null && gradientTexture != null)
            {
                instanceMaterial.SetTexture(GradientTexId, gradientTexture);
            }
        }

        #endregion
    }
}