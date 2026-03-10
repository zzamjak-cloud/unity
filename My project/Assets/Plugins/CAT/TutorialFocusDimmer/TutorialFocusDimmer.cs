using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.UI
{
    /// <summary>
    /// 튜토리얼 포커스 Dimmer — 메시 + 전용 셰이더로 구멍난 마스킹.
    /// 스프라이트 없이 전체 영역을 한 쿼드로 그리고, 셰이더에서 포커스 영역(버튼+padding)을 둥근 사각 구멍으로 뚫음.
    /// - 외곽: expansionMargin 만큼 확장된 영역에 Color Tint로 딤.
    /// - 구멍: focusTargets[currentIndex] + padding, holeCornerRadius로 라운드 처리.
    /// - 구멍 영역 Raycast 통과 (ICanvasRaycastFilter).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public class TutorialFocusDimmer : MaskableGraphic, ICanvasRaycastFilter
    {
        [Header("Focus Settings")]
        [Tooltip("튜토리얼 순서에 따라 포커싱할 대상 목록")]
        [SerializeField] private List<RectTransform> _focusTargets = new List<RectTransform>();
        public Vector2 padding;

        [Header("Hole")]
        [Tooltip("구멍 모서리 라운드 반경 (픽셀)")]
        [SerializeField, Range(0f, 200f)]
        private float _holeCornerRadius = 16f;

        [Tooltip("구멍 가장자리 부드러운 전환 폭 (0=하드 엣지, 픽셀 단위)")]
        [SerializeField, Range(0f, 100f)]
        private float _holeSoftness = 0f;

        [Header("Expansion")]
        [Tooltip("해상도 대응을 위해 사방으로 확장할 픽셀 크기")]
        public float expansionMargin = 200f;

        [Header("Shader")]
        [Tooltip("비어 있으면 CAT/UI/FocusDimmer 자동 검색. 빌드에 포함되지 않을 경우 여기서 할당")]
        [SerializeField] private Shader _focusDimmerShader;

        // GC 방지: GetWorldCorners 용 배열 캐싱 (new Vector3[4] 반복 할당 제거)
        private readonly Vector3[] _worldCorners  = new Vector3[4];
        private readonly Vector3[] _canvasCorners = new Vector3[4];

        private Rect     _cachedFocusRect;
        private Material _material;
        private int      _currentIndex = -1;

        private static readonly int FocusRectID    = Shader.PropertyToID("_FocusRect");
        private static readonly int CornerRadiusID = Shader.PropertyToID("_CornerRadius");
        private static readonly int HoleSoftnessID = Shader.PropertyToID("_HoleSoftness");

        // ── 외부 API ──────────────────────────────────────────

        /// <summary>등록된 포커스 타겟 수</summary>
        public int FocusCount => _focusTargets.Count;

        /// <summary>현재 활성 포커스 인덱스 (-1 = 포커싱 없음)</summary>
        public int CurrentIndex => _currentIndex;

        /// <summary>현재 활성 포커스 타겟 (없으면 null)</summary>
        public RectTransform CurrentTarget =>
            _currentIndex >= 0 && _currentIndex < _focusTargets.Count
                ? _focusTargets[_currentIndex]
                : null;

        /// <summary>포커스 타겟 리스트 (읽기 전용 접근)</summary>
        public IReadOnlyList<RectTransform> FocusTargets => _focusTargets;

        /// <summary>
        /// 지정한 인덱스의 타겟으로 포커싱. SetVerticesDirty 자동 호출.
        /// </summary>
        public void SetFocusIndex(int index)
        {
            if (index < 0 || index >= _focusTargets.Count)
            {
                Debug.LogWarning($"TutorialFocusDimmer: 유효하지 않은 인덱스 {index} (등록된 타겟 수: {_focusTargets.Count})");
                return;
            }

            _currentIndex = index;
            SetVerticesDirty();
        }

        /// <summary>포커싱 해제 — 구멍 없이 전체 딤 처리</summary>
        public void ClearFocus()
        {
            _currentIndex = -1;
            SetVerticesDirty();
        }

        /// <summary>런타임에서 타겟을 추가하고 해당 인덱스를 반환</summary>
        public int AddTarget(RectTransform target)
        {
            _focusTargets.Add(target);
            return _focusTargets.Count - 1;
        }

        // ── Properties ────────────────────────────────────────

        public float holeCornerRadius
        {
            get => _holeCornerRadius;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(_holeCornerRadius, clamped)) return;
                _holeCornerRadius = clamped;
                // 구멍 크기 변경은 Material 파라미터만 갱신 (메시 재빌드 불필요)
                SetMaterialDirty();
            }
        }

        public float holeSoftness
        {
            get => _holeSoftness;
            set
            {
                float clamped = Mathf.Clamp(value, 0f, 100f);
                if (Mathf.Approximately(_holeSoftness, clamped)) return;
                _holeSoftness = clamped;
                SetMaterialDirty();
            }
        }

        public override Texture mainTexture => s_WhiteTexture;

        // ── Lifecycle ─────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureMaterial();

            // 활성화 시 첫 번째 타겟이 있으면 자동으로 포커싱
            if (_currentIndex < 0 && _focusTargets.Count > 0)
                _currentIndex = 0;
        }

        private void EnsureMaterial()
        {
            if (_material != null) return;

            Shader shader = _focusDimmerShader != null
                ? _focusDimmerShader
                : Shader.Find("CAT/UI/FocusDimmer");

            if (shader == null)
            {
                Debug.LogError("TutorialFocusDimmer: CAT/UI/FocusDimmer 셰이더를 찾을 수 없습니다. 인스펙터 Shader 필드에 할당하세요.");
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material = _material;
        }

        // ── Mesh ──────────────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            _cachedFocusRect = GetFocusRect();
            Rect outerRect   = GetCanvasCoverRect();

            // 포커스 Rect를 외곽 Rect 내로 클램프
            float fxMin = Mathf.Clamp(Mathf.Min(_cachedFocusRect.xMin, _cachedFocusRect.xMax), outerRect.xMin, outerRect.xMax);
            float fxMax = Mathf.Clamp(Mathf.Max(_cachedFocusRect.xMin, _cachedFocusRect.xMax), outerRect.xMin, outerRect.xMax);
            float fyMin = Mathf.Clamp(Mathf.Min(_cachedFocusRect.yMin, _cachedFocusRect.yMax), outerRect.yMin, outerRect.yMax);
            float fyMax = Mathf.Clamp(Mathf.Max(_cachedFocusRect.yMin, _cachedFocusRect.yMax), outerRect.yMin, outerRect.yMax);
            if (fxMin >= fxMax) fxMax = fxMin + 1f;
            if (fyMin >= fyMax) fyMax = fyMin + 1f;

            // 전체 딤머를 단일 쿼드로 렌더. 구멍은 셰이더 SDF로 처리
            AddQuad(vh,
                new Vector2(outerRect.xMin, outerRect.yMin),
                new Vector2(outerRect.xMax, outerRect.yMax),
                Vector2.zero,
                Vector2.one);

            // 메시 재빌드와 함께 Material 파라미터도 갱신
            UpdateMaterialProperties(fxMin, fyMin, fxMax, fyMax);
        }

        /// <summary>메시 재빌드 없이 Material 파라미터만 갱신 (holeCornerRadius / holeSoftness 변경 시)</summary>
        private void UpdateMaterialProperties(float fxMin, float fyMin, float fxMax, float fyMax)
        {
            if (_material == null) return;
            _material.SetVector(FocusRectID,    new Vector4(fxMin, fyMin, fxMax, fyMax));
            _material.SetFloat(CornerRadiusID,  _holeCornerRadius);
            _material.SetFloat(HoleSoftnessID,  _holeSoftness);
        }

        /// <summary>루트 캔버스 전체를 이 Graphic 로컬 좌표로 덮는 사각 (expansionMargin 포함)</summary>
        private Rect GetCanvasCoverRect()
        {
            Canvas c = canvas;
            if (c == null)
            {
                Rect r = rectTransform.rect;
                return new Rect(r.xMin - expansionMargin, r.yMin - expansionMargin,
                    r.width + expansionMargin * 2, r.height + expansionMargin * 2);
            }

            RectTransform root = c.rootCanvas.transform as RectTransform;
            root.GetWorldCorners(_canvasCorners);  // 캐싱된 배열 재사용

            // 4개 코너를 모두 로컬 좌표로 변환 후 AABB 계산 (회전 Canvas 대응)
            Vector2 p0 = rectTransform.InverseTransformPoint(_canvasCorners[0]);
            Vector2 p1 = rectTransform.InverseTransformPoint(_canvasCorners[1]);
            Vector2 p2 = rectTransform.InverseTransformPoint(_canvasCorners[2]);
            Vector2 p3 = rectTransform.InverseTransformPoint(_canvasCorners[3]);

            float xMin = Mathf.Min(Mathf.Min(p0.x, p1.x), Mathf.Min(p2.x, p3.x));
            float xMax = Mathf.Max(Mathf.Max(p0.x, p1.x), Mathf.Max(p2.x, p3.x));
            float yMin = Mathf.Min(Mathf.Min(p0.y, p1.y), Mathf.Min(p2.y, p3.y));
            float yMax = Mathf.Max(Mathf.Max(p0.y, p1.y), Mathf.Max(p2.y, p3.y));

            return new Rect(
                xMin - expansionMargin,
                yMin - expansionMargin,
                (xMax - xMin) + expansionMargin * 2,
                (yMax - yMin) + expansionMargin * 2);
        }

        private Rect GetFocusRect()
        {
            RectTransform target = CurrentTarget;
            if (target == null) return Rect.zero;

            target.GetWorldCorners(_worldCorners);  // 캐싱된 배열 재사용

            Vector2 min = rectTransform.InverseTransformPoint(_worldCorners[0]);
            Vector2 max = rectTransform.InverseTransformPoint(_worldCorners[2]);

            return new Rect(min.x - padding.x, min.y - padding.y,
                (max.x - min.x) + padding.x * 2,
                (max.y - min.y) + padding.y * 2);
        }

        private void AddQuad(VertexHelper vh, Vector2 posMin, Vector2 posMax, Vector2 uvMin, Vector2 uvMax)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = color;

            v.position = new Vector3(posMin.x, posMin.y); v.uv0 = new Vector2(uvMin.x, uvMin.y); vh.AddVert(v);
            v.position = new Vector3(posMin.x, posMax.y); v.uv0 = new Vector2(uvMin.x, uvMax.y); vh.AddVert(v);
            v.position = new Vector3(posMax.x, posMax.y); v.uv0 = new Vector2(uvMax.x, uvMax.y); vh.AddVert(v);
            v.position = new Vector3(posMax.x, posMin.y); v.uv0 = new Vector2(uvMax.x, uvMin.y); vh.AddVert(v);

            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        // ── Raycast ───────────────────────────────────────────

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!rectTransform) return true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, eventCamera, out Vector2 localPoint);
            return !_cachedFocusRect.Contains(localPoint);
        }

        // ── Update ────────────────────────────────────────────

        private void Update()
        {
            RectTransform target = CurrentTarget;
            if (target == null || !target.hasChanged) return;

            // hasChanged 리셋: 리셋하지 않으면 한 번 이동 후 매 프레임 재빌드 발생
            target.hasChanged = false;
            SetVerticesDirty();
        }

        // ── Cleanup ───────────────────────────────────────────

        protected override void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
                _material = null;
            }
            base.OnDestroy();
        }
    }
}
