using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Effects
{
    /// <summary>
    /// UI의 4개 모서리를 앵커로 설정하여 메시를 변형하는 컴포넌트입니다.
    /// 모바일 최적화를 위해 메시 분할 기능을 지원합니다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class UIMultiAnchor : MonoBehaviour, IMeshModifier
    {
        [System.Serializable]
        public class VertexAnchor
        {
            [Header("앵커 설정")]
            public Transform anchorTarget;      // 앵커할 타겟 Transform
            public Vector2 localOffset;        // 타겟으로부터의 로컬 오프셋
            public bool useAnchor = true;       // 이 vertex에 앵커를 적용할지 여부
        }

        [Header("Vertex 앵커 설정")]
        [SerializeField] private VertexAnchor topLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor topRight = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomRight = new VertexAnchor();

        [Header("메시 분할 설정 (모바일 최적화)")]
        [SerializeField] private bool useSubdivision = false;
        [SerializeField][Range(2, 6)] private int subdivisionX = 2;  // 모바일 고려하여 최대 6으로 제한
        [SerializeField][Range(2, 6)] private int subdivisionY = 2;  // 모바일 고려하여 최대 6으로 제한
        [Space]
        [SerializeField] private bool showPerformanceInfo = true;

        [Header("업데이트 설정")]
        [SerializeField] private bool updateEveryFrame = true;
        [SerializeField] private bool optimizePerformance = true;

        private Graphic graphic;
        private Canvas parentCanvas;
        private Vector3[] lastAnchorPositions = new Vector3[4];
        private bool needsUpdate = true;

        // 성능 정보 캐싱
        private int lastVertexCount = 0;
        private int lastTriangleCount = 0;

#if UNITY_EDITOR
        private float lastEditorUpdateTime = 0f;
        private const float EDITOR_UPDATE_INTERVAL = 0.016f; // ~60fps
#endif

        private void Awake()
        {
            InitializeComponents();
        }

#if UNITY_EDITOR
        // 컴포넌트가 처음 추가될 때 호출됨 (Editor에서만)
        private void Reset()
        {
            InitializeComponents();

            // 앵커 포인트 자동 생성
            if (ShouldCreateAnchorPoints())
            {
                CreateAnchorPoints();
            }
        }
#endif

        private void OnEnable()
        {
            InitializeComponents();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update += EditorUpdate;
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
            }
#endif
        }

        private void InitializeComponents()
        {
            graphic = GetComponent<Graphic>();
            parentCanvas = GetComponentInParent<Canvas>();

            // Image 타입 검사
            ValidateImageType();
        }

        private void ValidateImageType()
        {
            Image image = GetComponent<Image>();
            if (image != null && image.type != Image.Type.Simple)
            {
                Debug.LogWarning($"[{gameObject.name}] UIMultiAnchor는 Image Type이 Simple일 때만 정상 작동합니다. 현재: {image.type}");
            }

            // 모바일 성능 경고
            if (useSubdivision && (subdivisionX > 4 || subdivisionY > 4))
            {
                Debug.LogWarning($"[{gameObject.name}] 모바일에서는 subdivision 4x4 이하를 권장합니다. 현재: {subdivisionX}x{subdivisionY}");
            }
        }

        private void Start()
        {
            // 초기화
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null || graphic == null) return;
            
            // 컴포넌트가 파괴되었거나 비활성화된 경우 체크
            if (!this || !graphic)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }

            // Editor에서 성능을 위해 업데이트 주기 제한
            float currentTime = (float)EditorApplication.timeSinceStartup;
            if (currentTime - lastEditorUpdateTime < EDITOR_UPDATE_INTERVAL) return;

            lastEditorUpdateTime = currentTime;
            CheckForAnchorChanges();
        }

        private void OnValidate()
        {
            // Inspector에서 값이 변경될 때
            if (graphic == null)
                InitializeComponents();
            else
                ValidateImageType();

            ForceUpdate();
        }
