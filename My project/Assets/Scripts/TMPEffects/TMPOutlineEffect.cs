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
    public class TMPOutlineEffect : TMPEffect, IMeshModifier
    {
        // ─────────────────────────────────────────────
        // TMP Underlay Property IDs
        // ─────────────────────────────────────────────
        // Underlay는 Outline과 Shadow를 모두 처리하는 상위 기능
        // Offset (0,0) + Dilate > 0 = Outline
        // Offset (X,Y) ≠ 0 = Shadow/Drop Shadow

        private static readonly int PropUnderlayColor = Shader.PropertyToID("_UnderlayColor");
        private static readonly int PropUnderlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int PropUnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int PropUnderlayDilate = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int PropUnderlaySoftness = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int PropFaceDilate = Shader.PropertyToID("_FaceDilate");

        // ─────────────────────────────────────────────
        // Outline Properties
        // ─────────────────────────────────────────────

        [Header("Underlay Settings")]
        [SerializeField] private Color _underlayColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _underlayDilate = 0.15f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetX = 0f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetY = 0f;
        [SerializeField, Range(0f, 1f)] private float _underlaySoftness = 0.0f;

        [Header("Face Settings")]
        [SerializeField, Range(-1f, 1f)] private float _faceDilate = 0.0f;

        [Header("Shadow Settings")]
        [SerializeField] private bool _enableShadow = false;
        [SerializeField] private Vector2 _shadowOffset = new Vector2(0.1f, -0.1f);
        [SerializeField] private Color _shadowColor = new Color(0, 0, 0, 0.5f);

        // 더티 체크용 이전 값
        private Color _prevUnderlayColor;
        private float _prevUnderlayDilate;
        private float _prevUnderlayOffsetX;
        private float _prevUnderlayOffsetY;
        private float _prevUnderlaySoftness;
        private float _prevFaceDilate;
        private bool _prevEnableShadow;
        private Vector2 _prevShadowOffset;
        private Color _prevShadowColor;

        // Shadow 메시 캐시 (GC 제거)
        private static System.Collections.Generic.List<UIVertex> s_vertexCache = new System.Collections.Generic.List<UIVertex>(256);

        // Material 캐시
        private Material _outlineMaterial;
        private Material _originalSharedMaterial;  // 진짜 원본 Material (한 번만 캐싱)

        // TMP 컴포넌트 캐시
        private TextMeshProUGUI _tmpText;

        // 초기화 플래그
        private bool _needsInitialization = true;

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
                _enableShadow = value;
                SetVerticesDirty();  // Mesh 업데이트
            }
        }

        public Vector2 ShadowOffset
        {
            get => _shadowOffset;
            set
            {
                _shadowOffset = value;
                if (_enableShadow)
                    SetVerticesDirty();
            }
        }

        public Color ShadowColor
        {
            get => _shadowColor;
            set
            {
                _shadowColor = value;
                if (_enableShadow)
                    SetVerticesDirty();
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
                if (sharedMat && sharedMat != _outlineMaterial)
                {
                    _originalSharedMaterial = sharedMat;
                }
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
            _prevShadowColor = _shadowColor;

            // 초기화 플래그 설정 (LateUpdate에서 처리)
            _needsInitialization = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Material 정리
            CleanupMaterial();

            // TMP가 기본 Material로 자동 복원
        }

        private void OnDestroy()
        {
            CleanupMaterial();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Editor에서 값 변경 시 즉시 반영
            if (_tmpText != null)
            {
                UpdateOutlineMaterial();

                if (_enableShadow)
                {
                    SetVerticesDirty();
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

            // 더티 체크: Material 파라미터 변경 감지
            if (_underlayColor != _prevUnderlayColor ||
                _underlayDilate != _prevUnderlayDilate ||
                _underlayOffsetX != _prevUnderlayOffsetX ||
                _underlayOffsetY != _prevUnderlayOffsetY ||
                _underlaySoftness != _prevUnderlaySoftness ||
                _faceDilate != _prevFaceDilate)
            {
                _prevUnderlayColor = _underlayColor;
                _prevUnderlayDilate = _underlayDilate;
                _prevUnderlayOffsetX = _underlayOffsetX;
                _prevUnderlayOffsetY = _underlayOffsetY;
                _prevUnderlaySoftness = _underlaySoftness;
                _prevFaceDilate = _faceDilate;

                UpdateOutlineMaterial();
            }

            // 더티 체크: Shadow 파라미터 변경 감지
            if (_enableShadow != _prevEnableShadow ||
                (_enableShadow && (_shadowOffset != _prevShadowOffset || _shadowColor != _prevShadowColor)))
            {
                _prevEnableShadow = _enableShadow;
                _prevShadowOffset = _shadowOffset;
                _prevShadowColor = _shadowColor;

                SetVerticesDirty();  // Mesh 재생성
            }
        }

        // ─────────────────────────────────────────────
        // Material 관리
        // ─────────────────────────────────────────────

        /// <summary>
        /// Outline Material을 업데이트하고 TMP에 직접 할당
        /// - IMaterialModifier 대신 fontMaterial 직접 할당 방식 사용
        /// - TMP가 Material 변경을 감지하고 Quad를 자동 확장
        /// </summary>
        private void UpdateOutlineMaterial()
        {
            if (!_tmpText) return;

            // 원본 Material 확보 (최초 1회만 캐싱)
            if (!_originalSharedMaterial)
            {
                Material sharedMat = _tmpText.fontSharedMaterial;

                // fontSharedMaterial이 우리가 만든 Material일 수 있으므로 체크
                if (sharedMat && sharedMat != _outlineMaterial)
                {
                    _originalSharedMaterial = sharedMat;
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
                    // 아무것도 하지 않음
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

            // Material 인스턴스 생성 또는 재사용
            if (!_outlineMaterial)
            {
                _outlineMaterial = new Material(_originalSharedMaterial)
                {
                    name = $"{_originalSharedMaterial.name} (Outline)",
                    hideFlags = HideFlags.DontSave
                };
            }

            // Underlay Shader Keyword 활성화
            _outlineMaterial.EnableKeyword("UNDERLAY_ON");

            // Underlay 속성 설정
            // Offset (0,0) + Dilate > 0 = Outline 효과
            // Offset (X,Y) ≠ 0 = Shadow/Drop Shadow 효과
            _outlineMaterial.SetColor(PropUnderlayColor, _underlayColor);
            _outlineMaterial.SetFloat(PropUnderlayOffsetX, _underlayOffsetX);
            _outlineMaterial.SetFloat(PropUnderlayOffsetY, _underlayOffsetY);
            _outlineMaterial.SetFloat(PropUnderlayDilate, _underlayDilate);
            _outlineMaterial.SetFloat(PropUnderlaySoftness, _underlaySoftness);

            // Face Dilate 설정 (텍스트 본체 두께 조절)
            _outlineMaterial.SetFloat(PropFaceDilate, _faceDilate);

            // 🔑 핵심: TMP의 fontMaterial에 직접 할당
            // 이렇게 하면 TMP가 Material 변경을 감지하고 내부적으로 Quad를 재계산함
            _tmpText.fontMaterial = _outlineMaterial;

            // TMP에게 업데이트 강제
            _tmpText.UpdateMeshPadding();  // Padding 재계산
            _tmpText.ForceMeshUpdate();    // Mesh 재생성

#if UNITY_EDITOR
            // 디버그: Underlay 활성화 확인
            bool underlayEnabled = _outlineMaterial.IsKeywordEnabled("UNDERLAY_ON");
            if (!underlayEnabled)
            {
                Debug.LogError("[TMPOutlineEffect] ❌ UNDERLAY_ON keyword is NOT enabled!", this);
            }
#endif
        }

        private void CleanupMaterial()
        {
            if (_outlineMaterial)
            {
                // Material 참조 해제
                if (_tmpText && _tmpText.fontMaterial == _outlineMaterial)
                {
                    _tmpText.fontMaterial = null;
                }

                if (Application.isPlaying)
                {
                    Destroy(_outlineMaterial);
                }
                else
                {
                    DestroyImmediate(_outlineMaterial);
                }

                _outlineMaterial = null;
            }

            // 원본 Material 참조는 유지 (재사용 가능)
        }

        // ─────────────────────────────────────────────
        // IMeshModifier 구현 (Shadow용)
        // ─────────────────────────────────────────────

        public void ModifyMesh(VertexHelper vh)
        {
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

            for (int i = 0; i < originalCount; i++)
            {
                UIVertex shadowVertex = s_vertexCache[i];

                // 위치 오프셋 (fontSize 기준으로 스케일)
                shadowVertex.position += new Vector3(_shadowOffset.x * scale, _shadowOffset.y * scale, 0);

                // Shadow 색상
                shadowVertex.color = _shadowColor;

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

    }
}
