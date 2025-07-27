using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Effects
{
    /// <summary>
    /// Sprite의 4개 모서리를 앵커로 설정하여 메시를 변형하는 컴포넌트입니다.
    /// MeshRenderer를 사용하여 독립적으로 작동합니다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class SpriteMultiAnchor : MonoBehaviour
    {
        [System.Serializable]
        public class VertexAnchor
        {
            [Header("앵커 설정")]
            public Transform anchorTarget;      // 앵커할 타겟 Transform
            public Vector2 localOffset;        // 타겟으로부터의 로컬 오프셋
            public bool useAnchor = true;       // 이 vertex에 앵커를 적용할지 여부
        }

        [Header("Sprite 설정")]
        [SerializeField] private Sprite targetSprite;
        [SerializeField] private Color spriteColor = Color.white;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 0;
        [SerializeField] private bool flipX = false;
        [SerializeField] private bool flipY = false;

        [Header("Vertex 앵커 설정")]
        [SerializeField] private VertexAnchor topLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor topRight = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomLeft = new VertexAnchor();
        [SerializeField] private VertexAnchor bottomRight = new VertexAnchor();

        [Header("메시 분할 설정 (모바일 최적화)")]
        [SerializeField] private bool useSubdivision = false;
        [SerializeField][Range(2, 6)] private int subdivisionX = 2;
        [SerializeField][Range(2, 6)] private int subdivisionY = 2;
        [Space]
        [SerializeField] private bool showPerformanceInfo = true;

        [Header("업데이트 설정")]
        [SerializeField] private bool updateEveryFrame = true;
        [SerializeField] private bool optimizePerformance = true;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh customMesh;
        private Material spriteMaterial;
        private Vector3[] lastAnchorPositions = new Vector3[4];
        private bool needsUpdate = true;
        private Sprite lastSprite;
        private Texture2D lastTexture;

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
            
            // 기본 Sprite 설정 시도
            SpriteRenderer existingSpriteRenderer = GetComponent<SpriteRenderer>();
            if (existingSpriteRenderer != null && existingSpriteRenderer.sprite != null)
            {
                targetSprite = existingSpriteRenderer.sprite;
                spriteColor = existingSpriteRenderer.color;
                sortingLayerName = SortingLayer.IDToName(existingSpriteRenderer.sortingLayerID);
                sortingOrder = existingSpriteRenderer.sortingOrder;
                flipX = existingSpriteRenderer.flipX;
                flipY = existingSpriteRenderer.flipY;
                
                // SpriteRenderer 제거 옵션 제공
                if (EditorUtility.DisplayDialog("SpriteMultiAnchor", 
                    "기존 SpriteRenderer의 설정을 복사했습니다.\nSpriteRenderer를 제거하시겠습니까?", 
                    "제거", "유지"))
                {
                    DestroyImmediate(existingSpriteRenderer);
                }
            }

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
            SetupMeshComponents();

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

            // 메시와 머티리얼 정리
            CleanupResources();
        }

        private void InitializeComponents()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            // 모바일 성능 경고
            if (useSubdivision && (subdivisionX > 4 || subdivisionY > 4))
            {
                Debug.LogWarning($"[{gameObject.name}] 모바일에서는 subdivision 4x4 이하를 권장합니다. 현재: {subdivisionX}x{subdivisionY}");
            }
        }

        private void SetupMeshComponents()
        {
            if (customMesh == null)
            {
                customMesh = new Mesh();
                customMesh.name = "SpriteMultiAnchor Mesh";
                customMesh.MarkDynamic(); // 동적 메시로 설정
            }

            if (meshFilter != null)
            {
                meshFilter.mesh = customMesh;
            }

            // Sprite 머티리얼 생성 또는 업데이트
            UpdateSpriteMaterial();

            // 렌더러 설정
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                
                UpdateSortingSettings();
            }
        }

        private void UpdateSpriteMaterial()
        {
            if (targetSprite == null) return;

            // 텍스처가 변경되었는지 확인
            bool needNewMaterial = spriteMaterial == null || 
                                 spriteMaterial.mainTexture != targetSprite.texture ||
                                 lastTexture != targetSprite.texture;

            if (needNewMaterial)
            {
                // 기존 머티리얼 정리
                if (spriteMaterial != null && spriteMaterial.name.Contains("Instance"))
                {
                    if (Application.isPlaying)
                        Destroy(spriteMaterial);
                    else
                        DestroyImmediate(spriteMaterial);
                }

                // 새 머티리얼 생성
                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader == null)
                    spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (spriteShader == null)
                    spriteShader = Shader.Find("Unlit/Transparent");

                spriteMaterial = new Material(spriteShader);
                spriteMaterial.name = $"SpriteMultiAnchor Material ({targetSprite.name})";
                spriteMaterial.mainTexture = targetSprite.texture;
                
                lastTexture = targetSprite.texture;
            }

            // 색상 업데이트
            spriteMaterial.color = spriteColor;

            // 렌더러에 적용
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = spriteMaterial;
            }
        }

        private void UpdateSortingSettings()
        {
            if (meshRenderer == null) return;

            // Sorting Layer 설정
            int layerID = SortingLayer.NameToID(sortingLayerName);
            if (layerID != 0)
            {
                meshRenderer.sortingLayerID = layerID;
            }
            else
            {
                meshRenderer.sortingLayerID = SortingLayer.NameToID("Default");
            }

            meshRenderer.sortingOrder = sortingOrder;
        }

        private void Start()
        {
            // 초기화
            ForceUpdate();
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null) return;
            
            // 컴포넌트가 파괴되었거나 비활성화된 경우 체크
            if (!this)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }

            // Editor에서 성능을 위해 업데이트 주기 제한
            float currentTime = (float)EditorApplication.timeSinceStartup;
            if (currentTime - lastEditorUpdateTime < EDITOR_UPDATE_INTERVAL) return;

            lastEditorUpdateTime = currentTime;
            CheckForChanges();
        }

        private void OnValidate()
        {
            // Inspector에서 값이 변경될 때
            if (meshFilter == null || meshRenderer == null)
                InitializeComponents();

            UpdateSpriteMaterial();
            UpdateSortingSettings();
            ForceUpdate();
        }
