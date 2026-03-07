using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using TMPro;
using Coffee.UIEffects;

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
        public const string VERSION = "2.0.0";

        // 셰이더/키워드 상수
        public const string SHADER_NAME = "SoftMaskLight/UI/Default";
        public const string TMP_SHADER_NAME = "SoftMaskLight/UI/TMP_SoftMask";
        private const string KEYWORD_NESTED = "_SOFTMASK_NESTED";
        private const string KEYWORD_SLICE = "_SOFTMASK_SLICE";
        private const string KEYWORD_NESTED_SLICE = "_SOFTMASK_NESTED_SLICE";
        private const string PARTICLE_SHADER_PREFIX = "SoftMaskLight/Particles/";

        // Optional Shader 패턴 상수
        private const string OPTIONAL_SUFFIX = "(SoftMaskLight)";
        private const string OPTIONAL_FORMAT = "Hidden/{0} (SoftMaskLight)";
        private const string DEFAULT_OPTIONAL = "Hidden/UI/Default (SoftMaskLight)";

        // 셰이더 직렬화 참조 (빌드에서 Shader.Find() 실패 방지)
        [SerializeField, HideInInspector] private Shader _maskShader;
        [SerializeField, HideInInspector] private Shader _tmpMaskShader;
        // Optional Shader 빌드 포함은 SoftMaskLightSettings (Resources) 에셋이 담당

        // 셰이더 캐싱
        private static Shader s_cachedShader;
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

            // 이미 Hidden/ 접두사가 있는 셰이더는 접두사 중복 방지
            // 예: "Hidden/UI/Default (UIEffect)" → "Hidden/UI/Default (UIEffect) (SoftMaskLight)"
            string shaderName = originalShader.name;
            string name;
            if (shaderName.StartsWith("Hidden/"))
                name = shaderName + " " + OPTIONAL_SUFFIX;
            else
                name = string.Format(OPTIONAL_FORMAT, shaderName);

            Shader variant = Shader.Find(name);
            if (variant == null)
                variant = Shader.Find(DEFAULT_OPTIONAL);

            s_optionalShaderCache[id] = variant;
            return variant;
        }

        // Shader Property ID 캐싱 (SoftMaskLight 셰이더용)
        private static readonly int PropMaskTex = Shader.PropertyToID("_MaskTex");
        private static readonly int PropSoftness = Shader.PropertyToID("_Softness");
        private static readonly int PropInvertMask = Shader.PropertyToID("_InvertMask");
        private static readonly int PropMaskWorldToUV = Shader.PropertyToID("_MaskWorldToUV");
        private static readonly int PropMaskUVRect = Shader.PropertyToID("_MaskUVRect");
        private static readonly int PropMaskSliceBorder = Shader.PropertyToID("_MaskSliceBorder");
        private static readonly int PropMaskSliceInnerUV = Shader.PropertyToID("_MaskSliceInnerUV");
        private static readonly int PropMaskTex2 = Shader.PropertyToID("_MaskTex2");
        private static readonly int PropSoftness2 = Shader.PropertyToID("_Softness2");
        private static readonly int PropInvertMask2 = Shader.PropertyToID("_InvertMask2");
        private static readonly int PropMaskWorldToUV2 = Shader.PropertyToID("_MaskWorldToUV2");
        private static readonly int PropMaskUVRect2 = Shader.PropertyToID("_MaskUVRect2");
        private static readonly int PropMaskSliceBorder2 = Shader.PropertyToID("_MaskSliceBorder2");
        private static readonly int PropMaskSliceInnerUV2 = Shader.PropertyToID("_MaskSliceInnerUV2");

        // TMP 셰이더용 프로퍼티 ID (_SoftMask* 접두사: TMP의 _MaskTex 충돌 방지)
        private static readonly int PropTMPMaskTex = Shader.PropertyToID("_SoftMaskTex");
        private static readonly int PropTMPSoftness = Shader.PropertyToID("_SoftMaskSoftness");
        private static readonly int PropTMPInvertMask = Shader.PropertyToID("_SoftMaskInvert");
        private static readonly int PropTMPMaskWorldToUV = Shader.PropertyToID("_SoftMaskWorldToUV");
        private static readonly int PropTMPMaskUVRect = Shader.PropertyToID("_SoftMaskUVRect");
        private static readonly int PropTMPMaskSliceBorder = Shader.PropertyToID("_SoftMaskSliceBorder");
        private static readonly int PropTMPMaskSliceInnerUV = Shader.PropertyToID("_SoftMaskSliceInnerUV");
        private static readonly int PropTMPMaskTex2 = Shader.PropertyToID("_SoftMaskTex2");
        private static readonly int PropTMPSoftness2 = Shader.PropertyToID("_SoftMaskSoftness2");
        private static readonly int PropTMPInvertMask2 = Shader.PropertyToID("_SoftMaskInvert2");
        private static readonly int PropTMPMaskWorldToUV2 = Shader.PropertyToID("_SoftMaskWorldToUV2");
        private static readonly int PropTMPMaskUVRect2 = Shader.PropertyToID("_SoftMaskUVRect2");
        private static readonly int PropTMPMaskSliceBorder2 = Shader.PropertyToID("_SoftMaskSliceBorder2");
        private static readonly int PropTMPMaskSliceInnerUV2 = Shader.PropertyToID("_SoftMaskSliceInnerUV2");

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

        // Optional Shader별 공유 Material (같은 Optional Shader를 사용하는 자식끼리 공유)
        private readonly Dictionary<Shader, Material> _sharedOptionalMaterials =
            new Dictionary<Shader, Material>();

        // 자식 원본 Material 복원용
        private readonly Dictionary<UnityEngine.UI.Graphic, Material> _originalChildMaterials =
            new Dictionary<UnityEngine.UI.Graphic, Material>();

        // 마스크 그래픽 원본 색상
        private Color _originalMaskColor;
        private bool _originalColorSaved;

        // 중첩 마스크: 부모 SoftMask
        private SoftMaskLight _parentSoftMask;
        private bool _hasParentMask;

        // 더티 체크용 캐싱
        private Matrix4x4 _cachedWorldToUV;
        private Matrix4x4 _cachedParentWorldToUV;
        private float _cachedSoftness;
        private bool _cachedInvertMask;
        private float _cachedParentSoftness;
        private bool _cachedParentInvertMask;
        private int _cachedMaskTexId;
        private int _cachedParentMaskTexId;
        private bool _materialDirty;

        // 슬라이스 마스크 더티 체크용 캐싱
        private bool _cachedIsSliced;
        private Vector4 _cachedSliceBorder;
        private Vector4 _cachedSliceInnerUV;
        private bool _cachedParentIsSliced;
        private Vector4 _cachedParentSliceBorder;
        private Vector4 _cachedParentSliceInnerUV;

        // TMP 전용 Material 리스트 (폰트 아틀라스별 개별 Material 필요)
        private readonly List<Material> _tmpMaskMaterials = new List<Material>(2);

        // TMP Graphic → 적용 중인 마스크 Material 매핑
        // 외부에서 TMP Material 변경 시 감지 및 자동 재적용에 사용
        private readonly Dictionary<UnityEngine.UI.Graphic, Material> _tmpAppliedMaskMats =
            new Dictionary<UnityEngine.UI.Graphic, Material>(2);

        // Particle (UIParticle) 전용 Material 리스트
        private readonly List<Material> _particleMaskMaterials = new List<Material>(2);

        // Particle Graphic → 적용 중인 마스크 Material 매핑
        private readonly Dictionary<UnityEngine.UI.Graphic, Material> _particleAppliedMaskMats =
            new Dictionary<UnityEngine.UI.Graphic, Material>(2);

        // 커스텀 셰이더(ColorReplace 등) 전용 Material 리스트 (파괴 관리용)
        private readonly List<Material> _customMaskMaterials = new List<Material>(2);

        // 커스텀 셰이더 Graphic → 적용 중인 마스크 Material 매핑
        private readonly Dictionary<UnityEngine.UI.Graphic, Material> _customAppliedMaskMats =
            new Dictionary<UnityEngine.UI.Graphic, Material>(2);

        // 커스텀 셰이더 공유 캐시: 동일한 원본 Material을 사용하는 자식끼리 clone 공유
        // (예: 같은 ColorReplace .mat 에셋을 참조하는 여러 자식이 하나의 마스크 clone을 공유)
        private readonly Dictionary<Material, Material> _sharedCustomClones =
            new Dictionary<Material, Material>(2);

        // UIEffect 프록시 목록 (UIParticle의 _particleMaskMaterials와 유사한 구조)
        private readonly List<UIEffectSoftMaskLightProxy> _uiEffectProxies = new List<UIEffectSoftMaskLightProxy>(2);

        // GC 방지: 재사용 리스트
        private readonly List<UnityEngine.UI.Graphic> _toRemove = new List<UnityEngine.UI.Graphic>(4);
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

        // Canvas 레이아웃 완료 후 갱신 플래그
        // OnEnable/OnTransformParentChanged 시점에는 레이아웃이 미완료 상태일 수 있음
        // Canvas.willRenderCanvases 이벤트에서 레이아웃 완료 후 마스크 갱신
        private bool _pendingLayoutRefresh;

