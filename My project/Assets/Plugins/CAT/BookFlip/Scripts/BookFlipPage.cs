using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace CAT.BookFlip
{
    /// <summary>
    /// 책 페이지 데이터를 추상화하는 클래스
    /// Sprite, Prefab, GameObject 세 가지 타입 지원
    /// Direct / ResourcesPath / CustomAsync 세 가지 로드 방식 지원
    /// </summary>
    [System.Serializable]
    public class BookFlipPage
    {
        // ─────────────────────────────────────────────
        // Enum 정의
        // ─────────────────────────────────────────────

        public enum PageType
        {
            /// <summary>이미지 스프라이트</summary>
            Sprite,
            /// <summary>프리팹 인스턴스</summary>
            Prefab,
            /// <summary>씬에 존재하는 GameObject 복제</summary>
            GameObject
        }

        public enum SourceMode
        {
            /// <summary>Inspector에서 직접 참조 (기본)</summary>
            Direct,
            /// <summary>Resources.Load로 경로 기반 동기 로드</summary>
            ResourcesPath,
            /// <summary>외부 비동기 로더 사용 (Addressables 등) — AsyncLoader 설정 필요</summary>
            CustomAsync,
        }

        // ─────────────────────────────────────────────
        // Inspector 필드
        // ─────────────────────────────────────────────

        [SerializeField] private PageType _type = PageType.Sprite;
        [SerializeField] private SourceMode _sourceMode = SourceMode.Direct;

        // Direct 모드
        [SerializeField] private Sprite _sprite;
        [SerializeField] private GameObject _prefab;
        /// <summary>
        /// 씬에 미리 둔 페이지 UI 템플릿(비활성 권장). 설정 시 복제 원본으로 사용하며 Project 프리팹 참조는 폴백으로만 쓴다.
        /// 런타임에는 SetScenePageTemplate으로 지정 가능.
        /// </summary>
        [SerializeField] private GameObject _scenePageTemplate;
        [SerializeField] private GameObject _gameObject;

        // ResourcesPath / CustomAsync 모드 (Resources 폴더 기준 경로 또는 Addressable 키)
        [SerializeField] private string _resourcePath;

        /// <summary>
        /// 인스턴스 유지 여부
        /// true : 한 번 생성된 인스턴스를 페이지 이동 후에도 비활성화 상태로 보존 → RuntimeInstance로 외부 접근 및 내용 수정 가능
        /// false: 페이지 표시 시마다 새로 생성, 숨겨질 때 파괴 (기본)
        /// </summary>
        [SerializeField] private bool _persistInstance = false;

        // ─────────────────────────────────────────────
        // 런타임 상태
        // ─────────────────────────────────────────────

        private GameObject _runtimeInstance;
        private Image _runtimeImage;
        private RectTransform _runtimeRectTransform;
        private CanvasGroup _canvasGroup;
        private bool _isInitialized;
        private bool _sourceLoaded; // Resources / CustomAsync 로드 완료 여부

        // ─────────────────────────────────────────────
        // 프로퍼티
        // ─────────────────────────────────────────────

        public PageType Type => _type;
        public SourceMode Source => _sourceMode;
        public Sprite Sprite => _sprite;
        public GameObject Prefab => _prefab;
        /// <summary>씬에 둔 복제 원본(직렬화 또는 SetScenePageTemplate으로 설정).</summary>
        public GameObject ScenePageTemplate => _scenePageTemplate;
        public GameObject SourceGameObject => _gameObject;
        public string ResourcePath => _resourcePath;
        public bool PersistInstance => _persistInstance;

        /// <summary>현재 활성화된 런타임 인스턴스 (PersistInstance 모드에서 외부 수정용)</summary>
        public GameObject RuntimeInstance => _runtimeInstance;

        // ─────────────────────────────────────────────
        // 정적 비동기 로더 훅
        // ─────────────────────────────────────────────

        /// <summary>
        /// Addressables 또는 커스텀 비동기 에셋 로더.
        /// CustomAsync 모드 사용 전 반드시 설정해야 한다.
        ///
        /// 사용 예 (Addressables):
        /// <code>
        /// BookFlipPage.AsyncLoader = (key, type, onDone) =>
        /// {
        ///     if (type == typeof(Sprite))
        ///         Addressables.LoadAssetAsync&lt;Sprite&gt;(key).Completed += op => onDone(op.Result);
        ///     else
        ///         Addressables.LoadAssetAsync&lt;GameObject&gt;(key).Completed += op => onDone(op.Result);
        /// };
        /// </code>
        /// </summary>
        public static System.Action<string, System.Type, System.Action<Object>> AsyncLoader { get; set; }

        /// <summary>
        /// 런타임 풀/초기화에서 씬에 올려 둔 인스턴스를 복제 원본으로 지정합니다. Project 프리팹 필드 없이 사용할 때 호출합니다.
        /// </summary>
        public void SetScenePageTemplate(GameObject sceneTemplate)
        {
            _scenePageTemplate = sceneTemplate;
        }

        // ─────────────────────────────────────────────
        // 공개 메서드 — 인스턴스 생성 / 관리
        // ─────────────────────────────────────────────

        /// <summary>
        /// 페이지를 렌더링할 Image 컴포넌트 반환 (없으면 생성).
        /// ResourcesPath 모드는 동기 로드, CustomAsync는 LoadAsync 코루틴 사용 권장.
        /// </summary>
        public Image GetOrCreateImage(Transform parent, string name)
        {
            // PersistInstance: 기존 인스턴스를 새 부모로 이동 후 재사용
            if (_isInitialized && _runtimeInstance != null)
            {
                if (_persistInstance)
                {
                    _runtimeInstance.transform.SetParent(parent, false);
                    ApplyFullStretchRT(_runtimeInstance.GetComponent<RectTransform>());
                    _runtimeInstance.SetActive(true);
                }
                return _runtimeImage;
            }

            // ResourcesPath 동기 로드 (최초 1회)
            if (_sourceMode == SourceMode.ResourcesPath && !_sourceLoaded)
                LoadFromResources();

            // 인스턴스 생성
            switch (_type)
            {
                case PageType.Sprite:
                    _runtimeInstance = CreateSpriteImage(parent, name);
                    break;
                case PageType.Prefab:
                    _runtimeInstance = CreatePrefabInstance(parent, name);
                    break;
                case PageType.GameObject:
                    _runtimeInstance = CreateGameObjectInstance(parent, name);
                    break;
            }

            if (_runtimeInstance != null)
                CacheComponents();

            return _runtimeImage;
        }

        /// <summary>
        /// Resources.Load 동기 로드 (ResourcesPath 모드).
        /// 로드 완료 후 RefreshInstance()를 호출하면 다음 표시 시 새 소스로 인스턴스가 생성된다.
        /// </summary>
        public void LoadFromResources()
        {
            if (string.IsNullOrEmpty(_resourcePath))
            {
                Debug.LogWarning("[BookFlipPage] ResourcePath가 비어 있습니다.");
                return;
            }

            switch (_type)
            {
                case PageType.Sprite:
                    _sprite = Resources.Load<Sprite>(_resourcePath);
                    if (_sprite == null)
                        Debug.LogWarning($"[BookFlipPage] Resources에서 Sprite를 찾을 수 없음: {_resourcePath}");
                    break;

                case PageType.Prefab:
                    _prefab = Resources.Load<GameObject>(_resourcePath);
                    if (_prefab == null)
                        Debug.LogWarning($"[BookFlipPage] Resources에서 Prefab을 찾을 수 없음: {_resourcePath}");
                    break;

                case PageType.GameObject:
                    _gameObject = Resources.Load<GameObject>(_resourcePath);
                    if (_gameObject == null)
                        Debug.LogWarning($"[BookFlipPage] Resources에서 GameObject를 찾을 수 없음: {_resourcePath}");
                    break;
            }

            _sourceLoaded = (_sprite != null || _prefab != null || _gameObject != null);
        }

        /// <summary>
        /// 비동기 에셋 로드 후 인스턴스 생성 (CustomAsync 모드).
        /// BookFlip 컴포넌트의 StartCoroutine으로 실행해야 한다.
        ///
        /// 사용 예:
        /// <code>
        /// StartCoroutine(page.LoadAsync(targetRT, "Page_0", img => { ... }));
        /// </code>
        /// </summary>
        public IEnumerator LoadAsync(Transform parent, string name, System.Action<Image> onComplete)
        {
            if (AsyncLoader == null)
            {
                Debug.LogError("[BookFlipPage] AsyncLoader가 설정되지 않았습니다. BookFlipPage.AsyncLoader를 먼저 설정하세요.");
                onComplete?.Invoke(null);
                yield break;
            }

            bool done = false;
            Object loadedAsset = null;
            System.Type assetType = _type == PageType.Sprite ? typeof(Sprite) : typeof(GameObject);

            AsyncLoader(_resourcePath, assetType, asset =>
            {
                loadedAsset = asset;
                done = true;
            });

            while (!done)
                yield return null;

            // 로드된 에셋 소스 필드에 적용
            switch (_type)
            {
                case PageType.Sprite:   _sprite = loadedAsset as Sprite; break;
                case PageType.Prefab:   _prefab = loadedAsset as GameObject; break;
                case PageType.GameObject: _gameObject = loadedAsset as GameObject; break;
            }

            _sourceLoaded = loadedAsset != null;

            Image img = GetOrCreateImage(parent, name);
            onComplete?.Invoke(img);
        }

        /// <summary>
        /// 소스 변경 후 다음 표시 시 인스턴스를 새로 생성하도록 표시.
        /// PersistInstance 모드에서는 기존 인스턴스를 비활성화 상태로 유지한다.
        /// </summary>
        public void RefreshInstance()
        {
            if (_runtimeInstance != null)
            {
                if (_persistInstance)
                    _runtimeInstance.SetActive(false); // PersistInstance: 유지, 비활성화
                else
                {
                    DestroyRuntimeGameObject(_runtimeInstance);
                    _runtimeInstance = null;
                }
            }

            _runtimeImage = null;
            _runtimeRectTransform = null;
            _canvasGroup = null;
            _isInitialized = false;
        }

        /// <summary>
        /// PersistInstance 모드와 무관하게 인스턴스를 즉시 파괴하고 초기화.
        /// 소스를 완전히 교체한 경우 사용한다.
        /// </summary>
        public void ForceDestroyInstance()
        {
            if (_runtimeInstance != null)
            {
                DestroyRuntimeGameObject(_runtimeInstance);
                _runtimeInstance = null;
            }
            _runtimeImage = null;
            _runtimeRectTransform = null;
            _canvasGroup = null;
            _isInitialized = false;
            _sourceLoaded = false;
        }

        /// <summary>
        /// 인스턴스를 표시 슬롯에서 해제.
        /// PersistInstance: 비활성화 후 poolParent로 이동 (null이면 현재 위치 유지).
        /// 아닌 경우: 파괴.
        /// </summary>
        public void Release(Transform poolParent = null)
        {
            if (_runtimeInstance == null) return;

            if (_persistInstance)
            {
                _runtimeInstance.SetActive(false);
                // 지정된 풀 부모로 이동하여 CleanupChildPageInstances가 건드리지 않도록 격리
                if (poolParent != null && _runtimeInstance.transform.parent != poolParent)
                    _runtimeInstance.transform.SetParent(poolParent, false);
                // _isInitialized = true 유지 → 다음 GetOrCreateImage에서 재사용
            }
            else
            {
                DestroyRuntimeGameObject(_runtimeInstance);
                _runtimeInstance = null;
                _runtimeImage = null;
                _runtimeRectTransform = null;
                _canvasGroup = null;
                _isInitialized = false;
            }
        }

        // ─────────────────────────────────────────────
        // 공개 메서드 — 상태 제어
        // ─────────────────────────────────────────────

        /// <summary>인스턴스 RectTransform 반환</summary>
        public RectTransform GetRectTransform()
        {
            if (_runtimeRectTransform == null && _runtimeInstance != null)
                CacheComponents();
            return _runtimeRectTransform;
        }

        /// <summary>인스턴스 활성화/비활성화</summary>
        public void SetActive(bool active)
        {
            if (_runtimeInstance != null)
                _runtimeInstance.SetActive(active);
        }

        /// <summary>UI 인터랙션 활성화/비활성화 (CanvasGroup 기반)</summary>
        public void SetInteractable(bool interactable)
        {
            if (_canvasGroup == null && _runtimeInstance != null)
                CacheComponents();

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = interactable;
                // blocksRaycasts를 interactable과 연동:
                // - true (정상 표시): 자식 Button 등이 raycast를 받아 클릭 가능
                // - false (플립 애니메이션 중): 페이지가 raycast를 차단하지 않아 HotSpot이 드래그를 감지 가능
                _canvasGroup.blocksRaycasts = interactable;
            }
        }

        /// <summary>CanvasGroup 알파값 설정</summary>
        public void SetAlpha(float alpha)
        {
            if (_canvasGroup == null && _runtimeInstance != null)
                CacheComponents();

            if (_canvasGroup != null)
                _canvasGroup.alpha = alpha;
        }

        /// <summary>
        /// 런타임 인스턴스 완전 파괴 (BookFlip.OnDestroy에서 호출).
        /// Sprite/Prefab/GameObject 모든 타입에서 복제본을 파괴한다.
        /// 원본 씬 오브젝트(_gameObject 필드)는 보존된다.
        /// </summary>
        public void Destroy()
        {
            if (_runtimeInstance != null)
                DestroyRuntimeGameObject(_runtimeInstance);

            _runtimeInstance = null;
            _runtimeImage = null;
            _runtimeRectTransform = null;
            _canvasGroup = null;
            _isInitialized = false;
        }

        // ─────────────────────────────────────────────
        // 유효성 / 디버그
        // ─────────────────────────────────────────────

        /// <summary>페이지 유효성 검사</summary>
        public bool IsValid()
        {
            // Path 기반 모드는 경로가 있으면 유효 (로드 전이어도)
            if (_sourceMode == SourceMode.ResourcesPath || _sourceMode == SourceMode.CustomAsync)
                return !string.IsNullOrEmpty(_resourcePath);

            // Direct 모드
            switch (_type)
            {
                case PageType.Sprite:     return _sprite != null;
                case PageType.Prefab:     return _scenePageTemplate != null || _prefab != null;
                case PageType.GameObject: return _gameObject != null;
                default:                  return false;
            }
        }

        /// <summary>디버그용 이름 반환</summary>
        public string GetDebugName()
        {
            if (_sourceMode != SourceMode.Direct)
                return string.IsNullOrEmpty(_resourcePath) ? "(path 없음)" : _resourcePath;

            switch (_type)
            {
                case PageType.Sprite:     return _sprite     != null ? _sprite.name     : "null";
                case PageType.Prefab:
                    if (_scenePageTemplate != null) return _scenePageTemplate.name;
                    return _prefab != null ? _prefab.name : "null";
                case PageType.GameObject: return _gameObject != null ? _gameObject.name : "null";
                default:                  return "unknown";
            }
        }

        // ─────────────────────────────────────────────
        // 내부 — 인스턴스 생성
        // ─────────────────────────────────────────────

        private GameObject CreateSpriteImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent, false);

            ApplyFullStretchRT(go.GetComponent<RectTransform>());

            Image img = go.GetComponent<Image>();
            img.sprite = _sprite;
            img.raycastTarget = false;

            return go;
        }

        private GameObject CreatePrefabInstance(Transform parent, string name)
        {
            // 씬 템플릿 우선(사용자 배치 또는 런타임 SetScenePageTemplate), 없으면 Project 프리팹 에셋
            GameObject cloneSource = _scenePageTemplate != null ? _scenePageTemplate : _prefab;
            if (cloneSource == null)
            {
                Debug.LogError($"[BookFlipPage] 씬 템플릿 또는 Prefab이 없습니다: {name}");
                return null;
            }

            GameObject instance = Object.Instantiate(cloneSource, parent);
            instance.name = name;
            instance.hideFlags = HideFlags.DontSave;
            ApplyFullStretchRT(instance.GetComponent<RectTransform>());
            return instance;
        }

        private GameObject CreateGameObjectInstance(Transform parent, string name)
        {
            // _gameObject 우선, 없으면 _prefab 폴백 (ResourcesPath로 프리팹을 로드한 경우 대응)
            GameObject source = _gameObject != null ? _gameObject : _prefab;
            if (source == null)
            {
                Debug.LogError($"[BookFlipPage] 소스 GameObject가 null입니다: {name}");
                return null;
            }

            GameObject instance = Object.Instantiate(source, parent);
            instance.name = name;
            instance.hideFlags = HideFlags.DontSave;
            ApplyFullStretchRT(instance.GetComponent<RectTransform>());
            return instance;
        }

        // ─────────────────────────────────────────────
        // 내부 — 캐싱 / 유틸
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_isInitialized || _runtimeInstance == null) return;

            _runtimeRectTransform = _runtimeInstance.GetComponent<RectTransform>();
            _runtimeImage = _runtimeInstance.GetComponent<Image>();

            _canvasGroup = _runtimeInstance.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _runtimeInstance.AddComponent<CanvasGroup>();

            _isInitialized = true;
        }

        /// <summary>BookFlip 웜풀 및 페이지 인스턴스에 공통으로 사용하는 전체 스트레치.</summary>
        public static void ApplyFullStretchRT(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Destroy는 프레임 끝까지 오브젝트를 유지하므로, 같은 페이지를 다른 슬롯에 즉시 재생성하면
        /// 한 프레임 동안 이전·새 인스턴스가 겹쳐 깜빡일 수 있다. 파괴 전 비활성화로 즉시 렌더에서 제외한다.
        /// </summary>
        private static void DestroyRuntimeGameObject(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            Object.Destroy(go);
        }
    }
}
