using System.Collections;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// 단일 SpriteRenderer에 Color Lerp와 Dissolve를 적용하는 통합 컴포넌트.
    /// 파츠로 분절된 캐릭터처럼 여러 스프라이트를 한 번에 다루려면 SpriteGroupEffect를 쓴다.
    ///
    /// 성능 설계
    /// - 머티리얼은 전역에서 2개(디졸브 on/off)만 만들어 모든 인스턴스가 공유하고,
    ///   값은 MaterialPropertyBlock으로 렌더러마다 따로 준다. 인스턴스가 늘어도 머티리얼은 늘지 않는다.
    /// - 두 효과가 모두 0이면 원본 머티리얼로 되돌려 두므로 효과가 꺼진 동안의 상시 비용이 없다.
    /// - 디졸브가 켜져 있는 동안에만 정적 드라이버에 등록된다. (스프라이트 교체 추적용)
    ///   컬러 Lerp만 쓰는 동안에는 Update 계열 호출이 전혀 없다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("CAT/Effects/SpriteEffect")]
    public class SpriteEffect : MonoBehaviour, ISpriteEffectTickable
    {
        /// <summary>SpriteGroupEffect와 공유하는 통합 셰이더.</summary>
        public const string ShaderName = "CAT/Effects/SpriteEffect";

        private const string DissolveKeyword = "_CAT_DISSOLVE";

        [Header("Color Lerp")]
        [Tooltip("Lerp 1일 때의 색. 알파는 무시하고 RGB만 보간한다.")]
        [SerializeField] private Color targetColor = Color.white;

        [SerializeField, Range(0f, 1f)] private float lerpValue = 0f;

        [Header("Dissolve")]
        [SerializeField] private Texture2D dissolveTex;

        [Tooltip("스프라이트 전체 크기를 1로 봤을 때의 타일 수.")]
        [SerializeField] private Vector2 dissolveScale = Vector2.one;

        [SerializeField, Range(0f, 1f)] private float threshold = 0f;

        [Tooltip("스프라이트 종횡비에 맞춰 디졸브 패턴이 늘어나지 않도록 보정한다.")]
        [SerializeField] private bool matchSpriteAspect = true;

        // 원본 머티리얼. 컴포넌트 제거/도메인 리로드 이후에도 되돌릴 수 있어야 하므로 직렬화한다.
        [SerializeField, HideInInspector] private Material originalMaterial;

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;

        private bool materialAssigned;
        private bool registered;

        // 스프라이트 단위 캐시 (아틀라스 UV 영역 / 종횡비 보정)
        private Sprite cachedSprite;
        private Vector4 spriteUVRect = new Vector4(0f, 0f, 1f, 1f);
        private Vector2 aspectCorrection = Vector2.one;

        // 실제로 기록한 값. 인스펙터/Animator가 덮어쓰는 필드와 분리해야 갱신 누락이 없다.
        private Color appliedColor;
        private float appliedLerp = -1f;
        private Texture2D appliedTex;
        private Vector2 appliedTiling = Vector2.negativeInfinity;
        private Vector4 appliedRect;
        private float appliedThreshold = -1f;
        private bool hasApplied;

        private Coroutine colorRoutine;
        private Coroutine dissolveRoutine;

        // 인스턴스가 몇 개든 머티리얼은 이 둘뿐이다. 값은 MaterialPropertyBlock으로 개별 지정한다.
        private static Material sharedPlainMaterial;
        private static Material sharedDissolveMaterial;
        private static Shader cachedShader;
        private static bool shaderMissingLogged;

        private static readonly int TargetColorId = Shader.PropertyToID("_TargetColor");
        private static readonly int LerpValueId = Shader.PropertyToID("_LerpValue");
        private static readonly int DissolveTexId = Shader.PropertyToID("_DissolveTex");
        private static readonly int DissolveScaleId = Shader.PropertyToID("_DissolveScale");
        private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");

        #region Public API

        public Color TargetColor
        {
            get => targetColor;
            set
            {
                if (targetColor == value)
                    return;

                targetColor = value;
                Apply();
            }
        }

        /// <summary>0이면 원본 색, 1이면 TargetColor.</summary>
        public float LerpValue
        {
            get => lerpValue;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (lerpValue == clamped)
                    return;

                lerpValue = clamped;
                Apply();
            }
        }

        public Texture2D DissolveTex
        {
            get => dissolveTex;
            set
            {
                if (dissolveTex == value)
                    return;

                dissolveTex = value;
                Apply();
            }
        }

        /// <summary>스프라이트 전체 크기를 1로 봤을 때의 타일 수.</summary>
        public Vector2 DissolveScale
        {
            get => dissolveScale;
            set
            {
                if (dissolveScale == value)
                    return;

                dissolveScale = value;
                Apply();
            }
        }

        /// <summary>0이면 원본, 1이면 완전히 사라진다.</summary>
        public float Threshold
        {
            get => threshold;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (threshold == clamped)
                    return;

                threshold = clamped;
                Apply();
            }
        }

        public bool MatchSpriteAspect
        {
            get => matchSpriteAspect;
            set
            {
                if (matchSpriteAspect == value)
                    return;

                matchSpriteAspect = value;
                Apply();
            }
        }

        /// <summary>두 효과 중 하나라도 켜져 있는지. (머티리얼이 교체된 상태인지)</summary>
        public bool IsActive => lerpValue > 0f || threshold > 0f;

        /// <summary>현재 SpriteRenderer. 인스펙터 확장에서 사용한다.</summary>
        public SpriteRenderer Renderer
        {
            get
            {
                InitializeComponents();
                return spriteRenderer;
            }
        }

        public void SetLerpValue(float value) => LerpValue = value;

        public void SetTargetColor(Color color) => TargetColor = color;

        public void SetThreshold(float value) => Threshold = value;

        public void SetTargetColorAndLerp(Color color, float lerp)
        {
            targetColor = color;
            lerpValue = Mathf.Clamp01(lerp);
            Apply();
        }

        /// <summary>첫 연출에서 셰이더 변형 컴파일 히칭이 걱정되면 스폰 시점에 미리 호출한다.</summary>
        public void Prewarm()
        {
            EnsureSharedMaterials();
        }

        /// <summary>머티리얼을 원본으로 되돌린다. (효과 강제 해제)</summary>
        public void RestoreOriginalMaterial()
        {
            RestoreMaterial();
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            InitializeComponents();

            hasApplied = false;
            cachedSprite = null;

            // 효과가 켜진 채로 저장/리로드되어 머티리얼 참조가 null로 남았다면 먼저 복구한다.
            RestoreMaterial(true);

            Apply();
        }

        private void OnDisable()
        {
            colorRoutine = null;
            dissolveRoutine = null;

            Unregister();
            RestoreMaterial();
        }

        private void OnDestroy()
        {
            Unregister();
            RestoreMaterial();
        }

        /// <summary>Animator/Animation이 필드를 기록한 직후 Unity가 호출한다.</summary>
        private void OnDidApplyAnimationProperties()
        {
            Apply();
        }

