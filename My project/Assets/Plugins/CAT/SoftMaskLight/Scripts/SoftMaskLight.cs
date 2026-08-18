using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using TMPro;
#if UIEFFECT_ENABLED
// UIEffect는 선택적 의존성 — asmdef versionDefines(com.coffee.ui-effect)로 심볼 정의
using Coffee.UIEffects;
#endif

namespace SoftMaskLight
{
    /// <summary>
    /// 알파 채널 기반 SoftMaskLight 컴포넌트
    /// - 부모 오브젝트가 Mask 역할 (자신의 이미지 알파 = 마스킹 영역)
    /// - 자식 오브젝트는 부모 마스크 내에서만 렌더링됨
    /// - 부모/자식 이동, 회전 시 동적으로 마스킹 갱신
    /// - 중첩 SoftMaskLight 지원 (최대 2단계)
    /// - Optional Shader 패턴: 원본 셰이더의 Hidden 변형 셰이더로 교체하여 마스킹 지원
    /// - 더티 체크로 불필요한 Material 업데이트 스킵
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UnityEngine.UI.Graphic))]
    [AddComponentMenu("SoftMaskLight/SoftMaskLight")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "CAT.UI", null, "SoftMask")]
    public class SoftMaskLight : MonoBehaviour
    {
        public const string VERSION = "2.4.0";

        // 셰이더/키워드 상수
        public const string SHADER_NAME = "SoftMaskLight/UI/Default";
        public const string TMP_SHADER_NAME = "SoftMaskLight/UI/TMP_SoftMask";
        private const string KEYWORD_NESTED = "_SOFTMASK_NESTED";
        private const string KEYWORD_SLICE = "_SOFTMASK_SLICE";
        private const string KEYWORD_CAT_SOFTMASK = "_CAT_SOFTMASK";
        // 슬라이스는 _SOFTMASK_SLICE 하나로 마스크 1·2를 모두 제어한다 (셰이더 변형 수 절감).
        // 슬라이스가 아닌 쪽은 항등 파라미터(0,0,1,1)를 넣어 리매핑을 무효화한다.
        private static readonly Vector4 IdentitySlice = new Vector4(0f, 0f, 1f, 1f);
        // 항등 슬라이스 기울기 (k1=0, k2=1, k3=0): 리매핑이 항등 함수가 되는 사전 계산 값
        // w = 타일 수 - ε (frac 반복의 끝 경계 가드). 항등 = 타일 1개
        private static readonly Vector4 IdentitySliceSlope = new Vector4(0f, 1f, 0f, 0.9999f);
        // Filled 커버리지 항등 반평면: c=1 → 항상 내부 (AND 결합)
        private static readonly Vector4 IdentityFillLine = new Vector4(0f, 0f, 1f, 0f);
        // 반평면 B의 w = AA 스케일. 항등에서도 saturate(dist*aa+0.5)가 step처럼 동작하도록 큰 값
        private static readonly Vector4 IdentityFillLineB = new Vector4(0f, 0f, 1f, 10000f);

        // Optional Shader 패턴 상수
        private const string OPTIONAL_SUFFIX = "(SoftMaskLight)";
        private const string OPTIONAL_FORMAT = "Hidden/{0} (SoftMaskLight)";
        private const string DEFAULT_OPTIONAL = "Hidden/UI/Default (SoftMaskLight)";

        // 셰이더 직렬화 참조 (빌드에서 Shader.Find() 실패 방지)
        [SerializeField, HideInInspector] private Shader _maskShader;
        [SerializeField, HideInInspector] private Shader _tmpMaskShader;
        // Optional Shader 빌드 포함은 SoftMaskLightSettings (Resources) 에셋이 담당

        // 셰이더 캐싱 (TMP 전용 — 기본 마스크 셰이더는 _maskShader 직렬화 참조로 빌드 포함만 담당)
        private static Shader s_cachedTMPShader;

        // Optional Shader 캐싱 (원본 셰이더 InstanceID → Hidden 변형 셰이더)
        private static readonly Dictionary<int, Shader> s_optionalShaderCache = new Dictionary<int, Shader>();

        /// <summary>
        /// 원본 셰이더에 대응하는 Optional Shader(Hidden 변형) 조회
        /// 이름 규칙: "Hidden/{원본셰이더이름} (SoftMaskLight)"
        /// 캐싱되어 동일 원본 셰이더에 대해 한 번만 Shader.Find() 호출
        /// </summary>
        internal static Shader FindOptionalShader(Shader originalShader)
        {
            if (originalShader == null) return null;

            int id = originalShader.GetInstanceID();
            if (s_optionalShaderCache.TryGetValue(id, out var cached))
                return cached;

            // 이미 Optional Shader인 경우 그대로 반환
            if (originalShader.name.Contains(OPTIONAL_SUFFIX))
            {
                s_optionalShaderCache[id] = originalShader;
                return originalShader;
            }

            string shaderName = originalShader.name;

            // mob-sakai SoftMask가 이미 적용된 셰이더 → 마스킹 스킵 (패스스루)
            // 결합 변형 셰이더가 없으므로 교체 시 mob-sakai 마스킹과 원본 기능이 모두 파괴됨
            if (shaderName.Contains("(SoftMaskable)"))
            {
                Debug.LogWarning($"[SoftMaskLight] '{shaderName}'은 mob-sakai SoftMask가 적용된 셰이더입니다. " +
                                 "동일 Graphic에 두 마스크를 중첩할 수 없어 SoftMaskLight 마스킹을 건너뜁니다.");
                s_optionalShaderCache[id] = null;
                return null;
            }

            // 이미 Hidden/ 접두사가 있는 셰이더는 접두사 중복 방지
            // 예: "Hidden/UI/Default (UIEffect)" → "Hidden/UI/Default (UIEffect) (SoftMaskLight)"
            string name;
            if (shaderName.StartsWith("Hidden/"))
                name = shaderName + " " + OPTIONAL_SUFFIX;
            else
                name = string.Format(OPTIONAL_FORMAT, shaderName);

            Shader variant = Shader.Find(name);
            if (variant == null)
            {
                // 변형 셰이더 부재 → 기본 UI 변형으로 폴백 (블렌드 모드/이펙트가 달라질 수 있음)
                variant = Shader.Find(DEFAULT_OPTIONAL);
                Debug.LogWarning($"[SoftMaskLight] '{name}' 변형 셰이더가 없어 '{DEFAULT_OPTIONAL}'로 대체합니다. " +
                                 "원본 셰이더의 블렌드 모드/이펙트가 손실될 수 있습니다. " +
                                 "커스텀 셰이더라면 (SoftMaskLight) 변형 셰이더를 추가하세요. (README 참조)");
            }

            s_optionalShaderCache[id] = variant;
            return variant;
        }

        // Shader Property ID 캐싱 (SoftMaskLight 셰이더용)
        private static readonly int PropMaskTex = Shader.PropertyToID("_MaskTex");
        // 소프트니스는 역수(1/max(softness, 0.001))로 전달 — 픽셀당 나눗셈 제거
        private static readonly int PropSoftnessRcp = Shader.PropertyToID("_SoftnessRcp");
        private static readonly int PropInvertMask = Shader.PropertyToID("_InvertMask");
        private static readonly int PropMaskWorldToUV = Shader.PropertyToID("_MaskWorldToUV");
        private static readonly int PropMaskUVRect = Shader.PropertyToID("_MaskUVRect");
        private static readonly int PropMaskSliceBorder = Shader.PropertyToID("_MaskSliceBorder");
        private static readonly int PropMaskSliceInnerUV = Shader.PropertyToID("_MaskSliceInnerUV");
        private static readonly int PropMaskSliceSlopeX = Shader.PropertyToID("_MaskSliceSlopeX");
        private static readonly int PropMaskSliceSlopeY = Shader.PropertyToID("_MaskSliceSlopeY");
        private static readonly int PropMaskFillLineA = Shader.PropertyToID("_MaskFillLineA");
        private static readonly int PropMaskFillLineB = Shader.PropertyToID("_MaskFillLineB");
        private static readonly int PropMaskTex2 = Shader.PropertyToID("_MaskTex2");
        private static readonly int PropSoftnessRcp2 = Shader.PropertyToID("_SoftnessRcp2");
        private static readonly int PropInvertMask2 = Shader.PropertyToID("_InvertMask2");
        private static readonly int PropMaskWorldToUV2 = Shader.PropertyToID("_MaskWorldToUV2");
        private static readonly int PropMaskUVRect2 = Shader.PropertyToID("_MaskUVRect2");
        private static readonly int PropMaskSliceBorder2 = Shader.PropertyToID("_MaskSliceBorder2");
        private static readonly int PropMaskSliceInnerUV2 = Shader.PropertyToID("_MaskSliceInnerUV2");
        private static readonly int PropMaskSliceSlopeX2 = Shader.PropertyToID("_MaskSliceSlopeX2");
        private static readonly int PropMaskSliceSlopeY2 = Shader.PropertyToID("_MaskSliceSlopeY2");
        private static readonly int PropMaskFillLineA2 = Shader.PropertyToID("_MaskFillLineA2");
        private static readonly int PropMaskFillLineB2 = Shader.PropertyToID("_MaskFillLineB2");
        private static readonly int PropClipRect = Shader.PropertyToID("_ClipRect");
        private static readonly Vector4 DefaultClipRect = new Vector4(-32767f, -32767f, 32767f, 32767f);
        private const string KeywordUIClipRect = "UNITY_UI_CLIP_RECT";

        // TMP 셰이더용 프로퍼티 ID (_SoftMask* 접두사: TMP의 _MaskTex 충돌 방지)
        private static readonly int PropTMPMaskTex = Shader.PropertyToID("_SoftMaskTex");
        private static readonly int PropTMPSoftnessRcp = Shader.PropertyToID("_SoftMaskSoftnessRcp");
        private static readonly int PropTMPInvertMask = Shader.PropertyToID("_SoftMaskInvert");
        private static readonly int PropTMPMaskWorldToUV = Shader.PropertyToID("_SoftMaskWorldToUV");
        private static readonly int PropTMPMaskUVRect = Shader.PropertyToID("_SoftMaskUVRect");
        private static readonly int PropTMPMaskSliceBorder = Shader.PropertyToID("_SoftMaskSliceBorder");
        private static readonly int PropTMPMaskSliceInnerUV = Shader.PropertyToID("_SoftMaskSliceInnerUV");
        private static readonly int PropTMPMaskSliceSlopeX = Shader.PropertyToID("_SoftMaskSliceSlopeX");
        private static readonly int PropTMPMaskSliceSlopeY = Shader.PropertyToID("_SoftMaskSliceSlopeY");
        private static readonly int PropTMPMaskFillLineA = Shader.PropertyToID("_SoftMaskFillLineA");
        private static readonly int PropTMPMaskFillLineB = Shader.PropertyToID("_SoftMaskFillLineB");
        private static readonly int PropTMPMaskTex2 = Shader.PropertyToID("_SoftMaskTex2");
        private static readonly int PropTMPSoftnessRcp2 = Shader.PropertyToID("_SoftMaskSoftnessRcp2");
        private static readonly int PropTMPInvertMask2 = Shader.PropertyToID("_SoftMaskInvert2");
        private static readonly int PropTMPMaskWorldToUV2 = Shader.PropertyToID("_SoftMaskWorldToUV2");
        private static readonly int PropTMPMaskUVRect2 = Shader.PropertyToID("_SoftMaskUVRect2");
        private static readonly int PropTMPMaskSliceBorder2 = Shader.PropertyToID("_SoftMaskSliceBorder2");
        private static readonly int PropTMPMaskSliceInnerUV2 = Shader.PropertyToID("_SoftMaskSliceInnerUV2");
        private static readonly int PropTMPMaskSliceSlopeX2 = Shader.PropertyToID("_SoftMaskSliceSlopeX2");
        private static readonly int PropTMPMaskSliceSlopeY2 = Shader.PropertyToID("_SoftMaskSliceSlopeY2");
        private static readonly int PropTMPMaskFillLineA2 = Shader.PropertyToID("_SoftMaskFillLineA2");
        private static readonly int PropTMPMaskFillLineB2 = Shader.PropertyToID("_SoftMaskFillLineB2");

        /// <summary>셰이더에 전달할 소프트니스 역수 (픽셀당 나눗셈 제거용 사전 계산)</summary>
        private static float SoftnessRcp(float softness)
        {
            return 1f / Mathf.Max(softness, 0.001f);
        }

        // ─────────────────────────────────────────────
        // 직렬화 필드
        // ─────────────────────────────────────────────

        [Header("Mask Settings")]
        [SerializeField] private bool _showMaskGraphic = true;
        public bool ShowMaskGraphic
        {
            get => _showMaskGraphic;
            set
            {
                _showMaskGraphic = value;
                UpdateMaskGraphicVisibility();
            }
        }

        [SerializeField, Range(0f, 1f)] private float _softness = 0.1f;
        public float Softness
        {
            get => _softness;
            set
            {
                _softness = Mathf.Clamp01(value);
                _materialDirty = true;
            }
        }

        [SerializeField] private bool _invertMask = false;
        public bool InvertMask
        {
            get => _invertMask;
            set
            {
                _invertMask = value;
                _materialDirty = true;
            }
        }

        // ─────────────────────────────────────────────
        // 내부 참조
        // ─────────────────────────────────────────────

        private UnityEngine.UI.Graphic _uiGraphic;
        private RectTransform _rectTransform;
        private Canvas _rootCanvas; // ComputeWorldToMaskUV()에서 프레임당 탐색 방지용 캐시
        private bool _initialized;

        // 자식 원본 Material 복원용
        private readonly Dictionary<UnityEngine.UI.Graphic, Material> _originalChildMaterials =
            new Dictionary<UnityEngine.UI.Graphic, Material>();

        // 마스크 그래픽 숨김 상태 (ShowMaskGraphic = false → 알파 0으로 숨김)
        // 상태 전환 시에만 색상을 변경하여 외부 색상 트윈/애니메이션과 충돌하지 않음
        private float _savedMaskAlpha;
        private bool _maskColorHidden;

        // 중첩 마스크: 부모 SoftMask
        private SoftMaskLight _parentSoftMask;
        private bool _hasParentMask;

        // 더티 체크용 캐싱
        private Matrix4x4 _cachedWorldToUV;
        private Matrix4x4 _cachedParentWorldToUV;
        // 행렬 캐시 초기화 여부 (m00/m11 == 0 검사는 90도 회전 시 오탐 → 명시적 플래그 사용)
        private bool _cachedWorldToUVValid;
        private bool _cachedParentWorldToUVValid;
        private float _cachedSoftness;
        private bool _cachedInvertMask;
        private float _cachedParentSoftness;
        private bool _cachedParentInvertMask;
        private int _cachedMaskTexId;
        private int _cachedParentMaskTexId;
        private bool _materialDirty;

        // ── 지오메트리/스프라이트 캐시 ──
        // 스프라이트 rect·border·아틀라스 UV 조회는 네이티브 바인딩 호출이라 매 프레임 반복하면 낭비다.
        // 스프라이트 인스턴스 / Image.type / RectTransform 크기가 바뀔 때만 재계산한다.
        private Sprite _geoSprite;
        private Texture _geoMaskTexture;
        private UnityEngine.UI.Image.Type _geoImageType;
        private Rect _geoContentRect;
        private Vector4 _geoUVRect = new Vector4(0f, 0f, 1f, 1f);
        // "형태 대응" 필요 여부: Sliced(테두리 있음) / Tiled / Filled → _SOFTMASK_SLICE 키워드 활성
        private bool _geoIsSliced;
        private Vector4 _geoSliceBorder = new Vector4(0f, 0f, 1f, 1f);
        private Vector4 _geoSliceInnerUV = new Vector4(0f, 0f, 1f, 1f);
        // 구간별 기울기 (k1, k2n, k3, 타일수-ε) — 셰이더의 픽셀당 나눗셈을 제거하기 위한 사전 계산 값
        private Vector4 _geoSliceSlopeX = IdentitySliceSlope;
        private Vector4 _geoSliceSlopeY = IdentitySliceSlope;
        // Filled 커버리지 반평면 (항등 = 항상 내부)
        private Vector4 _geoFillLineA = IdentityFillLine;
        private Vector4 _geoFillLineB = IdentityFillLineB;
        // Filled 파라미터 변경 감지 (fillAmount는 매 프레임 애니메이션될 수 있음)
        private float _geoFillAmount = -1f;
        private UnityEngine.UI.Image.FillMethod _geoFillMethod;
        private int _geoFillOrigin;
        private bool _geoFillClockwise;
        private bool _geometryDirty = true;
        private int _geoValidatedFrame = -1;

        // 형태 대응 더티 체크용 캐싱 (기본값은 각 단계를 무효화하는 항등값)
        private bool _cachedIsSliced;
        private Vector4 _cachedSliceBorder = new Vector4(0f, 0f, 1f, 1f);
        private Vector4 _cachedSliceInnerUV = new Vector4(0f, 0f, 1f, 1f);
        private Vector4 _cachedSliceSlopeX = IdentitySliceSlope;
        private Vector4 _cachedSliceSlopeY = IdentitySliceSlope;
        private Vector4 _cachedFillLineA = IdentityFillLine;
        private Vector4 _cachedFillLineB = IdentityFillLineB;
        private bool _cachedParentIsSliced;
        private Vector4 _cachedParentSliceBorder = new Vector4(0f, 0f, 1f, 1f);
        private Vector4 _cachedParentSliceInnerUV = new Vector4(0f, 0f, 1f, 1f);
        private Vector4 _cachedParentSliceSlopeX = IdentitySliceSlope;
        private Vector4 _cachedParentSliceSlopeY = IdentitySliceSlope;
        private Vector4 _cachedParentFillLineA = IdentityFillLine;
        private Vector4 _cachedParentFillLineB = IdentityFillLineB;

        // TMP 전용 Material 리스트 (폰트 아틀라스별 개별 Material 필요)
        private readonly List<Material> _tmpMaskMaterials = new List<Material>(2);

        // TMP Graphic → 적용 중인 마스크 Material 매핑
        // 외부에서 TMP Material 변경 시 감지 및 자동 재적용에 사용
        private readonly Dictionary<UnityEngine.UI.Graphic, Material> _tmpAppliedMaskMats =
            new Dictionary<UnityEngine.UI.Graphic, Material>(2);

        // TMP 마스크 Material 공유 캐시: 원본 폰트 Material → 마스크 Material
        // 같은 폰트 프리셋을 쓰는 TMP 자식끼리 마스크 Material을 공유 (드로우콜 N → 1)
        private readonly Dictionary<Material, Material> _tmpSharedMaskMats =
            new Dictionary<Material, Material>(2);

        // UIEffect 프록시 목록 (프로퍼티 전파 + 정리용)
        private readonly List<UIEffectSoftMaskLightProxy> _uiEffectProxies = new List<UIEffectSoftMaskLightProxy>(2);

        // SoftMaskLightChildProxy 목록 (프로퍼티 전파 + 정리용)
        private readonly List<SoftMaskLightChildProxy> _childProxies = new List<SoftMaskLightChildProxy>(8);
        // 위 리스트의 O(1) 중복 검사용 (자식 수가 많을 때 Contains의 O(N²) 방지)
        private readonly HashSet<SoftMaskLightChildProxy> _childProxySet = new HashSet<SoftMaskLightChildProxy>();

        // 마스킹할 수 없어 등록에서 제외된 자식 (대응 변형 셰이더 없음 / TMP 폰트 머티리얼 없음 등).
        // 기록해 두지 않으면 매 스캔마다 "미등록 자식"으로 재발견되어 전체 재적용이 반복된다.
        private readonly HashSet<UnityEngine.UI.Graphic> _unmaskableChildren =
            new HashSet<UnityEngine.UI.Graphic>();

        // 공유 프록시 Material 캐시: 원본 Material → 프록시 Material
        // 동일한 원본 Material을 가진 자식끼리 프록시 Material을 공유 (배칭 유지)
        private readonly Dictionary<Material, Material> _sharedProxyMaterials =
            new Dictionary<Material, Material>(4);

        // 프록시 Material 역방향 인덱스: O(1) ContainsValue 대체
        private readonly HashSet<Material> _proxyMaterialSet = new HashSet<Material>();

        // UIEffect 프록시 Material 공유 캐시: UIEffect가 생성한 baseMaterial → 프록시 Material
        // UIEffect는 이펙트 설정별로 Material을 공유하므로, 같은 설정의 자식끼리 프록시도 공유 (배칭 유지)
        private readonly Dictionary<Material, Material> _uiEffectProxyMaterials =
            new Dictionary<Material, Material>(2);

        // UIEffect 프록시 Material 역방향 인덱스: O(1) 판별용 (PropagateToStencilMaterials 스킵 판정)
        private readonly HashSet<Material> _uiEffectProxyMaterialSet = new HashSet<Material>();

        // _unmaskableChildren 정리용 캐시 조건자 (RemoveWhere 델리게이트 할당 방지)
        private static readonly System.Predicate<UnityEngine.UI.Graphic> s_IsNullGraphic = g => g == null;

        // GC 방지: 재사용 리스트
        private readonly List<UnityEngine.UI.Graphic> _toRemove = new List<UnityEngine.UI.Graphic>(4);
        private readonly List<Material> _toRemoveMaterials = new List<Material>(4);
        private readonly List<UnityEngine.UI.Graphic> _childGraphicsBuffer = new List<UnityEngine.UI.Graphic>(16);

        // 모드 전환 후 Stencil Material 강제 갱신 카운터
        // Canvas 리빌드(willRenderCanvases)가 LateUpdate 이후에 발생하므로
        // 2프레임 동안 PropagateToStencilMaterials() 강제 실행 필요
        private int _stencilRefreshCountdown;

        // UIEffect 동적 추가 감지 플래그 (이벤트 기반)
        // 상태 변화가 감지된 시점에만 true로 설정되어 다음 프레임에서 체크 실행
        private bool _checkUIEffectPending;

        // TMP 원본 Material 직렬화 백업 (플레이모드 전환 시 DontSave Material 유실 대응)
        // 에디터 모드에서 저장 → 씬 직렬화에 포함 → 플레이모드에서 프리셋 복원
        [System.Serializable]
        private struct TMPOriginalEntry
        {
            public UnityEngine.UI.Graphic graphic;
            public Material material;
        }

        [SerializeField, HideInInspector]
        private List<TMPOriginalEntry> _tmpOriginalBackup = new List<TMPOriginalEntry>(2);

        // 자식 수 변경 감지 (에디터 + 플레이모드 공통)
        private int _lastChildCount;

        // 주기 스캔 위상 오프셋 (마스크가 여러 개일 때 같은 프레임에 비용이 몰리는 것 방지)
        private int _scanPhase;

        // 다음 자식 스캔에서 "일반 자식 → UIEffect 전환"까지 검사할지 여부.
        // 이 검사는 자식마다 GetComponent가 발생하므로 상시 수행하지 않는다.
        private bool _scanUIEffectTransition;

        // Canvas 레이아웃 완료 후 갱신 플래그
        // OnEnable/OnTransformParentChanged 시점에는 레이아웃이 미완료 상태일 수 있음
        // Canvas.willRenderCanvases 이벤트에서 레이아웃 완료 후 마스크 갱신
        private bool _pendingLayoutRefresh;

#if UNITY_EDITOR
        // 부모 UI Mask의 showMaskGraphic 변경 감지 (에디터 전용)
        private UnityEngine.UI.Mask _parentUIMask;
        private bool _cachedParentMaskShowGraphic;

        // 에디터 폴링 주기 (초). 구조 변경은 hierarchyChanged/OnValidate가 즉시 처리하므로
        // 여기서는 놓친 변경을 뒤늦게 줍는 안전망 역할만 한다.
        private const double EDITOR_SCAN_INTERVAL = 0.25;
        private double _lastEditorScanTime;
#endif

        // ─────────────────────────────────────────────
        // 생명주기
        // ─────────────────────────────────────────────

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!_initialized) Initialize();

            _parentSoftMask = FindParentSoftMask();
            _hasParentMask = _parentSoftMask != null;
            _materialDirty = true;

            UpdateMaskGraphicVisibility();
            ApplyMaskToChildren();

            // GetOrCreateSharedMaterial()이 _materialDirty를 false로 초기화하므로
            // 첫 LateUpdate에서 PropagateToStencilMaterials() 실행을 보장
            _materialDirty = true;

            // Canvas 리빌드가 LateUpdate 이후(willRenderCanvases)에 실행되므로
            // Stencil Material이 새로 생성된 후에도 프로퍼티 전파가 필요
            _stencilRefreshCountdown = 2;

            _lastChildCount = transform.childCount;

            // Canvas 레이아웃 완료 후 마스크 갱신 예약
            // OnEnable 시점에는 RectTransform 레이아웃이 미완료 상태일 수 있음
            // (특히 UI Mask 하위에 프리팹 로드 시)
            _pendingLayoutRefresh = true;
            Canvas.willRenderCanvases += OnCanvasPreRender;