#if UNITY_EDITOR
        // 부모 UI Mask의 showMaskGraphic 변경 감지 (에디터 전용)
        private UnityEngine.UI.Mask _parentUIMask;
        private bool _cachedParentMaskShowGraphic;
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

            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;

            // TMP / Particle / UIEffect 외부 Material 변경 감지
            DetectTMPMaterialChanges();
            DetectParticleMaterialChanges();
            DetectUIEffectMaterialChanges();

            // UIEffect 동적 추가 감지: 상태 변화가 감지된 시점에만 체크 (이벤트 기반)
            // 에디터 비플레이 모드에서는 CheckForChildChanges에서 처리
            if (_checkUIEffectPending)
            {
                _checkUIEffectPending = false;
                DetectNewUIEffectOnExistingChildren();
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
                CheckForChildChanges();
                CheckParentUIMaskChanges();
            }
#endif
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= OnCanvasPreRender;
            _pendingLayoutRefresh = false;

            RestoreChildrenMaterials();

            if (_originalColorSaved && _uiGraphic != null)
            {
                _uiGraphic.color = _originalMaskColor;
            }

            _parentSoftMask = null;
            _hasParentMask = false;
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= OnCanvasPreRender;

            RestoreChildrenMaterials();

            if (_originalColorSaved && _uiGraphic != null)
            {
                _uiGraphic.color = _originalMaskColor;
            }
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
            _materialDirty = true;

            // 부모 마스크도 갱신
            if (_hasParentMask && _parentSoftMask != null)
            {
                _cachedParentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
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

            // Optional Material들의 중첩 마스크 키워드 갱신
            foreach (var mat in _sharedOptionalMaterials.Values)
            {
                if (mat == null) continue;
                if (_hasParentMask)
                    mat.EnableKeyword(KEYWORD_NESTED);
                else
                {
                    mat.DisableKeyword(KEYWORD_NESTED);
                    mat.DisableKeyword(KEYWORD_NESTED_SLICE);
                }
            }
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

        /// <summary>
        /// 자식 오브젝트 변경 감지 (에디터 전용)
        /// </summary>
        public void CheckForChildChanges()
        {
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
            foreach (var child in children)
            {
                if (child.gameObject == gameObject) continue;
                if (!BelongsToThisMask(child.transform)) continue;

                // 기존 일반 Graphic → UIEffect 추가 감지: 프록시 없이 UIEffect가 있으면 증분 재적용
                if (_originalChildMaterials.TryGetValue(child, out var origMat) && origMat != null && IsUIEffectGraphic(child))
                {
                    var existingProxy = child.GetComponent<UIEffectSoftMaskLightProxy>();
                    if (existingProxy == null || existingProxy.IsCleanedUp)
                    {
                        RestoreSingleChild(child, origMat);
                        _originalChildMaterials.Remove(child);
                        ApplyMaskToUIEffect(child);
                        continue;
                    }
                }

                if (!_originalChildMaterials.ContainsKey(child))
                {
                    ApplyMaskToChildren();
                    return;
                }
            }
        }

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

            Rect contentRect = GetContentLocalRect();
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
        /// 스프라이트 콘텐츠의 실제 로컬 영역 계산
        /// Atlas 패킹 시 투명 여백이 트리밍된 경우, 콘텐츠 영역만 반환
        /// 비트리밍 스프라이트 또는 비Image는 전체 RectTransform rect 반환
        /// </summary>
        private Rect GetContentLocalRect()
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
        /// 마스크 텍스처 가져오기 (자신의 텍스처)
        /// </summary>
        internal Texture GetMaskTexture()
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
            if (_uiGraphic is UnityEngine.UI.Image image && image.sprite != null)
            {
                Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite);
                return new Vector4(outerUV.x, outerUV.y, outerUV.z - outerUV.x, outerUV.w - outerUV.y);
            }
            return new Vector4(0, 0, 1, 1);
        }

        /// <summary>
        /// 마스크 이미지가 Sliced 타입인지 확인
        /// </summary>
        internal bool IsSlicedMask()
        {
            return _uiGraphic is UnityEngine.UI.Image image &&
                   image.type == UnityEngine.UI.Image.Type.Sliced &&
                   image.sprite != null &&
                   image.sprite.border != Vector4.zero;
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
            if (!IsSlicedMask()) return new Vector4(0f, 0f, 1f, 1f);

            var image = (UnityEngine.UI.Image)_uiGraphic;
            var sprite = image.sprite;

            Rect rect = _rectTransform.rect;
            float rectW = rect.width;
            float rectH = rect.height;
            if (rectW < 0.001f || rectH < 0.001f) return new Vector4(0f, 0f, 1f, 1f);

            // 스프라이트 테두리 픽셀 → 캔버스 단위 변환 (Image.pixelsPerUnit 사용)
            float ppu = image.pixelsPerUnit;
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
            if (!IsSlicedMask()) return new Vector4(0f, 0f, 1f, 1f);

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

            if (!_originalColorSaved)
            {
                _originalMaskColor = _uiGraphic.color;
                _originalColorSaved = true;
            }

            if (_showMaskGraphic)
            {
                _uiGraphic.color = _originalMaskColor;
            }
            else
            {
                Color c = _uiGraphic.color;
                c.a = 0f;
                _uiGraphic.color = c;
            }
        }

        // ─────────────────────────────────────────────
        // Material 관리 (Optional Shader 기반)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Optional Shader에 대응하는 공유 Material 생성 또는 가져오기
        /// 같은 Optional Shader를 사용하는 자식끼리 Material을 공유하여 배칭 최적화
        /// </summary>
        private Material GetOrCreateOptionalMaterial(Shader optionalShader)
        {
            if (optionalShader == null) return null;

            if (_sharedOptionalMaterials.TryGetValue(optionalShader, out var existing) && existing != null)
                return existing;

            var mat = new Material(optionalShader)
            {
                name = $"{optionalShader.name} (SoftMaskLight: {gameObject.name})",
                hideFlags = HideFlags.DontSave
            };

            // 자신의 마스크 설정
            Texture maskTex = GetMaskTexture();
            _cachedMaskTexId = maskTex != null ? maskTex.GetInstanceID() : 0;
            if (maskTex != null) mat.SetTexture(PropMaskTex, maskTex);

            Matrix4x4 worldToUV = ComputeWorldToMaskUV();
            mat.SetMatrix(PropMaskWorldToUV, worldToUV);
            mat.SetFloat(PropSoftness, _softness);
            mat.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
            mat.SetVector(PropMaskUVRect, GetMaskUVRect());
            _cachedWorldToUV = worldToUV;
            _cachedSoftness = _softness;
            _cachedInvertMask = _invertMask;

            // 슬라이스 마스크 초기 설정
            bool isSliced = IsSlicedMask();
            _cachedIsSliced = isSliced;
            if (isSliced)
            {
                mat.EnableKeyword(KEYWORD_SLICE);
                _cachedSliceBorder = GetMaskSliceBorder();
                _cachedSliceInnerUV = GetMaskSliceInnerUV();
                mat.SetVector(PropMaskSliceBorder, _cachedSliceBorder);
                mat.SetVector(PropMaskSliceInnerUV, _cachedSliceInnerUV);
            }
            else
            {
                mat.DisableKeyword(KEYWORD_SLICE);
                _cachedSliceBorder = new Vector4(0f, 0f, 1f, 1f);
                _cachedSliceInnerUV = new Vector4(0f, 0f, 1f, 1f);
            }

            // 중첩 마스크 설정
            if (_hasParentMask && _parentSoftMask != null)
            {
                mat.EnableKeyword(KEYWORD_NESTED);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                _cachedParentMaskTexId = parentTex != null ? parentTex.GetInstanceID() : 0;
                if (parentTex != null) mat.SetTexture(PropMaskTex2, parentTex);

                Matrix4x4 parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                mat.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                mat.SetFloat(PropSoftness2, _parentSoftMask._softness);
                mat.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                mat.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
                _cachedParentWorldToUV = parentWorldToUV;
                _cachedParentSoftness = _parentSoftMask._softness;
                _cachedParentInvertMask = _parentSoftMask._invertMask;

                // 중첩 슬라이스 초기 설정
                bool parentIsSliced = _parentSoftMask.IsSlicedMask();
                _cachedParentIsSliced = parentIsSliced;
                if (parentIsSliced)
                {
                    mat.EnableKeyword(KEYWORD_NESTED_SLICE);
                    _cachedParentSliceBorder = _parentSoftMask.GetMaskSliceBorder();
                    _cachedParentSliceInnerUV = _parentSoftMask.GetMaskSliceInnerUV();
                    mat.SetVector(PropMaskSliceBorder2, _cachedParentSliceBorder);
                    mat.SetVector(PropMaskSliceInnerUV2, _cachedParentSliceInnerUV);
                }
                else
                {
                    _cachedParentSliceBorder = new Vector4(0f, 0f, 1f, 1f);
                    _cachedParentSliceInnerUV = new Vector4(0f, 0f, 1f, 1f);
                }
            }
            else
            {
                mat.DisableKeyword(KEYWORD_NESTED);
                mat.DisableKeyword(KEYWORD_NESTED_SLICE);
                _cachedParentIsSliced = false;
                _cachedParentSliceBorder = new Vector4(0f, 0f, 1f, 1f);
                _cachedParentSliceInnerUV = new Vector4(0f, 0f, 1f, 1f);
            }

            _sharedOptionalMaterials[optionalShader] = mat;
            _materialDirty = false;
            return mat;
        }

        /// <summary>
        /// 공유 Material 프로퍼티 업데이트 (더티 체크 포함)
        /// Transform 변경 시에만 행렬 업데이트, 프로퍼티 변경 시에만 값 업데이트
        /// UI Mask 내에서 사용 시 Stencil 래핑 Material에도 프로퍼티 전파
        /// </summary>
        private void UpdateSharedMaterial()
        {
            if (_originalChildMaterials.Count == 0) return;
            if (_sharedOptionalMaterials.Count == 0 && _tmpMaskMaterials.Count == 0 && _particleMaskMaterials.Count == 0 && _customMaskMaterials.Count == 0 && _uiEffectProxies.Count == 0) return;

            bool anyChange = false;

            // 자신의 변환 행렬 더티 체크
            Matrix4x4 currentWorldToUV = ComputeWorldToMaskUV();
            if (_materialDirty || currentWorldToUV != _cachedWorldToUV)
            {
                foreach (var m in _sharedOptionalMaterials.Values)
                    if (m != null) m.SetMatrix(PropMaskWorldToUV, currentWorldToUV);
                _cachedWorldToUV = currentWorldToUV;
                anyChange = true;
            }

            // 마스크 텍스처 변경 체크
            Texture maskTex = GetMaskTexture();
            int texId = maskTex != null ? maskTex.GetInstanceID() : 0;
            if (texId != _cachedMaskTexId)
            {
                _cachedMaskTexId = texId;
                foreach (var m in _sharedOptionalMaterials.Values)
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
                foreach (var m in _sharedOptionalMaterials.Values)
                {
                    if (m == null) continue;
                    m.SetFloat(PropSoftness, _softness);
                    m.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
                }
                _cachedSoftness = _softness;
                _cachedInvertMask = _invertMask;
                anyChange = true;
            }

            // 슬라이스 타입 변경 체크
            bool isSliced = IsSlicedMask();
            Vector4 sliceBorder = isSliced ? GetMaskSliceBorder() : new Vector4(0f, 0f, 1f, 1f);
            Vector4 sliceInnerUV = isSliced ? GetMaskSliceInnerUV() : new Vector4(0f, 0f, 1f, 1f);
            if (_materialDirty || isSliced != _cachedIsSliced || sliceBorder != _cachedSliceBorder || sliceInnerUV != _cachedSliceInnerUV)
            {
                _cachedIsSliced = isSliced;
                _cachedSliceBorder = sliceBorder;
                _cachedSliceInnerUV = sliceInnerUV;
                foreach (var m in _sharedOptionalMaterials.Values)
                {
                    if (m == null) continue;
                    if (isSliced)
                    {
                        m.EnableKeyword(KEYWORD_SLICE);
                        m.SetVector(PropMaskSliceBorder, sliceBorder);
                        m.SetVector(PropMaskSliceInnerUV, sliceInnerUV);
                    }
                    else
                    {
                        m.DisableKeyword(KEYWORD_SLICE);
                    }
                }
                anyChange = true;
            }

            // 부모 마스크 업데이트 (중첩 마스크)
            if (_hasParentMask && _parentSoftMask != null && _parentSoftMask.enabled)
            {
                Matrix4x4 parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                if (_materialDirty || parentWorldToUV != _cachedParentWorldToUV)
                {
                    foreach (var m in _sharedOptionalMaterials.Values)
                        if (m != null) m.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                    _cachedParentWorldToUV = parentWorldToUV;
                    anyChange = true;
                }

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                int parentTexId = parentTex != null ? parentTex.GetInstanceID() : 0;
                if (parentTexId != _cachedParentMaskTexId)
                {
                    _cachedParentMaskTexId = parentTexId;
                    foreach (var m in _sharedOptionalMaterials.Values)
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
                    foreach (var m in _sharedOptionalMaterials.Values)
                    {
                        if (m == null) continue;
                        m.SetFloat(PropSoftness2, _parentSoftMask._softness);
                        m.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                    }
                    _cachedParentSoftness = _parentSoftMask._softness;
                    _cachedParentInvertMask = _parentSoftMask._invertMask;
                    anyChange = true;
                }

                // 부모 마스크 슬라이스 타입 변경 체크
                bool parentIsSliced = _parentSoftMask.IsSlicedMask();
                Vector4 parentSliceBorder = parentIsSliced ? _parentSoftMask.GetMaskSliceBorder() : new Vector4(0f, 0f, 1f, 1f);
                Vector4 parentSliceInnerUV = parentIsSliced ? _parentSoftMask.GetMaskSliceInnerUV() : new Vector4(0f, 0f, 1f, 1f);
                if (_materialDirty || parentIsSliced != _cachedParentIsSliced || parentSliceBorder != _cachedParentSliceBorder || parentSliceInnerUV != _cachedParentSliceInnerUV)
                {
                    _cachedParentIsSliced = parentIsSliced;
                    _cachedParentSliceBorder = parentSliceBorder;
                    _cachedParentSliceInnerUV = parentSliceInnerUV;
                    foreach (var m in _sharedOptionalMaterials.Values)
                    {
                        if (m == null) continue;
                        if (parentIsSliced)
                        {
                            m.EnableKeyword(KEYWORD_NESTED_SLICE);
                            m.SetVector(PropMaskSliceBorder2, parentSliceBorder);
                            m.SetVector(PropMaskSliceInnerUV2, parentSliceInnerUV);
                        }
                        else
                        {
                            m.DisableKeyword(KEYWORD_NESTED_SLICE);
                        }
                    }
                    anyChange = true;
                }
            }

            // TMP, Particle, Custom, UIEffect, Stencil Material에 마스크 프로퍼티 전파
            if (anyChange || _materialDirty)
            {
                UpdateTMPMaterials();
                UpdateParticleMaterials();
                UpdateCustomMaterials();
                UpdateUIEffectMaterials();
                PropagateToStencilMaterials();
            }

            _materialDirty = false;

            // 파괴된 자식 정리
            CleanupDestroyedChildren();
        }

        /// <summary>
        /// Stencil 래핑된 렌더링 Material에 마스크 프로퍼티 전파
        /// Unity UI Mask 내에서 사용 시, StencilMaterial.Add()가 생성한 복사본은
        /// 원본 Material 변경을 반영하지 않으므로 직접 프로퍼티를 설정
        /// </summary>
        private void PropagateToStencilMaterials()
        {
            Texture maskTex = GetMaskTexture();
            Vector4 maskUVRect = GetMaskUVRect();

            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) continue;

                Material rendered = kvp.Key.materialForRendering;
                if (rendered == null) continue;

                // 기본 Material 자체는 이미 업데이트됨 → 스킵
                if (IsSharedOptionalMaterial(rendered)) continue;
                if (_tmpMaskMaterials.Contains(rendered)) continue;
                if (_particleMaskMaterials.Contains(rendered)) continue;
                if (_customMaskMaterials.Contains(rendered)) continue;
                if (IsUIEffectProxyMaterial(rendered)) continue;

                // Shader 인스턴스 비교로 TMP 또는 표준 프로퍼티 ID 결정 (문자열 비교 회피)
                Shader renderedShader = rendered.shader;
                if (renderedShader == null) continue;

                int pTex, pSoftness, pInvert, pWorldToUV, pUVRect;
                int pTex2, pSoftness2, pInvert2, pWorldToUV2, pUVRect2;
                int pSliceBorder, pSliceInnerUV, pSliceBorder2, pSliceInnerUV2;

                Shader tmpShader = GetCachedTMPShader();
                if (tmpShader != null && renderedShader == tmpShader)
                {
                    pTex = PropTMPMaskTex; pSoftness = PropTMPSoftness; pInvert = PropTMPInvertMask;
                    pWorldToUV = PropTMPMaskWorldToUV; pUVRect = PropTMPMaskUVRect;
                    pTex2 = PropTMPMaskTex2; pSoftness2 = PropTMPSoftness2; pInvert2 = PropTMPInvertMask2;
                    pWorldToUV2 = PropTMPMaskWorldToUV2; pUVRect2 = PropTMPMaskUVRect2;
                    pSliceBorder = PropTMPMaskSliceBorder; pSliceInnerUV = PropTMPMaskSliceInnerUV;
                    pSliceBorder2 = PropTMPMaskSliceBorder2; pSliceInnerUV2 = PropTMPMaskSliceInnerUV2;
                }
                else if (rendered.HasProperty(PropMaskTex))
                {
                    // _MaskTex 프로퍼티가 있는 셰이더 = SoftMaskLight 대응 셰이더 (표준 프로퍼티 이름)
                    pTex = PropMaskTex; pSoftness = PropSoftness; pInvert = PropInvertMask;
                    pWorldToUV = PropMaskWorldToUV; pUVRect = PropMaskUVRect;
                    pTex2 = PropMaskTex2; pSoftness2 = PropSoftness2; pInvert2 = PropInvertMask2;
                    pWorldToUV2 = PropMaskWorldToUV2; pUVRect2 = PropMaskUVRect2;
                    pSliceBorder = PropMaskSliceBorder; pSliceInnerUV = PropMaskSliceInnerUV;
                    pSliceBorder2 = PropMaskSliceBorder2; pSliceInnerUV2 = PropMaskSliceInnerUV2;
                }
                else
                {
                    continue;
                }

                // Stencil 래핑된 Material에 마스크 프로퍼티 복사
                rendered.SetMatrix(pWorldToUV, _cachedWorldToUV);
                rendered.SetFloat(pSoftness, _cachedSoftness);
                rendered.SetFloat(pInvert, _cachedInvertMask ? 1f : 0f);

                if (maskTex != null) rendered.SetTexture(pTex, maskTex);
                rendered.SetVector(pUVRect, maskUVRect);

                // 슬라이스 프로퍼티 전파
                if (_cachedIsSliced)
                {
                    if (!rendered.IsKeywordEnabled(KEYWORD_SLICE))
                        rendered.EnableKeyword(KEYWORD_SLICE);
                    rendered.SetVector(pSliceBorder, _cachedSliceBorder);
                    rendered.SetVector(pSliceInnerUV, _cachedSliceInnerUV);
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
                    rendered.SetFloat(pSoftness2, _cachedParentSoftness);
                    rendered.SetFloat(pInvert2, _cachedParentInvertMask ? 1f : 0f);

                    Texture parentTex = _parentSoftMask != null ? _parentSoftMask.GetMaskTexture() : null;
                    if (parentTex != null) rendered.SetTexture(pTex2, parentTex);
                    if (_parentSoftMask != null)
                        rendered.SetVector(pUVRect2, _parentSoftMask.GetMaskUVRect());

                    // 중첩 슬라이스 프로퍼티 전파
                    if (_cachedParentIsSliced)
                    {
                        if (!rendered.IsKeywordEnabled(KEYWORD_NESTED_SLICE))
                            rendered.EnableKeyword(KEYWORD_NESTED_SLICE);
                        rendered.SetVector(pSliceBorder2, _cachedParentSliceBorder);
                        rendered.SetVector(pSliceInnerUV2, _cachedParentSliceInnerUV);
                    }
                    else if (rendered.IsKeywordEnabled(KEYWORD_NESTED_SLICE))
                    {
                        rendered.DisableKeyword(KEYWORD_NESTED_SLICE);
                    }
                }
            }
        }

        /// <summary>
        /// 해당 Material이 공유 Optional Material인지 확인
        /// </summary>
        private bool IsSharedOptionalMaterial(Material mat)
        {
            foreach (var m in _sharedOptionalMaterials.Values)
                if (m == mat) return true;
            return false;
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
                _tmpAppliedMaskMats.Remove(_toRemove[i]);
                _particleAppliedMaskMats.Remove(_toRemove[i]);
                _customAppliedMaskMats.Remove(_toRemove[i]);
            }

            // 파괴된 UIEffect 프록시 정리
            for (int i = _uiEffectProxies.Count - 1; i >= 0; i--)
            {
                if (_uiEffectProxies[i] == null)
                    _uiEffectProxies.RemoveAt(i);
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
            // 기존 커스텀 마스크 머티리얼 정리
            if (_customAppliedMaskMats.TryGetValue(child, out Material customMat))
            {
                _customAppliedMaskMats.Remove(child);
                // 공유 clone인 경우 다른 자식이 아직 사용 중이면 파괴하지 않음
                if (customMat != null && !IsCustomCloneInUse(customMat))
                {
                    _customMaskMaterials.Remove(customMat);
                    RemoveFromSharedCustomClones(customMat);
                    if (Application.isPlaying) Destroy(customMat);
                    else DestroyImmediate(customMat);
                }
            }
            _originalChildMaterials.Remove(child);
            ApplyMaskToChildren();
        }

        /// <summary>
        /// 자식 오브젝트에 공유 마스크 Material 적용
        /// </summary>
        public void ApplyMaskToChildren()
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

                    if (originalFontMat == null) continue;

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

                // 일반 Graphic (TMP_SubMeshUI 포함)
                Material originalMat = child.material;

                // 복제된 오브젝트 감지: graphic.material이 이미 마스크 관리 Material인 경우
                // (Ctrl+D 등으로 복제하면 마스크 Material 참조가 그대로 복사됨)
                if (IsSharedOptionalMaterial(originalMat))
                {
                    // 공유 Optional Material (UI/Default 등) → 그대로 재사용
                    _originalChildMaterials[child] = child.defaultMaterial;
                    child.SetAllDirty();
                    continue;
                }
                Material realOriginal = FindOriginalForCustomClone(originalMat);
                if (realOriginal != null)
                {
                    // 커스텀 clone → 원본 Material을 역추적
                    originalMat = realOriginal;
                }

                // TMP_SubMeshUI도 동일한 DontSave Material 유실 문제 대응
                Material subBackup = FindTMPOriginalBackup(child);
                if (subBackup != null && originalMat != subBackup)
                {
                    originalMat = subBackup;
                    child.material = subBackup;
                }

                _originalChildMaterials[child] = originalMat;

                // TMP_SubMeshUI는 material 세터가 m_sharedMaterial도 설정함
                if (IsTMPMaterial(originalMat))
                {
                    SaveTMPOriginalBackup(child, originalMat);
                    Material tmpMat = CreateTMPMaskMaterial(originalMat);
                    if (tmpMat != null)
                    {
                        child.material = tmpMat;
                        _tmpAppliedMaskMats[child] = tmpMat;
                        child.SetAllDirty();
                        continue;
                    }
                }

                // Particle (UIParticle) — Optional Shader로 교체
                if (IsParticleMaterial(originalMat))
                {
                    Material particleMat = CreateParticleMaskMaterial(originalMat);
                    if (particleMat != null)
                    {
                        child.material = particleMat;
                        _particleAppliedMaskMats[child] = particleMat;
                        child.SetAllDirty();
                        continue;
                    }
                }

                // 일반 Graphic — Optional Shader 기반 Material 할당
                Shader optShader = FindOptionalShader(originalMat.shader);
                if (optShader == null) continue;

                // UI/Default 등 기본 셰이더는 공유 Material (배칭 유지)
                // 커스텀 셰이더(ColorReplace 등)는 원본 Material이 같으면 clone 공유 (배칭 유지)
                bool isDefaultUI = originalMat.shader.name == "UI/Default";
                if (isDefaultUI)
                {
                    Material optMat = GetOrCreateOptionalMaterial(optShader);
                    if (optMat != null)
                    {
                        child.material = optMat;
                        child.SetAllDirty();
                    }
                }
                else
                {
                    Material cloneMat = GetOrCreateSharedCustomMaterial(originalMat, optShader);
                    if (cloneMat != null)
                    {
                        child.material = cloneMat;
                        _customAppliedMaskMats[child] = cloneMat;
                        child.SetAllDirty();
                    }
                }
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

            // UIEffect 자식 (originalMat == null) → 프록시 제거만
            if (originalMat == null)
            {
                var proxy = child.GetComponent<UIEffectSoftMaskLightProxy>();
                if (proxy != null && !proxy.IsCleanedUp)
                    proxy.Cleanup();
                else
                    child.SetMaterialDirty();
                return;
            }

            // TMP_Text는 fontSharedMaterial로 복원
            if (child is TMP_Text tmpText)
                tmpText.fontSharedMaterial = originalMat;
            else
                child.material = originalMat;

            // 해당 자식에 할당된 마스크 Material 정리
            if (_tmpAppliedMaskMats.TryGetValue(child, out var tmpMat))
            {
                if (tmpMat != null) { if (Application.isPlaying) Destroy(tmpMat); else DestroyImmediate(tmpMat); }
                _tmpMaskMaterials.Remove(tmpMat);
                _tmpAppliedMaskMats.Remove(child);
            }
            if (_particleAppliedMaskMats.TryGetValue(child, out var particleMat))
            {
                if (particleMat != null) { if (Application.isPlaying) Destroy(particleMat); else DestroyImmediate(particleMat); }
                _particleMaskMaterials.Remove(particleMat);
                _particleAppliedMaskMats.Remove(child);
            }
            if (_customAppliedMaskMats.TryGetValue(child, out var customMat))
            {
                _customAppliedMaskMats.Remove(child);
                // 공유 clone인 경우 다른 자식이 아직 사용 중이면 파괴하지 않음
                if (customMat != null && !IsCustomCloneInUse(customMat))
                {
                    _customMaskMaterials.Remove(customMat);
                    RemoveFromSharedCustomClones(customMat);
                    if (Application.isPlaying) Destroy(customMat); else DestroyImmediate(customMat);
                }
            }
        }

        /// <summary>
        /// 자식 오브젝트의 원본 Material 복원
        /// </summary>
        public void RestoreChildrenMaterials()
        {
            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) continue;

                // UIEffect 자식은 _originalChildMaterials 값이 null로 저장됨
                // → 원본 머티리얼 복원 불필요 (프록시 컴포넌트 제거만 처리)
                // → 다만 materialForRendering이 파괴될 프록시 머티리얼을 참조하므로
                //   canvas 재빌드를 트리거하여 IMaterialModifier 체인 갱신
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

            // 공유 Optional Material 파괴
            foreach (var mat in _sharedOptionalMaterials.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying)
                        Destroy(mat);
                    else
                        DestroyImmediate(mat);
                }
            }
            _sharedOptionalMaterials.Clear();

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

            // Particle Material 파괴
            for (int i = 0; i < _particleMaskMaterials.Count; i++)
            {
                if (_particleMaskMaterials[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(_particleMaskMaterials[i]);
                    else
                        DestroyImmediate(_particleMaskMaterials[i]);
                }
            }
            _particleMaskMaterials.Clear();
            _particleAppliedMaskMats.Clear();

            // 커스텀 셰이더 Material 파괴
            for (int i = 0; i < _customMaskMaterials.Count; i++)
            {
                if (_customMaskMaterials[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(_customMaskMaterials[i]);
                    else
                        DestroyImmediate(_customMaskMaterials[i]);
                }
            }
            _customMaskMaterials.Clear();
            _customAppliedMaskMats.Clear();
            _sharedCustomClones.Clear();

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

                // 기존 마스크 Material 정리
                if (_tmpAppliedMaskMats.TryGetValue(child, out Material oldMaskMat) && oldMaskMat != null)
                {
                    _tmpMaskMaterials.Remove(oldMaskMat);
                    if (Application.isPlaying) Destroy(oldMaskMat);
                    else DestroyImmediate(oldMaskMat);
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

            // 렌더 큐 보존
            tmpMat.renderQueue = originalTMPMat.renderQueue;

            // 셰이더 컴파일 실패 시 폴백
            if (!shader.isSupported)
            {
                Debug.LogWarning($"[SoftMaskLight] TMP 셰이더가 지원되지 않습니다: {TMP_SHADER_NAME}");
                _tmpMaskMaterials.Add(tmpMat);
                return tmpMat;
            }

            // SoftMask 프로퍼티 설정 (TMP 전용 프로퍼티 ID 사용)
            ApplyMaskPropertiesToTMPMaterial(tmpMat);

            _tmpMaskMaterials.Add(tmpMat);
            return tmpMat;
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
            if (worldToUV.m00 == 0f && worldToUV.m11 == 0f)
                worldToUV = ComputeWorldToMaskUV();

            mat.SetMatrix(PropTMPMaskWorldToUV, worldToUV);
            mat.SetFloat(PropTMPSoftness, _cachedSoftness);
            mat.SetFloat(PropTMPInvertMask, _cachedInvertMask ? 1f : 0f);
            if (maskTex != null) mat.SetTexture(PropTMPMaskTex, maskTex);
            mat.SetVector(PropTMPMaskUVRect, maskUVRect);

            // 슬라이스 프로퍼티
            if (_cachedIsSliced)
            {
                if (!mat.IsKeywordEnabled(KEYWORD_SLICE))
                    mat.EnableKeyword(KEYWORD_SLICE);
                mat.SetVector(PropTMPMaskSliceBorder, _cachedSliceBorder);
                mat.SetVector(PropTMPMaskSliceInnerUV, _cachedSliceInnerUV);
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
                if (parentWorldToUV.m00 == 0f && parentWorldToUV.m11 == 0f)
                    parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                mat.SetMatrix(PropTMPMaskWorldToUV2, parentWorldToUV);
                mat.SetFloat(PropTMPSoftness2, _cachedParentSoftness);
                mat.SetFloat(PropTMPInvertMask2, _cachedParentInvertMask ? 1f : 0f);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                if (parentTex != null) mat.SetTexture(PropTMPMaskTex2, parentTex);
                mat.SetVector(PropTMPMaskUVRect2, _parentSoftMask.GetMaskUVRect());

                // 중첩 슬라이스 프로퍼티
                if (_cachedParentIsSliced)
                {
                    if (!mat.IsKeywordEnabled(KEYWORD_NESTED_SLICE))
                        mat.EnableKeyword(KEYWORD_NESTED_SLICE);
                    mat.SetVector(PropTMPMaskSliceBorder2, _cachedParentSliceBorder);
                    mat.SetVector(PropTMPMaskSliceInnerUV2, _cachedParentSliceInnerUV);
                }
                else if (mat.IsKeywordEnabled(KEYWORD_NESTED_SLICE))
                {
                    mat.DisableKeyword(KEYWORD_NESTED_SLICE);
                }
            }
            else
            {
                if (mat.IsKeywordEnabled(KEYWORD_NESTED))
                    mat.DisableKeyword(KEYWORD_NESTED);
            }
        }

        // ─────────────────────────────────────────────
        // Particle (UIParticle) 지원
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material이 CAT 파티클 셰이더를 사용하는지 판별
        /// </summary>
        private static bool IsParticleMaterial(Material mat)
        {
            return mat != null && mat.shader != null &&
                   mat.shader.name.StartsWith(PARTICLE_SHADER_PREFIX);
        }

        /// <summary>
        /// 파티클 전용 SoftMaskLight Material 생성
        /// Optional Shader로 교체하여 마스크 샘플링 추가
        /// 원본 프로퍼티를 복사하여 블렌드 모드 보존
        /// </summary>
        private Material CreateParticleMaskMaterial(Material originalMat)
        {
            Shader optShader = FindOptionalShader(originalMat.shader);
            if (optShader == null) return null;

            Material mat = new Material(optShader)
            {
                name = $"{optShader.name} (SoftMaskLight: {gameObject.name})",
                hideFlags = HideFlags.DontSave
            };
            mat.CopyPropertiesFromMaterial(originalMat);
            mat.shader = optShader;

            // 마스크 프로퍼티 설정 (슬라이스, 중첩 포함)
            ApplyMaskPropertiesToMaterial(mat);

            _particleMaskMaterials.Add(mat);
            return mat;
        }

        /// <summary>
        /// 동일한 원본 Material을 사용하는 자식끼리 마스크 clone을 공유하여 배칭 유지.
        /// (예: 같은 ColorReplace .mat 에셋을 참조하는 10개의 자식 → 1개의 마스크 clone 공유)
        /// UIShining/Windable처럼 인스턴스별 고유 Material을 사용하는 경우는
        /// 원본 Material 인스턴스 자체가 다르므로 자연스럽게 개별 clone이 생성됨.
        /// </summary>
        private Material GetOrCreateSharedCustomMaterial(Material originalMat, Shader optShader)
        {
            if (optShader == null) return null;

            // 동일한 원본 Material에 대해 이미 clone이 있으면 공유
            if (_sharedCustomClones.TryGetValue(originalMat, out Material existing) && existing != null)
                return existing;

            Material mat = new Material(optShader)
            {
                name = $"{optShader.name} (SoftMaskLight: {gameObject.name})",
                hideFlags = HideFlags.DontSave
            };
            mat.CopyPropertiesFromMaterial(originalMat);
            mat.shader = optShader;

            ApplyMaskPropertiesToMaterial(mat);

            _customMaskMaterials.Add(mat);
            _sharedCustomClones[originalMat] = mat;
            return mat;
        }

        /// <summary>
        /// 공유 clone이 다른 자식에 의해 아직 사용 중인지 확인
        /// </summary>
        private bool IsCustomCloneInUse(Material clone)
        {
            foreach (var kvp in _customAppliedMaskMats)
            {
                if (kvp.Value == clone) return true;
            }
            return false;
        }

        /// <summary>
        /// Material이 이미 마스크 clone인 경우 원본 Material을 역추적.
        /// 오브젝트 복제 시 clone 참조가 그대로 복사되는 경우에 사용.
        /// 공유 clone 캐시와 공유 Optional Material 모두 확인.
        /// </summary>
        private Material FindOriginalForCustomClone(Material mat)
        {
            if (mat == null) return null;
            // 커스텀 셰이더 공유 clone에서 역추적
            foreach (var kvp in _sharedCustomClones)
            {
                if (kvp.Value == mat) return kvp.Key;
            }
            // 공유 Optional Material (UI/Default 등)인지도 확인
            if (_customMaskMaterials.Contains(mat))
            {
                // _customMaskMaterials에는 있지만 _sharedCustomClones에는 없는 경우
                // (InvalidateChild 등으로 캐시만 제거된 상태) → null 반환하여 새로 처리
                return null;
            }
            foreach (var kvp in _sharedOptionalMaterials)
            {
                if (kvp.Value == mat) return null; // Optional Material은 원본 추적 불필요
            }
            return null;
        }

        /// <summary>
        /// _sharedCustomClones에서 해당 clone을 역방향으로 찾아 제거
        /// </summary>
        private void RemoveFromSharedCustomClones(Material clone)
        {
            Material keyToRemove = null;
            foreach (var kvp in _sharedCustomClones)
            {
                if (kvp.Value == clone) { keyToRemove = kvp.Key; break; }
            }
            if (keyToRemove != null)
                _sharedCustomClones.Remove(keyToRemove);
        }

        private Material CreateCustomMaskMaterial(Material originalMat, Shader optShader)
        {
            if (optShader == null) return null;

            Material mat = new Material(optShader)
            {
                name = $"{optShader.name} (SoftMaskLight: {gameObject.name})",
                hideFlags = HideFlags.DontSave
            };
            mat.CopyPropertiesFromMaterial(originalMat);
            mat.shader = optShader;

            ApplyMaskPropertiesToMaterial(mat);

            _customMaskMaterials.Add(mat);
            return mat;
        }

        /// <summary>
        /// 커스텀 셰이더 Material에 마스크 프로퍼티 갱신
        /// </summary>
        private void UpdateCustomMaterials()
        {
            for (int i = 0; i < _customMaskMaterials.Count; i++)
            {
                if (_customMaskMaterials[i] != null)
                    ApplyMaskPropertiesToMaterial(_customMaskMaterials[i]);
            }
        }

        /// <summary>
        /// 파티클 외부 Material 변경 감지 및 마스크 자동 재적용
        /// UIParticleRenderer가 내부적으로 Material을 재할당한 경우
        /// (비활성→활성 전환, RefreshParticles 등)
        /// </summary>
        private void DetectParticleMaterialChanges()
        {
            if (_particleAppliedMaskMats.Count == 0) return;

            _toRemove.Clear();

            foreach (var kvp in _particleAppliedMaskMats)
            {
                if (kvp.Key == null) { _toRemove.Add(kvp.Key); continue; }

                Material currentMat = kvp.Key.material;

                // 적용한 마스크 Material과 동일 → 변경 없음
                if (currentMat == kvp.Value || currentMat == null) continue;

                // Material이 외부에서 교체됨
                _toRemove.Add(kvp.Key);
            }

            if (_toRemove.Count == 0) return;

            for (int i = 0; i < _toRemove.Count; i++)
            {
                var child = _toRemove[i];

                // null 오브젝트 정리
                if (child == null)
                {
                    _particleAppliedMaskMats.Remove(child);
                    _originalChildMaterials.Remove(child);
                    continue;
                }

                // 기존 마스크 Material 정리
                if (_particleAppliedMaskMats.TryGetValue(child, out Material oldMaskMat) && oldMaskMat != null)
                {
                    _particleMaskMaterials.Remove(oldMaskMat);
                    if (Application.isPlaying) Destroy(oldMaskMat);
                    else DestroyImmediate(oldMaskMat);
                }

                // 새 Material 가져오기
                Material userMat = child.material;

                if (userMat == null)
                {
                    _particleAppliedMaskMats.Remove(child);
                    _originalChildMaterials.Remove(child);
                    continue;
                }

                // 원본 Material 갱신
                _originalChildMaterials[child] = userMat;

                // 여전히 파티클 Material이면 새 마스크 Material 생성
                if (IsParticleMaterial(userMat))
                {
                    Material newMaskMat = CreateParticleMaskMaterial(userMat);
                    if (newMaskMat != null)
                    {
                        child.material = newMaskMat;
                        _particleAppliedMaskMats[child] = newMaskMat;
                        child.SetAllDirty();
                    }
                    else
                    {
                        _particleAppliedMaskMats.Remove(child);
                    }
                }
                else
                {
                    _particleAppliedMaskMats.Remove(child);
                }
            }

            _materialDirty = true;
        }

        /// <summary>
        /// Particle Material에 현재 마스크 프로퍼티 일괄 전파
        /// </summary>
        private void UpdateParticleMaterials()
        {
            for (int i = _particleMaskMaterials.Count - 1; i >= 0; i--)
            {
                Material mat = _particleMaskMaterials[i];
                if (mat == null)
                {
                    _particleMaskMaterials.RemoveAt(i);
                    continue;
                }
                ApplyMaskPropertiesToMaterial(mat);
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
            return child.GetComponent<UIEffect>() != null
                || child.GetComponent<UIEffectReplica>() != null;
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
            // 캐시가 zero matrix이면 직접 계산하여 안전한 값 보장
            Matrix4x4 worldToUV = _cachedWorldToUV;
            if (worldToUV.m00 == 0f && worldToUV.m11 == 0f)
                worldToUV = ComputeWorldToMaskUV();

            // 기본 마스크 프로퍼티
            mat.SetMatrix(PropMaskWorldToUV, worldToUV);
            mat.SetFloat(PropSoftness, _cachedSoftness);
            mat.SetFloat(PropInvertMask, _cachedInvertMask ? 1f : 0f);
            if (maskTex != null) mat.SetTexture(PropMaskTex, maskTex);
            mat.SetVector(PropMaskUVRect, maskUVRect);

            // 슬라이스 프로퍼티
            if (_cachedIsSliced)
            {
                if (!mat.IsKeywordEnabled(KEYWORD_SLICE))
                    mat.EnableKeyword(KEYWORD_SLICE);
                mat.SetVector(PropMaskSliceBorder, _cachedSliceBorder);
                mat.SetVector(PropMaskSliceInnerUV, _cachedSliceInnerUV);
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
                if (parentWorldToUV.m00 == 0f && parentWorldToUV.m11 == 0f)
                    parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                mat.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                mat.SetFloat(PropSoftness2, _cachedParentSoftness);
                mat.SetFloat(PropInvertMask2, _cachedParentInvertMask ? 1f : 0f);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                if (parentTex != null) mat.SetTexture(PropMaskTex2, parentTex);
                mat.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());

                // 중첩 슬라이스 프로퍼티
                if (_cachedParentIsSliced)
                {
                    if (!mat.IsKeywordEnabled(KEYWORD_NESTED_SLICE))
                        mat.EnableKeyword(KEYWORD_NESTED_SLICE);
                    mat.SetVector(PropMaskSliceBorder2, _cachedParentSliceBorder);
                    mat.SetVector(PropMaskSliceInnerUV2, _cachedParentSliceInnerUV);
                }
                else if (mat.IsKeywordEnabled(KEYWORD_NESTED_SLICE))
                {
                    mat.DisableKeyword(KEYWORD_NESTED_SLICE);
                }
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
            if (_uiEffectProxies.Count == 0) return;

            for (int i = _uiEffectProxies.Count - 1; i >= 0; i--)
            {
                var proxy = _uiEffectProxies[i];
                if (proxy == null)
                {
                    _uiEffectProxies.RemoveAt(i);
                    continue;
                }

                Material mat = proxy.ProxyMaterial;
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
                if (proxy.ProxyMaterial == null)
                {
                    var graphic = proxy.GetComponent<UnityEngine.UI.Graphic>();
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
            for (int i = 0; i < _uiEffectProxies.Count; i++)
            {
                if (_uiEffectProxies[i] != null && _uiEffectProxies[i].ProxyMaterial == mat)
                    return true;
            }
            return false;
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
        /// SoftMask 셰이더 가져오기
        /// 직렬화 참조 → 정적 캐시 → Shader.Find() 순으로 폴백
        /// 직렬화 참조가 있으면 빌드에 셰이더가 자동 포함되어 Shader.Find() 실패를 방지
        /// </summary>
        private Shader GetCachedShader()
        {
            if (_maskShader != null)
            {
                s_cachedShader = _maskShader;
                return _maskShader;
            }

            if (s_cachedShader != null) return s_cachedShader;

            s_cachedShader = Shader.Find(SHADER_NAME);
            if (s_cachedShader == null)
            {
                Debug.LogError($"[SoftMaskLight] 셰이더를 찾을 수 없습니다: {SHADER_NAME}");
            }
            return s_cachedShader;
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

