using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// TMP Underlay 효과 (Direct Material Assignment 방식)
    /// - TMP의 Underlay 기능 활용 (Outline + Shadow 통합)
    /// - Offset (0, 0) + Dilate > 0 = Outline 효과
    /// - Offset (X, Y) ≠ 0 = Shadow/Drop Shadow 효과
    /// - fontMaterial 직접 할당으로 TMP의 Quad 자동 확장 트리거
    /// - Material 인스턴스 생성으로 독립적 제어
    /// - 더티 체크로 불필요한 업데이트 스킵
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("CAT/UI/TMP Outline Effect")]
    public class TMPOutlineEffect : TMPEffect, IMeshModifier, ITMPEffectSettings
    {
        // ─────────────────────────────────────────────
        // TMP Underlay 개념
        // ─────────────────────────────────────────────
        // Underlay는 Outline과 Shadow를 모두 처리하는 상위 기능
        // Offset (0,0) + Dilate > 0 = Outline
        // Offset (X,Y) ≠ 0 = Shadow/Drop Shadow
        // Property IDs는 TMPEffectManager에서 관리

        // ─────────────────────────────────────────────
        // Preset
        // ─────────────────────────────────────────────

        [Header("Preset (Optional)")]
        [SerializeField] private TMPEffectPreset _preset;

        // ─────────────────────────────────────────────
        // Outline Properties (Header는 에디터에서 그림)
        // ─────────────────────────────────────────────

        [SerializeField] private Color _underlayColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _underlayDilate = 0.15f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetX = 0f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetY = 0f;
        [SerializeField, Range(0f, 1f)] private float _underlaySoftness = 0.0f;

        [SerializeField] private bool _enableFace = false;
        [SerializeField, Range(-1f, 1f)] private float _faceDilate = 0.0f;

        [SerializeField] private bool _enableShadow = false;
        [SerializeField] private Vector2 _shadowOffset = new Vector2(0.1f, -0.1f);
        [SerializeField, Range(0f, 1f)] private float _shadowAlpha = 0.5f;

        [Tooltip("안쪽으로 축소된 텍스트 활성화. 자식 TMP 오브젝트가 자동 생성됩니다.")]
        [SerializeField] private bool _enableSecondFace = false;
        [SerializeField] private Color _secondFaceColor = Color.white;
        [SerializeField, Range(-1f, 0f)] private float _secondFaceDilate = -0.1f;
        [SerializeField, Range(-1f, 1f)] private float _secondFaceOffsetX = 0f;
        [SerializeField, Range(-1f, 1f)] private float _secondFaceOffsetY = 0f;

        // ─────────────────────────────────────────────
        // 자식 TMP 오브젝트 (Second Face용)
        // ─────────────────────────────────────────────

        private GameObject _secondFaceObject;
        private TextMeshProUGUI _secondFaceText;
        private TMPCurve _secondFaceCurve;  // 부모에 TMPCurve가 있을 때 자식에도 추가

        // ─────────────────────────────────────────────
        // 더티 체크 최적화 (BitMask)
        // ─────────────────────────────────────────────

        [System.Flags]
        private enum DirtyFlags
        {
            None = 0,
            UnderlayColor = 1 << 0,
            UnderlayDilate = 1 << 1,
            UnderlayOffsetX = 1 << 2,
            UnderlayOffsetY = 1 << 3,
            UnderlaySoftness = 1 << 4,
            FaceDilate = 1 << 5,
            EnableShadow = 1 << 6,
            ShadowOffset = 1 << 7,
            ShadowColor = 1 << 8,
            Material = UnderlayColor | UnderlayDilate | UnderlayOffsetX | UnderlayOffsetY | UnderlaySoftness | FaceDilate,
            Shadow = EnableShadow | ShadowOffset | ShadowColor
        }

        private DirtyFlags _dirtyFlags = DirtyFlags.None;

        // 더티 체크용 이전 값 (Material 파라미터만)
        private Color _prevUnderlayColor;
        private float _prevUnderlayDilate;
        private float _prevUnderlayOffsetX;
        private float _prevUnderlayOffsetY;
        private float _prevUnderlaySoftness;
        private float _prevFaceDilate;
        private bool _prevEnableShadow;
        private Vector2 _prevShadowOffset;
        private float _prevShadowAlpha;

        // ─────────────────────────────────────────────
        // Static 캐시 최적화 (GC 제거)
        // ─────────────────────────────────────────────

        /// <summary>초기 정점 캐시 크기 (TMP 평균: 4 정점/글자 × 50글자 × 2배(Shadow) × 1.2(여유) = 512)</summary>
        private const int INITIAL_VERTEX_CACHE_SIZE = 512;

        /// <summary>Shadow 정점 배율 (원본 정점 × 2)</summary>
        private const int SHADOW_VERTEX_MULTIPLIER = 2;

        /// <summary>정점 캐시 (모든 인스턴스 공유, GC Alloc 없음)</summary>
        private static System.Collections.Generic.List<UIVertex> s_vertexCache = new System.Collections.Generic.List<UIVertex>(INITIAL_VERTEX_CACHE_SIZE);

        // Material 캐시
        private Material _sharedMaterial;
        private Material _originalSharedMaterial;

        // TMP 컴포넌트
        private TextMeshProUGUI _tmpText;

        // 초기화 플래그
        private bool _needsInitialization = true;

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
            UpdateOutlineMaterial();
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        public Color UnderlayColor
        {
            get => _underlayColor;
            set
            {
                _underlayColor = value;
                UpdateOutlineMaterial();
            }
        }

        public float UnderlayDilate
        {
            get => _underlayDilate;
            set
            {
                _underlayDilate = Mathf.Clamp01(value);
                UpdateOutlineMaterial();
            }
        }

        public float UnderlayOffsetX
        {
            get => _underlayOffsetX;
            set
            {
                _underlayOffsetX = Mathf.Clamp(value, -1f, 1f);
                UpdateOutlineMaterial();
            }
        }

        public float UnderlayOffsetY
        {
            get => _underlayOffsetY;
            set
            {
                _underlayOffsetY = Mathf.Clamp(value, -1f, 1f);
                UpdateOutlineMaterial();
            }
        }

        public float UnderlaySoftness
        {
            get => _underlaySoftness;
            set
            {
                _underlaySoftness = Mathf.Clamp01(value);
                UpdateOutlineMaterial();
            }
        }

        public bool EnableFace
        {
            get => _enableFace;
            set => _enableFace = value;
        }

        public float FaceDilate
        {
            get => _faceDilate;
            set
            {
                _faceDilate = Mathf.Clamp(value, -1f, 1f);
                UpdateOutlineMaterial();
            }
        }

        // ─────────────────────────────────────────────
        // 하위 호환성 API (Outline 명칭)
        // ─────────────────────────────────────────────

        [System.Obsolete("Use UnderlayDilate instead")]
        public float OutlineWidth
        {
            get => _underlayDilate;
            set => UnderlayDilate = value;
        }

        [System.Obsolete("Use UnderlayColor instead")]
        public Color OutlineColor
        {
            get => _underlayColor;
            set => UnderlayColor = value;
        }

        [System.Obsolete("Use UnderlaySoftness instead")]
        public float OutlineSoftness
        {
            get => _underlaySoftness;
            set => UnderlaySoftness = value;
        }

        // ─────────────────────────────────────────────
        // Shadow API
        // ─────────────────────────────────────────────

        public bool EnableShadow
        {
            get => _enableShadow;
            set
            {
                if (_enableShadow == value) return;

                _enableShadow = value;

                if (_tmpText != null)
                {
                    // TMP의 SetVerticesDirty를 직접 호출
                    _tmpText.SetVerticesDirty();

                    // 즉시 메시 재생성 (활성화/비활성화 모두)
                    _tmpText.ForceMeshUpdate();

#if UNITY_EDITOR
                    // 에디터에서 즉시 반영되도록 추가 업데이트
                    UnityEditor.EditorUtility.SetDirty(_tmpText);
#endif
                }
            }
        }

        public Vector2 ShadowOffset
        {
            get => _shadowOffset;
            set
            {
                _shadowOffset = value;
                if (_enableShadow && _tmpText != null)
                {
                    _tmpText.SetVerticesDirty();
                    _tmpText.ForceMeshUpdate();
                }
            }
        }

        public float ShadowAlpha
        {
            get => _shadowAlpha;
            set
            {
                _shadowAlpha = Mathf.Clamp01(value);
                if (_enableShadow && _tmpText != null)
                {
                    _tmpText.SetVerticesDirty();
                    _tmpText.ForceMeshUpdate();
                }
            }
        }

        // ─────────────────────────────────────────────
        // Second Face API
        // ─────────────────────────────────────────────

        public bool EnableSecondFace
        {
            get => _enableSecondFace;
            set
            {
                if (_enableSecondFace == value) return;

                _enableSecondFace = value;

                if (_enableSecondFace)
                {
                    CreateSecondFaceObject();
                }
                else
                {
                    DestroySecondFaceObject();
                }
            }
        }

        public Color SecondFaceColor
        {
            get => _secondFaceColor;
            set
            {
                _secondFaceColor = value;
                UpdateSecondFaceMaterial();
            }
        }

        public float SecondFaceDilate
        {
            get => _secondFaceDilate;
            set
            {
                _secondFaceDilate = Mathf.Clamp(value, -1f, 0f);
                UpdateSecondFaceMaterial();
            }
        }

        public float SecondFaceOffsetX
        {
            get => _secondFaceOffsetX;
            set
            {
                _secondFaceOffsetX = Mathf.Clamp(value, -1f, 1f);
                UpdateSecondFacePosition();
            }
        }

        public float SecondFaceOffsetY
        {
            get => _secondFaceOffsetY;
            set
            {
                _secondFaceOffsetY = Mathf.Clamp(value, -1f, 1f);
                UpdateSecondFacePosition();
            }
        }

        /// <summary>
        /// Second Face의 TMP_Text 컴포넌트 가져오기
        /// </summary>
        public TMP_Text GetSecondFaceText()
        {
            return _secondFaceText;
        }

        // ─────────────────────────────────────────────
        // ITMPEffectSettings 구현
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 공유를 위한 최적화된 해시 계산
        /// - FNV-1a 기반 충돌 최소화
        /// - Color는 RGBA32로 변환하여 정확한 비교
        /// - Float는 비트 패턴 직접 사용
        /// </summary>
        public int GetMaterialHash()
        {
            unchecked
            {
                const uint FNV_PRIME = 16777619;
                const uint FNV_OFFSET = 2166136261;
                uint hash = FNV_OFFSET;

                // Color → RGBA32 (정확한 비트 패턴)
                Color32 c = _underlayColor;
                hash = (hash ^ c.r) * FNV_PRIME;
                hash = (hash ^ c.g) * FNV_PRIME;
                hash = (hash ^ c.b) * FNV_PRIME;
                hash = (hash ^ c.a) * FNV_PRIME;

                // Float → int 비트 패턴 (올바른 변환)
                int dilate = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlayDilate), 0);
                int offsetX = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlayOffsetX), 0);
                int offsetY = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlayOffsetY), 0);
                int softness = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlaySoftness), 0);
                int faceDilate = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_faceDilate), 0);

                hash = (hash ^ (uint)dilate) * FNV_PRIME;
                hash = (hash ^ (uint)offsetX) * FNV_PRIME;
                hash = (hash ^ (uint)offsetY) * FNV_PRIME;
                hash = (hash ^ (uint)softness) * FNV_PRIME;
                hash = (hash ^ (uint)faceDilate) * FNV_PRIME;

                return (int)hash;
            }
        }

        // ─────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();

            // 컴포넌트 캐싱
            _tmpText = GetComponent<TextMeshProUGUI>();

            // 진짜 원본 Material 캐싱 (처음 한 번만)
            if (_tmpText && !_originalSharedMaterial)
            {
                // fontSharedMaterial이 우리가 만든 Material이 아닌지 확인
                Material sharedMat = _tmpText.fontSharedMaterial;
                if (sharedMat && sharedMat != _sharedMaterial)
                {
                    _originalSharedMaterial = sharedMat;
                }
            }

            // Preset이 설정되어 있으면 적용
            if (_preset != null)
            {
                _preset.ApplyTo(this);
            }

            // 초기 값 저장
            _prevUnderlayColor = _underlayColor;
            _prevUnderlayDilate = _underlayDilate;
            _prevUnderlayOffsetX = _underlayOffsetX;
            _prevUnderlayOffsetY = _underlayOffsetY;
            _prevUnderlaySoftness = _underlaySoftness;
            _prevFaceDilate = _faceDilate;
            _prevEnableShadow = _enableShadow;
            _prevShadowOffset = _shadowOffset;
            _prevShadowAlpha = _shadowAlpha;

            // Second Face 생성 (활성화된 경우)
            if (_enableSecondFace)
            {
                CreateSecondFaceObject();
            }

            // 초기화 플래그 설정 (LateUpdate에서 처리)
            _needsInitialization = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Material 정리
            CleanupMaterial();

            // Second Face 제거
            DestroySecondFaceObject();

            // TMP가 기본 Material로 자동 복원
        }

        private void OnDestroy()
        {
            CleanupMaterial();
            DestroySecondFaceObject();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Editor에서 값 변경 시 즉시 반영
            if (_tmpText != null)
            {
                UpdateOutlineMaterial();

                // Shadow 상태 변경 시 즉시 메시 업데이트
                _tmpText.SetVerticesDirty();
                _tmpText.ForceMeshUpdate();

                // Second Face 활성화/비활성화 처리
                if (_enableSecondFace)
                {
                    if (!_secondFaceObject)
                    {
                        CreateSecondFaceObject();
                    }
                    else
                    {
                        // 이미 존재하면 Material 업데이트
                        UpdateSecondFaceMaterial();
                    }
                }
                else
                {
                    if (_secondFaceObject)
                    {
                        DestroySecondFaceObject();
                    }
                }
            }
        }