#if UNITY_EDITOR
            // 부모 UI Mask 캐싱 (showMaskGraphic 변경 감지용)
            CacheParentUIMask();
#endif
        }

        private void Initialize()
        {
            if (_initialized) return;

            if (!Application.isPlaying && !gameObject.scene.IsValid()) return;

            _uiGraphic = GetComponent<UnityEngine.UI.Graphic>();
            _rectTransform = GetComponent<RectTransform>();

            if (_uiGraphic == null)
            {
                Debug.LogWarning($"[SoftMaskLight] {gameObject.name}: UI.Graphic 컴포넌트가 필요합니다.");
                return;
            }

            // Canvas 참조 캐싱 (ComputeWorldToMaskUV에서 프레임당 탐색 방지)
            CacheRootCanvas();

            // 인스턴스별 스캔 위상 (0~7)
            _scanPhase = GetInstanceID() & 7;

            _initialized = true;
        }

        // 프로파일러 마커 (Deep Profile 없이도 이름으로 비용 귀속이 가능하도록)
        // 프로파일링이 꺼져 있으면 사실상 무비용이다.
        private static readonly Unity.Profiling.ProfilerMarker s_MarkerLateUpdate =
            new Unity.Profiling.ProfilerMarker("SoftMaskLight.LateUpdate");
        private static readonly Unity.Profiling.ProfilerMarker s_MarkerUpdateShared =
            new Unity.Profiling.ProfilerMarker("SoftMaskLight.UpdateSharedMaterial");
        private static readonly Unity.Profiling.ProfilerMarker s_MarkerChildScan =
            new Unity.Profiling.ProfilerMarker("SoftMaskLight.CheckForChildChanges");
        private static readonly Unity.Profiling.ProfilerMarker s_MarkerApplyMask =
            new Unity.Profiling.ProfilerMarker("SoftMaskLight.ApplyMaskToChildren");
        private static readonly Unity.Profiling.ProfilerMarker s_MarkerPropagate =
            new Unity.Profiling.ProfilerMarker("SoftMaskLight.PropagateToStencilMaterials");

        private void LateUpdate()
        {
            if (!_initialized) return;
            using (s_MarkerLateUpdate.Auto())
            {
                LateUpdateInternal();
            }
        }

        private void LateUpdateInternal()
        {
            // TMP / UIEffect 외부 Material 변경 감지
            DetectTMPMaterialChanges();
            DetectUIEffectMaterialChanges();

            // UIEffect 동적 추가 감지: 상태 변화가 감지된 시점에만 체크 (이벤트 기반)
            // 에디터 비플레이 모드에서는 CheckForChildChanges에서 처리
            if (_checkUIEffectPending)
            {
                _checkUIEffectPending = false;
                DetectNewUIEffectOnExistingChildren();
                // 프록시 관리 자식(값이 null)은 위 함수가 다루지 않으므로 다음 주기 스캔에 위임
                _scanUIEffectTransition = true;
            }

            // 자식 수 변경 감지 (UIParticle 활성/비활성 시 새 자식 추가됨)
            int currentChildCount = transform.childCount;
            if (currentChildCount != _lastChildCount)
            {
                _lastChildCount = currentChildCount;
                ApplyMaskToChildren();
            }

            UpdateSharedMaterial();

            // 모드 전환 후 Stencil Material 강제 갱신
            // Canvas.willRenderCanvases에서 StencilMaterial이 새로 생성된 후
            // 다음 프레임에서 해당 Material에 마스크 프로퍼티를 전파
            if (_stencilRefreshCountdown > 0)
            {
                _stencilRefreshCountdown--;
                _materialDirty = true;
                _checkUIEffectPending = true;
            }

            UpdateMaskGraphicVisibility();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // 에디터에서도 [ExecuteAlways]로 매 프레임 돌기 때문에, 자식 트리 전수 스캔을
                // 프레임마다 하면 씬뷰에서 오브젝트를 드래그하는 동안 그대로 히칭이 된다.
                // 구조 변경은 hierarchyChanged / OnValidate가 즉시 처리하므로 여기서는 저빈도 폴링만 한다.
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                if (now - _lastEditorScanTime >= EDITOR_SCAN_INTERVAL)
                {
                    _lastEditorScanTime = now;
                    // 에디터에서는 컴포넌트 추가를 알리는 콜백이 없으므로 저빈도 폴링에 함께 태운다
                    _scanUIEffectTransition = true;
                    CheckForChildChanges();
                    CheckParentUIMaskChanges();
                    CleanupDestroyedChildren();
                }
                return;
            }
#endif
            // 플레이모드: 깊은 계층에 동적 추가된 Graphic / 마스크 밖 이동 감지
            // (직계 childCount 비교로는 중간 컨테이너 아래 Instantiate를 감지 못함)
            // 매 프레임 GetComponentsInChildren 비용을 피하기 위해 8프레임 주기로 스로틀하고,
            // 인스턴스별 위상 오프셋을 줘서 마스크가 여러 개일 때 같은 프레임에 몰리지 않게 한다.
            if (((Time.frameCount + _scanPhase) & 7) == 0)
            {
                CheckForChildChanges();
                CleanupDestroyedChildren();
            }
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= OnCanvasPreRender;
            _pendingLayoutRefresh = false;

            RestoreChildrenMaterials();

            RestoreMaskGraphicAlpha();

            _parentSoftMask = null;
            _hasParentMask = false;
        }

        /// <summary>
        /// 숨김 상태였다면 마스크 Graphic의 알파를 복원 (OnDisable/OnDestroy 공용)
        /// </summary>
        private void RestoreMaskGraphicAlpha()
        {
            if (_maskColorHidden && _uiGraphic != null)
            {
                _maskColorHidden = false;
                Color c = _uiGraphic.color;
                c.a = _savedMaskAlpha;
                _uiGraphic.color = c;
            }
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= OnCanvasPreRender;

            RestoreChildrenMaterials();

            RestoreMaskGraphicAlpha();
        }

        /// <summary>
        /// Canvas 레이아웃 완료 후 마스크 프로퍼티 갱신
        /// willRenderCanvases 이벤트는 모든 Canvas 레이아웃 계산 완료 후,
        /// 실제 렌더링 직전에 호출됨
        /// </summary>
        private void OnCanvasPreRender()
        {
            if (!_pendingLayoutRefresh) return;
            _pendingLayoutRefresh = false;

            // 레이아웃 완료 후 변환 행렬 재계산
            _cachedWorldToUV = ComputeWorldToMaskUV();
            _cachedWorldToUVValid = true;
            _materialDirty = true;

            // 부모 마스크도 갱신
            if (_hasParentMask && _parentSoftMask != null)
            {
                _cachedParentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                _cachedParentWorldToUVValid = true;
            }

            // Stencil Material 갱신 카운터 리셋
            _stencilRefreshCountdown = 2;
        }

        /// <summary>
        /// 부모 Transform 변경 시 마스크 갱신
        /// UI Mask 하위로 이동하거나 스크롤뷰에 배치될 때 호출됨
        /// </summary>
        private void OnTransformParentChanged()
        {
            if (!_initialized || !enabled) return;

            // 부모 SoftMask 재검색 + Canvas 재캐싱 (새 Canvas 계층일 수 있음)
            _parentSoftMask = FindParentSoftMask();
            _hasParentMask = _parentSoftMask != null;
            CacheRootCanvas();

            // 레이아웃 완료 후 갱신 예약
            _pendingLayoutRefresh = true;
            _materialDirty = true;
            _stencilRefreshCountdown = 2;

            if (!_hasParentMask) _cachedParentIsSliced = false;

#if UNITY_EDITOR
            // 부모 UI Mask 재캐싱
            CacheParentUIMask();
#endif
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _maskShader = Shader.Find(SHADER_NAME);
            _tmpMaskShader = Shader.Find(TMP_SHADER_NAME);
        }

        private void OnValidate()
        {
            if (!gameObject.scene.IsValid()) return;

            // 셰이더 직렬화 참조 자동 설정 (빌드 시 Shader.Find() 실패 방지)
            if (_maskShader == null) _maskShader = Shader.Find(SHADER_NAME);
            if (_tmpMaskShader == null) _tmpMaskShader = Shader.Find(TMP_SHADER_NAME);

            if (!_initialized) Initialize();

            _materialDirty = true;
            UpdateMaskGraphicVisibility();
        }

