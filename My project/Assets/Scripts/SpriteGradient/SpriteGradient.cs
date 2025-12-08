using UnityEngine;

namespace CAT.Effect
{
    /// <summary>
    /// SpriteRenderer의 텍스처에 2개의 컬러를 Gradient로 Multiply하는 효과
    /// Vertical/Horizontal 방향과 Lerp 옵션 지원
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("CAT/Effect/SpriteGradient")]
    public class SpriteGradient : MonoBehaviour
    {
        public static readonly string SHADER_NAME = "CAT/Effect/SpriteGradient";

        [Header("Gradient 설정")]
        [SerializeField] private Color color1 = Color.white;
        [SerializeField] private Color color2 = Color.black;
        
        [Header("Gradient 방향")]
        [SerializeField] private GradientDirection gradientDirection = GradientDirection.Vertical;
        
        [Header("Lerp 설정")]
        [SerializeField, Range(0f, 1f)] private float lerpValue = 0f;
        
        [Header("성능 설정")]
        [SerializeField] private bool alwaysUpdate = false;
        [SerializeField] private float updateThreshold = 0.001f;

        // 캐시된 컴포넌트
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Material gradientMaterial;
        private Material originalMaterial;
        private bool initialized = false;

        // 셰이더 캐싱 (성능 최적화 - Shader.Find는 한 번만 호출)
        private static Shader cachedShader;

        // 셰이더 프로퍼티 ID (성능 최적화)
        private static readonly int Color1Property = Shader.PropertyToID("_Color1");
        private static readonly int Color2Property = Shader.PropertyToID("_Color2");
        private static readonly int GradientDirectionProperty = Shader.PropertyToID("_GradientDirection");
        private static readonly int LerpValueProperty = Shader.PropertyToID("_LerpValue");

        // 이전 값 캐싱 (변화 감지용)
        private Color lastColor1;
        private Color lastColor2;
        private GradientDirection lastGradientDirection;
        private float lastLerpValue = -1f;

        public enum GradientDirection
        {
            Vertical = 0,
            Horizontal = 1
        }

        #region Public Properties

        public Color Color1
        {
            get => color1;
            set
            {
                if (color1 != value)
                {
                    color1 = value;
                    UpdateMaterialProperties();
                }
            }
        }

        public Color Color2
        {
            get => color2;
            set
            {
                if (color2 != value)
                {
                    color2 = value;
                    UpdateMaterialProperties();
                }
            }
        }

        public GradientDirection Direction
        {
            get => gradientDirection;
            set
            {
                if (gradientDirection != value)
                {
                    gradientDirection = value;
                    UpdateMaterialProperties();
                }
            }
        }

        public float LerpValue
        {
            get => lerpValue;
            set
            {
                float newValue = Mathf.Clamp01(value);
                if (Mathf.Abs(lerpValue - newValue) > updateThreshold)
                {
                    lerpValue = newValue;
                    UpdateMaterialProperties();
                }
            }
        }

        #endregion

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            RestoreOriginalMaterial();
        }

        private void OnDestroy()
        {
            RestoreOriginalMaterial();
            
            if (gradientMaterial != null && Application.isPlaying)
            {
                Destroy(gradientMaterial);
            }
            
            gradientMaterial = null;
        }

        private void Update()
        {
            if (!initialized) return;

            // 변경사항 감지만 하고 실제 업데이트는 LateUpdate에서 수행 (성능 최적화)
            if (alwaysUpdate && HasChanges())
            {
                UpdateMaterialProperties();
            }
        }

        private void LateUpdate()
        {
            if (!initialized) return;

            // 렌더링 전에 최종 업데이트 수행 (성능 최적화)
            if (HasChanges())
            {
                UpdateMaterialProperties();
                UpdateLastValues();
            }
        }

        private void OnValidate()
        {
            if (!initialized && Application.isPlaying == false)
            {
                Initialize();
            }
            
            if (initialized)
            {
                UpdateMaterialProperties();
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneView.RepaintAll();
                #endif
            }
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void Initialize()
        {
            if (initialized) return;

            if (!Application.isPlaying && !gameObject.scene.IsValid())
            {
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            // 셰이더 캐싱 (성능 최적화 - 한 번만 찾음)
            if (cachedShader == null)
            {
                cachedShader = Shader.Find(SHADER_NAME);
                if (cachedShader == null)
                {
                    Debug.LogError($"{SHADER_NAME} 셰이더를 찾을 수 없습니다!");
                    return;
                }
            }

            // PropertyBlock 초기화
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            // 원본 머티리얼 저장
            if (originalMaterial == null)
            {
                originalMaterial = spriteRenderer.sharedMaterial;
            }

            // Gradient 머티리얼 설정 (각 오브젝트마다 고유한 인스턴스 생성)
            SetupMaterial();

            initialized = true;
            UpdateMaterialProperties();
            UpdateLastValues();
        }

        /// <summary>
        /// Gradient 머티리얼 설정 (각 오브젝트마다 고유한 머티리얼 인스턴스 생성)
        /// </summary>
        private void SetupMaterial()
        {
            if (cachedShader == null)
            {
                cachedShader = Shader.Find(SHADER_NAME);
                if (cachedShader == null)
                {
                    Debug.LogError($"{SHADER_NAME} 셰이더를 찾을 수 없습니다!");
                    return;
                }
            }

            // 각 오브젝트마다 고유한 머티리얼 인스턴스 생성
            if (gradientMaterial == null)
            {
                gradientMaterial = new Material(cachedShader);
                gradientMaterial.name = $"SpriteGradient_Material_{gameObject.GetInstanceID()}";
            }

            // 원본 텍스처 복사 (각 오브젝트의 고유한 텍스처 사용)
            if (spriteRenderer.sprite != null && spriteRenderer.sprite.texture != null)
            {
                gradientMaterial.SetTexture("_MainTex", spriteRenderer.sprite.texture);
            }
            else if (originalMaterial != null && originalMaterial.HasProperty("_MainTex"))
            {
                gradientMaterial.SetTexture("_MainTex", originalMaterial.GetTexture("_MainTex"));
            }

            // 머티리얼 적용 (에디터 모드에서는 sharedMaterial 사용)
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                spriteRenderer.sharedMaterial = gradientMaterial;
            }
            else
            {
                spriteRenderer.material = gradientMaterial;
            }
            #else
            spriteRenderer.material = gradientMaterial;
            #endif
        }

        /// <summary>
        /// 머티리얼 프로퍼티 업데이트
        /// </summary>
        private void UpdateMaterialProperties()
        {
            if (!initialized || spriteRenderer == null || propertyBlock == null)
            {
                return;
            }

            // Gradient 머티리얼이 제대로 설정되었는지 확인 (에디터 모드에서는 sharedMaterial 사용)
            Material currentMaterial = null;
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentMaterial = spriteRenderer.sharedMaterial;
            }
            else
            {
                currentMaterial = spriteRenderer.material;
            }
            #else
            currentMaterial = spriteRenderer.material;
            #endif
            
            if (currentMaterial == null || 
                !currentMaterial.shader.name.Contains("SpriteGradient"))
            {
                SetupMaterial();
                return;
            }

            // 값이 실제로 변경되었는지 확인 (성능 최적화)
            bool hasChanged = false;

            if (lastColor1 != color1)
            {
                lastColor1 = color1;
                hasChanged = true;
            }

            if (lastColor2 != color2)
            {
                lastColor2 = color2;
                hasChanged = true;
            }

            if (lastGradientDirection != gradientDirection)
            {
                lastGradientDirection = gradientDirection;
                hasChanged = true;
            }

            if (Mathf.Abs(lastLerpValue - lerpValue) > updateThreshold)
            {
                lastLerpValue = lerpValue;
                hasChanged = true;
            }

            // 변경된 경우에만 PropertyBlock 업데이트
            if (hasChanged || !Application.isPlaying || alwaysUpdate)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(Color1Property, color1);
                propertyBlock.SetColor(Color2Property, color2);
                propertyBlock.SetFloat(GradientDirectionProperty, (float)gradientDirection);
                propertyBlock.SetFloat(LerpValueProperty, lerpValue);
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>
        /// 원본 머티리얼 복원
        /// </summary>
        private void RestoreOriginalMaterial()
        {
            if (spriteRenderer != null && originalMaterial != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    spriteRenderer.sharedMaterial = originalMaterial;
                }
                else
                {
                    spriteRenderer.material = originalMaterial;
                }
                #else
                spriteRenderer.material = originalMaterial;
                #endif
            }
        }

        /// <summary>
        /// 변경사항 확인
        /// </summary>
        private bool HasChanges()
        {
            return color1 != lastColor1 ||
                   color2 != lastColor2 ||
                   gradientDirection != lastGradientDirection ||
                   Mathf.Abs(lerpValue - lastLerpValue) > updateThreshold;
        }

        /// <summary>
        /// 이전 값 업데이트
        /// </summary>
        private void UpdateLastValues()
        {
            lastColor1 = color1;
            lastColor2 = color2;
            lastGradientDirection = gradientDirection;
            lastLerpValue = lerpValue;
        }

        /// <summary>
        /// 강제 업데이트
        /// </summary>
        public void ForceUpdate()
        {
            lastLerpValue = -1f;
            UpdateMaterialProperties();
        }

        /// <summary>
        /// 런타임에서 값 변경을 위한 헬퍼 메서드들
        /// </summary>
        public void SetColor1(Color color)
        {
            Color1 = color;
        }

        public void SetColor2(Color color)
        {
            Color2 = color;
        }

        public void SetGradientDirection(GradientDirection direction)
        {
            Direction = direction;
        }

        public void SetLerpValue(float value)
        {
            LerpValue = value;
        }

        /// <summary>
        /// 컴포넌트 리셋
        /// </summary>
        private void Reset()
        {
            color1 = Color.white;
            color2 = Color.black;
            gradientDirection = GradientDirection.Vertical;
            lerpValue = 0f;
            alwaysUpdate = false;
            updateThreshold = 0.001f;

            Initialize();
            SetupMaterial();
            ForceUpdate();
        }
    }
}