#endif

        private void LateUpdate()
        {
            // 초기화 처리 (OnEnable 이후 첫 LateUpdate)
            if (_needsInitialization)
            {
                _needsInitialization = false;
                UpdateOutlineMaterial();
                if (_enableShadow)
                {
                    SetVerticesDirty();
                }
                return;
            }

            // 더티 체크: BitMask 기반 최적화
            CheckDirtyFlags();

            // Material 업데이트 필요 시
            if ((_dirtyFlags & DirtyFlags.Material) != 0)
            {
                UpdateOutlineMaterial();
            }

            // Shadow 업데이트 필요 시
            if ((_dirtyFlags & DirtyFlags.Shadow) != 0)
            {
                SetVerticesDirty();
            }

            // Second Face 동기화
            SyncSecondFace();

            // 더티 플래그 초기화
            _dirtyFlags = DirtyFlags.None;
        }

        /// <summary>
        /// 값 변경 감지 및 더티 플래그 설정 (BitMask 최적화)
        /// </summary>
        /// <remarks>
        /// 최적화 포인트:
        /// - 개별 bool 비교 대신 BitMask 연산 (DirtyFlags enum)
        /// - Material 관련 플래그: UnderlayColor | Dilate | Offset | Softness | FaceDilate
        /// - Shadow 관련 플래그: EnableShadow | ShadowOffset | ShadowColor
        /// - LateUpdate에서 플래그 그룹 체크 후 일괄 업데이트
        /// </remarks>
        private void CheckDirtyFlags()
        {
            if (_underlayColor != _prevUnderlayColor)
            {
                _dirtyFlags |= DirtyFlags.UnderlayColor;
                _prevUnderlayColor = _underlayColor;
            }

            if (_underlayDilate != _prevUnderlayDilate)
            {
                _dirtyFlags |= DirtyFlags.UnderlayDilate;
                _prevUnderlayDilate = _underlayDilate;
            }

            if (_underlayOffsetX != _prevUnderlayOffsetX)
            {
                _dirtyFlags |= DirtyFlags.UnderlayOffsetX;
                _prevUnderlayOffsetX = _underlayOffsetX;
            }

            if (_underlayOffsetY != _prevUnderlayOffsetY)
            {
                _dirtyFlags |= DirtyFlags.UnderlayOffsetY;
                _prevUnderlayOffsetY = _underlayOffsetY;
            }

            if (_underlaySoftness != _prevUnderlaySoftness)
            {
                _dirtyFlags |= DirtyFlags.UnderlaySoftness;
                _prevUnderlaySoftness = _underlaySoftness;
            }

            if (_faceDilate != _prevFaceDilate)
            {
                _dirtyFlags |= DirtyFlags.FaceDilate;
                _prevFaceDilate = _faceDilate;
            }

            if (_enableShadow != _prevEnableShadow)
            {
                _dirtyFlags |= DirtyFlags.EnableShadow;
                _prevEnableShadow = _enableShadow;
            }

            if (_enableShadow && _shadowOffset != _prevShadowOffset)
            {
                _dirtyFlags |= DirtyFlags.ShadowOffset;
                _prevShadowOffset = _shadowOffset;
            }

            if (_enableShadow && !Mathf.Approximately(_shadowAlpha, _prevShadowAlpha))
            {
                _dirtyFlags |= DirtyFlags.ShadowColor;
                _prevShadowAlpha = _shadowAlpha;
            }
        }

        // ─────────────────────────────────────────────
        // Material 관리
        // ─────────────────────────────────────────────

        /// <summary>
        /// Outline Material을 업데이트하고 TMP에 직접 할당
        /// </summary>
        /// <remarks>
        /// Direct Assignment 방식 (IMaterialModifier 대신):
        /// - fontMaterial 직접 할당 → TMP가 Material 변경 감지
        /// - UpdateMeshPadding() → Underlay 영역 포함하도록 Quad 확장
        /// - ForceMeshUpdate() → Mesh 즉시 재생성
        ///
        /// Material 공유 (TMPMaterialCache):
        /// - 같은 원본 + 같은 설정 = 동일 Material 공유
        /// - 100개 컴포넌트 → 5-10개 Material (메모리/Draw Call 감소)
        ///
        /// 순환 참조 방지:
        /// - _originalSharedMaterial: 최초 1회만 캐싱 (TMP 기본 Material)
        /// - _sharedMaterial: 현재 적용 중인 효과 Material
        /// - fontSharedMaterial이 _sharedMaterial인 경우 스킵 (자기 참조 방지)
        /// </remarks>
        private void UpdateOutlineMaterial()
        {
            if (!_tmpText) return;

            // 원본 Material 확보 (최초 1회만 캐싱)
            if (!_originalSharedMaterial)
            {
                Material sharedMat = _tmpText.fontSharedMaterial;

                // fontSharedMaterial이 우리가 만든 Material일 수 있으므로 체크
                if (sharedMat && sharedMat != _sharedMaterial)
                {
                    _originalSharedMaterial = sharedMat;

#if UNITY_EDITOR
                    // 디버깅: 원본 Material Shader 확인
                    Debug.Log($"[TMPOutlineEffect] Original Material:\n" +
                        $"- Name: {_originalSharedMaterial.name}\n" +
                        $"- Shader: {_originalSharedMaterial.shader.name}\n" +
                        $"- Has UNDERLAY_ON: {_originalSharedMaterial.shader.keywordSpace.FindKeyword("UNDERLAY_ON").isValid}", this);

                    // Underlay Property 존재 여부 확인
                    bool hasUnderlayProps =
                        _originalSharedMaterial.HasProperty("_UnderlayColor") &&
                        _originalSharedMaterial.HasProperty("_UnderlayDilate") &&
                        _originalSharedMaterial.HasProperty("_UnderlayOffsetX") &&
                        _originalSharedMaterial.HasProperty("_UnderlayOffsetY");

                    if (!hasUnderlayProps)
                    {
                        Debug.LogError($"[TMPOutlineEffect] 현재 Material({_originalSharedMaterial.shader.name})은 Underlay를 지원하지 않습니다!\n" +
                            $"TMP Font Asset에서 'TextMeshPro/Distance Field' 또는 'TextMeshPro/Mobile/Distance Field' Shader를 사용하는 Material을 설정해주세요.", this);
                    }
#endif
                }
                else if (!sharedMat)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[TMPOutlineEffect] fontSharedMaterial is null. Waiting for TMP initialization.", this);
#endif
                    return;
                }
                else
                {
                    // fontSharedMaterial이 우리 Material인 경우 - 이미 적용된 상태
                    return;
                }
            }

            // 원본 Material이 유효한지 확인
            if (!_originalSharedMaterial)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[TMPOutlineEffect] Original shared material is not available yet.", this);
