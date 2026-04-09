using UnityEngine;
using UnityEngine.UI;

namespace CAT.BookFlip
{
    /// <summary>
    /// 책 페이지 데이터를 추상화하는 클래스
    /// Sprite, Prefab, GameObject 세 가지 타입 지원
    /// </summary>
    [System.Serializable]
    public class BookFlipPage
    {
        /// <summary>
        /// 페이지 타입
        /// </summary>
        public enum PageType
        {
            /// <summary>이미지 스프라이트</summary>
            Sprite,
            /// <summary>프리팹 인스턴스</summary>
            Prefab,
            /// <summary>씬에 이미 존재하는 GameObject</summary>
            GameObject
        }

        [SerializeField] private PageType _type = PageType.Sprite;
        [SerializeField] private Sprite _sprite;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private GameObject _gameObject;

        // 런타임에 생성된 페이지 인스턴스
        private GameObject _runtimeInstance;
        private Image _runtimeImage;
        private RectTransform _runtimeRectTransform;
        private CanvasGroup _canvasGroup;

        // 캐싱된 컴포넌트들
        private bool _isInitialized;

        public PageType Type => _type;
        public Sprite Sprite => _sprite;
        public GameObject Prefab => _prefab;
        public GameObject GameObject => _gameObject;

        /// <summary>
        /// 페이지를 렌더링할 Image 컴포넌트 생성 또는 반환
        /// </summary>
        public Image GetOrCreateImage(Transform parent, string name)
        {
            if (_runtimeImage != null)
                return _runtimeImage;

            switch (_type)
            {
                case PageType.Sprite:
                    _runtimeInstance = CreateSpriteImage(parent, name);
                    break;

                case PageType.Prefab:
                    _runtimeInstance = CreatePrefabInstance(parent, name);
                    break;

                case PageType.GameObject:
                    _runtimeInstance = _gameObject;
                    break;
            }

            if (_runtimeInstance != null)
            {
                CacheComponents();
            }

            return _runtimeImage;
        }

        /// <summary>
        /// Sprite 타입 페이지용 Image GameObject 생성
        /// </summary>
        private GameObject CreateSpriteImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            Image img = go.GetComponent<Image>();
            img.sprite = _sprite;
            img.raycastTarget = false; // 기본적으로 raycast 비활성화

            return go;
        }

        /// <summary>
        /// Prefab 타입 페이지 인스턴스 생성
        /// </summary>
        private GameObject CreatePrefabInstance(Transform parent, string name)
        {
            if (_prefab == null)
            {
                Debug.LogError($"[BookFlipPage] Prefab이 null입니다: {name}");
                return null;
            }

            GameObject instance = Object.Instantiate(_prefab, parent);
            instance.name = name;
            instance.hideFlags = HideFlags.DontSave;

            // RectTransform 설정
            RectTransform rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }

            return instance;
        }

        /// <summary>
        /// 컴포넌트 캐싱
        /// </summary>
        private void CacheComponents()
        {
            if (_isInitialized || _runtimeInstance == null)
                return;

            _runtimeRectTransform = _runtimeInstance.GetComponent<RectTransform>();
            _runtimeImage = _runtimeInstance.GetComponent<Image>();

            // CanvasGroup이 없으면 추가
            _canvasGroup = _runtimeInstance.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = _runtimeInstance.AddComponent<CanvasGroup>();
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 페이지의 RectTransform 반환
        /// </summary>
        public RectTransform GetRectTransform()
        {
            if (_runtimeRectTransform == null && _runtimeInstance != null)
                CacheComponents();

            return _runtimeRectTransform;
        }

        /// <summary>
        /// 페이지 활성화/비활성화
        /// </summary>
        public void SetActive(bool active)
        {
            if (_runtimeInstance != null)
            {
                _runtimeInstance.SetActive(active);
            }
        }

        /// <summary>
        /// UI 인터랙션 활성화/비활성화
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (_canvasGroup == null && _runtimeInstance != null)
                CacheComponents();

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = interactable;
                // blocksRaycasts는 항상 false로 유지 (LeftHotSpot/RightHotSpot Raycast 차단 방지)
                // 개별 UI 요소(Button 등)의 raycastTarget은 interactable에 따라 동작
                _canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 페이지의 알파값 설정
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (_canvasGroup == null && _runtimeInstance != null)
                CacheComponents();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
            }
        }

        /// <summary>
        /// 페이지 인스턴스 파괴
        /// </summary>
        public void Destroy()
        {
            // Sprite와 Prefab 타입만 파괴 (GameObject 타입은 씬에 원래 있던 것이므로 유지)
            if (_type != PageType.GameObject && _runtimeInstance != null)
            {
                Object.Destroy(_runtimeInstance);
            }

            _runtimeInstance = null;
            _runtimeImage = null;
            _runtimeRectTransform = null;
            _canvasGroup = null;
            _isInitialized = false;
        }

        /// <summary>
        /// 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            switch (_type)
            {
                case PageType.Sprite:
                    return _sprite != null;
                case PageType.Prefab:
                    return _prefab != null;
                case PageType.GameObject:
                    return _gameObject != null;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 페이지 이름 반환 (디버깅용)
        /// </summary>
        public string GetDebugName()
        {
            switch (_type)
            {
                case PageType.Sprite:
                    return _sprite != null ? _sprite.name : "null";
                case PageType.Prefab:
                    return _prefab != null ? _prefab.name : "null";
                case PageType.GameObject:
                    return _gameObject != null ? _gameObject.name : "null";
                default:
                    return "unknown";
            }
        }
    }
}
