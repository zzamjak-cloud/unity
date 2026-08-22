using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// 스프라이트 그룹(파츠로 분절된 캐릭터 등)에 Color Lerp와 Dissolve를 함께 적용하는 통합 컴포넌트.
    /// 하위에 SpriteRenderer가 하나뿐이어도 그대로 동작하므로 단일 스프라이트에도 쓸 수 있다.
    ///
    /// 성능 설계
    /// - 그룹당 머티리얼 인스턴스 1개를 자식 전체가 공유한다. 자식마다 MaterialPropertyBlock을 쓰면
    ///   렌더러 단위로 배칭이 끊기지만, 파라미터가 그룹 전체에 동일한 이 효과는 머티리얼 공유가 가능하다.
    /// - 두 효과가 모두 0이면 원본 머티리얼로 되돌려 두므로 효과가 꺼진 동안의 상시 비용이 없다.
    /// - 디졸브는 셰이더 키워드로 분리되어, 쓰지 않으면 텍스처 페치와 정점 변환이 아예 컴파일되지 않는다.
    /// - 디졸브 진행 중에만 정적 드라이버에 등록되어 프레임당 행렬 1개를 갱신한다.
    ///   컬러 Lerp만 쓰는 동안에는 Update 계열 호출이 전혀 없다.
    ///
    /// 디졸브 UV는 파츠 로컬이 아니라 그룹(이 컴포넌트) 로컬 공간을 쓴다.
    /// 파츠별 로컬 UV를 쓰면 팔·다리·몸통이 제각각 녹아버린다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("CAT/Effects/SpriteGroupEffect")]
    public class SpriteGroupEffect : MonoBehaviour, ISpriteEffectTickable
    {
        /// <summary>SpriteEffect(단일)와 공유하는 통합 셰이더.</summary>
        public const string ShaderName = "CAT/Effects/SpriteEffect";

        private const string DissolveKeyword = "_CAT_DISSOLVE";

        // 디졸브 UV를 그룹 로컬 공간에서 뽑도록 하는 키워드. 그룹 머티리얼에는 항상 켜 둔다.
        private const string GroupUVKeyword = "_CAT_GROUPUV";

        [Header("Color Lerp")]
        [Tooltip("Lerp 1일 때의 색. 알파는 무시하고 RGB만 보간한다.")]
        [SerializeField] private Color targetColor = Color.white;

        [SerializeField, Range(0f, 1f)] private float lerpValue = 0f;

        [Header("Dissolve")]
        [SerializeField] private Texture2D dissolveTex;

        [Tooltip("그룹 전체 크기를 1로 봤을 때의 타일 수. 1이면 캐릭터 전체에 패턴이 한 번 덮인다.")]
        [SerializeField] private Vector2 dissolveScale = Vector2.one;

        [SerializeField, Range(0f, 1f)] private float threshold = 0f;

        [Header("Group Settings")]
        [Tooltip("비활성 자식 렌더러도 대상에 포함한다.")]
        [SerializeField] private bool includeInactive = true;

        // 자식 목록과 원본 머티리얼. 컴포넌트 제거/도메인 리로드 이후에도 되돌릴 수 있어야 하므로 직렬화한다.
        [SerializeField, HideInInspector] private List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        [SerializeField, HideInInspector] private List<Material> originalMaterials = new List<Material>();

        // 그룹 전체가 공유하는 런타임 머티리얼. 씬에 저장되면 안 되므로 DontSave로 만든다.
        private Material groupMaterial;
        private bool materialAssigned;
        private bool cacheValid;
        private bool registered;
        private bool dissolveKeywordOn;

        // 그룹 로컬 바운즈를 0~1로 정규화하고 타일 스케일을 곱하는 고정 파트.
        // 캐릭터가 이동해도 패턴이 흐르지 않도록 매 프레임 worldToLocal만 곱해 준다.
        private Matrix4x4 uvConstPart = Matrix4x4.identity;
        private bool boundsValid;

        private Matrix4x4 lastWorldToLocal;
        private bool hasLastMatrix;

        // 실제로 머티리얼에 기록한 값. 인스펙터/Animator가 덮어쓰는 필드와 분리해야 갱신 누락이 없다.
        private Color appliedColor;
        private float appliedLerp = -1f;
        private Texture2D appliedTex;
        private float appliedThreshold = -1f;
        private bool hasApplied;

        private Coroutine colorRoutine;
        private Coroutine dissolveRoutine;

        // 계층 재수집용 스크래치. 재사용하므로 워밍업 이후 힙 할당이 없다.
        private readonly List<SpriteRenderer> scratchRenderers = new List<SpriteRenderer>();
        private readonly List<Material> scratchMaterials = new List<Material>();
        private readonly List<int> scratchIndices = new List<int>();

        private static Shader cachedShader;
        private static bool shaderMissingLogged;

        private static readonly int TargetColorId = Shader.PropertyToID("_TargetColor");
        private static readonly int LerpValueId = Shader.PropertyToID("_LerpValue");
        private static readonly int DissolveTexId = Shader.PropertyToID("_DissolveTex");
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int GroupMatrixId = Shader.PropertyToID("_GroupMatrix");

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
                appliedTex = null;
                Apply();
            }
        }

        /// <summary>그룹 전체 크기를 1로 봤을 때의 타일 수.</summary>
        public Vector2 DissolveScale
        {
            get => dissolveScale;
            set
            {
                if (dissolveScale == value)
                    return;

                dissolveScale = value;
                boundsValid = false;
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

        /// <summary>효과 대상 렌더러 수. 인스펙터 확장에서 사용한다.</summary>
        public int RendererCount => renderers.Count;

        /// <summary>두 효과 중 하나라도 켜져 있는지. (자식 머티리얼이 교체된 상태인지)</summary>
        public bool IsActive => lerpValue > 0f || threshold > 0f;

        public void SetLerpValue(float value) => LerpValue = value;

        public void SetTargetColor(Color color) => TargetColor = color;

        public void SetThreshold(float value) => Threshold = value;

        public void SetTargetColorAndLerp(Color color, float lerp)
        {
            targetColor = color;
            lerpValue = Mathf.Clamp01(lerp);
            Apply();
        }

        /// <summary>런타임에 자식을 붙였거나 머티리얼을 바꿨다면 호출한다.</summary>
        public void RefreshRenderers()
        {
            cacheValid = false;
            boundsValid = false;
            Apply();
        }

        /// <summary>현재 포즈 기준으로 디졸브 패턴 범위를 다시 계산한다.</summary>
        public void RecalculateBounds()
        {
            boundsValid = false;
            Apply();
        }

        /// <summary>
        /// 첫 연출에서 셰이더 변형 컴파일 히칭이 걱정되면 스폰 시점에 미리 호출한다.
        /// 머티리얼 인스턴스만 만들어 두고 자식에는 적용하지 않는다.
        /// </summary>
        public void Prewarm()
        {
            EnsureMaterial();
        }

        /// <summary>자식 머티리얼을 원본으로 되돌린다. (효과 강제 해제)</summary>
        public void RestoreOriginalMaterials()
        {
            RestoreMaterials();
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // 비활성 중에 계층이 바뀌었을 수 있으므로 캐시를 무효화한다.
            cacheValid = false;
            boundsValid = false;
            hasApplied = false;
            hasLastMatrix = false;

            // 효과가 켜진 채로 저장/리로드되어 자식 머티리얼이 null로 남았다면 먼저 복구한다.
            // 이걸 먼저 하지 않으면 아래 재수집에서 null을 원본으로 캡처해 버린다.
            RestoreMaterials(true);

            Apply();
        }

        private void OnDisable()
        {
            colorRoutine = null;
            dissolveRoutine = null;

            Unregister();
            RestoreMaterials();
            DestroyGroupMaterial();
        }

        private void OnDestroy()
        {
            Unregister();
            RestoreMaterials();
            DestroyGroupMaterial();
        }

        private void OnTransformChildrenChanged()
        {
            cacheValid = false;
            boundsValid = false;

            if (isActiveAndEnabled)
                Apply();
        }

        /// <summary>
        /// Animator/Animation이 필드를 기록한 직후 Unity가 호출한다.
        /// 매 프레임 폴링 없이 클립 구동을 처리하기 위한 진입점이다.
        /// </summary>
        private void OnDidApplyAnimationProperties()
        {
            Apply();
        }

#if UNITY_EDITOR
        // 에디터 전용. 메서드 자체를 #if로 감싸야 빌드에 빈 Update가 남지 않는다.
        private void Update()
        {
            if (!Application.isPlaying)
                ApplyIfChanged();
        }

        private void OnValidate()
        {
            lerpValue = Mathf.Clamp01(lerpValue);
            threshold = Mathf.Clamp01(threshold);
            cacheValid = false;
            boundsValid = false;

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

        private void ApplyIfChanged()
        {
            if (hasApplied
                && cacheValid
                && appliedColor == targetColor
                && appliedLerp == lerpValue
                && appliedThreshold == threshold
                && ReferenceEquals(appliedTex, dissolveTex)
                && (threshold <= 0f || boundsValid))
                return;

            Apply();
        }

        /// <summary>현재 값에 맞춰 머티리얼 적용/해제와 프로퍼티 기록을 수행한다.</summary>
        public void Apply()
        {
            if (!cacheValid)
                RebuildCache();

            bool dissolveActive = threshold > 0f;
            bool anyActive = dissolveActive || lerpValue > 0f;

            // 둘 다 0이면 시각적으로 무효과이므로 원본 머티리얼로 되돌려 배칭을 회복시킨다.
            if (!isActiveAndEnabled || !anyActive)
            {
                Unregister();
                RestoreMaterials();
                appliedColor = targetColor;
                appliedLerp = lerpValue;
                appliedThreshold = threshold;
                hasApplied = true;
                return;
            }

            if (!EnsureMaterial())
                return;

            SetDissolveKeyword(dissolveActive);
            AssignMaterials();

            if (!hasApplied || appliedColor != targetColor)
            {
                groupMaterial.SetColor(TargetColorId, targetColor);
                appliedColor = targetColor;
            }

            if (!hasApplied || appliedLerp != lerpValue)
            {
                groupMaterial.SetFloat(LerpValueId, lerpValue);
                appliedLerp = lerpValue;
            }

            if (dissolveActive)
            {
                if (!boundsValid)
                    RebuildUVTransform();

                if (!ReferenceEquals(appliedTex, dissolveTex))
                {
                    groupMaterial.SetTexture(DissolveTexId, dissolveTex);
                    appliedTex = dissolveTex;
                }

                if (!hasApplied || appliedThreshold != threshold)
                {
                    groupMaterial.SetFloat(ThresholdId, threshold);
                    appliedThreshold = threshold;
                }

                UpdateGroupMatrix();
                Register();
            }
            else
            {
                Unregister();
                appliedThreshold = threshold;
            }

            hasApplied = true;
        }

        /// <summary>SpriteEffectUpdater가 디졸브 진행 중에만 프레임당 한 번 호출한다.</summary>
        void ISpriteEffectTickable.Tick()
        {
            // 스크립트가 필드를 직접 건드린 경우(애니메이션 클립 등)도 여기서 흡수한다.
            if (threshold <= 0f || groupMaterial == null)
            {
                Apply();
                return;
            }

            if (appliedThreshold != threshold)
            {
                groupMaterial.SetFloat(ThresholdId, threshold);
                appliedThreshold = threshold;
            }

            if (appliedLerp != lerpValue)
            {
                groupMaterial.SetFloat(LerpValueId, lerpValue);
                appliedLerp = lerpValue;
            }

            UpdateGroupMatrix();
        }

        private void SetDissolveKeyword(bool on)
        {
            if (dissolveKeywordOn == on)
                return;

            dissolveKeywordOn = on;

            if (on)
            {
                groupMaterial.EnableKeyword(DissolveKeyword);
                groupMaterial.EnableKeyword(GroupUVKeyword);
            }
            else
            {
                // 디졸브를 쓰지 않으면 UV 좌표계 키워드도 꺼서 사용 변형 수를 줄인다.
                groupMaterial.DisableKeyword(DissolveKeyword);
                groupMaterial.DisableKeyword(GroupUVKeyword);
            }
        }

        // 캐릭터가 움직이면 패턴이 함께 따라가야 하므로 월드→그룹 변환을 갱신한다.
        private void UpdateGroupMatrix()
        {
            Matrix4x4 w2l = transform.worldToLocalMatrix;

            // 제자리에 서 있는 동안에는 업로드를 건너뛴다.
            if (hasLastMatrix && lastWorldToLocal == w2l)
                return;

            lastWorldToLocal = w2l;
            hasLastMatrix = true;
            groupMaterial.SetMatrix(GroupMatrixId, uvConstPart * w2l);
        }

        // 그룹 로컬 바운즈를 0~1로 정규화하고 타일 스케일을 곱하는 고정 파트를 만든다.
        private void RebuildUVTransform()
        {
            boundsValid = true;
            hasLastMatrix = false;

            Matrix4x4 w2l = transform.worldToLocalMatrix;
            bool hasBounds = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;

            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer r = renderers[i];
                if (r == null || r.sprite == null)
                    continue;

                Bounds wb = r.bounds;
                Vector3 c = wb.center;
                Vector3 e = wb.extents;

                // 월드 AABB의 XY 코너 4개를 그룹 로컬로 옮겨 감싼다.
                for (int k = 0; k < 4; k++)
                {
                    float sx = (k & 1) == 0 ? -1f : 1f;
                    float sy = (k & 2) == 0 ? -1f : 1f;
                    Vector3 p = w2l.MultiplyPoint3x4(new Vector3(c.x + e.x * sx, c.y + e.y * sy, c.z));

                    if (!hasBounds)
                    {
                        min = max = p;
                        hasBounds = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                }
            }

            if (!hasBounds)
            {
                uvConstPart = Matrix4x4.identity;
                return;
            }

            Vector2 size = max - min;
            float sizeX = Mathf.Abs(size.x) > 1e-4f ? size.x : 1f;
            float sizeY = Mathf.Abs(size.y) > 1e-4f ? size.y : 1f;

            uvConstPart = Matrix4x4.Scale(new Vector3(dissolveScale.x / sizeX, dissolveScale.y / sizeY, 1f))
                          * Matrix4x4.Translate(new Vector3(-min.x, -min.y, 0f));
        }

        private bool EnsureMaterial()
        {
            if (groupMaterial != null)
                return true;

            if (cachedShader == null)
                cachedShader = Shader.Find(ShaderName);

            if (cachedShader == null)
            {
                if (!shaderMissingLogged)
                {
                    shaderMissingLogged = true;
                    Debug.LogError(
                        $"[SpriteGroupEffect] 셰이더 '{ShaderName}'를 찾을 수 없습니다. " +
                        "Project Settings > Graphics > Always Included Shaders 에 등록하세요.", this);
                }
                return false;
            }

            groupMaterial = new Material(cachedShader)
            {
                name = $"{name} (GroupEffect)",
                hideFlags = HideFlags.DontSave
            };

            // 새 머티리얼에는 아직 아무 값도 없으므로 다음 기록을 강제한다.
            hasApplied = false;
            appliedTex = null;
            appliedThreshold = -1f;
            appliedLerp = -1f;
            hasLastMatrix = false;
            dissolveKeywordOn = false;

            return true;
        }

        private void DestroyGroupMaterial()
        {
            if (groupMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(groupMaterial);
            else
                DestroyImmediate(groupMaterial);

            groupMaterial = null;
        }

        private void AssignMaterials()
        {
            if (materialAssigned)
                return;

            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer r = renderers[i];
                if (r == null)
                    continue;

                r.sharedMaterial = groupMaterial;
            }

            materialAssigned = true;
        }

        /// <summary>
        /// 자식 머티리얼을 원본으로 되돌린다.
        /// force는 도메인 리로드/씬 저장 복구용이다. 그룹 머티리얼은 DontSave라 씬에 저장되지 않으므로,
        /// 효과가 켜진 채로 저장/리로드되면 자식의 머티리얼 참조가 null로 남는다. 그 상태를 복구한다.
        /// </summary>
        private void RestoreMaterials(bool force = false)
        {
            if (!force && !materialAssigned)
                return;

            // 직렬화 데이터가 어긋난 경우(수동 편집/버전 차이) 잘못된 인덱스 접근을 막는다.
            int count = Mathf.Min(renderers.Count, originalMaterials.Count);

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer r = renderers[i];
                if (r == null)
                    continue;

                // 다른 시스템이 그 사이에 머티리얼을 바꿨다면 존중한다. (null = 사라진 그룹 머티리얼)
                Material current = r.sharedMaterial;
                if (current == null || current == groupMaterial)
                    r.sharedMaterial = originalMaterials[i];
            }

            materialAssigned = false;
        }

        /// <summary>자식 렌더러 목록과 원본 머티리얼을 다시 수집한다.</summary>
        private void RebuildCache()
        {
            // 적용 중에 계층이 바뀌면 옛 목록 기준으로 먼저 원복해야 머티리얼이 새는 것을 막을 수 있다.
            RestoreMaterials();

            if (originalMaterials.Count != renderers.Count)
            {
                renderers.Clear();
                originalMaterials.Clear();
            }

            // 수집과 중첩 그룹 양보 판정은 SpriteGroupTint와 공유한다.
            SpriteGroupCollector.Collect<SpriteGroupEffect, SpriteRenderer>(this, includeInactive, scratchRenderers);
            SpriteGroupCollector.MapPreviousIndices(renderers, scratchRenderers, scratchIndices);

            scratchMaterials.Clear();

            for (int i = 0; i < scratchRenderers.Count; i++)
            {
                SpriteRenderer r = scratchRenderers[i];
                int known = scratchIndices[i];
                Material current = r.sharedMaterial;

                // 그룹 이펙트가 씌운 런타임 머티리얼(DontSave)이나 사라진 참조(null)를 원본으로 캡처하면
                // 효과 해제 시 자식이 잘못된 머티리얼로 돌아간다. 이미 아는 원본이 있으면 그걸 유지한다.
                bool unreliable = current == null || (current.hideFlags & HideFlags.DontSave) != 0;

                if (unreliable && known >= 0)
                    scratchMaterials.Add(originalMaterials[known]);
                else
                    scratchMaterials.Add(current);
            }

            renderers.Clear();
            renderers.AddRange(scratchRenderers);
            originalMaterials.Clear();
            originalMaterials.AddRange(scratchMaterials);

            cacheValid = true;
            boundsValid = false;
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
            includeInactive = true;

            cacheValid = false;
            boundsValid = false;
            Apply();
        }
    }
}