#endif
                return;
            }

            // 🔑 핵심: TMPMaterialCache를 통한 Material 공유
            // 같은 설정을 가진 효과들이 동일한 Material을 공유함
            Material newSharedMaterial = TMPMaterialCache.Instance.GetOrCreate(
                _originalSharedMaterial,
                this
            );

            if (!newSharedMaterial)
            {
#if UNITY_EDITOR
                Debug.LogError("[TMPOutlineEffect] Failed to get shared material!", this);
#endif
                return;
            }

            // Material이 변경되었을 때만 TMP에 할당
            if (_sharedMaterial != newSharedMaterial)
            {
                _sharedMaterial = newSharedMaterial;

                // TMP의 fontMaterial에 할당
                _tmpText.fontMaterial = _sharedMaterial;

#if UNITY_EDITOR
                // 디버깅: Material 정보 출력
                Debug.Log($"[TMPOutlineEffect] Material Updated:\n" +
                    $"- Shader: {_sharedMaterial.shader.name}\n" +
                    $"- UNDERLAY_ON: {_sharedMaterial.IsKeywordEnabled("UNDERLAY_ON")}\n" +
                    $"- UnderlayColor: {_sharedMaterial.GetColor(TMPEffectManager.PropUnderlayColor)}\n" +
                    $"- UnderlayDilate: {_sharedMaterial.GetFloat(TMPEffectManager.PropUnderlayDilate)}\n" +
                    $"- UnderlayOffsetX: {_sharedMaterial.GetFloat(TMPEffectManager.PropUnderlayOffsetX)}\n" +
                    $"- UnderlayOffsetY: {_sharedMaterial.GetFloat(TMPEffectManager.PropUnderlayOffsetY)}");
#endif

                // TMP 업데이트 강제
                _tmpText.UpdateMeshPadding();  // Padding 재계산
                _tmpText.ForceMeshUpdate();    // Mesh 재생성
            }
        }

        private void CleanupMaterial()
        {
            // 공유 Material은 TMPEffectManager가 관리하므로 파괴하지 않음
            // TMP 참조만 해제
            if (_tmpText && _sharedMaterial && _tmpText.fontMaterial == _sharedMaterial)
            {
                _tmpText.fontMaterial = null;
            }

            _sharedMaterial = null;
            // 원본 Material 참조는 유지 (재사용 가능)
        }

        // ─────────────────────────────────────────────
        // Second Face 관리
        // ─────────────────────────────────────────────

        /// <summary>
        /// Second Face용 자식 TMP 오브젝트 생성
        /// - 자식 GameObject 생성 (이름: "[Inner Face]")
        /// - TextMeshProUGUI 컴포넌트 추가
        /// - HideFlags.DontSaveInEditor 설정
        /// - 부모 텍스트 내용 복사
        /// - Face Dilate < 0인 Material 생성 및 적용
        /// - Raycast Target 비활성화
        /// </summary>
        private void CreateSecondFaceObject()
        {
            if (!_tmpText) return;
            if (_secondFaceObject) return; // 이미 존재하면 스킵

            // 기존에 생성된 [Inner Face] 오브젝트가 있는지 확인 (중복 생성 방지)
            foreach (Transform child in transform)
            {
                if (child.name == "[Inner Face]")
                {
                    _secondFaceObject = child.gameObject;
                    _secondFaceText = _secondFaceObject.GetComponent<TextMeshProUGUI>();
                    _secondFaceCurve = _secondFaceObject.GetComponent<TMPCurve>();
                    Debug.Log($"[TMPOutlineEffect] 기존 Inner Face 오브젝트 재사용: {_secondFaceObject.name}", this);
                    UpdateSecondFaceMaterial();
                    return;
                }
            }

            // 자식 GameObject 생성
            _secondFaceObject = new GameObject("[Inner Face]");
            _secondFaceObject.hideFlags = HideFlags.DontSaveInEditor;
            _secondFaceObject.transform.SetParent(transform, false);

            // TextMeshProUGUI 컴포넌트 추가
            _secondFaceText = _secondFaceObject.AddComponent<TextMeshProUGUI>();

            // RectTransform 복사 (부모와 완전히 겹치도록)
            RectTransform parentRect = _tmpText.rectTransform;
            RectTransform childRect = _secondFaceText.rectTransform;

            childRect.anchorMin = parentRect.anchorMin;
            childRect.anchorMax = parentRect.anchorMax;
            childRect.anchoredPosition3D = Vector3.zero;  // 부모와 완전히 겹침
            childRect.sizeDelta = parentRect.sizeDelta;
            childRect.pivot = parentRect.pivot;
            childRect.localScale = Vector3.one;  // 부모의 스케일은 이미 반영되므로 1로 설정
            childRect.localRotation = Quaternion.identity;  // 부모의 회전은 이미 반영되므로 0으로 설정

            // TMP 속성 복사 (모든 중요 속성 동기화)
            _secondFaceText.text = _tmpText.text;
            _secondFaceText.font = _tmpText.font;
            _secondFaceText.fontSize = _tmpText.fontSize;
            _secondFaceText.fontStyle = _tmpText.fontStyle;

            // Alignment
            _secondFaceText.alignment = _tmpText.alignment;

            // Spacing
            _secondFaceText.characterSpacing = _tmpText.characterSpacing;
            _secondFaceText.wordSpacing = _tmpText.wordSpacing;
            _secondFaceText.lineSpacing = _tmpText.lineSpacing;
            _secondFaceText.paragraphSpacing = _tmpText.paragraphSpacing;

            // Overflow & Wrapping
            _secondFaceText.overflowMode = _tmpText.overflowMode;
            _secondFaceText.enableWordWrapping = _tmpText.enableWordWrapping;
            _secondFaceText.horizontalMapping = _tmpText.horizontalMapping;
            _secondFaceText.verticalMapping = _tmpText.verticalMapping;

            // Margin
            _secondFaceText.margin = _tmpText.margin;

            // Auto Sizing
            _secondFaceText.enableAutoSizing = _tmpText.enableAutoSizing;
            _secondFaceText.fontSizeMin = _tmpText.fontSizeMin;
            _secondFaceText.fontSizeMax = _tmpText.fontSizeMax;

            // Extra Settings
            _secondFaceText.richText = _tmpText.richText;
            _secondFaceText.parseCtrlCharacters = _tmpText.parseCtrlCharacters;
            _secondFaceText.isOrthographic = _tmpText.isOrthographic;

            // Raycast Target 비활성화 (인터랙션 방지)
            _secondFaceText.raycastTarget = false;

            // TMPCurve 컴포넌트 복사 (부모에 있는 경우)
            TMPCurve parentCurve = GetComponent<TMPCurve>();
            if (parentCurve)
            {
                _secondFaceCurve = _secondFaceObject.AddComponent<TMPCurve>();
                _secondFaceCurve.Curve = new AnimationCurve(parentCurve.Curve.keys);  // Deep copy
                _secondFaceCurve.CurveScale = parentCurve.CurveScale;
                _secondFaceCurve.RotateAlongCurve = parentCurve.RotateAlongCurve;
                _secondFaceCurve.RotationStrength = parentCurve.RotationStrength;
            }

            // Material 생성 및 적용
            UpdateSecondFaceMaterial();

#if UNITY_EDITOR
            Debug.Log($"[TMPOutlineEffect] Second Face 생성됨: {_secondFaceObject.name}", this);
#endif
        }

        /// <summary>
        /// Second Face 자식 오브젝트 파괴
        /// </summary>
        private void DestroySecondFaceObject()
        {
            // 모든 [Inner Face] 자식 찾아서 파괴 (중복 생성된 것들도 제거)
            var childrenToDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name == "[Inner Face]")
                {
                    childrenToDestroy.Add(child.gameObject);
                }
            }

            if (childrenToDestroy.Count == 0 && _secondFaceObject == null) return;

            Debug.Log($"[TMPOutlineEffect] Second Face 파괴: {childrenToDestroy.Count}개", this);

