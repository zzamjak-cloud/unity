using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Effects
{
    /// <summary>
    /// UI Image와 Sprite Renderer 모두를 지원하는 통합 변형 컴포넌트입니다.
    /// 4개의 앵커포인트를 사용하여 메시를 변형시킵니다.
    /// Unity 2022.3 및 Unity 6 호환 버전
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("CAT/Effects/Image Deform")]
    public partial class ImageDeform : MonoBehaviour, IMeshModifier
    {
        // ===== 열거형 및 클래스 =====
        public enum DeformMode
        {
            UIImage,     // UI Image 변형 모드
            Sprite       // Sprite Renderer 변형 모드
        }

        public enum SubdivisionLevel
        {
            None = 1,
            Level2x2 = 2,
            Level3x3 = 3,
            Level4x4 = 4,
            Level5x5 = 5,
            Level6x6 = 6
        }

        [System.Serializable]
        public class VertexAnchor
        {
            [Header("앵커 설정")]
            public Transform anchorTarget;
            public Vector2 localOffset;
            public bool useAnchor = true;
        }

        // ===== 필드 선언 =====
        [Header("변형 모드")]
        [SerializeField] private DeformMode deformMode = DeformMode.UIImage;

        [Header("Vertex 앵커 설정")]
        [SerializeField] private VertexAnchor topLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor topRight = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomRight = new VertexAnchor();

        [Header("메시 분할 설정")]
        [SerializeField] private bool useSubdivision = false;
        [SerializeField] private SubdivisionLevel subdivisionLevel = SubdivisionLevel.Level2x2;

        [Header("Sprite 설정 (Sprite 모드용)")]
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color spriteColor = Color.white;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 0;

        [Header("아틀라스 지원 (런타임용)")]
        [HideInInspector][SerializeField] private SpriteAtlas spriteAtlas;
        [HideInInspector][SerializeField] private string spriteName;

        [Header("성능 설정")]
        [SerializeField] private bool updateEveryFrame = true;
        [SerializeField] private bool optimizePerformance = true;
        [SerializeField] private bool useLateUpdate = true;
        [SerializeField] private bool useSharedMaterial = true;

        [Header("디버그")]
        [SerializeField] private bool showPerformanceInfo = true;
        [SerializeField] private bool showVertices = true;
        [SerializeField] private float gizmoSize = 0.05f;

        // 내부 변수들
        private Graphic graphic;
        private Canvas parentCanvas;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Material instanceMaterial;

        // 메시 데이터
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Color[] colors;
        private int[] triangles;

        // 성능 추적
        private int lastVertexCount = 0;
        private int lastTriangleCount = 0;
        private List<UIVertex> lastMeshVertices = new List<UIVertex>();
        private List<Transform> createdAnchorPoints = new List<Transform>();

        // 공유 머티리얼 캐시
        private static Dictionary<Texture2D, Material> sharedMaterials = new Dictionary<Texture2D, Material>();

        // ===== 공개 프로퍼티 =====
        public DeformMode CurrentDeformMode => deformMode;
        public Sprite CurrentSprite => sprite;
        public SpriteAtlas CurrentSpriteAtlas => spriteAtlas;
        public SubdivisionLevel CurrentSubdivisionLevel => subdivisionLevel;
        public bool UseSubdivision => useSubdivision;
        public bool UseSharedMaterial => useSharedMaterial;

        // ===== Unity 생명주기 =====
        void Awake()
        {
            Initialize();
            
#if UNITY_EDITOR
            // SpriteRenderer가 있다면 자동 변환 (백업 없이)
            ConvertFromSpriteRenderer();
#endif
        }

        void Start()
        {
            Initialize();
            AutoCreateAnchorPoints();
        }

        void OnEnable()
        {
            UpdateReferences();
            RefreshMesh();
        }

        void OnDisable()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        void Update()
        {
            if (updateEveryFrame && !useLateUpdate)
            {
                UpdateMesh();
            }
        }

        void LateUpdate()
        {
            if (updateEveryFrame && useLateUpdate)
            {
                UpdateMesh();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // 에디터에서 값이 변경될 때마다 업데이트
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    ValidateComponents();
                    UpdateReferences();
                    RefreshMesh();
                }
            };
        }

        void OnDestroy()
        {
            CleanupGeneratedAnchorPoints();
            CleanupMesh();
        }

        private void RestoreSpriteRenderer()
        {
            if (!hasBackupData) return;

            Debug.Log($"[{gameObject.name}] ImageDeform 제거 → SpriteRenderer 복원 중...");

            // SpriteRenderer 컴포넌트 추가
            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            
            // 백업된 정보 복원
            spriteRenderer.sprite = spriteRendererBackup.sprite;
            spriteRenderer.color = spriteRendererBackup.color;
            spriteRenderer.flipX = spriteRendererBackup.flipX;
            spriteRenderer.flipY = spriteRendererBackup.flipY;
            spriteRenderer.sortingLayerName = spriteRendererBackup.sortingLayerName;
            spriteRenderer.sortingOrder = spriteRendererBackup.sortingOrder;
            
            // 기본 머티리얼이 아닌 경우에만 적용
            if (spriteRendererBackup.material != null && 
                spriteRendererBackup.material.name != "Sprites-Default")
            {
                spriteRenderer.sharedMaterial = spriteRendererBackup.material;
            }

            // ImageDeform 관련 컴포넌트들 제거
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            CanvasRenderer canvasRenderer = gameObject.GetComponent<CanvasRenderer>();
            
            if (meshFilter != null) DestroyImmediate(meshFilter);
            if (meshRenderer != null) DestroyImmediate(meshRenderer);
            if (canvasRenderer != null) DestroyImmediate(canvasRenderer);
            
            // RectTransform을 일반 Transform으로 복원 (필요한 경우)
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform != null && gameObject.GetComponentInParent<Canvas>() == null)
            {
                // RectTransform을 일반 Transform으로 변환
                Vector3 position = rectTransform.position;
                Quaternion rotation = rectTransform.rotation;
                Vector3 scale = rectTransform.localScale;
                Transform parent = rectTransform.parent;
                
                DestroyImmediate(rectTransform);
                
                // Transform이 자동으로 추가되므로 별도 추가 불필요
                Transform newTransform = gameObject.transform;
                newTransform.SetParent(parent, false);
                newTransform.position = position;
                newTransform.rotation = rotation;
                newTransform.localScale = scale;
            }

            // 플립 복원 (Transform 스케일 정규화)
            Vector3 finalScale = gameObject.transform.localScale;
            finalScale.x = Mathf.Abs(finalScale.x);
            finalScale.y = Mathf.Abs(finalScale.y);
            gameObject.transform.localScale = finalScale;

            Debug.Log($"[{gameObject.name}] ImageDeform → SpriteRenderer 복원 완료!");
        }

        private void CleanupGeneratedAnchorPoints()
        {
            if (createdAnchorPoints == null) return;

            for (int i = createdAnchorPoints.Count - 1; i >= 0; i--)
            {
                if (createdAnchorPoints[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(createdAnchorPoints[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(createdAnchorPoints[i].gameObject);
                    }
                }
            }
            createdAnchorPoints.Clear();
        }

        private void ValidateComponents()
        {
            // 모드에 따른 컴포넌트 검증 및 추가
            if (deformMode == DeformMode.UIImage)
            {
                // UI 모드: Canvas 환경에서만 작동
                if (GetComponentInParent<Canvas>() == null)
                {
                    Debug.LogWarning($"[{gameObject.name}] UI 모드를 사용하려면 Canvas 하위에 있어야 합니다.");
                    return;
                }

                // RectTransform 확인 및 추가
                if (GetComponent<RectTransform>() == null)
                {
                    // 기존 Transform을 RectTransform으로 변환
                    Transform oldTransform = transform;
                    Vector3 position = oldTransform.position;
                    Quaternion rotation = oldTransform.rotation;
                    Vector3 scale = oldTransform.localScale;
                    Transform parent = oldTransform.parent;

                    DestroyImmediate(GetComponent<Transform>());
                    RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
                    
                    rectTransform.SetParent(parent, false);
                    rectTransform.position = position;
                    rectTransform.rotation = rotation;
                    rectTransform.localScale = scale;
                }

                // Graphic 컴포넌트 확인 및 추가
                if (GetComponent<Graphic>() == null)
                {
                    gameObject.AddComponent<Image>();
                }

                // Sprite 관련 컴포넌트 제거
                RemoveComponentSafely<MeshFilter>();
                RemoveComponentSafely<MeshRenderer>();
                RemoveComponentSafely<SpriteRenderer>();
            }
            else if (deformMode == DeformMode.Sprite)
            {
                // Sprite 모드: MeshFilter와 MeshRenderer 필요
                if (GetComponent<MeshFilter>() == null)
                {
                    MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
                    // MeshFilter를 ImageDeform 컴포넌트 바로 앞에 배치
                    MoveComponentBefore<MeshFilter, ImageDeform>();
                }
                if (GetComponent<MeshRenderer>() == null)
                {
                    MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
                    // MeshRenderer를 ImageDeform 컴포넌트 바로 앞에 배치
                    MoveComponentBefore<MeshRenderer, ImageDeform>();
                }

                // UI 관련 컴포넌트 제거
                RemoveComponentSafely<Graphic>();
                RemoveComponentSafely<Image>();
                RemoveComponentSafely<CanvasRenderer>();
                
                // RectTransform을 일반 Transform으로 변환 (필요한 경우)
                ConvertRectTransformToTransform();
            }
        }

        private void RemoveComponentSafely<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component != null)
            {
                if (Application.isPlaying)
                    Destroy(component);
                else
                    DestroyImmediate(component);
            }
        }

        private void ConvertRectTransformToTransform()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null && GetComponentInParent<Canvas>() == null)
            {
                // Canvas 밖에 있는 RectTransform을 일반 Transform으로 변환
                Vector3 position = rectTransform.position;
                Quaternion rotation = rectTransform.rotation;
                Vector3 scale = rectTransform.localScale;
                Transform parent = rectTransform.parent;
                
                DestroyImmediate(rectTransform);
                Transform newTransform = gameObject.AddComponent<Transform>();
                
                newTransform.SetParent(parent, false);
                newTransform.position = position;
                newTransform.rotation = rotation;
                newTransform.localScale = scale;
            }
        }

        private void MoveComponentBefore<TComponent, TTarget>() 
            where TComponent : Component 
            where TTarget : Component
        {
            // Unity의 컴포넌트 순서 변경은 제한적이므로 로그만 남김
            // 실제 순서 변경은 Unity Inspector에서 수동으로 해야 함
            Debug.Log($"[{gameObject.name}] {typeof(TComponent).Name} 컴포넌트가 추가되었습니다. " +
                     $"인스펙터에서 {typeof(TTarget).Name} 위로 이동시켜 주세요.");
        }
