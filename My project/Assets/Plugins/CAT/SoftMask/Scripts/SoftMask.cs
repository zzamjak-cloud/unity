using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// 알파 채널 기반 SoftMask 컴포넌트
    /// - 부모 오브젝트가 Mask 역할 (자신의 이미지 알파 = 마스킹 영역)
    /// - 자식 오브젝트는 부모 마스크 내에서만 렌더링됨
    /// - 부모/자식 이동, 회전 시 동적으로 마스킹 갱신
    /// - 중첩 SoftMask 지원 (최대 2단계)
    /// - SoftMask당 1개 공유 Material (배칭 최적화)
    /// - 더티 체크로 불필요한 Material 업데이트 스킵
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UnityEngine.UI.Graphic))]
    [AddComponentMenu("CAT/UI/SoftMask")]
    public class SoftMask : MonoBehaviour
    {
        public const string VERSION = "1.2.0";

        public static readonly string SHADER_NAME = "CAT/UI/SoftMask";
        public static readonly string TMP_SHADER_NAME = "CAT/UI/TMP_SoftMask";
        private static readonly string KEYWORD_NESTED = "_SOFTMASK_NESTED";
        private static readonly string PARTICLE_SHADER_PREFIX = "CAT/Particles/";
        private static readonly string KEYWORD_CAT_SOFTMASK = "_CAT_SOFTMASK";

        // 셰이더 직렬화 참조 (빌드에서 Shader.Find() 실패 방지)
        // 직렬화된 참조가 있으면 빌드에 셰이더가 자동 포함됨
        [SerializeField, HideInInspector] private Shader _maskShader;
        [SerializeField, HideInInspector] private Shader _tmpMaskShader;

        // 셰이더 캐싱
        private static Shader s_cachedShader;
        private static Shader s_cachedTMPShader;

        // Shader Property ID 캐싱 (일반 CAT/UI/SoftMask 셰이더용)
        private static readonly int PropMaskTex = Shader.PropertyToID("_MaskTex");
        private static readonly int PropSoftness = Shader.PropertyToID("_Softness");
        private static readonly int PropInvertMask = Shader.PropertyToID("_InvertMask");
        private static readonly int PropMaskWorldToUV = Shader.PropertyToID("_MaskWorldToUV");
        private static readonly int PropMaskUVRect = Shader.PropertyToID("_MaskUVRect");
        private static readonly int PropMaskTex2 = Shader.PropertyToID("_MaskTex2");
        private static readonly int PropSoftness2 = Shader.PropertyToID("_Softness2");
        private static readonly int PropInvertMask2 = Shader.PropertyToID("_InvertMask2");
        private static readonly int PropMaskWorldToUV2 = Shader.PropertyToID("_MaskWorldToUV2");
        private static readonly int PropMaskUVRect2 = Shader.PropertyToID("_MaskUVRect2");

        // TMP 셰이더용 프로퍼티 ID (_SoftMask* 접두사: TMP의 _MaskTex 충돌 방지)
        private static readonly int PropTMPMaskTex = Shader.PropertyToID("_SoftMaskTex");
        private static readonly int PropTMPSoftness = Shader.PropertyToID("_SoftMaskSoftness");
        private static readonly int PropTMPInvertMask = Shader.PropertyToID("_SoftMaskInvert");
        private static readonly int PropTMPMaskWorldToUV = Shader.PropertyToID("_SoftMaskWorldToUV");
        private static readonly int PropTMPMaskUVRect = Shader.PropertyToID("_SoftMaskUVRect");
        private static readonly int PropTMPMaskTex2 = Shader.PropertyToID("_SoftMaskTex2");
        private static readonly int PropTMPSoftness2 = Shader.PropertyToID("_SoftMaskSoftness2");
        private static readonly int PropTMPInvertMask2 = Shader.PropertyToID("_SoftMaskInvert2");
        private static readonly int PropTMPMaskWorldToUV2 = Shader.PropertyToID("_SoftMaskWorldToUV2");
        private static readonly int PropTMPMaskUVRect2 = Shader.PropertyToID("_SoftMaskUVRect2");

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
        private bool _initialized;

        // 공유 Material (이 SoftMask의 모든 자식이 공유)
        private Material _sharedMaskMaterial;

        // 자식 원본 Material 복원용
        private readonly Dictionary<UnityEngine.UI.Graphic, Material> _originalChildMaterials =
            new Dictionary<UnityEngine.UI.Graphic, Material>();

        // 마스크 그래픽 원본 색상
        private Color _originalMaskColor;
        private bool _originalColorSaved;

        // 중첩 마스크: 부모 SoftMask
        private SoftMask _parentSoftMask;
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

        // GC 방지: 재사용 리스트
        private readonly List<UnityEngine.UI.Graphic> _toRemove = new List<UnityEngine.UI.Graphic>(4);

        // 모드 전환 후 Stencil Material 강제 갱신 카운터
        // Canvas 리빌드(willRenderCanvases)가 LateUpdate 이후에 발생하므로
        // 2프레임 동안 PropagateToStencilMaterials() 강제 실행 필요
        private int _stencilRefreshCountdown;

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
                Debug.LogWarning($"[SoftMask] {gameObject.name}: UI.Graphic 컴포넌트가 필요합니다.");
                return;
            }

            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;

            // TMP / Particle 외부 Material 변경 감지
            DetectTMPMaterialChanges();
            DetectParticleMaterialChanges();

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

            // 부모 SoftMask 재검색
            _parentSoftMask = FindParentSoftMask();
            _hasParentMask = _parentSoftMask != null;

            // 레이아웃 완료 후 갱신 예약
            _pendingLayoutRefresh = true;
            _materialDirty = true;
            _stencilRefreshCountdown = 2;

            // 공유 Material의 중첩 마스크 키워드 갱신
            if (_sharedMaskMaterial != null)
            {
                if (_hasParentMask)
                    _sharedMaskMaterial.EnableKeyword(KEYWORD_NESTED);
                else
                    _sharedMaskMaterial.DisableKeyword(KEYWORD_NESTED);
            }

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
                ApplyMaskToChildren();
                return;
            }

            var children = GetComponentsInChildren<UnityEngine.UI.Graphic>(includeInactive: true);
            foreach (var child in children)
            {
                if (child.gameObject == gameObject) continue;
                if (!BelongsToThisMask(child.transform)) continue;
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
        /// 월드 좌표 → 마스크 UV (0~1) 변환 행렬 계산
        /// RectTransform의 회전, 스케일을 모두 반영
        /// Atlas 스프라이트 트리밍(투명 여백 제거) 보정 포함
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

            return localToUV * worldToLocal;
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

        // ─────────────────────────────────────────────
        // 중첩 마스크
        // ─────────────────────────────────────────────

        /// <summary>
        /// 부모 SoftMask 검색
        /// </summary>
        private SoftMask FindParentSoftMask()
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent<SoftMask>(out var mask) && mask.enabled && mask._initialized)
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
                if (current.TryGetComponent<SoftMask>(out var mask) && mask.enabled)
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
        // Material 관리 (SoftMask당 1개 공유)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 공유 Material 생성 또는 가져오기
        /// </summary>
        private Material GetOrCreateSharedMaterial()
        {
            if (_sharedMaskMaterial != null) return _sharedMaskMaterial;

            Shader shader = GetCachedShader();
            if (shader == null) return null;

            _sharedMaskMaterial = new Material(shader)
            {
                name = $"{SHADER_NAME} (Shared: {gameObject.name})",
                hideFlags = HideFlags.DontSave
            };

            // 자신의 마스크 설정
            Texture maskTex = GetMaskTexture();
            _cachedMaskTexId = maskTex != null ? maskTex.GetInstanceID() : 0;
            if (maskTex != null) _sharedMaskMaterial.SetTexture(PropMaskTex, maskTex);

            Matrix4x4 worldToUV = ComputeWorldToMaskUV();
            _sharedMaskMaterial.SetMatrix(PropMaskWorldToUV, worldToUV);
            _sharedMaskMaterial.SetFloat(PropSoftness, _softness);
            _sharedMaskMaterial.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
            _sharedMaskMaterial.SetVector(PropMaskUVRect, GetMaskUVRect());
            _cachedWorldToUV = worldToUV;
            _cachedSoftness = _softness;
            _cachedInvertMask = _invertMask;

            // 중첩 마스크 설정
            if (_hasParentMask && _parentSoftMask != null)
            {
                _sharedMaskMaterial.EnableKeyword(KEYWORD_NESTED);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                _cachedParentMaskTexId = parentTex != null ? parentTex.GetInstanceID() : 0;
                if (parentTex != null) _sharedMaskMaterial.SetTexture(PropMaskTex2, parentTex);

                Matrix4x4 parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                _sharedMaskMaterial.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                _sharedMaskMaterial.SetFloat(PropSoftness2, _parentSoftMask._softness);
                _sharedMaskMaterial.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                _sharedMaskMaterial.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
                _cachedParentWorldToUV = parentWorldToUV;
                _cachedParentSoftness = _parentSoftMask._softness;
                _cachedParentInvertMask = _parentSoftMask._invertMask;
            }
            else
            {
                _sharedMaskMaterial.DisableKeyword(KEYWORD_NESTED);
            }

            _materialDirty = false;
            return _sharedMaskMaterial;
        }

        /// <summary>
        /// 공유 Material 프로퍼티 업데이트 (더티 체크 포함)
        /// Transform 변경 시에만 행렬 업데이트, 프로퍼티 변경 시에만 값 업데이트
        /// UI Mask 내에서 사용 시 Stencil 래핑 Material에도 프로퍼티 전파
        /// </summary>
        private void UpdateSharedMaterial()
        {
            if (_originalChildMaterials.Count == 0) return;
            if (_sharedMaskMaterial == null && _tmpMaskMaterials.Count == 0 && _particleMaskMaterials.Count == 0) return;

            bool anyChange = false;

            // 자신의 변환 행렬 더티 체크
            Matrix4x4 currentWorldToUV = ComputeWorldToMaskUV();
            if (currentWorldToUV != _cachedWorldToUV)
            {
                if (_sharedMaskMaterial != null)
                    _sharedMaskMaterial.SetMatrix(PropMaskWorldToUV, currentWorldToUV);
                _cachedWorldToUV = currentWorldToUV;
                anyChange = true;
            }

            // 마스크 텍스처 변경 체크
            Texture maskTex = GetMaskTexture();
            int texId = maskTex != null ? maskTex.GetInstanceID() : 0;
            if (texId != _cachedMaskTexId)
            {
                _cachedMaskTexId = texId;
                if (_sharedMaskMaterial != null)
                {
                    if (maskTex != null) _sharedMaskMaterial.SetTexture(PropMaskTex, maskTex);
                    _sharedMaskMaterial.SetVector(PropMaskUVRect, GetMaskUVRect());
                }
                anyChange = true;
            }

            // Softness / InvertMask 변경 체크
            if (_materialDirty || _softness != _cachedSoftness || _invertMask != _cachedInvertMask)
            {
                if (_sharedMaskMaterial != null)
                {
                    _sharedMaskMaterial.SetFloat(PropSoftness, _softness);
                    _sharedMaskMaterial.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
                }
                _cachedSoftness = _softness;
                _cachedInvertMask = _invertMask;
                anyChange = true;
            }

            // 부모 마스크 업데이트 (중첩 마스크)
            if (_hasParentMask && _parentSoftMask != null && _parentSoftMask.enabled)
            {
                Matrix4x4 parentWorldToUV = _parentSoftMask.ComputeWorldToMaskUV();
                if (parentWorldToUV != _cachedParentWorldToUV)
                {
                    if (_sharedMaskMaterial != null)
                        _sharedMaskMaterial.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                    _cachedParentWorldToUV = parentWorldToUV;
                    anyChange = true;
                }

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                int parentTexId = parentTex != null ? parentTex.GetInstanceID() : 0;
                if (parentTexId != _cachedParentMaskTexId)
                {
                    _cachedParentMaskTexId = parentTexId;
                    if (_sharedMaskMaterial != null)
                    {
                        if (parentTex != null) _sharedMaskMaterial.SetTexture(PropMaskTex2, parentTex);
                        _sharedMaskMaterial.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
                    }
                    anyChange = true;
                }

                if (_parentSoftMask._softness != _cachedParentSoftness ||
                    _parentSoftMask._invertMask != _cachedParentInvertMask)
                {
                    if (_sharedMaskMaterial != null)
                    {
                        _sharedMaskMaterial.SetFloat(PropSoftness2, _parentSoftMask._softness);
                        _sharedMaskMaterial.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                    }
                    _cachedParentSoftness = _parentSoftMask._softness;
                    _cachedParentInvertMask = _parentSoftMask._invertMask;
                    anyChange = true;
                }
            }

            // TMP, Particle, Stencil Material에 마스크 프로퍼티 전파
            if (anyChange || _materialDirty)
            {
                UpdateTMPMaterials();
                UpdateParticleMaterials();
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
                if (rendered == _sharedMaskMaterial) continue;
                if (_tmpMaskMaterials.Contains(rendered)) continue;
                if (_particleMaskMaterials.Contains(rendered)) continue;

                // 셰이더 이름 기반으로 일반/TMP/Particle 프로퍼티 ID 결정
                int pTex, pSoftness, pInvert, pWorldToUV, pUVRect;
                int pTex2, pSoftness2, pInvert2, pWorldToUV2, pUVRect2;

                string shaderName = rendered.shader != null ? rendered.shader.name : "";
                if (shaderName == TMP_SHADER_NAME)
                {
                    // CAT/UI/TMP_SoftMask 셰이더 (_SoftMask* 접두사)
                    pTex = PropTMPMaskTex; pSoftness = PropTMPSoftness; pInvert = PropTMPInvertMask;
                    pWorldToUV = PropTMPMaskWorldToUV; pUVRect = PropTMPMaskUVRect;
                    pTex2 = PropTMPMaskTex2; pSoftness2 = PropTMPSoftness2; pInvert2 = PropTMPInvertMask2;
                    pWorldToUV2 = PropTMPMaskWorldToUV2; pUVRect2 = PropTMPMaskUVRect2;
                }
                else if (shaderName == SHADER_NAME)
                {
                    // 일반 CAT/UI/SoftMask 셰이더
                    pTex = PropMaskTex; pSoftness = PropSoftness; pInvert = PropInvertMask;
                    pWorldToUV = PropMaskWorldToUV; pUVRect = PropMaskUVRect;
                    pTex2 = PropMaskTex2; pSoftness2 = PropSoftness2; pInvert2 = PropInvertMask2;
                    pWorldToUV2 = PropMaskWorldToUV2; pUVRect2 = PropMaskUVRect2;
                }
                else if (shaderName.StartsWith(PARTICLE_SHADER_PREFIX))
                {
                    // CAT/Particles/* 셰이더 (표준 프로퍼티 이름 + _CAT_SOFTMASK 키워드)
                    if (!rendered.IsKeywordEnabled(KEYWORD_CAT_SOFTMASK))
                        rendered.EnableKeyword(KEYWORD_CAT_SOFTMASK);

                    pTex = PropMaskTex; pSoftness = PropSoftness; pInvert = PropInvertMask;
                    pWorldToUV = PropMaskWorldToUV; pUVRect = PropMaskUVRect;
                    pTex2 = PropMaskTex2; pSoftness2 = PropSoftness2; pInvert2 = PropInvertMask2;
                    pWorldToUV2 = PropMaskWorldToUV2; pUVRect2 = PropMaskUVRect2;
                }
                else
                {
                    // SoftMask 셰이더가 아닌 Material → 스킵
                    continue;
                }

                // Stencil 래핑된 Material에 마스크 프로퍼티 복사
                rendered.SetMatrix(pWorldToUV, _cachedWorldToUV);
                rendered.SetFloat(pSoftness, _cachedSoftness);
                rendered.SetFloat(pInvert, _cachedInvertMask ? 1f : 0f);

                if (maskTex != null) rendered.SetTexture(pTex, maskTex);
                rendered.SetVector(pUVRect, maskUVRect);

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
                }
            }
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
            }
        }

        // ─────────────────────────────────────────────
        // 자식 오브젝트 마스킹
        // ─────────────────────────────────────────────

        /// <summary>
        /// 자식 오브젝트에 공유 마스크 Material 적용
        /// </summary>
        public void ApplyMaskToChildren()
        {
            if (!_initialized) return;

            Texture maskTex = GetMaskTexture();
            if (maskTex == null) return;

            Material mat = GetOrCreateSharedMaterial();
            if (mat == null) return;

            var children = GetComponentsInChildren<UnityEngine.UI.Graphic>(includeInactive: true);
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

                // 일반 Graphic (TMP_SubMeshUI 포함)
                Material originalMat = child.material;

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

                // Particle (UIParticle) — 원본 셰이더 유지, _CAT_SOFTMASK 키워드 추가
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

                child.material = mat;
                child.SetAllDirty();
            }
        }

        /// <summary>
        /// 자식 오브젝트의 원본 Material 복원
        /// </summary>
        public void RestoreChildrenMaterials()
        {
            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null || kvp.Value == null) continue;

                // TMP_Text는 fontSharedMaterial로 복원
                if (kvp.Key is TMP_Text tmpText)
                    tmpText.fontSharedMaterial = kvp.Value;
                else
                    kvp.Key.material = kvp.Value;
            }

            _originalChildMaterials.Clear();

            // 공유 Material 파괴
            if (_sharedMaskMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_sharedMaskMaterial);
                else
                    DestroyImmediate(_sharedMaskMaterial);
                _sharedMaskMaterial = null;
            }

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
                name = $"{TMP_SHADER_NAME} (SoftMask: {gameObject.name})",
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
                Debug.LogWarning($"[SoftMask] TMP 셰이더가 지원되지 않습니다: {TMP_SHADER_NAME}");
                _tmpMaskMaterials.Add(tmpMat);
                return tmpMat;
            }

            // CopyPropertiesFromMaterial() 후 SoftMask 프로퍼티 재설정
            // CopyPropertiesFromMaterial이 Material 내부 프로퍼티 시트를
            // 원본 TMP Material 기준으로 덮어쓰므로, SoftMask 프로퍼티를 항상 재설정해야 함
            Texture maskTex = GetMaskTexture();
            if (maskTex != null) tmpMat.SetTexture(PropTMPMaskTex, maskTex);
            tmpMat.SetMatrix(PropTMPMaskWorldToUV, _cachedWorldToUV);
            tmpMat.SetFloat(PropTMPSoftness, _softness);
            tmpMat.SetFloat(PropTMPInvertMask, _invertMask ? 1f : 0f);
            tmpMat.SetVector(PropTMPMaskUVRect, GetMaskUVRect());

            // 중첩 마스크 설정
            if (_hasParentMask && _parentSoftMask != null)
            {
                tmpMat.EnableKeyword(KEYWORD_NESTED);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                if (parentTex != null) tmpMat.SetTexture(PropTMPMaskTex2, parentTex);
                tmpMat.SetMatrix(PropTMPMaskWorldToUV2, _parentSoftMask.ComputeWorldToMaskUV());
                tmpMat.SetFloat(PropTMPSoftness2, _parentSoftMask._softness);
                tmpMat.SetFloat(PropTMPInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                tmpMat.SetVector(PropTMPMaskUVRect2, _parentSoftMask.GetMaskUVRect());
            }
            else
            {
                tmpMat.DisableKeyword(KEYWORD_NESTED);
            }

            _tmpMaskMaterials.Add(tmpMat);
            return tmpMat;
        }

        /// <summary>
        /// TMP Material에 현재 마스크 프로퍼티 일괄 전파
        /// </summary>
        private void UpdateTMPMaterials()
        {
            Texture maskTex = GetMaskTexture();
            Vector4 maskUVRect = GetMaskUVRect();

            for (int i = _tmpMaskMaterials.Count - 1; i >= 0; i--)
            {
                Material tmpMat = _tmpMaskMaterials[i];
                if (tmpMat == null)
                {
                    _tmpMaskMaterials.RemoveAt(i);
                    continue;
                }

                // TMP 셰이더는 _SoftMask* 접두사 프로퍼티 사용
                // (HasProperty 대신 shader 기준 체크 - CopyPropertiesFromMaterial이 프로퍼티 시트를 덮어쓸 수 있음)
                if (tmpMat.shader == null || !tmpMat.shader.isSupported) continue;
                tmpMat.SetMatrix(PropTMPMaskWorldToUV, _cachedWorldToUV);
                tmpMat.SetFloat(PropTMPSoftness, _cachedSoftness);
                tmpMat.SetFloat(PropTMPInvertMask, _cachedInvertMask ? 1f : 0f);
                if (maskTex != null) tmpMat.SetTexture(PropTMPMaskTex, maskTex);
                tmpMat.SetVector(PropTMPMaskUVRect, maskUVRect);

                if (_hasParentMask && _parentSoftMask != null)
                {
                    if (!tmpMat.IsKeywordEnabled(KEYWORD_NESTED))
                        tmpMat.EnableKeyword(KEYWORD_NESTED);

                    tmpMat.SetMatrix(PropTMPMaskWorldToUV2, _cachedParentWorldToUV);
                    tmpMat.SetFloat(PropTMPSoftness2, _cachedParentSoftness);
                    tmpMat.SetFloat(PropTMPInvertMask2, _cachedParentInvertMask ? 1f : 0f);

                    Texture parentTex = _parentSoftMask.GetMaskTexture();
                    if (parentTex != null) tmpMat.SetTexture(PropTMPMaskTex2, parentTex);
                    tmpMat.SetVector(PropTMPMaskUVRect2, _parentSoftMask.GetMaskUVRect());
                }
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
        /// 파티클 전용 SoftMask Material 생성
        /// 원본 Material을 복제하여 블렌드 모드/셰이더를 보존하고
        /// _CAT_SOFTMASK 키워드를 활성화하여 마스크 샘플링 추가
        /// </summary>
        private Material CreateParticleMaskMaterial(Material originalMat)
        {
            Material mat = new Material(originalMat)
            {
                name = $"{originalMat.shader.name} (SoftMask: {gameObject.name})",
                hideFlags = HideFlags.DontSave
            };

            mat.EnableKeyword(KEYWORD_CAT_SOFTMASK);

            // 마스크 프로퍼티 설정 (표준 프로퍼티 이름)
            Texture maskTex = GetMaskTexture();
            if (maskTex != null) mat.SetTexture(PropMaskTex, maskTex);
            mat.SetMatrix(PropMaskWorldToUV, ComputeWorldToMaskUV());
            mat.SetFloat(PropSoftness, _softness);
            mat.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
            mat.SetVector(PropMaskUVRect, GetMaskUVRect());

            // 중첩 마스크 설정
            if (_hasParentMask && _parentSoftMask != null)
            {
                mat.EnableKeyword(KEYWORD_NESTED);

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                if (parentTex != null) mat.SetTexture(PropMaskTex2, parentTex);
                mat.SetMatrix(PropMaskWorldToUV2, _parentSoftMask.ComputeWorldToMaskUV());
                mat.SetFloat(PropSoftness2, _parentSoftMask._softness);
                mat.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                mat.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
            }

            _particleMaskMaterials.Add(mat);
            return mat;
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
            Texture maskTex = GetMaskTexture();
            Vector4 maskUVRect = GetMaskUVRect();

            for (int i = _particleMaskMaterials.Count - 1; i >= 0; i--)
            {
                Material mat = _particleMaskMaterials[i];
                if (mat == null)
                {
                    _particleMaskMaterials.RemoveAt(i);
                    continue;
                }

                // Particle 셰이더는 표준 프로퍼티 이름 사용
                mat.SetMatrix(PropMaskWorldToUV, _cachedWorldToUV);
                mat.SetFloat(PropSoftness, _cachedSoftness);
                mat.SetFloat(PropInvertMask, _cachedInvertMask ? 1f : 0f);
                if (maskTex != null) mat.SetTexture(PropMaskTex, maskTex);
                mat.SetVector(PropMaskUVRect, maskUVRect);

                if (_hasParentMask && _parentSoftMask != null)
                {
                    if (!mat.IsKeywordEnabled(KEYWORD_NESTED))
                        mat.EnableKeyword(KEYWORD_NESTED);

                    mat.SetMatrix(PropMaskWorldToUV2, _cachedParentWorldToUV);
                    mat.SetFloat(PropSoftness2, _cachedParentSoftness);
                    mat.SetFloat(PropInvertMask2, _cachedParentInvertMask ? 1f : 0f);

                    Texture parentTex = _parentSoftMask.GetMaskTexture();
                    if (parentTex != null) mat.SetTexture(PropMaskTex2, parentTex);
                    mat.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
                }
            }
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
                Debug.LogError($"[SoftMask] 셰이더를 찾을 수 없습니다: {SHADER_NAME}");
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
                Debug.LogError($"[SoftMask] TMP 셰이더를 찾을 수 없습니다: {TMP_SHADER_NAME}");
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
        /// 현재 마스킹된 자식 수 (에디터 정보 표시용)
        /// </summary>
        public int MaskedChildCount => _originalChildMaterials.Count;

        /// <summary>
        /// 부모 SoftMask 참조 (에디터 정보 표시용)
        /// </summary>
        public SoftMask ParentSoftMask => _parentSoftMask;
    }
}

