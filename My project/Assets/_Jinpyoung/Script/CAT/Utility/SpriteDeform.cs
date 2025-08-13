using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D; // SpriteAtlas 사용을 위해 추가
using System.Collections.Generic;

#if UNITY_EDITOR
// 에디터 관련 클래스를 사용하기 위해 네임스페이스 추가
using UnityEditor;
#endif

namespace CAT.Utility
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public class SpriteDeform : MonoBehaviour
    {
        [System.Serializable]
        public enum SubdivisionLevel
        {
            None = 1,
            Level2x2 = 2,
            Level3x3 = 3,
            Level4x4 = 4,
            Level5x5 = 5,
            Level6x6 = 6
        }

        [Header("Sprite Settings")]
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = Color.white;
        
        // --- 아틀라스 지원을 위한 필드 (런타임용, 인스펙터에서 숨김) ---
        [HideInInspector][SerializeField] private SpriteAtlas spriteAtlas;
        [HideInInspector][SerializeField] private string spriteName; // 에디터에서 선택된 스프라이트 이름을 저장

        [Header("Subdivision")]
        [SerializeField] private SubdivisionLevel subdivisionLevel = SubdivisionLevel.None;

        [Header("Sorting")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 0;

        [Header("Corner Handles")]
        [SerializeField] private Transform topLeftHandle;
        [SerializeField] private Transform topRightHandle;
        [SerializeField] private Transform bottomLeftHandle;
        [SerializeField] private Transform bottomRightHandle;

        [Header("Performance")]
        [SerializeField] private bool useLOD = false;
        [SerializeField] private float lodDistance = 20f;
        [SerializeField] private SubdivisionLevel lodLevel = SubdivisionLevel.None;
        [SerializeField] private bool useSharedMaterial = true;

        [Header("Debug")]
        [SerializeField] private bool showVertices = true;
        [SerializeField] private float gizmoSize = 0.05f;

        // 에디터에서 접근할 수 있는 프로퍼티들
        public Sprite Sprite => sprite;
        public SpriteAtlas SpriteAtlas => spriteAtlas; // 아틀라스 프로퍼티 추가
        public SubdivisionLevel CurrentSubdivisionLevel => subdivisionLevel;
        public bool UseSharedMaterial => useSharedMaterial;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        
        // 재사용 가능한 배열들
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Color[] colors;
        private int[] triangles;

        // Material 공유를 위한 static 딕셔너리
        private static Dictionary<int, Material> sharedMaterials = new Dictionary<int, Material>();
        private Material instanceMaterial;

        // 성능 최적화를 위한 플래그
        private bool isDirty = false;
        private bool isInitialized = false;
        private bool needsFlipApplication = false;
        private bool pendingFlipX = false;
        private bool pendingFlipY = false;

        #if UNITY_EDITOR
        // OnValidate에서 스프라이트 변경을 감지하기 위한 변수
        private Sprite lastSprite;
        #endif

        // 마지막 핸들 위치를 저장하기 위한 변수들 (런타임/에디터 공용)
        private Vector3 lastTopLeftPos;
        private Vector3 lastTopRightPos;
        private Vector3 lastBottomLeftPos;
        private Vector3 lastBottomRightPos;

        private void Awake()
        {
            // Awake에서는 자동 변환하지 않음 (컴포넌트 충돌 방지)
            Initialize();
        }
        
        /// <summary>
        /// Flip 정보를 적용 대기열에 추가합니다. 핸들이 생성된 후 적용됩니다.
        /// </summary>
        /// <param name="flipX">X축 반전 여부</param>
        /// <param name="flipY">Y축 반전 여부</param>
        public void ApplyFlip(bool flipX, bool flipY)
        {
            // 플립 정보를 저장해두고 핸들 생성 후에 적용
            needsFlipApplication = true;
            pendingFlipX = flipX;
            pendingFlipY = flipY;
        }

        private void Initialize()
        {
            if (isInitialized) return;

            // SpriteRenderer가 있으면 경고만 하고 초기화를 중단하지 않습니다.
            if (GetComponent<SpriteRenderer>() != null)
            {
                Debug.LogWarning($"[SpriteDeform] {gameObject.name}에 SpriteRenderer가 있습니다. " +
                                 "컨텍스트 메뉴를 사용하여 복사본을 생성하세요.");
            }

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Deformed Sprite Mesh";
                mesh.MarkDynamic(); // 동적 메쉬 최적화
            }

            // 초기화 시에는 핸들 위치를 강제로 리셋하지 않습니다.
            CreateOrUpdateHandles(false);
            SetupMaterial();
            UpdateSortingSettings();
            
            isInitialized = true;
            isDirty = true;
        }

        private void OnEnable()
        {
            #if UNITY_EDITOR
            // 에디터에서 플레이 모드로 진입하거나 스크립트가 리로드될 때,
            // OnValidate가 호출되기 전에 lastSprite를 현재 스프라이트로 초기화하여
            // 불필요한 리셋을 방지합니다.
            lastSprite = sprite;
            #endif

            if (!isInitialized)
            {
                Initialize();
            }
            // OnEnable에서 핸들 위치를 초기화하여 런타임 시작 시 위치를 동기화합니다.
            UpdateLastHandlePositions();
            isDirty = true;
        }

        private void UpdateLastHandlePositions()
        {
            if (HasValidHandles())
            {
                lastTopLeftPos = topLeftHandle.localPosition;
                lastTopRightPos = topRightHandle.localPosition;
                lastBottomLeftPos = bottomLeftHandle.localPosition;
                lastBottomRightPos = bottomRightHandle.localPosition;
            }
        }
        
        private void LateUpdate()
        {
            if (sprite == null || !HasValidHandles()) return;

            bool handlesMoved = false;
            // 런타임과 에디터 모두에서 핸들의 움직임을 감지합니다.
            if (topLeftHandle.localPosition != lastTopLeftPos ||
                topRightHandle.localPosition != lastTopRightPos ||
                bottomLeftHandle.localPosition != lastBottomLeftPos ||
                bottomRightHandle.localPosition != lastBottomRightPos)
            {
                handlesMoved = true;
            }

            // isDirty 플래그가 설정되었거나 핸들이 움직였을 경우 메쉬를 업데이트합니다.
            if (isDirty || handlesMoved)
            {
                UpdateMesh();
                
                // 핸들이 움직여서 업데이트된 경우, 마지막 위치를 갱신합니다.
                if (handlesMoved)
                {
                    UpdateLastHandlePositions();
                }
                
                isDirty = false;
            }
        }

        private void OnValidate()
        {
            // OnValidate는 에디터에서만 호출됩니다.
            if (Application.isPlaying) return;

            bool spriteChanged = false;
            #if UNITY_EDITOR
            if (sprite != lastSprite)
            {
                spriteChanged = true;
                lastSprite = sprite;
            }
            #endif

            // 스프라이트 이름 동기화
            if (sprite != null)
            {
                string currentSpriteName = sprite.name.Replace("(Clone)", "");
                if (spriteName != currentSpriteName)
                {
                    spriteName = currentSpriteName;
                }
            }
            else
            {
                spriteName = null;
            }
            
            // isInitialized는 컴포넌트가 처음 추가되었을 때를 구분하기 위해 필요합니다.
            if (isInitialized)
            {
                // 스프라이트가 실제로 변경되었을 때만 핸들 위치를 리셋합니다.
                if (spriteChanged && sprite != null)
                {
                    CreateOrUpdateHandles(true); // 위치 강제 리셋
                }
                
                // 머티리얼, 정렬 순서 등 다른 속성 변경은 항상 반영합니다.
                SetupMaterial();
                UpdateSortingSettings();
                isDirty = true;
            }
        }

        private void SetupMaterial()
        {
            if (sprite == null) return;

            if (useSharedMaterial)
            {
                int textureID = sprite.texture.GetInstanceID();
                if (!sharedMaterials.ContainsKey(textureID))
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("Unlit/Transparent");
                    
                    Material mat = new Material(shader);
                    mat.mainTexture = sprite.texture;

                    #if UNITY_EDITOR
                    // 이 플래그는 머티리얼이 씬에 저장되거나 언로드되지 않도록 하여
                    // 플레이 모드 종료 시 파괴되는 것을 방지합니다.
                    mat.hideFlags = HideFlags.HideAndDontSave;
                    #endif

                    sharedMaterials[textureID] = mat;
                }
                
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial = sharedMaterials[textureID];
                }
            }
            else
            {
                if (instanceMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("Unlit/Transparent");
                    instanceMaterial = new Material(shader);
                }
                
                instanceMaterial.mainTexture = sprite.texture;
                if (meshRenderer != null)
                {
                    meshRenderer.material = instanceMaterial;
                }
            }
        }

        private bool HasValidHandles()
        {
            return topLeftHandle != null && topRightHandle != null && 
                   bottomLeftHandle != null && bottomRightHandle != null;
        }

        private void CreateOrUpdateHandles(bool forceResetPosition)
        {
            #if UNITY_EDITOR
            if (sprite == null) return;

            bool handlesWereCreated = false;
            // 핸들이 없으면 생성합니다.
            if (topLeftHandle == null)
            {
                topLeftHandle = CreateHandle("TL");
                handlesWereCreated = true;
            }
            if (topRightHandle == null)
            {
                topRightHandle = CreateHandle("TR");
                handlesWereCreated = true;
            }
            if (bottomLeftHandle == null)
            {
                bottomLeftHandle = CreateHandle("BL");
                handlesWereCreated = true;
            }
            if (bottomRightHandle == null)
            {
                bottomRightHandle = CreateHandle("BR");
                handlesWereCreated = true;
            }

            // 핸들을 처음 생성했거나, 명시적으로 위치 리셋을 요청했을 때만 위치를 설정합니다.
            if (handlesWereCreated || forceResetPosition)
            {
                ApplyOriginSize();
            }
            #endif
        }

        #if UNITY_EDITOR
        private Transform CreateHandle(string name)
        {
            GameObject handle = new GameObject(name);
            handle.transform.SetParent(transform);
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = Vector3.one;

            var iconContent = EditorGUIUtility.IconContent("sv_icon_dot0_pix16_gizmo");
            if (iconContent != null && iconContent.image != null)
            {
                EditorGUIUtility.SetIconForObject(handle, (Texture2D)iconContent.image);
            }

            return handle.transform;
        }
        #endif

        public void ApplyOriginSize()
        {
            if (sprite == null) return;

            float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;

            float halfWidth = spriteWidth * 0.5f;
            float halfHeight = spriteHeight * 0.5f;

            Vector2 pivotOffset = new Vector2(
                (sprite.pivot.x / sprite.rect.width - 0.5f) * spriteWidth,
                (sprite.pivot.y / sprite.rect.height - 0.5f) * spriteHeight
            );

            if (topLeftHandle != null)
                topLeftHandle.localPosition = new Vector3(-halfWidth - pivotOffset.x, halfHeight - pivotOffset.y, 0);
            if (topRightHandle != null)
                topRightHandle.localPosition = new Vector3(halfWidth - pivotOffset.x, halfHeight - pivotOffset.y, 0);
            if (bottomLeftHandle != null)
                bottomLeftHandle.localPosition = new Vector3(-halfWidth - pivotOffset.x, -halfHeight - pivotOffset.y, 0);
            if (bottomRightHandle != null)
                bottomRightHandle.localPosition = new Vector3(halfWidth - pivotOffset.x, -halfHeight - pivotOffset.y, 0);

            // 플립 적용이 필요한 경우
            if (needsFlipApplication && HasValidHandles())
            {
                if (pendingFlipX)
                {
                    Vector3 tlPos = topLeftHandle.localPosition;
                    Vector3 trPos = topRightHandle.localPosition;
                    Vector3 blPos = bottomLeftHandle.localPosition;
                    Vector3 brPos = bottomRightHandle.localPosition;
                    
                    topLeftHandle.localPosition = new Vector3(trPos.x, tlPos.y, tlPos.z);
                    topRightHandle.localPosition = new Vector3(tlPos.x, trPos.y, trPos.z);
                    bottomLeftHandle.localPosition = new Vector3(brPos.x, blPos.y, blPos.z);
                    bottomRightHandle.localPosition = new Vector3(blPos.x, brPos.y, brPos.z);
                }
                
                if (pendingFlipY)
                {
                    Vector3 tlPos = topLeftHandle.localPosition;
                    Vector3 trPos = topRightHandle.localPosition;
                    Vector3 blPos = bottomLeftHandle.localPosition;
                    Vector3 brPos = bottomRightHandle.localPosition;
                    
                    topLeftHandle.localPosition = new Vector3(tlPos.x, blPos.y, tlPos.z);
                    topRightHandle.localPosition = new Vector3(trPos.x, brPos.y, trPos.z);
                    bottomLeftHandle.localPosition = new Vector3(blPos.x, tlPos.y, blPos.z);
                    bottomRightHandle.localPosition = new Vector3(brPos.x, trPos.y, brPos.z);
                }
                
                needsFlipApplication = false;
            }

            // 핸들 위치를 갱신했으므로, 마지막 위치 캐시도 업데이트합니다.
            UpdateLastHandlePositions();
            
            isDirty = true;
        }

        public void UpdateMesh()
        {
            if (sprite == null || !HasValidHandles() || mesh == null) return;

            // LOD 시스템
            SubdivisionLevel effectiveLevel = subdivisionLevel;
            if (useLOD && Camera.main != null)
            {
                float distance = Vector3.Distance(transform.position, Camera.main.transform.position);
                if (distance > lodDistance)
                {
                    effectiveLevel = lodLevel;
                }
            }

            // 핸들 위치에서 코너 좌표 가져오기
            Vector2 tl = topLeftHandle.localPosition;
            Vector2 tr = topRightHandle.localPosition;
            Vector2 bl = bottomLeftHandle.localPosition;
            Vector2 br = bottomRightHandle.localPosition;

            // Tight 메쉬인지 확인 (Unity는 Tight 메쉬에 대해 vertices와 triangles를 제공)
            if (sprite.triangles.Length > 0 && sprite.vertices.Length > 4)
            {
                // --- Tight Mesh 로직 ---
                Vector2[] spriteVertices = sprite.vertices;
                Vector2[] spriteUVs = sprite.uv;
                ushort[] spriteTriangles = sprite.triangles;

                int vertexCount = spriteVertices.Length;
                InitializeArrays(vertexCount);

                Bounds spriteBounds = sprite.bounds;

                for (int i = 0; i < vertexCount; i++)
                {
                    // 원본 정점의 위치를 바운드 내에서 정규화 (0~1)
                    float u = (spriteVertices[i].x - spriteBounds.min.x) / spriteBounds.size.x;
                    float v = (spriteVertices[i].y - spriteBounds.min.y) / spriteBounds.size.y;

                    // 정규화된 위치를 사용하여 핸들로 정의된 사각형 내에서 보간된 위치를 계산
                    Vector2 deformedPosition = BilinearInterpolate(bl, br, tl, tr, u, v);
                    vertices[i] = new Vector3(deformedPosition.x, deformedPosition.y, 0);
                    uvs[i] = spriteUVs[i];
                    colors[i] = color;
                }

                // 삼각형 인덱스 변환 (ushort[] -> int[])
                if (triangles == null || triangles.Length != spriteTriangles.Length)
                {
                    triangles = new int[spriteTriangles.Length];
                }
                for (int i = 0; i < spriteTriangles.Length; i++)
                {
                    triangles[i] = spriteTriangles[i];
                }
            }
            else
            {
                // --- Full Rect 로직 (기존 코드) ---
                int subdivisions = (int)effectiveLevel;
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
                        
                        colors[vertexIndex] = color;
                        
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
            
            // 메쉬 업데이트 (공통)
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            if (meshFilter != null)
            {
                meshFilter.mesh = mesh;
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

        private Vector2 BilinearInterpolate(Vector2 p00, Vector2 p10, Vector2 p01, Vector2 p11, float u, float v)
        {
            float u1 = 1f - u;
            float v1 = 1f - v;
            
            return new Vector2(
                p00.x * u1 * v1 + p10.x * u * v1 + p01.x * u1 * v + p11.x * u * v,
                p00.y * u1 * v1 + p10.y * u * v1 + p01.y * u1 * v + p11.y * u * v
            );
        }

        /// <summary>
        /// 스프라이트를 교체합니다.
        /// </summary>
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
            
            SetupMaterial();
            
            #if UNITY_EDITOR
            // 스프라이트가 변경되었으므로 핸들을 업데이트합니다.
            if (sprite != null)
            {
                CreateOrUpdateHandles(true); // 위치 강제 리셋
            }
            #endif
            
            isDirty = true;
        }

        /// <summary>
        /// 할당된 아틀라스에서 이름으로 스프라이트를 찾아 교체합니다. (런타임용)
        /// 이 메서드를 사용하려면 먼저 코드로 `spriteAtlas`를 할당해야 합니다.
        /// </summary>
        /// <returns>성공 여부</returns>
        public bool SetSprite(string newSpriteName)
        {
            if (spriteAtlas == null)
            {
                Debug.LogWarning("SpriteAtlas가 할당되지 않아 이름으로 스프라이트를 교체할 수 없습니다.", this);
                return false;
            }

            Sprite newSprite = spriteAtlas.GetSprite(newSpriteName);
            if (newSprite == null)
            {
                Debug.LogWarning($"아틀라스 '{spriteAtlas.name}'에서 '{newSpriteName}' 스프라이트를 찾을 수 없습니다.", this);
                return false;
            }

            SetSprite(newSprite);
            return true;
        }
        
        public void SetSubdivisionLevel(SubdivisionLevel level)
        {
            subdivisionLevel = level;
            isDirty = true;
        }

        public void SetColor(Color newColor)
        {
            color = newColor;
            isDirty = true;
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

        private void UpdateSortingSettings()
        {
            if (meshRenderer == null) return;

            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }

        public void MarkDirty()
        {
            isDirty = true;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showVertices || mesh == null) return;

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // 스프라이트 바운드 표시
            if (sprite != null)
            {
                Gizmos.color = Color.green;
                float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
                float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;
                
                Vector2 pivotOffset = new Vector2(
                    (sprite.pivot.x / sprite.rect.width - 0.5f) * spriteWidth,
                    (sprite.pivot.y / sprite.rect.height - 0.5f) * spriteHeight
                );
                
                Vector3 center = new Vector3(-pivotOffset.x, -pivotOffset.y, 0);
                Vector3 size = new Vector3(spriteWidth, spriteHeight, 0);
                Gizmos.DrawWireCube(center, size);
            }

            // 메쉬 버텍스 표시
            if (vertices != null && vertices.Length > 0)
            {
                Gizmos.color = Color.yellow;
                
                foreach (Vector3 vertex in vertices)
                {
                    Gizmos.DrawWireSphere(vertex, gizmoSize * 0.5f);
                }
            }

            // 핸들 간 연결선 표시
            if (HasValidHandles())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(topLeftHandle.localPosition, topRightHandle.localPosition);
                Gizmos.DrawLine(topRightHandle.localPosition, bottomRightHandle.localPosition);
                Gizmos.DrawLine(bottomRightHandle.localPosition, bottomLeftHandle.localPosition);
                Gizmos.DrawLine(bottomLeftHandle.localPosition, topLeftHandle.localPosition);
            }
            
            Gizmos.matrix = originalMatrix;
        }
        #endif

        private void OnDestroy()
        {
            #if UNITY_EDITOR
            // 핸들 오브젝트 제거
            if (topLeftHandle != null) DestroyImmediate(topLeftHandle.gameObject);
            if (topRightHandle != null) DestroyImmediate(topRightHandle.gameObject);
            if (bottomLeftHandle != null) DestroyImmediate(bottomLeftHandle.gameObject);
            if (bottomRightHandle != null) DestroyImmediate(bottomRightHandle.gameObject);
            #endif

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

        // 정적 리소스 정리
        private void OnApplicationQuit()
        {
            sharedMaterials.Clear();
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(SpriteDeform))]
    public class SpriteDeformEditor : Editor
    {
        private SerializedProperty sortingLayerProp;
        private SerializedProperty sortingOrderProp;
        private SerializedProperty subdivisionLevelProp;

        private void OnEnable()
        {
            sortingLayerProp = serializedObject.FindProperty("sortingLayerName");
            sortingOrderProp = serializedObject.FindProperty("sortingOrder");
            subdivisionLevelProp = serializedObject.FindProperty("subdivisionLevel");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // 기본 인스펙터 그리기 (커스텀 UI로 그릴 프로퍼티 제외)
            DrawPropertiesExcluding(serializedObject, "m_Script", "sortingLayerName", "sortingOrder", "subdivisionLevel");
            
            // Subdivision Level은 Tight Mesh가 아닐 때만 표시합니다.
            SpriteDeform spriteDeform = (SpriteDeform)target;
            bool isTightMesh = spriteDeform.Sprite != null && spriteDeform.Sprite.triangles.Length > 0 && spriteDeform.Sprite.vertices.Length > 4;
            if (!isTightMesh)
            {
                EditorGUILayout.PropertyField(subdivisionLevelProp);
            }

            // Sorting Layer 커스텀 UI
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sorting", EditorStyles.boldLabel);

            string[] sortingLayerNames = GetSortingLayerNames();
            int currentLayerIndex = System.Array.IndexOf(sortingLayerNames, sortingLayerProp.stringValue);
            if (currentLayerIndex == -1) currentLayerIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newLayerIndex = EditorGUILayout.Popup("Sorting Layer", currentLayerIndex, sortingLayerNames);
            if (EditorGUI.EndChangeCheck())
            {
                sortingLayerProp.stringValue = sortingLayerNames[newLayerIndex];
            }

            EditorGUILayout.PropertyField(sortingOrderProp, new GUIContent("Order in Layer"));

            serializedObject.ApplyModifiedProperties();

            // Apply Origin Size 버튼
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Apply Origin Size", GUILayout.Height(30)))
            {
                spriteDeform.ApplyOriginSize();
                EditorUtility.SetDirty(target);
            }

            // 성능 정보 표시
            if (Application.isPlaying && spriteDeform.Sprite != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Performance Info", EditorStyles.boldLabel);
                
                int vertexCount = ((int)spriteDeform.CurrentSubdivisionLevel + 1) * ((int)spriteDeform.CurrentSubdivisionLevel + 1);
                int triangleCount = (int)spriteDeform.CurrentSubdivisionLevel * (int)spriteDeform.CurrentSubdivisionLevel * 2;
                
                EditorGUILayout.LabelField($"Vertices: {vertexCount}");
                EditorGUILayout.LabelField($"Triangles: {triangleCount}");
                EditorGUILayout.LabelField($"Material Sharing: {(spriteDeform.UseSharedMaterial ? "Enabled" : "Disabled")}");
            }
        }

        private string[] GetSortingLayerNames()
        {
            // 안정적인 Public API를 사용하여 Sorting Layer 이름 목록을 가져옵니다.
            var layers = SortingLayer.layers;
            var layerNames = new string[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                layerNames[i] = layers[i].name;
            }
            return layerNames;
        }

        // 컨텍스트 메뉴 - SpriteRenderer를 기반으로 Deformable Sprite 생성
        [MenuItem("CONTEXT/SpriteRenderer/Create Sprite Deform Copy")]
        static void CreateSpriteDeformCopy(MenuCommand command)
        {
            SpriteRenderer sourceRenderer = (SpriteRenderer)command.context;
            GameObject sourceGo = sourceRenderer.gameObject;
            
            // 새로운 게임 오브젝트 생성
            GameObject newGo = new GameObject(sourceGo.name + " (Sprite Deform)");
            Undo.RegisterCreatedObjectUndo(newGo, "Create Sprite Deform Copy");
            
            // Transform 정보 복사
            newGo.transform.SetParent(sourceGo.transform.parent, true);
            newGo.transform.position = sourceGo.transform.position;
            newGo.transform.rotation = sourceGo.transform.rotation;
            newGo.transform.localScale = sourceGo.transform.localScale;
            
            // SpriteDeform 컴포넌트 추가
            SpriteDeform spriteDeform = newGo.AddComponent<SpriteDeform>();
            
            // SpriteRenderer 정보 복사
            spriteDeform.SetSprite(sourceRenderer.sprite);
            spriteDeform.SetColor(sourceRenderer.color);
            spriteDeform.SetSortingLayer(sourceRenderer.sortingLayerName);
            spriteDeform.SetSortingOrder(sourceRenderer.sortingOrder);
            
            // 플립 적용
            if (sourceRenderer.flipX || sourceRenderer.flipY)
            {
                spriteDeform.ApplyFlip(sourceRenderer.flipX, sourceRenderer.flipY);
            }
            
            // 새로 생성된 오브젝트 선택
            Selection.activeGameObject = newGo;
            
            EditorUtility.SetDirty(newGo);
            
            Debug.Log($"'{sourceGo.name}'을 기반으로 Sprite Deform 오브젝트를 생성했습니다: {newGo.name}");
        }

        // GameObject 메뉴에 추가
        [MenuItem("GameObject/2D Object/Sprite Deform", false, 10)]
        static void CreateSpriteDeform(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Sprite Deform");
            go.AddComponent<SpriteDeform>();
            
            // 선택된 오브젝트의 자식으로 생성
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            
            // Undo 지원
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            
            // 선택
            Selection.activeObject = go;
        }
    }
    #endif
}