#endif

        private void Update()
        {
            // 런타임에서만 매 프레임 업데이트
            if (Application.isPlaying && updateEveryFrame)
            {
                CheckForAnchorChanges();
            }
        }

        private void CheckForAnchorChanges()
        {
            if (graphic == null) return;

            if (!optimizePerformance)
            {
                needsUpdate = true;
                graphic.SetVerticesDirty();
                return;
            }

            // 성능 최적화: 앵커 위치가 변경되었을 때만 업데이트
            VertexAnchor[] anchors = { topLeft, topRight, bottomLeft, bottomRight };
            bool hasChanged = false;

            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i].useAnchor && anchors[i].anchorTarget != null)
                {
                    Vector3 currentPos = anchors[i].anchorTarget.position;
                    if (Vector3.Distance(currentPos, lastAnchorPositions[i]) > 0.01f)
                    {
                        lastAnchorPositions[i] = currentPos;
                        hasChanged = true;
                    }
                }
            }

            if (hasChanged)
            {
                needsUpdate = true;
                graphic.SetVerticesDirty();
            }
        }

        public void ModifyMesh(VertexHelper vh)
        {
            if (!needsUpdate) return;

            RectTransform rectTransform = transform as RectTransform;
            Rect rect = rectTransform.rect;
            vh.Clear();

            if (useSubdivision)
            {
                CreateSubdividedMesh(vh, rect);
            }
            else
            {
                CreateBasicMesh(vh, rect);
            }

            needsUpdate = false;

            // 성능 정보 업데이트
            if (showPerformanceInfo)
            {
                UpdatePerformanceInfo(vh);
            }
        }

        private void CreateBasicMesh(VertexHelper vh, Rect rect)
        {
            // 4개 모서리의 앵커 위치 가져오기
            Vector3 bottomLeftPos = GetAnchorPosition(bottomLeft, new Vector2(rect.xMin, rect.yMin));
            Vector3 topLeftPos = GetAnchorPosition(topLeft, new Vector2(rect.xMin, rect.yMax));
            Vector3 topRightPos = GetAnchorPosition(topRight, new Vector2(rect.xMax, rect.yMax));
            Vector3 bottomRightPos = GetAnchorPosition(bottomRight, new Vector2(rect.xMax, rect.yMin));

            // 4개의 vertex 생성
            UIVertex bottomLeftVert = CreateUIVertex(bottomLeftPos, new Vector2(0, 0));
            UIVertex topLeftVert = CreateUIVertex(topLeftPos, new Vector2(0, 1));
            UIVertex topRightVert = CreateUIVertex(topRightPos, new Vector2(1, 1));
            UIVertex bottomRightVert = CreateUIVertex(bottomRightPos, new Vector2(1, 0));

            // Quad를 2개의 삼각형으로 구성
            vh.AddVert(bottomLeftVert);
            vh.AddVert(topLeftVert);
            vh.AddVert(topRightVert);
            vh.AddVert(bottomLeftVert);
            vh.AddVert(topRightVert);
            vh.AddVert(bottomRightVert);

            // 삼각형 인덱스 추가
            vh.AddTriangle(0, 1, 2);  // 첫 번째 삼각형
            vh.AddTriangle(3, 4, 5);  // 두 번째 삼각형
        }

        private void CreateSubdividedMesh(VertexHelper vh, Rect rect)
        {
            // 모바일 성능을 위한 subdivision 제한
            int safeSubdivisionX = Mathf.Clamp(subdivisionX, 2, 6);
            int safeSubdivisionY = Mathf.Clamp(subdivisionY, 2, 6);

            // 4개 모서리의 앵커 위치
            Vector3 bottomLeftPos = GetAnchorPosition(bottomLeft, new Vector2(rect.xMin, rect.yMin));
            Vector3 topLeftPos = GetAnchorPosition(topLeft, new Vector2(rect.xMin, rect.yMax));
            Vector3 topRightPos = GetAnchorPosition(topRight, new Vector2(rect.xMax, rect.yMax));
            Vector3 bottomRightPos = GetAnchorPosition(bottomRight, new Vector2(rect.xMax, rect.yMin));

            // Subdivision된 vertex들 생성
            for (int y = 0; y <= safeSubdivisionY; y++)
            {
                for (int x = 0; x <= safeSubdivisionX; x++)
                {
                    // 정규화된 좌표 (0~1)
                    float normalizedX = (float)x / safeSubdivisionX;
                    float normalizedY = (float)y / safeSubdivisionY;

                    // Bilinear interpolation으로 위치 계산
                    Vector3 bottomInterp = Vector3.Lerp(bottomLeftPos, bottomRightPos, normalizedX);
                    Vector3 topInterp = Vector3.Lerp(topLeftPos, topRightPos, normalizedX);
                    Vector3 finalPosition = Vector3.Lerp(bottomInterp, topInterp, normalizedY);

                    // UV 좌표
                    Vector2 uv = new Vector2(normalizedX, normalizedY);

                    // Vertex 생성
                    UIVertex vertex = CreateUIVertex(finalPosition, uv);
                    vh.AddVert(vertex);
                }
            }

            // 삼각형 인덱스 생성
            for (int y = 0; y < safeSubdivisionY; y++)
            {
                for (int x = 0; x < safeSubdivisionX; x++)
                {
                    int bottomLeft = y * (safeSubdivisionX + 1) + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + (safeSubdivisionX + 1);
                    int topRight = topLeft + 1;

                    // 첫 번째 삼각형
                    vh.AddTriangle(bottomLeft, topLeft, topRight);
                    // 두 번째 삼각형
                    vh.AddTriangle(bottomLeft, topRight, bottomRight);
                }
            }
        }

        private UIVertex CreateUIVertex(Vector3 position, Vector2 uv)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            vertex.color = Color.white;
            return vertex;
        }

        private void UpdatePerformanceInfo(VertexHelper vh)
        {
            lastVertexCount = vh.currentVertCount;
            lastTriangleCount = vh.currentVertCount / 3;

#if UNITY_EDITOR && !APPLICATION_IS_PLAYING
            // Editor에서만 성능 정보 로깅
            if (useSubdivision)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    Debug.Log($"[{gameObject.name}] 성능 정보 - Vertex: {lastVertexCount}, 삼각형: {lastTriangleCount} " +
                             $"(Subdivision: {subdivisionX}x{subdivisionY})");
                };
            }
