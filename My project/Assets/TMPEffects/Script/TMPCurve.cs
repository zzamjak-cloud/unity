using UnityEngine;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// TMP 텍스트 곡선 효과
    /// - AnimationCurve를 따라 텍스트 정점을 변형
    /// - TMP 이벤트 기반으로 텍스트 변경 즉시 곡선 적용 (깜빡임 방지)
    /// - LateUpdate에서 설정 변경 감지 및 추가 업데이트
    /// - 모바일 최적화: 불필요한 업데이트 스킵
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("CAT/UI/TMP Curve")]
    public class TMPCurve : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector 설정
        // ─────────────────────────────────────────────

        [Header("Curve Settings")]
        [Tooltip("텍스트가 따라갈 곡선. X축은 텍스트 위치(0~1), Y축은 높이 오프셋")]
        [SerializeField]
        private AnimationCurve _curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );

        [Tooltip("곡선의 수직 스케일 (픽셀 단위)")]
        [SerializeField]
        private float _curveScale = 50f;

        [Header("Rotation Settings")]
        [Tooltip("글자가 곡선의 접선 방향을 따라 회전할지 여부")]
        [SerializeField]
        private bool _rotateAlongCurve = true;

        [Tooltip("회전 강도 (0 = 회전 없음, 1 = 완전 회전)")]
        [SerializeField, Range(0f, 1f)]
        private float _rotationStrength = 1f;

        // ─────────────────────────────────────────────
        // 캐싱
        // ─────────────────────────────────────────────

        private TMP_Text _tmpText;
        private RectTransform _rectTransform;
        private bool _isDirty = true;
        private bool _forceUpdateNextFrame = false;
        private float _previousCurveScale;
        private bool _previousRotateAlongCurve;
        private float _previousRotationStrength;

        // RectTransform 크기 변경 감지용
        private Vector2 _previousRectSize;

        // Curve 키프레임 해시 (변경 감지용)
        private int _previousCurveHash;

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        /// <summary>
        /// 텍스트가 따라갈 곡선
        /// </summary>
        public AnimationCurve Curve
        {
            get => _curve;
            set
            {
                _curve = value;
                SetDirty();
            }
        }

        /// <summary>
        /// 곡선의 수직 스케일 (픽셀 단위)
        /// </summary>
        public float CurveScale
        {
            get => _curveScale;
            set
            {
                _curveScale = value;
                SetDirty();
            }
        }

        /// <summary>
        /// 글자가 곡선의 접선 방향을 따라 회전할지 여부
        /// </summary>
        public bool RotateAlongCurve
        {
            get => _rotateAlongCurve;
            set
            {
                _rotateAlongCurve = value;
                SetDirty();
            }
        }

        /// <summary>
        /// 회전 강도 (0~1)
        /// </summary>
        public float RotationStrength
        {
            get => _rotationStrength;
            set
            {
                _rotationStrength = Mathf.Clamp01(value);
                SetDirty();
            }
        }

        // ─────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();

            // TMP 텍스트 변경 이벤트 구독 (깜빡임 방지의 핵심)
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

            _forceUpdateNextFrame = true;
            SetDirty();
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

            // 비활성화 시 원본 메시로 복원
            if (_tmpText != null)
            {
                _tmpText.ForceMeshUpdate();
            }
        }

        /// <summary>
        /// TMP 텍스트 변경 이벤트 핸들러
        /// - TMP가 메시를 업데이트한 직후 호출됨
        /// - 이 시점에 곡선을 적용해야 깜빡임이 없음
        /// </summary>
        private void OnTextChanged(Object obj)
        {
            // 이 컴포넌트의 TMP인지 확인
            if (obj == _tmpText)
            {
                ApplyCurveToMesh();
            }
        }

        private void LateUpdate()
        {
            if (_tmpText == null) return;

            // 강제 업데이트 플래그 (OnEnable 직후)
            if (_forceUpdateNextFrame)
            {
                _forceUpdateNextFrame = false;
                _tmpText.ForceMeshUpdate();
                // OnTextChanged가 호출되어 곡선 적용됨
                _isDirty = false;
                return;
            }

            // 설정 변경 감지 (Inspector 변경 등)
            CheckSettingsDirty();

            // RectTransform 크기 변경 감지
            CheckRectSizeDirty();

            if (_isDirty)
            {
                _tmpText.ForceMeshUpdate();
                // OnTextChanged가 호출되어 곡선 적용됨
                _isDirty = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheComponents();
            SetDirty();
        }
#endif

        // ─────────────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_tmpText == null)
            {
                _tmpText = GetComponent<TMP_Text>();
            }
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
        }

        private void SetDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 설정 변경 감지 (Inspector 값 변경 등)
        /// </summary>
        private void CheckSettingsDirty()
        {
            // 스케일 변경 확인
            if (!Mathf.Approximately(_curveScale, _previousCurveScale))
            {
                _previousCurveScale = _curveScale;
                _isDirty = true;
            }

            // 회전 설정 변경 확인
            if (_rotateAlongCurve != _previousRotateAlongCurve)
            {
                _previousRotateAlongCurve = _rotateAlongCurve;
                _isDirty = true;
            }

            if (!Mathf.Approximately(_rotationStrength, _previousRotationStrength))
            {
                _previousRotationStrength = _rotationStrength;
                _isDirty = true;
            }

            // Curve 변경 확인 (간단한 해시 비교)
            int curveHash = GetCurveHash();
            if (curveHash != _previousCurveHash)
            {
                _previousCurveHash = curveHash;
                _isDirty = true;
            }
        }

        /// <summary>
        /// RectTransform 크기 변경 감지 (LayoutElement에 의한 크기 변경)
        /// </summary>
        private void CheckRectSizeDirty()
        {
            if (_rectTransform == null) return;

            Vector2 currentSize = _rectTransform.rect.size;
            if (!Mathf.Approximately(currentSize.x, _previousRectSize.x) ||
                !Mathf.Approximately(currentSize.y, _previousRectSize.y))
            {
                _previousRectSize = currentSize;
                _isDirty = true;
            }
        }

        /// <summary>
        /// AnimationCurve의 간단한 해시 계산
        /// </summary>
        private int GetCurveHash()
        {
            if (_curve == null || _curve.length == 0) return 0;

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < _curve.length; i++)
                {
                    var key = _curve[i];
                    hash = hash * 31 + key.time.GetHashCode();
                    hash = hash * 31 + key.value.GetHashCode();
                }
                return hash;
            }
        }

        /// <summary>
        /// 곡선 효과를 현재 메시에 적용 (ForceMeshUpdate 없이)
        /// - TEXT_CHANGED_EVENT에서 호출됨
        /// - TMP가 이미 메시를 업데이트한 상태이므로 정점만 수정
        /// </summary>
        private void ApplyCurveToMesh()
        {
            if (_curve == null || _tmpText == null) return;
            if (!isActiveAndEnabled) return;

            // Curve 래핑 모드 설정
            _curve.preWrapMode = WrapMode.Clamp;
            _curve.postWrapMode = WrapMode.Clamp;

            TMP_TextInfo textInfo = _tmpText.textInfo;
            int characterCount = textInfo.characterCount;

            if (characterCount == 0) return;

            // 텍스트 경계 계산
            float boundsMinX = _tmpText.bounds.min.x;
            float boundsMaxX = _tmpText.bounds.max.x;
            float boundsWidth = boundsMaxX - boundsMinX;

            if (boundsWidth <= 0) return;

            // 각 글자에 곡선 적용
            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

                if (!charInfo.isVisible) continue;

                int vertexIndex = charInfo.vertexIndex;
                int materialIndex = charInfo.materialReferenceIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                // 글자 중심점 계산 (baseline 기준)
                Vector3 charCenter = new Vector3(
                    (vertices[vertexIndex].x + vertices[vertexIndex + 2].x) / 2f,
                    charInfo.baseLine,
                    0f
                );

                // 정점을 중심 기준으로 이동
                vertices[vertexIndex + 0] -= charCenter;
                vertices[vertexIndex + 1] -= charCenter;
                vertices[vertexIndex + 2] -= charCenter;
                vertices[vertexIndex + 3] -= charCenter;

                // 곡선 상의 위치 계산 (0~1)
                float normalizedX = (charCenter.x - boundsMinX) / boundsWidth;

                // 곡선에서 Y 오프셋 계산
                float curveY = _curve.Evaluate(normalizedX) * _curveScale;

                // 회전 계산 (곡선의 접선 방향)
                if (_rotateAlongCurve && _rotationStrength > 0f)
                {
                    // 미분을 통한 접선 계산 (작은 델타 사용)
                    float delta = 0.001f;
                    float x0 = Mathf.Clamp01(normalizedX - delta);
                    float x1 = Mathf.Clamp01(normalizedX + delta);
                    float y0 = _curve.Evaluate(x0) * _curveScale;
                    float y1 = _curve.Evaluate(x1) * _curveScale;

                    // 접선 벡터
                    Vector2 tangent = new Vector2((x1 - x0) * boundsWidth, y1 - y0);

                    // 각도 계산 (라디안 → 도)
                    float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg * _rotationStrength;

                    // 변환 행렬 생성 (위치 이동 + 회전)
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(0f, curveY, 0f),
                        Quaternion.Euler(0f, 0f, angle),
                        Vector3.one
                    );

                    // 정점 변환
                    vertices[vertexIndex + 0] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 0]);
                    vertices[vertexIndex + 1] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 1]);
                    vertices[vertexIndex + 2] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 2]);
                    vertices[vertexIndex + 3] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 3]);
                }
                else
                {
                    // 회전 없음: 단순 위치 오프셋만 (Matrix4x4 생성 회피)
                    Vector3 offset = new Vector3(0f, curveY, 0f);
                    vertices[vertexIndex + 0] += offset;
                    vertices[vertexIndex + 1] += offset;
                    vertices[vertexIndex + 2] += offset;
                    vertices[vertexIndex + 3] += offset;
                }

                // 원래 위치로 복원
                vertices[vertexIndex + 0] += charCenter;
                vertices[vertexIndex + 1] += charCenter;
                vertices[vertexIndex + 2] += charCenter;
                vertices[vertexIndex + 3] += charCenter;
            }

            // 메시 업데이트 (정점 데이터만)
            _tmpText.UpdateVertexData();
        }

        // ─────────────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────────────

        /// <summary>
        /// 강제로 곡선 효과 다시 적용
        /// </summary>
        public void Refresh()
        {
            SetDirty();
        }

        /// <summary>
        /// 곡선을 아치 형태로 설정
        /// </summary>
        /// <param name="height">아치 높이 (픽셀)</param>
        public void SetArchCurve(float height)
        {
            _curve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f),
                new Keyframe(0.5f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, -2f, 0f)
            );
            _curveScale = height;
            SetDirty();
        }

        /// <summary>
        /// 곡선을 웨이브 형태로 설정
        /// </summary>
        /// <param name="amplitude">웨이브 진폭 (픽셀)</param>
        /// <param name="frequency">웨이브 주기 (1 = 한 주기)</param>
        public void SetWaveCurve(float amplitude, float frequency = 1f)
        {
            int keyCount = Mathf.Max(5, Mathf.RoundToInt(frequency * 4) + 1);
            Keyframe[] keys = new Keyframe[keyCount];

            for (int i = 0; i < keyCount; i++)
            {
                float t = (float)i / (keyCount - 1);
                float value = Mathf.Sin(t * frequency * Mathf.PI * 2f);
                keys[i] = new Keyframe(t, value);
            }

            _curve = new AnimationCurve(keys);
            _curveScale = amplitude;
            SetDirty();
        }

        /// <summary>
        /// 곡선을 직선으로 리셋
        /// </summary>
        public void ResetCurve()
        {
            _curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(1f, 0f)
            );
            _curveScale = 0f;
            SetDirty();
        }
    }
}
