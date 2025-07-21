using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
#endif

namespace CAT.Utilities
{
    /// <summary>
    /// SpriteRenderer의 색상을 지정된 색상으로 선형 보간하는 컴포넌트입니다.
    /// </summary>

    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteColorLerp : MonoBehaviour
    {
        [Header("Color Settings")]
        [SerializeField] private Color targetColor = Color.red;
        [SerializeField][Range(0f, 1f)] private float lerpValue = 0f;

        // 애니메이션 시스템에서 접근할 수 있도록 public 프로퍼티로 노출
        public Color TargetColor
        {
            get => targetColor;
            set
            {
                targetColor = value;
                UpdateMaterialProperties();
            }
        }

        public float LerpValue
        {
            get => lerpValue;
            set
            {
                lerpValue = Mathf.Clamp01(value);
                UpdateMaterialProperties();
            }
        }

        // 캐시된 컴포넌트들
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Material originalMaterial;
        private static Material sharedColorLerpMaterial;

        // 셰이더 프로퍼티 이름
        private const string TARGET_COLOR_PROPERTY = "_TargetColor";
        private const string LERP_VALUE_PROPERTY = "_LerpValue";
        private const string SHADER_NAME = "CAT/2D/SpriteColorLerp";

        private void Awake()
        {
            InitializeComponents();
            SetupMaterial();
        }

        private void OnEnable()
        {
            InitializeComponents();
            SetupMaterial();
            UpdateMaterialProperties();
        }

        private void OnDisable()
        {
            // 컴포넌트가 비활성화될 때는 머티리얼을 유지
        }

        private void OnDestroy()
        {
            // 컴포넌트가 제거될 때 Sprites-Default로 복구
            if (spriteRenderer != null)
            {
                // Sprites-Default 머티리얼 직접 할당
                Material defaultSprite = null;

    #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    // 에디터에서는 AssetDatabase를 통해 로드
                    defaultSprite = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                }
                else
    #endif
                {
                    // 런타임에서는 Resources를 통해 로드
                    defaultSprite = Resources.GetBuiltinResource<Material>("Sprites-Default.mat");
                }

                if (defaultSprite != null)
                {
                    spriteRenderer.sharedMaterial = defaultSprite;
                }

                // PropertyBlock 제거
                spriteRenderer.SetPropertyBlock(null);

    #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    // 에디터에서 변경사항 즉시 적용
                    EditorUtility.SetDirty(spriteRenderer);
                    EditorUtility.SetDirty(gameObject);

                    // 씬 더티 마킹
                    var scene = gameObject.scene;
                    if (scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
    #endif
            }
        }

        private void InitializeComponents()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }

