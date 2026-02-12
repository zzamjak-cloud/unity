using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// TMP 외곽 Glow 효과 (Underlay 기반)
    /// - TMP SDF를 활용한 방사형 외곽 glow 효과
    /// - Material 공유로 모바일 최적화 (90-95% Material 감소)
    /// - Offset (0, 0) 고정으로 균일한 glow 표현
    /// - _UnderlayDilate로 glow 범위 조절
    /// - Inner Glow: 자식 TMP 오브젝트로 내부 빛 효과 (항상 활성화)
    /// - GlowColor RGB가 TMP Tint color에 자동 반영
    /// </summary>
    /// <remarks>
    /// 설계:
    /// - GlowColor → _UnderlayColor + TMP Tint RGB
    /// - GlowRange → _UnderlayDilate (SDF 확장 크기)
    /// - _UnderlaySoftness = 1f (고정, 최대 블러)
    /// - _UnderlayOffsetX/Y = 0 (방사형 glow)
    /// - InnerGlow: 항상 활성화, GlowColor RGB + 별도 Alpha 제어
    ///
    /// 제한사항:
    /// - SDF 폰트 필요 (8-16px 패딩 권장)
    ///
    /// 버전: v1.2.0
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("CAT/UI/TMP Out Glow")]
    public class TMPOutGlow : TMPEffect, ITMPEffectSettings
    {
        // ─────────────────────────────────────────────
        // Preset
        // ─────────────────────────────────────────────

        [Header("Preset (Optional)")]
        [SerializeField] private TMPEffectPreset _preset;

        // ─────────────────────────────────────────────
        // Glow Properties
        // ─────────────────────────────────────────────

        [Header("Glow")]
        [Tooltip("색상 (외곽으로 번지는 색상, TMP Tint RGB에도 자동 반영)")]
        [SerializeField] private Color _glowColor = new Color(1f, 0.8f, 0f, 0.5f);

        [Tooltip("범위 (0~1, SDF 확장 크기)")]
        [SerializeField, Range(0f, 1f)] private float _glowRange = 0.3f;

        [Tooltip("Alpha (0~1, 내부 빛 강도)")]
        [SerializeField, Range(0f, 1f)] private float _innerGlowAlpha = 1f;

        [Tooltip("Dilate (-1~1, 텍스트 본체 굵기)")]
        [SerializeField, Range(-1f, 1f)] private float _faceDilate = 0f;

        // ─────────────────────────────────────────────
        // 자식 TMP 오브젝트 (Inner Glow용)
        // ─────────────────────────────────────────────

        private GameObject _innerGlowObject;
        private TextMeshProUGUI _innerGlowText;
        private TMPCurve _innerGlowCurve;
        private CanvasGroup _innerGlowCanvasGroup;  // 깜빡임 방지용
        private bool _innerGlowNeedsShow = false;  // 첫 프레임 후 표시 플래그

        // ─────────────────────────────────────────────
        // 더티 체크 최적화 (BitMask)
        // ─────────────────────────────────────────────

        [System.Flags]
        private enum DirtyFlags
        {
            None = 0,
            GlowColor = 1 << 0,
            GlowRange = 1 << 1,
            FaceDilate = 1 << 2,
            InnerGlow = 1 << 3,
            Material = GlowColor | GlowRange | FaceDilate
        }

        private DirtyFlags _dirtyFlags = DirtyFlags.None;

        // 더티 체크용 이전 값
        private Color _prevGlowColor;
        private float _prevGlowRange;
        private float _prevFaceDilate;

        // ─────────────────────────────────────────────
        // 캐시
        // ─────────────────────────────────────────────

        // Material 캐시
        private Material _sharedMaterial;
        private Material _originalSharedMaterial;

        // TMP 컴포넌트
        private TextMeshProUGUI _tmpText;

        // 초기화 플래그
        private bool _needsInitialization = true;

        // ─────────────────────────────────────────────
        // ITMPEffectSettings 구현 (Underlay 파라미터 매핑)
        // ─────────────────────────────────────────────

        Color ITMPEffectSettings.UnderlayColor => _glowColor;
        float ITMPEffectSettings.UnderlayDilate => _glowRange;
        float ITMPEffectSettings.UnderlaySoftness => 1f;  // 고정 (최대 블러)
        float ITMPEffectSettings.UnderlayOffsetX => 0f;  // 방사형 glow를 위해 0 고정
        float ITMPEffectSettings.UnderlayOffsetY => 0f;  // 방사형 glow를 위해 0 고정
        float ITMPEffectSettings.FaceDilate => _faceDilate;  // 항상 활성화

        // Shadow 관련 (Glow에서는 미사용)
        bool ITMPEffectSettings.EnableShadow => false;
        Vector2 ITMPEffectSettings.ShadowOffset => Vector2.zero;
        float ITMPEffectSettings.ShadowAlpha => 0f;

        /// <summary>
        /// Material 공유를 위한 해시 계산
        /// </summary>
        public int GetMaterialHash()
        {
            return TMPEffectUtility.CalculateMaterialHash(
                _glowColor,
                _glowRange,
                0f,  // UnderlayOffsetX
                0f,  // UnderlayOffsetY
                1f,  // UnderlaySoftness (고정)
                _faceDilate  // 항상 활성화
            );
        }

        // ─────────────────────────────────────────────
        // Preset API
        // ─────────────────────────────────────────────

        public TMPEffectPreset Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                if (_preset != null)
                {
                    _preset.ApplyTo(this);
                }
            }
        }

        /// <summary>
        /// Preset을 적용하고 Material 업데이트
        /// </summary>
        public void ApplyPreset(TMPEffectPreset preset)
        {
            if (preset == null) return;

            _preset = preset;
            preset.ApplyTo(this);
            UpdateGlowMaterial();
        }

        // ─────────────────────────────────────────────
        // Public Properties (Dirty Check)
        // ─────────────────────────────────────────────

        public Color GlowColor
        {
            get => _glowColor;
            set
            {
                if (_glowColor != value)
                {
                    _glowColor = value;
                    _dirtyFlags |= DirtyFlags.GlowColor | DirtyFlags.InnerGlow;
                    UpdateGlowMaterial();

                    // TMP Tint color의 RGB를 Glow Color RGB로 자동 업데이트 (Alpha는 유지)
                    if (_tmpText != null)
                    {
                        Color tintColor = _tmpText.color;
                        _tmpText.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, tintColor.a);
                    }
                }
            }
        }

        public float GlowRange
        {
            get => _glowRange;
            set
            {
                value = Mathf.Clamp01(value);
                if (!Mathf.Approximately(_glowRange, value))
                {
                    _glowRange = value;
                    _dirtyFlags |= DirtyFlags.GlowRange;
                    UpdateGlowMaterial();
                }
            }
        }

        public float FaceDilate
        {
            get => _faceDilate;
            set
            {
                value = Mathf.Clamp(value, -1f, 1f);
                if (!Mathf.Approximately(_faceDilate, value))
                {
                    _faceDilate = value;
                    _dirtyFlags |= DirtyFlags.FaceDilate;
                    UpdateGlowMaterial();
                }
            }
        }

        public float InnerGlowAlpha
        {
            get => _innerGlowAlpha;
            set
            {
                value = Mathf.Clamp01(value);
                if (!Mathf.Approximately(_innerGlowAlpha, value))
                {
                    _innerGlowAlpha = value;
                    _dirtyFlags |= DirtyFlags.InnerGlow;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Glow 설정을 한 번에 변경
        /// </summary>
        public void SetGlow(Color color, float range)
        {
            _glowColor = color;
            _glowRange = Mathf.Clamp01(range);
            _dirtyFlags = DirtyFlags.Material;
            UpdateGlowMaterial();
        }

        /// <summary>
        /// Inner Glow TMP 텍스트 반환 (TMPAnimation 등에서 사용)
        /// </summary>
        public TextMeshProUGUI GetInnerGlowText()
        {
            return _innerGlowText;
        }

        /// <summary>
        /// Inner Glow를 강제로 동기화 및 업데이트 (TMPAnimation 복원 시 사용)
        /// </summary>
        public void ForceUpdateInnerGlow()
        {
            if (_innerGlowText == null || _tmpText == null) return;

            // RectTransform 설정 복원 (부모를 완전히 채움)
            RectTransform parentRect = _tmpText.rectTransform;
            RectTransform childRect = _innerGlowText.rectTransform;

            childRect.anchorMin = Vector2.zero;
            childRect.anchorMax = Vector2.one;
            childRect.anchoredPosition = Vector2.zero;
            childRect.sizeDelta = Vector2.zero;
            childRect.pivot = parentRect.pivot;
            childRect.localScale = Vector3.one;  // 스케일 강제 리셋
            childRect.localRotation = Quaternion.identity;

            // TMP 속성 동기화
            SyncInnerGlow();

            // Material 업데이트
            UpdateInnerGlowMaterial();

            // 메시 강제 갱신
            _innerGlowText.ForceMeshUpdate();
        }

        /// <summary>
        /// Glow 효과 초기화 (기본값으로 리셋)
        /// </summary>
        public void ResetEffect()
        {
            _glowColor = new Color(1f, 0.8f, 0f, 0.5f);
            _glowRange = 0.3f;
            _faceDilate = 0f;
            _innerGlowAlpha = 1f;
            _dirtyFlags = DirtyFlags.Material | DirtyFlags.InnerGlow;
            UpdateGlowMaterial();

            // TMP Tint color RGB도 업데이트
            if (_tmpText != null)
            {
                Color tintColor = _tmpText.color;
                _tmpText.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, tintColor.a);
            }
        }

        // ─────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            CacheComponents();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheComponents();
            _needsInitialization = true;
            UpdateGlowMaterial();

            // TMP Tint color RGB 초기화
            if (_tmpText != null)
            {
                Color tintColor = _tmpText.color;
                _tmpText.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, tintColor.a);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RestoreOriginalMaterial();
        }

        private void OnDestroy()
        {
            RestoreOriginalMaterial();
            DestroyInnerGlowObject();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CacheComponents();
            _dirtyFlags = DirtyFlags.Material | DirtyFlags.InnerGlow;
            UpdateGlowMaterial();

            // TMP Tint color RGB도 업데이트
            if (_tmpText != null)
            {
                Color tintColor = _tmpText.color;
                _tmpText.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, tintColor.a);
            }

            // InnerGlow Material 업데이트 (항상 활성화)
            if (_innerGlowObject)
            {
                UpdateInnerGlowMaterial();
            }
        }

        protected override void Reset()
        {
            base.Reset();
            CacheComponents();
            ResetEffect();
        }