#endif
        }

        private Vector3 GetAnchorPosition(VertexAnchor anchor, Vector2 originalPosition)
        {
            if (!anchor.useAnchor || anchor.anchorTarget == null)
            {
                // 앵커가 설정되지 않은 경우 원래 위치 사용
                return originalPosition;
            }

            // 앵커 타겟이 자식인지 확인
            bool isChildAnchor = anchor.anchorTarget.IsChildOf(transform);
            
            if (isChildAnchor)
            {
                // 자식 앵커인 경우: 로컬 좌표 직접 사용
                RectTransform anchorRect = anchor.anchorTarget as RectTransform;
                if (anchorRect != null)
                {
                    // 로컬 위치에 오프셋 추가
                    return anchorRect.anchoredPosition + anchor.localOffset;
                }
            }
            
            // 외부 앵커인 경우: 기존 월드 좌표 변환 사용
            Vector3 worldPosition = anchor.anchorTarget.position + (Vector3)anchor.localOffset;
            return WorldToCanvasPosition(worldPosition);
        }

        private Vector3 WorldToCanvasPosition(Vector3 worldPosition)
        {
            if (parentCanvas == null)
                return worldPosition;

            Vector2 screenPoint;
            Camera canvasCamera = parentCanvas.worldCamera;
            
            if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            }
            else
            {
                if (canvasCamera == null)
                    canvasCamera = Camera.main;
                screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldPosition);
            }

            // Screen 좌표를 이 오브젝트의 로컬 좌표로 변환
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,  // parentCanvas 대신 자신의 transform 사용
                screenPoint,
                canvasCamera,
                out localPoint);

            return localPoint;
        }

        public void ModifyMesh(Mesh mesh)
        {
            // Legacy method - 사용하지 않음
        }

        private bool ShouldCreateAnchorPoints()
        {
            // 이미 앵커가 설정되어 있으면 생성하지 않음
            if (topLeft.anchorTarget != null || topRight.anchorTarget != null ||
                bottomLeft.anchorTarget != null || bottomRight.anchorTarget != null)
            {
                return false;
            }

            // 이미 해당 이름의 자식이 있으면 생성하지 않음
            Transform tl = transform.Find("TL");
            Transform tr = transform.Find("TR");
            Transform bl = transform.Find("BL");
            Transform br = transform.Find("BR");

            return tl == null && tr == null && bl == null && br == null;
        }

        private void CreateAnchorPoints()
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null) return;

            Rect rect = rectTransform.rect;

            // 4개의 앵커 포인트 생성 및 위치 설정
            GameObject tlObject = CreateAnchorPoint("TL", new Vector2(rect.xMin, rect.yMax));
            GameObject trObject = CreateAnchorPoint("TR", new Vector2(rect.xMax, rect.yMax));
            GameObject blObject = CreateAnchorPoint("BL", new Vector2(rect.xMin, rect.yMin));
            GameObject brObject = CreateAnchorPoint("BR", new Vector2(rect.xMax, rect.yMin));

            // 생성된 오브젝트들을 앵커로 자동 설정
            SetTopLeftAnchor(tlObject.transform);
            SetTopRightAnchor(trObject.transform);
            SetBottomLeftAnchor(blObject.transform);
            SetBottomRightAnchor(brObject.transform);

