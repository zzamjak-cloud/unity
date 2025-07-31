using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.UI
{
    /// <summary>
    /// UI의 4개 모서리를 앵커로 설정하여 메시를 변형하는 컴포넌트입니다.
    /// 모바일 최적화를 위해 메시 분할 기능을 지원하며, Tight Mesh 타입의 Sprite도 지원합니다.
    /// Unity 2022.3 및 Unity 6 호환 버전
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIImageDeform : MonoBehaviour, IMeshModifier
    {
        // ===== 필드 선언 =====
        [System.Serializable]
        public class VertexAnchor
        {
            [Header("앵커 설정")]
            public Transform anchorTarget;
            public Vector2 localOffset;
            public bool useAnchor = true;
        }

        [Header("Vertex 앵커 설정")]
        [SerializeField] private VertexAnchor topLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor topRight = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomRight = new VertexAnchor();

        [Header("메시 분할 설정 (모바일 최적화)")]
        [SerializeField] private bool useSubdivision = false;
        [SerializeField][Range(2, 6)] private int subdivisionX = 2;
        [SerializeField][Range(2, 6)] private int subdivisionY = 2;

        [Header("Sprite 메시 설정")]
        [Tooltip("Sprite의 Mesh Type이 Tight일 때, Sprite의 메시를 기반으로 변형할지 여부를 결정합니다.")]
        [SerializeField] private bool useTightSpriteMesh = true;
        [Space]
        [SerializeField] private bool showPerformanceInfo = true;

        [Header("업데이트 설정")]
        [SerializeField] private bool updateEveryFrame = true;
        [SerializeField] private bool optimizePerformance = true;
        [SerializeField] private bool useLateUpdate = true;

        private Graphic graphic;
        private Canvas parentCanvas;
        private Vector3[] lastAnchorPositions = new Vector3[4];
        private bool needsUpdate = true;
        private bool hasAnimator = false;

        // 메시 데이터 캐싱 (정점 스트림 방식)
        private List<UIVertex> lastMeshVertices = new List<UIVertex>();

        // 성능 정보 캐싱
        private int lastVertexCount = 0;
        private int lastTriangleCount = 0;

#if UNITY_EDITOR
        private float lastEditorUpdateTime = 0f;
        private const float EDITOR_UPDATE_INTERVAL = 0.016f;
#endif

        // ===== Unity 이벤트 메서드 =====
        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            InitializeComponents();
            if (graphic != null)
                graphic.SetVerticesDirty();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update += EditorUpdate;
#endif
        }

        private void OnDisable()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update -= EditorUpdate;
#endif
        }

        private void Start()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        private void Update()
        {
            if (!useLateUpdate)
                PerformUpdate();
        }

        private void LateUpdate()
        {
            // 앵커가 애니메이션으로 움직일 때 항상 갱신
            if (useLateUpdate)
            {
                needsUpdate = true; // 항상 업데이트 플래그 활성화
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        private void PerformUpdate()
        {
            if (Application.isPlaying && updateEveryFrame)
            {
                needsUpdate = true; // 항상 업데이트
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        // 메시만 강제 업데이트 (needsUpdate 플래그 변경 없이)
        private void ForceUpdateMesh()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        private void CheckForAnchorChanges()
        {
            if (graphic == null) return;

            // 애니메이션 모드에서는 항상 업데이트
            if (hasAnimator && Application.isPlaying)
            {
                needsUpdate = true;
                graphic.SetVerticesDirty();
                return;
            }

            // Color 변경으로 인한 SetVerticesDirty 호출을 무시하기 위해
            // needsUpdate가 false인 경우 바로 리턴
            if (!needsUpdate && optimizePerformance && Application.isPlaying)
            {
                return;
            }

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
            // Color 변경만으로 인한 호출인지 체크 (메시 구조 변경 없음)
            if (!needsUpdate && lastMeshVertices != null && lastMeshVertices.Count > 0)
            {
                // 이전에 저장한 메시 데이터 복원
                vh.Clear();
                
                // 현재 Graphic의 색상 가져오기
                Color currentColor = graphic != null ? graphic.color : Color.white;
                
                // 캐시된 버텍스 리스트에 현재 색상을 적용합니다.
                // UIVertex는 구조체이므로 리스트의 항목을 직접 수정하려면 다시 할당해야 합니다.
                for (int i = 0; i < lastMeshVertices.Count; i++)
                {
                    UIVertex vertex = lastMeshVertices[i];
                    vertex.color = currentColor;
                    lastMeshVertices[i] = vertex;
                }
                
                // 캐시된 버텍스 스트림을 사용하여 메시를 한 번에 복원합니다.
                // 이 메서드는 버텍스와 삼각형을 모두 올바르게 추가합니다.
                vh.AddUIVertexTriangleStream(lastMeshVertices);
                return;
            }

            if (!needsUpdate) return;

            vh.Clear();

            Image image = graphic as Image;
            Sprite sprite = (image != null) ? (image.overrideSprite ?? image.sprite) : null;

            // Tight Mesh 타입이고 옵션이 활성화된 경우 Sprite의 메시를 사용
            // sprite.triangles.Length > 6 조건으로 Tight Mesh 여부를 간접적으로 확인
            if (useTightSpriteMesh && sprite != null && sprite.triangles.Length > 6)
            {
                CreateDeformedSpriteMesh(vh, sprite);
            }
            else // Full Rect 또는 기본 Quad 메시 생성
            {
                RectTransform rectTransform = transform as RectTransform;
                Rect rect = rectTransform.rect;

                if (useSubdivision)
                {
                    CreateSubdividedMesh(vh, rect);
                }
                else
                {
                    CreateBasicMesh(vh, rect);
                }
            }

            // 메시 데이터 저장
            SaveMeshData(vh);
            
            needsUpdate = false;

            // 성능 정보 업데이트
            if (showPerformanceInfo)
            {
                UpdatePerformanceInfo(vh);
            }
        }
        
        /// <summary>
        /// Sprite의 Tight Mesh를 기반으로 변형된 메시를 생성합니다.
        /// </summary>
        private void CreateDeformedSpriteMesh(VertexHelper vh, Sprite sprite)
        {
            // 1. 네 모서리의 최종 앵커 위치를 가져옵니다.
            RectTransform rectTransform = transform as RectTransform;
            Rect rect = rectTransform.rect;
            Vector3 bottomLeftPos = GetAnchorPosition(bottomLeft, new Vector2(rect.xMin, rect.yMin));
            Vector3 topLeftPos = GetAnchorPosition(topLeft, new Vector2(rect.xMin, rect.yMax));
            Vector3 topRightPos = GetAnchorPosition(topRight, new Vector2(rect.xMax, rect.yMax));
            Vector3 bottomRightPos = GetAnchorPosition(bottomRight, new Vector2(rect.xMax, rect.yMin));

            // 2. Sprite에서 메시 데이터를 가져옵니다.
            Vector2[] spriteVertices = sprite.vertices;
            Vector2[] spriteUVs = sprite.uv;
            ushort[] spriteTriangles = sprite.triangles;
            Bounds spriteBounds = sprite.bounds;

            // 3. 각 Sprite Vertex를 변형된 사각형에 맞게 재계산합니다.
            for (int i = 0; i < spriteVertices.Length; i++)
            {
                Vector2 vert = spriteVertices[i];
                Vector2 uv = spriteUVs[i];

                // Sprite 바운더리 내에서 현재 Vertex의 정규화된 위치(0-1)를 계산합니다.
                float normalizedX = (vert.x - spriteBounds.min.x) / spriteBounds.size.x;
                float normalizedY = (vert.y - spriteBounds.min.y) / spriteBounds.size.y;

                // Bilinear Interpolation을 사용하여 최종 위치를 계산합니다.
                Vector3 bottomInterp = Vector3.Lerp(bottomLeftPos, bottomRightPos, normalizedX);
                Vector3 topInterp = Vector3.Lerp(topLeftPos, topRightPos, normalizedX);
                Vector3 finalPosition = Vector3.Lerp(bottomInterp, topInterp, normalizedY);

                vh.AddVert(CreateUIVertex(finalPosition, uv));
            }

            // 4. Sprite의 삼각형 정보를 그대로 추가합니다.
            for (int i = 0; i < spriteTriangles.Length; i += 3)
            {
                vh.AddTriangle(spriteTriangles[i], spriteTriangles[i + 1], spriteTriangles[i + 2]);
            }
        }


        private void CreateBasicMesh(VertexHelper vh, Rect rect)
        {
            // 4개 모서리의 앵커 위치 가져오기
            Vector3 bottomLeftPos = GetAnchorPosition(bottomLeft, new Vector2(rect.xMin, rect.yMin));
            Vector3 topLeftPos = GetAnchorPosition(topLeft, new Vector2(rect.xMin, rect.yMax));
            Vector3 topRightPos = GetAnchorPosition(topRight, new Vector2(rect.xMax, rect.yMax));
            Vector3 bottomRightPos = GetAnchorPosition(bottomRight, new Vector2(rect.xMax, rect.yMin));

            // UV 좌표 가져오기 (아틀라스 대응)
            Vector4 uv = GetAdjustedUV();
            
            // 4개의 vertex 생성
            UIVertex bottomLeftVert = CreateUIVertex(bottomLeftPos, new Vector2(uv.x, uv.y));
            UIVertex topLeftVert = CreateUIVertex(topLeftPos, new Vector2(uv.x, uv.w));
            UIVertex topRightVert = CreateUIVertex(topRightPos, new Vector2(uv.z, uv.w));
            UIVertex bottomRightVert = CreateUIVertex(bottomRightPos, new Vector2(uv.z, uv.y));

            // Quad를 2개의 삼각형으로 구성
            vh.AddVert(bottomLeftVert);
            vh.AddVert(topLeftVert);
            vh.AddVert(topRightVert);
            vh.AddVert(bottomRightVert);

            // 삼각형 인덱스 추가
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
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

            // UV 좌표 가져오기 (아틀라스 대응)
            Vector4 uv = GetAdjustedUV();

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

                    // UV 좌표 (아틀라스 범위 내에서 보간)
                    Vector2 finalUV = new Vector2(
                        Mathf.Lerp(uv.x, uv.z, normalizedX),
                        Mathf.Lerp(uv.y, uv.w, normalizedY)
                    );

                    // Vertex 생성
                    UIVertex vertex = CreateUIVertex(finalPosition, finalUV);
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

        private void SaveMeshData(VertexHelper vh)
        {
            lastMeshVertices.Clear();
            // GetUIVertexStream은 삼각형을 구성하는 모든 정점의 목록을 채웁니다.
            // 이 "펼쳐진" 정점 목록은 나중에 AddUIVertexTriangleStream으로 메시를 복원하는 데 사용됩니다.
            vh.GetUIVertexStream(lastMeshVertices);
        }

        private UIVertex CreateUIVertex(Vector3 position, Vector2 uv)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            
            // Graphic의 현재 색상 사용 (Alpha 포함)
            if (graphic != null)
            {
                Color color = graphic.color;
                
                // CanvasGroup의 alpha도 고려
                CanvasGroup canvasGroup = GetComponentInParent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    color.a *= canvasGroup.alpha;
                }
                
                vertex.color = color;
            }
            else
            {
                vertex.color = Color.white;
            }
            
            return vertex;
        }

        private void UpdatePerformanceInfo(VertexHelper vh)
        {
            lastVertexCount = vh.currentVertCount;
            lastTriangleCount = vh.currentIndexCount / 3;

#if UNITY_EDITOR
            // Editor에서만 성능 정보 로깅
            if (showPerformanceInfo && !Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    Debug.Log($"[{gameObject.name}] 메시 정보 - Vertex: {lastVertexCount}, 삼각형: {lastTriangleCount}");
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
                return transform.InverseTransformPoint(worldPosition);

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
                transform as RectTransform,
                screenPoint,
                canvasCamera,
                out localPoint);

            return localPoint;
        }

        /// <summary>
        /// 아틀라스 사용 여부와 관계없이 Sprite의 전체 영역에 해당하는 UV 좌표를 가져옵니다.
        /// </summary>
        private Vector4 GetAdjustedUV()
        {
            Image image = graphic as Image;
            if (image != null)
            {
                Sprite currentSprite = image.overrideSprite ?? image.sprite;
                if (currentSprite != null)
                {
                    // GetOuterUV는 스프라이트가 아틀라스에 포함되어 있든 아니든
                    // 텍스처 내에서 스프라이트가 차지하는 사각 영역의 UV를 반환합니다.
                    // 이 컴포넌트는 자체 메시를 생성하므로 항상 OuterUV를 사용하는 것이 맞습니다.
                    return UnityEngine.Sprites.DataUtility.GetOuterUV(currentSprite);
                }
            }
            
            // 기본 UV (0,0 ~ 1,1)
            return new Vector4(0f, 0f, 1f, 1f);
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
        public void ShowPerformanceInfoContext()
        {
            LogPerformanceInfo();
        }

        // 성능 정보 확인 메서드 (디버깅용)
        public void LogPerformanceInfo()
        {
            int vertexCount = lastVertexCount;
            int triangleCount = lastTriangleCount;

            Debug.Log($"[{gameObject.name}] 성능 정보:\n" +
                      $"- Vertex 수: {vertexCount}\n" +
                      $"- 삼각형 수: {triangleCount}");
        }

        // 모바일 최적화 프리셋 적용
        [ContextMenu("Apply Mobile Preset (Basic Quad)")]
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
            Debug.Log($"[{gameObject.name}] 부드러운 변형 프리셋 적용됨 (3x3 = 18 triangles)");
        }

        [ContextMenu("Apply Quality Preset (4x4)")]
        public void ApplyQualityPreset()
        {
            useSubdivision = true;
            subdivisionX = 4;
            subdivisionY = 4;
            optimizePerformance = true;
            ForceUpdate();
            Debug.Log($"[{gameObject.name}] 고품질 변형 프리셋 적용됨 (4x4 = 32 triangles)");
        }

        // 수동으로 업데이트를 강제하는 메서드
        public void ForceUpdate()
        {
            needsUpdate = true;
            
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
                
                // Unity 2022.3에서 Canvas 재구성 강제
#if UNITY_2022_3_OR_NEWER && !UNITY_6000_0_OR_NEWER
                Canvas.ForceUpdateCanvases();
#endif
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
            if (!this.enabled) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null) return;
            Rect rect = rectTransform.rect;

            Vector3 bl = GetAnchorPosition(bottomLeft, new Vector2(rect.xMin, rect.yMin));
            Vector3 tl = GetAnchorPosition(topLeft, new Vector2(rect.xMin, rect.yMax));
            Vector3 tr = GetAnchorPosition(topRight, new Vector2(rect.xMax, rect.yMax));
            Vector3 br = GetAnchorPosition(bottomRight, new Vector2(rect.xMax, rect.yMin));

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(bl, tl);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
        }

#if UNITY_EDITOR
        // 에디터 전용 메서드들은 클래스 맨 아래에 배치
        [ContextMenu("Toggle Animation Mode")]
        public void ToggleAnimationMode()
        {
            hasAnimator = !hasAnimator;
            useLateUpdate = hasAnimator;
            Debug.Log($"[{gameObject.name}] Animation Mode: {(hasAnimator ? "ON" : "OFF")}");
            ForceUpdate();
        }

        [ContextMenu("Force Animation Detection")]
        public void ForceAnimationDetection()
        {
            hasAnimator = GetComponent<Animator>() != null || GetComponentInParent<Animator>() != null;
            if (hasAnimator)
            {
                useLateUpdate = true;
                Debug.Log($"[{gameObject.name}] Animator 감지됨. Animation Mode 활성화.");
            }
            else
            {
                Debug.Log($"[{gameObject.name}] Animator를 찾을 수 없습니다.");
            }
        }

        private void EditorUpdate()
        {
            if (this == null || graphic == null || !this.enabled || !this.gameObject.activeInHierarchy)
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

        // 컴포넌트 초기화 (graphic, parentCanvas 등)
        private void InitializeComponents()
        {
            graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                parentCanvas = graphic.canvas;
            }
            else
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
        }

        // Image 타입 유효성 검사 (필요시 확장)
        private void ValidateImageType()
        {
            Image image = graphic as Image;
            if (image != null && image.sprite == null)
            {
                Debug.LogWarning($"[{gameObject.name}] Image 컴포넌트에 Sprite가 없습니다.");
            }
        }
    }
}