#if UNITY_EDITOR
            // Edit Mode에서는 EditorApplication.delayCall로 지연 파괴
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    foreach (var obj in childrenToDestroy)
                    {
                        if (obj != null)
                        {
                            DestroyImmediate(obj);
                        }
                    }
                };
            }
            else
            {
                foreach (var obj in childrenToDestroy)
                {
                    if (obj != null)
                    {
                        Destroy(obj);
                    }
                }
            }
#else
            foreach (var obj in childrenToDestroy)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
#endif

            _secondFaceObject = null;
            _secondFaceText = null;
            _secondFaceCurve = null;
        }

        /// <summary>
        /// Second Face Material 생성 및 업데이트
        /// - Face Dilate를 _secondFaceDilate (< 0) 값으로 설정하여 안쪽으로 축소
        /// - Face Color를 _secondFaceColor로 설정
        /// - Underlay는 비활성화 (외곽선 없이 순수 텍스트만)
        /// </summary>
        private void UpdateSecondFaceMaterial()
        {
            if (!_secondFaceText) return;
            if (!_tmpText) return;

            // 원본 Material 확보
            Material baseMaterial = _originalSharedMaterial;
            if (!baseMaterial)
            {
                baseMaterial = _tmpText.fontSharedMaterial;
                if (!baseMaterial)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[TMPOutlineEffect] fontSharedMaterial이 없습니다. Second Face Material을 생성할 수 없습니다.", this);
#endif
                    return;
                }
            }

            // 원본 Material 기반으로 새 Material 인스턴스 생성
            Material secondFaceMat = new Material(baseMaterial)
            {
                name = $"{baseMaterial.name} (Second Face)",
                hideFlags = HideFlags.DontSave
            };

            // Face Dilate 설정 (음수로 안쪽 축소)
            secondFaceMat.SetFloat(TMPEffectManager.PropFaceDilate, _secondFaceDilate);

            // Face Color 설정
            secondFaceMat.SetColor(TMPEffectManager.PropFaceColor, _secondFaceColor);

            // Underlay 설정 (정점 확장을 위해 활성화, 하지만 투명하게)
            // ⚠️ 중요: TMPCurve와 같은 메시 수정 컴포넌트와 함께 사용할 때,
            // 부모와 자식의 정점 위치를 일치시키기 위해 Underlay를 동일하게 설정해야 함
            secondFaceMat.EnableKeyword("UNDERLAY_ON");

            // Underlay를 부모와 동일하게 설정하되 알파=0 (정점만 확장, 렌더링 안 됨)
            Color underlayColor = _underlayColor;
            underlayColor.a = 0f;  // 투명하게 설정
            secondFaceMat.SetColor(TMPEffectManager.PropUnderlayColor, underlayColor);
            secondFaceMat.SetFloat(TMPEffectManager.PropUnderlayDilate, _underlayDilate);
            secondFaceMat.SetFloat(TMPEffectManager.PropUnderlayOffsetX, _underlayOffsetX);
            secondFaceMat.SetFloat(TMPEffectManager.PropUnderlayOffsetY, _underlayOffsetY);
            secondFaceMat.SetFloat(TMPEffectManager.PropUnderlaySoftness, _underlaySoftness);

            // Material 적용
            _secondFaceText.fontMaterial = secondFaceMat;

            // TMP 업데이트 강제
            _secondFaceText.ForceMeshUpdate();