#endif

        // CheckForChildChanges 재진입 방지 (에디터 hierarchyChanged 핸들러에서
        // DestroyImmediate → hierarchyChanged 재발화로 재진입될 수 있음)
        private bool _checkingChildChanges;

        /// <summary>
        /// 자식 오브젝트 변경 감지 (에디터: 매 프레임 / 플레이모드: 8프레임 주기)
        /// 자식 추가, 마스크 밖 이동, UIEffect 추가 전환을 감지하여 마스크 재적용/복원
        /// </summary>
        public void CheckForChildChanges()
        {
            if (_checkingChildChanges) return;
            _checkingChildChanges = true;
            try
            {
                using (s_MarkerChildScan.Auto())
                    CheckForChildChangesInternal();
            }
            finally
            {
                _checkingChildChanges = false;
            }
        }

        private void CheckForChildChangesInternal()
        {
            // 마스크 텍스처가 없으면 ApplyMaskToChildren이 아무것도 등록하지 못한다.
            // 이 상태에서 미등록 자식을 찾아 재적용을 시도하면 스캔마다 헛도는 루프가 된다.
            if (GetMaskTexture() == null) return;

            int currentChildCount = transform.childCount;
            if (currentChildCount != _lastChildCount)
            {
                _lastChildCount = currentChildCount;
                RestoreChildrenMaterials();
                ApplyMaskToChildren();
                return;
            }

            // 기존 등록된 자식이 밖으로 이동했는지 감지 → 원본 Material 복원
            bool needsReapply = false;
            _toRemove.Clear();
            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) continue;

                // 이 마스크의 자식이 아니게 되었으면 원본 복원
                if (!kvp.Key.transform.IsChildOf(transform) || !BelongsToThisMask(kvp.Key.transform))
                {
                    RestoreSingleChild(kvp.Key, kvp.Value);
                    _toRemove.Add(kvp.Key);
                    needsReapply = true;
                }
            }
            for (int i = 0; i < _toRemove.Count; i++)
                _originalChildMaterials.Remove(_toRemove[i]);

            if (needsReapply)
            {
                ApplyMaskToChildren();
                return;
            }

            GetComponentsInChildren(true, _childGraphicsBuffer);
            var children = _childGraphicsBuffer;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.gameObject == gameObject) continue;

                // 미추적 자식 발견 → 전체 재적용 (BelongsToThisMask는 이때만 확인)
                // 마스킹 불가로 판정된 자식은 재시도하지 않는다 (재적용 루프 방지)
                if (!_originalChildMaterials.ContainsKey(child))
                {
                    if (_unmaskableChildren.Contains(child)) continue;
                    if (!BelongsToThisMask(child.transform)) continue;
                    ApplyMaskToChildren();
                    return;
                }

                // 기존 자식에 UIEffect가 나중에 추가된 경우의 전환 감지.
                // GetComponent가 자식 수만큼 곱해지는 구간이라 매 스캔 수행하지 않고,
                // 전환 후보가 생겼다고 표시된 경우(_checkUIEffectPending 소비 시)에만 검사한다.
                if (!_scanUIEffectTransition) continue;
                if (!IsUIEffectGraphic(child)) continue;

                var existingUIProxy = child.GetComponent<UIEffectSoftMaskLightProxy>();
                if (existingUIProxy != null && !existingUIProxy.IsCleanedUp) continue;

                // 일반 프록시 제거 후 UIEffect 프록시로 전환
                var childProxy = child.GetComponent<SoftMaskLightChildProxy>();
                if (childProxy != null && !childProxy.IsCleanedUp)
                {
                    _childProxies.Remove(childProxy);
                    _childProxySet.Remove(childProxy);
                    childProxy.Cleanup();
                }
                // TMP 자식이었다면 폰트 Material 복원 + 공유 마스크 Material 해제
                // (해제 없이 전환하면 _tmpAppliedMaskMats의 stale 엔트리가 공유 Material 파괴를 영구히 막는다)
                if (_originalChildMaterials.TryGetValue(child, out var prevOriginal) && prevOriginal != null)
                    RestoreSingleChild(child, prevOriginal);
                _originalChildMaterials.Remove(child);
                ApplyMaskToUIEffect(child);
            }

            _scanUIEffectTransition = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 부모 UI Mask 캐싱 (showMaskGraphic 변경 감지용)
        /// </summary>
        private void CacheParentUIMask()
        {
            _parentUIMask = GetComponentInParent<UnityEngine.UI.Mask>();
            if (_parentUIMask != null)
            {
                _cachedParentMaskShowGraphic = _parentUIMask.showMaskGraphic;
            }
        }

        /// <summary>
        /// 부모 UI Mask의 showMaskGraphic 변경 감지 및 마스크 갱신
        /// showMaskGraphic 변경 시 Stencil 설정이 변경되어 자식 렌더링에 영향
        /// </summary>
        private void CheckParentUIMaskChanges()
        {
            if (_parentUIMask == null)
            {
                // 부모 Mask가 새로 추가되었을 수 있음
                var newParentMask = GetComponentInParent<UnityEngine.UI.Mask>();
                if (newParentMask != null)
                {
                    _parentUIMask = newParentMask;
                    _cachedParentMaskShowGraphic = newParentMask.showMaskGraphic;
                    _pendingLayoutRefresh = true;
                    _stencilRefreshCountdown = 2;
                }
                return;
            }

            // showMaskGraphic 변경 감지
            if (_parentUIMask.showMaskGraphic != _cachedParentMaskShowGraphic)
            {
                _cachedParentMaskShowGraphic = _parentUIMask.showMaskGraphic;
                _pendingLayoutRefresh = true;
                _materialDirty = true;
                _stencilRefreshCountdown = 2;
            }
        }
