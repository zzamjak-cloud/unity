using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그룹의 하위 자식 스프라이트와 메시 렌더러 전체에 대한 타겟 컬러 Lerp 처리
/// 에디터와 런타임에서 실시간 업데이트 지원
/// </summary>

namespace CAT.Effects
{
    [ExecuteAlways]
    [System.Serializable]
    public class SpriteGroupColorLerp : MonoBehaviour
    {
        [Header("Tint 설정")]
        [SerializeField] private Color targetColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float blendAmount = 0f;  // 0: 원본 컬러, 1: 타겟 컬러
        
        [Header("Target 설정")]
        [SerializeField] private bool includeInactive = false;  // 비활성화된 오브젝트 포함 여부
        [SerializeField] private bool includeUIImages = true;  // UI 이미지 포함 여부
        [SerializeField] private bool includeMeshRenderers = true;  // Mesh Renderer 포함 여부
        [SerializeField] private bool autoRefresh = true;  // 자식 오브젝트 변경시 자동 리프레시 여부
        
        // 캐시된 컴포넌트들과 PropertyBlock
        private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        private List<Image> uiImages = new List<Image>();
        private List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
        private List<MaterialPropertyBlock> spritePropertyBlocks = new List<MaterialPropertyBlock>();
        private List<MaterialPropertyBlock> meshPropertyBlocks = new List<MaterialPropertyBlock>();
        
        // 공유 머티리얼 (성능 최적화)
        private static Material sharedSpriteMaterial;
        private static Material sharedUIMaterial;
        
        // 셰이더 프로퍼티 ID (성능 최적화)
        private static readonly int TargetColorProperty = Shader.PropertyToID("_TargetColor");
        private static readonly int LerpValueProperty = Shader.PropertyToID("_LerpValue");
        
        // 에디터에서 변경사항 감지
        private Color lastTargetColor;
        private float lastBlendAmount;
        private int lastChildCount;
        
        // PropertyBlock 재적용 플래그
        private bool needsPropertyBlockRefresh = false;
        
        #if UNITY_EDITOR
        // 에디터 업데이트 관리
        private static bool isEditorUpdateRegistered = false;
        private const float EDITOR_UPDATE_INTERVAL = 0.016f; // 60fps에 맞춘 업데이트 주기
        private float lastEditorUpdateTime = 0f;
        #endif
        
        void Awake()
        {
            InitializeSharedMaterials(); // 공유 머티리얼 초기화
        }
        
        void OnEnable()
        {
            #if UNITY_EDITOR
            RegisterEditorUpdate();
            #endif
            
            needsPropertyBlockRefresh = true;  // 컴포넌트가 활성화될 때 PropertyBlock 재적용 필요 플래그 설정
        }
        
        void OnDisable()
        {
            #if UNITY_EDITOR
            UnregisterEditorUpdate();
            #endif
        }
        
        void Start()
        {
            RefreshComponents();
            UpdateBlending();
        }
        