#if UNITY_EDITOR
            Debug.Log($"[TMPOutlineEffect] Second Face Material 업데이트:\n" +
                $"- Face Dilate: {_secondFaceDilate}\n" +
                $"- Face Color: {_secondFaceColor}", this);
#endif

            // 위치 오프셋 적용
            UpdateSecondFacePosition();
        }

        /// <summary>
        /// Second Face 위치 오프셋 업데이트
        /// </summary>
        private void UpdateSecondFacePosition()
        {
            if (!_secondFaceText) return;
            if (!_tmpText) return;

            RectTransform childRect = _secondFaceText.rectTransform;
            float scale = _tmpText.fontSize;

            // Offset 적용 (fontSize 기준으로 스케일)
            childRect.anchoredPosition = new Vector2(
                _secondFaceOffsetX * scale,
                _secondFaceOffsetY * scale
            );
        }

        /// <summary>
        /// Second Face 텍스트를 부모와 동기화
        /// - 텍스트 내용, 폰트, 크기, 스타일, 정렬 등 모든 속성 동기화
        /// - RectTransform 동기화 (레이아웃 변경 대응)
        /// - LateUpdate에서 매 프레임 호출
        /// </summary>
        private void SyncSecondFace()
        {
            if (!_enableSecondFace) return;
            if (!_secondFaceText) return;
            if (!_tmpText) return;

            // 텍스트 내용 동기화
            if (_secondFaceText.text != _tmpText.text)
                _secondFaceText.text = _tmpText.text;

            // 폰트 동기화
            if (_secondFaceText.font != _tmpText.font)
                _secondFaceText.font = _tmpText.font;

            // 폰트 크기 및 스타일 동기화
            if (_secondFaceText.fontSize != _tmpText.fontSize)
                _secondFaceText.fontSize = _tmpText.fontSize;

            if (_secondFaceText.fontStyle != _tmpText.fontStyle)
                _secondFaceText.fontStyle = _tmpText.fontStyle;

            // Alignment 동기화
            if (_secondFaceText.alignment != _tmpText.alignment)
                _secondFaceText.alignment = _tmpText.alignment;

            // Spacing 동기화
            if (_secondFaceText.characterSpacing != _tmpText.characterSpacing)
                _secondFaceText.characterSpacing = _tmpText.characterSpacing;

            if (_secondFaceText.wordSpacing != _tmpText.wordSpacing)
                _secondFaceText.wordSpacing = _tmpText.wordSpacing;

            if (_secondFaceText.lineSpacing != _tmpText.lineSpacing)
                _secondFaceText.lineSpacing = _tmpText.lineSpacing;

            if (_secondFaceText.paragraphSpacing != _tmpText.paragraphSpacing)
                _secondFaceText.paragraphSpacing = _tmpText.paragraphSpacing;

            // Overflow & Wrapping 동기화
            if (_secondFaceText.overflowMode != _tmpText.overflowMode)
                _secondFaceText.overflowMode = _tmpText.overflowMode;

            if (_secondFaceText.enableWordWrapping != _tmpText.enableWordWrapping)
                _secondFaceText.enableWordWrapping = _tmpText.enableWordWrapping;

            if (_secondFaceText.horizontalMapping != _tmpText.horizontalMapping)
                _secondFaceText.horizontalMapping = _tmpText.horizontalMapping;

            if (_secondFaceText.verticalMapping != _tmpText.verticalMapping)
                _secondFaceText.verticalMapping = _tmpText.verticalMapping;

            // Margin 동기화
            if (_secondFaceText.margin != _tmpText.margin)
                _secondFaceText.margin = _tmpText.margin;

            // Auto Sizing 동기화
            if (_secondFaceText.enableAutoSizing != _tmpText.enableAutoSizing)
            {
                _secondFaceText.enableAutoSizing = _tmpText.enableAutoSizing;
                _secondFaceText.fontSizeMin = _tmpText.fontSizeMin;
                _secondFaceText.fontSizeMax = _tmpText.fontSizeMax;
            }

            // Extra Settings 동기화
            if (_secondFaceText.richText != _tmpText.richText)
                _secondFaceText.richText = _tmpText.richText;

            if (_secondFaceText.parseCtrlCharacters != _tmpText.parseCtrlCharacters)
                _secondFaceText.parseCtrlCharacters = _tmpText.parseCtrlCharacters;

            // RectTransform 동기화 (레이아웃 변경 대응)
            RectTransform parentRect = _tmpText.rectTransform;
            RectTransform childRect = _secondFaceText.rectTransform;

            // Anchor 동기화 (부모 레이아웃 변경 대응)
            if (childRect.anchorMin != parentRect.anchorMin)
                childRect.anchorMin = parentRect.anchorMin;

            if (childRect.anchorMax != parentRect.anchorMax)
                childRect.anchorMax = parentRect.anchorMax;

            // 크기 동기화 (Content Size Fitter 대응)
            if (childRect.sizeDelta != parentRect.sizeDelta)
                childRect.sizeDelta = parentRect.sizeDelta;

            // Pivot 동기화
            if (childRect.pivot != parentRect.pivot)
                childRect.pivot = parentRect.pivot;

            // TMPCurve 동기화 (부모에 있는 경우)
            TMPCurve parentCurve = GetComponent<TMPCurve>();
            if (parentCurve)
            {
                // 부모에 TMPCurve가 있는데 자식에 없으면 추가
                if (!_secondFaceCurve)
                {
                    _secondFaceCurve = _secondFaceObject.AddComponent<TMPCurve>();
                }

                // 설정 동기화
                if (_secondFaceCurve.CurveScale != parentCurve.CurveScale)
                    _secondFaceCurve.CurveScale = parentCurve.CurveScale;

                if (_secondFaceCurve.RotateAlongCurve != parentCurve.RotateAlongCurve)
                    _secondFaceCurve.RotateAlongCurve = parentCurve.RotateAlongCurve;

                if (_secondFaceCurve.RotationStrength != parentCurve.RotationStrength)
                    _secondFaceCurve.RotationStrength = parentCurve.RotationStrength;

                // Curve 키프레임이 다르면 복사 (Deep copy)
                if (!AreCurvesEqual(_secondFaceCurve.Curve, parentCurve.Curve))
                    _secondFaceCurve.Curve = new AnimationCurve(parentCurve.Curve.keys);
            }
            else
            {
                // 부모에 TMPCurve가 없는데 자식에 있으면 제거
                if (_secondFaceCurve)
                {
#if UNITY_EDITOR
                    DestroyImmediate(_secondFaceCurve);
#else
                    Destroy(_secondFaceCurve);
#endif
                    _secondFaceCurve = null;
                }
            }
        }

        /// <summary>
        /// 두 AnimationCurve가 동일한지 비교 (키프레임 개수와 값 비교)
        /// </summary>
        private bool AreCurvesEqual(AnimationCurve curve1, AnimationCurve curve2)
        {
            if (curve1 == null || curve2 == null)
                return curve1 == curve2;

            if (curve1.length != curve2.length)
                return false;

            for (int i = 0; i < curve1.length; i++)
            {
                Keyframe k1 = curve1.keys[i];
                Keyframe k2 = curve2.keys[i];

                if (!Mathf.Approximately(k1.time, k2.time) ||
                    !Mathf.Approximately(k1.value, k2.value) ||
                    !Mathf.Approximately(k1.inTangent, k2.inTangent) ||
                    !Mathf.Approximately(k1.outTangent, k2.outTangent))
                {
                    return false;
                }
            }

            return true;
        }

        // ─────────────────────────────────────────────
        // IMeshModifier 구현 (Shadow용)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Shadow 효과를 위한 정점 메시 수정
        /// </summary>
        /// <param name="vh">Unity VertexHelper (TMP가 생성한 원본 메시)</param>
        /// <remarks>
        /// 구현 방식 (GPU 기반 Outline + CPU 기반 Shadow):
        /// - Outline: TMP Underlay (GPU 셰이더, Offset = 0)
        /// - Shadow: 정점 복제 (CPU 메시 수정, Offset ≠ 0)
        ///
        /// 정점 복제 알고리즘:
        /// 1. 원본 정점 가져오기 (4 정점/글자)
        /// 2. Shadow 레이어 생성 (위치 오프셋 + Underlay Color 기반)
        /// 3. InsertRange(0, shadow) → 원본 앞에 삽입 (먼저 그려지게)
        /// 4. 최종 정점 수 = 원본 × 2
        ///
        /// 최적화:
        /// - static 캐시 재사용 (GC Alloc 없음)
        /// - fontSize 기준 스케일 (Canvas 해상도 대응)
        /// - 활성화/비활성화 시에만 메시 재생성
        /// </remarks>
        public void ModifyMesh(VertexHelper vh)
        {
            // Shadow 비활성화 시 원본 메시 유지 (아무 수정도 하지 않음)
            if (!isActiveAndEnabled || !_enableShadow)
            {
                return;
            }

            // 빈 메시는 스킵
            if (vh.currentVertCount == 0)
            {
                return;
            }

            // UIVertex 리스트 가져오기 (static 캐시 재사용)
            s_vertexCache.Clear();
            vh.GetUIVertexStream(s_vertexCache);

            int originalCount = s_vertexCache.Count;

            // Shadow 레이어를 원본 앞에 삽입 (먼저 그려지게)
            // 임시 리스트에 Shadow 정점 생성
            var shadowVertices = new System.Collections.Generic.List<UIVertex>(originalCount);

            float scale = _tmpText != null ? _tmpText.fontSize : 1f;

            // Shadow 색상 계산 (Underlay Color 기반, Alpha만 별도 제어)
            Color32 shadowColor = new Color32(
                (byte)(_underlayColor.r * 255f),
                (byte)(_underlayColor.g * 255f),
                (byte)(_underlayColor.b * 255f),
                (byte)(_underlayColor.a * _shadowAlpha * 255f)
            );

            for (int i = 0; i < originalCount; i++)
            {
                UIVertex shadowVertex = s_vertexCache[i];

                // 위치 오프셋 (fontSize 기준으로 스케일)
                shadowVertex.position += new Vector3(_shadowOffset.x * scale, _shadowOffset.y * scale, 0);

                // Shadow 색상 적용 (Underlay Color + Alpha 조절)
                shadowVertex.color = shadowColor;

                shadowVertices.Add(shadowVertex);
            }

            // Shadow 정점을 앞에, 원본 정점을 뒤에 배치
            s_vertexCache.InsertRange(0, shadowVertices);

            // 메시 재구성
            vh.Clear();
            vh.AddUIVertexTriangleStream(s_vertexCache);
        }

        public void ModifyMesh(Mesh mesh)
        {
            // Unity 2022+ compatibility
            // 이 메서드는 더 이상 사용되지 않지만 인터페이스 호환성을 위해 유지
        }

        // ─────────────────────────────────────────────
        // Runtime API 개선 (편의 메서드)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 간단한 Outline 효과 설정 (Offset = 0)
        /// </summary>
        /// <param name="color">Outline 색상</param>
        /// <param name="width">Outline 두께 (0~1)</param>
        /// <param name="softness">부드러움 (0~1, 기본값 0)</param>
        public void SetOutline(Color color, float width, float softness = 0f)
        {
            UnderlayColor = color;
            UnderlayDilate = width;
            UnderlayOffsetX = 0f;
            UnderlayOffsetY = 0f;
            UnderlaySoftness = softness;
            EnableShadow = false;
        }

        /// <summary>
        /// 간단한 Drop Shadow 효과 설정 (Offset ≠ 0)
        /// </summary>
        /// <param name="color">Shadow 색상</param>
        /// <param name="offsetX">X 오프셋 (-1~1)</param>
        /// <param name="offsetY">Y 오프셋 (-1~1)</param>
        /// <param name="dilate">Shadow 두께 (0~1, 기본값 0.1)</param>
        public void SetDropShadow(Color color, float offsetX, float offsetY, float dilate = 0.1f)
        {
            UnderlayColor = color;
            UnderlayDilate = dilate;
            UnderlayOffsetX = offsetX;
            UnderlayOffsetY = offsetY;
            UnderlaySoftness = 0f;
            EnableShadow = false;
        }

        /// <summary>
        /// Outline + Shadow 복합 효과 설정
        /// </summary>
        /// <param name="outlineColor">Outline 색상</param>
        /// <param name="outlineWidth">Outline 두께 (0~1)</param>
        /// <param name="shadowAlpha">Shadow 알파값 (0~1)</param>
        /// <param name="shadowOffset">Shadow 오프셋</param>
        public void SetOutlineWithShadow(
            Color outlineColor,
            float outlineWidth,
            float shadowAlpha,
            Vector2 shadowOffset)
        {
            // Outline (GPU)
            UnderlayColor = outlineColor;
            UnderlayDilate = outlineWidth;
            UnderlayOffsetX = 0f;
            UnderlayOffsetY = 0f;
            UnderlaySoftness = 0f;

            // Shadow (CPU)
            EnableShadow = true;
            ShadowAlpha = shadowAlpha;
            ShadowOffset = shadowOffset;
        }

        /// <summary>
        /// 효과 초기화 (모든 효과 제거)
        /// </summary>
        public void ResetEffect()
        {
            UnderlayColor = Color.black;
            UnderlayDilate = 0f;
            UnderlayOffsetX = 0f;
            UnderlayOffsetY = 0f;
            UnderlaySoftness = 0f;
            FaceDilate = 0f;
            EnableShadow = false;
            ShadowOffset = Vector2.zero;
            ShadowAlpha = 0f;
        }

        /// <summary>
        /// Material 캐시 통계 가져오기 (디버깅용)
        /// </summary>
        /// <returns>캐시 통계 정보</returns>
        public static TMPMaterialCache.CacheStats GetCacheStats()
        {
            return TMPMaterialCache.Instance.GetStats();
        }

        /// <summary>
        /// Material 캐시 초기화 (디버깅용)
        /// </summary>
        [ContextMenu("Clear Material Cache")]
        public static void ClearMaterialCache()
        {
            TMPMaterialCache.Instance.Clear();
#if UNITY_EDITOR
            Debug.Log("[TMPOutlineEffect] Material cache cleared!");
#endif
        }

    }
}