#endif

        private void Update()
        {
            CheckForChanges();
        }

        private void CheckForChanges()
        {
            bool hasChanged = false;

            // Sprite 변경 체크
            if (lastSprite != targetSprite)
            {
                lastSprite = targetSprite;
                hasChanged = true;
                UpdateSpriteMaterial();
            }

            // 색상 변경 체크
            if (spriteMaterial != null && spriteMaterial.color != spriteColor)
            {
                spriteMaterial.color = spriteColor;
            }

            if (!optimizePerformance || !Application.isPlaying)
            {
                hasChanged = true;
            }
            else if (updateEveryFrame)
            {
                // 성능 최적화: 앵커 위치가 변경되었을 때만 업데이트
                VertexAnchor[] anchors = { topLeft, topRight, bottomLeft, bottomRight };

                for (int i = 0; i < anchors.Length; i++)
                {
                    if (anchors[i].useAnchor && anchors[i].anchorTarget != null)
                    {
                        Vector3 currentPos = anchors[i].anchorTarget.position;
                        if (Vector3.Distance(currentPos, lastAnchorPositions[i]) > 0.001f)
                        {
                            lastAnchorPositions[i] = currentPos;
                            hasChanged = true;
                        }
                    }
                }
            }

            if (hasChanged)
            {
                needsUpdate = true;
                UpdateMesh();
            }
        }

        private void UpdateMesh()
        {
            if (!needsUpdate || customMesh == null || targetSprite == null) return;

            customMesh.Clear();

            Bounds spriteBounds = targetSprite.bounds;

            // Flip 적용
            if (flipX)
            {
                float temp = spriteBounds.min.x;
                spriteBounds.min = new Vector3(spriteBounds.max.x * -1, spriteBounds.min.y, spriteBounds.min.z);
                spriteBounds.max = new Vector3(temp * -1, spriteBounds.max.y, spriteBounds.max.z);
            }
            if (flipY)
            {
                float temp = spriteBounds.min.y;
                spriteBounds.min = new Vector3(spriteBounds.min.x, spriteBounds.max.y * -1, spriteBounds.min.z);
                spriteBounds.max = new Vector3(spriteBounds.max.x, temp * -1, spriteBounds.max.z);
            }

            if (useSubdivision)
            {
                CreateSubdividedMesh(spriteBounds);
            }
            else
            {
                CreateBasicMesh(spriteBounds);
            }

            // 색상 정보 추가
            Color32[] colors = new Color32[customMesh.vertexCount];
            Color32 color32 = spriteColor;
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = color32;
            }
            customMesh.colors32 = colors;

            customMesh.RecalculateBounds();
            
            // 메시가 업데이트되었음을 명시적으로 알림
            if (meshFilter != null)
            {
                meshFilter.mesh = null; // 강제 리프레시
                meshFilter.mesh = customMesh;
            }
            
            needsUpdate = false;

            // 성능 정보 업데이트
            if (showPerformanceInfo)
            {
                UpdatePerformanceInfo();
            }
        }

        private void CreateBasicMesh(Bounds bounds)
        {
            // 4개 모서리의 앵커 위치 가져오기
            Vector3 bottomLeftPos = GetAnchorPosition(bottomLeft, new Vector2(bounds.min.x, bounds.min.y));
            Vector3 topLeftPos = GetAnchorPosition(topLeft, new Vector2(bounds.min.x, bounds.max.y));
            Vector3 topRightPos = GetAnchorPosition(topRight, new Vector2(bounds.max.x, bounds.max.y));
            Vector3 bottomRightPos = GetAnchorPosition(bottomRight, new Vector2(bounds.max.x, bounds.min.y));

            // Sprite의 UV 좌표 가져오기
            Vector2[] spriteUVs = targetSprite.uv;
            
            // 기본 4개 vertex 설정
            Vector3[] vertices = new Vector3[]
            {
                bottomLeftPos,   // 0
                topLeftPos,      // 1
                topRightPos,     // 2
                bottomRightPos   // 3
            };

            // UV 매핑 (Sprite의 UV 사용)
            Vector2[] uvs = new Vector2[4];
            if (spriteUVs.Length >= 4)
            {
                // Flip 적용
                if (flipX && flipY)
                {
                    uvs[0] = spriteUVs[2]; // Top-right
                    uvs[1] = spriteUVs[3]; // Bottom-right
                    uvs[2] = spriteUVs[0]; // Bottom-left
                    uvs[3] = spriteUVs[1]; // Top-left
                }
                else if (flipX)
                {
                    uvs[0] = spriteUVs[3]; // Bottom-right
                    uvs[1] = spriteUVs[2]; // Top-right
                    uvs[2] = spriteUVs[1]; // Top-left
                    uvs[3] = spriteUVs[0]; // Bottom-left
                }
                else if (flipY)
                {
                    uvs[0] = spriteUVs[1]; // Top-left
                    uvs[1] = spriteUVs[0]; // Bottom-left
                    uvs[2] = spriteUVs[3]; // Bottom-right
                    uvs[3] = spriteUVs[2]; // Top-right
                }
                else
                {
                    uvs[0] = spriteUVs[0]; // Bottom-left
                    uvs[1] = spriteUVs[1]; // Top-left
                    uvs[2] = spriteUVs[2]; // Top-right
                    uvs[3] = spriteUVs[3]; // Bottom-right
                }
            }
            else
            {
                // 기본 UV (Flip 고려)
                uvs[0] = new Vector2(flipX ? 1 : 0, flipY ? 1 : 0);
                uvs[1] = new Vector2(flipX ? 1 : 0, flipY ? 0 : 1);
                uvs[2] = new Vector2(flipX ? 0 : 1, flipY ? 0 : 1);
                uvs[3] = new Vector2(flipX ? 0 : 1, flipY ? 1 : 0);
            }

            // 노말 설정 (모두 앞쪽을 향함)
            Vector3[] normals = new Vector3[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };

            // 삼각형 인덱스
            int[] triangles = new int[]
            {
                0, 1, 2,  // 첫 번째 삼각형
                0, 2, 3   // 두 번째 삼각형
            };

            // 메시에 적용
            customMesh.vertices = vertices;
            customMesh.uv = uvs;
            customMesh.normals = normals;
            customMesh.triangles = triangles;
        }

        private void CreateSubdividedMesh(Bounds bounds)
        {
            // 모바일 성능을 위한 subdivision 제한
            int safeSubdivisionX = Mathf.Clamp(subdivisionX, 2, 6);
            int safeSubdivisionY = Mathf.Clamp(subdivisionY, 2, 6);

            // 4개 모서리의 앵커 위치
            Vector3 bottomLeftPos = GetAnchorPosition(bottomLeft, new Vector2(bounds.min.x, bounds.min.y));
            Vector3 topLeftPos = GetAnchorPosition(topLeft, new Vector2(bounds.min.x, bounds.max.y));
            Vector3 topRightPos = GetAnchorPosition(topRight, new Vector2(bounds.max.x, bounds.max.y));
            Vector3 bottomRightPos = GetAnchorPosition(bottomRight, new Vector2(bounds.max.x, bounds.min.y));

            // Sprite의 UV 좌표 가져오기
            Vector2[] spriteUVs = targetSprite.uv;
            Vector2 uvMin, uvMax;
            
            if (spriteUVs.Length >= 4)
            {
                // Flip을 고려한 UV 범위 설정
                if (flipX && flipY)
                {
                    uvMin = spriteUVs[2];
                    uvMax = spriteUVs[0];
                }
                else if (flipX)
                {
                    uvMin = new Vector2(spriteUVs[2].x, spriteUVs[0].y);
                    uvMax = new Vector2(spriteUVs[0].x, spriteUVs[2].y);
                }
                else if (flipY)
                {
                    uvMin = new Vector2(spriteUVs[0].x, spriteUVs[2].y);
                    uvMax = new Vector2(spriteUVs[2].x, spriteUVs[0].y);
                }
                else
                {
                    uvMin = spriteUVs[0];
                    uvMax = spriteUVs[2];
                }
            }
            else
            {
                uvMin = new Vector2(flipX ? 1 : 0, flipY ? 1 : 0);
                uvMax = new Vector2(flipX ? 0 : 1, flipY ? 0 : 1);
            }

            int vertexCount = (safeSubdivisionX + 1) * (safeSubdivisionY + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];

            // Subdivision된 vertex들 생성
            int vertexIndex = 0;
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

                    vertices[vertexIndex] = finalPosition;
                    normals[vertexIndex] = Vector3.back;

                    // UV 좌표 (Sprite의 UV 범위 내에서 보간)
                    uvs[vertexIndex] = new Vector2(
                        Mathf.Lerp(uvMin.x, uvMax.x, normalizedX),
                        Mathf.Lerp(uvMin.y, uvMax.y, normalizedY)
                    );

                    vertexIndex++;
                }
            }

            // 삼각형 인덱스 생성
            List<int> triangles = new List<int>();
            for (int y = 0; y < safeSubdivisionY; y++)
            {
                for (int x = 0; x < safeSubdivisionX; x++)
                {
                    int bottomLeft = y * (safeSubdivisionX + 1) + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + (safeSubdivisionX + 1);
                    int topRight = topLeft + 1;

                    // 첫 번째 삼각형
                    triangles.Add(bottomLeft);
                    triangles.Add(topLeft);
                    triangles.Add(topRight);

                    // 두 번째 삼각형
                    triangles.Add(bottomLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomRight);
                }
            }

            // 메시에 적용
            customMesh.vertices = vertices;
            customMesh.uv = uvs;
            customMesh.normals = normals;
            customMesh.triangles = triangles.ToArray();
        }

        private void UpdatePerformanceInfo()
        {
            lastVertexCount = customMesh.vertexCount;
            lastTriangleCount = customMesh.triangles.Length / 3;

#if UNITY_EDITOR && !APPLICATION_IS_PLAYING
            // Editor에서만 성능 정보 로깅
            if (useSubdivision)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        Debug.Log($"[{gameObject.name}] 성능 정보 - Vertex: {lastVertexCount}, 삼각형: {lastTriangleCount} " +
                                 $"(Subdivision: {subdivisionX}x{subdivisionY})");
                    }
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
                return anchor.anchorTarget.localPosition + (Vector3)anchor.localOffset;
            }
            
            // 외부 앵커인 경우: 월드 좌표를 로컬 좌표로 변환
            Vector3 worldPosition = anchor.anchorTarget.position + (Vector3)anchor.localOffset;
            return transform.InverseTransformPoint(worldPosition);
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
            if (targetSprite == null) return;

            Bounds bounds = targetSprite.bounds;

            // 4개의 앵커 포인트 생성 및 위치 설정
            GameObject tlObject = CreateAnchorPoint("TL", new Vector2(bounds.min.x, bounds.max.y));
            GameObject trObject = CreateAnchorPoint("TR", new Vector2(bounds.max.x, bounds.max.y));
            GameObject blObject = CreateAnchorPoint("BL", new Vector2(bounds.min.x, bounds.min.y));
            GameObject brObject = CreateAnchorPoint("BR", new Vector2(bounds.max.x, bounds.min.y));

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

            // 로컬 위치 설정 (Sprite의 vertex 위치와 정확히 일치)
            anchorPoint.transform.localPosition = localPosition;
            
            // 스케일 초기화 (애니메이션 오류 방지)
            anchorPoint.transform.localScale = Vector3.one;
            anchorPoint.transform.localRotation = Quaternion.identity;

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
            SetupMeshComponents();
            UpdateMesh();
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
                    
                    // 라벨 표시
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(targetPos, names[i]);
                    #endif
                }
            }

            // 메시 와이어프레임 표시
            if (customMesh != null && customMesh.vertexCount > 0)
            {
                Gizmos.color = Color.cyan;
                Vector3[] vertices = customMesh.vertices;
                int[] triangles = customMesh.triangles;
                
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 v1 = transform.TransformPoint(vertices[triangles[i]]);
                    Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
                    Vector3 v3 = transform.TransformPoint(vertices[triangles[i + 2]]);
                    
                    Gizmos.DrawLine(v1, v2);
                    Gizmos.DrawLine(v2, v3);
                    Gizmos.DrawLine(v3, v1);
                }
            }

            // Sprite bounds 표시
            if (targetSprite != null)
            {
                Gizmos.color = Color.gray;
                Bounds bounds = targetSprite.bounds;
                Vector3[] corners = new Vector3[]
                {
                    transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, 0)),
                    transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, 0)),
                    transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, 0)),
                    transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, 0))
                };

                Gizmos.DrawLine(corners[0], corners[1]);
                Gizmos.DrawLine(corners[1], corners[2]);
                Gizmos.DrawLine(corners[2], corners[3]);
                Gizmos.DrawLine(corners[3], corners[0]);
            }
        }

        private void CleanupResources()
        {
            // 메시 정리
            if (customMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(customMesh);
                else
                    DestroyImmediate(customMesh);
                customMesh = null;
            }

            // 머티리얼 정리 (인스턴스인 경우만)
            if (spriteMaterial != null && spriteMaterial.name.Contains("Instance"))
            {
                if (Application.isPlaying)
                    Destroy(spriteMaterial);
                else
                    DestroyImmediate(spriteMaterial);
                spriteMaterial = null;
            }
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        // Editor 전용 메서드
        #if UNITY_EDITOR
        [ContextMenu("Reset to Original Sprite")]
        public void ResetToOriginalSprite()
        {
            // 앵커 초기화
            topLeft.useAnchor = false;
            topRight.useAnchor = false;
            bottomLeft.useAnchor = false;
            bottomRight.useAnchor = false;

            // 메시 재생성
            ForceUpdate();
            
            Debug.Log($"[{gameObject.name}] 원본 Sprite 형태로 리셋되었습니다.");
        }
        
        [ContextMenu("Debug Mesh Info")]
        public void DebugMeshInfo()
        {
            if (customMesh != null)
            {
                Debug.Log($"[{gameObject.name}] Mesh Debug Info:\n" +
                         $"- Vertex Count: {customMesh.vertexCount}\n" +
                         $"- Triangle Count: {customMesh.triangles.Length / 3}\n" +
                         $"- Has UVs: {customMesh.uv.Length > 0}\n" +
                         $"- Has Colors: {customMesh.colors.Length > 0}\n" +
                         $"- Has Normals: {customMesh.normals.Length > 0}\n" +
                         $"- Bounds: {customMesh.bounds}");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] No mesh created yet!");
            }
            
            if (meshRenderer != null && spriteMaterial != null)
            {
                Debug.Log($"[{gameObject.name}] Renderer Debug Info:\n" +
                         $"- Enabled: {meshRenderer.enabled}\n" +
                         $"- Material: {spriteMaterial.name}\n" +
                         $"- Shader: {spriteMaterial.shader.name}\n" +
                         $"- Texture: {spriteMaterial.mainTexture?.name ?? "None"}\n" +
                         $"- Color: {spriteMaterial.color}\n" +
                         $"- Sorting Layer: {SortingLayer.IDToName(meshRenderer.sortingLayerID)}\n" +
                         $"- Sorting Order: {meshRenderer.sortingOrder}");
            }
        }
        
        [ContextMenu("Force Refresh All")]
        public void ForceRefreshAll()
        {
            // 컴포넌트 재초기화
            InitializeComponents();
            SetupMeshComponents();
            ForceUpdate();
            
            Debug.Log($"[{gameObject.name}] 모든 컴포넌트가 강제로 새로고침되었습니다.");
        }

        [ContextMenu("Auto Setup from SpriteRenderer")]
        public void AutoSetupFromSpriteRenderer()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                targetSprite = sr.sprite;
                spriteColor = sr.color;
                sortingLayerName = SortingLayer.IDToName(sr.sortingLayerID);
                sortingOrder = sr.sortingOrder;
                flipX = sr.flipX;
                flipY = sr.flipY;
                
                if (EditorUtility.DisplayDialog("SpriteMultiAnchor", 
                    "SpriteRenderer의 설정을 복사했습니다.\nSpriteRenderer를 제거하시겠습니까?", 
                    "제거", "유지"))
                {
                    DestroyImmediate(sr);
                }
                
                ForceUpdate();
                Debug.Log($"[{gameObject.name}] SpriteRenderer 설정이 복사되었습니다.");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] SpriteRenderer가 없거나 Sprite가 설정되지 않았습니다.");
            }
        }
        #endif

        // Sprite 설정 Helper 메서드
        public void SetSprite(Sprite newSprite)
        {
            targetSprite = newSprite;
            ForceUpdate();
        }

        public void SetColor(Color newColor)
        {
            spriteColor = newColor;
            if (spriteMaterial != null)
            {
                spriteMaterial.color = newColor;
            }
        }

        public void SetSortingLayer(string layerName)
        {
            sortingLayerName = layerName;
            UpdateSortingSettings();
        }

        public void SetSortingOrder(int order)
        {
            sortingOrder = order;
            UpdateSortingSettings();
        }

        public void SetFlip(bool x, bool y)
        {
            flipX = x;
            flipY = y;
            ForceUpdate();
        }
    }
}