#endif

        private void LateUpdate()
        {
            // 초기화가 필요하거나 TMP Material이 변경된 경우
            if (_needsInitialization || HasMaterialChanged())
            {
                _needsInitialization = false;
                UpdateGlowMaterial();

                // TMP Tint color RGB 동기화
                if (_tmpText != null)
                {
                    Color tintColor = _tmpText.color;
                    _tmpText.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, tintColor.a);
                }

                // InnerGlow도 같이 업데이트
                if (_innerGlowObject)
                {
                    UpdateInnerGlowMaterial();
                }
            }

            // Inner Glow 관리 (항상 활성화)
            if (!_innerGlowObject)
            {
                CreateInnerGlowObject();
            }
            else
            {
                // TMP 속성 동기화 (텍스트 내용 변경 시)
                SyncInnerGlow();

                // Transform 강제 동기화 (사용자 수정 방지)
                ForceInnerGlowTransform();

                // InnerGlow 파라미터가 변경되었는지 확인
                if ((_dirtyFlags & DirtyFlags.InnerGlow) != 0)
                {
                    UpdateInnerGlowMaterial();
                    _dirtyFlags &= ~DirtyFlags.InnerGlow;
                }
            }

            // Inner Glow 표시 (깜빡임 방지 - 메시 초기화 후 표시)
            if (_innerGlowNeedsShow && _innerGlowCanvasGroup != null)
            {
                _innerGlowCanvasGroup.alpha = 1f;
                _innerGlowNeedsShow = false;
            }
        }

        /// <summary>
        /// Inner Glow Transform 강제 동기화 (사용자 수정 방지)
        /// </summary>
        private void ForceInnerGlowTransform()
        {
            if (_innerGlowText == null || _tmpText == null) return;

            RectTransform parentRect = _tmpText.rectTransform;
            RectTransform childRect = _innerGlowText.rectTransform;

            // Anchor 강제 리셋 (부모를 100% 채움)
            if (childRect.anchorMin != Vector2.zero)
                childRect.anchorMin = Vector2.zero;
            if (childRect.anchorMax != Vector2.one)
                childRect.anchorMax = Vector2.one;

            // 위치 강제 리셋
            if (childRect.anchoredPosition != Vector2.zero)
                childRect.anchoredPosition = Vector2.zero;

            // 크기 강제 리셋
            if (childRect.sizeDelta != Vector2.zero)
                childRect.sizeDelta = Vector2.zero;

            // Pivot 동기화
            if (childRect.pivot != parentRect.pivot)
                childRect.pivot = parentRect.pivot;

            // 스케일 강제 리셋
            if (childRect.localScale != Vector3.one)
                childRect.localScale = Vector3.one;

            // 회전 강제 리셋
            if (childRect.localRotation != Quaternion.identity)
                childRect.localRotation = Quaternion.identity;
        }

        // ─────────────────────────────────────────────
        // 내부 메서드
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_tmpText == null)
            {
                _tmpText = GetComponent<TextMeshProUGUI>();
            }
        }

        /// <summary>
        /// TMP Material이 외부에서 변경되었는지 확인
        /// </summary>
        private bool HasMaterialChanged()
        {
            if (_tmpText == null) return false;

            Material currentOriginal = _tmpText.fontSharedMaterial;
            if (currentOriginal != _originalSharedMaterial)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Glow Material 업데이트 (TMPMaterialCache 사용)
        /// </summary>
        private void UpdateGlowMaterial()
        {
            if (_tmpText == null || _tmpText.fontSharedMaterial == null)
            {
                return;
            }

            // 원본 Material 저장
            _originalSharedMaterial = _tmpText.fontSharedMaterial;

            // 변경사항이 있을 때만 업데이트 (최적화)
            if (_dirtyFlags == DirtyFlags.None && !_needsInitialization)
            {
                return;
            }

            // Material 캐시에서 가져오거나 생성
            _sharedMaterial = TMPMaterialCache.Instance.GetOrCreate(_originalSharedMaterial, this);

            if (_sharedMaterial == null)
            {
                Debug.LogWarning("[TMPOutGlow] Material 생성 실패", this);
                return;
            }

            // Material 속성 적용
            ApplyMaterialProperties(_sharedMaterial);

            // TMP에 Material 할당 (Direct Assignment 패턴)
            _tmpText.fontMaterial = _sharedMaterial;

            // TMP Tint color RGB 동기화 (Alpha는 유지)
            Color tintColor = _tmpText.color;
            _tmpText.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, tintColor.a);

            // 이전 값 저장 (다음 프레임 dirty check용)
            _prevGlowColor = _glowColor;
            _prevGlowRange = _glowRange;
            _prevFaceDilate = _faceDilate;

            // Dirty flag 초기화
            _dirtyFlags = DirtyFlags.None;
        }

        /// <summary>
        /// Material 속성 적용 (Shader Property)
        /// </summary>
        private void ApplyMaterialProperties(Material material)
        {
            if (material == null) return;

            // Underlay 설정 (Glow 파라미터 매핑)
            material.SetColor(TMPEffectManager.PropUnderlayColor, _glowColor);
            material.SetFloat(TMPEffectManager.PropUnderlayDilate, _glowRange);
            material.SetFloat(TMPEffectManager.PropUnderlaySoftness, 1f);  // 고정 (최대 블러)
            material.SetFloat(TMPEffectManager.PropUnderlayOffsetX, 0f);  // 고정
            material.SetFloat(TMPEffectManager.PropUnderlayOffsetY, 0f);  // 고정

            // Face 설정 (항상 활성화)
            material.SetFloat(TMPEffectManager.PropFaceDilate, _faceDilate);
        }

        /// <summary>
        /// 원본 Material 복원
        /// </summary>
        private void RestoreOriginalMaterial()
        {
            if (_tmpText != null && _originalSharedMaterial != null)
            {
                _tmpText.fontMaterial = _originalSharedMaterial;
            }

            _sharedMaterial = null;
        }

        // ─────────────────────────────────────────────
        // Inner Glow 관리 (자식 TMP 오브젝트)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Inner Glow 자식 오브젝트 생성
        /// </summary>
        private void CreateInnerGlowObject()
        {
            if (!_tmpText) return;
            if (_innerGlowObject) return;

            // 부모에 CanvasGroup이 있는지 미리 확인 (TMPAnimation 등이 추가한 경우)
            CanvasGroup parentCanvasGroup = GetComponent<CanvasGroup>();

            // 기존에 생성된 오브젝트 확인
            foreach (Transform child in transform)
            {
                if (child.name == "[Inner Glow]")
                {
                    _innerGlowObject = child.gameObject;
                    _innerGlowText = _innerGlowObject.GetComponent<TextMeshProUGUI>();
                    _innerGlowCurve = _innerGlowObject.GetComponent<TMPCurve>();

                    // 부모에 CanvasGroup이 있으면 자식의 CanvasGroup 제거 (중복 방지)
                    _innerGlowCanvasGroup = _innerGlowObject.GetComponent<CanvasGroup>();

                    if (parentCanvasGroup != null && _innerGlowCanvasGroup != null)
                    {
                        // 부모가 처리하므로 자식 CanvasGroup 제거
                        if (Application.isPlaying)
                            Destroy(_innerGlowCanvasGroup);
                        else
                            DestroyImmediate(_innerGlowCanvasGroup);
                        _innerGlowCanvasGroup = null;
                    }
                    else if (parentCanvasGroup == null && _innerGlowCanvasGroup == null)
                    {
                        // 부모도 없고 자식도 없으면 자식에 추가
                        _innerGlowCanvasGroup = _innerGlowObject.AddComponent<CanvasGroup>();
                        _innerGlowCanvasGroup.alpha = 1f;
                    }
                    else if (_innerGlowCanvasGroup != null)
                    {
                        _innerGlowCanvasGroup.alpha = 1f;  // 이미 존재하면 보이게
                    }

                    UpdateInnerGlowMaterial();
                    return;
                }
            }

            // 자식 GameObject 생성
            _innerGlowObject = new GameObject("[Inner Glow]");
            _innerGlowObject.hideFlags = HideFlags.NotEditable | HideFlags.DontSaveInEditor;
            _innerGlowObject.transform.SetParent(transform, false);

            // CanvasGroup 처리 (깜빡임 방지)
            // 부모에 CanvasGroup이 있으면 자식에 추가하지 않음 (부모의 alpha가 자식에 자동 적용됨)
            if (parentCanvasGroup == null)
            {
                // 부모에 CanvasGroup이 없으면 자식에 추가
                _innerGlowCanvasGroup = _innerGlowObject.AddComponent<CanvasGroup>();
                _innerGlowCanvasGroup.alpha = 0f;  // 첫 프레임에서 숨김
                _innerGlowNeedsShow = true;  // 다음 프레임에서 표시
            }
            // 부모에 CanvasGroup이 있으면 (TMPAnimation 등) 부모가 처리하므로 자식에 추가 안함

            // TextMeshProUGUI 추가
            _innerGlowText = _innerGlowObject.AddComponent<TextMeshProUGUI>();

            // RectTransform 설정 (부모를 완전히 채움)
            RectTransform parentRect = _tmpText.rectTransform;
            RectTransform childRect = _innerGlowText.rectTransform;

            // Anchor를 (0,0)~(1,1)로 설정하여 부모를 100% 채움
            // Content Size Fitter와 함께 사용할 때도 자동으로 따라감
            childRect.anchorMin = Vector2.zero;
            childRect.anchorMax = Vector2.one;
            childRect.anchoredPosition = Vector2.zero;
            childRect.sizeDelta = Vector2.zero;
            childRect.pivot = parentRect.pivot;
            childRect.localScale = Vector3.one;
            childRect.localRotation = Quaternion.identity;

            // TMP 속성 동기화
            SyncInnerGlow();

            // Raycast Target 비활성화
            _innerGlowText.raycastTarget = false;

            // TMPCurve 복사 (부모에 있는 경우)
            TMPCurve parentCurve = GetComponent<TMPCurve>();
            if (parentCurve)
            {
                _innerGlowCurve = _innerGlowObject.AddComponent<TMPCurve>();
                _innerGlowCurve.Curve = new AnimationCurve(parentCurve.Curve.keys);
                _innerGlowCurve.CurveScale = parentCurve.CurveScale;
                _innerGlowCurve.RotateAlongCurve = parentCurve.RotateAlongCurve;
                _innerGlowCurve.RotationStrength = parentCurve.RotationStrength;
            }

            // TMPAnimation 복사 (부모에 있는 경우)
            TMPAnimation parentAnimation = GetComponent<TMPAnimation>();
            if (parentAnimation)
            {
                TMPAnimation childAnimation = _innerGlowObject.AddComponent<TMPAnimation>();
                // Preset이 있으면 적용
                if (parentAnimation.Preset != null)
                {
                    childAnimation.Preset = parentAnimation.Preset;
                }
            }

            // Material 생성
            UpdateInnerGlowMaterial();
        }

        /// <summary>
        /// Inner Glow 자식 오브젝트 파괴
        /// </summary>
        private void DestroyInnerGlowObject()
        {
            var childrenToDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name == "[Inner Glow]")
                {
                    childrenToDestroy.Add(child.gameObject);
                }
            }

            foreach (var child in childrenToDestroy)
            {
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            _innerGlowObject = null;
            _innerGlowText = null;
            _innerGlowCurve = null;
            _innerGlowCanvasGroup = null;
            _innerGlowNeedsShow = false;
        }

        /// <summary>
        /// Inner Glow Material 업데이트
        /// </summary>
        private void UpdateInnerGlowMaterial()
        {
            if (!_innerGlowText || !_tmpText) return;

            // 원본 Material 기반으로 새 Material 생성
            Material baseMat = _tmpText.fontSharedMaterial;
            if (!baseMat) return;

            Material innerMat = new Material(baseMat);
            innerMat.name = $"{baseMat.name} (Inner Glow)";
            innerMat.hideFlags = HideFlags.DontSave;

            // Inner Glow 색상: GlowColor RGB + InnerGlowAlpha
            Color innerGlowColor = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _innerGlowAlpha);

            // Inner Glow Underlay 설정 (고정값 사용)
            innerMat.SetColor(TMPEffectManager.PropUnderlayColor, innerGlowColor);
            innerMat.SetFloat(TMPEffectManager.PropUnderlayDilate, 0f);  // 고정
            innerMat.SetFloat(TMPEffectManager.PropUnderlaySoftness, 0.5f);  // 고정
            innerMat.SetFloat(TMPEffectManager.PropUnderlayOffsetX, 0f);  // 고정
            innerMat.SetFloat(TMPEffectManager.PropUnderlayOffsetY, 0f);  // 고정

            // Face Dilate (고정)
            innerMat.SetFloat(TMPEffectManager.PropFaceDilate, 0f);

            // Material 적용
            _innerGlowText.fontMaterial = innerMat;
        }

        /// <summary>
        /// Inner Glow TMP 속성 동기화
        /// </summary>
        private void SyncInnerGlow()
        {
            if (!_innerGlowText || !_tmpText) return;

            _innerGlowText.text = _tmpText.text;
            _innerGlowText.font = _tmpText.font;
            _innerGlowText.fontSize = _tmpText.fontSize;
            _innerGlowText.fontStyle = _tmpText.fontStyle;
            _innerGlowText.alignment = _tmpText.alignment;
            _innerGlowText.characterSpacing = _tmpText.characterSpacing;
            _innerGlowText.wordSpacing = _tmpText.wordSpacing;
            _innerGlowText.lineSpacing = _tmpText.lineSpacing;
            _innerGlowText.paragraphSpacing = _tmpText.paragraphSpacing;
            _innerGlowText.overflowMode = _tmpText.overflowMode;
            _innerGlowText.enableWordWrapping = _tmpText.enableWordWrapping;
            _innerGlowText.horizontalMapping = _tmpText.horizontalMapping;
            _innerGlowText.verticalMapping = _tmpText.verticalMapping;
            _innerGlowText.margin = _tmpText.margin;
            _innerGlowText.enableAutoSizing = _tmpText.enableAutoSizing;
            _innerGlowText.fontSizeMin = _tmpText.fontSizeMin;
            _innerGlowText.fontSizeMax = _tmpText.fontSizeMax;
            _innerGlowText.richText = _tmpText.richText;
            _innerGlowText.parseCtrlCharacters = _tmpText.parseCtrlCharacters;
            _innerGlowText.isOrthographic = _tmpText.isOrthographic;

            // Color는 Inner Glow 색상 사용 (GlowColor RGB + InnerGlowAlpha)
            _innerGlowText.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _innerGlowAlpha);
        }
    }
}