#if UNITY_EDITOR
            Debug.Log($"[{gameObject.name}] 앵커 포인트 4개가 자동으로 생성되었습니다: TL, TR, BL, BR");
#endif
        }

        private GameObject CreateAnchorPoint(string name, Vector2 localPosition)
        {
            // 새 게임오브젝트 생성
            GameObject anchorPoint = new GameObject(name);

            // 부모 설정
            anchorPoint.transform.SetParent(transform, false);

            // RectTransform 추가 및 설정
            RectTransform anchorRect = anchorPoint.AddComponent<RectTransform>();

            // 앵커를 부모의 중심으로 설정 (애니메이션 시 안정적)
            anchorRect.anchorMin = Vector2.one * 0.5f;
            anchorRect.anchorMax = Vector2.one * 0.5f;
            anchorRect.pivot = Vector2.one * 0.5f;
            
            // 로컬 위치 설정 (이미지의 vertex 위치와 정확히 일치)
            anchorRect.anchoredPosition = localPosition;
            anchorRect.sizeDelta = Vector2.zero;
            
            // 스케일 초기화 (애니메이션 오류 방지)
            anchorRect.localScale = Vector3.one;
            anchorRect.localRotation = Quaternion.identity;

#if UNITY_EDITOR
            // Editor에서 변경사항 기록 (Undo 지원)
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(anchorPoint, "Create Anchor Point");
            }