#endif

        // ===== 공개 메서드 =====
        public void SetTopLeftAnchor(Transform anchor) 
        {
            topLeft.anchorTarget = anchor;
            RefreshMesh();
        }

        public void SetTopRightAnchor(Transform anchor) 
        {
            topRight.anchorTarget = anchor;
            RefreshMesh();
        }

        public void SetBottomLeftAnchor(Transform anchor) 
        {
            bottomLeft.anchorTarget = anchor;
            RefreshMesh();
        }

        public void SetBottomRightAnchor(Transform anchor) 
        {
            bottomRight.anchorTarget = anchor;
            RefreshMesh();
        }

        public void SetSprite(Sprite newSprite)
        {
            sprite = newSprite;
            if (sprite != null)
            {
                spriteName = sprite.name.Replace("(Clone)", "");
            }
            else
            {
                spriteName = null;
            }
            
            if (deformMode == DeformMode.Sprite)
            {
                SetupMaterial();
            }
            
            RefreshMesh();
        }

        public void SetColor(Color newColor)
        {
            spriteColor = newColor;
            if (deformMode == DeformMode.UIImage && graphic != null)
            {
                graphic.color = newColor;
            }
            RefreshMesh();
        }

        public void SetDeformMode(DeformMode mode)
        {
            if (deformMode != mode)
            {
                deformMode = mode;
#if UNITY_EDITOR
                ValidateComponents();
#endif
                UpdateReferences();
                RefreshMesh();
            }
        }

        public void RefreshMesh()
        {
            if (deformMode == DeformMode.UIImage && graphic != null)
            {
                graphic.SetVerticesDirty();
            }
            else if (deformMode == DeformMode.Sprite)
            {
                UpdateSpriteMesh();
            }
        }

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

        // ===== 비공개 메서드 =====
        private void Initialize()
        {
#if UNITY_EDITOR
            ValidateComponents();
#endif
            UpdateReferences();
        }

        private void UpdateReferences()
        {
            if (deformMode == DeformMode.UIImage)
            {
                graphic = GetComponent<Graphic>();
                parentCanvas = GetComponentInParent<Canvas>();
            }
            else if (deformMode == DeformMode.Sprite)
            {
                meshFilter = GetComponent<MeshFilter>();
                meshRenderer = GetComponent<MeshRenderer>();
                CreateMesh();
                SetupMaterial();
            }
        }

        private void AutoCreateAnchorPoints()
        {
            if (ShouldCreateAnchorPoints())
            {
                CreateAnchorPoints();
            }
        }

        private void UpdateMesh()
        {
            if (optimizePerformance && !HasAnchorMoved())
                return;

            RefreshMesh();
        }

        private bool HasAnchorMoved()
        {
            // 간단한 구현: 매번 업데이트 (더 복잡한 최적화 가능)
            return true;
        }

        // ===== IMeshModifier 구현 (UI Image용) =====
        public void ModifyMesh(VertexHelper vh)
        {
            if (deformMode != DeformMode.UIImage || graphic == null) return;

            List<UIVertex> vertices = new List<UIVertex>();
            vh.GetUIVertexStream(vertices);

            if (vertices.Count == 0) return;

            vh.Clear();

            if (useSubdivision)
            {
                CreateSubdividedUIMesh(vh, vertices);
            }
            else
            {
                CreateSimpleUIMesh(vh, vertices);
            }

            UpdatePerformanceInfo(vh);
            lastMeshVertices.Clear();
            vh.GetUIVertexStream(lastMeshVertices);
        }

        public void ModifyMesh(Mesh mesh)
        {
            // Legacy method - 사용하지 않음
        }

        private void CreateSimpleUIMesh(VertexHelper vh, List<UIVertex> originalVertices)
        {
            if (originalVertices.Count < 4) return;

            Vector4 uvRect = GetAdjustedUV();

            UIVertex tl = CreateUIVertex(GetAnchorPosition(topLeft, originalVertices[0].position), new Vector2(uvRect.x, uvRect.w));
            UIVertex tr = CreateUIVertex(GetAnchorPosition(topRight, originalVertices[1].position), new Vector2(uvRect.z, uvRect.w));
            UIVertex bl = CreateUIVertex(GetAnchorPosition(bottomLeft, originalVertices[2].position), new Vector2(uvRect.x, uvRect.y));
            UIVertex br = CreateUIVertex(GetAnchorPosition(bottomRight, originalVertices[3].position), new Vector2(uvRect.z, uvRect.y));

            vh.AddVert(tl);
            vh.AddVert(tr);
            vh.AddVert(bl);
            vh.AddVert(br);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 1, 3);
        }

        private void CreateSubdividedUIMesh(VertexHelper vh, List<UIVertex> originalVertices)
        {
            if (originalVertices.Count < 4) return;

            Vector4 uvRect = GetAdjustedUV();
            int subdivisions = (int)subdivisionLevel;

            for (int y = 0; y <= subdivisions; y++)
            {
                float yLerp = (float)y / subdivisions;
                
                for (int x = 0; x <= subdivisions; x++)
                {
                    float xLerp = (float)x / subdivisions;
                    
                    Vector3 position = CalculateBilinearPosition(xLerp, yLerp);
                    Vector2 uv = new Vector2(
                        Mathf.Lerp(uvRect.x, uvRect.z, xLerp),
                        Mathf.Lerp(uvRect.y, uvRect.w, yLerp)
                    );
                    
                    vh.AddVert(CreateUIVertex(position, uv));
                }
            }

            for (int y = 0; y < subdivisions; y++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int index = y * (subdivisions + 1) + x;
                    
                    vh.AddTriangle(index, index + 1, index + subdivisions + 1);
                    vh.AddTriangle(index + 1, index + subdivisions + 2, index + subdivisions + 1);
                }
            }
        }

        private UIVertex CreateUIVertex(Vector3 position, Vector2 uv)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.uv0 = uv;
            
            if (graphic != null)
            {
                Color color = graphic.color;
                
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

        // ===== Sprite 메시 처리 =====
        private void UpdateSpriteMesh()
        {
            if (sprite == null || meshFilter == null) return;

            CreateMesh();

            Vector2[] spriteVertices = sprite.vertices;
            Vector2[] spriteUVs = sprite.uv;
            ushort[] spriteTriangles = sprite.triangles;

            if (spriteVertices.Length > 4 && spriteTriangles.Length > 0)
            {
                // Tight mesh 처리
                CreateTightSpriteMesh(spriteVertices, spriteUVs, spriteTriangles);
            }
            else
            {
                // Rectangle mesh 처리
                CreateRectangleSpriteMesh();
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            meshFilter.mesh = mesh;
        }

        private void CreateTightSpriteMesh(Vector2[] spriteVertices, Vector2[] spriteUVs, ushort[] spriteTriangles)
        {
            Bounds spriteBounds = sprite.bounds;
            
            Vector2 tl = GetAnchorWorldPosition(topLeft, new Vector2(spriteBounds.min.x, spriteBounds.max.y));
            Vector2 tr = GetAnchorWorldPosition(topRight, new Vector2(spriteBounds.max.x, spriteBounds.max.y));
            Vector2 bl = GetAnchorWorldPosition(bottomLeft, new Vector2(spriteBounds.min.x, spriteBounds.min.y));
            Vector2 br = GetAnchorWorldPosition(bottomRight, new Vector2(spriteBounds.max.x, spriteBounds.min.y));

            InitializeArrays(spriteVertices.Length);

            for (int i = 0; i < spriteVertices.Length; i++)
            {
                float u = (spriteVertices[i].x - spriteBounds.min.x) / spriteBounds.size.x;
                float v = (spriteVertices[i].y - spriteBounds.min.y) / spriteBounds.size.y;

                Vector2 deformedPosition = BilinearInterpolate(bl, br, tl, tr, u, v);
                vertices[i] = new Vector3(deformedPosition.x, deformedPosition.y, 0);
                uvs[i] = spriteUVs[i];
                colors[i] = spriteColor;
            }

            if (triangles == null || triangles.Length != spriteTriangles.Length)
            {
                triangles = new int[spriteTriangles.Length];
            }
            for (int i = 0; i < spriteTriangles.Length; i++)
            {
                triangles[i] = spriteTriangles[i];
            }
        }

        private void CreateRectangleSpriteMesh()
        {
            int subdivisions = (int)subdivisionLevel;
            int vertexCount = (subdivisions + 1) * (subdivisions + 1);
            
            InitializeArrays(vertexCount);

            Vector2[] spriteUVs = sprite.uv;
            Vector2 uvMin = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 uvMax = new Vector2(float.MinValue, float.MinValue);
            foreach (var uv in spriteUVs)
            {
                uvMin = Vector2.Min(uvMin, uv);
                uvMax = Vector2.Max(uvMax, uv);
            }

            Vector2 tl = GetAnchorWorldPosition(topLeft, Vector2.zero);
            Vector2 tr = GetAnchorWorldPosition(topRight, Vector2.zero);
            Vector2 bl = GetAnchorWorldPosition(bottomLeft, Vector2.zero);
            Vector2 br = GetAnchorWorldPosition(bottomRight, Vector2.zero);

            int vertexIndex = 0;
            for (int y = 0; y <= subdivisions; y++)
            {
                float v = y / (float)subdivisions;
                
                for (int x = 0; x <= subdivisions; x++)
                {
                    float u = x / (float)subdivisions;
                    
                    Vector2 position = BilinearInterpolate(bl, br, tl, tr, u, v);
                    vertices[vertexIndex] = new Vector3(position.x, position.y, 0);
                    
                    float uvX = Mathf.Lerp(uvMin.x, uvMax.x, u);
                    float uvY = Mathf.Lerp(uvMin.y, uvMax.y, v);
                    uvs[vertexIndex] = new Vector2(uvX, uvY);
                    
                    colors[vertexIndex] = spriteColor;
                    vertexIndex++;
                }
            }
            
            if (triangles == null || triangles.Length != subdivisions * subdivisions * 6)
            {
                triangles = new int[subdivisions * subdivisions * 6];
            }
            int triangleIndex = 0;
            for (int y = 0; y < subdivisions; y++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int bottomLeftIndex = y * (subdivisions + 1) + x;
                    int bottomRightIndex = bottomLeftIndex + 1;
                    int topLeftIndex = bottomLeftIndex + subdivisions + 1;
                    int topRightIndex = topLeftIndex + 1;
                    
                    triangles[triangleIndex++] = bottomLeftIndex;
                    triangles[triangleIndex++] = topLeftIndex;
                    triangles[triangleIndex++] = bottomRightIndex;
                    
                    triangles[triangleIndex++] = bottomRightIndex;
                    triangles[triangleIndex++] = topLeftIndex;
                    triangles[triangleIndex++] = topRightIndex;
                }
            }
        }

        // ===== 유틸리티 메서드 =====
        private Vector3 GetAnchorPosition(VertexAnchor anchor, Vector2 originalPosition)
        {
            if (!anchor.useAnchor || anchor.anchorTarget == null)
            {
                return originalPosition;
            }

            if (deformMode == DeformMode.UIImage)
            {
                bool isChildAnchor = anchor.anchorTarget.IsChildOf(transform);
                
                if (isChildAnchor)
                {
                    RectTransform anchorRect = anchor.anchorTarget as RectTransform;
                    if (anchorRect != null)
                    {
                        return anchorRect.anchoredPosition + anchor.localOffset;
                    }
                }
                
                Vector3 worldPosition = anchor.anchorTarget.position + (Vector3)anchor.localOffset;
                return WorldToCanvasPosition(worldPosition);
            }
            else
            {
                return anchor.anchorTarget.position + (Vector3)anchor.localOffset;
            }
        }

        private Vector2 GetAnchorWorldPosition(VertexAnchor anchor, Vector2 originalPosition)
        {
            if (!anchor.useAnchor || anchor.anchorTarget == null)
            {
                return originalPosition;
            }

            return anchor.anchorTarget.position + (Vector3)anchor.localOffset;
        }

        private Vector3 CalculateBilinearPosition(float x, float y)
        {
            if (deformMode == DeformMode.UIImage)
            {
                RectTransform rectTransform = transform as RectTransform;
                if (rectTransform == null) return Vector3.zero;

                Rect rect = rectTransform.rect;

                Vector3 originalTL = new Vector3(rect.xMin, rect.yMax, 0f);
                Vector3 originalTR = new Vector3(rect.xMax, rect.yMax, 0f);
                Vector3 originalBL = new Vector3(rect.xMin, rect.yMin, 0f);
                Vector3 originalBR = new Vector3(rect.xMax, rect.yMin, 0f);

                Vector3 deformedTL = GetAnchorPosition(topLeft, originalTL);
                Vector3 deformedTR = GetAnchorPosition(topRight, originalTR);
                Vector3 deformedBL = GetAnchorPosition(bottomLeft, originalBL);
                Vector3 deformedBR = GetAnchorPosition(bottomRight, originalBR);

                Vector3 top = Vector3.Lerp(deformedTL, deformedTR, x);
                Vector3 bottom = Vector3.Lerp(deformedBL, deformedBR, x);
                return Vector3.Lerp(bottom, top, y);
            }
            else
            {
                Vector2 tl = GetAnchorWorldPosition(topLeft, Vector2.zero);
                Vector2 tr = GetAnchorWorldPosition(topRight, Vector2.zero);
                Vector2 bl = GetAnchorWorldPosition(bottomLeft, Vector2.zero);
                Vector2 br = GetAnchorWorldPosition(bottomRight, Vector2.zero);
                
                return BilinearInterpolate(bl, br, tl, tr, x, y);
            }
        }

        private Vector2 BilinearInterpolate(Vector2 p00, Vector2 p10, Vector2 p01, Vector2 p11, float u, float v)
        {
            float u1 = 1f - u;
            float v1 = 1f - v;
            
            return new Vector2(
                p00.x * u1 * v1 + p10.x * u * v1 + p01.x * u1 * v + p11.x * u * v,
                p00.y * u1 * v1 + p10.y * u * v1 + p01.y * u1 * v + p11.y * u * v
            );
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

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                screenPoint,
                canvasCamera,
                out localPoint);

            return localPoint;
        }

        private Vector4 GetAdjustedUV()
        {
            if (deformMode == DeformMode.UIImage)
            {
                Image image = graphic as Image;
                if (image != null && image.sprite != null)
                {
                    Sprite currentSprite = image.overrideSprite ?? image.sprite;
                    
                    if (currentSprite.packed && currentSprite.packingMode != SpritePackingMode.Tight)
                    {
                        Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(currentSprite);
                        
                        if (image.type == Image.Type.Simple || image.type == Image.Type.Filled)
                        {
                            return outerUV;
                        }
                        else if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled)
                        {
                            Vector4 innerUV = UnityEngine.Sprites.DataUtility.GetInnerUV(currentSprite);
                            return innerUV;
                        }
                    }
                    else
                    {
                        return UnityEngine.Sprites.DataUtility.GetOuterUV(currentSprite);
                    }
                }
            }
            
            return new Vector4(0f, 0f, 1f, 1f);
        }

        // ===== 앵커포인트 생성 =====
        private bool ShouldCreateAnchorPoints()
        {
            if (topLeft.anchorTarget != null || topRight.anchorTarget != null ||
                bottomLeft.anchorTarget != null || bottomRight.anchorTarget != null)
            {
                return false;
            }

            Transform tl = transform.Find("TL");
            Transform tr = transform.Find("TR");
            Transform bl = transform.Find("BL");
            Transform br = transform.Find("BR");

            return tl == null && tr == null && bl == null && br == null;
        }

        private void CreateAnchorPoints()
        {
            Vector2 topLeftPos, topRightPos, bottomLeftPos, bottomRightPos;

            if (deformMode == DeformMode.UIImage)
            {
                RectTransform rectTransform = transform as RectTransform;
                if (rectTransform == null) return;

                Rect rect = rectTransform.rect;
                topLeftPos = new Vector2(rect.xMin, rect.yMax);
                topRightPos = new Vector2(rect.xMax, rect.yMax);
                bottomLeftPos = new Vector2(rect.xMin, rect.yMin);
                bottomRightPos = new Vector2(rect.xMax, rect.yMin);
            }
            else
            {
                if (sprite == null) return;
                
                Bounds bounds = sprite.bounds;
                topLeftPos = new Vector2(bounds.min.x, bounds.max.y);
                topRightPos = new Vector2(bounds.max.x, bounds.max.y);
                bottomLeftPos = new Vector2(bounds.min.x, bounds.min.y);
                bottomRightPos = new Vector2(bounds.max.x, bounds.min.y);
            }

            GameObject tlObject = CreateAnchorPoint("TL", topLeftPos);
            GameObject trObject = CreateAnchorPoint("TR", topRightPos);
            GameObject blObject = CreateAnchorPoint("BL", bottomLeftPos);
            GameObject brObject = CreateAnchorPoint("BR", bottomRightPos);

            createdAnchorPoints.Add(tlObject.transform);
            createdAnchorPoints.Add(trObject.transform);
            createdAnchorPoints.Add(blObject.transform);
            createdAnchorPoints.Add(brObject.transform);

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
            GameObject anchorPoint = new GameObject(name);
            anchorPoint.transform.SetParent(transform, false);

            if (deformMode == DeformMode.UIImage)
            {
                RectTransform anchorRect = anchorPoint.AddComponent<RectTransform>();
                anchorRect.anchorMin = Vector2.one * 0.5f;
                anchorRect.anchorMax = Vector2.one * 0.5f;
                anchorRect.pivot = Vector2.one * 0.5f;
                anchorRect.anchoredPosition = localPosition;
                anchorRect.sizeDelta = Vector2.zero;
                anchorRect.localScale = Vector3.one;
                anchorRect.localRotation = Quaternion.identity;
            }
            else
            {
                anchorPoint.transform.localPosition = localPosition;
                anchorPoint.transform.localScale = Vector3.one;
                anchorPoint.transform.localRotation = Quaternion.identity;
            }

#if UNITY_EDITOR
            // 에디터에서 기즈모 아이콘 설정
            Texture2D iconTexture = EditorGUIUtility.IconContent("sv_icon_dot8_pix16_gizmo").image as Texture2D;
            if (iconTexture != null)
            {
                EditorGUIUtility.SetIconForObject(anchorPoint, iconTexture);
            }

            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(anchorPoint, "Create Anchor Point");
            }
