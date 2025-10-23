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
        [Tooltip("따라갈 이미지가 있는 소스 게임 오브젝트입니다.")]
        public GameObject targetObject;

        // 소스(Target)의 컴포넌트 캐시
        private Image _sourceImage;
        private RawImage _sourceRawImage;
        private SpriteRenderer _sourceSpriteRenderer;

        // 팔로워(자신)의 컴포넌트 캐시
        private Image _followerImage;
        private RawImage _followerRawImage;
        private SpriteRenderer _followerSpriteRenderer;

        // 변화를 감지하기 위한 마지막 상태 저장 변수
        private Sprite _lastSprite;
        private Texture _lastTexture;
        private GameObject _lastTargetObject;

        private void OnEnable()
        {
            InitializeComponents();
        }

        private void LateUpdate()
        {
            // 타겟 오브젝트가 변경되었는지 확인 (에디터에서 드래그 앤 드롭 변경 등)
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
            if (gameObject.activeInHierarchy)
            {
                InitializeComponents();
                // 즉시 변경사항을 적용하기 위해 FollowImage 호출
                FollowImage();
            }
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
                Sprite newSprite = null;
                if (newTexture != null)
                {
                    // Texture2D로 변환 시도
                    Texture2D tex2D = newTexture as Texture2D;
                    if (tex2D != null)
                    {
                        newSprite = Sprite.Create(tex2D, new Rect(0, 0, tex2D.width, tex2D.height), new Vector2(0.5f, 0.5f));
                    }
                }

                if (_followerImage != null) _followerImage.sprite = newSprite;
                if (_followerSpriteRenderer != null) _followerSpriteRenderer.sprite = newSprite;
            }
        }
    }
}