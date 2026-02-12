using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace CAT.UI
{
    /// <summary>
    /// TMP 텍스트 레이아웃 제한
    /// - 최대 너비/높이에 도달하면 LayoutElement로 크기 고정
    /// - Auto Size와 함께 사용하여 텍스트 깜빡임 방지
    /// - LateUpdate 기반 더티 체크로 최적화
    /// - TMPCurve보다 먼저 실행되어야 함 (ExecutionOrder: -10)
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-10)]  // TMPCurve(0)보다 먼저 실행
    [RequireComponent(typeof(TMP_Text), typeof(LayoutElement))]
    [AddComponentMenu("CAT/UI/TMP Layout Limiter")]
    public class TMPLayoutLimiter : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector 설정
        // ─────────────────────────────────────────────

        [Header("Size Limits")]
        [Tooltip("최대 너비. 이 값에 도달하면 크기가 고정됩니다. (0 = 제한 없음)")]
        [SerializeField, Min(0f)]
        private float _maxWidth = 300f;

        [Tooltip("최대 높이. 이 값에 도달하면 크기가 고정됩니다. (0 = 제한 없음)")]
        [SerializeField, Min(0f)]
        private float _maxHeight = 0f;

        // ─────────────────────────────────────────────
        // 캐싱
        // ─────────────────────────────────────────────

        private TMP_Text _tmpText;
        private LayoutElement _layoutElement;

        private string _previousText;
        private float _previousMaxWidth;
        private float _previousMaxHeight;
        private bool _isDirty = true;

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        /// <summary>
        /// 최대 너비 (0 = 제한 없음)
        /// </summary>
        public float MaxWidth
        {
            get => _maxWidth;
            set
            {
                _maxWidth = Mathf.Max(0f, value);
                SetDirty();
            }
        }

        /// <summary>
        /// 최대 높이 (0 = 제한 없음)
        /// </summary>
        public float MaxHeight
        {
            get => _maxHeight;
            set
            {
                _maxHeight = Mathf.Max(0f, value);
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
            SetDirty();
        }

        private void OnDisable()
        {
            // 비활성화 시 LayoutElement 제한 해제
            if (_layoutElement != null)
            {
                _layoutElement.preferredWidth = -1;
                _layoutElement.preferredHeight = -1;
            }
        }

        private void LateUpdate()
        {
            if (_tmpText == null || _layoutElement == null) return;

            // 더티 체크
            CheckDirty();

            if (_isDirty)
            {
                UpdateLayout();
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
            if (_layoutElement == null)
            {
                _layoutElement = GetComponent<LayoutElement>();
            }
        }

        private void SetDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 변경 사항 감지
        /// </summary>
        private void CheckDirty()
        {
            // 텍스트 변경 확인
            if (_tmpText.text != _previousText)
            {
                _previousText = _tmpText.text;
                _isDirty = true;
            }

            // TMP 내부 변경 확인 (폰트 크기, 스타일 등)
            if (_tmpText.havePropertiesChanged)
            {
                _isDirty = true;
            }

            // max 값 변경 확인
            if (!Mathf.Approximately(_maxWidth, _previousMaxWidth))
            {
                _previousMaxWidth = _maxWidth;
                _isDirty = true;
            }

            if (!Mathf.Approximately(_maxHeight, _previousMaxHeight))
            {
                _previousMaxHeight = _maxHeight;
                _isDirty = true;
            }
        }

        /// <summary>
        /// 레이아웃 업데이트
        /// </summary>
        private void UpdateLayout()
        {
            // Auto Size 사용 시 최대 폰트 크기 기준으로 계산
            float originalFontSize = _tmpText.fontSize;
            bool useAutoSize = _tmpText.enableAutoSizing;

            if (useAutoSize)
            {
                // 최대 폰트 크기일 때의 예상 크기 계산
                _tmpText.fontSize = _tmpText.fontSizeMax;
            }

            // 현재 텍스트의 예상 크기 계산
            Vector2 preferredSize = _tmpText.GetPreferredValues(_tmpText.text);

            if (useAutoSize)
            {
                // 원래 폰트 크기로 복원
                _tmpText.fontSize = originalFontSize;
            }

            // 너비 제어
            if (_maxWidth > 0f && preferredSize.x >= _maxWidth)
            {
                if (!Mathf.Approximately(_layoutElement.preferredWidth, _maxWidth))
                {
                    _layoutElement.preferredWidth = _maxWidth;
                }
            }
            else
            {
                if (_layoutElement.preferredWidth != -1)
                {
                    _layoutElement.preferredWidth = -1;
                }
            }

            // 높이 제어
            if (_maxHeight > 0f && preferredSize.y >= _maxHeight)
            {
                if (!Mathf.Approximately(_layoutElement.preferredHeight, _maxHeight))
                {
                    _layoutElement.preferredHeight = _maxHeight;
                }
            }
            else
            {
                if (_layoutElement.preferredHeight != -1)
                {
                    _layoutElement.preferredHeight = -1;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────────────

        /// <summary>
        /// 강제로 레이아웃 다시 계산
        /// </summary>
        public void Refresh()
        {
            SetDirty();
        }
    }
}
