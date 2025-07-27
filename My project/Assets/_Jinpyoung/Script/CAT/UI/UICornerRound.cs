using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.UI
{
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("CAT/UI/CornerRound")]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class UICornerRound : MonoBehaviour
    {
        // 공유 머티리얼 관리를 위한 정적 딕셔너리
        private static Dictionary<string, Material> _sharedMaterials = new Dictionary<string, Material>();

        [Header("Corner Radii")]
        [Tooltip("Top-Left corner radius in pixels")]
        public float topLeftRadius = 10f;

        [Tooltip("Top-Right corner radius in pixels")]
        public float topRightRadius = 10f;

        [Tooltip("Bottom-Left corner radius in pixels")]
        public float bottomLeftRadius = 10f;

        [Tooltip("Bottom-Right corner radius in pixels")]
        public float bottomRightRadius = 10f;

        [Header("Size")]
        [Tooltip("Size of the rectangle in pixels")]
        public float size = 100f;

        [Header("Optimization")]
        [Tooltip("공유 머티리얼 사용 (성능 최적화)")]
        public bool useSharedMaterial = true;

        [Header("References")]
        [Tooltip("Custom shader for rounded rectangles")]
        public Shader cornerRoundShader;

        private Image _image;
        private Material _material;
        private bool _initialized = false;

        // 이전 값들 (변경 감지용)
        private float _prevTLRadius;
        private float _prevTRRadius;
        private float _prevBLRadius;
        private float _prevBRRadius;
        private float _prevSize;
        private bool _prevSharedMaterial;

        // Material property IDs (cached for performance)
        private static readonly int RadiusTLID = Shader.PropertyToID("_RadiusTL");
        private static readonly int RadiusTRID = Shader.PropertyToID("_RadiusTR");
        private static readonly int RadiusBLID = Shader.PropertyToID("_RadiusBL");
        private static readonly int RadiusBRID = Shader.PropertyToID("_RadiusBR");
        private static readonly int SizeID = Shader.PropertyToID("_Size");

        // 정적 생성자 - 정리 로직 등록
        static UICornerRound()
        {
            // 에디터에서 플레이 모드 종료 시 정리
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += (state) => {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    CleanupAllSharedMaterials();
                }
            };
#endif
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            // 에디터에서도 초기화되도록 설정
            if (!Application.isPlaying)
            {
                Initialize();
            }
        }

        private void Start()
        {
            // 이전 값 초기화
            SaveCurrentValues();
        }

        // 현재 값 저장 (변경 감지용)
        private void SaveCurrentValues()
        {
            _prevTLRadius = topLeftRadius;
            _prevTRRadius = topRightRadius;
            _prevBLRadius = bottomLeftRadius;
            _prevBRRadius = bottomRightRadius;
            _prevSize = size;
            _prevSharedMaterial = useSharedMaterial;
        }

        // 값이 변경되었는지 확인
        private bool ValuesChanged()
        {
            return _prevTLRadius != topLeftRadius ||
                   _prevTRRadius != topRightRadius ||
                   _prevBLRadius != bottomLeftRadius ||
                   _prevBRRadius != bottomRightRadius ||
                   _prevSize != size ||
                   _prevSharedMaterial != useSharedMaterial;
        }

        private void OnValidate()
        {
            // Update properties when changed in inspector
            if (!_initialized)
            {
                Initialize();
            }
            else if (ValuesChanged())
            {
                CleanupMaterial();
                UpdateMaterial();
                SaveCurrentValues();
            }
            else
            {
                UpdateShaderProperties();
            }

            // 에디터에서 변경사항 즉시 반영
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
#endif
            }
        }

        private void Initialize()
        {
            if (_initialized) return;

            // Get required components
            _image = GetComponent<Image>();
            if (_image == null) return;

            // Create material from shader
            if (cornerRoundShader == null)
            {
                cornerRoundShader = Shader.Find("CAT/UI/CornerRound");
                if (cornerRoundShader == null)
                {
                    Debug.LogError("CornerRound Shader not found! Please assign it manually.");
                    return;
                }
            }

            // 기존 머티리얼 파괴 (메모리 누수 방지)
            CleanupMaterial();

            // 머티리얼 생성 및 할당
            UpdateMaterial();

            // 현재 값 저장
            SaveCurrentValues();

            _initialized = true;
        }

        // 머티리얼 키 생성 (반경 값들의 조합)
        private string GetMaterialKey()
        {
            // 소수점 1자리까지만 고려 (성능과 정확도의 균형)
            return string.Format("{0:F1}_{1:F1}_{2:F1}_{3:F1}_{4:F1}",
                               topLeftRadius, topRightRadius, bottomLeftRadius, bottomRightRadius, size);
        }

        // 주어진 속성에 맞는 공유 머티리얼을 가져오거나 생성
        private Material GetSharedMaterial()
        {
            string key = GetMaterialKey();

            // 이미 같은 설정의 머티리얼이 있는지 확인
            if (_sharedMaterials.ContainsKey(key))
            {
                return _sharedMaterials[key];
            }

            // 없으면 새로 생성
            if (cornerRoundShader == null)
            {
                cornerRoundShader = Shader.Find("CAT/UI/CornerRound");
                if (cornerRoundShader == null)
                {
                    Debug.LogError("CornerRound Shader를 찾을 수 없습니다!");
                    return null;
                }
            }

            // 새 머티리얼 생성
            Material newMaterial = new Material(cornerRoundShader);
            newMaterial.name = "CornerRound_" + key;

            // 속성 설정
            newMaterial.SetFloat(RadiusTLID, topLeftRadius);
            newMaterial.SetFloat(RadiusTRID, topRightRadius);
            newMaterial.SetFloat(RadiusBLID, bottomLeftRadius);
            newMaterial.SetFloat(RadiusBRID, bottomRightRadius);
            newMaterial.SetFloat(SizeID, size);

            // 딕셔너리에 저장
            _sharedMaterials.Add(key, newMaterial);

            return newMaterial;
        }

        // 머티리얼 정리 (메모리 누수 방지)
        private void CleanupMaterial()
        {
            if (_material != null && !useSharedMaterial)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(_material);
                else
                    Destroy(_material);
#else
                Destroy(_material);
#endif
                _material = null;
            }
        }

        // 모든 공유 머티리얼 정리 (정적 메서드)
        public static void CleanupAllSharedMaterials()
        {
            foreach (var material in _sharedMaterials.Values)
            {
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }

            _sharedMaterials.Clear();
        }

        // 머티리얼 생성 또는 공유 머티리얼 가져오기
        private void UpdateMaterial()
        {
            if (_image == null) return;

            if (useSharedMaterial)
            {
                // 공유 머티리얼 사용
                _material = GetSharedMaterial();
            }
            else
            {
                // 개별 머티리얼 생성
                _material = new Material(cornerRoundShader);
                UpdateShaderProperties();
            }

            // 이미지에 머티리얼 할당
            if (_image != null)
            {
                _image.material = _material;
            }
        }

        public void UpdateShaderProperties()
        {
            if (_material == null) return;

            // 공유 머티리얼은 이미 속성이 설정되어 있으므로 개별 머티리얼만 업데이트
            if (!useSharedMaterial)
            {
                // Update material properties
                _material.SetFloat(RadiusTLID, topLeftRadius);
                _material.SetFloat(RadiusTRID, topRightRadius);
                _material.SetFloat(RadiusBLID, bottomLeftRadius);
                _material.SetFloat(RadiusBRID, bottomRightRadius);
                _material.SetFloat(SizeID, size);
            }
        }

        /// <summary>
        /// Set the same radius for all corners
        /// </summary>
        /// <param name="radius">Radius in pixels</param>
        public void SetUniformRadius(float radius)
        {
            topLeftRadius = radius;
            topRightRadius = radius;
            bottomLeftRadius = radius;
            bottomRightRadius = radius;

            if (ValuesChanged())
            {
                CleanupMaterial();
                UpdateMaterial();
                SaveCurrentValues();
            }
            else
            {
                UpdateShaderProperties();
            }
        }

        /// <summary>
        /// Set individual radii for each corner
        /// </summary>
        public void SetCornerRadii(float topLeft, float topRight, float bottomLeft, float bottomRight)
        {
            topLeftRadius = topLeft;
            topRightRadius = topRight;
            bottomLeftRadius = bottomLeft;
            bottomRightRadius = bottomRight;

            if (ValuesChanged())
            {
                CleanupMaterial();
                UpdateMaterial();
                SaveCurrentValues();
            }
            else
            {
                UpdateShaderProperties();
            }
        }

        /// <summary>
        /// Set the size of the rectangle
        /// </summary>
        /// <param name="newSize">Size in pixels</param>
        public void SetSize(float newSize)
        {
            size = newSize;

            if (ValuesChanged())
            {
                CleanupMaterial();
                UpdateMaterial();
                SaveCurrentValues();
            }
            else
            {
                UpdateShaderProperties();
            }
        }

        /// <summary>
        /// Automatically set size based on RectTransform dimensions
        /// </summary>
        public void AutoSetSize()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Use the smaller dimension to ensure the corner radius looks the same
                float newSize = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);

                if (size != newSize)
                {
                    size = newSize;

                    if (ValuesChanged())
                    {
                        CleanupMaterial();
                        UpdateMaterial();
                        SaveCurrentValues();
                    }
                    else
                    {
                        UpdateShaderProperties();
                    }
                }
            }
        }

        /// <summary>
        /// Update the component when the RectTransform is resized
        /// </summary>
        public void OnRectTransformDimensionsChange()
        {
            if (_initialized && _material != null)
            {
                AutoSetSize();

                // 에디터에서 변경사항 즉시 반영
                if (!Application.isPlaying)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
#endif
                }
            }
        }

        // 오브젝트가 파괴될 때 메모리 누수 방지
        private void OnDestroy()
        {
            CleanupMaterial();
        }

        // 공유 머티리얼 사용 설정 변경
        public void SetUseSharedMaterial(bool shared)
        {
            if (useSharedMaterial != shared)
            {
                useSharedMaterial = shared;
                CleanupMaterial();
                UpdateMaterial();
                SaveCurrentValues();
            }
        }
    }
}