        private void SetupMaterial()
        {
            if (spriteRenderer == null)
                return;

            // 현재 머티리얼이 이미 ColorLerp 셰이더를 사용하는지 확인
            if (spriteRenderer.sharedMaterial != null &&
                spriteRenderer.sharedMaterial.shader.name.Contains(SHADER_NAME))
            {
                // 이미 올바른 셰이더 사용 중
                return;
            }

            // 원본 머티리얼 저장 (나중에 복구용)
            if (originalMaterial == null && spriteRenderer.sharedMaterial != null)
            {
                originalMaterial = spriteRenderer.sharedMaterial;
            }

            // 공유 머티리얼이 없으면 생성
            if (sharedColorLerpMaterial == null)
            {
                CreateOrLoadSharedMaterial();
            }

            // 머티리얼 적용
            if (sharedColorLerpMaterial != null)
            {
                spriteRenderer.sharedMaterial = sharedColorLerpMaterial;

    #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(spriteRenderer);
                }
    #endif
            }
        }

        private void CreateOrLoadSharedMaterial()
        {
            Shader colorLerpShader = Shader.Find(SHADER_NAME);

            if (colorLerpShader == null)
            {
                Debug.LogError($"Cannot find shader '{SHADER_NAME}'. Make sure the shader is included in the project.");
                return;
            }

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // 에디터: 셰이더와 같은 디렉토리에 머티리얼 저장
                string shaderPath = AssetDatabase.GetAssetPath(colorLerpShader);
                if (!string.IsNullOrEmpty(shaderPath))
                {
                    string shaderDirectory = Path.GetDirectoryName(shaderPath);
                    string materialPath = Path.Combine(shaderDirectory, "SpriteColorLerp.mat");

                    // 기존 머티리얼 확인
                    sharedColorLerpMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                    if (sharedColorLerpMaterial == null)
                    {
                        // 새 머티리얼 생성
                        sharedColorLerpMaterial = new Material(colorLerpShader);
                        sharedColorLerpMaterial.name = "SpriteColorLerp";

                        // 머티리얼을 에셋으로 저장
                        AssetDatabase.CreateAsset(sharedColorLerpMaterial, materialPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        Debug.Log($"Created shared material at: {materialPath}");
                    }
                }
                else
                {
                    // 셰이더 경로를 찾을 수 없는 경우 임시 머티리얼 생성
                    sharedColorLerpMaterial = new Material(colorLerpShader);
                    sharedColorLerpMaterial.name = "SpriteColorLerp (Temp)";
                }
            }
            else
    #endif
            {
                // 런타임: 메모리에 머티리얼 생성
                sharedColorLerpMaterial = new Material(colorLerpShader);
                sharedColorLerpMaterial.name = "SpriteColorLerp (Runtime)";
            }
        }

        private void RestoreOriginalMaterial()
        {
            if (spriteRenderer == null)
                return;

            // 항상 Sprites-Default로 복구
            Material defaultSprite = null;

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // 에디터에서는 AssetDatabase를 통해 로드
                defaultSprite = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            }
            else
    #endif
            {
                // 런타임에서는 Resources를 통해 로드
                defaultSprite = Resources.GetBuiltinResource<Material>("Sprites-Default.mat");
            }

            if (defaultSprite != null)
            {
                spriteRenderer.sharedMaterial = defaultSprite;
            }

            // PropertyBlock 제거
            if (propertyBlock != null)
            {
                spriteRenderer.SetPropertyBlock(null);
            }

    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(spriteRenderer);
                EditorUtility.SetDirty(gameObject);
            }
    #endif
        }

        // 에디터에서 값이 변경될 때 호출
        private void OnValidate()
        {
            if (!gameObject.activeInHierarchy)
                return;

            // 컴포넌트가 처음 추가될 때도 머티리얼 설정
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            SetupMaterial();
            UpdateMaterialProperties();
        }

    #if UNITY_EDITOR
        private void Update()
        {
            // 에디터에서만 실행
            if (!Application.isPlaying)
            {
                // 머티리얼이 변경되었는지 확인
                if (spriteRenderer != null &&
                    spriteRenderer.sharedMaterial != sharedColorLerpMaterial &&
                    (originalMaterial == null || spriteRenderer.sharedMaterial != originalMaterial))
                {
                    // 사용자가 수동으로 머티리얼을 변경한 경우
                    originalMaterial = spriteRenderer.sharedMaterial;
                    SetupMaterial();
                }

                UpdateMaterialProperties();
            }
        }
    #endif

        private void UpdateMaterialProperties()
        {
            if (spriteRenderer == null || propertyBlock == null)
                return;

            // 올바른 셰이더를 사용하는지 확인
            if (spriteRenderer.sharedMaterial == null ||
                !spriteRenderer.sharedMaterial.shader.name.Contains(SHADER_NAME))
            {
                return;
            }

            // MaterialPropertyBlock을 사용하여 인스턴스별 값 설정
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TARGET_COLOR_PROPERTY, targetColor);
            propertyBlock.SetFloat(LERP_VALUE_PROPERTY, lerpValue);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        // 컴포넌트가 리셋될 때
        private void Reset()
        {
            targetColor = Color.red;
            lerpValue = 0f;

            // 원본 머티리얼 저장
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
            {
                originalMaterial = spriteRenderer.sharedMaterial;
            }

            InitializeComponents();
            SetupMaterial();
            UpdateMaterialProperties();
        }

        // 런타임에서 값 변경을 위한 헬퍼 메서드들
        public void SetLerpValue(float value)
        {
            LerpValue = value;
        }

        public void SetTargetColor(Color color)
        {
            TargetColor = color;
        }

        public void SetTargetColorAndLerp(Color color, float lerp)
        {
            targetColor = color;
            lerpValue = Mathf.Clamp01(lerp);
            UpdateMaterialProperties();
        }

        // 머티리얼 재설정
        [ContextMenu("Refresh Material")]
        public void RefreshMaterial()
        {
            propertyBlock.Clear();
            SetupMaterial();
            UpdateMaterialProperties();
        }

        // 원본 머티리얼로 복구
        [ContextMenu("Restore Original Material")]
        public void RestoreOriginal()
        {
            RestoreOriginalMaterial();
        }

        // 애니메이션을 위한 편의 메서드
        public void LerpToTargetColor(float duration)
        {
            if (Application.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(LerpCoroutine(1f, duration));
            }
        }

        public void LerpToOriginalColor(float duration)
        {
            if (Application.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(LerpCoroutine(0f, duration));
            }
        }

        private System.Collections.IEnumerator LerpCoroutine(float targetValue, float duration)
        {
            float startValue = lerpValue;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                LerpValue = Mathf.Lerp(startValue, targetValue, t);
                yield return null;
            }

            LerpValue = targetValue;
        }

        // 디버그용 메서드
        [ContextMenu("Debug Info")]
        private void DebugInfo()
        {
            Debug.Log($"=== SpriteColorLerp Debug Info ===");
            Debug.Log($"GameObject: {gameObject.name}");
            Debug.Log($"SpriteRenderer: {(spriteRenderer != null ? "Found" : "NULL")}");

            if (spriteRenderer != null)
            {
                Debug.Log($"Current Material: {(spriteRenderer.sharedMaterial != null ? spriteRenderer.sharedMaterial.name : "NULL")}");
                Debug.Log($"Current Shader: {(spriteRenderer.sharedMaterial != null ? spriteRenderer.sharedMaterial.shader.name : "NULL")}");
                Debug.Log($"Original Material: {(originalMaterial != null ? originalMaterial.name : "NULL")}");
            }

            Debug.Log($"Target Color: {targetColor}");
            Debug.Log($"Lerp Value: {lerpValue}");
            Debug.Log($"Using PropertyBlock: Yes");
        }
    }
}