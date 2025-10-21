using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;


public class UnitPrefabGeneratorWindow : EditorWindow
{
    #region Constants and Configuration
    
    // Face 표정 오브젝트 이름들 (앞으로 추가될 수 있음)
    private static readonly string[] FACE_EXPRESSION_NAMES = 
    {
        "Normal", "Happy", "Attack", "Blank", "Eye"
        // 새로운 표정이 추가되면 여기에 추가하면 됩니다.
        // 예: "Surprised", "Confused", "Excited", "Worried" 등
    };

    // 지원하는 이미지 확장자
    private static readonly string[] SUPPORTED_IMAGE_EXTENSIONS = { ".png", ".jpg" };

    // Animator 이름들 (앞으로 추가될 수 있음)
    private static readonly string[] ANIMATOR_NAMES = 
    {
        "Ar", "Hu", "Pa", "Pr", "Wa", "Wi", "Sister", "Goblin", "Golem", "Ogre", "Slime", "Treeant", "Wolf"
        // 새로운 Animator가 추가되면 여기에 추가하면 됩니다.
    };
    
    #endregion

    #region Helper Methods
    
    // 주어진 이름이 Face 표정 오브젝트 이름인지 확인합니다.
    private static bool IsFaceExpressionName(string name)
    {
        return System.Array.Exists(FACE_EXPRESSION_NAMES, expressionName => expressionName == name);
    }
    
