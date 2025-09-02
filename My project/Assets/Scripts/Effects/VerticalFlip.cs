using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace CAT.Effects
{
    [AddComponentMenu("CAT/Effects/VerticalFlip")]
    [DisallowMultipleComponent]
    public class VerticalFlip : MonoBehaviour
    {
        [Header("스프라이트 설정")]
        public Sprite firstSprite;
        public Sprite secondSprite;

        [Header("플립 애니메이션 설정")]
        [Range(1, 50)]
        public int sliceCount = 6;
        [Range(0.1f, 2.0f)]
        public float flipDuration = 0.2f;
        [Range(0f, 1f)]
        public float flipOffsetBetweenSlices = 0.1f;
        [Range(0.5f, 10f)]
        public float timeBetweenFlips = 3f;

        [Header("라인 설정")]
        public bool showColumnLines = true;
        public Color lineColor = Color.black;
        [Range(0.001f, 0.05f)]
        public float lineWidth = 0.05f;

        [Header("성능 최적화")]
        [Tooltip("모바일에서 성능 향상을 위해 애니메이션 중 프레임 스킵 허용")]
        public bool allowFrameSkipping = true;
        [Tooltip("모바일에서 저사양 모드 사용")]
        public bool useLowQualityOnMobile = true;

        // 내부 변수
        private SpriteRenderer spriteRenderer;
        private Image uiImage;
        private Material flipMaterial;
        private int currentSpriteIndex = 0; // 0: 첫 번째, 1: 두 번째
        private bool isAnimating = false;
        private Coroutine animationCoroutine;
        private bool isUI = false;

        // 마스킹 관련 컴포넌트 (UI 전용)
        private Mask uiMask;
        private RectMask2D rectMask2D;

        // 캐싱된 속성 ID
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int SecondTexProperty = Shader.PropertyToID("_SecondTex");
        private static readonly int FlipProgressProperty = Shader.PropertyToID("_FlipProgress");
        private static readonly int SliceCountProperty = Shader.PropertyToID("_SliceCount");
        private static readonly int FlipDurationProperty = Shader.PropertyToID("_FlipDuration");
        private static readonly int FlipOffsetProperty = Shader.PropertyToID("_FlipOffset");
        private static readonly int ShowLinesProperty = Shader.PropertyToID("_ShowLines");
        private static readonly int LineColorProperty = Shader.PropertyToID("_LineColor");
        private static readonly int LineWidthProperty = Shader.PropertyToID("_LineWidth");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private void Awake()
        {
            // 컴포넌트 자동 감지
            spriteRenderer = GetComponent<SpriteRenderer>();
            uiImage = GetComponent<Image>();

            // UI와 스프라이트 중 하나만 지원
            if (spriteRenderer != null && uiImage != null)
            {
                Debug.LogWarning("같은 오브젝트에 SpriteRenderer와 Image 컴포넌트가 모두 있습니다. Image(UI)가 우선 적용됩니다.");
            }

            // 컴포넌트 유효성 검사 및 타입 결정
            if (uiImage != null)
            {
                isUI = true;
                // UI 관련 컴포넌트 참조
                uiMask = GetComponent<Mask>();
                rectMask2D = GetComponent<RectMask2D>();
            }
            else if (spriteRenderer != null)
            {
                isUI = false;
            }
            else
            {
                Debug.LogError("SpriteRenderer 또는 Image 컴포넌트가 필요합니다!");
                enabled = false;
                return;
            }

            // 모바일 최적화
            if (Application.isMobilePlatform && useLowQualityOnMobile)
            {
                sliceCount = Mathf.Min(sliceCount, 12);
            }
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
            StartAnimationSequence();
        }

        private void OnDisable()
        {
            StopAnimationSequence();
        }

        private void InitializeIfNeeded()
        {
            // 스프라이트 유효성 검사
            if (firstSprite == null || secondSprite == null)
            {
                Debug.LogError("두 스프라이트를 모두 할당해야 합니다!");
                enabled = false;
                return;
            }

            // 셰이더 및 머티리얼 초기화
            if (flipMaterial == null)
            {
                Shader flipShader;

                if (isUI)
                {
                    // UI용 셰이더 사용
                    flipShader = Shader.Find("CAT/UI/VerticalFlipUI");
                }
                else
                {
                    // 스프라이트용 셰이더 사용
                    flipShader = Shader.Find("CAT/Effects/VerticalFlipSprite");
                }

                if (flipShader == null)
                {
                    Debug.LogError($"셰이더를 찾을 수 없습니다: {(isUI ? "CAT/UI/VerticalFlipUI" : "CAT/Effects/VerticalFlipSprite")}");
                    enabled = false;
                    return;
                }

                flipMaterial = new Material(flipShader);

                // 컴포넌트에 머티리얼 적용
                if (isUI)
                {
                    // UI 이미지에 머티리얼 적용
                    // 기존 머티리얼 스텐실 속성 복사 (마스킹 지원)
                    if (uiImage.material != null)
                    {
                        CopyMaterialStencilProperties(uiImage.material, flipMaterial);
                    }
                    uiImage.material = flipMaterial;
                }
                else
                {
                    // 스프라이트에 머티리얼 적용
                    spriteRenderer.material = flipMaterial;
                }
            }

            // 현재 스프라이트 설정
            if (isUI)
            {
                uiImage.sprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
            }
            else
            {
                spriteRenderer.sprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
            }

            // 머티리얼 초기 설정
            SetupMaterial();

            // UI일 경우 캔버스 갱신
            if (isUI)
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        private void CopyMaterialStencilProperties(Material sourceMaterial, Material targetMaterial)
        {
            if (sourceMaterial != null && targetMaterial != null)
            {
                // 스텐실 관련 프로퍼티들 복사
                if (sourceMaterial.HasProperty("_StencilComp"))
                    targetMaterial.SetFloat("_StencilComp", sourceMaterial.GetFloat("_StencilComp"));

                if (sourceMaterial.HasProperty("_Stencil"))
                    targetMaterial.SetFloat("_Stencil", sourceMaterial.GetFloat("_Stencil"));

                if (sourceMaterial.HasProperty("_StencilOp"))
                    targetMaterial.SetFloat("_StencilOp", sourceMaterial.GetFloat("_StencilOp"));

                if (sourceMaterial.HasProperty("_StencilWriteMask"))
                    targetMaterial.SetFloat("_StencilWriteMask", sourceMaterial.GetFloat("_StencilWriteMask"));

                if (sourceMaterial.HasProperty("_StencilReadMask"))
                    targetMaterial.SetFloat("_StencilReadMask", sourceMaterial.GetFloat("_StencilReadMask"));

                if (sourceMaterial.HasProperty("_ColorMask"))
                    targetMaterial.SetFloat("_ColorMask", sourceMaterial.GetFloat("_ColorMask"));
            }
        }

        private void SetupMaterial()
        {
            // 머티리얼 기본 설정
            // 애니메이션 매개변수 설정
            flipMaterial.SetFloat(SliceCountProperty, sliceCount);
            flipMaterial.SetFloat(FlipDurationProperty, flipDuration);
            flipMaterial.SetFloat(FlipOffsetProperty, flipOffsetBetweenSlices);
            flipMaterial.SetFloat(FlipProgressProperty, 0);

            if (isUI)
            {
                // UI 이미지 모드 설정
                uiImage.type = Image.Type.Simple;

                // 이미지 색상 유지
                flipMaterial.SetColor(ColorProperty, uiImage.color);

                // 현재 스프라이트 텍스처 설정
                Texture2D currentTexture = (currentSpriteIndex == 0) ? GetSpriteTextureForUI(firstSprite) : GetSpriteTextureForUI(secondSprite);
                flipMaterial.SetTexture(MainTexProperty, currentTexture);
                flipMaterial.SetTexture(SecondTexProperty, currentTexture);

                // 마스킹 관련 설정
                UpdateMaskingSettings();
            }
            else
            {
                // 스프라이트용 텍스처 설정
                Texture2D currentTexture = (currentSpriteIndex == 0) ? firstSprite.texture : secondSprite.texture;
                flipMaterial.SetTexture(MainTexProperty, currentTexture);
                flipMaterial.SetTexture(SecondTexProperty, currentTexture);
            }

            // 라인 속성 업데이트
            UpdateLineProperties();

            // UI 변경사항 즉시 적용을 위한 그래픽 강제 업데이트
            if (isUI && uiImage != null)
            {
                uiImage.SetMaterialDirty();
                uiImage.SetVerticesDirty();
            }
        }

        private void UpdateMaskingSettings()
        {
            // RectMask2D 사용 중이면 특별한 설정 필요
            if (rectMask2D != null && rectMask2D.enabled)
            {
                // RectMask2D 사용 시 클리핑 처리를 위한 설정
                flipMaterial.EnableKeyword("UNITY_UI_CLIP_RECT");
            }

            // 일반 Mask 사용 중이면 스텐실 설정 필요
            if (uiMask != null && uiMask.enabled && uiMask.showMaskGraphic)
            {
                // 마스크 그래픽 표시 모드일 때의 설정
                // 여기서는 기본 스텐실 설정을 사용
            }
        }

        private Texture2D GetSpriteTextureForUI(Sprite sprite)
        {
            // UI 이미지용 스프라이트에서 텍스처 가져오기
            if (sprite != null && sprite.texture != null)
            {
                return sprite.texture;
            }

            Debug.LogWarning("스프라이트에서 텍스처를 가져올 수 없습니다!");
            return Texture2D.whiteTexture;
        }

        private void UpdateLineProperties()
        {
            if (flipMaterial != null)
            {
                flipMaterial.SetFloat(ShowLinesProperty, showColumnLines ? 1.0f : 0.0f);
                flipMaterial.SetColor(LineColorProperty, lineColor);
                flipMaterial.SetFloat(LineWidthProperty, lineWidth);
            }
        }

        private void StartAnimationSequence()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            animationCoroutine = StartCoroutine(AnimationSequence());
        }

        private void StopAnimationSequence()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            isAnimating = false;
        }

        private IEnumerator AnimationSequence()
        {
            // 초기 대기
            yield return new WaitForSeconds(timeBetweenFlips);

            while (true)
            {
                // 다음 스프라이트 인덱스 계산
                int nextSpriteIndex = 1 - currentSpriteIndex; // 0 <-> 1 전환

                // 현재 스프라이트에서 다음 스프라이트로 플립 준비
                Sprite fromSprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
                Sprite toSprite = (nextSpriteIndex == 0) ? firstSprite : secondSprite;

                // 플립 애니메이션 준비
                if (isUI)
                {
                    flipMaterial.SetTexture(MainTexProperty, GetSpriteTextureForUI(fromSprite));
                    flipMaterial.SetTexture(SecondTexProperty, GetSpriteTextureForUI(toSprite));
                }
                else
                {
                    flipMaterial.SetTexture(MainTexProperty, fromSprite.texture);
                    flipMaterial.SetTexture(SecondTexProperty, toSprite.texture);
                }

                flipMaterial.SetFloat(FlipProgressProperty, 0);

                // 애니메이션 실행
                yield return StartCoroutine(PerformFlipAnimation());

                // 애니메이션 완료 후 상태 업데이트
                currentSpriteIndex = nextSpriteIndex;

                if (isUI)
                {
                    uiImage.sprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
                }
                else
                {
                    spriteRenderer.sprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
                }

                // 다음 플립까지 대기
                yield return new WaitForSeconds(timeBetweenFlips);
            }
        }

        private IEnumerator PerformFlipAnimation()
        {
            isAnimating = true;
            float elapsedTime = 0;
            float totalDuration = flipDuration + (sliceCount * flipOffsetBetweenSlices);

            // 모바일에서 프레임 스킵 계산 (성능 최적화)
            int frameSkip = (Application.isMobilePlatform && allowFrameSkipping) ? 1 : 0;
            int frameCounter = 0;

            // 애니메이션 진행
            while (elapsedTime < totalDuration)
            {
                elapsedTime += Time.deltaTime;

                // 프레임 스킵 적용 (모바일 최적화)
                if (frameCounter <= 0)
                {
                    flipMaterial.SetFloat(FlipProgressProperty, elapsedTime);
                    frameCounter = frameSkip;

                    // UI일 경우 레이아웃 갱신 (마스킹이 제대로 작동하도록)
                    if (isUI)
                    {
                        LayoutRebuilder.MarkLayoutForRebuild(uiImage.rectTransform);
                    }
                }
                else
                {
                    frameCounter--;
                }

                yield return null;
            }

            // 최종 상태 설정
            flipMaterial.SetFloat(FlipProgressProperty, totalDuration);
            isAnimating = false;

            // UI일 경우 캔버스 최종 갱신
            if (isUI)
            {
                Canvas.ForceUpdateCanvases();
            }
        }

#if UNITY_EDITOR
        // 에디터에서만 실행되는 코드
        private void OnValidate()
        {
            if (flipMaterial != null)
            {
                flipMaterial.SetFloat(SliceCountProperty, sliceCount);
                flipMaterial.SetFloat(FlipDurationProperty, flipDuration);
                flipMaterial.SetFloat(FlipOffsetProperty, flipOffsetBetweenSlices);
                UpdateLineProperties();
            }
        }
#endif

        private void OnDestroy()
        {
            // 머티리얼 정리
            if (flipMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(flipMaterial);
                else
                    DestroyImmediate(flipMaterial);
            }
        }

        // UI 이미지 색상 업데이트 (UI 색상 변경 지원)
        public void UpdateImageColor(Color newColor)
        {
            if (isUI && uiImage != null)
            {
                uiImage.color = newColor;

                if (flipMaterial != null)
                {
                    flipMaterial.SetColor(ColorProperty, newColor);
                }
            }
        }

        // 퍼블릭 메서드 - 라인 설정
        public void SetColumnLines(bool show, Color color, float width)
        {
            showColumnLines = show;
            lineColor = color;
            lineWidth = Mathf.Clamp(width, 0.001f, 0.05f);

            if (flipMaterial != null)
            {
                UpdateLineProperties();
            }
        }

        // 런타임에 스프라이트 변경
        public void SetSprites(Sprite newFirstSprite, Sprite newSecondSprite)
        {
            if (newFirstSprite != null)
                firstSprite = newFirstSprite;

            if (newSecondSprite != null)
                secondSprite = newSecondSprite;

            // 애니메이션 중이 아닐 때만 현재 스프라이트 업데이트
            if (!isAnimating && flipMaterial != null)
            {
                if (isUI)
                {
                    uiImage.sprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
                    Texture2D currentTexture = GetSpriteTextureForUI(uiImage.sprite);
                    flipMaterial.SetTexture(MainTexProperty, currentTexture);
                    flipMaterial.SetTexture(SecondTexProperty, currentTexture);
                }
                else
                {
                    spriteRenderer.sprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
                    Texture2D currentTexture = spriteRenderer.sprite.texture;
                    flipMaterial.SetTexture(MainTexProperty, currentTexture);
                    flipMaterial.SetTexture(SecondTexProperty, currentTexture);
                }
            }
        }

        // 외부에서 애니메이션 제어
        public void StartFlipping()
        {
            if (!isAnimating)
            {
                StartAnimationSequence();
            }
        }

        public void StopFlipping()
        {
            StopAnimationSequence();
        }
    }
}