#endif

            return anchorPoint;
        }

        // 수동으로 앵커 포인트를 생성하는 메서드
        [ContextMenu("Create Anchor Points")]
        public void CreateAnchorPointsManually()
        {
            if (ShouldCreateAnchorPoints())
            {
                CreateAnchorPoints();
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 앵커 포인트가 이미 존재하거나 앵커가 설정되어 있습니다.");
            }
        }

        [ContextMenu("Show Performance Info")]
        public void ShowPerformanceInfo()
        {
            LogPerformanceInfo();
        }

        // 성능 정보 확인 메서드 (디버깅용)
        public void LogPerformanceInfo()
        {
            int vertexCount = useSubdivision ? (subdivisionX + 1) * (subdivisionY + 1) : 4;
            int triangleCount = useSubdivision ? subdivisionX * subdivisionY * 2 : 2;

            Debug.Log($"[{gameObject.name}] 성능 정보:\n" +
                     $"- Subdivision 사용: {useSubdivision}\n" +
                     $"- 분할 설정: {subdivisionX}x{subdivisionY}\n" +
                     $"- Vertex 수: {vertexCount}\n" +
                     $"- 삼각형 수: {triangleCount}\n" +
                     $"- 모바일 권장: {(vertexCount <= 25 ? "✓" : "✗")}");
        }

        // 모바일 최적화 프리셋 적용
        [ContextMenu("Apply Mobile Preset (2x2)")]
        public void ApplyMobilePreset()
        {
            useSubdivision = false;
            subdivisionX = 2;
            subdivisionY = 2;
            optimizePerformance = true;
            ForceUpdate();
            Debug.Log($"[{gameObject.name}] 모바일 최적화 프리셋 적용됨 (Basic Quad)");
        }

        [ContextMenu("Apply Smooth Preset (3x3)")]
        public void ApplySmoothPreset()
        {
            useSubdivision = true;
            subdivisionX = 3;
            subdivisionY = 3;
            optimizePerformance = true;
            ForceUpdate();
            Debug.Log($"[{gameObject.name}] 부드러운 변형 프리셋 적용됨 (3x3 = 16 vertex)");
        }

        [ContextMenu("Apply Quality Preset (4x4)")]
        public void ApplyQualityPreset()
        {
            useSubdivision = true;
            subdivisionX = 4;
            subdivisionY = 4;
            optimizePerformance = true;
            ForceUpdate();
            Debug.Log($"[{gameObject.name}] 고품질 변형 프리셋 적용됨 (4x4 = 25 vertex)");
        }

        // 수동으로 업데이트를 강제하는 메서드
        public void ForceUpdate()
        {
            needsUpdate = true;
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        // 특정 vertex의 앵커를 설정하는 메서드들
        public void SetTopLeftAnchor(Transform target, Vector2 offset = default)
        {
            topLeft.anchorTarget = target;
            topLeft.localOffset = offset;
            topLeft.useAnchor = target != null;
            ForceUpdate();
        }

        public void SetTopRightAnchor(Transform target, Vector2 offset = default)
        {
            topRight.anchorTarget = target;
            topRight.localOffset = offset;
            topRight.useAnchor = target != null;
            ForceUpdate();
        }

        public void SetBottomLeftAnchor(Transform target, Vector2 offset = default)
        {
            bottomLeft.anchorTarget = target;
            bottomLeft.localOffset = offset;
            bottomLeft.useAnchor = target != null;
            ForceUpdate();
        }

        public void SetBottomRightAnchor(Transform target, Vector2 offset = default)
        {
            bottomRight.anchorTarget = target;
            bottomRight.localOffset = offset;
            bottomRight.useAnchor = target != null;
            ForceUpdate();
        }

        // 디버깅용 Gizmo
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            VertexAnchor[] anchors = { bottomLeft, topLeft, topRight, bottomRight };
            Color[] colors = { Color.blue, Color.green, Color.red, Color.yellow };
            string[] names = { "Bottom-Left", "Top-Left", "Top-Right", "Bottom-Right" };

            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i].useAnchor && anchors[i].anchorTarget != null)
                {
                    Gizmos.color = colors[i];
                    Vector3 targetPos = anchors[i].anchorTarget.position + (Vector3)anchors[i].localOffset;
                    Gizmos.DrawWireSphere(targetPos, 0.1f);

                    // 선으로 연결
                    Gizmos.DrawLine(transform.position, targetPos);
                }
            }
        }
    }
}