    // 주어진 파일 경로가 지원되는 이미지 파일인지 확인합니다.
    private static bool IsSupportedImageFile(string filePath)
    {
        return System.Array.Exists(SUPPORTED_IMAGE_EXTENSIONS, ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
    
    // 주어진 이름이 유효한 Animator 이름인지 확인합니다.
    private static bool IsValidAnimatorName(string name)
    {
        return System.Array.Exists(ANIMATOR_NAMES, animatorName => animatorName == name);
    }
    
    #endregion

    #region Fields
    
    private GameObject baseModelPrefab;
    private TextAsset referenceJSON;
    private DefaultAsset imageFolder;
    private DefaultAsset outputFolder;
    private DefaultAsset animationFolder;
    
    #endregion

    #region Unity Editor Integration
    
    [MenuItem("CAT/Utility/Unit Prefab Generator")]
    public static void ShowWindow()
    {
        GetWindow<UnitPrefabGeneratorWindow>("Unit Prefab Generator");
    }
    
    private void OnEnable()
    {
        LoadSettings();
    }
    
    private void OnDisable()
    {
        SaveSettings();
    }
    
    #endregion

    #region Settings Management
    
    /// <summary>
    /// PlayerPrefs에 설정을 저장합니다.
    /// </summary>
    private void SaveSettings()
    {
        // GameObject 참조 저장 (GUID 사용)
        if (baseModelPrefab != null)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(baseModelPrefab));
            EditorPrefs.SetString("UnitPrefabGenerator_BaseModelPrefab", guid);
        }
        else
        {
            EditorPrefs.DeleteKey("UnitPrefabGenerator_BaseModelPrefab");
        }
        
        if (referenceJSON != null)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(referenceJSON));
            EditorPrefs.SetString("UnitPrefabGenerator_ReferenceJSON", guid);
        }
        else
        {
            EditorPrefs.DeleteKey("UnitPrefabGenerator_ReferenceJSON");
        }
        
        if (imageFolder != null)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(imageFolder));
            EditorPrefs.SetString("UnitPrefabGenerator_ImageFolder", guid);
        }
        else
        {
            EditorPrefs.DeleteKey("UnitPrefabGenerator_ImageFolder");
        }
        
        if (animationFolder != null)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(animationFolder));
            EditorPrefs.SetString("UnitPrefabGenerator_AnimationFolder", guid);
        }
        else
        {
            EditorPrefs.DeleteKey("UnitPrefabGenerator_AnimationFolder");
        }
        
        if (outputFolder != null)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(outputFolder));
            EditorPrefs.SetString("UnitPrefabGenerator_OutputFolder", guid);
        }
        else
        {
            EditorPrefs.DeleteKey("UnitPrefabGenerator_OutputFolder");
        }
        
    }
    
    /// <summary>
    /// PlayerPrefs에서 설정을 로드합니다.
    /// </summary>
    private void LoadSettings()
    {
        // Base Model Prefab 로드
        string baseModelGuid = EditorPrefs.GetString("UnitPrefabGenerator_BaseModelPrefab", "");
        if (!string.IsNullOrEmpty(baseModelGuid))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(baseModelGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                baseModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
        }
        
        // Reference JSON 로드
        string referenceJSONGuid = EditorPrefs.GetString("UnitPrefabGenerator_ReferenceJSON", "");
        if (!string.IsNullOrEmpty(referenceJSONGuid))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(referenceJSONGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                referenceJSON = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            }
        }
        
        // Image Folder 로드
        string imageFolderGuid = EditorPrefs.GetString("UnitPrefabGenerator_ImageFolder", "");
        if (!string.IsNullOrEmpty(imageFolderGuid))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(imageFolderGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                imageFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
            }
        }
        
        // Animation Folder 로드
        string animationFolderGuid = EditorPrefs.GetString("UnitPrefabGenerator_AnimationFolder", "");
        if (!string.IsNullOrEmpty(animationFolderGuid))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(animationFolderGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                animationFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
            }
        }
        
        // Output Folder 로드
        string outputFolderGuid = EditorPrefs.GetString("UnitPrefabGenerator_OutputFolder", "");
        if (!string.IsNullOrEmpty(outputFolderGuid))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(outputFolderGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
            }
        }
        
    }
    
    /// <summary>
    /// 모든 설정을 초기화합니다.
    /// </summary>
    private void ClearSettings()
    {
        // 필드 초기화
        baseModelPrefab = null;
        referenceJSON = null;
        imageFolder = null;
        animationFolder = null;
        outputFolder = null;
        
        // EditorPrefs에서 키 삭제
        EditorPrefs.DeleteKey("UnitPrefabGenerator_BaseModelPrefab");
        EditorPrefs.DeleteKey("UnitPrefabGenerator_ReferenceJSON");
        EditorPrefs.DeleteKey("UnitPrefabGenerator_ImageFolder");
        EditorPrefs.DeleteKey("UnitPrefabGenerator_AnimationFolder");
        EditorPrefs.DeleteKey("UnitPrefabGenerator_OutputFolder");
        
    }
    
    #endregion

    #region UI Methods
    
    private void OnGUI()
    {
        DrawHeader();
        DrawInputFields();
        DrawGenerateButton();
    }
    
    private void DrawHeader()
    {
        GUILayout.Label("Unit Prefab Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
    }
    
    private void DrawInputFields()
    {
        baseModelPrefab = (GameObject)EditorGUILayout.ObjectField("Base Model Prefab", baseModelPrefab, typeof(GameObject), false);
        referenceJSON = (TextAsset)EditorGUILayout.ObjectField("Reference JSON", referenceJSON, typeof(TextAsset), false);
        imageFolder = (DefaultAsset)EditorGUILayout.ObjectField("Image Folder", imageFolder, typeof(DefaultAsset), false);
        animationFolder = (DefaultAsset)EditorGUILayout.ObjectField("Animation Folder", animationFolder, typeof(DefaultAsset), false);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
        
        EditorGUILayout.Space(10);
        
        // 설정 관리 버튼들
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("설정 초기화", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("설정 초기화", "모든 설정을 초기화하시겠습니까?", "예", "아니오"))
            {
                ClearSettings();
            }
        }
        
        if (GUILayout.Button("설정 저장", GUILayout.Height(25)))
        {
            SaveSettings();
            EditorUtility.DisplayDialog("설정 저장", "설정이 저장되었습니다.", "확인");
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawGenerateButton()
    {
        EditorGUILayout.Space(20);
        
        if (GUILayout.Button("Generate Prefab", GUILayout.Height(30)))
        {
            SetupHierarchyFromJSON();
        }
    }
    
    #endregion

    #region Main Logic
    
    // 입력 필드들 유효성 검사
    private bool ValidateInputs()
    {
        if (baseModelPrefab == null || referenceJSON == null || imageFolder == null || animationFolder == null || outputFolder == null)
        {
            Debug.LogError("모든 필드를 지정해주세요.");
            return false;
        }
        return true;
    }
    
    // JSON 파일을 기반으로 프리팹 생성
    private void SetupHierarchyFromJSON()
    {
        if (!ValidateInputs())
        {
            return;
        }

        // JSON 파일 로드
        string jsonContent = referenceJSON.text;
        HierarchyData hierarchyData = JsonUtility.FromJson<HierarchyData>(jsonContent);
        
        if (hierarchyData == null || hierarchyData.gameObjects == null)
        {
            Debug.LogError("JSON 파일을 파싱할 수 없습니다. 파일 형식을 확인해주세요.");
            return;
        }

        string imagePath = AssetDatabase.GetAssetPath(imageFolder); // 이미지 폴더 경로 가져오기
        Debug.Log($"🔍 이미지 폴더 경로: {imagePath}");
        
        var variantSprites = GroupSpritesByVariantName(imagePath);  // 이미지 폴더에서 스프라이트 그룹화
        Debug.Log($"🔍 GroupSpritesByVariantName 결과: {variantSprites.Count}개 베리언트 발견");

        if (variantSprites.Count == 0)
        {
            Debug.LogError("처리할 이미지가 없습니다. 이미지 폴더를 확인해주세요.");
            return;
        }
        
        Debug.Log($"✅ {variantSprites.Count} 베리언트 개수 확인");

        foreach (var entry in variantSprites)
        {
            string variantName = entry.Key;  // 베리언트 이름
            List<Sprite> spritesForVariant = entry.Value;  // 베리언트에 해당하는 스프라이트 리스트

            // 각 베리언트 처리
            ProcessVariant(variantName, spritesForVariant, hierarchyData);
        }
    }
    
    /// <summary>
    /// 개별 베리언트를 처리합니다 (프리팹 생성부터 저장까지)
    /// </summary>
    private void ProcessVariant(string variantName, List<Sprite> spritesForVariant, HierarchyData hierarchyData)
    {
        Debug.Log($"🚀 베리언트 처리 시작: {variantName} (스프라이트 {spritesForVariant.Count}개)");
        
        // 1. 기본 모델을 하이어라키에 로드
        GameObject baseModelInstance = PrefabUtility.InstantiatePrefab(baseModelPrefab) as GameObject;
        if (baseModelInstance == null)
        {
            Debug.LogError($"❌ 기본 모델 프리팹을 로드하는 데 실패했습니다: {variantName}");
            return;
        }
        
        try
        {
            // 2. 하이어라키에 로드한 프리팹 이름을 텍스처 이름 기반으로 변경
            baseModelInstance.name = variantName;
            
            // 3. 베리언트 이름에서 Animator 이름 추출 및 변경
            string[] variantParts = variantName.Split('_');
            if (variantParts.Length > 0)
            {
                string animatorName = variantParts[0];
                ChangeAnimatorController(baseModelInstance, animatorName);
            }
            
            // 4. Pivot 오브젝트 찾기 (프리팹 상태 유지)
            Transform basePivot = FindPivotTransform(baseModelInstance.transform);
            if (basePivot == null)
            {
                Debug.LogError($"❌ Pivot 오브젝트를 찾을 수 없습니다: {variantName}");
                return;
            }
            
            // 5. JSON 데이터를 기반으로 게임오브젝트 생성
            CreateGameObjectsFromJSON(basePivot, hierarchyData);

            // 6. 이미지 리소스에 맞춰 오브젝트 구조 동적 생성
            CreateDynamicObjectsFromSprites(baseModelInstance.transform, spritesForVariant, variantName);

            // 7. 이미지 교체
            ReplaceSprites(baseModelInstance.transform, spritesForVariant, variantName);

            // 8. 빈 Sprite Renderer 오브젝트와 부모 오브젝트 정리
            CleanupEmptySpriteRenderers(baseModelInstance.transform);
            
            // 9. 정렬 수행 (프리팹 저장 전에)
            SortGameObjectHierarchy(baseModelInstance.transform);
            
            // 10. 베리언트 프리팹으로 저장 (한 번만)
            SaveAsVariantPrefab(baseModelInstance, variantName);

            Debug.Log($"✅ {variantName} 베리언트 처리 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ {variantName} 베리언트 처리 중 오류 발생: {e.Message}");
            // 오류 발생 시에만 메모리 정리
            if (baseModelInstance != null)
            {
                GameObject.DestroyImmediate(baseModelInstance);
            }
        }
    }
    
    #endregion

    #region Prefab Management
    
    /// <summary>
    /// 처리된 게임오브젝트를 베리언트 프리팹으로 저장합니다.
    /// </summary>
    /// <param name="gameObject">저장할 게임오브젝트</param>
    /// <param name="variantName">베리언트 이름</param>
    private void SaveAsVariantPrefab(GameObject gameObject, string variantName)
    {
        if (outputFolder == null)
        {
            Debug.LogError("출력 폴더가 지정되지 않았습니다.");
            return;
        }

        string outputPath = AssetDatabase.GetAssetPath(outputFolder);
        if (string.IsNullOrEmpty(outputPath))
        {
            Debug.LogError("출력 폴더 경로를 가져올 수 없습니다.");
            return;
        }

        // 베리언트 프리팹 파일 경로 생성
        string prefabPath = Path.Combine(outputPath, variantName + ".prefab").Replace("\\", "/");
        
        // 동일한 이름의 프리팹이 이미 존재하는지 확인
        if (File.Exists(prefabPath))
        {
            Debug.LogWarning($"⚠️ 프리팹이 이미 존재합니다: {prefabPath}");
            EditorUtility.DisplayDialog("프리팹 중복", $"저장 폴더에 동일한 이름의 프리팹이 존재합니다.\n경로: {prefabPath}", "확인");
            return;
        }
        
        Debug.Log($"💾 프리팹 저장 시도: {prefabPath}");

        try
        {
            // 베리언트 프리팹으로 저장 (기본 프리팹과 연결)
            GameObject variantPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                gameObject, 
                prefabPath, 
                InteractionMode.AutomatedAction, 
                out bool success
            );
            
            if (!success || variantPrefab == null)
            {
                Debug.LogError($"❌ 베리언트 프리팹 '{variantName}' 저장에 실패했습니다.");
            }
            else
            {
                Debug.Log($"✅ 베리언트 프리팹 '{variantName}' 저장 성공: {prefabPath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 베리언트 프리팹 '{variantName}' 저장 중 오류 발생: {e.Message}");
        }
    }
    
    #endregion

    #region Sorting and Organization
    
    // 게임오브젝트 하이어라키를 정렬합니다 (프리팹 저장 전에 수행)
    private void SortGameObjectHierarchy(Transform root)
    {
        // Pivot 오브젝트 찾기
        Transform pivot = FindPivotTransform(root);
        if (pivot == null) return;
        
        // 정렬 수행
        SortPivotChildrenAlphabetically(pivot);
        SortHeadChildrenAlphabetically(pivot);
    }
    
    private void SortPivotChildrenAlphabetically(Transform pivot)
    {
        // 자식들을 리스트로 수집
        List<Transform> children = new List<Transform>();
        for (int i = 0; i < pivot.childCount; i++)
        {
            children.Add(pivot.GetChild(i));
        }
        
        if (children.Count == 0) return;
        
        // HierarchyRenamerInjector의 정렬 로직을 사용하여 정렬
        var sortedChildren = children.OrderBy(child => child.name, new HierarchicalNameComparer()).ToArray();
        
        // 정렬된 순서대로 하이어라키에서의 위치를 변경, 역순으로 설정하여 인덱스 충돌을 방지
        for (int i = sortedChildren.Length - 1; i >= 0; i--)
        {
            sortedChildren[i].SetSiblingIndex(i);
        }
    }
    
    private void SortHeadChildrenAlphabetically(Transform pivot)
    {
        Transform headTransform = FindTransformByName(pivot, "Head");
        if (headTransform == null) 
        {
            Debug.Log($"❌ Head 오브젝트를 찾을 수 없습니다. {pivot.name}");
            return;
        }
        
        // Head의 자식들을 리스트로 수집
        List<Transform> headChildren = new List<Transform>();
        for (int i = 0; i < headTransform.childCount; i++)
        {
            Transform child = headTransform.GetChild(i);
            headChildren.Add(child);
        }
        
        if (headChildren.Count == 0) return;
        
        // HierarchyRenamerInjector의 정렬 로직을 사용하여 정렬
        var sortedChildren = headChildren.OrderBy(child => child.name, new HierarchicalNameComparer()).ToArray();
        
        // 정렬된 순서대로 하이어라키에서의 위치를 변경, 역순으로 설정하여 인덱스 충돌을 방지
        for (int i = sortedChildren.Length - 1; i >= 0; i--)
        {
            sortedChildren[i].SetSiblingIndex(i);
        }
    }
    
    // "_" 기준으로 분리하여 다단계 정렬을 위한 비교자 (HierarchyRenamerInjector에서 가져옴)
    private class HierarchicalNameComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var partsX = x.Split('_');
            var partsY = y.Split('_');

            int maxLength = Math.Max(partsX.Length, partsY.Length);

            for (int i = 0; i < maxLength; i++)
            {
                string partX = i < partsX.Length ? partsX[i] : "";
                string partY = i < partsY.Length ? partsY[i] : "";

                // 숫자와 문자열을 구분하여 비교
                int comparison = CompareParts(partX, partY);
                if (comparison != 0)
                    return comparison;
            }

            return 0;
        }

        private int CompareParts(string partX, string partY)
        {
            // 둘 다 숫자인지 확인
            if (int.TryParse(partX, out int numX) && int.TryParse(partY, out int numY))
            {
                return numX.CompareTo(numY);
            }

            // 둘 다 숫자가 아니면 문자열로 비교
            return string.Compare(partX, partY, StringComparison.OrdinalIgnoreCase);
        }
    }
    
    #endregion

    #region GameObject Creation and Management
    
    private Transform FindPivotTransform(Transform root)
    {
        // 현재 Transform이 "Pivot"인지 확인
        if (root.name == "Pivot") return root;

        // 자식들에서 재귀적으로 검색
        foreach (Transform child in root)
        {
            Transform found = FindPivotTransform(child);
            if (found != null) return found;
        }

        return null;
    }
    
    // Animator 오브젝트를 찾습니다.
    private Transform FindAnimatorTransform(Transform root)
    {
        // 현재 Transform이 "Animator"인지 확인
        if (root.name == "Animator") return root;

        // 자식들에서 재귀적으로 검색
        foreach (Transform child in root)
        {
            Transform found = FindAnimatorTransform(child);
            if (found != null) return found;
        }

        return null;
    }
    
    // Animator 컨트롤러를 변경합니다.
    private void ChangeAnimatorController(GameObject gameObject, string animatorName)
    {
        if (!IsValidAnimatorName(animatorName))
        {
            Debug.LogWarning($"⚠️ 유효하지 않은 Animator 이름입니다: '{animatorName}'. 유효한 이름: {string.Join(", ", ANIMATOR_NAMES)}");
            return;
        }

        if (animationFolder == null)
        {
            Debug.LogError("Animation 폴더가 지정되지 않았습니다.");
            return;
        }

        // Animator 오브젝트 찾기
        Transform animatorTransform = FindAnimatorTransform(gameObject.transform);
        if (animatorTransform == null)
        {
            Debug.LogWarning($"⚠️ Animator 오브젝트를 찾을 수 없습니다.");
            return;
        }

        // Animator 컴포넌트 가져오기
        Animator animator = animatorTransform.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"⚠️ Animator 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        // Animation 폴더 경로 가져오기
        string animationFolderPath = AssetDatabase.GetAssetPath(animationFolder);
        if (string.IsNullOrEmpty(animationFolderPath))
        {
            Debug.LogError("Animation 폴더 경로를 가져올 수 없습니다.");
            return;
        }

        // Animator Controller 찾기 (하위 폴더 포함)
        RuntimeAnimatorController controller = FindAnimatorController(animationFolderPath, animatorName);
        if (controller == null)
        {
            Debug.LogWarning($"⚠️ Animator Controller를 찾을 수 없습니다: '{animatorName}.controller' 또는 '{animatorName}.overrideController'");
            return;
        }

        // Animator Controller 변경
        animator.runtimeAnimatorController = controller;
    }

    // Animation 폴더에서 Animator Controller를 찾습니다 (하위 폴더 포함).
    private RuntimeAnimatorController FindAnimatorController(string folderPath, string animatorName)
    {
        // .controller와 .overrideController 둘 다 검색
        string[] possibleExtensions = { ".controller", ".overrideController" };
        
        foreach (string extension in possibleExtensions)
        {
            string controllerFileName = $"{animatorName}{extension}";
            
            // 현재 폴더에서 직접 검색
            string directPath = Path.Combine(folderPath, controllerFileName).Replace("\\", "/");
            
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(directPath);
            if (controller != null)
            {
                return controller;
            }
        }

        // 하위 폴더들을 재귀적으로 검색
        string[] subDirectories = Directory.GetDirectories(folderPath);
        foreach (string subDir in subDirectories)
        {
            string subDirPath = subDir.Replace("\\", "/");
            
            RuntimeAnimatorController controller = FindAnimatorController(subDirPath, animatorName);
            if (controller != null) return controller;
        }

        return null;
    }

    private void CreateGameObjectsFromJSON(Transform basePivot, HierarchyData hierarchyData)
    {
        
        // JSON 데이터를 부모-자식 관계에 따라 정렬 (부모가 먼저 생성되도록)
        List<GameObjectData> sortedGameObjects = new List<GameObjectData>();
        Dictionary<string, GameObjectData> gameObjectMap = new Dictionary<string, GameObjectData>();
        
        foreach (var gameObjectData in hierarchyData.gameObjects)
        {
            gameObjectMap[gameObjectData.path] = gameObjectData;
        }
        
        // Pivot을 제외한 모든 게임오브젝트를 부모-자식 순서로 정렬
        foreach (var gameObjectData in hierarchyData.gameObjects)
        {
            // Pivot 자체는 건너뛰기
            if (gameObjectData.name == "Pivot")
                continue;
                
            sortedGameObjects.Add(gameObjectData);
        }
        
        // 부모가 먼저 생성되도록 정렬 (path 길이 기준)
        sortedGameObjects.Sort((a, b) => a.path.Split('/').Length.CompareTo(b.path.Split('/').Length));
        
        // 정렬된 순서대로 게임오브젝트 생성
        foreach (var gameObjectData in sortedGameObjects)
        {
            CreateGameObjectFromData(basePivot, gameObjectData, gameObjectMap);
        }
        
    }
    
    private void CreateGameObjectFromData(Transform basePivot, GameObjectData data, Dictionary<string, GameObjectData> gameObjectMap)
    {
        // 부모 Transform 찾기
        Transform parentTransform = basePivot;
        if (!string.IsNullOrEmpty(data.parentPath) && data.parentPath != "Pivot")
        {
            parentTransform = FindTransformByPath(basePivot, data.parentPath);
            if (parentTransform == null)
            {
                parentTransform = basePivot;
            }
        }
        
        // 이미 존재하는지 확인 (기본 모델의 Pivot은 비어있으므로 일반적으로 존재하지 않음)
        Transform existingTransform = parentTransform.Find(data.name);
        if (existingTransform != null)
        {
            ApplyTransformData(existingTransform, data);
            ApplyComponentData(existingTransform, data);
            return;
        }
        
        // 새로운 게임오브젝트 생성
        GameObject newGameObject = new GameObject(data.name);
        newGameObject.transform.SetParent(parentTransform, false);
        
        // Transform 정보 적용
        ApplyTransformData(newGameObject.transform, data);
        
        // 컴포넌트 정보 적용
        ApplyComponentData(newGameObject.transform, data);
    }
    
    private Transform FindTransformByPath(Transform root, string path)
    {
        string[] pathParts = path.Split('/');
        Transform current = root;
        
        foreach (string part in pathParts)
        {
            if (part == "Pivot")
                continue;
                
            current = current.Find(part);
            if (current == null)
                return null;
        }
        
        return current;
    }
    
    private void ApplyTransformData(Transform transform, GameObjectData data)
    {
        transform.localPosition = new Vector3(data.posX, data.posY, data.posZ);
        transform.localEulerAngles = new Vector3(data.rotX, data.rotY, data.rotZ);
        transform.localScale = new Vector3(data.scaleX, data.scaleY, data.scaleZ);
    }
    
    private void ApplyComponentData(Transform transform, GameObjectData data)
    {
        foreach (var componentInfo in data.components)
        {
            // Transform은 이미 적용했으므로 건너뛰기
            if (componentInfo.componentName == "Transform")
                continue;
                
            // 컴포넌트 타입 가져오기
            Type componentType = Type.GetType(componentInfo.componentType);
            if (componentType == null)
            {
                // Unity 네임스페이스에서 찾기
                componentType = Type.GetType("UnityEngine." + componentInfo.componentName + ", UnityEngine");
            }
            
            if (componentType != null && componentType.IsSubclassOf(typeof(Component)))
            {
                // 이미 해당 컴포넌트가 있는지 확인
                Component existingComponent = transform.GetComponent(componentType);
                if (existingComponent == null)
                {
                    // 컴포넌트 추가
                    existingComponent = transform.gameObject.AddComponent(componentType);
                }
                
                // SpriteRenderer의 경우 SortingOrder 적용
                if (componentInfo.componentName == "SpriteRenderer" && existingComponent is SpriteRenderer spriteRenderer)
                {
                    spriteRenderer.sortingOrder = componentInfo.sortingOrder;
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ 컴포넌트 타입 '{componentInfo.componentType}'을 찾을 수 없습니다.");
            }
        }
    }
    
    #endregion

    #region Dynamic Object Creation
    
    /// <summary>
    /// 스프라이트 이름에서 부모 오브젝트 이름들을 추출하여 동적 오브젝트를 생성합니다.
    /// </summary>
    private void CreateDynamicObjectsFromSprites(Transform parent, List<Sprite> sprites, string variantName)
    {
        // 스프라이트 이름에서 부모 오브젝트 이름들을 추출
        Dictionary<string, List<string>> parentToSuffixes = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> parentToVariants = new Dictionary<string, List<string>>();
        
        foreach (Sprite sprite in sprites)
        {
            string spriteName = sprite.name;
            if (spriteName.StartsWith(variantName + "_"))
            {
                string suffix = spriteName.Substring(variantName.Length + 1); // "_" 제거
                string[] parts = suffix.Split('_');
                
                if (parts.Length >= 2)
                {
                    // 마지막 부분이 F, B인지 확인
                    string lastPart = parts[parts.Length - 1];
                    if (lastPart == "F" || lastPart == "B")
                    {
                        string parentName = string.Join("_", parts, 0, parts.Length - 1);
                        
                        // Head_0_1_F, Head_0_2_B 등의 경우 Head variant로도 처리
                        if (parentName.StartsWith("Head_"))
                        {
                            // Head variant로 추가
                            if (!parentToVariants.ContainsKey("Head"))
                            {
                                parentToVariants["Head"] = new List<string>();
                            }
                            if (!parentToVariants["Head"].Contains(parentName))
                            {
                                parentToVariants["Head"].Add(parentName);
                                Debug.Log($"🔍 Head variant 추가: {parentName} (F/B suffix에서)");
                            }
                        }
                        else
                        {
                            // 일반적인 F, B suffix 처리
                            if (!parentToSuffixes.ContainsKey(parentName))
                            {
                                parentToSuffixes[parentName] = new List<string>();
                            }
                            if (!parentToSuffixes[parentName].Contains(lastPart))
                            {
                                parentToSuffixes[parentName].Add(lastPart);
                            }
                        }
                    }
                    else
                    {
                        // F, B가 아닌 경우 (예: Head_0_1, Head_0_2, Normal, Happy 등)
                        if (parts.Length >= 3 && parts[0] == "Head")
                        {
                            // Head_0_1, Head_0_2 등의 경우 Head를 부모로, Head_0_1을 변형으로 처리
                            string parentName = "Head";
                            string variantPart = string.Join("_", parts, 0, parts.Length);
                            
                            if (!parentToVariants.ContainsKey(parentName))
                            {
                                parentToVariants[parentName] = new List<string>();
                            }
                            if (!parentToVariants[parentName].Contains(variantPart))
                            {
                                parentToVariants[parentName].Add(variantPart);
                            }
                        }
                        else if (parts.Length >= 2)
                        {
                            string parentName = string.Join("_", parts, 0, parts.Length - 1);
                            string variantPart = parts[parts.Length - 1];
                            
                            if (!parentToVariants.ContainsKey(parentName))
                            {
                                parentToVariants[parentName] = new List<string>();
                            }
                            if (!parentToVariants[parentName].Contains(variantPart))
                            {
                                parentToVariants[parentName].Add(variantPart);
                            }
                        }
                        else if (parts.Length == 1)
                        {
                            // 단일 부분인 경우 (예: Body, Hand_L, Hand_R 등)
                            // 이들은 이미 존재하는 오브젝트들이므로 변형 오브젝트 생성이 필요하지 않음
                        }
                    }
                }
            }
        }

        // 각 부모 오브젝트에 대해 동적 오브젝트 생성
        foreach (var entry in parentToSuffixes)
        {
            string parentName = entry.Key;
            List<string> suffixes = entry.Value;
            
            Transform parentTransform = FindTransformByName(parent, parentName);
            if (parentTransform != null)
            {
                CreateDynamicImageObjects(parentTransform, suffixes, variantName, parentName);
            }
        }

        // Head 오브젝트들을 별도로 처리
        Dictionary<string, List<string>> headVariants = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> otherVariants = new Dictionary<string, List<string>>();
        
        foreach (var entry in parentToVariants)
        {
            string parentName = entry.Key;
            List<string> variants = entry.Value;
            
            if (parentName == "Head")
            {
                // Head 오브젝트의 직접적인 자식들 (Head_0_1, Head_0_2 등)
                headVariants[parentName] = variants;
            }
            else if (parentName.StartsWith("Head_"))
            {
                // Head_0_1, Head_0_2 등의 개별 Head 변형들
                if (!headVariants.ContainsKey("Head"))
                {
                    headVariants["Head"] = new List<string>();
                }
                headVariants["Head"].Add(parentName);
            }
            else
            {
                otherVariants[parentName] = variants;
            }
        }
        
        // Head 오브젝트들 처리
        foreach (var entry in headVariants)
        {
            string parentName = entry.Key;
            List<string> variants = entry.Value;
            
            Debug.Log($"🔍 Head variant 처리 시작: {parentName} (variants: {string.Join(", ", variants)})");
            
            Transform parentTransform = FindTransformByName(parent, parentName);
            if (parentTransform != null)
            {
                Debug.Log($"✅ Head 부모 오브젝트 '{parentName}' 찾음 - CreateHeadObjects 호출");
                CreateHeadObjects(parentTransform, variants, variantName, parentName);
            }
            else
            {
                Debug.LogWarning($"❌ Head 부모 오브젝트 '{parentName}'를 찾을 수 없습니다.");
            }
        }
        
        // 다른 오브젝트들 처리
        foreach (var entry in otherVariants)
        {
            string parentName = entry.Key;
            List<string> variants = entry.Value;
            
            Transform parentTransform = FindTransformByName(parent, parentName);
            if (parentTransform != null)
            {
                CreateDynamicVariantObjects(parentTransform, variants, variantName, parentName);
            }
            else
            {
                // Debug.Log($"❌ 부모 오브젝트 '{parentName}' 찾을 수 없음, 상위 부모에서 검색");
                // 부모 오브젝트를 찾을 수 없는 경우, 상위 부모에서 찾아서 생성
                CreateDynamicVariantObjectsWithParentSearch(parent, parentName, variants, variantName);
            }
        }
    }

    // 이름으로 오브젝트 찾기
    private Transform FindTransformByName(Transform root, string name)
    {
        if (root.name == name)
            return root;
            
        foreach (Transform child in root)
        {
            Transform result = FindTransformByName(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    // 이미지 오브젝트를 생성
    private void CreateDynamicImageObjects(Transform parent, List<string> suffixes, string variantName, string parentName)
    {
        // 기존 Image 오브젝트 찾기
        Transform existingImage = parent.Find("Image");
        
        if (existingImage != null && suffixes.Count > 1)
        {
            // Image 오브젝트가 있고 F, B 둘 다 있는 경우
            foreach (string suffix in suffixes)
            {
                Transform targetTransform = parent.Find(suffix);
                if (targetTransform == null)
                {
                    // 새로운 오브젝트 생성
                    GameObject newObj = new GameObject(suffix);
                    newObj.transform.SetParent(parent, false);
                    
                    // SpriteRenderer 컴포넌트 추가
                    SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
                    
                    // 기존 Image의 SpriteRenderer 설정 복사
                    SpriteRenderer originalRenderer = existingImage.GetComponent<SpriteRenderer>();
                    if (originalRenderer != null)
                    {
                        spriteRenderer.sortingLayerID = originalRenderer.sortingLayerID;
                        spriteRenderer.sortingOrder = originalRenderer.sortingOrder;
                        spriteRenderer.color = originalRenderer.color;
                        spriteRenderer.flipX = originalRenderer.flipX;
                        spriteRenderer.flipY = originalRenderer.flipY;
                        spriteRenderer.drawMode = originalRenderer.drawMode;
                        spriteRenderer.size = originalRenderer.size;
                    }
                    
                    // F, B 오브젝트에 대한 Sorting Order 설정 (기존 값이 0인 경우에만)
                    if (spriteRenderer.sortingOrder == 0)
                    {
                        SetSortingOrderForFBObject(spriteRenderer, suffix);
                    }
                    
                    Debug.Log($"🔧 '{parentName}'에 동적 오브젝트 '{suffix}' 생성");
                }
                else
                {
                    Debug.Log($"ℹ️ '{parentName}'에 '{suffix}' 오브젝트 이미 존재 - 중복 생성 방지");
                }
            }
            
            // 기존 Image 오브젝트를 첫 번째 suffix로 변경
            if (suffixes.Count > 0)
            {
                existingImage.name = suffixes[0];
                Debug.Log($"🔄 '{parentName}'의 Image를 '{suffixes[0]}'로 변경");
            }
        }
    }

    private void CreateDynamicVariantObjects(Transform parent, List<string> variants, string variantName, string parentName)
    {
        // Head 오브젝트의 경우 특별한 처리
        if (parentName.StartsWith("Head"))
        {
            CreateHeadObjects(parent, variants, variantName, parentName);
            return;
        }
        
        // 기존 Image 오브젝트 찾기
        Transform existingImage = parent.Find("Image");
        
        if (variants.Count > 0)
        {
            // 변형 오브젝트들이 있는 경우
            foreach (string variant in variants)
            {
                Transform targetTransform = parent.Find(variant);
                if (targetTransform == null)
                {
                    // 새로운 오브젝트 생성
                    GameObject newObj = new GameObject(variant);
                    newObj.transform.SetParent(parent, false);
                    
                    // SpriteRenderer 컴포넌트 추가
                    SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
                    
                    // 기존 Image의 SpriteRenderer 설정 복사 (있는 경우)
                    if (existingImage != null)
                    {
                        SpriteRenderer originalRenderer = existingImage.GetComponent<SpriteRenderer>();
                        if (originalRenderer != null)
                        {
                            spriteRenderer.sortingLayerID = originalRenderer.sortingLayerID;
                            spriteRenderer.sortingOrder = originalRenderer.sortingOrder;
                            spriteRenderer.color = originalRenderer.color;
                            spriteRenderer.flipX = originalRenderer.flipX;
                            spriteRenderer.flipY = originalRenderer.flipY;
                            spriteRenderer.drawMode = originalRenderer.drawMode;
                            spriteRenderer.size = originalRenderer.size;
                        }
                    }
                    
                    // F, B 오브젝트에 대한 Sorting Order 설정 (기존 값이 0인 경우에만)
                    if (spriteRenderer.sortingOrder == 0)
                    {
                        SetSortingOrderForFBObject(spriteRenderer, variant);
                    }
                    
                    Debug.Log($"🔧 '{parentName}'에 변형 오브젝트 '{variant}' 생성");
                }
                else
                {
                    Debug.Log($"ℹ️ '{parentName}'에 변형 오브젝트 '{variant}' 이미 존재 - 중복 생성 방지");
                }
            }
            
            // 기존 Image 오브젝트가 있고 변형이 여러 개인 경우 첫 번째 variant로 변경
            if (existingImage != null && variants.Count > 1)
            {
                existingImage.name = variants[0];
            }
        }
    }
    
    private void CreateHeadObjects(Transform parent, List<string> variants, string variantName, string parentName)
    {
        // Head 하위 오브젝트들을 알파벳 정렬 순서로 정렬
        var comparer = new HierarchicalNameComparer();
        variants.Sort((a, b) => comparer.Compare(a, b));
        
        foreach (string variant in variants)
        {
            Transform existingVariant = parent.Find(variant);
            if (existingVariant == null)
            {
                // 새로운 Head 변형 오브젝트 생성
                GameObject newObj = new GameObject(variant);
                newObj.transform.SetParent(parent, false);
                
                // SpriteRenderer 컴포넌트 추가
                SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
                
                Debug.Log($"🔧 '{parentName}'에 Head 변형 오브젝트 '{variant}' 생성");
                
                // 이 Head 변형에 대해 F/B 이미지가 있는지 확인하고 자식 오브젝트 생성
                CreateHeadImageObjects(newObj.transform, variantName, variant, variant);
            }
            else
            {
                Debug.Log($"ℹ️ '{parentName}'에 Head 변형 오브젝트 '{variant}' 이미 존재 (JSON에서 생성됨) - F/B 이미지 오브젝트만 확인");
                
                // 기존 오브젝트에 F/B 이미지 오브젝트가 있는지 확인하고, 없으면 생성
                EnsureHeadImageObjects(existingVariant, variantName, variant, variant);
            }
        }
    }
    
    private void CreateHeadImageObjects(Transform headTransform, string variantName, string parentName, string variant)
    {
        // 이 Head 변형에 대한 F/B 이미지가 있는지 확인
        // parentName이 "Head_0_1" 형태이므로 그대로 사용
        string headSpriteName = variantName + "_" + parentName;
        string headSpriteNameF = headSpriteName + "_F";
        string headSpriteNameB = headSpriteName + "_B";
        
        // 이미지 폴더에서 F, B 이미지가 있는지 확인
        bool hasF = HasSpriteInFolder(headSpriteNameF);
        bool hasB = HasSpriteInFolder(headSpriteNameB);
        bool hasBase = HasSpriteInFolder(headSpriteName);
        
        if (hasF && hasB)
        {
            // F, B 둘 다 있는 경우
            CreateImageObjectIfNotExists(headTransform, "F", headSpriteNameF);
            CreateImageObjectIfNotExists(headTransform, "B", headSpriteNameB);
        }
        else if (hasF || hasB)
        {
            // F 또는 B 중 하나만 있는 경우
            if (hasF)
            {
                CreateImageObjectIfNotExists(headTransform, "F", headSpriteNameF);
            }
            if (hasB)
            {
                CreateImageObjectIfNotExists(headTransform, "B", headSpriteNameB);
            }
        }
        else
        {
            // F, B 둘 다 없는 경우 기본 이미지로 F, B 오브젝트 생성
            CreateImageObjectIfNotExists(headTransform, "F", headSpriteName);
            CreateImageObjectIfNotExists(headTransform, "B", headSpriteName);
        }
    }
    
    private void EnsureHeadImageObjects(Transform headTransform, string variantName, string parentName, string variant)
    {
        // 이 Head 변형에 대한 F/B 이미지가 있는지 확인
        string headSpriteName = variantName + "_" + parentName;
        string headSpriteNameF = headSpriteName + "_F";
        string headSpriteNameB = headSpriteName + "_B";
        
        // 이미지 폴더에서 F, B 이미지가 있는지 확인
        bool hasF = HasSpriteInFolder(headSpriteNameF);
        bool hasB = HasSpriteInFolder(headSpriteNameB);
        bool hasBase = HasSpriteInFolder(headSpriteName);
        
        // F, B 오브젝트가 이미 존재하는지 확인
        Transform existingF = headTransform.Find("F");
        Transform existingB = headTransform.Find("B");
        
        Debug.Log($"🔍 '{headTransform.name}' F/B 오브젝트 확인 - F: {(existingF != null ? "존재" : "없음")}, B: {(existingB != null ? "존재" : "없음")}");
        
        if (hasF && hasB)
        {
            // F, B 둘 다 있는 경우 - 없으면 생성
            if (existingF == null)
            {
                Debug.Log($"🔧 '{headTransform.name}'에 F 오브젝트 생성 (이미지 있음)");
                CreateImageObjectIfNotExists(headTransform, "F", headSpriteNameF);
            }
            else
            {
                Debug.Log($"ℹ️ '{headTransform.name}'에 F 오브젝트 이미 존재 - 중복 생성 방지");
            }
            
            if (existingB == null)
            {
                Debug.Log($"🔧 '{headTransform.name}'에 B 오브젝트 생성 (이미지 있음)");
                CreateImageObjectIfNotExists(headTransform, "B", headSpriteNameB);
            }
            else
            {
                Debug.Log($"ℹ️ '{headTransform.name}'에 B 오브젝트 이미 존재 - 중복 생성 방지");
            }
        }
        else if (hasF || hasB)
        {
            // F 또는 B 중 하나만 있는 경우
            if (hasF && existingF == null)
            {
                Debug.Log($"🔧 '{headTransform.name}'에 F 오브젝트 생성 (F 이미지만 있음)");
                CreateImageObjectIfNotExists(headTransform, "F", headSpriteNameF);
            }
            else if (hasF)
            {
                Debug.Log($"ℹ️ '{headTransform.name}'에 F 오브젝트 이미 존재 - 중복 생성 방지");
            }
            
            if (hasB && existingB == null)
            {
                Debug.Log($"🔧 '{headTransform.name}'에 B 오브젝트 생성 (B 이미지만 있음)");
                CreateImageObjectIfNotExists(headTransform, "B", headSpriteNameB);
            }
            else if (hasB)
            {
                Debug.Log($"ℹ️ '{headTransform.name}'에 B 오브젝트 이미 존재 - 중복 생성 방지");
            }
        }
        else
        {
            // F, B 둘 다 없는 경우 기본 이미지로 F, B 오브젝트 생성 (없으면)
            if (existingF == null)
            {
                Debug.Log($"🔧 '{headTransform.name}'에 F 오브젝트 생성 (기본 이미지)");
                CreateImageObjectIfNotExists(headTransform, "F", headSpriteName);
            }
            else
            {
                Debug.Log($"ℹ️ '{headTransform.name}'에 F 오브젝트 이미 존재 - 중복 생성 방지");
            }
            
            if (existingB == null)
            {
                Debug.Log($"🔧 '{headTransform.name}'에 B 오브젝트 생성 (기본 이미지)");
                CreateImageObjectIfNotExists(headTransform, "B", headSpriteName);
            }
            else
            {
                Debug.Log($"ℹ️ '{headTransform.name}'에 B 오브젝트 이미 존재 - 중복 생성 방지");
            }
        }
    }
    
    private void CreateImageObject(Transform parent, string objectName, string spriteName)
    {
        Transform existingObject = parent.Find(objectName);
        if (existingObject == null)
        {
            GameObject newObj = new GameObject(objectName);
            newObj.transform.SetParent(parent, false);
            
            // SpriteRenderer 컴포넌트 추가
            SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
        }
        else
        {
            Debug.Log($"ℹ️ '{parent.name}'에 '{objectName}' 오브젝트 이미 존재");
        }
    }
    
    private void CreateImageObjectIfNotExists(Transform parent, string objectName, string spriteName)
    {
        Transform existingObject = parent.Find(objectName);
        if (existingObject == null)
        {
            GameObject newObj = new GameObject(objectName);
            newObj.transform.SetParent(parent, false);
            
            // SpriteRenderer 컴포넌트 추가
            SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
            
            // F, B 오브젝트에 대한 Sorting Order 설정
            SetSortingOrderForFBObject(spriteRenderer, objectName);
            
            Debug.Log($"🔧 '{parent.name}'에 '{objectName}' 오브젝트 생성 (Sorting Order: {spriteRenderer.sortingOrder})");
        }
        else
        {
            Debug.Log($"ℹ️ '{parent.name}'에 '{objectName}' 오브젝트 이미 존재 - 중복 생성 방지");
        }
    }
    
    /// <summary>
    /// F, B 오브젝트에 대한 Sorting Order를 설정합니다.
    /// F: 6, B: 1로 설정합니다.
    /// </summary>
    private void SetSortingOrderForFBObject(SpriteRenderer spriteRenderer, string objectName)
    {
        if (objectName == "F")
        {
            spriteRenderer.sortingOrder = 6;
        }
        else if (objectName == "B")
        {
            spriteRenderer.sortingOrder = 1;
        }
        // 다른 오브젝트들은 기본값(0) 유지
    }
    
    private bool HasSpriteInFolder(string spriteName)
    {
        if (imageFolder == null) return false;
        
        string folderPath = AssetDatabase.GetAssetPath(imageFolder);
        
        // 더 정확한 검색을 위해 파일명으로 직접 검색
        string[] allFiles = System.IO.Directory.GetFiles(folderPath, "*.png", System.IO.SearchOption.AllDirectories);
        bool found = false;
        
        foreach (string file in allFiles)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
            if (fileName == spriteName)
            {
                found = true;
                break;
            }
        }
        
        return found;
    }

    private void CreateDynamicVariantObjectsWithParentSearch(Transform root, string parentName, List<string> variants, string variantName)
    {
        // Face_Attack, Face_Normal 등의 경우 Face를 찾아서 그 하위에 생성
        // Head_0_2, Head_0_3 등의 경우 Head를 찾아서 그 하위에 생성
        string[] parentParts = parentName.Split('_');
        if (parentParts.Length >= 2)
        {
            // Face_Attack -> Face를 찾아서 Attack 생성
            // Head_0_2 -> Head를 찾아서 Head_0_2 생성
            string baseParentName = parentParts[0]; // "Face" 또는 "Head"
            Transform baseParent = FindTransformByName(root, baseParentName);
            
            if (baseParent != null)
            {
                // Face 하위에 Attack, Normal 등을 생성
                // Head 하위에 Head_0_2, Head_0_3 등을 생성
                foreach (string variant in variants)
                {
                    Transform existingVariant = baseParent.Find(variant);
                    if (existingVariant == null)
                    {
                        // 새로운 오브젝트 생성
                        GameObject newObj = new GameObject(variant);
                        newObj.transform.SetParent(baseParent, false);
                        
                        // SpriteRenderer 컴포넌트 추가
                        SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
                        
                        // F, B 오브젝트에 대한 Sorting Order 설정
                        SetSortingOrderForFBObject(spriteRenderer, variant);
                        
                        Debug.Log($"🔧 '{baseParentName}'에 변형 오브젝트 '{variant}' 생성 (Sorting Order: {spriteRenderer.sortingOrder})");
                    }
                    else
                    {
                        Debug.Log($"ℹ️ '{baseParentName}'에 변형 오브젝트 '{variant}' 이미 존재 - 중복 생성 방지");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"❌ 상위 부모 '{baseParentName}'를 찾을 수 없습니다.");
            }
        }
        else
        {
            // 단일 이름인 경우 (Hand, Leg, Weapon 등) 직접 생성
            // Hand_L, Hand_R, Leg_L, Leg_R, Weapon_L, Weapon_R 등을 찾아야 함
            Transform directParent = FindTransformByName(root, parentName);
            if (directParent != null)
            {
                foreach (string variant in variants)
                {
                    Transform existingVariant = directParent.Find(variant);
                    if (existingVariant == null)
                    {
                        // 새로운 오브젝트 생성
                        GameObject newObj = new GameObject(variant);
                        newObj.transform.SetParent(directParent, false);
                        
                        // SpriteRenderer 컴포넌트 추가
                        SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
                        
                        // F, B 오브젝트에 대한 Sorting Order 설정
                        SetSortingOrderForFBObject(spriteRenderer, variant);
                        
                        Debug.Log($"🔧 '{parentName}'에 변형 오브젝트 '{variant}' 생성 (Sorting Order: {spriteRenderer.sortingOrder})");
                    }
                    else
                    {
                        Debug.Log($"ℹ️ '{parentName}'에 변형 오브젝트 '{variant}' 이미 존재 - 중복 생성 방지");
                    }
                }
            }
            else
            {
                // 가능한 부모 이름들을 찾아보기
                List<string> possibleNames = new List<string>();
                
                if (parentName == "Weapon")
                {
                    // Weapon의 경우 Weapon_0, Weapon_1, Weapon_2, Weapon_3 등을 찾기
                    for (int i = 0; i < 10; i++) // 0~9까지 확인
                    {
                        possibleNames.Add(parentName + "_" + i);
                    }
                }
                else
                {
                    // Hand, Leg의 경우 _L, _R 형식
                    possibleNames.Add(parentName + "_L");
                    possibleNames.Add(parentName + "_R");
                }
                
                bool found = false;
                
                foreach (string possibleName in possibleNames)
                {
                    Transform possibleParent = FindTransformByName(root, possibleName);
                    if (possibleParent != null)
                    {
                        found = true;
                        
                        foreach (string variant in variants)
                        {
                            Transform existingVariant = possibleParent.Find(variant);
                            if (existingVariant == null)
                            {
                                // 새로운 오브젝트 생성
                                GameObject newObj = new GameObject(variant);
                                newObj.transform.SetParent(possibleParent, false);
                                
                                // SpriteRenderer 컴포넌트 추가
                                SpriteRenderer spriteRenderer = newObj.AddComponent<SpriteRenderer>();
                                
                                // F, B 오브젝트에 대한 Sorting Order 설정
                                SetSortingOrderForFBObject(spriteRenderer, variant);
                                
                                Debug.Log($"🔧 '{possibleName}'에 변형 오브젝트 '{variant}' 생성 (Sorting Order: {spriteRenderer.sortingOrder})");
                            }
                            else
                            {
                                Debug.Log($"ℹ️ '{possibleName}'에 변형 오브젝트 '{variant}' 이미 존재 - 중복 생성 방지");
                            }
                        }
                    }
                }
                
                if (!found)
                {
                    if (parentName == "Weapon")
                    {
                        Debug.LogWarning($"❌ 부모 이름 '{parentName}' 또는 '{parentName}_0'~'{parentName}_9'을 찾을 수 없습니다. (JSON에서 생성되지 않았거나 이름이 다를 수 있습니다)");
                    }
                    else
                    {
                        Debug.LogWarning($"❌ 부모 이름 '{parentName}' 또는 '{parentName}_L', '{parentName}_R'을 찾을 수 없습니다. (JSON에서 생성되지 않았거나 이름이 다를 수 있습니다)");
                    }
                }
            }
        }
    }
    
    #endregion

    #region Sprite Management
    
    private void ReplaceSprites(Transform parent, List<Sprite> sprites, string variantName)
    {
        foreach (Transform child in parent)
        {
            // Image, F, B, 그리고 동적으로 생성된 오브젝트들 (Normal, Happy, Head_0_1, Head_0_2 등) 모두 처리
            if (child.name == "Image" || child.name == "F" || child.name == "B" || 
                IsDynamicObject(child.name))
            {
                SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    string targetSpriteName = GetTargetSpriteName(child, variantName);
                    if (!string.IsNullOrEmpty(targetSpriteName))
                    {
                        Sprite targetSprite = sprites.Find(s => s.name.Equals(targetSpriteName, System.StringComparison.OrdinalIgnoreCase));
                        
                        if (targetSprite != null)
                        {
                            spriteRenderer.sprite = targetSprite;
                        }
                        else
                        {
                            // 이미지 리소스가 없는 경우 경고만 출력하고 계속 진행
                            Debug.LogWarning($"⚠️ 스프라이트 '{targetSpriteName}'을 찾을 수 없습니다. (이미지 리소스가 없거나 이름이 다를 수 있습니다)");
                        }
                    }
                }
            }
            ReplaceSprites(child, sprites, variantName);
        }
    }

    private bool IsDynamicObject(string objectName)
    {
        // 동적으로 생성된 오브젝트인지 확인 (Normal, Happy, Head_0_1, Head_0_2 등)
        return objectName.Contains("_") || 
               IsFaceExpressionName(objectName);
    }

    private string GetTargetSpriteName(Transform transform, string variantName)
    {
        // F, B 오브젝트의 경우 부모 이름 + 자신의 이름을 사용
        if (transform.name == "F" || transform.name == "B")
        {
            string spriteName = variantName + "_" + transform.parent.name + "_" + transform.name;
            return spriteName;
        }
        
        // Image 오브젝트의 경우 부모 이름을 사용
        if (transform.name == "Image")
        {
            // Face 하위의 Image인지 확인 (Face/Normal/Image 구조)
            if (IsFaceExpressionImageObject(transform))
            {
                // Face/Normal/Image -> Wi_Dorothy_6_Face_Normal
                string spriteName = variantName + "_Face_" + transform.parent.name;
                return spriteName;
            }
            else
            {
                string spriteName = variantName + "_" + transform.parent.name;
                return spriteName;
            }
        }
        
        // 동적으로 생성된 오브젝트의 경우 (Normal, Happy, Head_0_1 등)
        if (IsDynamicObject(transform.name))
        {
            // Face 하위의 표정 오브젝트인지 확인
            if (IsFaceExpressionObject(transform))
            {
                // Face/Normal -> Wi_Dorothy_6_Face_Normal
                string spriteName = variantName + "_Face_" + transform.name;
                return spriteName;
            }
            else
            {
                // Head_0_1 -> Wi_Dorothy_6_Head_0_1
                string spriteName = variantName + "_" + transform.name;
                return spriteName;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Face 오브젝트의 직접적인 자식인 표정 오브젝트인지 확인합니다.
    /// 예: Face/Normal, Face/Happy, Face/Attack 등
    /// </summary>
    private bool IsFaceExpressionObject(Transform transform)
    {
        // Face 오브젝트의 직접적인 자식인지 확인
        Transform parent = transform.parent;
        bool isFaceChild = parent != null && parent.name == "Face";
        
        // 표정 오브젝트 이름들 확인 (헬퍼 메서드 사용)
        bool isFaceExpressionName = IsFaceExpressionName(transform.name);
        
        bool result = isFaceChild && isFaceExpressionName;
        return result;
    }
    
    /// <summary>
    /// Face 표정 오브젝트 하위의 Image 오브젝트인지 확인합니다.
    /// 예: Face/Normal/Image, Face/Happy/Image, Face/Attack/Image 등
    /// </summary>
    private bool IsFaceExpressionImageObject(Transform transform)
    {
        // Image 오브젝트인지 먼저 확인
        if (transform.name != "Image") return false;
        
        // 부모 오브젝트 확인 (표정 오브젝트)
        Transform parent = transform.parent;
        if (parent == null) return false;
        
        // 할아버지 오브젝트 확인 (Face 오브젝트)
        Transform grandParent = parent.parent;
        if (grandParent == null) return false;
        
        // 표정 오브젝트 이름들 확인 (헬퍼 메서드 사용)
        bool isFaceExpressionName = IsFaceExpressionName(parent.name);
        
        // Face/Normal/Image 구조인지 확인
        bool result = grandParent.name == "Face" && isFaceExpressionName;
        return result;
    }
    
    #endregion

    #region Sprite Name Processing
    
    // 스프라이트 이름 접두사 추출
    // Image, F, B 오브젝트의 경우 부모 이름을 사용
    // 그 외의 경우 기존 로직 사용
    private string GetSpriteNamePrefix(Transform transform)
    {
        // Image, F, B 오브젝트의 경우 부모 이름을 사용
        if (transform.name == "Image" || transform.name == "F" || transform.name == "B")
        {
            return transform.parent.name;
        }
        
        // 그 외의 경우 기존 로직 사용
        string path = transform.name;
        Transform current = transform.parent;
        while (current != null && current.name != "Pivot")
        {
            path = current.name + "_" + path;
            current = current.parent;
        }
        return path;
    }
    
    #endregion

    #region Sprite Grouping and File Processing
    
    // 이미지 폴더에서 스프라이트 그룹화하고 export할 프리팹 이름을 추출 (하위 폴더 포함)
    private Dictionary<string, List<Sprite>> GroupSpritesByVariantName(string imagePath)
    {
        Dictionary<string, List<Sprite>> variantSprites = new Dictionary<string, List<Sprite>>();
        
        Debug.Log($"🔍 GroupSpritesByVariantName 시작 - 경로: {imagePath}");
        
        // 하위 폴더까지 재귀적으로 모든 이미지 파일 검색
        string[] spritePaths = Directory.GetFiles(imagePath, "*.*", SearchOption.AllDirectories);
        Debug.Log($"🔍 발견된 파일 개수: {spritePaths.Length}");

        if (spritePaths.Length == 0)
        {
            Debug.LogWarning("지정된 이미지 폴더에 파일이 없습니다.");
            return variantSprites;
        }
        
        int processedCount = 0;
        int supportedCount = 0;
        int validFormatCount = 0;
        
        foreach (string path in spritePaths)
        {
            processedCount++;
            string assetPath = path.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            
            if (IsSupportedImageFile(assetPath))
            {
                supportedCount++;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    string[] parts = fileName.Split('_');  // 스프라이트 이름에서 부모 오브젝트 이름들을 추출
                    
                    if (parts.Length >= 3)
                    {
                        validFormatCount++;
                        // 첫 3개 부분을 베리언트 이름으로 사용 (예: "Wa_Leon_4")
                        string variantName = parts[0] + "_" + parts[1] + "_" + parts[2];
                        if (!variantSprites.ContainsKey(variantName))
                        {
                            variantSprites[variantName] = new List<Sprite>();
                        }
                        variantSprites[variantName].Add(sprite);
                        Debug.Log($"✅ 처리됨: {fileName} -> 베리언트: {variantName}");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ 파일명 '{fileName}'이 예상 형식과 다릅니다. (최소 3개의 '_' 구분자 필요, 현재: {parts.Length}개)");
                    }
                }
                else
                {
                    Debug.LogWarning($"⚠️ 스프라이트를 로드할 수 없습니다: {assetPath}");
                }
            }
        }
        
        Debug.Log($"🔍 파일 처리 결과 - 전체: {processedCount}, 지원형식: {supportedCount}, 유효형식: {validFormatCount}, 최종베리언트: {variantSprites.Count}");
        
        return variantSprites;
    }
    
    #endregion

    #region Utility Methods
    
    // 계층 구조 경로 추출
    private string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    // 비어있는 Sprite Renderer 오브젝트와 부모 오브젝트 정리
    private void CleanupEmptySpriteRenderers(Transform root)
    {
        
        // 모든 Transform을 수집 (하위부터 상위로 정렬)
        List<Transform> allTransforms = new List<Transform>();
        CollectAllTransforms(root, allTransforms);
        
        // 하위부터 상위로 정렬하여 부모가 먼저 삭제되는 것을 방지
        allTransforms.Sort((a, b) => b.GetSiblingIndex().CompareTo(a.GetSiblingIndex()));
        
        int removedCount = 0;
        
        foreach (Transform transform in allTransforms)
        {
            if (transform == null) continue; // 이미 삭제된 경우
            
            SpriteRenderer spriteRenderer = transform.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite == null)
            {
                // Sprite Renderer가 있지만 이미지가 없는 경우
                // 부모 오브젝트도 확인
                Transform parent = transform.parent;
                if (parent != null && parent != root)
                {
                    // 부모가 자식이 하나뿐이고, 그 자식이 현재 삭제할 오브젝트인 경우
                    if (parent.childCount == 1)
                    {
                        UnityEngine.Object.DestroyImmediate(parent.gameObject);
                        removedCount++;
                    }
                    else
                    {
                        // 부모에 다른 자식이 있는 경우 현재 오브젝트만 삭제
                        UnityEngine.Object.DestroyImmediate(transform.gameObject);
                        removedCount++;
                    }
                }
                else
                {
                    // 부모가 없거나 루트인 경우 현재 오브젝트만 삭제
                    UnityEngine.Object.DestroyImmediate(transform.gameObject);
                    removedCount++;
                }
            }
        }
        
    }
    
    // 모든 Transform을 수집
    private void CollectAllTransforms(Transform root, List<Transform> transforms)
    {
        transforms.Add(root);
        foreach (Transform child in root)
        {
            CollectAllTransforms(child, transforms);
        }
    }
    
    #endregion
}