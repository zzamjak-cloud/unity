using UnityEngine;
using System.Collections.Generic;

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
        public static readonly string SHADER_NAME = "CAT/UI/SoftMask";
        private static readonly string KEYWORD_NESTED = "_SOFTMASK_NESTED";

        // 셰이더 캐싱
        private static Shader s_cachedShader;

        // Shader Property ID 캐싱
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

        // GC 방지: 재사용 리스트
        private readonly List<UnityEngine.UI.Graphic> _toRemove = new List<UnityEngine.UI.Graphic>(4);

        // 모드 전환 후 Stencil Material 강제 갱신 카운터
        // Canvas 리빌드(willRenderCanvases)가 LateUpdate 이후에 발생하므로
        // 2프레임 동안 PropagateToStencilMaterials() 강제 실행 필요
        private int _stencilRefreshCountdown;

#if UNITY_EDITOR
        private int _lastChildCount;
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

#if UNITY_EDITOR
            _lastChildCount = transform.childCount;
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
            }
#endif
        }

        private void OnDisable()
        {
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
            RestoreChildrenMaterials();

            if (_originalColorSaved && _uiGraphic != null)
            {
                _uiGraphic.color = _originalMaskColor;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!gameObject.scene.IsValid()) return;

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
            if (_sharedMaskMaterial == null || _originalChildMaterials.Count == 0) return;

            bool anyChange = false;

            // 자신의 변환 행렬 더티 체크
            Matrix4x4 currentWorldToUV = ComputeWorldToMaskUV();
            if (currentWorldToUV != _cachedWorldToUV)
            {
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
                if (maskTex != null) _sharedMaskMaterial.SetTexture(PropMaskTex, maskTex);
                _sharedMaskMaterial.SetVector(PropMaskUVRect, GetMaskUVRect());
                anyChange = true;
            }

            // Softness / InvertMask 변경 체크
            if (_materialDirty || _softness != _cachedSoftness || _invertMask != _cachedInvertMask)
            {
                _sharedMaskMaterial.SetFloat(PropSoftness, _softness);
                _sharedMaskMaterial.SetFloat(PropInvertMask, _invertMask ? 1f : 0f);
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
                    _sharedMaskMaterial.SetMatrix(PropMaskWorldToUV2, parentWorldToUV);
                    _cachedParentWorldToUV = parentWorldToUV;
                    anyChange = true;
                }

                Texture parentTex = _parentSoftMask.GetMaskTexture();
                int parentTexId = parentTex != null ? parentTex.GetInstanceID() : 0;
                if (parentTexId != _cachedParentMaskTexId)
                {
                    _cachedParentMaskTexId = parentTexId;
                    if (parentTex != null) _sharedMaskMaterial.SetTexture(PropMaskTex2, parentTex);
                    _sharedMaskMaterial.SetVector(PropMaskUVRect2, _parentSoftMask.GetMaskUVRect());
                    anyChange = true;
                }

                if (_parentSoftMask._softness != _cachedParentSoftness ||
                    _parentSoftMask._invertMask != _cachedParentInvertMask)
                {
                    _sharedMaskMaterial.SetFloat(PropSoftness2, _parentSoftMask._softness);
                    _sharedMaskMaterial.SetFloat(PropInvertMask2, _parentSoftMask._invertMask ? 1f : 0f);
                    _cachedParentSoftness = _parentSoftMask._softness;
                    _cachedParentInvertMask = _parentSoftMask._invertMask;
                    anyChange = true;
                }
            }

            // UI Mask의 StencilMaterial 복사본에 마스크 프로퍼티 전파
            if (anyChange || _materialDirty)
            {
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
            foreach (var kvp in _originalChildMaterials)
            {
                if (kvp.Key == null) continue;

                Material rendered = kvp.Key.materialForRendering;
                if (rendered == null || rendered == _sharedMaskMaterial) continue;

                // Stencil 래핑된 Material에 마스크 프로퍼티 복사
                rendered.SetMatrix(PropMaskWorldToUV, _cachedWorldToUV);
                rendered.SetFloat(PropSoftness, _cachedSoftness);
                rendered.SetFloat(PropInvertMask, _cachedInvertMask ? 1f : 0f);

                Texture maskTex = _sharedMaskMaterial.GetTexture(PropMaskTex);
                if (maskTex != null) rendered.SetTexture(PropMaskTex, maskTex);
                rendered.SetVector(PropMaskUVRect, _sharedMaskMaterial.GetVector(PropMaskUVRect));

                if (_hasParentMask)
                {
                    if (!rendered.IsKeywordEnabled(KEYWORD_NESTED))
                        rendered.EnableKeyword(KEYWORD_NESTED);

                    rendered.SetMatrix(PropMaskWorldToUV2, _cachedParentWorldToUV);
                    rendered.SetFloat(PropSoftness2, _cachedParentSoftness);
                    rendered.SetFloat(PropInvertMask2, _cachedParentInvertMask ? 1f : 0f);

                    Texture parentTex = _sharedMaskMaterial.GetTexture(PropMaskTex2);
                    if (parentTex != null) rendered.SetTexture(PropMaskTex2, parentTex);
                    rendered.SetVector(PropMaskUVRect2, _sharedMaskMaterial.GetVector(PropMaskUVRect2));
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

                // 원본 Material 저장 후 공유 Material 적용
                _originalChildMaterials[child] = child.material;
                child.material = mat;
                // Stencil Material 재생성을 위한 Canvas 강제 리빌드
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
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.material = kvp.Value;
                }
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
        }

        // ─────────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────────

        private static Shader GetCachedShader()
        {
            if (s_cachedShader == null)
            {
                s_cachedShader = Shader.Find(SHADER_NAME);
                if (s_cachedShader == null)
                {
                    Debug.LogError($"[SoftMask] 셰이더를 찾을 수 없습니다: {SHADER_NAME}");
                }
            }
            return s_cachedShader;
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