#endif

        // ─────────────────────────────────────────────
        // 마스크 변환 행렬 계산 (회전 대응)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Canvas 로컬 좌표 → 마스크 UV (0~1) 변환 행렬 계산
        /// RectTransform의 회전, 스케일을 모두 반영
        /// Atlas 스프라이트 트리밍(투명 여백 제거) 보정 포함
        ///
        /// UI 셰이더에서 v.vertex는 Canvas 로컬 좌표로 전달됨
        /// Canvas.localToWorldMatrix를 곱하여 Canvas 로컬 → 월드 → 마스크 로컬 변환을 구성
        /// Screen Space - Overlay 모드에서도 정확한 좌표 변환 보장
        /// (unity_ObjectToWorld 의존성 제거)
        /// </summary>
        internal Matrix4x4 ComputeWorldToMaskUV()
        {
            if (_rectTransform == null) return Matrix4x4.identity;

            EnsureGeometryCache();
            Rect contentRect = _geoContentRect;
            if (contentRect.width < 0.001f || contentRect.height < 0.001f) return Matrix4x4.identity;

            Matrix4x4 worldToLocal = _rectTransform.worldToLocalMatrix;

            // 로컬 좌표 → UV (0~1) 변환 (콘텐츠 영역 기준)
            Matrix4x4 localToUV = Matrix4x4.identity;
            localToUV.m00 = 1f / contentRect.width;
            localToUV.m11 = 1f / contentRect.height;
            localToUV.m03 = -contentRect.x / contentRect.width;
            localToUV.m13 = -contentRect.y / contentRect.height;

            // Canvas 로컬 좌표 기반 변환:
            // 셰이더에서 v.vertex.xyz(Canvas 로컬 좌표)를 직접 사용하므로
            // Canvas.localToWorldMatrix를 곱하여 Canvas 로컬 → 마스크 UV 변환 구성
            // 캐시 미스 시 즉시 재조회 (Initialize 시점에 Canvas 미준비 대응)
            if (_rootCanvas == null) CacheRootCanvas();

            if (_rootCanvas != null)
            {
                Matrix4x4 canvasLocalToWorld = _rootCanvas.transform.localToWorldMatrix;
                return localToUV * worldToLocal * canvasLocalToWorld;
            }

            return localToUV * worldToLocal;
        }

        /// <summary>
        /// Root Canvas 참조 캐싱 (ComputeWorldToMaskUV에서 프레임당 탐색 방지)
        /// Graphic.canvas는 내부 캐시를 사용하지만, rootCanvas는 계층 탐색이 발생하므로
        /// Initialize / OnTransformParentChanged 시점에만 갱신
        /// </summary>
        private void CacheRootCanvas()
        {
            Canvas canvas = _uiGraphic != null ? _uiGraphic.canvas : null;
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
        }

        /// <summary>
        /// RectTransform 크기 변경 시 지오메트리 캐시 무효화 (Unity 메시지)
        /// </summary>
        private void OnRectTransformDimensionsChange()
        {
            _geometryDirty = true;
        }

        /// <summary>
        /// 스프라이트/사이즈 의존 값(콘텐츠 rect, 아틀라스 UV, 9-slice 파라미터, 마스크 텍스처)을
        /// 프레임당 1회만 검증하고, 실제 변경이 있을 때만 재계산한다.
        /// 스프라이트 프로퍼티는 네이티브 바인딩이므로 매 프레임 반복 접근을 피하는 것이 핵심.
        /// </summary>
        private void EnsureGeometryCache()
        {
            if (_rectTransform == null) return;

            // 에디터 비플레이 모드에서는 인스펙터 편집 반응성을 위해 항상 검증
            bool alwaysValidate = !Application.isPlaying;
            if (!alwaysValidate && !_geometryDirty && _geoValidatedFrame == Time.frameCount) return;
            _geoValidatedFrame = Time.frameCount;

            var image = _uiGraphic as UnityEngine.UI.Image;
            Sprite sprite = image != null ? image.sprite : null;
            UnityEngine.UI.Image.Type imageType = image != null ? image.type : default;

            bool changed = _geometryDirty
                || sprite != _geoSprite
                || (image != null && imageType != _geoImageType);

            // RawImage는 텍스처가 임의 시점에 교체될 수 있어 매 프레임 확인 (참조 비교라 저렴)
            if (!changed && _uiGraphic is UnityEngine.UI.RawImage rawCheck && rawCheck.texture != _geoMaskTexture)
                changed = true;

            // Filled 파라미터는 매 프레임 애니메이션될 수 있어 항상 확인 (float/enum 비교라 저렴)
            // sprite == null이면 isFilled 판정이 false라 _geoFillAmount가 -1로 유지되므로
            // 비교 대상에서 제외 (매 프레임 무한 재계산 방지)
            if (!changed && image != null && imageType == UnityEngine.UI.Image.Type.Filled && sprite != null &&
                (image.fillAmount != _geoFillAmount || image.fillMethod != _geoFillMethod ||
                 image.fillOrigin != _geoFillOrigin || image.fillClockwise != _geoFillClockwise))
                changed = true;

            if (!changed) return;

            _geometryDirty = false;
            _geoSprite = sprite;
            _geoImageType = imageType;

            Texture prevMaskTexture = _geoMaskTexture;
            _geoMaskTexture = ComputeMaskTexture();
            // 스프라이트/텍스처가 바뀌면 "마스킹 불가" 판정 근거도 달라지므로 재시도를 허용한다
            if (prevMaskTexture != _geoMaskTexture && _unmaskableChildren.Count > 0)
                _unmaskableChildren.Clear();
            _geoContentRect = ComputeContentLocalRect();
            _geoUVRect = ComputeMaskUVRect();

            // 형태 대응 판정: Sliced(테두리 있음) / Tiled / Filled → _SOFTMASK_SLICE 키워드 필요
            bool isSliced = image != null && imageType == UnityEngine.UI.Image.Type.Sliced &&
                            sprite != null && sprite.border != Vector4.zero;
            bool isTiled = image != null && imageType == UnityEngine.UI.Image.Type.Tiled && sprite != null;
            bool isFilled = image != null && imageType == UnityEngine.UI.Image.Type.Filled && sprite != null;
            _geoIsSliced = isSliced || isTiled || isFilled;

            if (isSliced || isTiled)
            {
                // Tiled(테두리 0)도 동일 함수로 처리됨: border → 항등, innerUV → 전체 스프라이트
                _geoSliceBorder = ComputeMaskSliceBorder();
                _geoSliceInnerUV = ComputeMaskSliceInnerUV();
                Vector2 tiles = isTiled ? ComputeTileCounts(image) : Vector2.one;
                _geoSliceSlopeX = ComputeSliceSlopes(_geoSliceBorder.x, _geoSliceBorder.z, _geoSliceInnerUV.x, _geoSliceInnerUV.z, tiles.x);
                _geoSliceSlopeY = ComputeSliceSlopes(_geoSliceBorder.y, _geoSliceBorder.w, _geoSliceInnerUV.y, _geoSliceInnerUV.w, tiles.y);
            }
            else
            {
                _geoSliceBorder = IdentitySlice;
                _geoSliceInnerUV = IdentitySlice;
                _geoSliceSlopeX = IdentitySliceSlope;
                _geoSliceSlopeY = IdentitySliceSlope;
            }

            if (isFilled)
            {
                _geoFillAmount = image.fillAmount;
                _geoFillMethod = image.fillMethod;
                _geoFillOrigin = image.fillOrigin;
                _geoFillClockwise = image.fillClockwise;
                // AA 스케일: 1 로컬 단위(≈1 참조 px) 폭의 소프트 경계
                float aaScale = Mathf.Max(Mathf.Max(_geoContentRect.width, _geoContentRect.height), 2f);
                ComputeFillLines(_geoFillMethod, _geoFillOrigin, _geoFillAmount, _geoFillClockwise,
                                 aaScale, out _geoFillLineA, out _geoFillLineB);
            }
            else
            {
                _geoFillAmount = -1f;
                _geoFillLineA = IdentityFillLine;
                _geoFillLineB = IdentityFillLineB;
            }
        }

        /// <summary>
        /// 9-슬라이스/타일 리매핑의 구간별 기울기 사전 계산 (셰이더 픽셀당 나눗셈 제거)
        /// k1 = pA/uA, k2n = 타일수/(uB-uA), k3 = (1-pB)/(1-uB), w = 타일수 - ε (frac 끝 경계 가드)
        /// Sliced는 타일수 1 → frac(min(x, 1-ε)) = x 로 기존 선형 스트레치와 동일
        /// 분모 하한(0.00001)은 셰이더의 기존 max() 가드와 동일
        /// </summary>
        private static Vector4 ComputeSliceSlopes(float uA, float uB, float pA, float pB, float tiles)
        {
            const float EPS = 0.00001f;
            float n = Mathf.Max(tiles, EPS);
            float k1 = pA / Mathf.Max(uA, EPS);
            float k2 = n / Mathf.Max(uB - uA, EPS);
            float k3 = (1f - pB) / Mathf.Max(1f - uB, EPS);
            return new Vector4(k1, k2, k3, n - 0.0001f);
        }

        /// <summary>
        /// Tiled 마스크의 중앙 구간 타일 반복 수 계산 (X, Y)
        /// Unity의 타일 크기 = 스프라이트 내부 영역(px) / (pixelsPerUnit * pixelsPerUnitMultiplier)
        /// </summary>
        private static Vector2 ComputeTileCounts(UnityEngine.UI.Image image)
        {
            Sprite sprite = image.sprite;
            // multipliedPixelsPerUnit은 protected라 공개 API로 동일 값을 재구성
            float ppu = image.pixelsPerUnit * image.pixelsPerUnitMultiplier;
            if (ppu < 0.001f) ppu = 1f;

            Vector4 border = sprite.border; // (L, B, R, T) px
            Rect spriteRect = sprite.rect;
            float innerPxW = Mathf.Max(spriteRect.width - border.x - border.z, 1f);
            float innerPxH = Mathf.Max(spriteRect.height - border.y - border.w, 1f);
            float tileW = innerPxW / ppu;
            float tileH = innerPxH / ppu;

            RectTransform rt = image.rectTransform;
            Rect rect = rt.rect;
            float middleW = Mathf.Max(rect.width - (border.x + border.z) / ppu, 0f);
            float middleH = Mathf.Max(rect.height - (border.y + border.w) / ppu, 0f);

            return new Vector2(
                tileW > 0.0001f ? middleW / tileW : 1f,
                tileH > 0.0001f ? middleH / tileH : 1f);
        }

        /// <summary>
        /// Filled 마스크의 커버리지를 반평면 2개 + 결합 모드로 사전 계산 (셰이더 atan2 제거)
        /// 반평면 (a,b,c): a*u + b*v + c >= 0 이면 내부. (a,b)는 단위 벡터로 정규화 →
        /// dot 결과가 uv 공간 부호 거리. lineA.w: 0=교집합(부채꼴 ≤180°), 1=합집합(>180°)
        /// lineB.w: AA 스케일 (셰이더에서 saturate(dist*aa+0.5)로 ~1px 소프트 경계)
        /// 좌표계: 마스크 rect 정규화 [0,1]², 각도는 +x축 기준 반시계(CCW)
        /// </summary>
        private static void ComputeFillLines(
            UnityEngine.UI.Image.FillMethod method, int origin, float fill, bool clockwise,
            float aaScale, out Vector4 lineA, out Vector4 lineB)
        {
            lineA = IdentityFillLine;
            lineB = IdentityFillLineB;
            lineB.w = aaScale;

            if (fill >= 0.9999f) return; // 전체 표시 → 항등
            // Unity와 동일 임계: fillAmount < 0.001 이면 아예 렌더하지 않음 (Image.cs 참조)
            if (fill < 0.001f)
            {
                lineA = new Vector4(0f, 0f, -1f, 0f); // 항상 외부 → 전체 숨김
                return;
            }

            switch (method)
            {
                case UnityEngine.UI.Image.FillMethod.Horizontal:
                    lineA = origin == 0
                        ? new Vector4(-1f, 0f, fill, 0f)      // Left:   u <= fill
                        : new Vector4(1f, 0f, fill - 1f, 0f); // Right:  u >= 1-fill
                    return;

                case UnityEngine.UI.Image.FillMethod.Vertical:
                    lineA = origin == 0
                        ? new Vector4(0f, -1f, fill, 0f)      // Bottom: v <= fill
                        : new Vector4(0f, 1f, fill - 1f, 0f); // Top:    v >= 1-fill
                    return;
            }

            // 방사형: 중심 / 시작각 / 스윕각 → 부채꼴 반평면 2개
            Vector2 center;
            float startDeg;
            float sweepDeg;
            // Radial180은 rect를 반으로 나눈 서브쿼드의 파라미터 공간에서 컷이 보간되므로
            // (Unity Image.RadialCut), 경계 방향에 축별 비등방 스케일을 곱해야 실제 규약과 일치한다
            Vector2 aniso = Vector2.one;
            switch (method)
            {
                case UnityEngine.UI.Image.FillMethod.Radial90:
                {
                    // 내부 사분면 [θa, θa+90°]. BottomLeft/TopLeft/TopRight/BottomRight
                    center = origin switch
                    {
                        1 => new Vector2(0f, 1f),
                        2 => new Vector2(1f, 1f),
                        3 => new Vector2(1f, 0f),
                        _ => new Vector2(0f, 0f),
                    };
                    float qa = origin switch { 1 => 270f, 2 => 180f, 3 => 90f, _ => 0f };
                    sweepDeg = fill * 90f;
                    startDeg = clockwise ? qa + 90f : qa;
                    break;
                }
                case UnityEngine.UI.Image.FillMethod.Radial180:
                {
                    // 내부 반원 [θa, θa+180°]. Bottom/Left/Top/Right (원점 = 변의 중점)
                    center = origin switch
                    {
                        1 => new Vector2(0f, 0.5f),
                        2 => new Vector2(0.5f, 1f),
                        3 => new Vector2(1f, 0.5f),
                        _ => new Vector2(0.5f, 0f),
                    };
                    float ha = origin switch { 1 => 270f, 2 => 180f, 3 => 90f, _ => 0f };
                    sweepDeg = fill * 180f;
                    startDeg = clockwise ? ha + 180f : ha;
                    // 서브쿼드 0.5×1(Bottom/Top) 또는 1×0.5(Left/Right)의 비등방 보정
                    aniso = (origin == 0 || origin == 2)
                        ? new Vector2(0.5f, 1f)
                        : new Vector2(1f, 0.5f);
                    break;
                }
                default: // Radial360
                {
                    center = new Vector2(0.5f, 0.5f);
                    startDeg = origin switch { 1 => 0f, 2 => 90f, 3 => 180f, _ => 270f }; // Bottom/Right/Top/Left
                    sweepDeg = fill * 360f;
                    break;
                }
            }

            float dir = clockwise ? -1f : 1f;
            Vector2 s = Vector2.Scale(DirFromDeg(startDeg), aniso);
            Vector2 e = Vector2.Scale(DirFromDeg(startDeg + dir * sweepDeg), aniso);

            // 부채꼴 내부 = "시작선의 스윕 방향 쪽" ∩ "끝선의 반대 쪽"
            // 스윕이 180° 초과면 여집합 부채꼴의 드모르간 → 합집합으로 전환
            // (양의 대각 스케일은 cross 부호를 보존하므로 판정 논리는 그대로 유효)
            float combiner = sweepDeg > 180.001f ? 1f : 0f;
            lineA = LineFromCross(s, dir, center);
            lineB = LineFromCross(e, -dir, center);
            lineA.w = combiner;
            lineB.w = aaScale;
        }

        private static Vector2 DirFromDeg(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>
        /// 방향 벡터 V와 중심 c에 대해 sign*cross(V, D) >= 0 (D = p - c)을
        /// 반평면 (a, b, c') 형태로 변환: a*u + b*v + c' >= 0
        /// (a,b)를 단위 길이로 정규화해 dot 결과가 uv 공간 부호 거리가 되게 한다 (AA용)
        /// </summary>
        private static Vector4 LineFromCross(Vector2 v, float sign, Vector2 center)
        {
            float a = -sign * v.y;
            float b = sign * v.x;
            float len = Mathf.Sqrt(a * a + b * b);
            if (len > 0.00001f) { a /= len; b /= len; }
            float c = -(a * center.x + b * center.y);
            return new Vector4(a, b, c, 0f);
        }

        /// <summary>
        /// 스프라이트 콘텐츠의 실제 로컬 영역 계산
        /// Atlas 패킹 시 투명 여백이 트리밍된 경우, 콘텐츠 영역만 반환
        /// 비트리밍 스프라이트 또는 비Image는 전체 RectTransform rect 반환
        /// </summary>
        private Rect ComputeContentLocalRect()
        {
            if (_uiGraphic is UnityEngine.UI.Image image && image.sprite != null)
            {
                Sprite sprite = image.sprite;
                Vector2 spriteSize = sprite.rect.size;
                if (spriteSize.x < 0.001f || spriteSize.y < 0.001f)
                    return _rectTransform.rect;

                Vector2 trimOffset = sprite.textureRectOffset;
                Rect texRect = sprite.textureRect;

                bool isTrimmed = trimOffset.x > 0.001f || trimOffset.y > 0.001f ||
                                 texRect.width < spriteSize.x - 0.001f ||
                                 texRect.height < spriteSize.y - 0.001f;

                if (isTrimmed)
                {
                    Rect fullRect = _rectTransform.rect;

                    float ratioX = trimOffset.x / spriteSize.x;
                    float ratioY = trimOffset.y / spriteSize.y;
                    float ratioW = texRect.width / spriteSize.x;
                    float ratioH = texRect.height / spriteSize.y;

                    return new Rect(
                        fullRect.x + fullRect.width * ratioX,
                        fullRect.y + fullRect.height * ratioY,
                        fullRect.width * ratioW,
                        fullRect.height * ratioH
                    );
                }
            }

            return _rectTransform.rect;
        }

        /// <summary>
        /// 마스크 텍스처 가져오기 (자신의 텍스처) — 캐시 조회
        /// </summary>
        internal Texture GetMaskTexture()
        {
            EnsureGeometryCache();
            return _geoMaskTexture;
        }

        private Texture ComputeMaskTexture()
        {
            if (_uiGraphic is UnityEngine.UI.Image image && image.sprite != null)
                return image.sprite.texture;
            if (_uiGraphic is UnityEngine.UI.RawImage rawImage)
                return rawImage.texture;
            return null;
        }

        /// <summary>
        /// Atlas 스프라이트 UV Rect 계산
        /// 비아틀라스 스프라이트는 (0, 0, 1, 1) 반환
        /// </summary>
        internal Vector4 GetMaskUVRect()
        {
            EnsureGeometryCache();
            return _geoUVRect;
        }

        private Vector4 ComputeMaskUVRect()
        {
            if (_uiGraphic is UnityEngine.UI.Image image && image.sprite != null)
            {
                Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite);
                return new Vector4(outerUV.x, outerUV.y, outerUV.z - outerUV.x, outerUV.w - outerUV.y);
            }
            return IdentitySlice;
        }

        /// <summary>
        /// 마스크 이미지가 Sliced 타입인지 확인 — 캐시 조회
        /// </summary>
        internal bool IsSlicedMask()
        {
            EnsureGeometryCache();
            return _geoIsSliced;
        }

        /// <summary>
        /// 9-슬라이스 테두리 break point 계산 (rect 정규화 좌표 기준)
        /// 반환값: (leftBreak, bottomBreak, rightBreak, topBreak)
        ///   leftBreak  = 왼쪽 테두리 폭 / rect 폭  (0~1)
        ///   bottomBreak = 아래쪽 테두리 높이 / rect 높이  (0~1)
        ///   rightBreak  = 1 - (오른쪽 테두리 폭 / rect 폭)
        ///   topBreak    = 1 - (위쪽 테두리 높이 / rect 높이)
        /// </summary>
        internal Vector4 GetMaskSliceBorder()
        {
            EnsureGeometryCache();
            return _geoSliceBorder;
        }

        private Vector4 ComputeMaskSliceBorder()
        {
            var image = (UnityEngine.UI.Image)_uiGraphic;
            var sprite = image.sprite;

            Rect rect = _rectTransform.rect;
            float rectW = rect.width;
            float rectH = rect.height;
            if (rectW < 0.001f || rectH < 0.001f) return IdentitySlice;

            // 스프라이트 테두리 픽셀 → 캔버스 단위 변환
            // Unity는 테두리/타일 변환 모두 multipliedPixelsPerUnit 기준 — 타일 수 계산과 기준 통일
            float ppu = image.pixelsPerUnit * image.pixelsPerUnitMultiplier;
            if (ppu < 0.001f) ppu = 1f;

            float bL = sprite.border.x / ppu;
            float bB = sprite.border.y / ppu;
            float bR = sprite.border.z / ppu;
            float bT = sprite.border.w / ppu;

            // 반대편 테두리 합이 rect 크기를 초과할 경우 스케일 조정 (Unity 내부 동작과 동일)
            float totalX = bL + bR;
            if (totalX > rectW)
            {
                float scale = rectW / totalX;
                bL *= scale;
                bR *= scale;
            }
            float totalY = bB + bT;
            if (totalY > rectH)
            {
                float scale = rectH / totalY;
                bB *= scale;
                bT *= scale;
            }

            return new Vector4(
                bL / rectW,         // leftBreak
                bB / rectH,         // bottomBreak
                1f - bR / rectW,    // rightBreak
                1f - bT / rectH     // topBreak
            );
        }

        /// <summary>
        /// 9-슬라이스 내부 UV break point 계산 (스프라이트 UV 공간 기준)
        /// 반환값: (innerLeft, innerBottom, innerRight, innerTop) in [0,1]
        /// </summary>
        internal Vector4 GetMaskSliceInnerUV()
        {
            EnsureGeometryCache();
            return _geoSliceInnerUV;
        }

        /// <summary>9-슬라이스 X축 구간별 기울기 (사전 계산) — 캐시 조회</summary>
        internal Vector4 GetMaskSliceSlopeX()
        {
            EnsureGeometryCache();
            return _geoSliceSlopeX;
        }

        /// <summary>9-슬라이스 Y축 구간별 기울기 (사전 계산) — 캐시 조회</summary>
        internal Vector4 GetMaskSliceSlopeY()
        {
            EnsureGeometryCache();
            return _geoSliceSlopeY;
        }

        /// <summary>Filled 커버리지 반평면 A (사전 계산) — 캐시 조회</summary>
        internal Vector4 GetMaskFillLineA()
        {
            EnsureGeometryCache();
            return _geoFillLineA;
        }

        /// <summary>Filled 커버리지 반평면 B (사전 계산) — 캐시 조회</summary>
        internal Vector4 GetMaskFillLineB()
        {
            EnsureGeometryCache();
            return _geoFillLineB;
        }

        private Vector4 ComputeMaskSliceInnerUV()
        {
            var image = (UnityEngine.UI.Image)_uiGraphic;
            var sprite = image.sprite;

            Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            Vector4 innerUV = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);

            float outerW = outerUV.z - outerUV.x;
            float outerH = outerUV.w - outerUV.y;
            if (outerW < 0.0001f || outerH < 0.0001f) return new Vector4(0f, 0f, 1f, 1f);

            // 스프라이트 UV 공간 (0~1) 기준으로 내부 UV 정규화
            return new Vector4(
                (innerUV.x - outerUV.x) / outerW,  // innerLeft
                (innerUV.y - outerUV.y) / outerH,  // innerBottom
                (innerUV.z - outerUV.x) / outerW,  // innerRight
                (innerUV.w - outerUV.y) / outerH   // innerTop
            );
        }

        // ─────────────────────────────────────────────
        // 중첩 마스크
        // ─────────────────────────────────────────────

        /// <summary>
        /// 부모 SoftMask 검색
        /// </summary>
        private SoftMaskLight FindParentSoftMask()
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent<SoftMaskLight>(out var mask) && mask.enabled && mask._initialized)
                    return mask;
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// 자식이 이 SoftMask에 직접 속하는지 확인
        /// 중첩 SoftMask의 자식은 제외
        /// </summary>
        private bool BelongsToThisMask(Transform childTransform)
        {
            Transform current = childTransform.parent;
            while (current != null && current != transform)
            {
                if (current.TryGetComponent<SoftMaskLight>(out var mask) && mask.enabled)
                    return false;
                current = current.parent;
            }
            return true;
        }

        // ─────────────────────────────────────────────
        // 마스크 그래픽 표시/숨김
        // ─────────────────────────────────────────────

        private void UpdateMaskGraphicVisibility()
        {
            if (!_initialized || _uiGraphic == null) return;

            if (_showMaskGraphic)
            {
                // 숨김 → 표시 전환 시에만 알파 복원
                // 표시 상태에서는 색상에 개입하지 않음 (외부 트윈/애니메이션 허용)
                if (_maskColorHidden)
                {
                    _maskColorHidden = false;
                    Color c = _uiGraphic.color;
                    c.a = _savedMaskAlpha;
                    _uiGraphic.color = c;
                }
            }
            else
            {
                Color c = _uiGraphic.color;
                if (!_maskColorHidden)
                {
                    // 표시 → 숨김 전환: 현재 알파 저장 후 0으로
                    _maskColorHidden = true;
                    _savedMaskAlpha = c.a;
                    c.a = 0f;
                    _uiGraphic.color = c;
                }
                else if (c.a != 0f)
                {
                    // 숨김 중 외부에서 알파가 변경됨 → 새 알파를 기억하고 다시 숨김
                    _savedMaskAlpha = c.a;
                    c.a = 0f;
                    _uiGraphic.color = c;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Material 관리 (Optional Shader 기반)
        // ─────────────────────────────────────────────

        /// <summary>
        /// SoftMaskLightChildProxy에서 호출하여 공유 프록시 Material을 조회/생성
        /// 동일한 baseMaterial을 가진 자식끼리 같은 프록시 Material을 공유 (배칭 유지)
        /// baseMaterial의 프로퍼티를 복사하고 셰이더를 Hidden 변형으로 교체 + 마스크 프로퍼티 적용
        /// </summary>
        internal Material GetOrCreateProxyMaterial(Material baseMaterial, Shader optShader)
        {
            if (baseMaterial == null || optShader == null) return null;

            // 동일한 baseMaterial에 대해 이미 프록시 Material이 있으면 공유
            if (_sharedProxyMaterials.TryGetValue(baseMaterial, out var existing) && existing != null)
            {
                // 셰이더 불일치 감지 (baseMaterial 셰이더가 런타임에 변경된 경우)
                if (existing.shader == optShader)
                    return existing;

                // 기존 프록시 파괴 후 재생성
                _proxyMaterialSet.Remove(existing);
                if (Application.isPlaying) Destroy(existing);
                else DestroyImmediate(existing);
            }

            Material proxy = new Material(optShader)
            {
                name = $"{optShader.name} (SoftMaskLight: {gameObject.name})",
                hideFlags = HideFlags.HideAndDontSave
            };
            proxy.CopyPropertiesFromMaterial(baseMaterial);
            proxy.shader = optShader;
            // 셰이더 키워드/렌더 큐 유지 (CopyPropertiesFromMaterial은 프로퍼티만 복사)
            // 생성 시 1회만 수행되므로 shaderKeywords 배열 할당은 GC 부담 없음
            proxy.shaderKeywords = baseMaterial.shaderKeywords;
            proxy.renderQueue = baseMaterial.renderQueue;
            // RectMask2D 키워드/_ClipRect는 CanvasRenderer가 드로우마다 주입한다.
            // 공유 프록시에 구우면 _ClipRect=(0,0,0,0)으로 자식이 전부 사라진다.
            ResetRectMask2DMaterialState(proxy);

            // 마스크 프로퍼티 적용
            ApplyMaskPropertiesToMaterial(proxy);

            _sharedProxyMaterials[baseMaterial] = proxy;
            _proxyMaterialSet.Add(proxy);
            return proxy;
        }

        /// <summary>
        /// RectMask2D 클리핑은 CanvasRenderer가 드로우마다 주입한다.
        /// 공유 프록시에 UNITY_UI_CLIP_RECT와 _ClipRect=0이 남으면 자식이 전부 사라진다.
        /// </summary>
        internal static void ResetRectMask2DMaterialState(Material mat)
        {
            if (mat == null) return;
            mat.DisableKeyword(KeywordUIClipRect);
            mat.SetVector(PropClipRect, DefaultClipRect);
        }

        /// <summary>
        /// 공유 Material 프로퍼티 업데이트 (더티 체크 포함)
        /// Transform 변경 시에만 행렬 업데이트, 프로퍼티 변경 시에만 값 업데이트
        /// UI Mask 내에서 사용 시 Stencil 래핑 Material에도 프로퍼티 전파
        /// </summary>
        private void UpdateSharedMaterial()
        {
            using (s_MarkerUpdateShared.Auto())
                UpdateSharedMaterialInternal();
        }

        private void UpdateSharedMaterialInternal()
        {
            if (_originalChildMaterials.Count == 0) return;
            if (_sharedProxyMaterials.Count == 0 && _tmpMaskMaterials.Count == 0 && _childProxies.Count == 0 && _uiEffectProxies.Count == 0) return;

            bool anyChange = false;

            // 자신의 변환 행렬 더티 체크
            Matrix4x4 currentWorldToUV = ComputeWorldToMaskUV();
            if (_materialDirty || currentWorldToUV != _cachedWorldToUV)
            {
                foreach (var m in _sharedProxyMaterials.Values)
                    if (m != null) m.SetMatrix(PropMaskWorldToUV, currentWorldToUV);
                _cachedWorldToUV = currentWorldToUV;
                _cachedWorldToUVValid = true;
                anyChange = true;
            }

            // 마스크 텍스처 변경 체크
            Texture maskTex = GetMaskTexture();
            int texId = maskTex != null ? maskTex.GetInstanceID() : 0;
            if (texId != _cachedMaskTexId)
            {
                _cachedMaskTexId = texId;
                foreach (var m in _sharedProxyMaterials.Values)
                {
                    if (m == null) continue;
                    if (maskTex != null) m.SetTexture(PropMaskTex, maskTex);
                    m.SetVector(PropMaskUVRect, GetMaskUVRect());
                }
                anyChange = true;
            }

            // Softness / InvertMask 변경 체크
            if (_materialDirty || _softness != _cachedSoftness || _invertMask != _cachedInvertMask)
            {
                float softnessRcp = SoftnessRcp(_softness);
                foreach (var m in _sharedProxyMaterials.Values)
                {
                    if (m == null) continue;
                    m.SetFloat(PropSoftnessRcp, softnessRcp);
                    m.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
                }
                _cachedSoftness = _softness;
                _cachedInvertMask = _invertMask;
                anyChange = true;
            }

            // 형태 대응 값 계산 (키워드 적용은 부모 마스크까지 확인한 뒤 아래에서 일괄 처리)
            bool isSliced = IsSlicedMask();
            Vector4 sliceBorder = GetMaskSliceBorder();
            Vector4 sliceInnerUV = GetMaskSliceInnerUV();
            Vector4 sliceSlopeX = GetMaskSliceSlopeX();
            Vector4 sliceSlopeY = GetMaskSliceSlopeY();
            Vector4 fillLineA = GetMaskFillLineA();
            Vector4 fillLineB = GetMaskFillLineB();
            bool sliceChanged = _materialDirty || isSliced != _cachedIsSliced
                || sliceBorder != _cachedSliceBorder || sliceInnerUV != _cachedSliceInnerUV
                || sliceSlopeX != _cachedSliceSlopeX || sliceSlopeY != _cachedSliceSlopeY
                || fillLineA != _cachedFillLineA || fillLineB != _cachedFillLineB;
            // 캐시는 변경 감지 시에만 갱신한다. 무조건 갱신하면 Vector4 == 의 근사 비교 때문에
            // 프레임당 변화가 임계 미만인 초저속 fill 애니메이션이 영영 반영되지 않는다 (드리프트 누적 허용)
            if (sliceChanged)
            {
                _cachedIsSliced = isSliced;
                _cachedSliceBorder = sliceBorder;
                _cachedSliceInnerUV = sliceInnerUV;
                _cachedSliceSlopeX = sliceSlopeX;
                _cachedSliceSlopeY = sliceSlopeY;
                _cachedFillLineA = fillLineA;
                _cachedFillLineB = fillLineB;
            }

            // 부모 마스크 업데이트 (중첩 마스크)
            if (_hasParentMask && _parentSoftMask != null && _parentSoftMask.enabled)
            {
                Matrix4x4 parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                if (_materialDirty || parentWorldToUV != _cachedParentWorldToUV)
                {
                    foreach (var m in _sharedProxyMaterials.Values)
                        if (m != null) m.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                    _cachedParentWorldToUV = parentWorldToUV;
                    _cachedParentWorldToUVValid = true;
                    anyChange = true;
                }

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                int parentTexId = parentTex != null ? parentTex.GetInstanceID() : 0;
                if (parentTexId != _cachedParentMaskTexId)
                {
                    _cachedParentMaskTexId = parentTexId;
                    foreach (var m in _sharedProxyMaterials.Values)
                    {
                        if (m == null) continue;
                        if (parentTex != null) m.SetTexture(PropMaskTex2, parentTex);
                        m.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
                    }
                    anyChange = true;
                }

                if (_parentSoftMask._softness != _cachedParentSoftness ||
                    _parentSoftMask._invertMask != _cachedParentInvertMask)
                {
                    float parentSoftnessRcp = SoftnessRcp(_parentSoftMask._softness);
                    foreach (var m in _sharedProxyMaterials.Values)
                    {
                        if (m == null) continue;
                        m.SetFloat(PropSoftnessRcp2, parentSoftnessRcp);
                        m.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                    }
                    _cachedParentSoftness = _parentSoftMask._softness;
                    _cachedParentInvertMask = _parentSoftMask._invertMask;
                    anyChange = true;
                }

                // 부모 마스크 형태 대응 값 계산 (적용은 아래 일괄 처리)
                bool parentIsSliced = _parentSoftMask.IsSlicedMask();
                Vector4 parentSliceBorder = _parentSoftMask.GetMaskSliceBorder();
                Vector4 parentSliceInnerUV = _parentSoftMask.GetMaskSliceInnerUV();
                Vector4 parentSliceSlopeX = _parentSoftMask.GetMaskSliceSlopeX();
                Vector4 parentSliceSlopeY = _parentSoftMask.GetMaskSliceSlopeY();
                Vector4 parentFillLineA = _parentSoftMask.GetMaskFillLineA();
                Vector4 parentFillLineB = _parentSoftMask.GetMaskFillLineB();
                if (_materialDirty || parentIsSliced != _cachedParentIsSliced
                    || parentSliceBorder != _cachedParentSliceBorder || parentSliceInnerUV != _cachedParentSliceInnerUV
                    || parentSliceSlopeX != _cachedParentSliceSlopeX || parentSliceSlopeY != _cachedParentSliceSlopeY
                    || parentFillLineA != _cachedParentFillLineA || parentFillLineB != _cachedParentFillLineB)
                {
                    sliceChanged = true;
                    // 변경 감지 시에만 캐시 갱신 (근사 비교로 인한 초저속 애니메이션 정체 방지)
                    _cachedParentIsSliced = parentIsSliced;
                    _cachedParentSliceBorder = parentSliceBorder;
                    _cachedParentSliceInnerUV = parentSliceInnerUV;
                    _cachedParentSliceSlopeX = parentSliceSlopeX;
                    _cachedParentSliceSlopeY = parentSliceSlopeY;
                    _cachedParentFillLineA = parentFillLineA;
                    _cachedParentFillLineB = parentFillLineB;
                }
            }
            else if (_cachedParentIsSliced)
            {
                // 부모 마스크가 사라짐 → 항등값으로 되돌림
                _cachedParentIsSliced = false;
                _cachedParentSliceBorder = IdentitySlice;
                _cachedParentSliceInnerUV = IdentitySlice;
                _cachedParentSliceSlopeX = IdentitySliceSlope;
                _cachedParentSliceSlopeY = IdentitySliceSlope;
                _cachedParentFillLineA = IdentityFillLine;
                _cachedParentFillLineB = IdentityFillLineB;
                sliceChanged = true;
            }

            // 슬라이스 일괄 적용: _SOFTMASK_SLICE 하나가 마스크 1·2를 모두 담당한다.
            // 슬라이스가 아닌 쪽은 항등 파라미터가 들어가 리매핑이 no-op이 되므로 항상 값을 써 준다.
            if (sliceChanged)
            {
                bool anySliced = _cachedIsSliced || _cachedParentIsSliced;
                foreach (var m in _sharedProxyMaterials.Values)
                {
                    if (m == null) continue;
                    if (anySliced)
                    {
                        if (!m.IsKeywordEnabled(KEYWORD_SLICE)) m.EnableKeyword(KEYWORD_SLICE);
                        m.SetVector(PropMaskSliceBorder, _cachedSliceBorder);
                        m.SetVector(PropMaskSliceInnerUV, _cachedSliceInnerUV);
                        m.SetVector(PropMaskSliceSlopeX, _cachedSliceSlopeX);
                        m.SetVector(PropMaskSliceSlopeY, _cachedSliceSlopeY);
                        m.SetVector(PropMaskFillLineA, _cachedFillLineA);
                        m.SetVector(PropMaskFillLineB, _cachedFillLineB);
                        m.SetVector(PropMaskSliceBorder2, _cachedParentSliceBorder);
                        m.SetVector(PropMaskSliceInnerUV2, _cachedParentSliceInnerUV);
                        m.SetVector(PropMaskSliceSlopeX2, _cachedParentSliceSlopeX);
                        m.SetVector(PropMaskSliceSlopeY2, _cachedParentSliceSlopeY);
                        m.SetVector(PropMaskFillLineA2, _cachedParentFillLineA);
                        m.SetVector(PropMaskFillLineB2, _cachedParentFillLineB);
                    }
                    else if (m.IsKeywordEnabled(KEYWORD_SLICE))
                    {
                        m.DisableKeyword(KEYWORD_SLICE);
                    }
                }
                anyChange = true;
            }

            // TMP, ChildProxy, UIEffect, Stencil Material에 마스크 프로퍼티 전파
            if (anyChange || _materialDirty)
            {
                UpdateTMPMaterials();
                // 주의: ChildProxy의 ProxyMaterial은 _sharedProxyMaterials.Values와 동일 인스턴스이므로
                // 위 루프에서 이미 업데이트됨 (별도 전파 불필요)
                UpdateUIEffectMaterials();
                PropagateToStencilMaterials();
            }

            _materialDirty = false;

            // 파괴된 자식 정리는 매 프레임이 아니라 LateUpdate의 주기 스캔에서 수행 (아래 참조)
        }

        /// <summary>
        /// Stencil 래핑된 렌더링 Material에 마스크 프로퍼티 전파
        /// Unity UI Mask 내에서 사용 시, StencilMaterial.Add()가 생성한 복사본은
        /// 원본 Material 변경을 반영하지 않으므로 직접 프로퍼티를 설정
        /// </summary>
        private void PropagateToStencilMaterials()
        {
            using (s_MarkerPropagate.Auto())
                PropagateToStencilMaterialsInternal();
        }

        private void PropagateToStencilMaterialsInternal()
        {
            Texture maskTex = GetMaskTexture();
            Vector4 maskUVRect = GetMaskUVRect();

            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) continue;

                // materialForRendering은 접근할 때마다 IMaterialModifier 체인 전체를 재평가하므로
                // CanvasRenderer에 이미 설정된 최종 머티리얼을 직접 참조 (프로젝트 ActiveMaterial 패턴)
                var cr = kvp.Key.canvasRenderer;
                if (cr == null || cr.materialCount == 0) continue;
                Material rendered = cr.GetMaterial(0);
                if (rendered == null) continue;

                // 기본 Material 자체는 이미 업데이트됨 → 스킵
                if (IsChildProxyMaterial(rendered)) continue;
                if (_tmpMaskMaterials.Contains(rendered)) continue;
                if (IsUIEffectProxyMaterial(rendered)) continue;

                // Shader 인스턴스 비교로 TMP 또는 표준 프로퍼티 ID 결정 (문자열 비교 회피)
                Shader renderedShader = rendered.shader;
                if (renderedShader == null) continue;

                int pTex, pSoftnessRcp, pInvert, pWorldToUV, pUVRect;
                int pTex2, pSoftnessRcp2, pInvert2, pWorldToUV2, pUVRect2;
                int pSliceBorder, pSliceInnerUV, pSliceBorder2, pSliceInnerUV2;
                int pSliceSlopeX, pSliceSlopeY, pSliceSlopeX2, pSliceSlopeY2;
                int pFillLineA, pFillLineB, pFillLineA2, pFillLineB2;

                Shader tmpShader = GetCachedTMPShader();
                if (tmpShader != null && renderedShader == tmpShader)
                {
                    pTex = PropTMPMaskTex; pSoftnessRcp = PropTMPSoftnessRcp; pInvert = PropTMPInvertMask;
                    pWorldToUV = PropTMPMaskWorldToUV; pUVRect = PropTMPMaskUVRect;
                    pTex2 = PropTMPMaskTex2; pSoftnessRcp2 = PropTMPSoftnessRcp2; pInvert2 = PropTMPInvertMask2;
                    pWorldToUV2 = PropTMPMaskWorldToUV2; pUVRect2 = PropTMPMaskUVRect2;
                    pSliceBorder = PropTMPMaskSliceBorder; pSliceInnerUV = PropTMPMaskSliceInnerUV;
                    pSliceBorder2 = PropTMPMaskSliceBorder2; pSliceInnerUV2 = PropTMPMaskSliceInnerUV2;
                    pSliceSlopeX = PropTMPMaskSliceSlopeX; pSliceSlopeY = PropTMPMaskSliceSlopeY;
                    pSliceSlopeX2 = PropTMPMaskSliceSlopeX2; pSliceSlopeY2 = PropTMPMaskSliceSlopeY2;
                    pFillLineA = PropTMPMaskFillLineA; pFillLineB = PropTMPMaskFillLineB;
                    pFillLineA2 = PropTMPMaskFillLineA2; pFillLineB2 = PropTMPMaskFillLineB2;
                }
                else if (rendered.HasProperty(PropMaskTex))
                {
                    // _MaskTex 프로퍼티가 있는 셰이더 = SoftMaskLight 대응 셰이더 (표준 프로퍼티 이름)
                    pTex = PropMaskTex; pSoftnessRcp = PropSoftnessRcp; pInvert = PropInvertMask;
                    pWorldToUV = PropMaskWorldToUV; pUVRect = PropMaskUVRect;
                    pTex2 = PropMaskTex2; pSoftnessRcp2 = PropSoftnessRcp2; pInvert2 = PropInvertMask2;
                    pWorldToUV2 = PropMaskWorldToUV2; pUVRect2 = PropMaskUVRect2;
                    pSliceBorder = PropMaskSliceBorder; pSliceInnerUV = PropMaskSliceInnerUV;
                    pSliceBorder2 = PropMaskSliceBorder2; pSliceInnerUV2 = PropMaskSliceInnerUV2;
                    pSliceSlopeX = PropMaskSliceSlopeX; pSliceSlopeY = PropMaskSliceSlopeY;
                    pSliceSlopeX2 = PropMaskSliceSlopeX2; pSliceSlopeY2 = PropMaskSliceSlopeY2;
                    pFillLineA = PropMaskFillLineA; pFillLineB = PropMaskFillLineB;
                    pFillLineA2 = PropMaskFillLineA2; pFillLineB2 = PropMaskFillLineB2;
                }
                else
                {
                    continue;
                }

                // Stencil 래핑된 Material에 마스크 프로퍼티 복사
                rendered.SetMatrix(pWorldToUV, _cachedWorldToUV);
                rendered.SetFloat(pSoftnessRcp, SoftnessRcp(_cachedSoftness));
                rendered.SetFloat(pInvert, _cachedInvertMask ? 1f : 0f);

                if (maskTex != null) rendered.SetTexture(pTex, maskTex);
                rendered.SetVector(pUVRect, maskUVRect);

                // 슬라이스 프로퍼티 전파 (_SOFTMASK_SLICE 하나가 마스크 1·2를 모두 담당)
                if (_cachedIsSliced || _cachedParentIsSliced)
                {
                    if (!rendered.IsKeywordEnabled(KEYWORD_SLICE))
                        rendered.EnableKeyword(KEYWORD_SLICE);
                    rendered.SetVector(pSliceBorder, _cachedSliceBorder);
                    rendered.SetVector(pSliceInnerUV, _cachedSliceInnerUV);
                    rendered.SetVector(pSliceSlopeX, _cachedSliceSlopeX);
                    rendered.SetVector(pSliceSlopeY, _cachedSliceSlopeY);
                    rendered.SetVector(pFillLineA, _cachedFillLineA);
                    rendered.SetVector(pFillLineB, _cachedFillLineB);
                    rendered.SetVector(pSliceBorder2, _cachedParentSliceBorder);
                    rendered.SetVector(pSliceInnerUV2, _cachedParentSliceInnerUV);
                    rendered.SetVector(pSliceSlopeX2, _cachedParentSliceSlopeX);
                    rendered.SetVector(pSliceSlopeY2, _cachedParentSliceSlopeY);
                    rendered.SetVector(pFillLineA2, _cachedParentFillLineA);
                    rendered.SetVector(pFillLineB2, _cachedParentFillLineB);
                }
                else if (rendered.IsKeywordEnabled(KEYWORD_SLICE))
                {
                    rendered.DisableKeyword(KEYWORD_SLICE);
                }

                if (_hasParentMask)
                {
                    if (!rendered.IsKeywordEnabled(KEYWORD_NESTED))
                        rendered.EnableKeyword(KEYWORD_NESTED);

                    rendered.SetMatrix(pWorldToUV2, _cachedParentWorldToUV);
                    rendered.SetFloat(pSoftnessRcp2, SoftnessRcp(_cachedParentSoftness));
                    rendered.SetFloat(pInvert2, _cachedParentInvertMask ? 1f : 0f);

                    Texture parentTex = _parentSoftMask != null ? _parentSoftMask.GetMaskTexture() : null;
                    if (parentTex != null) rendered.SetTexture(pTex2, parentTex);
                    if (_parentSoftMask != null)
                        rendered.SetVector(pUVRect2, _parentSoftMask.GetMaskUVRect());
                }
            }
        }

        /// <summary>
        /// 해당 머티리얼이 자식 프록시 머티리얼인지 확인 (PropagateToStencilMaterials 스킵용)
        /// </summary>
        private bool IsChildProxyMaterial(Material mat)
        {
            return _proxyMaterialSet.Contains(mat);
        }

        /// <summary>
        /// 파괴된 자식 오브젝트 정리
        /// </summary>
        private void CleanupDestroyedChildren()
        {
            _toRemove.Clear();
            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) _toRemove.Add(kvp.Key);
            }

            for (int i = 0; i < _toRemove.Count; i++)
            {
                _originalChildMaterials.Remove(_toRemove[i]);
                if (_tmpAppliedMaskMats.TryGetValue(_toRemove[i], out var destroyedTmpMat))
                {
                    _tmpAppliedMaskMats.Remove(_toRemove[i]);
                    ReleaseTMPMaskMaterial(destroyedTmpMat);
                }
            }

            // 파괴된 Graphic이 누적되지 않도록 정리 (마스킹 불가 목록 누수 방지)
            if (_unmaskableChildren.Count > 0)
                _unmaskableChildren.RemoveWhere(s_IsNullGraphic);

            // 파괴된 Graphic의 TMP 원본 백업 엔트리 정리 (직렬화 리스트에 null 누적 방지)
            for (int i = _tmpOriginalBackup.Count - 1; i >= 0; i--)
            {
                if (_tmpOriginalBackup[i].graphic == null)
                    _tmpOriginalBackup.RemoveAt(i);
            }

            // 파괴된 프록시 정리
            for (int i = _childProxies.Count - 1; i >= 0; i--)
            {
                if (_childProxies[i] == null)
                {
                    _childProxySet.Remove(_childProxies[i]);
                    _childProxies.RemoveAt(i);
                }
            }
            for (int i = _uiEffectProxies.Count - 1; i >= 0; i--)
            {
                if (_uiEffectProxies[i] == null)
                    _uiEffectProxies.RemoveAt(i);
            }

            // 사용되지 않는 프록시 Material 정리
            CleanupStaleProxyMaterials();
        }

        /// <summary>
        /// _sharedProxyMaterials에서 키(baseMaterial)가 파괴되었거나
        /// 더 이상 사용되지 않는 프록시 Material 정리
        /// </summary>
        private void CleanupStaleProxyMaterials()
        {
            if (_sharedProxyMaterials.Count > 0)
            {
                _toRemoveMaterials.Clear();
                foreach (var kvp in _sharedProxyMaterials)
                {
                    // 키(baseMaterial)가 파괴됨 → 프록시도 함께 파괴
                    if (kvp.Key == null)
                    {
                        if (kvp.Value != null)
                        {
                            _proxyMaterialSet.Remove(kvp.Value);
                            if (Application.isPlaying) Destroy(kvp.Value);
                            else DestroyImmediate(kvp.Value);
                        }
                        _toRemoveMaterials.Add(kvp.Key);
                        continue;
                    }
                    // 값(proxyMaterial)이 외부에서 파괴됨 → 엔트리만 제거
                    if (kvp.Value == null)
                    {
                        _proxyMaterialSet.Remove(kvp.Value);
                        _toRemoveMaterials.Add(kvp.Key);
                    }
                }

                for (int i = 0; i < _toRemoveMaterials.Count; i++)
                    _sharedProxyMaterials.Remove(_toRemoveMaterials[i]);
            }

            // UIEffect 공유 프록시 캐시도 동일하게 정리
            // (UIEffect가 설정 변경으로 baseMaterial을 재생성하면 옛 키가 파괴됨)
            if (_uiEffectProxyMaterials.Count > 0)
            {
                _toRemoveMaterials.Clear();
                foreach (var kvp in _uiEffectProxyMaterials)
                {
                    if (kvp.Key == null)
                    {
                        if (kvp.Value != null)
                        {
                            _uiEffectProxyMaterialSet.Remove(kvp.Value);
                            if (Application.isPlaying) Destroy(kvp.Value);
                            else DestroyImmediate(kvp.Value);
                        }
                        _toRemoveMaterials.Add(kvp.Key);
                        continue;
                    }
                    if (kvp.Value == null)
                    {
                        _uiEffectProxyMaterialSet.Remove(kvp.Value);
                        _toRemoveMaterials.Add(kvp.Key);
                    }
                }

                for (int i = 0; i < _toRemoveMaterials.Count; i++)
                    _uiEffectProxyMaterials.Remove(_toRemoveMaterials[i]);
            }
        }

        // ─────────────────────────────────────────────
        // 자식 오브젝트 마스킹
        // ─────────────────────────────────────────────

        /// <summary>
        /// 외부에서 자식의 머티리얼이 변경된 경우, 해당 자식의 추적을 무효화하여
        /// 다음 ApplyMaskToChildren에서 재처리되도록 합니다.
        /// (예: Windable/UIShining 에디터 테스트에서 _graphic.material을 교체한 경우)
        /// </summary>
        public void InvalidateChild(UnityEngine.UI.Graphic child)
        {
            if (child == null) return;

            // 일반 프록시 컴포넌트 제거
            var childProxy = child.GetComponent<SoftMaskLightChildProxy>();
            if (childProxy != null && !childProxy.IsCleanedUp)
            {
                _childProxies.Remove(childProxy);
                _childProxySet.Remove(childProxy);
                childProxy.Cleanup();
            }

            _originalChildMaterials.Remove(child);
            ApplyMaskToChildren();
        }

        /// <summary>
        /// 자식 오브젝트에 공유 마스크 Material 적용
        /// </summary>
        public void ApplyMaskToChildren()
        {
            using (s_MarkerApplyMask.Auto())
                ApplyMaskToChildrenInternal();
        }

        private void ApplyMaskToChildrenInternal()
        {
            if (!_initialized) return;

            Texture maskTex = GetMaskTexture();
            if (maskTex == null) return;

            GetComponentsInChildren(true, _childGraphicsBuffer);
            var children = _childGraphicsBuffer;
            foreach (var child in children)
            {
                if (child.gameObject == gameObject) continue;
                if (!BelongsToThisMask(child.transform)) continue;
                if (_originalChildMaterials.ContainsKey(child)) continue;

                // TMP_Text (TextMeshProUGUI 포함)
                // TMP는 materialForRendering이 m_sharedMaterial을 사용하므로
                // Graphic.material 대신 fontSharedMaterial로 접근해야 함
                if (child is TMP_Text tmpText)
                {
                    Material originalFontMat = tmpText.fontSharedMaterial;

                    // 플레이모드 진입 시 DontSave 마스크 Material이 유실되어
                    // fontSharedMaterial이 null 또는 폰트 기본 Material로 폴백된 경우
                    // 직렬화된 백업에서 원본 프리셋 Material 복원
                    Material backup = FindTMPOriginalBackup(child);
                    if (backup != null && originalFontMat != backup)
                    {
                        originalFontMat = backup;
                        tmpText.fontSharedMaterial = backup;
                    }

                    if (originalFontMat == null)
                    {
                        _unmaskableChildren.Add(child);
                        continue;
                    }

                    _originalChildMaterials[child] = originalFontMat;
                    SaveTMPOriginalBackup(child, originalFontMat);

                    Material tmpMat = CreateTMPMaskMaterial(originalFontMat);
                    if (tmpMat != null)
                    {
                        tmpText.fontSharedMaterial = tmpMat;
                        _tmpAppliedMaskMats[child] = tmpMat;
                        child.SetAllDirty();
                    }
                    continue;
                }

                // UIEffect — IMaterialModifier 체인으로 프록시 머티리얼 생성
                // _originalChildMaterials 저장 전에 체크해야 ApplyMaskToUIEffect의
                // ContainsKey 가드가 올바르게 동작함
                if (IsUIEffectGraphic(child))
                {
                    ApplyMaskToUIEffect(child);
                    continue;
                }

                // TMP_SubMeshUI — fontSharedMaterial 방식 유지
                Material subOriginal = child.material;
                Material subBackup = FindTMPOriginalBackup(child);
                if (subBackup != null && subOriginal != subBackup)
                {
                    subOriginal = subBackup;
                    child.material = subBackup;
                }
                if (IsTMPMaterial(subOriginal))
                {
                    _originalChildMaterials[child] = subOriginal;
                    SaveTMPOriginalBackup(child, subOriginal);
                    Material tmpMat = CreateTMPMaskMaterial(subOriginal);
                    if (tmpMat != null)
                    {
                        child.material = tmpMat;
                        _tmpAppliedMaskMats[child] = tmpMat;
                        child.SetAllDirty();
                    }
                    continue;
                }

                // ─────────────────────────────────────────
                // 일반 Graphic + 커스텀 셰이더 + 파티클 → SoftMaskLightChildProxy
                // graphic.m_Material을 건드리지 않고 IMaterialModifier 체인으로 마스킹
                // ─────────────────────────────────────────

                // 이미 유효한 프록시가 있으면 재사용
                var existingProxy = child.GetComponent<SoftMaskLightChildProxy>();
                if (existingProxy != null && !existingProxy.IsCleanedUp && existingProxy.SoftMask == this)
                {
                    _originalChildMaterials[child] = null; // 프록시 관리 표시
                    if (!_childProxies.Contains(existingProxy))
                        _childProxies.Add(existingProxy);
                    child.SetMaterialDirty();
                    continue;
                }

                // Optional Shader 존재 확인 (없으면 마스킹 불가 → 스킵)
                // 스킵된 자식은 기록해 둔다. 기록하지 않으면 다음 스캔에서 다시 "미등록 자식"으로
                // 잡혀 ApplyMaskToChildren이 매번 재호출되는 무한 재적용 루프가 된다.
                Shader optShader = FindOptionalShader(child.material != null ? child.material.shader : null);
                if (optShader == null)
                {
                    _unmaskableChildren.Add(child);
                    continue;
                }

                // 다른 마스크(외부 마스크)가 관리 중이던 자식을 인수하는 경우
                // → 이전 마스크의 추적에서 먼저 제거하여 프로퍼티 경합(깜빡임) 방지
                // (이 과정에서 기존 프록시가 정리될 수 있으므로 반드시 생성 판단 이전에 수행)
                if (existingProxy != null && !existingProxy.IsCleanedUp &&
                    existingProxy.SoftMask != null && existingProxy.SoftMask != this)
                {
                    existingProxy.SoftMask.NotifyChildMovedOut(child);
                }

                // 프록시 컴포넌트 생성 및 초기화
                if (existingProxy == null || existingProxy.IsCleanedUp)
                    existingProxy = child.gameObject.AddComponent<SoftMaskLightChildProxy>();

                existingProxy.Initialize(this);

                if (_childProxySet.Add(existingProxy))
                    _childProxies.Add(existingProxy);

                // 프록시 관리 자식으로 등록 (원본 Material = null: 프록시가 관리)
                _originalChildMaterials[child] = null;

                // Canvas 재빌드 트리거 → GetModifiedMaterial() 호출 → 프록시 Material 생성
                child.SetMaterialDirty();
            }

            // 마스크 적용 완료 후 다음 프레임에서 UIEffect 동적 추가 감지 예약
            _checkUIEffectPending = true;
        }

        /// <summary>
        /// 단일 자식의 원본 Material 복원 (마스크 밖으로 이동한 경우)
        /// </summary>
        private void RestoreSingleChild(UnityEngine.UI.Graphic child, Material originalMat)
        {
            if (child == null) return;

            // 프록시 관리 자식 (originalMat == null): UIEffect 또는 일반 프록시
            if (originalMat == null)
            {
                // 주의: 다른 SoftMaskLight(예: 런타임에 삽입된 중첩 마스크)가 이미 인수한
                // 프록시는 파괴하지 않음 — 이 마스크 소유의 프록시만 정리
                var uiProxy = child.GetComponent<UIEffectSoftMaskLightProxy>();
                if (uiProxy != null && !uiProxy.IsCleanedUp && uiProxy.SoftMask == this)
                    uiProxy.Cleanup();

                var childProxy = child.GetComponent<SoftMaskLightChildProxy>();
                if (childProxy != null && !childProxy.IsCleanedUp && childProxy.SoftMask == this)
                {
                    _childProxies.Remove(childProxy);
                    _childProxySet.Remove(childProxy);
                    childProxy.Cleanup();
                }
                else
                {
                    child.SetMaterialDirty();
                }
                return;
            }

            // TMP_Text는 fontSharedMaterial로 복원
            if (child is TMP_Text tmpText)
                tmpText.fontSharedMaterial = originalMat;
            else
                child.material = originalMat;

            // TMP 마스크 Material 정리 (공유 중이면 마지막 사용자만 파괴)
            if (_tmpAppliedMaskMats.TryGetValue(child, out var tmpMat))
            {
                _tmpAppliedMaskMats.Remove(child);
                ReleaseTMPMaskMaterial(tmpMat);
            }
        }

        /// <summary>
        /// 자식이 마스크 밖으로 이동했을 때 프록시가 호출하는 즉시 복원 경로
        /// (플레이모드에서 SetParent 등으로 이탈 시 8프레임 스로틀을 기다리지 않고 복원)
        /// </summary>
        internal void NotifyChildMovedOut(UnityEngine.UI.Graphic child)
        {
            if (child == null) return;
            if (!_originalChildMaterials.TryGetValue(child, out var origMat)) return;

            // 아직 이 마스크에 직접 속해 있으면 무시 (마스크 내부 이동)
            if (child.transform.IsChildOf(transform) && BelongsToThisMask(child.transform)) return;

            RestoreSingleChild(child, origMat);
            _originalChildMaterials.Remove(child);
            _tmpAppliedMaskMats.Remove(child);
        }

        /// <summary>
        /// 자식 오브젝트의 원본 Material 복원
        /// </summary>
        public void RestoreChildrenMaterials()
        {
            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) continue;

                // 프록시 관리 자식 (값 == null): UIEffect 또는 일반 프록시
                // materialForRendering이 프록시 머티리얼을 참조하므로 canvas 재빌드 트리거
                if (kvp.Value == null)
                {
                    kvp.Key.SetMaterialDirty();
                    continue;
                }

                // TMP_Text는 fontSharedMaterial로 복원
                if (kvp.Key is TMP_Text tmpText)
                    tmpText.fontSharedMaterial = kvp.Value;
                else
                    kvp.Key.material = kvp.Value;
            }

            _originalChildMaterials.Clear();

            // TMP Material 파괴
            for (int i = 0; i < _tmpMaskMaterials.Count; i++)
            {
                if (_tmpMaskMaterials[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(_tmpMaskMaterials[i]);
                    else
                        DestroyImmediate(_tmpMaskMaterials[i]);
                }
            }
            _tmpMaskMaterials.Clear();
            _tmpAppliedMaskMats.Clear();
            _tmpSharedMaskMats.Clear();

            // 공유 프록시 Material 파괴
            foreach (var mat in _sharedProxyMaterials.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying) Destroy(mat);
                    else DestroyImmediate(mat);
                }
            }
            _sharedProxyMaterials.Clear();
            _proxyMaterialSet.Clear();

            // UIEffect 공유 프록시 Material 파괴 (소유권은 SoftMaskLight)
            foreach (var mat in _uiEffectProxyMaterials.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying) Destroy(mat);
                    else DestroyImmediate(mat);
                }
            }
            _uiEffectProxyMaterials.Clear();
            _uiEffectProxyMaterialSet.Clear();

            // SoftMaskLightChildProxy 컴포넌트 정리
            for (int i = 0; i < _childProxies.Count; i++)
            {
                if (_childProxies[i] != null)
                    _childProxies[i].Cleanup();
            }
            _childProxies.Clear();
            _childProxySet.Clear();
            _unmaskableChildren.Clear();

            // UIEffect 프록시 컴포넌트 정리
            for (int i = 0; i < _uiEffectProxies.Count; i++)
            {
                if (_uiEffectProxies[i] != null)
                    _uiEffectProxies[i].Cleanup();
            }
            _uiEffectProxies.Clear();
        }

        // ─────────────────────────────────────────────
        // TMP 지원
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material이 TextMeshPro 셰이더를 사용하는지 판별
        /// 셰이더 이름 기반으로 TMP/SubMeshUI 등 모든 TMP 변형 감지
        /// </summary>
        private static bool IsTMPMaterial(Material mat)
        {
            return mat != null && mat.shader != null &&
                   mat.shader.name.Contains("TextMeshPro");
        }

        /// <summary>
        /// TMP 외부 Material 변경 감지 및 마스크 자동 재적용
        /// 사용자가 TMP Material Preset을 변경했을 때 (Outline, Underlay 등)
        /// 새 Material에 마스크를 자동 적용하고 원본 참조를 갱신
        /// </summary>
        private void DetectTMPMaterialChanges()
        {
            if (_tmpAppliedMaskMats.Count == 0) return;

            _toRemove.Clear();

            foreach (var kvp in _tmpAppliedMaskMats)
            {
                if (kvp.Key == null) { _toRemove.Add(kvp.Key); continue; }

                // 현재 Material 가져오기
                Material currentMat;
                if (kvp.Key is TMP_Text tmpText)
                    currentMat = tmpText.fontSharedMaterial;
                else
                    currentMat = kvp.Key.material;

                // 적용한 마스크 Material과 동일 → 변경 없음
                if (currentMat == kvp.Value || currentMat == null) continue;

                // 사용자가 외부에서 Material을 변경함
                _toRemove.Add(kvp.Key);
            }

            if (_toRemove.Count == 0) return;

            for (int i = 0; i < _toRemove.Count; i++)
            {
                var child = _toRemove[i];

                // null 오브젝트 정리
                if (child == null)
                {
                    _tmpAppliedMaskMats.Remove(child);
                    _originalChildMaterials.Remove(child);
                    continue;
                }

                // 기존 마스크 Material 정리 (공유 중이면 마지막 사용자만 파괴)
                if (_tmpAppliedMaskMats.TryGetValue(child, out Material oldMaskMat))
                {
                    _tmpAppliedMaskMats.Remove(child);
                    ReleaseTMPMaskMaterial(oldMaskMat);
                }

                // 사용자의 새 Material 가져오기
                Material userMat;
                if (child is TMP_Text tmp)
                    userMat = tmp.fontSharedMaterial;
                else
                    userMat = child.material;

                if (userMat == null)
                {
                    _tmpAppliedMaskMats.Remove(child);
                    _originalChildMaterials.Remove(child);
                    continue;
                }

                // 원본 Material 갱신 (비활성화 시 이 Material로 복원됨)
                _originalChildMaterials[child] = userMat;
                SaveTMPOriginalBackup(child, userMat);

                // 새 마스크 Material 생성 및 적용
                Material newMaskMat = CreateTMPMaskMaterial(userMat);
                if (newMaskMat != null)
                {
                    if (child is TMP_Text tmpChild)
                        tmpChild.fontSharedMaterial = newMaskMat;
                    else
                        child.material = newMaskMat;

                    _tmpAppliedMaskMats[child] = newMaskMat;
                    child.SetAllDirty();
                }
                else
                {
                    _tmpAppliedMaskMats.Remove(child);
                }
            }

            _materialDirty = true;
        }

        /// <summary>
        /// TMP 전용 SoftMask Material 생성
        /// 원본 TMP Material의 폰트 아틀라스, 색상, SDF 파라미터를 복사하고
        /// SoftMask 프로퍼티를 추가 설정
        /// </summary>
        private Material CreateTMPMaskMaterial(Material originalTMPMat)
        {
            // 동일 원본 폰트 Material을 쓰는 자식끼리 마스크 Material 공유 (드로우콜 N → 1)
            if (originalTMPMat != null &&
                _tmpSharedMaskMats.TryGetValue(originalTMPMat, out var shared) && shared != null)
                return shared;

            Shader shader = GetCachedTMPShader();
            if (shader == null) return null;

            Material tmpMat = new Material(shader)
            {
                name = $"{TMP_SHADER_NAME} (SoftMaskLight: {gameObject.name})",
                hideFlags = HideFlags.DontSave
            };

            // TMP 프로퍼티 개별 복사 (CopyPropertiesFromMaterial 대신)
            // CopyPropertiesFromMaterial()은 Material 프로퍼티 시트를 통째로 교체하여
            // 우리 셰이더의 _SoftMask* 프로퍼티를 제거하는 부작용이 있음
            // 원본 셰이더의 프로퍼티만 개별 복사하면 대상 셰이더 고유 프로퍼티가 보존됨
            CopyShaderProperties(tmpMat, originalTMPMat);

            // TMP 셰이더 키워드 유지 (OUTLINE_ON, UNDERLAY_ON 등)
            foreach (string keyword in originalTMPMat.shaderKeywords)
            {
                tmpMat.EnableKeyword(keyword);
            }
            ResetRectMask2DMaterialState(tmpMat);

            // 렌더 큐 보존
            tmpMat.renderQueue = originalTMPMat.renderQueue;

            // 셰이더 컴파일 실패 시 폴백
            if (!shader.isSupported)
            {
                Debug.LogWarning($"[SoftMaskLight] TMP 셰이더가 지원되지 않습니다: {TMP_SHADER_NAME}");
                _tmpMaskMaterials.Add(tmpMat);
                if (originalTMPMat != null) _tmpSharedMaskMats[originalTMPMat] = tmpMat;
                return tmpMat;
            }

            // SoftMask 프로퍼티 설정 (TMP 전용 프로퍼티 ID 사용)
            ApplyMaskPropertiesToTMPMaterial(tmpMat);

            _tmpMaskMaterials.Add(tmpMat);
            if (originalTMPMat != null) _tmpSharedMaskMats[originalTMPMat] = tmpMat;
            return tmpMat;
        }

        /// <summary>
        /// TMP 마스크 Material 해제. 다른 TMP 자식이 아직 공유 중이면 유지하고,
        /// 마지막 사용자가 해제할 때만 파괴한다.
        /// 호출 전에 _tmpAppliedMaskMats에서 해당 자식 엔트리를 먼저 제거할 것.
        /// </summary>
        private void ReleaseTMPMaskMaterial(Material maskMat)
        {
            if (maskMat == null) return;

            foreach (var m in _tmpAppliedMaskMats.Values)
                if (m == maskMat) return; // 아직 다른 자식이 공유 중

            _tmpMaskMaterials.Remove(maskMat);

            // 공유 캐시에서 역방향 제거
            _toRemoveMaterials.Clear();
            foreach (var kvp in _tmpSharedMaskMats)
                if (kvp.Value == maskMat) _toRemoveMaterials.Add(kvp.Key);
            for (int i = 0; i < _toRemoveMaterials.Count; i++)
                _tmpSharedMaskMats.Remove(_toRemoveMaterials[i]);

            if (Application.isPlaying) Destroy(maskMat);
            else DestroyImmediate(maskMat);
        }

        /// <summary>
        /// TMP Material에 현재 마스크 프로퍼티 일괄 전파
        /// </summary>
        private void UpdateTMPMaterials()
        {
            for (int i = _tmpMaskMaterials.Count - 1; i >= 0; i--)
            {
                Material tmpMat = _tmpMaskMaterials[i];
                if (tmpMat == null)
                {
                    _tmpMaskMaterials.RemoveAt(i);
                    continue;
                }
                if (tmpMat.shader == null || !tmpMat.shader.isSupported) continue;
                ApplyMaskPropertiesToTMPMaterial(tmpMat);
            }
        }

        /// <summary>
        /// TMP Material에 마스크 프로퍼티 설정 (TMP 전용 _SoftMask* 접두사 프로퍼티 ID 사용)
        /// CreateTMPMaskMaterial, UpdateTMPMaterials에서 공통 사용
        /// </summary>
        private void ApplyMaskPropertiesToTMPMaterial(Material mat)
        {
            if (mat == null) return;

            Texture maskTex = GetMaskTexture();
            Vector4 maskUVRect = GetMaskUVRect();

            Matrix4x4 worldToUV = _cachedWorldToUV;
            if (!_cachedWorldToUVValid)
                worldToUV = ComputeWorldToMaskUV();

            // 소프트니스/인버트는 캐시가 아닌 현재 값 사용 (첫 프레임 캐시 미초기화 대비)
            mat.SetMatrix(PropTMPMaskWorldToUV, worldToUV);
            mat.SetFloat(PropTMPSoftnessRcp, SoftnessRcp(_softness));
            mat.SetFloat(PropTMPInvertMask, _invertMask ? 1f : 0f);
            if (maskTex != null) mat.SetTexture(PropTMPMaskTex, maskTex);
            mat.SetVector(PropTMPMaskUVRect, maskUVRect);

            // 형태 대응 프로퍼티 (_SOFTMASK_SLICE 하나가 마스크 1·2를 모두 담당)
            // 지연 캐시가 아닌 지오메트리 캐시 게터 사용 — 첫 프레임부터 올바른 형태 적용 보장
            bool hasParent = _hasParentMask && _parentSoftMask != null;
            if (IsSlicedMask() || (hasParent && _parentSoftMask.IsSlicedMask()))
            {
                if (!mat.IsKeywordEnabled(KEYWORD_SLICE))
                    mat.EnableKeyword(KEYWORD_SLICE);
                mat.SetVector(PropTMPMaskSliceBorder, GetMaskSliceBorder());
                mat.SetVector(PropTMPMaskSliceInnerUV, GetMaskSliceInnerUV());
                mat.SetVector(PropTMPMaskSliceSlopeX, GetMaskSliceSlopeX());
                mat.SetVector(PropTMPMaskSliceSlopeY, GetMaskSliceSlopeY());
                mat.SetVector(PropTMPMaskFillLineA, GetMaskFillLineA());
                mat.SetVector(PropTMPMaskFillLineB, GetMaskFillLineB());
                mat.SetVector(PropTMPMaskSliceBorder2, hasParent ? _parentSoftMask.GetMaskSliceBorder() : IdentitySlice);
                mat.SetVector(PropTMPMaskSliceInnerUV2, hasParent ? _parentSoftMask.GetMaskSliceInnerUV() : IdentitySlice);
                mat.SetVector(PropTMPMaskSliceSlopeX2, hasParent ? _parentSoftMask.GetMaskSliceSlopeX() : IdentitySliceSlope);
                mat.SetVector(PropTMPMaskSliceSlopeY2, hasParent ? _parentSoftMask.GetMaskSliceSlopeY() : IdentitySliceSlope);
                mat.SetVector(PropTMPMaskFillLineA2, hasParent ? _parentSoftMask.GetMaskFillLineA() : IdentityFillLine);
                mat.SetVector(PropTMPMaskFillLineB2, hasParent ? _parentSoftMask.GetMaskFillLineB() : IdentityFillLineB);
            }
            else if (mat.IsKeywordEnabled(KEYWORD_SLICE))
            {
                mat.DisableKeyword(KEYWORD_SLICE);
            }

            // 중첩 마스크 프로퍼티
            if (_hasParentMask && _parentSoftMask != null)
            {
                if (!mat.IsKeywordEnabled(KEYWORD_NESTED))
                    mat.EnableKeyword(KEYWORD_NESTED);

                Matrix4x4 parentWorldToUV = _cachedParentWorldToUV;
                if (!_cachedParentWorldToUVValid)
                    parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                mat.SetMatrix(PropTMPMaskWorldToUV2, parentWorldToUV);
                mat.SetFloat(PropTMPSoftnessRcp2, SoftnessRcp(_parentSoftMask._softness));
                mat.SetFloat(PropTMPInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                if (parentTex != null) mat.SetTexture(PropTMPMaskTex2, parentTex);
                mat.SetVector(PropTMPMaskUVRect2, _parentSoftMask.GetMaskUVRect());
            }
            else
            {
                if (mat.IsKeywordEnabled(KEYWORD_NESTED))
                    mat.DisableKeyword(KEYWORD_NESTED);
            }
        }

        // ─────────────────────────────────────────────
        // UIEffect 지원
        // ─────────────────────────────────────────────

        /// <summary>
        /// 해당 Graphic이 UIEffect 또는 UIEffectReplica 컴포넌트를 보유하는지 확인
        /// </summary>
        private static bool IsUIEffectGraphic(UnityEngine.UI.Graphic child)
        {
#if UIEFFECT_ENABLED
            return child.GetComponent<UIEffect>() != null
                || child.GetComponent<UIEffectReplica>() != null;
#else
            return false;
#endif
        }

        /// <summary>
        /// UIEffect 자식에 프록시 컴포넌트를 추가하여 _CAT_SOFTMASK 키워드가 활성화된
        /// 프록시 머티리얼이 IMaterialModifier 체인에서 생성되도록 트리거
        /// </summary>
        private void ApplyMaskToUIEffect(UnityEngine.UI.Graphic child)
        {
            // 이미 원본 머티리얼 매핑이 있으면 재처리 불필요
            // (이미 프록시가 적용된 상태)
            if (_originalChildMaterials.ContainsKey(child)) return;

            // 프록시 컴포넌트 확보 (없으면 추가)
            // Cleanup() → Destroy(this)로 프레임 끝 지연 파괴 중인 zombie 프록시는 재사용 불가
            // → 새 프록시 추가 (zombie는 GetModifiedMaterial에서 패스스루, OnDestroy에서 머티리얼 미파괴)
            var proxy = child.GetComponent<UIEffectSoftMaskLightProxy>();

            // 다른 마스크가 관리 중이던 자식 인수 → 이전 마스크 추적을 먼저 제거 (경합 방지)
            if (proxy != null && !proxy.IsCleanedUp &&
                proxy.SoftMask != null && proxy.SoftMask != this)
            {
                proxy.SoftMask.NotifyChildMovedOut(child);
            }

            if (proxy == null || proxy.IsCleanedUp)
                proxy = child.gameObject.AddComponent<UIEffectSoftMaskLightProxy>();

            proxy.Initialize(this);

            if (!_uiEffectProxies.Contains(proxy))
                _uiEffectProxies.Add(proxy);

            // 원본 머티리얼 기록 (복원 시 사용)
            // UIEffect 자식은 child.material이 UIEffect에 의해 관리되므로
            // 여기서는 null을 저장하여 복원 시 material 리셋 없이 프록시만 제거
            _originalChildMaterials[child] = null;

            // Canvas 재빌드 트리거 → GetModifiedMaterial() 호출 → 프록시 머티리얼 생성
            child.SetMaterialDirty();
        }

        /// <summary>
        /// UIEffectSoftMaskLightProxy에서 호출하여 공유 프록시 Material을 조회/생성.
        /// UIEffect는 이펙트 설정별로 baseMaterial을 공유하므로, 같은 baseMaterial을 가진
        /// 자식끼리 프록시를 공유해 배칭을 유지한다 (소유권은 SoftMaskLight).
        ///
        /// baseMaterial이 마스크 프로퍼티(_MaskTex)를 갖지 않으면(= 패키지 UIEffect 셰이더)
        /// 설정 에셋에 직렬화된 오버라이드 셰이더로 교체한다.
        /// Shader.Find는 이름 충돌 시 임포트 순서에 따라 아무 쪽이나 선택하므로 사용하지 않는다.
        /// 키워드는 Material에 보존되므로 셰이더 교체 후에도 UIEffect의 shader_feature 변형이 유지된다.
        /// 오버라이드 부재 시 null 반환 (호출부에서 마스킹 스킵).
        /// </summary>
        internal Material GetOrCreateUIEffectProxyMaterial(Material baseMaterial)
        {
            if (baseMaterial == null) return null;

            if (_uiEffectProxyMaterials.TryGetValue(baseMaterial, out var existing) && existing != null)
            {
                // CopyPropertiesFromMaterial은 TexEnv 시트를 원본으로 교체해 _MaskTex 슬롯이 사라진다.
                // CopyMatching은 대상 고유 슬롯을 유지한다.
                existing.CopyMatchingPropertiesFromMaterial(baseMaterial);
                if (!existing.IsKeywordEnabled(KEYWORD_CAT_SOFTMASK))
                    existing.EnableKeyword(KEYWORD_CAT_SOFTMASK);
                ResetRectMask2DMaterialState(existing);
                ApplyMaskPropertiesToMaterial(existing);
                Texture existingMask = GetMaskTexture();
                if (existingMask == null || existing.GetTexture(PropMaskTex) != null)
                    return existing;

                _uiEffectProxyMaterials.Remove(baseMaterial);
                _uiEffectProxyMaterialSet.Remove(existing);
                if (Application.isPlaying) Destroy(existing);
                else DestroyImmediate(existing);
            }

            // 패키지 UIEffect 셰이더면 마스크 대응 오버라이드로 교체 필요
            Shader overrideShader = null;
            if (!baseMaterial.HasProperty(PropMaskTex))
            {
                var settings = SoftMaskLightSettings.Instance;
                overrideShader = settings != null ? settings.UIEffectOverrideShader : null;
                if (overrideShader == null) return null; // 오버라이드 부재 → 마스킹 불가
            }

            // 오버라이드 셰이더로 생성해야 _MaskTex TexEnv 슬롯이 생긴다.
            Material proxy;
            if (overrideShader != null)
            {
                proxy = new Material(overrideShader)
                {
                    name = $"UIEffect Proxy (SoftMaskLight: {gameObject.name})",
                    hideFlags = HideFlags.HideAndDontSave
                };
                proxy.CopyMatchingPropertiesFromMaterial(baseMaterial);
            }
            else
            {
                proxy = new Material(baseMaterial)
                {
                    name = $"UIEffect Proxy (SoftMaskLight: {gameObject.name})",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            proxy.EnableKeyword(KEYWORD_CAT_SOFTMASK);
            ResetRectMask2DMaterialState(proxy);
            ApplyMaskPropertiesToMaterial(proxy);

            _uiEffectProxyMaterials[baseMaterial] = proxy;
            _uiEffectProxyMaterialSet.Add(proxy);
            return proxy;
        }

        /// <summary>
        /// 지정된 머티리얼에 현재 마스크 프로퍼티를 적용
        /// UIEffectSoftMaskLightProxy.GetModifiedMaterial()에서 호출되어
        /// 캔버스 리빌드 시점에 프록시 머티리얼에 마스크 프로퍼티를 직접 적용
        ///
        /// 핵심: LateUpdate(UpdateSharedMaterial)는 Canvas 리빌드 이전에 실행되므로
        /// _cachedWorldToUV 등 캐시 값은 항상 최신 상태
        /// </summary>
        internal void ApplyMaskPropertiesToMaterial(Material mat)
        {
            if (mat == null) return;

            Texture maskTex = GetMaskTexture();
            Vector4 maskUVRect = GetMaskUVRect();

            // 캐시가 아직 초기화되지 않았을 수 있으므로 (GetModifiedMaterial이 LateUpdate 이전에 호출되는 경우)
            // 캐시 미초기화 시 직접 계산하여 안전한 값 보장
            Matrix4x4 worldToUV = _cachedWorldToUV;
            if (!_cachedWorldToUVValid)
                worldToUV = ComputeWorldToMaskUV();

            // 기본 마스크 프로퍼티 (소프트니스/인버트는 첫 프레임 캐시 미초기화 대비로 현재 값 사용)
            mat.SetMatrix(PropMaskWorldToUV, worldToUV);
            mat.SetFloat(PropSoftnessRcp, SoftnessRcp(_softness));
            mat.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
            if (maskTex != null) mat.SetTexture(PropMaskTex, maskTex);
            mat.SetVector(PropMaskUVRect, maskUVRect);

            // 형태 대응 프로퍼티 (_SOFTMASK_SLICE 하나가 마스크 1·2를 모두 담당)
            // 지연 캐시가 아닌 지오메트리 캐시 게터 사용 — UpdateSharedMaterial 조기 리턴 상태에서
            // 프록시가 새로 생성돼도 첫 프레임부터 올바른 형태가 적용되도록 (비용 동일: 프레임당 1회 검증)
            bool hasParent = _hasParentMask && _parentSoftMask != null;
            bool anySliced = IsSlicedMask() || (hasParent && _parentSoftMask.IsSlicedMask());
            if (anySliced)
            {
                if (!mat.IsKeywordEnabled(KEYWORD_SLICE))
                    mat.EnableKeyword(KEYWORD_SLICE);
                mat.SetVector(PropMaskSliceBorder, GetMaskSliceBorder());
                mat.SetVector(PropMaskSliceInnerUV, GetMaskSliceInnerUV());
                mat.SetVector(PropMaskSliceSlopeX, GetMaskSliceSlopeX());
                mat.SetVector(PropMaskSliceSlopeY, GetMaskSliceSlopeY());
                mat.SetVector(PropMaskFillLineA, GetMaskFillLineA());
                mat.SetVector(PropMaskFillLineB, GetMaskFillLineB());
                mat.SetVector(PropMaskSliceBorder2, hasParent ? _parentSoftMask.GetMaskSliceBorder() : IdentitySlice);
                mat.SetVector(PropMaskSliceInnerUV2, hasParent ? _parentSoftMask.GetMaskSliceInnerUV() : IdentitySlice);
                mat.SetVector(PropMaskSliceSlopeX2, hasParent ? _parentSoftMask.GetMaskSliceSlopeX() : IdentitySliceSlope);
                mat.SetVector(PropMaskSliceSlopeY2, hasParent ? _parentSoftMask.GetMaskSliceSlopeY() : IdentitySliceSlope);
                mat.SetVector(PropMaskFillLineA2, hasParent ? _parentSoftMask.GetMaskFillLineA() : IdentityFillLine);
                mat.SetVector(PropMaskFillLineB2, hasParent ? _parentSoftMask.GetMaskFillLineB() : IdentityFillLineB);
            }
            else if (mat.IsKeywordEnabled(KEYWORD_SLICE))
            {
                mat.DisableKeyword(KEYWORD_SLICE);
            }

            // 중첩 마스크 프로퍼티
            if (_hasParentMask && _parentSoftMask != null)
            {
                if (!mat.IsKeywordEnabled(KEYWORD_NESTED))
                    mat.EnableKeyword(KEYWORD_NESTED);

                Matrix4x4 parentWorldToUV = _cachedParentWorldToUV;
                if (!_cachedParentWorldToUVValid)
                    parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                mat.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                mat.SetFloat(PropSoftnessRcp2, SoftnessRcp(_parentSoftMask._softness));
                mat.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                if (parentTex != null) mat.SetTexture(PropMaskTex2, parentTex);
                mat.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
            }
            else
            {
                if (mat.IsKeywordEnabled(KEYWORD_NESTED))
                    mat.DisableKeyword(KEYWORD_NESTED);
            }
        }

        /// <summary>
        /// UIEffect 프록시 머티리얼에 현재 마스크 프로퍼티 일괄 전파
        /// LateUpdate에서 anyChange 시 호출되어 프록시 머티리얼을 갱신
        /// (캔버스 리빌드 없이 마스크 프로퍼티만 변경된 경우 대응)
        /// </summary>
        private void UpdateUIEffectMaterials()
        {
            // 공유 캐시를 직접 순회 — 프록시 컴포넌트 수와 무관하게 Material당 1회만 적용
            if (_uiEffectProxyMaterials.Count == 0) return;

            foreach (var mat in _uiEffectProxyMaterials.Values)
            {
                if (mat == null) continue;
                ApplyMaskPropertiesToMaterial(mat);
            }
        }

        /// <summary>
        /// UIEffect 프록시 머티리얼이 null인지 감지하여 캔버스 재빌드 트리거
        /// UIEffect가 내부적으로 머티리얼을 재생성했을 때 프록시도 재생성하도록 처리
        /// </summary>
        private void DetectUIEffectMaterialChanges()
        {
            if (_uiEffectProxies.Count == 0) return;

            for (int i = _uiEffectProxies.Count - 1; i >= 0; i--)
            {
                var proxy = _uiEffectProxies[i];
                if (proxy == null)
                {
                    _uiEffectProxies.RemoveAt(i);
                    continue;
                }

                // 프록시 머티리얼이 null이면 UIEffect가 머티리얼을 재생성한 것
                // → SetMaterialDirty()로 GetModifiedMaterial() 재호출 트리거
                //
                // 비활성 오브젝트는 캔버스 리빌드가 없어 ProxyMaterial이 영원히 null이므로,
                // 그대로 두면 매 프레임 헛도는 호출이 된다. 활성 상태에서만 처리한다.
                if (proxy.ProxyMaterial == null && proxy.isActiveAndEnabled)
                {
                    var graphic = proxy.OwnerGraphic;
                    if (graphic != null) graphic.SetMaterialDirty();
                }
            }
        }

        /// <summary>
        /// 이미 일반 Graphic으로 처리된 자식에 UIEffect가 새로 추가된 경우 감지
        /// → 기존 머티리얼 복원 후 UIEffect 프록시 경로로 재적용
        /// </summary>
        private void DetectNewUIEffectOnExistingChildren()
        {
            _toRemove.Clear();

            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) continue;
                // 이미 UIEffect 프록시 경로인 경우 스킵 (value가 null)
                if (kvp.Value == null) continue;

                if (IsUIEffectGraphic(kvp.Key))
                {
                    var proxy = kvp.Key.GetComponent<UIEffectSoftMaskLightProxy>();
                    if (proxy == null || proxy.IsCleanedUp)
                        _toRemove.Add(kvp.Key);
                }
            }

            // 변경된 자식만 증분 업데이트 (전체 재생성 대신)
            for (int i = 0; i < _toRemove.Count; i++)
            {
                var child = _toRemove[i];
                // 기존 Material 복원
                RestoreSingleChild(child, _originalChildMaterials[child]);
                _originalChildMaterials.Remove(child);
                // UIEffect 프록시 경로로 재적용
                ApplyMaskToUIEffect(child);
            }
        }

        /// <summary>
        /// 해당 머티리얼이 UIEffect 프록시 머티리얼인지 확인 (PropagateToStencilMaterials 스킵용)
        /// </summary>
        private bool IsUIEffectProxyMaterial(Material mat)
        {
            return _uiEffectProxyMaterialSet.Contains(mat);
        }

        // ─────────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────────

        /// <summary>
        /// TMP 원본 Material을 직렬화 백업에 저장
        /// 값이 변경된 경우에만 업데이트 (불필요한 씬 dirty 방지)
        /// </summary>
        private void SaveTMPOriginalBackup(UnityEngine.UI.Graphic g, Material mat)
        {
            for (int i = 0; i < _tmpOriginalBackup.Count; i++)
            {
                if (_tmpOriginalBackup[i].graphic == g)
                {
                    if (_tmpOriginalBackup[i].material == mat) return;
                    _tmpOriginalBackup[i] = new TMPOriginalEntry { graphic = g, material = mat };
                    return;
                }
            }
            _tmpOriginalBackup.Add(new TMPOriginalEntry { graphic = g, material = mat });
        }

        /// <summary>
        /// 직렬화 백업에서 TMP 원본 Material 검색
        /// </summary>
        private Material FindTMPOriginalBackup(UnityEngine.UI.Graphic g)
        {
            for (int i = 0; i < _tmpOriginalBackup.Count; i++)
            {
                if (_tmpOriginalBackup[i].graphic == g)
                    return _tmpOriginalBackup[i].material;
            }
            return null;
        }

        /// <summary>
        /// TMP SoftMask 셰이더 가져오기
        /// 직렬화 참조 → 정적 캐시 → Shader.Find() 순으로 폴백
        /// </summary>
        private Shader GetCachedTMPShader()
        {
            if (_tmpMaskShader != null)
            {
                s_cachedTMPShader = _tmpMaskShader;
                return _tmpMaskShader;
            }

            if (s_cachedTMPShader != null) return s_cachedTMPShader;

            s_cachedTMPShader = Shader.Find(TMP_SHADER_NAME);
            if (s_cachedTMPShader == null)
            {
                Debug.LogError($"[SoftMaskLight] TMP 셰이더를 찾을 수 없습니다: {TMP_SHADER_NAME}");
            }
            return s_cachedTMPShader;
        }

        /// <summary>
        /// 원본 Material의 셰이더 프로퍼티 값만 개별 복사
        /// CopyPropertiesFromMaterial()과 달리 대상 Material의 프로퍼티 시트를
        /// 통째로 교체하지 않으므로, 대상 셰이더 고유 프로퍼티가 보존됨
        /// </summary>
        private static void CopyShaderProperties(Material dest, Material src)
        {
            Shader srcShader = src.shader;
            if (srcShader == null) return;

            int count = srcShader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                int nameId = srcShader.GetPropertyNameId(i);
                switch (srcShader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        dest.SetColor(nameId, src.GetColor(nameId));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        dest.SetFloat(nameId, src.GetFloat(nameId));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        dest.SetTexture(nameId, src.GetTexture(nameId));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        dest.SetVector(nameId, src.GetVector(nameId));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Int:
                        dest.SetInteger(nameId, src.GetInteger(nameId));
                        break;
                }
            }
        }

        /// <summary>
        /// 자식 오브젝트의 마스크를 강제 재적용
        /// 런타임에서 자식에 UIEffect 등 컴포넌트를 동적으로 추가/제거한 후 호출
        /// </summary>
        public void RefreshMasks()
        {
            RestoreChildrenMaterials();
            ApplyMaskToChildren();
            _materialDirty = true;
            _stencilRefreshCountdown = 2;
        }

        /// <summary>
        /// 현재 마스킹된 자식 수 (에디터 정보 표시용)
        /// </summary>
        public int MaskedChildCount => _originalChildMaterials.Count;

        /// <summary>
        /// 부모 SoftMask 참조 (에디터 정보 표시용)
        /// </summary>
        public SoftMaskLight ParentSoftMask => _parentSoftMask;

    }
}