#endif

            return anchorPoint;
        }

        // ===== 메시 관리 =====
        private void CreateMesh()
        {
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = $"{gameObject.name} Deform Mesh";
            }
        }

        private void CleanupMesh()
        {
            if (mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(mesh);
                else
                    DestroyImmediate(mesh);
            }

            if (!useSharedMaterial && instanceMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(instanceMaterial);
                else
                    DestroyImmediate(instanceMaterial);
            }
        }

        private void InitializeArrays(int vertexCount)
        {
            if (vertices == null || vertices.Length != vertexCount)
            {
                vertices = new Vector3[vertexCount];
                uvs = new Vector2[vertexCount];
                colors = new Color[vertexCount];
            }
        }

        private void SetupMaterial()
        {
            if (deformMode != DeformMode.Sprite || meshRenderer == null || sprite == null)
                return;

            Material targetMaterial = null;

            if (useSharedMaterial)
            {
                Texture2D spriteTexture = sprite.texture;
                if (!sharedMaterials.TryGetValue(spriteTexture, out targetMaterial))
                {
                    targetMaterial = CreateSpriteMaterial(spriteTexture);
                    sharedMaterials[spriteTexture] = targetMaterial;
                }
            }
            else
            {
                if (instanceMaterial == null)
                {
                    instanceMaterial = CreateSpriteMaterial(sprite.texture);
                }
                targetMaterial = instanceMaterial;
            }

            meshRenderer.material = targetMaterial;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }

        private Material CreateSpriteMaterial(Texture2D texture)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = texture;
            mat.name = $"Sprite Deform Material ({texture.name})";
            return mat;
        }

        // ===== 성능 및 디버그 =====
        private void UpdatePerformanceInfo(VertexHelper vh)
        {
            lastVertexCount = vh.currentVertCount;
            lastTriangleCount = vh.currentIndexCount / 3;

#if UNITY_EDITOR
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

        public void LogPerformanceInfo()
        {
            int vertexCount, triangleCount;
            
            if (useSubdivision)
            {
                int subdivisions = (int)subdivisionLevel;
                vertexCount = (subdivisions + 1) * (subdivisions + 1);
                triangleCount = subdivisions * subdivisions * 2;
            }
            else
            {
                vertexCount = 4;
                triangleCount = 2;
            }

            Debug.Log($"[{gameObject.name}] 성능 정보:\n" +
                     $"- 모드: {deformMode}\n" +
                     $"- Subdivision 사용: {useSubdivision}\n" +
                     $"- 분할 설정: {subdivisionLevel}\n" +
                     $"- Vertex 수: {vertexCount}\n" +
                     $"- 삼각형 수: {triangleCount}\n" +
                     $"- 모바일 권장: {(vertexCount <= 25 ? "예" : "아니오 (너무 많은 정점)")}");
        }

        // ===== 기즈모 그리기 =====
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!showVertices) return;

            Gizmos.color = Color.cyan;
            
            if (deformMode == DeformMode.UIImage)
            {
                DrawUIGizmos();
            }
            else if (deformMode == DeformMode.Sprite && vertices != null)
            {
                DrawSpriteGizmos();
            }
        }

        private void DrawUIGizmos()
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null) return;

            Rect rect = rectTransform.rect;
            Vector3[] corners = new Vector3[4];
            corners[0] = GetAnchorPosition(topLeft, new Vector3(rect.xMin, rect.yMax, 0f));
            corners[1] = GetAnchorPosition(topRight, new Vector3(rect.xMax, rect.yMax, 0f));
            corners[2] = GetAnchorPosition(bottomRight, new Vector3(rect.xMax, rect.yMin, 0f));
            corners[3] = GetAnchorPosition(bottomLeft, new Vector3(rect.xMin, rect.yMin, 0f));

            // 로컬 좌표를 월드 좌표로 변환
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = transform.TransformPoint(corners[i]);
                Gizmos.DrawWireSphere(corners[i], gizmoSize);
            }

            // 변형된 사각형 그리기
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);
        }

        private void DrawSpriteGizmos()
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(vertices[i]);
                Gizmos.DrawWireSphere(worldPos, gizmoSize);
            }
        }
