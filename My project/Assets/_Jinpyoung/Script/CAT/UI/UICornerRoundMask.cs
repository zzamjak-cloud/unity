using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CAT.UI
{
    /// <summary>
    /// 라운드 코너가 있는 마스크 컴포넌트 (정적 머티리얼 캐시 최적화)
    /// </summary>
    [RequireComponent(typeof(Image), typeof(Mask))]
    [AddComponentMenu("CAT/UI/CornerRoundMask")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class UICornerRoundMask : MonoBehaviour
    {
        // 정적 머티리얼 캐시 (성능 최적화)
        private static Dictionary<(float, float, float, float), Material> materialCache =
            new Dictionary<(float, float, float, float), Material>();

        [Header("코너 설정")]
        [SerializeField]
        private bool uniformCorners = true;

        [SerializeField, Range(0, 50), Tooltip("모든 코너에 적용될 반경 값")]
        private float cornerRadius = 10f;

        // [Header("개별 코너 설정 (Uniform 해제 시)")]
        [SerializeField, Range(0, 50), Tooltip("왼쪽 상단 코너 반경")]
        private float topLeftRadius = 10f;

        [SerializeField, Range(0, 50), Tooltip("오른쪽 상단 코너 반경")]
        private float topRightRadius = 10f;

        [SerializeField, Range(0, 50), Tooltip("왼쪽 하단 코너 반경")]
        private float bottomLeftRadius = 10f;

        [SerializeField, Range(0, 50), Tooltip("오른쪽 하단 코너 반경")]
        private float bottomRightRadius = 10f;

        private Material maskMaterial;
        private Image targetImage;
        private Mask targetMask;

        // 셰이더 프로퍼티 ID
        private static readonly int RadiusTLProperty = Shader.PropertyToID("_RadiusTL");
        private static readonly int RadiusTRProperty = Shader.PropertyToID("_RadiusTR");
        private static readonly int RadiusBLProperty = Shader.PropertyToID("_RadiusBL");
        private static readonly int RadiusBRProperty = Shader.PropertyToID("_RadiusBR");

        // 프로퍼티 구현
        public bool UniformCorners
        {
            get => uniformCorners;
            set
            {
                if (uniformCorners != value)
                {
                    uniformCorners = value;
                    UpdateMaterial();
                }
            }
        }

        public float CornerRadius
        {
            get => cornerRadius;
            set
            {
                value = Mathf.Clamp(value, 0f, 50f);
                if (cornerRadius != value)
                {
                    cornerRadius = value;
                    if (uniformCorners)
                    {
                        topLeftRadius = topRightRadius = bottomLeftRadius = bottomRightRadius = value;
                    }
                    UpdateMaterial();
                }
            }
        }

        public float TopLeftRadius
        {
            get => topLeftRadius;
            set
            {
                value = Mathf.Clamp(value, 0f, 50f);
                if (topLeftRadius != value)
                {
                    topLeftRadius = value;
                    UpdateMaterial();
                }
            }
        }

        public float TopRightRadius
        {
            get => topRightRadius;
            set
            {
                value = Mathf.Clamp(value, 0f, 50f);
                if (topRightRadius != value)
                {
                    topRightRadius = value;
                    UpdateMaterial();
                }
            }
        }

        public float BottomLeftRadius
        {
            get => bottomLeftRadius;
            set
            {
                value = Mathf.Clamp(value, 0f, 50f);
                if (bottomLeftRadius != value)
                {
                    bottomLeftRadius = value;
                    UpdateMaterial();
                }
            }
        }

        public float BottomRightRadius
        {
            get => bottomRightRadius;
            set
            {
                value = Mathf.Clamp(value, 0f, 50f);
                if (bottomRightRadius != value)
                {
                    bottomRightRadius = value;
                    UpdateMaterial();
                }
            }
        }

        private void OnEnable()
        {
            InitializeComponents();
            UpdateMaterial();
        }

        private void Start()
        {
            InitializeComponents();
            UpdateMaterial();
        }

        // 에디터 모드 업데이트 로직 최소화
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!enabled) return;

            // Uniform 모드에서는 모든 코너에 동일한 값 적용
            if (uniformCorners)
            {
                topLeftRadius = topRightRadius = bottomLeftRadius = bottomRightRadius = cornerRadius;
            }

            InitializeComponents();
            UpdateMaterial();
        }
#endif

        private void InitializeComponents()
        {
            targetImage ??= GetComponent<Image>();
            targetMask ??= GetComponent<Mask>();
        }

        private void UpdateMaterial()
        {
            if (targetImage == null) return;

            // 캐시 키 생성 (모든 코너 반경 값)
            var cacheKey = (topLeftRadius, topRightRadius, bottomLeftRadius, bottomRightRadius);

            // 캐시된 머티리얼 확인
            if (!materialCache.TryGetValue(cacheKey, out maskMaterial))
            {
                Shader shader = Shader.Find("CAT/UI/CornerRoundMask");
                if (shader != null)
                {
                    // 새 머티리얼 생성 및 캐시
                    maskMaterial = new Material(shader);
                    maskMaterial.hideFlags = HideFlags.HideAndDontSave;

                    // 코너 반경 설정
                    maskMaterial.SetFloat(RadiusTLProperty, topLeftRadius);
                    maskMaterial.SetFloat(RadiusTRProperty, topRightRadius);
                    maskMaterial.SetFloat(RadiusBLProperty, bottomLeftRadius);
                    maskMaterial.SetFloat(RadiusBRProperty, bottomRightRadius);

                    // 캐시에 추가
                    materialCache[cacheKey] = maskMaterial;
                }
                else
                {
                    Debug.LogError("CAT/UI/CornerRoundMask 셰이더를 찾을 수 없습니다.");
                    return;
                }
            }

            // 이미지 컴포넌트에 캐시된 머티리얼 적용
            targetImage.material = maskMaterial;
        }

        // 앱 종료 시 머티리얼 정리 (선택적)
        private void OnApplicationQuit()
        {
            foreach (var material in materialCache.Values)
            {
                Destroy(material);
            }
            materialCache.Clear();
        }

        private void OnDestroy()
        {
            // 런타임에서만 머티리얼 정리
            if (Application.isPlaying && maskMaterial != null)
            {
                Destroy(maskMaterial);
            }
        }
    }
}