        // 앱 일시정지/재개 시 PropertyBlock 재연결
        void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                needsPropertyBlockRefresh = true;
            }
        }
        
        void Update()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorRuntimeUpdate();
                return;
            }
            #endif
            
            // 변경사항 감지만 하고 실제 업데이트는 LateUpdate에서 수행
            if (HasChanges() || (autoRefresh && transform.childCount != lastChildCount))
            {
                if (transform.childCount != lastChildCount)
                {
                    RefreshComponents();
                }
            }
        }
        
        void LateUpdate()
        {
            // 렌더링 전에 최종 컬러 업데이트 수행
            if (HasChanges())
            {
                UpdateBlending();
                UpdateLastValues();
            }
            
            // PropertyBlock 연결 상태 자동 검증
            if (blendAmount > 0.1f)
            {
                ValidatePropertyBlockConnections();
            }
        }
        
        void OnValidate()
        {
            // 에디터에서 Inspector 값이 변경될 때
            // 컴포넌트가 초기화되지 않았으면 초기화
            if (spriteRenderers == null || spritePropertyBlocks == null || meshRenderers == null)
            {
                RefreshComponents();
            }
            
            // PropertyBlock 연결 상태 강제 검증
            needsPropertyBlockRefresh = true;
            
            // 즉시 업데이트
            UpdateBlending();
            UpdateLastValues();
            
            #if UNITY_EDITOR
            // 에디터에서 씬 뷰 업데이트 강제
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneView.RepaintAll();
            #endif
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// 에디터 업데이트 시스템 등록
        /// </summary>
        private void RegisterEditorUpdate()
        {
            if (!isEditorUpdateRegistered)
            {
                UnityEditor.EditorApplication.update += GlobalEditorUpdate;
                isEditorUpdateRegistered = true;
            }
        }
        
        /// <summary>
        /// 에디터 업데이트 시스템 해제
        /// </summary>
        private void UnregisterEditorUpdate()
        {
            #if UNITY_2023_1_OR_NEWER  // Unity 2023.1 이상
            var allInstances = FindObjectsByType<SpriteGroupColorLerp>(FindObjectsSortMode.None);
            #else
            // 씬에 다른 인스턴스가 있는지 확인
            var allInstances = FindObjectsOfType<SpriteGroupColorLerp>();
            #endif
            if (allInstances.Length <= 1) // 자신만 남았다면
            {
                UnityEditor.EditorApplication.update -= GlobalEditorUpdate;
                isEditorUpdateRegistered = false;
            }
        }
        
        /// <summary>
        /// 모든 SpriteGroupColorLerp 인스턴스에 대한 글로벌 에디터 업데이트
        /// </summary>
        private static void GlobalEditorUpdate()
        {
            if (Application.isPlaying) return;
            #if UNITY_2023_1_OR_NEWER  // Unity 2023.1 이상
            var components = FindObjectsByType<SpriteGroupColorLerp>(FindObjectsSortMode.None);
            #else
            var components = FindObjectsOfType<SpriteGroupColorLerp>();
            #endif
            foreach (var component in components)
            {
                if (component != null && component.gameObject.activeInHierarchy)
                {
                    component.EditorUpdateCheck();
                }
            }
        }
        
        /// <summary>
        /// 에디터에서의 실시간 업데이트 체크
        /// </summary>
        private void EditorUpdateCheck()
        {
            float currentTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
            if (currentTime - lastEditorUpdateTime < EDITOR_UPDATE_INTERVAL) return;
            
            lastEditorUpdateTime = currentTime;
            
            // 컴포넌트가 초기화되지 않았으면 초기화
            if (spriteRenderers == null || spritePropertyBlocks == null || meshRenderers == null)
            {
                RefreshComponents();
            }
            
            // 변경사항이 있으면 업데이트
            if (HasChanges() || (autoRefresh && transform.childCount != lastChildCount))
            {
                if (transform.childCount != lastChildCount)
                {
                    RefreshComponents();
                }
                UpdateBlending();
                UpdateLastValues();
            }
        }
        
        /// <summary>
        /// 에디터 모드에서 런타임 업데이트
        /// </summary>
        private void EditorRuntimeUpdate()
        {
            // 자식 수가 변경되었는지 확인
            if (autoRefresh && transform.childCount != lastChildCount)
            {
                RefreshComponents();
            }
            
            // 값이 변경되었는지 확인하고 업데이트
            if (HasChanges())
            {
                UpdateBlending();
                UpdateLastValues();
            }
        }
        #endif
        
        /// <summary>
        /// 공유 머티리얼을 초기화합니다.
        /// </summary>
        private void InitializeSharedMaterials()
        {
            if (sharedSpriteMaterial == null)
            {
                Shader spriteShader = Shader.Find("CAT/2D/SpriteGroupColorLerp");
                if (spriteShader != null)
                {
                    sharedSpriteMaterial = new Material(spriteShader);
                    sharedSpriteMaterial.name = "Shared_SpriteColorLerp";
                }
                else
                {
                    Debug.LogError("CAT/2D/SpriteGroupColorLerp 셰이더를 찾을 수 없습니다!");
                }
            }
            
            if (sharedUIMaterial == null)
            {
                Shader uiShader = Shader.Find("CAT/2D/SpriteGroupColorLerp");
                if (uiShader != null)
                {
                    sharedUIMaterial = new Material(uiShader);
                    sharedUIMaterial.name = "Shared_UIColorLerp";
                }
                else
                {
                    Debug.LogError("CAT/2D/SpriteGroupColorLerp 셰이더를 찾을 수 없습니다!");
                }
            }
        }
        
        /// <summary>
        /// 모든 자식 스프라이트, UI 이미지, 메시 렌더러를 찾아서 캐시합니다.
        /// </summary>
        public void RefreshComponents()
        {
            // 리스트 초기화 (null 체크)
            if (spriteRenderers == null) spriteRenderers = new List<SpriteRenderer>();
            if (uiImages == null) uiImages = new List<Image>();
            if (meshRenderers == null) meshRenderers = new List<MeshRenderer>();
            if (spritePropertyBlocks == null) spritePropertyBlocks = new List<MaterialPropertyBlock>();
            if (meshPropertyBlocks == null) meshPropertyBlocks = new List<MaterialPropertyBlock>();
            
            // 기존 데이터 클리어
            spriteRenderers.Clear();
            uiImages.Clear();
            meshRenderers.Clear();
            spritePropertyBlocks.Clear();
            meshPropertyBlocks.Clear();
            
            // SpriteRenderer 찾기
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(includeInactive);
            foreach (var sprite in sprites)
            {
                spriteRenderers.Add(sprite);
                
                // PropertyBlock 생성
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                spritePropertyBlocks.Add(propertyBlock);
                
                // 공유 머티리얼 적용
                if (sharedSpriteMaterial != null)
                {
                    sprite.material = sharedSpriteMaterial;
                }
            }
            
            // UI Image 찾기
            if (includeUIImages)
            {
                Image[] images = GetComponentsInChildren<Image>(includeInactive);
                foreach (var image in images)
                {
                    uiImages.Add(image);
                    
                    // 공유 머티리얼 적용
                    if (sharedUIMaterial != null)
                    {
                        image.material = sharedUIMaterial;
                    }
                }
            }
            
            // MeshRenderer 찾기
            if (includeMeshRenderers)
            {
                MeshRenderer[] meshes = GetComponentsInChildren<MeshRenderer>(includeInactive);
                foreach (var mesh in meshes)
                {
                    meshRenderers.Add(mesh);
                    
                    // PropertyBlock 생성
                    MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                    meshPropertyBlocks.Add(propertyBlock);
                    
                    // 기존 머티리얼의 MainTexture를 보존하면서 공유 머티리얼 적용
                    if (sharedSpriteMaterial != null)
                    {
                        // 기존 머티리얼의 MainTexture 저장
                        Texture originalMainTex = null;
                        
                        #if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            // 에디터 모드에서는 sharedMaterial 사용
                            if (mesh.sharedMaterial != null && mesh.sharedMaterial.HasProperty("_MainTex"))
                            {
                                originalMainTex = mesh.sharedMaterial.GetTexture("_MainTex");
                            }
                        }
                        else
                        {
                            // 런타임에서는 material 사용
                            if (mesh.material != null && mesh.material.HasProperty("_MainTex"))
                            {
                                originalMainTex = mesh.material.GetTexture("_MainTex");
                            }
                        }
                        #else
                        // 빌드에서는 material 사용
                        if (mesh.material != null && mesh.material.HasProperty("_MainTex"))
                        {
                            originalMainTex = mesh.material.GetTexture("_MainTex");
                        }
                        #endif
                        
                        // 공유 머티리얼을 복사하여 새로운 머티리얼 생성
                        Material newMaterial = new Material(sharedSpriteMaterial);
                        
                        // 기존 MainTexture가 있으면 복사
                        if (originalMainTex != null)
                        {
                            newMaterial.SetTexture("_MainTex", originalMainTex);
                        }
                        
                        mesh.material = newMaterial;
                    }
                }
            }
            
            UpdateLastValues();
            
            #if UNITY_EDITOR
            Debug.Log($"RefreshComponents 완료 - SpriteRenderer: {spriteRenderers.Count}개, UI Image: {uiImages.Count}개, MeshRenderer: {meshRenderers.Count}개");
            #endif
        }
        
        /// <summary>
        /// PropertyBlock 연결 상태를 검증하고 필요시 재연결합니다.
        /// </summary>
        private void ValidatePropertyBlockConnections()
        {
            // SpriteRenderer PropertyBlock 검증
            for (int i = 0; i < spriteRenderers.Count && i < spritePropertyBlocks.Count; i++)
            {
                var sprite = spriteRenderers[i];
                var propertyBlock = spritePropertyBlocks[i];
                
                if (sprite != null && propertyBlock != null)
                {
                    var checkPropertyBlock = new MaterialPropertyBlock();
                    sprite.GetPropertyBlock(checkPropertyBlock);
                    
                    // PropertyBlock이 비어있거나 연결이 끊어진 경우 재연결
                    if (checkPropertyBlock.isEmpty)
                    {
                        propertyBlock.SetColor(TargetColorProperty, targetColor);
                        propertyBlock.SetFloat(LerpValueProperty, blendAmount);
                        sprite.SetPropertyBlock(propertyBlock);
                    }
                }
            }
            
            // MeshRenderer PropertyBlock 검증
            for (int i = 0; i < meshRenderers.Count && i < meshPropertyBlocks.Count; i++)
            {
                var mesh = meshRenderers[i];
                var propertyBlock = meshPropertyBlocks[i];
                
                if (mesh != null && propertyBlock != null)
                {
                    var checkPropertyBlock = new MaterialPropertyBlock();
                    mesh.GetPropertyBlock(checkPropertyBlock);
                    
                    // PropertyBlock이 비어있거나 연결이 끊어진 경우 재연결
                    if (checkPropertyBlock.isEmpty)
                    {
                        propertyBlock.SetColor(TargetColorProperty, targetColor);
                        propertyBlock.SetFloat(LerpValueProperty, blendAmount);
                        mesh.SetPropertyBlock(propertyBlock);
                    }
                }
            }
        }
        
        /// <summary>
        /// PropertyBlock을 스프라이트와 메시에 재적용합니다.
        /// </summary>
        private void RefreshPropertyBlocks()
        {
            // SpriteRenderer PropertyBlock 재연결
            for (int i = 0; i < spriteRenderers.Count && i < spritePropertyBlocks.Count; i++)
            {
                var sprite = spriteRenderers[i];
                var propertyBlock = spritePropertyBlocks[i];
                
                if (sprite != null && propertyBlock != null)
                {
                    // 현재 설정값으로 PropertyBlock 업데이트
                    propertyBlock.SetColor(TargetColorProperty, targetColor);
                    propertyBlock.SetFloat(LerpValueProperty, blendAmount);
                    
                    // PropertyBlock을 스프라이트에 강제 재적용
                    sprite.SetPropertyBlock(propertyBlock);
                    
                    // 추가 검증: PropertyBlock이 제대로 적용되었는지 확인
                    if (blendAmount > 0.1f)
                    {
                        // PropertyBlock이 실제로 적용되었는지 확인
                        var appliedPropertyBlock = new MaterialPropertyBlock();
                        sprite.GetPropertyBlock(appliedPropertyBlock);
                        if (appliedPropertyBlock.isEmpty)
                        {
                            // PropertyBlock이 비어있으면 다시 적용
                            sprite.SetPropertyBlock(propertyBlock);
                        }
                    }
                }
            }
            
            // MeshRenderer PropertyBlock 재연결
            for (int i = 0; i < meshRenderers.Count && i < meshPropertyBlocks.Count; i++)
            {
                var mesh = meshRenderers[i];
                var propertyBlock = meshPropertyBlocks[i];
                
                if (mesh != null && propertyBlock != null)
                {
                    // 현재 설정값으로 PropertyBlock 업데이트
                    propertyBlock.SetColor(TargetColorProperty, targetColor);
                    propertyBlock.SetFloat(LerpValueProperty, blendAmount);
                    
                    // PropertyBlock을 메시에 강제 재적용
                    mesh.SetPropertyBlock(propertyBlock);
                    
                    // 추가 검증: PropertyBlock이 제대로 적용되었는지 확인
                    if (blendAmount > 0.1f)
                    {
                        // PropertyBlock이 실제로 적용되었는지 확인
                        var appliedPropertyBlock = new MaterialPropertyBlock();
                        mesh.GetPropertyBlock(appliedPropertyBlock);
                        if (appliedPropertyBlock.isEmpty)
                        {
                            // PropertyBlock이 비어있으면 다시 적용
                            mesh.SetPropertyBlock(propertyBlock);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 현재 설정으로 블랜딩을 업데이트합니다.
        /// </summary>
        public void UpdateBlending()
        {
            // 안전성 검사: 컴포넌트가 초기화되지 않았으면 스킵
            if (spriteRenderers == null || spritePropertyBlocks == null || uiImages == null || meshRenderers == null || meshPropertyBlocks == null)
            {
                return;
            }
            
            // PropertyBlock 재적용이 필요한 경우
            if (needsPropertyBlockRefresh)
            {
                RefreshPropertyBlocks();
                needsPropertyBlockRefresh = false;
            }
            
            // SpriteRenderer 업데이트 (PropertyBlock 사용 - 성능 최적화)
            for (int i = 0; i < spriteRenderers.Count && i < spritePropertyBlocks.Count; i++)
            {
                var sprite = spriteRenderers[i];
                var propertyBlock = spritePropertyBlocks[i];
                
                if (sprite != null && propertyBlock != null)
                {
                    propertyBlock.SetColor(TargetColorProperty, targetColor);
                    propertyBlock.SetFloat(LerpValueProperty, blendAmount);
                    sprite.SetPropertyBlock(propertyBlock);
                    
                    // PropertyBlock 연결 상태 확인 및 재연결
                    if (blendAmount > 0.1f)
                    {
                        var checkPropertyBlock = new MaterialPropertyBlock();
                        sprite.GetPropertyBlock(checkPropertyBlock);
                        if (checkPropertyBlock.isEmpty)
                        {
                            // PropertyBlock이 연결되지 않았으면 재연결
                            sprite.SetPropertyBlock(propertyBlock);
                        }
                    }
                }
            }
            
            // MeshRenderer 업데이트 (PropertyBlock 사용 - 성능 최적화)
            for (int i = 0; i < meshRenderers.Count && i < meshPropertyBlocks.Count; i++)
            {
                var mesh = meshRenderers[i];
                var propertyBlock = meshPropertyBlocks[i];
                
                if (mesh != null && propertyBlock != null)
                {
                    propertyBlock.SetColor(TargetColorProperty, targetColor);
                    propertyBlock.SetFloat(LerpValueProperty, blendAmount);
                    mesh.SetPropertyBlock(propertyBlock);
                    
                    // PropertyBlock 연결 상태 확인 및 재연결
                    if (blendAmount > 0.1f)
                    {
                        var checkPropertyBlock = new MaterialPropertyBlock();
                        mesh.GetPropertyBlock(checkPropertyBlock);
                        if (checkPropertyBlock.isEmpty)
                        {
                            // PropertyBlock이 연결되지 않았으면 재연결
                            mesh.SetPropertyBlock(propertyBlock);
                        }
                    }
                }
            }
            
            // UI Image 업데이트 (직접 머티리얼 프로퍼티 설정)
            for (int i = 0; i < uiImages.Count; i++)
            {
                var image = uiImages[i];
                
                if (image != null && image.material != null)
                {
                    image.material.SetColor(TargetColorProperty, targetColor);
                    image.material.SetFloat(LerpValueProperty, blendAmount);
                }
            }
        }
        
        /// <summary>
        /// 타겟 컬러를 설정합니다.
        /// </summary>
        public void SetTargetColor(Color color)
        {
            targetColor = color;
            
            #if UNITY_EDITOR
            // 에디터에서 즉시 업데이트
            if (!Application.isPlaying)
            {
                UpdateBlending();
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneView.RepaintAll();
            }
            #endif
        }
        
        /// <summary>
        /// 블랜드 양을 설정합니다 (0 = 원본, 1 = 완전히 타겟 컬러).
        /// </summary>
        public void SetBlendAmount(float amount)
        {
            blendAmount = Mathf.Clamp01(amount);
            
            #if UNITY_EDITOR
            // 에디터에서 즉시 업데이트
            if (!Application.isPlaying)
            {
                UpdateBlending();
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneView.RepaintAll();
            }
            #endif
        }
        
        /// <summary>
        /// 타겟 컬러와 블랜드 양을 동시에 설정합니다.
        /// </summary>
        public void SetTint(Color color, float amount)
        {
            targetColor = color;
            blendAmount = Mathf.Clamp01(amount);
            
            #if UNITY_EDITOR
            // 에디터에서 즉시 업데이트
            if (!Application.isPlaying)
            {
                UpdateBlending();
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneView.RepaintAll();
            }
            #endif
        }
        
        private bool HasChanges()
        {
            return targetColor != lastTargetColor || 
                !Mathf.Approximately(blendAmount, lastBlendAmount);
        }
        
        private void UpdateLastValues()
        {
            lastTargetColor = targetColor;
            lastBlendAmount = blendAmount;
            lastChildCount = transform.childCount;
        }
        
        void OnDestroy()
        {
            #if UNITY_EDITOR
            UnregisterEditorUpdate();
            #endif
            
            // PropertyBlock 정리
            spritePropertyBlocks?.Clear();
            meshPropertyBlocks?.Clear();
        }
        
        #region 에디터 전용 메서드
        
        #if UNITY_EDITOR
        
        [ContextMenu("Refresh Components")]
        private void EditorRefreshComponents()
        {
            RefreshComponents();
            UpdateBlending();
            Debug.Log($"컴포넌트 새로고침 완료 - SpriteRenderer: {spriteRenderers.Count}개, UI Image: {uiImages.Count}개, MeshRenderer: {meshRenderers.Count}개");
        }

        #endif
        
        #endregion
    }
}