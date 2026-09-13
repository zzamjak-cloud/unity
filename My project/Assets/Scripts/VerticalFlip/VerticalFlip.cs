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
        [Tooltip("UV 기준 두께. 슬라이스 개수와 무관하게 일정한 굵기를 유지한다")]
        [Range(0.0005f, 0.02f)]
        public float lineWidth = 0.004f;

        [Header("성능 최적화")]
        [Tooltip("모바일에서 슬라이스 개수를 12개로 제한")]
        public bool useLowQualityOnMobile = true;

        // Shader.Find만 쓰면 셰이더가 빌드 의존성에 잡히지 않아 플레이어에서 스트리핑된다.
        // 직렬화된 참조를 들고 있어야 빌드에 포함된다
        [SerializeField, HideInInspector] private Shader spriteShader;
        [SerializeField, HideInInspector] private Shader uiShader;

        // 내부 변수
        private SpriteRenderer spriteRenderer;
        private Image uiImage;
        private Material flipMaterial;
        private Material originalMaterial;
        private bool hadCustomMaterial;
        private int currentSpriteIndex = 0; // 0: 첫 번째, 1: 두 번째
        private bool isAnimating = false;
        private Coroutine animationCoroutine;
        private bool isUI = false;

        // 인스펙터 값을 변조하지 않기 위해 런타임 적용값을 따로 둔다
        private int effectiveSliceCount = 6;

        // 사이클마다 new 하지 않도록 캐싱한다
        private WaitForSeconds cachedWait;
        private float cachedWaitSeconds = -1f;

        // 캐싱된 속성 ID
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int SecondTexProperty = Shader.PropertyToID("_SecondTex");
        private static readonly int MainTexRectProperty = Shader.PropertyToID("_MainTexRect");
        private static readonly int SecondTexRectProperty = Shader.PropertyToID("_SecondTexRect");
        private static readonly int FlipProgressProperty = Shader.PropertyToID("_FlipProgress");
        private static readonly int SliceCountProperty = Shader.PropertyToID("_SliceCount");
        private static readonly int FlipDurationProperty = Shader.PropertyToID("_FlipDuration");
        private static readonly int FlipOffsetProperty = Shader.PropertyToID("_FlipOffset");
        private static readonly int ShowLinesProperty = Shader.PropertyToID("_ShowLines");
        private static readonly int LineColorProperty = Shader.PropertyToID("_LineColor");
        private static readonly int LineWidthProperty = Shader.PropertyToID("_LineWidth");
        private const string ShowLinesKeyword = "_SHOWLINES_ON";
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        /// <summary>모바일 저사양 모드가 반영된 실제 슬라이스 개수</summary>
        public int EffectiveSliceCount => effectiveSliceCount;

        public const string SpriteShaderName = "CAT/Effects/VerticalFlipSprite";
        public const string UIShaderName = "CAT/UI/VerticalFlipUI";

        /// <summary>스프라이트가 텍스처(아틀라스) 안에서 차지하는 영역을 0~1 UV로 반환</summary>
        public static Vector4 GetSpriteAtlasRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Vector4(0f, 0f, 1f, 1f);

            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;
            return new Vector4(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height);
        }

        private void Awake()
        {
            if (!CacheComponentRefs(true))
            {
                enabled = false;
                return;
            }

            // 모바일 최적화 (직렬화 필드는 건드리지 않는다)
            RecomputeEffectiveSliceCount();
        }

        /// <summary>렌더러 참조와 UI/스프라이트 모드를 판정한다. 에디트 모드 프리뷰에서도 쓰인다</summary>
        private bool CacheComponentRefs(bool logErrors)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            uiImage = GetComponent<Image>();

            if (logErrors && spriteRenderer != null && uiImage != null)
            {
                Debug.LogWarning("같은 오브젝트에 SpriteRenderer와 Image 컴포넌트가 모두 있습니다. Image(UI)가 우선 적용됩니다.", this);
            }

            if (uiImage != null)
            {
                isUI = true;
                return true;
            }

            if (spriteRenderer != null)
            {
                isUI = false;
                return true;
            }

            if (logErrors)
                Debug.LogError("SpriteRenderer 또는 Image 컴포넌트가 필요합니다!", this);

            return false;
        }

        private void OnEnable()
        {
            if (!InitializeIfNeeded())
                return;

            StartAnimationSequence();
        }

        private void OnDisable()
        {
            StopAnimationSequence();
            ResetToCurrentSprite();
        }

        /// <summary>플립 도중 정지해 슬라이스가 깨진 채 남지 않도록 현재 스프라이트 상태로 되돌린다</summary>
        private void ResetToCurrentSprite()
        {
            if (flipMaterial == null)
                return;

            Sprite current = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
            SetFlipSprites(current, current);
            flipMaterial.SetFloat(FlipProgressProperty, 0f);
        }

        /// <summary>초기화 성공 여부. 실패하면 컴포넌트를 비활성화한다</summary>
        private bool InitializeIfNeeded()
        {
            // 스프라이트 유효성 검사
            if (firstSprite == null || secondSprite == null)
            {
                Debug.LogError("두 스프라이트를 모두 할당해야 합니다!", this);
                enabled = false;
                return false;
            }

            if (spriteRenderer == null && uiImage == null)
            {
                enabled = false;
                return false;
            }

            // 셰이더 및 머티리얼 초기화
            if (flipMaterial == null)
            {
                string shaderName = isUI ? UIShaderName : SpriteShaderName;
                Shader flipShader = isUI ? uiShader : spriteShader;

                // 직렬화 참조가 비어 있으면(구버전 컴포넌트 등) 이름으로 보완한다
                if (flipShader == null)
                    flipShader = Shader.Find(shaderName);

                if (flipShader == null)
                {
                    Debug.LogError($"셰이더를 찾을 수 없습니다: {shaderName}. 빌드에서 스트리핑되었을 수 있습니다.", this);
                    enabled = false;
                    return false;
                }

                // hideFlags를 주지 않으면 에디터가 직렬화를 시도하며 DontSaveInEditor Assertion이 난다
                flipMaterial = new Material(flipShader)
                {
                    name = $"{shaderName} (Instance)",
                    hideFlags = HideFlags.DontSave
                };

                if (isUI)
                {
                    // UGUI 마스킹은 StencilMaterial이 별도 변형을 만들어 처리하므로
                    // 여기서 스텐실 값을 복사할 필요가 없다.
                    // material getter는 미지정 시 defaultMaterial을 돌려주므로 원본 보유 여부를 따로 기억한다
                    hadCustomMaterial = uiImage.material != null && uiImage.material != uiImage.defaultMaterial;
                    originalMaterial = hadCustomMaterial ? uiImage.material : null;
                    uiImage.material = flipMaterial;
                }
                else
                {
                    originalMaterial = spriteRenderer.sharedMaterial;
                    hadCustomMaterial = originalMaterial != null;
                    spriteRenderer.material = flipMaterial;
                }
            }

            // 현재 스프라이트 설정
            ApplyCurrentSprite();

            // 머티리얼 초기 설정
            SetupMaterial();
            return true;
        }

        private void ApplyCurrentSprite()
        {
            ApplyDisplaySprite((currentSpriteIndex == 0) ? firstSprite : secondSprite);
        }

        private void ApplyDisplaySprite(Sprite sprite)
        {
            if (isUI)
            {
                if (uiImage != null && uiImage.sprite != sprite)
                    uiImage.sprite = sprite;
            }
            else if (spriteRenderer != null && spriteRenderer.sprite != sprite)
            {
                spriteRenderer.sprite = sprite;
            }
        }


        private void SetupMaterial()
        {
            // 애니메이션 매개변수 설정
            flipMaterial.SetFloat(SliceCountProperty, effectiveSliceCount);
            flipMaterial.SetFloat(FlipDurationProperty, flipDuration);
            flipMaterial.SetFloat(FlipOffsetProperty, flipOffsetBetweenSlices);
            flipMaterial.SetFloat(FlipProgressProperty, 0f);

            if (isUI)
            {
                if (uiImage.type != Image.Type.Simple)
                {
                    Debug.LogWarning($"Image.type을 {uiImage.type}에서 Simple로 변경합니다. 플립 효과는 Simple만 지원합니다.", this);
                    uiImage.type = Image.Type.Simple;
                }
                flipMaterial.SetColor(ColorProperty, Color.white); // 틴트는 버텍스 컬러로 전달된다
            }
            else
            {
                flipMaterial.SetColor(ColorProperty, Color.white);
            }

            // 시작 시점에는 양쪽 모두 현재 스프라이트로 맞춰 둔다
            Sprite current = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
            SetFlipSprites(current, current);

            // 라인 속성 업데이트
            UpdateLineProperties();

            if (isUI && uiImage != null)
            {
                uiImage.SetMaterialDirty();
            }
        }

        /// <summary>플립 양쪽 텍스처와 아틀라스 UV 영역을 셰이더에 전달</summary>
        private void SetFlipSprites(Sprite from, Sprite to)
        {
            if (flipMaterial == null || from == null || to == null)
                return;

            // UI는 CanvasRenderer가 _MainTex를 덮어쓰지만, 스프라이트/에디터 프리뷰 경로를 위해 함께 설정한다
            flipMaterial.SetTexture(MainTexProperty, from.texture);
            flipMaterial.SetTexture(SecondTexProperty, to.texture);
            flipMaterial.SetVector(MainTexRectProperty, GetSpriteAtlasRect(from));
            flipMaterial.SetVector(SecondTexRectProperty, GetSpriteAtlasRect(to));
        }

        private void UpdateLineProperties()
        {
            if (flipMaterial == null)
                return;

            // 키워드로 변형을 분리해 라인을 끄면 미분/라인 연산이 셰이더에서 통째로 빠진다
            flipMaterial.SetFloat(ShowLinesProperty, showColumnLines ? 1.0f : 0.0f);
            if (showColumnLines)
                flipMaterial.EnableKeyword(ShowLinesKeyword);
            else
                flipMaterial.DisableKeyword(ShowLinesKeyword);

            flipMaterial.SetColor(LineColorProperty, lineColor);
            flipMaterial.SetFloat(LineWidthProperty, lineWidth);
        }

        private void StartAnimationSequence()
        {
            StopAnimationSequence();
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

        /// <summary>마지막 슬라이스가 플립을 끝내는 시점</summary>
        private float TotalFlipDuration =>
            flipDuration + Mathf.Max(0, effectiveSliceCount - 1) * flipOffsetBetweenSlices;

        private WaitForSeconds GetWaitBetweenFlips()
        {
            if (cachedWait == null || !Mathf.Approximately(cachedWaitSeconds, timeBetweenFlips))
            {
                cachedWaitSeconds = timeBetweenFlips;
                cachedWait = new WaitForSeconds(timeBetweenFlips);
            }

            return cachedWait;
        }

        private IEnumerator AnimationSequence()
        {
            // 초기 대기
            yield return GetWaitBetweenFlips();

            while (true)
            {
                int nextSpriteIndex = 1 - currentSpriteIndex; // 0 <-> 1 전환

                Sprite fromSprite = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
                Sprite toSprite = (nextSpriteIndex == 0) ? firstSprite : secondSprite;

                // 플립 애니메이션 준비
                SetFlipSprites(fromSprite, toSprite);
                flipMaterial.SetFloat(FlipProgressProperty, 0f);

                // 중첩 코루틴은 사이클마다 이터레이터를 할당하므로 여기에 펼친다
                isAnimating = true;
                float elapsedTime = 0f;
                float totalDuration = TotalFlipDuration;

                // 애니메이션은 전적으로 셰이더에서 일어나므로 지오메트리/레이아웃 갱신이 필요 없다
                while (elapsedTime < totalDuration)
                {
                    elapsedTime += Time.deltaTime;
                    flipMaterial.SetFloat(FlipProgressProperty, elapsedTime);
                    yield return null;
                }

                flipMaterial.SetFloat(FlipProgressProperty, totalDuration);
                isAnimating = false;

                // 애니메이션 완료 후 상태 업데이트
                currentSpriteIndex = nextSpriteIndex;
                ApplyCurrentSprite();

                // 표시 스프라이트가 바뀌었으므로 양쪽을 새 기준으로 되돌린다
                ResetToCurrentSprite();

                // 다음 플립까지 대기
                yield return GetWaitBetweenFlips();
            }
        }


#if UNITY_EDITOR
        // ===== 씬 뷰 프리뷰 =====
        // 에디트 모드에서는 Awake/OnEnable/코루틴이 돌지 않으므로 에디터가 직접 구동한다.
        // 실제 렌더러 머티리얼을 교체하므로 반드시 EditorPreviewEnd로 원복해야 한다.

        private bool editorPreviewActive;
        private float editorPreviewTime;
        private int editorPreviewStartIndex;
        private float editorPreviewWait;

        public bool IsEditorPreviewActive => editorPreviewActive;

        /// <summary>현재 플립 구간인지(false면 다음 플립 대기 중)</summary>
        public bool EditorPreviewIsFlipping { get; private set; }

        /// <summary>현재 구간의 진행도 0~1</summary>
        public float EditorPreviewPhase { get; private set; }

        /// <summary>프리뷰에 실제로 적용 중인 대기 시간</summary>
        public float EditorPreviewWait => editorPreviewWait;

        /// <summary>플립 한 번에 걸리는 시간(마지막 슬라이스 종료 기준)</summary>
        public float EditorFlipDuration => TotalFlipDuration;

        /// <summary>대기 + 플립 한 사이클 길이</summary>
        public float EditorCycleDuration => editorPreviewWait + TotalFlipDuration;

        /// <summary>
        /// 씬 뷰 프리뷰 시작. 성공하면 true.
        /// waitSecondsOverride가 0 이상이면 그 값을 대기 시간으로 쓴다(프리뷰를 지루하지 않게 하려는 용도).
        /// 첫 플립은 대기 없이 곧바로 시작한다.
        /// </summary>
        public bool EditorPreviewBegin(float waitSecondsOverride = -1f)
        {
            if (Application.isPlaying)
                return false;

            if (firstSprite == null || secondSprite == null)
                return false;

            if (!CacheComponentRefs(false))
                return false;

            RecomputeEffectiveSliceCount();

            if (!EditorEnsureMaterial())
                return false;

            editorPreviewWait = (waitSecondsOverride >= 0f)
                ? waitSecondsOverride
                : timeBetweenFlips;

            editorPreviewActive = true;
            // 대기 구간을 건너뛴 지점에서 시작해 버튼을 누르자마자 플립이 보이게 한다
            editorPreviewTime = editorPreviewWait;
            editorPreviewStartIndex = currentSpriteIndex;
            EditorPreviewIsFlipping = true;
            EditorPreviewPhase = 0f;
            SetupMaterial();
            return true;
        }

        /// <summary>경과 시간만큼 프리뷰를 진행시킨다</summary>
        public void EditorPreviewTick(float deltaTime)
        {
            if (!editorPreviewActive || flipMaterial == null)
                return;

            editorPreviewTime += deltaTime;

            float cycle = EditorCycleDuration;
            if (cycle <= 0.0001f)
                return;

            int cycleIndex = Mathf.FloorToInt(editorPreviewTime / cycle);
            float t = editorPreviewTime - cycleIndex * cycle;

            // 사이클마다 앞/뒤 면이 뒤바뀐다
            int fromIndex = (editorPreviewStartIndex + cycleIndex) % 2;
            Sprite from = (fromIndex == 0) ? firstSprite : secondSprite;
            Sprite to = (fromIndex == 0) ? secondSprite : firstSprite;

            // UI는 CanvasRenderer가 _MainTex를 uiImage.sprite로 덮어쓰므로 표시 스프라이트를 from으로 유지한다
            ApplyDisplaySprite(from);

            if (t < editorPreviewWait)
            {
                // 대기 구간
                SetFlipSprites(from, from);
                flipMaterial.SetFloat(FlipProgressProperty, 0f);
                EditorPreviewIsFlipping = false;
                EditorPreviewPhase = (editorPreviewWait > 0f) ? Mathf.Clamp01(t / editorPreviewWait) : 1f;
            }
            else
            {
                float flipTime = t - editorPreviewWait;
                SetFlipSprites(from, to);
                flipMaterial.SetFloat(FlipProgressProperty, flipTime);
                EditorPreviewIsFlipping = true;
                float total = TotalFlipDuration;
                EditorPreviewPhase = (total > 0f) ? Mathf.Clamp01(flipTime / total) : 1f;
            }
        }

        /// <summary>정지 상태에서 진행도를 직접 지정한다(0 ~ EditorFlipDuration)</summary>
        public void EditorPreviewScrub(float progress)
        {
            if (Application.isPlaying || firstSprite == null || secondSprite == null)
                return;

            if (!CacheComponentRefs(false))
                return;

            RecomputeEffectiveSliceCount();

            if (!EditorEnsureMaterial())
                return;

            SetupMaterial();

            Sprite from = (currentSpriteIndex == 0) ? firstSprite : secondSprite;
            Sprite to = (currentSpriteIndex == 0) ? secondSprite : firstSprite;
            ApplyDisplaySprite(from);
            SetFlipSprites(from, to);
            flipMaterial.SetFloat(FlipProgressProperty, progress);
        }

        /// <summary>프리뷰 종료 및 머티리얼 원복. 에디터는 반드시 이걸 호출해야 한다</summary>
        public void EditorPreviewEnd()
        {
            editorPreviewActive = false;
            editorPreviewTime = 0f;
            EditorPreviewIsFlipping = false;
            EditorPreviewPhase = 0f;

            if (flipMaterial == null)
                return;

            ApplyDisplaySprite((currentSpriteIndex == 0) ? firstSprite : secondSprite);
            RestoreOriginalMaterial();

            DestroyImmediate(flipMaterial);
            flipMaterial = null;
        }

        /// <summary>에디트 모드용 머티리얼 확보(런타임 초기화와 달리 컴포넌트를 비활성화하지 않는다)</summary>
        private bool EditorEnsureMaterial()
        {
            if (flipMaterial != null)
                return true;

            string shaderName = isUI ? UIShaderName : SpriteShaderName;
            Shader flipShader = isUI ? uiShader : spriteShader;
            if (flipShader == null)
                flipShader = Shader.Find(shaderName);

            if (flipShader == null)
            {
                Debug.LogError($"셰이더를 찾을 수 없습니다: {shaderName}", this);
                return false;
            }

            flipMaterial = new Material(flipShader)
            {
                name = $"{shaderName} (Scene Preview)",
                hideFlags = HideFlags.DontSave
            };

            if (isUI)
            {
                hadCustomMaterial = uiImage.material != null && uiImage.material != uiImage.defaultMaterial;
                originalMaterial = hadCustomMaterial ? uiImage.material : null;
                uiImage.material = flipMaterial;
            }
            else
            {
                originalMaterial = spriteRenderer.sharedMaterial;
                hadCustomMaterial = originalMaterial != null;
                spriteRenderer.sharedMaterial = flipMaterial;
            }

            return true;
        }

        private void Reset()
        {
            CacheShaderReferences();
        }

        /// <summary>빌드에 셰이더가 포함되도록 에디터에서 참조를 직렬화해 둔다</summary>
        private void CacheShaderReferences()
        {
            if (spriteShader == null) spriteShader = Shader.Find(SpriteShaderName);
            if (uiShader == null) uiShader = Shader.Find(UIShaderName);
        }

        private void OnValidate()
        {
            CacheShaderReferences();
            RecomputeEffectiveSliceCount();

            if (flipMaterial != null)
            {
                flipMaterial.SetFloat(SliceCountProperty, effectiveSliceCount);
                flipMaterial.SetFloat(FlipDurationProperty, flipDuration);
                flipMaterial.SetFloat(FlipOffsetProperty, flipOffsetBetweenSlices);
                UpdateLineProperties();
            }
        }
#endif

        private void OnDestroy()
        {
            if (flipMaterial == null)
                return;

            RestoreOriginalMaterial();

            if (Application.isPlaying)
                Destroy(flipMaterial);
            else
                DestroyImmediate(flipMaterial);

            flipMaterial = null;
        }

        /// <summary>파괴된 머티리얼을 참조한 채로 남지 않도록 원래 머티리얼을 되돌린다</summary>
        private void RestoreOriginalMaterial()
        {
            if (isUI && uiImage != null)
                uiImage.material = hadCustomMaterial ? originalMaterial : null;
            else if (!isUI && spriteRenderer != null)
                spriteRenderer.sharedMaterial = originalMaterial;
        }

        // UI 이미지 색상 업데이트 (UI 색상 변경 지원)
        public void UpdateImageColor(Color newColor)
        {
            if (isUI && uiImage != null)
                uiImage.color = newColor;
        }

        /// <summary>인스펙터 값을 런타임에 바꾼 뒤 호출해 머티리얼에 반영한다</summary>
        public void ApplySettings()
        {
            RecomputeEffectiveSliceCount();

            if (flipMaterial != null)
                SetupMaterial();
        }

        private void RecomputeEffectiveSliceCount()
        {
            effectiveSliceCount = (Application.isMobilePlatform && useLowQualityOnMobile)
                ? Mathf.Min(sliceCount, 12)
                : sliceCount;
        }

        // 퍼블릭 메서드 - 라인 설정
        public void SetColumnLines(bool show, Color color, float width)
        {
            showColumnLines = show;
            lineColor = color;
            lineWidth = Mathf.Clamp(width, 0.0005f, 0.02f);

            UpdateLineProperties();
        }

        // 런타임에 스프라이트 변경
        public void SetSprites(Sprite newFirstSprite, Sprite newSecondSprite)
        {
            if (newFirstSprite != null)
                firstSprite = newFirstSprite;

            if (newSecondSprite != null)
                secondSprite = newSecondSprite;

            // 애니메이션 중이 아닐 때만 현재 스프라이트 업데이트
            if (isAnimating || flipMaterial == null)
                return;

            ApplyCurrentSprite();
            ResetToCurrentSprite();
        }

        // 외부에서 애니메이션 제어
        public void StartFlipping()
        {
            if (!isActiveAndEnabled || animationCoroutine != null)
                return;

            StartAnimationSequence();
        }

        public void StopFlipping()
        {
            StopAnimationSequence();
        }
    }
}