#endif

        // ===== 정적 메서드 (정리) =====
        private void OnApplicationQuit()
        {
            sharedMaterials.Clear();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ImageDeform))]
    public class ImageDeformEditor : Editor
    {
        private SerializedProperty deformModeProp;
        private SerializedProperty useSubdivisionProp;
        private SerializedProperty subdivisionLevelProp;
        private SerializedProperty spriteProp;
        private SerializedProperty spriteColorProp;
        private SerializedProperty sortingLayerProp;
        private SerializedProperty sortingOrderProp;

        private void OnEnable()
        {
            deformModeProp = serializedObject.FindProperty("deformMode");
            useSubdivisionProp = serializedObject.FindProperty("useSubdivision");
            subdivisionLevelProp = serializedObject.FindProperty("subdivisionLevel");
            spriteProp = serializedObject.FindProperty("sprite");
            spriteColorProp = serializedObject.FindProperty("spriteColor");
            sortingLayerProp = serializedObject.FindProperty("sortingLayerName");
            sortingOrderProp = serializedObject.FindProperty("sortingOrder");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ImageDeform imageDeform = (ImageDeform)target;

            // 변형 모드 선택
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(deformModeProp, new GUIContent("변형 모드"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                imageDeform.SetDeformMode((ImageDeform.DeformMode)deformModeProp.enumValueIndex);
            }

            EditorGUILayout.Space();

            // 앵커 설정
            DrawPropertiesExcluding(serializedObject, 
                "m_Script", "deformMode", "useSubdivision", "subdivisionLevel",
                "sprite", "spriteColor", "sortingLayerName", "sortingOrder",
                "spriteAtlas", "spriteName", "updateEveryFrame", "optimizePerformance", 
                "useLateUpdate", "useSharedMaterial", "showPerformanceInfo", 
                "showVertices", "gizmoSize");

            EditorGUILayout.Space();

            // 메시 분할 설정
            
            // Sprite 모드에서 Tight Mesh인지 확인
            bool isTightMesh = imageDeform.CurrentDeformMode == ImageDeform.DeformMode.Sprite && 
                              imageDeform.CurrentSprite != null && 
                              imageDeform.CurrentSprite.triangles.Length > 0 && 
                              imageDeform.CurrentSprite.vertices.Length > 4;
            
            if (isTightMesh)
            {
                EditorGUILayout.HelpBox("Tight Mesh 스프라이트는 자동으로 원본 메시를 사용합니다.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(useSubdivisionProp, new GUIContent("메시 분할 사용"));
                if (useSubdivisionProp.boolValue)
                {
                    EditorGUILayout.PropertyField(subdivisionLevelProp, new GUIContent("분할 레벨"));
                }
            }

            // 모드별 설정
            if (imageDeform.CurrentDeformMode == ImageDeform.DeformMode.Sprite)
            {
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(spriteProp);
                EditorGUILayout.PropertyField(spriteColorProp);
                
                // Sorting Layer 설정
                string[] sortingLayerNames = GetSortingLayerNames();
                int currentLayerIndex = System.Array.IndexOf(sortingLayerNames, sortingLayerProp.stringValue);
                if (currentLayerIndex == -1) currentLayerIndex = 0;

                EditorGUI.BeginChangeCheck();
                int newLayerIndex = EditorGUILayout.Popup("Sorting Layer", currentLayerIndex, sortingLayerNames);
                if (EditorGUI.EndChangeCheck())
                {
                    sortingLayerProp.stringValue = sortingLayerNames[newLayerIndex];
                }

                EditorGUILayout.PropertyField(sortingOrderProp);
            }

            // 성능 및 디버그 설정
            EditorGUILayout.Space();
            SerializedProperty iterator = serializedObject.GetIterator();
            iterator.NextVisible(true);
            while (iterator.NextVisible(false))
            {
                if (iterator.name.StartsWith("updateEveryFrame") ||
                    iterator.name.StartsWith("optimizePerformance") ||
                    iterator.name.StartsWith("useLateUpdate") ||
                    iterator.name.StartsWith("useSharedMaterial") ||
                    iterator.name.StartsWith("showPerformanceInfo") ||
                    iterator.name.StartsWith("showVertices") ||
                    iterator.name.StartsWith("gizmoSize"))
                {
                    EditorGUILayout.PropertyField(iterator);
                }
            }

            // 성능 정보 표시
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("실시간 성능 정보", EditorStyles.boldLabel);
                
                int vertexCount, triangleCount;
                if (imageDeform.UseSubdivision)
                {
                    int subdivisions = (int)imageDeform.CurrentSubdivisionLevel;
                    vertexCount = (subdivisions + 1) * (subdivisions + 1);
                    triangleCount = subdivisions * subdivisions * 2;
                }
                else
                {
                    vertexCount = 4;
                    triangleCount = 2;
                }
                
                EditorGUILayout.LabelField($"Vertices: {vertexCount}");
                EditorGUILayout.LabelField($"Triangles: {triangleCount}");
                EditorGUILayout.LabelField($"Material Sharing: {(imageDeform.UseSharedMaterial ? "Enabled" : "Disabled")}");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private string[] GetSortingLayerNames()
        {
            var layers = SortingLayer.layers;
            var layerNames = new string[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                layerNames[i] = layers[i].name;
            }
            return layerNames;
        }
    }

    // ===== SpriteRenderer 자동 변환 시스템 =====
#if UNITY_EDITOR
    public partial class ImageDeform
    {
        // SpriteRenderer 정보 백업용 구조체
        [System.Serializable]
        private struct SpriteRendererBackup
        {
            public Sprite sprite;
            public Color color;
            public bool flipX;
            public bool flipY;
            public string sortingLayerName;
            public int sortingOrder;
            public Material material;
        }

        [HideInInspector][SerializeField] private SpriteRendererBackup spriteRendererBackup;
        [HideInInspector][SerializeField] private bool hasBackupData = false;

        private void ConvertFromSpriteRenderer()
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;

            Debug.Log($"[{gameObject.name}] SpriteRenderer에서 ImageDeform으로 자동 변환 중...");

            // SpriteRenderer 정보 백업
            spriteRendererBackup = new SpriteRendererBackup
            {
                sprite = spriteRenderer.sprite,
                color = spriteRenderer.color,
                flipX = spriteRenderer.flipX,
                flipY = spriteRenderer.flipY,
                sortingLayerName = spriteRenderer.sortingLayerName,
                sortingOrder = spriteRenderer.sortingOrder,
                material = spriteRenderer.sharedMaterial  // sharedMaterial 사용
            };
            hasBackupData = true;

            // ImageDeform 설정 적용
            deformMode = DeformMode.Sprite;
            sprite = spriteRendererBackup.sprite;
            spriteColor = spriteRendererBackup.color;
            sortingLayerName = spriteRendererBackup.sortingLayerName;
            sortingOrder = spriteRendererBackup.sortingOrder;

            // Undo 시스템에 등록
            Undo.RecordObject(this, "Convert SpriteRenderer to ImageDeform");
            Undo.DestroyObjectImmediate(spriteRenderer);

            // 필요한 컴포넌트 추가
            ValidateComponents();

            // 플립 적용 (필요시)
            if (spriteRendererBackup.flipX || spriteRendererBackup.flipY)
            {
                ApplyFlip(spriteRendererBackup.flipX, spriteRendererBackup.flipY);
            }

            EditorUtility.SetDirty(this);
            Debug.Log($"[{gameObject.name}] SpriteRenderer → ImageDeform 변환 완료!");
        }

        private void ConvertToSpriteRenderer()
        {
            // 백업 데이터가 없거나 Sprite 모드가 아니면 복원하지 않음
            if (!hasBackupData || deformMode != DeformMode.Sprite) return;

            Debug.Log($"[{gameObject.name}] ImageDeform에서 SpriteRenderer로 복원 중...");

            // SpriteRenderer 컴포넌트 추가
            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            
            // 백업된 정보 복원
            spriteRenderer.sprite = spriteRendererBackup.sprite;
            spriteRenderer.color = spriteRendererBackup.color;
            spriteRenderer.flipX = spriteRendererBackup.flipX;
            spriteRenderer.flipY = spriteRendererBackup.flipY;
            spriteRenderer.sortingLayerName = spriteRendererBackup.sortingLayerName;
            spriteRenderer.sortingOrder = spriteRendererBackup.sortingOrder;
            
            // 기본 머티리얼이 아닌 경우에만 적용
            if (spriteRendererBackup.material != null && 
                spriteRendererBackup.material.name != "Sprites-Default")
            {
                spriteRenderer.sharedMaterial = spriteRendererBackup.material;  // sharedMaterial 사용
            }

            // Undo 시스템에 등록
            Undo.RegisterCreatedObjectUndo(spriteRenderer, "Restore SpriteRenderer");

            // ImageDeform 관련 컴포넌트들 제거
            RemoveComponentSafely<MeshFilter>();
            RemoveComponentSafely<MeshRenderer>();
            RemoveComponentSafely<CanvasRenderer>();
            
            // RectTransform을 일반 Transform으로 복원 (필요한 경우)
            ConvertRectTransformToTransform();

            // 플립 복원 (Transform 스케일 정규화)
            RestoreOriginalScale();

            Debug.Log($"[{gameObject.name}] ImageDeform → SpriteRenderer 복원 완료!");
        }

        private void RestoreOriginalScale()
        {
            // 플립으로 인한 음수 스케일을 원래대로 복원
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x);
            scale.y = Mathf.Abs(scale.y);
            transform.localScale = scale;
        }

        private void ApplyFlip(bool flipX, bool flipY)
        {
            // Transform 스케일을 통한 플립 구현
            Vector3 scale = transform.localScale;
            if (flipX) scale.x = -Mathf.Abs(scale.x);
            if (flipY) scale.y = -Mathf.Abs(scale.y);
            transform.localScale = scale;
        }

        // 수동 변환 메서드들 (필요시 사용)
        public void ManualConvertFromSpriteRenderer()
        {
            ConvertFromSpriteRenderer();
        }

        public void ManualConvertToSpriteRenderer()
        {
            RestoreSpriteRenderer();
        }
    }
#endif
#endif
}