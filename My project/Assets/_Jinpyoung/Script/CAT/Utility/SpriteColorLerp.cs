using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
#endif

namespace CAT.Utility
{
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteColorLerp : MonoBehaviour
    {
        [Header("Color Settings")]
        [SerializeField] private Color targetColor = Color.red;
        [SerializeField][Range(0f, 1f)] private float lerpValue = 0f;

        [Header("Performance Settings")]
        [SerializeField] private bool alwaysUpdate = false; // 애니메이션 사용 시에만 true
        [SerializeField] private float updateThreshold = 0.001f; // 변화 감지 임계값

        // 이전 값 캐싱 (변화 감지용)
        private Color lastTargetColor;
        private float lastLerpValue = -1f;
        private bool isDirty = true;

        // 애니메이션 시스템에서 접근할 수 있도록 public 프로퍼티로 노출
        public Color TargetColor
        {
            get => targetColor;
            set
            {
                if (targetColor != value)
                {
                    targetColor = value;
                    isDirty = true;
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
                    isDirty = true;
                }
            }
        }

        // 캐시된 컴포넌트들
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;
        private static Material sharedColorLerpMaterial;

        // 셰이더 프로퍼티 ID (성능 최적화)
        private static readonly int TargetColorProperty = Shader.PropertyToID("_TargetColor");
        private static readonly int LerpValueProperty = Shader.PropertyToID("_LerpValue");

        // 셰이더 이름
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
            ForceUpdate();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
        // 에디터에서만 실행 (런타임에서는 컴포넌트 제거 없음)
        if (!Application.isPlaying && spriteRenderer != null)
        {
            // Sprites-Default로 복구
            Material defaultSprite = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (defaultSprite != null)
            {
                spriteRenderer.sharedMaterial = defaultSprite;
            }
            
            // PropertyBlock 제거
            spriteRenderer.SetPropertyBlock(null);
            
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

        // 에디터에서 값이 변경될 때 호출
        private void OnValidate()
        {
            if (!gameObject.activeInHierarchy)
                return;

            isDirty = true;

            // 컴포넌트가 처음 추가될 때도 머티리얼 설정
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            SetupMaterial();
            UpdateMaterialProperties();
        }

        private void Update()
        {
#if UNITY_EDITOR
        // 에디터에서만 실행
        if (!Application.isPlaying)
        {
            // 머티리얼이 변경되었는지 확인
            if (spriteRenderer != null && 
                spriteRenderer.sharedMaterial != sharedColorLerpMaterial &&
                spriteRenderer.sharedMaterial != null &&
                !spriteRenderer.sharedMaterial.name.Contains("Sprites-Default"))
            {
                // 사용자가 수동으로 다른 머티리얼을 변경한 경우 다시 설정
                SetupMaterial();
            }
            
            UpdateMaterialProperties();
        }
#endif
        }

        // 애니메이션 시스템에서 값이 변경될 때 호출
        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                // alwaysUpdate가 true이거나 값이 변경되었을 때만 업데이트
                if (alwaysUpdate || isDirty)
                {
                    UpdateMaterialProperties();
                    isDirty = false;
                }
            }
        }

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

            // 값이 실제로 변경되었는지 확인 (성능 최적화)
            bool hasChanged = false;

            if (lastTargetColor != targetColor)
            {
                lastTargetColor = targetColor;
                hasChanged = true;
            }

            if (Mathf.Abs(lastLerpValue - lerpValue) > updateThreshold)
            {
                lastLerpValue = lerpValue;
                hasChanged = true;
            }

            // 변경된 경우에만 PropertyBlock 업데이트
            if (hasChanged || !Application.isPlaying)
            {
                // MaterialPropertyBlock을 사용하여 인스턴스별 값 설정
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(TargetColorProperty, targetColor);
                propertyBlock.SetFloat(LerpValueProperty, lerpValue);
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        // 강제 업데이트
        public void ForceUpdate()
        {
            isDirty = true;
            lastLerpValue = -1f;
            UpdateMaterialProperties();
        }

        // 컴포넌트가 리셋될 때
        private void Reset()
        {
            targetColor = Color.red;
            lerpValue = 0f;
            alwaysUpdate = false;
            updateThreshold = 0.001f;

            InitializeComponents();
            SetupMaterial();
            ForceUpdate();
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
            isDirty = true;
        }

        // 애니메이션 사용 설정
        public void SetAnimationMode(bool useAnimation)
        {
            alwaysUpdate = useAnimation;
        }

        // 머티리얼 재설정
        [ContextMenu("Refresh Material")]
        public void RefreshMaterial()
        {
            propertyBlock.Clear();
            SetupMaterial();
            ForceUpdate();
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

            // 코루틴 실행 중에는 항상 업데이트
            bool previousAlwaysUpdate = alwaysUpdate;
            alwaysUpdate = true;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                LerpValue = Mathf.Lerp(startValue, targetValue, t);
                yield return null;
            }

            LerpValue = targetValue;

            // 원래 설정으로 복구
            alwaysUpdate = previousAlwaysUpdate;
        }

        // 디버그용 메서드
        [ContextMenu("Debug Info")]
        private void DebugInfo()
        {
            Debug.Log($"=== SpriteColorLerp Debug Info ===");
            Debug.Log($"GameObject: {gameObject.name}");
            Debug.Log($"Always Update: {alwaysUpdate}");
            Debug.Log($"Is Dirty: {isDirty}");
            Debug.Log($"Update Threshold: {updateThreshold}");
            Debug.Log($"Target Color: {targetColor}");
            Debug.Log($"Lerp Value: {lerpValue}");
        }
    }
}