#if UNITY_EDITOR
        // 에디터 전용. 메서드 자체를 #if로 감싸야 빌드에 빈 Update가 남지 않는다.
        private void Update()
        {
            if (!Application.isPlaying)
                Apply();
        }

        private void OnValidate()
        {
            lerpValue = Mathf.Clamp01(lerpValue);
            threshold = Mathf.Clamp01(threshold);
            cachedSprite = null;

            // OnValidate 컨텍스트에서 렌더러를 직접 건드리면 경고가 나므로 다음 에디터 틱으로 미룬다.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;

                Apply();
            };
        }
#endif

        #endregion

        #region Core

        private void InitializeComponents()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>현재 값에 맞춰 머티리얼 적용/해제와 프로퍼티 기록을 수행한다.</summary>
        public void Apply()
        {
            InitializeComponents();

            if (spriteRenderer == null)
                return;

            bool dissolveActive = threshold > 0f;
            bool anyActive = dissolveActive || lerpValue > 0f;

            // 둘 다 0이면 시각적으로 무효과이므로 원본 머티리얼로 되돌린다.
            if (!isActiveAndEnabled || !anyActive)
            {
                Unregister();
                RestoreMaterial();
                appliedColor = targetColor;
                appliedLerp = lerpValue;
                appliedThreshold = threshold;
                hasApplied = true;
                return;
            }

            if (!EnsureSharedMaterials())
                return;

            AssignMaterial(dissolveActive);

            if (dissolveActive)
                RefreshSpriteData();

            Vector2 correction = matchSpriteAspect ? aspectCorrection : Vector2.one;
            Vector2 tiling = new Vector2(dissolveScale.x * correction.x, dissolveScale.y * correction.y);

            bool changed = !hasApplied
                           || appliedColor != targetColor
                           || appliedLerp != lerpValue;

            if (dissolveActive)
            {
                changed = changed
                          || !ReferenceEquals(appliedTex, dissolveTex)
                          || appliedTiling != tiling
                          || appliedRect != spriteUVRect
                          || appliedThreshold != threshold;
            }

            if (changed)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(TargetColorId, targetColor);
                propertyBlock.SetFloat(LerpValueId, lerpValue);

                if (dissolveActive)
                {
                    // MaterialPropertyBlock.SetTexture는 null을 허용하지 않는다. 비어 있으면 셰이더 기본값을 쓴다.
                    if (dissolveTex != null)
                        propertyBlock.SetTexture(DissolveTexId, dissolveTex);

                    propertyBlock.SetVector(DissolveScaleId, new Vector4(tiling.x, tiling.y, 0f, 0f));
                    propertyBlock.SetVector(SpriteRectId, spriteUVRect);
                    propertyBlock.SetFloat(ThresholdId, threshold);
                }

                spriteRenderer.SetPropertyBlock(propertyBlock);

                appliedColor = targetColor;
                appliedLerp = lerpValue;
                appliedTex = dissolveTex;
                appliedTiling = tiling;
                appliedRect = spriteUVRect;
                appliedThreshold = threshold;
                hasApplied = true;
            }

            // 스프라이트 교체(스프라이트 시트 애니메이션)를 따라가야 하므로 디졸브 중에만 등록한다.
            if (dissolveActive)
                Register();
            else
                Unregister();
        }

        /// <summary>SpriteEffectUpdater가 디졸브 진행 중에만 프레임당 한 번 호출한다.</summary>
        void ISpriteEffectTickable.Tick()
        {
            Apply();
        }

        /// <summary>스프라이트가 바뀌면 아틀라스 UV 영역과 종횡비 보정값을 다시 계산한다.</summary>
        private void RefreshSpriteData()
        {
            Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            if (ReferenceEquals(sprite, cachedSprite))
                return;

            cachedSprite = sprite;

            if (sprite == null)
            {
                spriteUVRect = new Vector4(0f, 0f, 1f, 1f);
                aspectCorrection = Vector2.one;
                return;
            }

            spriteUVRect = CalculateSpriteUVRect(sprite);

            float width = sprite.rect.width;
            float height = sprite.rect.height;

            if (width > 0f && height > 0f)
            {
                float aspect = width / height;
                aspectCorrection = aspect >= 1f
                    ? new Vector2(aspect, 1f)
                    : new Vector2(1f, 1f / aspect);
            }
            else
            {
                aspectCorrection = Vector2.one;
            }
        }

        /// <summary>
        /// 스프라이트가 아틀라스에서 차지하는 UV 영역을 구한다.
        /// Sprite.textureRect는 Tight 패킹에서 예외를 던지므로 실제 메시 UV의 바운딩 박스를 사용한다.
        /// </summary>
        private static Vector4 CalculateSpriteUVRect(Sprite sprite)
        {
            Vector2[] uvs = sprite.uv;
            if (uvs == null || uvs.Length == 0)
                return new Vector4(0f, 0f, 1f, 1f);

            Vector2 min = uvs[0];
            Vector2 max = uvs[0];

            for (int i = 1; i < uvs.Length; i++)
            {
                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
            }

            Vector2 size = max - min;
            if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon)
                return new Vector4(0f, 0f, 1f, 1f);

            return new Vector4(min.x, min.y, size.x, size.y);
        }

        private static bool EnsureSharedMaterials()
        {
            if (sharedPlainMaterial != null && sharedDissolveMaterial != null)
                return true;

            if (cachedShader == null)
                cachedShader = Shader.Find(ShaderName);

            if (cachedShader == null)
            {
                if (!shaderMissingLogged)
                {
                    shaderMissingLogged = true;
                    Debug.LogError(
                        $"[SpriteEffect] 셰이더 '{ShaderName}'를 찾을 수 없습니다. " +
                        "Project Settings > Graphics > Always Included Shaders 에 등록하세요.");
                }
                return false;
            }

            if (sharedPlainMaterial == null)
            {
                sharedPlainMaterial = new Material(cachedShader)
                {
                    name = "SpriteEffect (Shared)",
                    hideFlags = HideFlags.DontSave
                };
            }

            if (sharedDissolveMaterial == null)
            {
                sharedDissolveMaterial = new Material(cachedShader)
                {
                    name = "SpriteEffect Dissolve (Shared)",
                    hideFlags = HideFlags.DontSave
                };
                sharedDissolveMaterial.EnableKeyword(DissolveKeyword);
            }

            return true;
        }

        private void AssignMaterial(bool dissolveActive)
        {
            Material wanted = dissolveActive ? sharedDissolveMaterial : sharedPlainMaterial;

            if (spriteRenderer.sharedMaterial == wanted)
            {
                materialAssigned = true;
                return;
            }

            if (!materialAssigned)
            {
                // 공유 머티리얼(DontSave)이나 사라진 참조를 원본으로 캡처하면 원복이 깨진다.
                Material current = spriteRenderer.sharedMaterial;
                bool unreliable = current == null || (current.hideFlags & HideFlags.DontSave) != 0;

                if (!unreliable)
                    originalMaterial = current;
            }

            spriteRenderer.sharedMaterial = wanted;
            materialAssigned = true;
        }

        /// <summary>
        /// 머티리얼을 원본으로 되돌린다.
        /// force는 도메인 리로드/씬 저장 복구용이다. 공유 머티리얼은 DontSave라 씬에 저장되지 않으므로,
        /// 효과가 켜진 채로 저장/리로드되면 렌더러의 머티리얼 참조가 null로 남는다. 그 상태를 복구한다.
        /// </summary>
        private void RestoreMaterial(bool force = false)
        {
            if (!force && !materialAssigned)
                return;

            materialAssigned = false;

            if (spriteRenderer == null)
                return;

            Material current = spriteRenderer.sharedMaterial;
            bool ours = current == null
                        || current == sharedPlainMaterial
                        || current == sharedDissolveMaterial;

            // 다른 시스템이 그 사이에 머티리얼을 바꿨다면 존중한다.
            if (ours)
                spriteRenderer.sharedMaterial = originalMaterial;
        }

        private void Register()
        {
            if (registered)
                return;

            SpriteEffectUpdater.Register(this);
            registered = true;
        }

        private void Unregister()
        {
            if (!registered)
                return;

            SpriteEffectUpdater.Unregister(this);
            registered = false;
        }

        #endregion

        #region Convenience

        /// <summary>duration 동안 컬러 Lerp를 1까지 올린다. (하얗게 태우기)</summary>
        public void BurnOut(float duration) => LerpColorTo(1f, duration);

        /// <summary>duration 동안 컬러 Lerp를 0까지 내린다. (원래 색으로 복귀)</summary>
        public void RestoreColor(float duration) => LerpColorTo(0f, duration);

        /// <summary>duration 동안 디졸브를 1까지 올린다. (사라짐)</summary>
        public void Dissolve(float duration) => DissolveTo(1f, duration);

        /// <summary>duration 동안 디졸브를 0까지 내린다. (복구)</summary>
        public void Undissolve(float duration) => DissolveTo(0f, duration);

        /// <summary>컬러 Lerp 트윈. 디졸브 트윈과 독립적으로 동작한다.</summary>
        public void LerpColorTo(float target, float duration)
        {
            target = Mathf.Clamp01(target);

            if (!CanRunRoutine() || duration <= 0f)
            {
                LerpValue = target;
                return;
            }

            if (colorRoutine != null)
                StopCoroutine(colorRoutine);

            colorRoutine = StartCoroutine(ColorRoutine(target, duration));
        }

        /// <summary>디졸브 트윈. 컬러 Lerp 트윈과 독립적으로 동작한다.</summary>
        public void DissolveTo(float target, float duration)
        {
            target = Mathf.Clamp01(target);

            if (!CanRunRoutine() || duration <= 0f)
            {
                Threshold = target;
                return;
            }

            if (dissolveRoutine != null)
                StopCoroutine(dissolveRoutine);

            dissolveRoutine = StartCoroutine(DissolveRoutine(target, duration));
        }

        private bool CanRunRoutine()
        {
            return Application.isPlaying && isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        private IEnumerator ColorRoutine(float target, float duration)
        {
            float start = lerpValue;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                LerpValue = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            LerpValue = target;
            colorRoutine = null;
        }

        private IEnumerator DissolveRoutine(float target, float duration)
        {
            float start = threshold;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Threshold = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            Threshold = target;
            dissolveRoutine = null;
        }

        #endregion

        private void Reset()
        {
            targetColor = Color.white;
            lerpValue = 0f;
            dissolveScale = Vector2.one;
            threshold = 0f;
            matchSpriteAspect = true;

            InitializeComponents();
            Apply();
        }
    }
}
