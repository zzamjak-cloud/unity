using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// RawImage의 uvRect를 조작하여 패턴 텍스처 UV를 X/Y 방향으로 Loop 스크롤하는 컴포넌트.
    /// 별도 셰이더 없이 동작하며, 텍스처의 Wrap Mode를 Repeat으로 설정해야 합니다.
    /// </summary>
    [AddComponentMenu("CAT/Effects/UVPatternFlow")]
    [RequireComponent(typeof(RawImage))]
    [ExecuteAlways]
    public class UVPatternFlow : MonoBehaviour
    {
        [SerializeField, Tooltip("초당 UV 스크롤 속도 (X/Y축)")]
        private Vector2 _scrollSpeed = new Vector2(0.1f, 0f);

        [SerializeField, Tooltip("컴포넌트 활성화 시 자동 재생")]
        private bool _playOnEnable = true;

        public Vector2 ScrollSpeed
        {
            get => _scrollSpeed;
            set => _scrollSpeed = value;
        }

        public bool IsPlaying => _isPlaying;

        private RawImage _rawImage;
        private Vector2 _offset;
        private bool _isPlaying;

        public void Play()
        {
            _isPlaying = true;
        }

        public void Pause()
        {
            _isPlaying = false;
        }

        public void Stop()
        {
            _isPlaying = false;
            _offset = Vector2.zero;
            ApplyOffset();
        }

        public void SetOffset(Vector2 offset)
        {
            _offset = offset;
            ApplyOffset();
        }

        public void ResetOffset()
        {
            _offset = Vector2.zero;
            ApplyOffset();
        }

        /// <summary>에디터 전용: 외부에서 deltaTime을 전달하여 스크롤을 진행시킨다.</summary>
        public void EditorAdvance(float dt)
        {
            _offset += _scrollSpeed * dt;
            WrapOffset();
            ApplyOffset();
        }

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
        }

        private void OnEnable()
        {
            if (_rawImage == null)
                _rawImage = GetComponent<RawImage>();

            _offset = Vector2.zero;
            ApplyOffset();

            if (_playOnEnable && Application.isPlaying)
                Play();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_isPlaying) return;

            _offset += _scrollSpeed * Time.deltaTime;
            WrapOffset();
            ApplyOffset();
        }

        private void WrapOffset()
        {
            // 부동소수점 정밀도 유지를 위해 [0, 1) 범위로 래핑
            _offset.x -= Mathf.Floor(_offset.x);
            _offset.y -= Mathf.Floor(_offset.y);
        }

        private void ApplyOffset()
        {
            if (_rawImage == null) return;
            // 오프셋(x, y)만 갱신하고 타일링 크기(w, h)는 인스펙터 설정값을 유지
            Rect rect = _rawImage.uvRect;
            rect.x = _offset.x;
            rect.y = _offset.y;
            _rawImage.uvRect = rect;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_rawImage != null && !Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null) ApplyOffset();
                };
            }
        }
#endif
    }
}
