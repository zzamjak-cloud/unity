using System.Collections.Generic;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// 자식 렌더러의 색을 그룹 단위로 틴트한다.
    /// 자식의 원본 색을 보존하고 tintColor를 곱해서 적용하므로 자식별 고유 색이 유지된다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SpriteGroupTint : MonoBehaviour
    {
        [SerializeField] private Color tintColor = Color.white;

        [Tooltip("자식 MeshRenderer도 틴트한다. MaterialPropertyBlock을 쓰므로 해당 렌더러는 SRP Batcher 대상에서 제외된다.")]
        [SerializeField] private bool includeMeshRenderers = false;

        // SpriteRenderer.color 는 우리가 직접 덮어쓰는 값이므로 원본을 직렬화해 둬야 한다.
        // 직렬화하지 않으면 도메인 리로드/씬 저장 후 이미 틴트된 색을 원본으로 다시 캡처해
        // 틴트가 반복 누적된다(색이 계속 어두워짐).
        [SerializeField, HideInInspector] private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        [SerializeField, HideInInspector] private List<Color> spriteBaseColors = new List<Color>();

        // MeshRenderer 는 MaterialPropertyBlock 으로만 건드리므로 머티리얼 원본 색이 그대로 남는다.
        // 따라서 매번 다시 계산할 수 있고 직렬화가 필요 없다. (셰이더 프로퍼티 ID 는 실행 간 불변이 보장되지 않는다)
        private readonly List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
        private readonly List<Color> meshBaseColors = new List<Color>();
        private readonly List<int> meshColorIds = new List<int>();

        // 계층 재구성용 스크래치. 재사용하므로 워밍업 이후 힙 할당이 없다.
        private readonly List<SpriteRenderer> scratchSprites = new List<SpriteRenderer>();
        private readonly List<Color> scratchColors = new List<Color>();
        private readonly List<int> scratchIndices = new List<int>();

        private MaterialPropertyBlock mpb;
        private bool cacheValid;

        // 실제로 자식에게 반영한 색. 인스펙터/Animator 가 덮어쓰는 tintColor 와 반드시 분리해야 한다.
        // (직렬화된 필드와 비교하면 Animator 가 값을 되돌릴 때 갱신이 누락된다)
        private Color appliedTint;
        private bool hasApplied;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void OnEnable()
        {
            // 비활성 중에 계층이 바뀔 수 있으므로 캐시를 무효화한다. 리스트를 재사용하므로 할당은 없다.
            cacheValid = false;
            hasApplied = false;
            ApplyTintToChildren();
        }

        void OnTransformChildrenChanged()
        {
            cacheValid = false;
        }

        /// <summary>
        /// Animator/Animation 이 tintColor 를 애니메이션한 직후 Unity 가 호출한다.
        /// 매 프레임 폴링(Update/LateUpdate) 없이 애니메이션 구동을 처리하기 위한 진입점이다.
        /// </summary>
        void OnDidApplyAnimationProperties()
        {
            ApplyIfChanged();
        }

#if UNITY_EDITOR
        // 에디터 전용. 메서드 자체를 #if 로 감싸야 한다.
        // 본문만 비우면 빌드에도 Update 가 남아 인스턴스마다 매 프레임 빈 호출이 발생한다.
        void Update()
        {
            if (!Application.isPlaying)
            {
                ApplyIfChanged();
            }
        }
#endif

        private void ApplyIfChanged()
        {
            if (hasApplied && SameColor(tintColor, appliedTint)) return;
            ApplyTintToChildren();
        }

        /// <summary>자식 렌더러에 원본 색 x tintColor 를 적용한다.</summary>
        public void ApplyTintToChildren()
        {
            if (!cacheValid) RebuildCache();

            // 자식이 파괴돼 캐시가 깨졌으면 한 번 재구성하고 다시 시도한다.
            if (!ApplyToCache())
            {
                RebuildCache();
                ApplyToCache();
            }

            appliedTint = tintColor;
            hasApplied = true;
        }

        /// <summary>자식 렌더러 목록을 강제로 다시 수집한다. 런타임에 자식을 붙였다면 호출한다.</summary>
        public void RefreshRenderers()
        {
            cacheValid = false;
            ApplyTintToChildren();
        }

        // 캐시된 렌더러에 색 적용. 파괴된 항목을 만나면 false 를 반환한다.
        private bool ApplyToCache()
        {
            bool intact = true;

            int spriteCount = spriteRenderers.Count;
            for (int i = 0; i < spriteCount; i++)
            {
                SpriteRenderer r = spriteRenderers[i];
                if (r == null)
                {
                    intact = false;
                    continue;
                }

                Color target = spriteBaseColors[i] * tintColor;
                if (SameColor(r.color, target)) continue;

                r.color = target;
#if UNITY_EDITOR
                // 값이 실제로 바뀐 경우에만 dirty 로 만든다.
                // 무조건 SetDirty 하면 씬/프리팹을 열기만 해도 수정됨으로 표시된다.
                if (!Application.isPlaying) SafeSetDirty(r);
#endif
            }

            if (!includeMeshRenderers) return intact;

            if (mpb == null) mpb = new MaterialPropertyBlock();

            int meshCount = meshRenderers.Count;
            for (int i = 0; i < meshCount; i++)
            {
                MeshRenderer r = meshRenderers[i];
                if (r == null)
                {
                    intact = false;
                    continue;
                }

                int propertyId = meshColorIds[i];
                if (propertyId == 0) continue;

                // 단일 mpb 를 모든 렌더러에 재사용하므로 렌더러 간 프로퍼티 누출을 막기 위해 비운 뒤
                // 해당 렌더러가 이미 가진 블록을 읽어와 우리 프로퍼티만 덧쓴다.
                mpb.Clear();
                r.GetPropertyBlock(mpb);
                mpb.SetColor(propertyId, meshBaseColors[i] * tintColor);
                r.SetPropertyBlock(mpb);
            }

            return intact;
        }

        // 자식 렌더러 목록을 다시 수집한다. 이미 알고 있던 원본 색은 승계한다.
        private void RebuildCache()
        {
            // 직렬화 데이터가 어긋난 경우(수동 편집/버전 차이) 원본 색을 다시 캡처하도록 초기화한다.
            if (spriteBaseColors.Count != spriteRenderers.Count)
            {
                spriteRenderers.Clear();
                spriteBaseColors.Clear();
            }

            // 수집과 중첩 그룹 양보 판정은 SpriteGroupEffect 와 공유한다.
            SpriteGroupCollector.Collect<SpriteGroupTint, SpriteRenderer>(this, true, scratchSprites);
            SpriteGroupCollector.MapPreviousIndices(spriteRenderers, scratchSprites, scratchIndices);

            bool changed = !SpriteGroupCollector.AreSame(spriteRenderers, scratchSprites);

            scratchColors.Clear();

            for (int i = 0; i < scratchSprites.Count; i++)
            {
                int known = scratchIndices[i];

                // 이미 틴트를 적용한 렌더러의 현재 색을 원본으로 다시 캡처하면 색이 이중으로 곱해진다.
                // 따라서 아는 렌더러는 기존 원본 색을 그대로 쓰고, 새로 붙은 렌더러만 현재 색을 캡처한다.
                scratchColors.Add(known >= 0 ? spriteBaseColors[known] : scratchSprites[i].color);
            }

            if (changed)
            {
                spriteRenderers.Clear();
                spriteRenderers.AddRange(scratchSprites);
                spriteBaseColors.Clear();
                spriteBaseColors.AddRange(scratchColors);

#if UNITY_EDITOR
                // 직렬화된 원본 색이 실제로 바뀐 경우에만 dirty 로 만든다.
                if (!Application.isPlaying) SafeSetDirty(this);
#endif
            }

            RebuildMeshCache();
            cacheValid = true;
        }

        private void RebuildMeshCache()
        {
            meshRenderers.Clear();
            meshBaseColors.Clear();
            meshColorIds.Clear();

            if (!includeMeshRenderers) return;

            SpriteGroupCollector.Collect<SpriteGroupTint, MeshRenderer>(this, true, meshRenderers);

            for (int i = meshRenderers.Count - 1; i >= 0; i--)
            {
                // 머티리얼이 없으면 색을 적용할 대상이 없다.
                if (meshRenderers[i].sharedMaterial == null)
                    meshRenderers.RemoveAt(i);
            }

            // 문자열 키 조회를 매 적용마다 반복하지 않도록 프로퍼티 ID 와 원본 색을 미리 확정한다.
            for (int i = 0; i < meshRenderers.Count; i++)
            {
                Material mat = meshRenderers[i].sharedMaterial;

                if (mat.HasProperty(ColorId))
                {
                    meshColorIds.Add(ColorId);
                    meshBaseColors.Add(mat.GetColor(ColorId));
                }
                else if (mat.HasProperty(BaseColorId))
                {
                    meshColorIds.Add(BaseColorId);
                    meshBaseColors.Add(mat.GetColor(BaseColorId));
                }
                else
                {
                    meshColorIds.Add(0);
                    meshBaseColors.Add(Color.white);
                }
            }
        }

        // Color 의 == 연산자는 Vector4 근사 비교(약 1e-5 허용오차)이므로 미세 변화를 놓친다.
        // 적용 자체가 저비용이므로 채널별 정확 비교를 쓴다.
        private static bool SameColor(Color a, Color b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        // 애니메이터/스크립트에서 호출할 수 있는 공용 메서드
        public void SetTintColor(Color color)
        {
            tintColor = color;
            ApplyIfChanged();
        }

        public void SetTintColorR(float r)
        {
            tintColor.r = r;
            ApplyIfChanged();
        }

        public void SetTintColorG(float g)
        {
            tintColor.g = g;
            ApplyIfChanged();
        }

        public void SetTintColorB(float b)
        {
            tintColor.b = b;
            ApplyIfChanged();
        }

        public void SetTintColorA(float a)
        {
            tintColor.a = a;
            ApplyIfChanged();
        }

        public Color TintColor
        {
            get { return tintColor; }
            set
            {
                tintColor = value;
                ApplyIfChanged();
            }
        }

#if UNITY_EDITOR
        /// <summary>에디터 전용. 현재 자식 색을 원본으로 확정하고 틴트를 흰색으로 되돌린다.</summary>
        public void CaptureCurrentAsBaseColors()
        {
            cacheValid = false;
            spriteRenderers.Clear();
            spriteBaseColors.Clear();
            tintColor = Color.white;
            hasApplied = false;
            ApplyTintToChildren();
        }

        /// <summary>에디터 전용. 자식을 원본 색으로 되돌리고 틴트를 흰색으로 초기화한다.</summary>
        public void RestoreBaseColors()
        {
            tintColor = Color.white;
            hasApplied = false;
            ApplyTintToChildren();
        }

        /// <summary>에디터 전용. 현재 캐시된 자식 렌더러 목록(Undo 기록 대상).</summary>
        public Object[] CollectUndoTargets()
        {
            if (!cacheValid) RebuildCache();

            var targets = new List<Object>(spriteRenderers.Count + 1) { this };
            for (int i = 0; i < spriteRenderers.Count; i++)
            {
                if (spriteRenderers[i] != null) targets.Add(spriteRenderers[i]);
            }
            return targets.ToArray();
        }

        // DontSaveInEditor 플래그가 설정된 오브젝트는 SetDirty 호출 시 에러가 발생한다.
        private static void SafeSetDirty(Object obj)
        {
            if (obj == null) return;
            if ((obj.hideFlags & HideFlags.DontSaveInEditor) != 0) return;

            try
            {
                UnityEditor.EditorUtility.SetDirty(obj);
            }
            catch (System.Exception)
            {
                // 런타임 생성 오브젝트 등에서의 실패는 무시
            }
        }

        void OnValidate()
        {
            // includeMeshRenderers 토글 등 인스펙터 변경을 반영한다.
            // OnValidate 컨텍스트에서 직접 렌더러를 건드리면 경고가 나므로 다음 에디터 틱으로 넘긴다.
            cacheValid = false;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ApplyTintToChildren();
            };
        }
#endif
    }
}
