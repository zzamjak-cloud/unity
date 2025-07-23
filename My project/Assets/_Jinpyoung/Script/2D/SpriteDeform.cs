using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

        #if UNITY_EDITOR
        // 에디터 전용 변수들
        private Vector3 lastTopLeftPos;
        private Vector3 lastTopRightPos;
        private Vector3 lastBottomLeftPos;
        private Vector3 lastBottomRightPos;
        #endif

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Deformed Sprite Mesh";
                mesh.MarkDynamic(); // 동적 메쉬 최적화
            }

            CreateOrUpdateHandles();
            SetupMaterial();
            UpdateSortingSettings();
            
            isInitialized = true;
            isDirty = true;
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                Initialize();
            }
            isDirty = true;
        }

        #if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying)
            {
                CheckHandleMovement();
            }
        }

        private void CheckHandleMovement()
        {
            if (!HasValidHandles()) return;

            if (topLeftHandle.localPosition != lastTopLeftPos ||
                topRightHandle.localPosition != lastTopRightPos ||
                bottomLeftHandle.localPosition != lastBottomLeftPos ||
                bottomRightHandle.localPosition != lastBottomRightPos)
            {
                isDirty = true;
                UpdateLastHandlePositions();
            }
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
        #endif

        private void LateUpdate()
        {
            if (isDirty && sprite != null && HasValidHandles())
            {
                UpdateMesh();
                isDirty = false;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying || isInitialized)
            {
                if (sprite != null && !HasValidHandles())
                {
                    CreateOrUpdateHandles();
                }
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

        private void CreateOrUpdateHandles()
        {
            #if UNITY_EDITOR
            if (sprite == null) return;

            if (topLeftHandle == null)
                topLeftHandle = CreateHandle("TL");
            if (topRightHandle == null)
                topRightHandle = CreateHandle("TR");
            if (bottomLeftHandle == null)
                bottomLeftHandle = CreateHandle("BL");
            if (bottomRightHandle == null)
                bottomRightHandle = CreateHandle("BR");

            ApplyOriginSize();
            #endif
        }

        #if UNITY_EDITOR
        private Transform CreateHandle(string name)
        {
            GameObject handle = new GameObject($"Handle_{name}");
            handle.transform.SetParent(transform);
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = Vector3.one;

            var iconContent = UnityEditor.EditorGUIUtility.IconContent("sv_icon_dot0_pix16_gizmo");
            if (iconContent != null && iconContent.image != null)
            {
                UnityEditor.EditorGUIUtility.SetIconForObject(handle, (Texture2D)iconContent.image);
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

            #if UNITY_EDITOR
            UpdateLastHandlePositions();
            #endif
            
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

            int subdivisions = (int)effectiveLevel;
            int vertexCount = (subdivisions + 1) * (subdivisions + 1);
            
            // 배열 초기화 (필요한 경우에만)
            InitializeArrays(vertexCount, subdivisions);
            
            // 버텍스와 UV 생성
            int vertexIndex = 0;
            for (int y = 0; y <= subdivisions; y++)
            {
                float v = y / (float)subdivisions;
                
                for (int x = 0; x <= subdivisions; x++)
                {
                    float u = x / (float)subdivisions;
                    
                    // Bilinear interpolation으로 위치 계산
                    Vector2 position = BilinearInterpolate(bl, br, tl, tr, u, v);
                    vertices[vertexIndex] = new Vector3(position.x, position.y, 0);
                    
                    // UV 좌표 설정
                    Rect spriteRect = sprite.rect;
                    Texture2D texture = sprite.texture;
                    
                    float uvX = Mathf.Lerp(spriteRect.x / texture.width, 
                                          (spriteRect.x + spriteRect.width) / texture.width, u);
                    float uvY = Mathf.Lerp(spriteRect.y / texture.height, 
                                          (spriteRect.y + spriteRect.height) / texture.height, v);
                    
                    uvs[vertexIndex] = new Vector2(uvX, uvY);
                    colors[vertexIndex] = color;
                    
                    vertexIndex++;
                }
            }
            
            // 삼각형 인덱스 생성 (변경되지 않은 경우 스킵)
            if (triangles == null || triangles.Length != subdivisions * subdivisions * 6)
            {
                triangles = new int[subdivisions * subdivisions * 6];
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
            
            // 메쉬 업데이트
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

        private void InitializeArrays(int vertexCount, int subdivisions)
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

        public void SetSprite(Sprite newSprite)
        {
            sprite = newSprite;
            SetupMaterial();
            #if UNITY_EDITOR
            if (sprite != null && !HasValidHandles())
            {
                CreateOrUpdateHandles();
            }
            #endif
            isDirty = true;
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
    [UnityEditor.CustomEditor(typeof(SpriteDeform))]
    public class SpriteDeformEditor : UnityEditor.Editor
    {
        private UnityEditor.SerializedProperty sortingLayerProp;
        private UnityEditor.SerializedProperty sortingOrderProp;

        private void OnEnable()
        {
            sortingLayerProp = serializedObject.FindProperty("sortingLayerName");
            sortingOrderProp = serializedObject.FindProperty("sortingOrder");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 기본 인스펙터 그리기 (Sorting 제외)
            DrawPropertiesExcluding(serializedObject, "sortingLayerName", "sortingOrder");

            // Sorting Layer 커스텀 UI
            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Sorting", UnityEditor.EditorStyles.boldLabel);

            // Sorting Layer 드롭다운
            string[] sortingLayerNames = GetSortingLayerNames();
            int currentIndex = System.Array.IndexOf(sortingLayerNames, sortingLayerProp.stringValue);
            if (currentIndex == -1) currentIndex = 0;

            UnityEditor.EditorGUI.BeginChangeCheck();
            int newIndex = UnityEditor.EditorGUILayout.Popup("Sorting Layer", currentIndex, sortingLayerNames);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                sortingLayerProp.stringValue = sortingLayerNames[newIndex];
            }

            // Order in Layer
            UnityEditor.EditorGUILayout.PropertyField(sortingOrderProp, new GUIContent("Order in Layer"));

            serializedObject.ApplyModifiedProperties();

            // Apply Origin Size 버튼
            SpriteDeform spriteDeform = (SpriteDeform)target;
            
            UnityEditor.EditorGUILayout.Space();
            
            if (GUILayout.Button("Apply Origin Size", GUILayout.Height(30)))
            {
                spriteDeform.ApplyOriginSize();
                UnityEditor.EditorUtility.SetDirty(target);
            }

            // 성능 정보 표시
            if (Application.isPlaying && spriteDeform.Sprite != null)
            {
                UnityEditor.EditorGUILayout.Space();
                UnityEditor.EditorGUILayout.LabelField("Performance Info", UnityEditor.EditorStyles.boldLabel);
                
                int vertexCount = ((int)spriteDeform.CurrentSubdivisionLevel + 1) * ((int)spriteDeform.CurrentSubdivisionLevel + 1);
                int triangleCount = (int)spriteDeform.CurrentSubdivisionLevel * (int)spriteDeform.CurrentSubdivisionLevel * 2;
                
                UnityEditor.EditorGUILayout.LabelField($"Vertices: {vertexCount}");
                UnityEditor.EditorGUILayout.LabelField($"Triangles: {triangleCount}");
                UnityEditor.EditorGUILayout.LabelField($"Material Sharing: {(spriteDeform.UseSharedMaterial ? "Enabled" : "Disabled")}");
            }
        }

        private string[] GetSortingLayerNames()
        {
            System.Type internalEditorUtilityType = typeof(UnityEditor.EditorGUIUtility).Assembly.GetType("UnityEditorInternal.InternalEditorUtility");
            if (internalEditorUtilityType != null)
            {
                var sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (sortingLayersProperty != null)
                {
                    return (string[])sortingLayersProperty.GetValue(null, new object[0]);
                }
            }
            return new string[] { "Default" };
        }
    }
    #endif
}