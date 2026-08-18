using UnityEngine;
using UnityEngine.UI;

namespace CAT.Utility
{
    /// <summary>
    /// 지정된 Target 게임 오브젝트의 이미지(Image, RawImage, SpriteRenderer)를 실시간으로 추적하여 자신의 이미지를 동기화합니다.
    /// 런타임과 에디터 모드 모두에서 작동합니다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ImageFollow : MonoBehaviour
    {
        [Tooltip("follow할 대상 이미지")]
        public GameObject targetObject;

        [Tooltip("매 프레임 소스 변경을 자동 감지할지 여부.\n애니메이션처럼 런타임에 소스 이미지가 계속 바뀌는 경우에만 켜세요.\n순수 이미지 교체(도감/인벤토리 등)는 끄고 Refresh()로 갱신하는 것이 성능상 유리합니다.")]
        public bool autoFollow = false;

        // target 이미지 컴포넌트 캐시
        private Image _sourceImage;
        private RawImage _sourceRawImage;
        private SpriteRenderer _sourceSpriteRenderer;

        // follow 이미지 컴포넌트 캐시
        private Image _followerImage;
        private RawImage _followerRawImage;
        private SpriteRenderer _followerSpriteRenderer;

        // 변화를 감지하기 위한 마지막 상태 저장 변수
        private Sprite _lastSprite;
        private Texture _lastTexture;
        private GameObject _lastTargetObject;

        // 이 컴포넌트가 직접 생성한 Sprite (Texture -> Sprite 변환 시). 반드시 추적하여 파괴해야 메모리 누수를 막음
        private Sprite _generatedSprite;

        private void OnEnable()
        {
            InitializeComponents();
            // 활성화(오브젝트 풀 스폰 포함) 시 1회 동기화하여 즉시 올바른 이미지를 표시
            FollowImage();
        }

        private void OnDisable()
        {
            // 생성한 Sprite 정리 (메모리 누수 방지)
            ReleaseGeneratedSprite();
        }

        private void LateUpdate()
        {
            // autoFollow가 꺼져 있으면 폴링하지 않음 (정적 이미지 교체는 Refresh()로 갱신)
            // 도감/인벤토리처럼 수십~수백 개가 나열되는 경우 매 프레임 비용을 완전히 제거
            if (!autoFollow) return;

            // 타겟 오브젝트가 변경되었는지 확인 (에디터에서 드래그 앤 드롭 변경 등)
            if (targetObject != _lastTargetObject)
            {
                InitializeComponents();
            }

            if (targetObject == null) return;

            FollowImage();
        }

        /// <summary>
        /// 소스 이미지를 즉시 팔로워에 반영합니다.
        /// autoFollow를 끈 상태에서 데이터 바인딩 / 상태 전환 / 풀 스폰 시점에 외부에서 호출하세요.
        /// </summary>
        public void Refresh()
        {
            // 타겟이 바뀌었으면 컴포넌트 캐시 갱신
            if (targetObject != _lastTargetObject)
            {
                InitializeComponents();
            }

            if (targetObject == null) return;

            FollowImage();
        }

        // 인스펙터에서 값이 변경될 때마다 호출되어 에디터에서의 실시간 반응성을 높입니다.
        private void OnValidate()
        {
            if (!gameObject.activeInHierarchy) return;

#if UNITY_EDITOR
            // OnValidate는 임포트/직렬화 도중에도 호출될 수 있어, 이 시점에 Sprite.Create 등
            // 오브젝트 생성이 일어나면 어설션/경고가 발생함. delayCall로 안전한 시점까지 지연.
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    // 지연 실행 시점에 오브젝트가 파괴되었을 수 있으므로 재확인
                    if (this == null) return;
                    InitializeComponents();
                    FollowImage();
                };
                return;
            }
#endif
            InitializeComponents();
            // 즉시 변경사항을 적용하기 위해 FollowImage 호출
            FollowImage();
        }

        /// <summary>
        /// 소스와 팔로워의 이미지 관련 컴포넌트를 찾아서 캐싱합니다.
        /// </summary>
        private void InitializeComponents()
        {
            // 이전 캐시 초기화
            _sourceImage = null;
            _sourceRawImage = null;
            _sourceSpriteRenderer = null;

            // 소스 컴포넌트 캐싱
            if (targetObject != null)
            {
                _sourceImage = targetObject.GetComponent<Image>();
                _sourceRawImage = targetObject.GetComponent<RawImage>();
                _sourceSpriteRenderer = targetObject.GetComponent<SpriteRenderer>();
            }

            // 팔로워(자신) 컴포넌트 캐싱
            _followerImage = GetComponent<Image>();
            _followerRawImage = GetComponent<RawImage>();
            _followerSpriteRenderer = GetComponent<SpriteRenderer>();

            // 마지막 상태 초기화하여 첫 프레임에 반드시 업데이트 되도록 함
            _lastSprite = null;
            _lastTexture = null;
            _lastTargetObject = targetObject;
        }

        /// <summary>
        /// 소스 이미지의 변경을 감지하고 팔로워의 이미지를 업데이트합니다.
        /// </summary>
        private void FollowImage()
        {
            if (targetObject == null) return;

            // 1. 소스가 Image 컴포넌트를 사용하는 경우
            if (_sourceImage != null)
            {
                Sprite currentSprite = _sourceImage.sprite;
                if (currentSprite != _lastSprite)
                {
                    SetImage(currentSprite);
                    _lastSprite = currentSprite;
                    _lastTexture = currentSprite != null ? currentSprite.texture : null;
                }
            }
            // 2. 소스가 RawImage 컴포넌트를 사용하는 경우
            else if (_sourceRawImage != null)
            {
                Texture currentTexture = _sourceRawImage.texture;
                if (currentTexture != _lastTexture)
                {
                    SetImage(currentTexture);
                    _lastTexture = currentTexture;
                    _lastSprite = null; // RawImage는 Sprite 정보가 없음
                }
            }
            // 3. 소스가 SpriteRenderer 컴포넌트를 사용하는 경우
            else if (_sourceSpriteRenderer != null)
            {
                Sprite currentSprite = _sourceSpriteRenderer.sprite;
                if (currentSprite != _lastSprite)
                {
                    SetImage(currentSprite);
                    _lastSprite = currentSprite;
                    _lastTexture = currentSprite != null ? currentSprite.texture : null;
                }
            }
        }

        /// <summary>
        /// 팔로워의 컴포넌트 타입에 맞춰 Sprite를 설정합니다.
        /// </summary>
        private void SetImage(Sprite newSprite)
        {
            // 직접 생성한 Sprite를 덮어쓰기 전에 파괴 (고아 Sprite 누수 방지)
            ReleaseGeneratedSprite();

            if (_followerImage != null)
            {
                _followerImage.sprite = newSprite;
            }
            if (_followerRawImage != null)
            {
                _followerRawImage.texture = (newSprite != null) ? newSprite.texture : null;
            }
            if (_followerSpriteRenderer != null)
            {
                _followerSpriteRenderer.sprite = newSprite;
            }
        }

        /// <summary>
        /// 팔로워의 컴포넌트 타입에 맞춰 Texture를 설정합니다.
        /// </summary>
        private void SetImage(Texture newTexture)
        {
            if (_followerRawImage != null)
            {
                _followerRawImage.texture = newTexture;
            }
            // Image나 SpriteRenderer는 Texture를 직접 받지 않으므로, Sprite를 생성하여 적용합니다.
            if (_followerImage != null || _followerSpriteRenderer != null)
            {
                // 이전에 직접 생성한 Sprite를 먼저 파괴 (메모리 누수 방지)
                ReleaseGeneratedSprite();

                Sprite newSprite = null;
                // Texture2D만 Sprite로 변환 가능. RenderTexture 등 GPU 전용 텍스처는
                // Sprite.Create 대상이 아니므로 null 처리하여 잘못된 생성을 방지함.
                Texture2D tex2D = newTexture as Texture2D;
                if (tex2D != null)
                {
                    newSprite = Sprite.Create(tex2D, new Rect(0, 0, tex2D.width, tex2D.height), new Vector2(0.5f, 0.5f));
                    // 런타임 생성 오브젝트에 HideFlags 미설정 시 DontSaveInEditor 어설션 발생
                    newSprite.hideFlags = HideFlags.DontSave;
                    _generatedSprite = newSprite; // 추적하여 다음 갱신/해제 시 파괴
                }
                else if (newTexture != null)
                {
                    Debug.LogWarning($"[ImageFollow] '{name}': Texture2D가 아닌 텍스처({newTexture.GetType().Name})는 Image/SpriteRenderer로 변환할 수 없습니다. 팔로워/소스 컴포넌트 타입을 통일하세요.", this);
                }

                if (_followerImage != null) _followerImage.sprite = newSprite;
                if (_followerSpriteRenderer != null) _followerSpriteRenderer.sprite = newSprite;
            }
        }

        /// <summary>
        /// 이 컴포넌트가 직접 생성한 Sprite를 안전하게 파괴합니다.
        /// </summary>
        private void ReleaseGeneratedSprite()
        {
            if (_generatedSprite == null) return;

            // 아직 팔로워에 할당되어 있다면 참조 해제
            if (_followerImage != null && _followerImage.sprite == _generatedSprite)
                _followerImage.sprite = null;
            if (_followerSpriteRenderer != null && _followerSpriteRenderer.sprite == _generatedSprite)
                _followerSpriteRenderer.sprite = null;

            if (Application.isPlaying)
                Destroy(_generatedSprite);
            else
                DestroyImmediate(_generatedSprite);

            _generatedSprite = null;
        }
    }
}