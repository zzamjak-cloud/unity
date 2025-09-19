using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;


public class PrefabGeneratorTool : EditorWindow
{
    private GameObject baseModelPrefab;
    private TextAsset referenceJSON;
    private DefaultAsset imageFolder;

    [MenuItem("CAT/Utility/Setup Unit")]
    public static void ShowWindow()
    {
        GetWindow<PrefabGeneratorTool>("Hierarchy Setup Unit");
    }

    private void OnGUI()
    {
        GUILayout.Label("Hierarchy Setup Unit", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        baseModelPrefab = (GameObject)EditorGUILayout.ObjectField("Base Model Prefab", baseModelPrefab, typeof(GameObject), false);
        referenceJSON = (TextAsset)EditorGUILayout.ObjectField("Reference JSON", referenceJSON, typeof(TextAsset), false);
        imageFolder = (DefaultAsset)EditorGUILayout.ObjectField("Image Folder", imageFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Setup Hierarchy", GUILayout.Height(30)))
        {
            SetupHierarchyFromJSON();
        }
    }

    private void SetupHierarchyFromJSON()
    {
        if (baseModelPrefab == null || referenceJSON == null || imageFolder == null)
        {
            Debug.LogError("모든 필드를 지정해주세요.");
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

        string imagePath = AssetDatabase.GetAssetPath(imageFolder);
        var variantSprites = GroupSpritesByVariantName(imagePath);

        Debug.Log($"총 {variantSprites.Count}개의 베리언트를 처리합니다.");
        
        if (variantSprites.Count == 0)
        {
            Debug.LogError("처리할 베리언트가 없습니다. 이미지 폴더를 확인해주세요.");
            return;
        }

        foreach (var entry in variantSprites)
        {
            string variantName = entry.Key;
            List<Sprite> spritesForVariant = entry.Value;
            
            Debug.Log($"🔄 베리언트 '{variantName}' 설정 시작 (스프라이트 {spritesForVariant.Count}개)");

            // 1. 기본 모델을 하이어라키에 로드
            GameObject baseModelInstance = PrefabUtility.InstantiatePrefab(baseModelPrefab) as GameObject;
            if (baseModelInstance == null)
            {
                Debug.LogError("기본 모델 프리팹을 로드하는 데 실패했습니다.");
                continue;
            }
            
            // 2. 하이어라키에 로드한 프리팹 이름을 텍스처 이름 기반으로 변경
            baseModelInstance.name = variantName;
            
            // 3. Pivot 오브젝트 찾기 (프리팹 상태 유지)
            Transform basePivot = FindPivotTransform(baseModelInstance.transform);

            if (basePivot == null)
            {
                Debug.LogError("Pivot 오브젝트를 찾을 수 없습니다. 프리팹 구조를 확인해주세요.");
                GameObject.DestroyImmediate(baseModelInstance);
                continue;
            }
            
            Debug.Log($"📋 기본 모델 Pivot 하위 오브젝트 수: {basePivot.childCount}");
            
            // 4. JSON 데이터를 기반으로 게임오브젝트 생성
            CreateGameObjectsFromJSON(basePivot, hierarchyData);

            // 5. 이미지 리소스에 맞춰 오브젝트 구조 동적 생성
            CreateDynamicObjectsFromSprites(baseModelInstance.transform, spritesForVariant, variantName);

            // 6. 이미지 교체
            ReplaceSprites(baseModelInstance.transform, spritesForVariant, variantName);

            // 7. Pivot 하위 오브젝트들을 알파벳 순서로 정렬
            SortPivotChildrenAlphabetically(basePivot);
            
            // 8. Head 하위 오브젝트들도 개별적으로 정렬
            SortHeadChildrenAlphabetically(basePivot);

            Debug.Log($"✅ 베리언트 '{variantName}' 설정이 완료되었습니다. 하이어라키에서 확인하세요.");
        }

        Debug.Log("🎉 모든 베리언트 설정이 완료되었습니다.");
    }

    private void SortPivotChildrenAlphabetically(Transform pivot)
    {
        Debug.Log($"🔤 Pivot 하위 오브젝트들을 알파벳 순서로 정렬 시작");
        
        // 자식들을 리스트로 수집
        List<Transform> children = new List<Transform>();
        foreach (Transform child in pivot)
        {
            children.Add(child);
        }
        
        // 알파벳 순서로 정렬 (숫자도 고려)
        children.Sort((a, b) => CompareNamesWithNumbers(a.name, b.name));
        
        // 정렬된 순서대로 다시 배치
        for (int i = 0; i < children.Count; i++)
        {
            children[i].SetSiblingIndex(i);
        }
        
        Debug.Log($"✅ Pivot 하위 오브젝트 {children.Count}개를 알파벳 순서로 정렬 완료");
    }
    
    private void SortHeadChildrenAlphabetically(Transform pivot)
    {
        Debug.Log($"🔤 Head 하위 오브젝트들을 하드코딩 순서로 정렬 시작");
        
        // Head 오브젝트 찾기
        Transform headTransform = pivot.Find("Head");
        if (headTransform == null)
        {
            Debug.Log("ℹ️ Head 오브젝트를 찾을 수 없습니다.");
            return;
        }
        
        // Head의 자식들을 리스트로 수집
        List<Transform> headChildren = new List<Transform>();
        foreach (Transform child in headTransform)
        {
            headChildren.Add(child);
        }
        
        // 하드코딩된 정렬 순서
        string[] sortOrder = { "Face", "Head_0_1", "Head_0_2", "Head_0_3", "Head_1_1", "Head_1_2", "Head_1_3", "Head_2_1", "Head_2_2", "Head_2_3" };
        
        // 정렬된 순서대로 다시 배치
        for (int i = 0; i < sortOrder.Length; i++)
        {
            Transform child = headChildren.Find(c => c.name == sortOrder[i]);
            if (child != null)
            {
                child.SetSiblingIndex(i);
                Debug.Log($"🔧 '{child.name}'을 인덱스 {i}로 이동");
            }
        }
        
        Debug.Log($"✅ Head 하위 오브젝트 {headChildren.Count}개를 하드코딩 순서로 정렬 완료");
    }
    
    private int CompareNamesWithNumbers(string nameA, string nameB)
    {
        // Head_0_1, Head_0_2, Head_1_1 등의 형식을 고려한 정렬
        string[] partsA = nameA.Split('_');
        string[] partsB = nameB.Split('_');
        
        // 먼저 첫 번째 부분으로 비교
        int firstCompare = partsA[0].CompareTo(partsB[0]);
        if (firstCompare != 0) return firstCompare;
        
        // 두 번째 부분이 숫자인 경우 숫자로 비교
        if (partsA.Length > 1 && partsB.Length > 1)
        {
            if (int.TryParse(partsA[1], out int numA) && int.TryParse(partsB[1], out int numB))
            {
                int numCompare = numA.CompareTo(numB);
                if (numCompare != 0) return numCompare;
            }
            else
            {
                int secondCompare = partsA[1].CompareTo(partsB[1]);
                if (secondCompare != 0) return secondCompare;
            }
        }
        
        // 세 번째 부분이 숫자인 경우 숫자로 비교
        if (partsA.Length > 2 && partsB.Length > 2)
        {
            if (int.TryParse(partsA[2], out int numA) && int.TryParse(partsB[2], out int numB))
            {
                return numA.CompareTo(numB);
            }
            else
            {
                return partsA[2].CompareTo(partsB[2]);
            }
        }
        
        // 기본 알파벳 비교
        return nameA.CompareTo(nameB);
    }

    private Transform FindPivotTransform(Transform root)
    {
        // 현재 Transform이 "Pivot"인지 확인
        if (root.name == "Pivot")
        {
            return root;
        }

        // 자식들에서 재귀적으로 검색
        foreach (Transform child in root)
        {
            Transform found = FindPivotTransform(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }


    private void CreateGameObjectsFromJSON(Transform basePivot, HierarchyData hierarchyData)
    {
        Debug.Log($"🔄 JSON 데이터를 기반으로 게임오브젝트 생성 시작");
        
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
        
        Debug.Log($"📋 JSON 기반 생성 완료 후 기본 모델 Pivot 하위 오브젝트 수: {basePivot.childCount}");
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
                Debug.LogWarning($"⚠️ 부모 오브젝트 '{data.parentPath}'를 찾을 수 없습니다. 기본 Pivot에 생성합니다.");
                parentTransform = basePivot;
            }
        }
        
        // 이미 존재하는지 확인 (기본 모델의 Pivot은 비어있으므로 일반적으로 존재하지 않음)
        Transform existingTransform = parentTransform.Find(data.name);
        if (existingTransform != null)
        {
            Debug.Log($"ℹ️ '{data.name}' 오브젝트가 이미 존재합니다. Transform 정보를 업데이트합니다.");
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
        
        Debug.Log($"✨ '{data.name}' 오브젝트를 생성했습니다. (부모: {parentTransform.name}, 경로: {data.path})");
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
                    Debug.Log($"🔧 '{transform.name}'에 '{componentInfo.componentName}' 컴포넌트를 추가했습니다.");
                }
                
                // SpriteRenderer의 경우 SortingOrder 적용
                if (componentInfo.componentName == "SpriteRenderer" && existingComponent is SpriteRenderer spriteRenderer)
                {
                    spriteRenderer.sortingOrder = componentInfo.sortingOrder;
                    Debug.Log($"🎨 '{transform.name}'의 SpriteRenderer SortingOrder를 {componentInfo.sortingOrder}로 설정했습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ 컴포넌트 타입 '{componentInfo.componentType}'을 찾을 수 없습니다.");
            }
        }
    }


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
                        if (!parentToSuffixes.ContainsKey(parentName))
                        {
                            parentToSuffixes[parentName] = new List<string>();
                        }
                        if (!parentToSuffixes[parentName].Contains(lastPart))
                        {
                            parentToSuffixes[parentName].Add(lastPart);
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
                            Debug.Log($"ℹ️ 단일 부분 이름 '{parts[0]}'은 변형 오브젝트 생성이 필요하지 않습니다.");
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
            
            Debug.Log($"🔍 Head 오브젝트 생성 시도: 부모='{parentName}', 변형들={string.Join(", ", variants)}");
            
            Transform parentTransform = FindTransformByName(parent, parentName);
            if (parentTransform != null)
            {
                Debug.Log($"✅ Head 부모 오브젝트 '{parentName}' 찾음, Head 오브젝트들 생성");
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
            
            Debug.Log($"🔍 변형 오브젝트 생성 시도: 부모='{parentName}', 변형들={string.Join(", ", variants)}");
            
            Transform parentTransform = FindTransformByName(parent, parentName);
            if (parentTransform != null)
            {
                Debug.Log($"✅ 부모 오브젝트 '{parentName}' 찾음, 직접 생성");
                CreateDynamicVariantObjects(parentTransform, variants, variantName, parentName);
            }
            else
            {
                Debug.Log($"❌ 부모 오브젝트 '{parentName}' 찾을 수 없음, 상위 부모에서 검색");
                // 부모 오브젝트를 찾을 수 없는 경우, 상위 부모에서 찾아서 생성
                CreateDynamicVariantObjectsWithParentSearch(parent, parentName, variants, variantName);
            }
        }
    }

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
                    
                    Debug.Log($"🔧 '{parentName}'에 동적 오브젝트 '{suffix}' 생성");
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
        Debug.Log($"🔧 CreateDynamicVariantObjects 호출: 부모='{parentName}', 변형들={string.Join(", ", variants)}");
        
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
                    
                    Debug.Log($"🔧 '{parentName}'에 변형 오브젝트 '{variant}' 생성");
                }
                else
                {
                    Debug.Log($"ℹ️ '{parentName}'에 변형 오브젝트 '{variant}' 이미 존재");
                }
            }
            
            // 기존 Image 오브젝트가 있고 변형이 여러 개인 경우 첫 번째 variant로 변경
            if (existingImage != null && variants.Count > 1)
            {
                existingImage.name = variants[0];
                Debug.Log($"🔄 '{parentName}'의 Image를 '{variants[0]}'로 변경");
            }
        }
    }
    
    private void CreateHeadObjects(Transform parent, List<string> variants, string variantName, string parentName)
    {
        Debug.Log($"🔧 Head 오브젝트 생성: '{parentName}' 하위에 {variants.Count}개 변형 생성");
        Debug.Log($"🔧 Head 변형들: {string.Join(", ", variants)}");
        
        // Head 하위 오브젝트들을 알파벳 정렬 순서로 정렬
        variants.Sort((a, b) => CompareNamesWithNumbers(a, b));
        
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
                Debug.Log($"ℹ️ '{parentName}'에 Head 변형 오브젝트 '{variant}' 이미 존재");
                
                // 기존 오브젝트에 대해서도 F/B 이미지 확인
                CreateHeadImageObjects(existingVariant, variantName, variant, variant);
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
        
        Debug.Log($"🔍 Head 이미지 확인: '{headSpriteName}' (variant: {variant}, parentName: {parentName})");
        
        // 이미지 폴더에서 F, B 이미지가 있는지 확인
        bool hasF = HasSpriteInFolder(headSpriteNameF);
        bool hasB = HasSpriteInFolder(headSpriteNameB);
        bool hasBase = HasSpriteInFolder(headSpriteName);
        
        Debug.Log($"🔍 Head 이미지 확인: '{headSpriteName}' -> Base: {hasBase}, F: {hasF}, B: {hasB}");
        
        if (hasF && hasB)
        {
            // F, B 둘 다 있는 경우
            CreateImageObject(headTransform, "F", headSpriteNameF);
            CreateImageObject(headTransform, "B", headSpriteNameB);
            Debug.Log($"🔧 Head '{variant}'에 F, B 오브젝트 생성");
        }
        else if (hasF || hasB)
        {
            // F 또는 B 중 하나만 있는 경우
            if (hasF)
            {
                CreateImageObject(headTransform, "F", headSpriteNameF);
                Debug.Log($"🔧 Head '{variant}'에 F 오브젝트 생성");
            }
            if (hasB)
            {
                CreateImageObject(headTransform, "B", headSpriteNameB);
                Debug.Log($"🔧 Head '{variant}'에 B 오브젝트 생성");
            }
        }
        else
        {
            // F, B 둘 다 없는 경우 기본 이미지로 F, B 오브젝트 생성
            CreateImageObject(headTransform, "F", headSpriteName);
            CreateImageObject(headTransform, "B", headSpriteName);
            Debug.Log($"🔧 Head '{variant}'에 F, B 오브젝트 생성 (기본 이미지: {headSpriteName})");
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
            
            Debug.Log($"🔧 '{parent.name}'에 '{objectName}' 오브젝트 생성 (스프라이트: {spriteName})");
        }
        else
        {
            Debug.Log($"ℹ️ '{parent.name}'에 '{objectName}' 오브젝트 이미 존재");
        }
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
        
        Debug.Log($"🔍 HasSpriteInFolder: '{spriteName}' -> {found} (폴더: {folderPath})");
        
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
                Debug.Log($"✅ 상위 부모 '{baseParentName}' 찾음, 변형 오브젝트들 생성");
                
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
                        
                        Debug.Log($"🔧 '{baseParentName}'에 변형 오브젝트 '{variant}' 생성");
                    }
                    else
                    {
                        Debug.Log($"ℹ️ '{baseParentName}'에 변형 오브젝트 '{variant}' 이미 존재");
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
                Debug.Log($"✅ 직접 부모 '{parentName}' 찾음, 변형 오브젝트들 생성");
                
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
                        
                        Debug.Log($"🔧 '{parentName}'에 변형 오브젝트 '{variant}' 생성");
                    }
                    else
                    {
                        Debug.Log($"ℹ️ '{parentName}'에 변형 오브젝트 '{variant}' 이미 존재");
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
                        Debug.Log($"✅ 가능한 부모 '{possibleName}' 찾음, 변형 오브젝트들 생성");
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
                                
                                Debug.Log($"🔧 '{possibleName}'에 변형 오브젝트 '{variant}' 생성");
                            }
                            else
                            {
                                Debug.Log($"ℹ️ '{possibleName}'에 변형 오브젝트 '{variant}' 이미 존재");
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
                            Debug.Log($"🎨 '{GetHierarchyPath(child)}'에 스프라이트 '{targetSprite.name}' 적용");
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
               objectName == "Normal" || objectName == "Happy" || objectName == "Attack" || 
               objectName == "Blank" || objectName == "Sad" || objectName == "Angry";
    }

    private string GetTargetSpriteName(Transform transform, string variantName)
    {
        // F, B 오브젝트의 경우 부모 이름 + 자신의 이름을 사용
        if (transform.name == "F" || transform.name == "B")
        {
            string spriteName = variantName + "_" + transform.parent.name + "_" + transform.name;
            Debug.Log($"🔍 F/B 오브젝트 '{transform.name}' -> 스프라이트 이름: '{spriteName}'");
            return spriteName;
        }
        
        // Image 오브젝트의 경우 부모 이름을 사용
        if (transform.name == "Image")
        {
            // Face 하위의 Image인지 확인 (Face/Normal/Image 구조)
            if (IsFaceExpressionImage(transform))
            {
                // Face/Normal/Image -> Wi_Dorothy_6_Face_Normal
                string spriteName = variantName + "_Face_" + transform.parent.name;
                Debug.Log($"🔍 Face/Image 오브젝트 '{transform.name}' (부모: {transform.parent.name}) -> 스프라이트 이름: '{spriteName}'");
                return spriteName;
            }
            else
            {
                string spriteName = variantName + "_" + transform.parent.name;
                Debug.Log($"🔍 일반 Image 오브젝트 '{transform.name}' (부모: {transform.parent.name}) -> 스프라이트 이름: '{spriteName}'");
                return spriteName;
            }
        }
        
        // 동적으로 생성된 오브젝트의 경우 (Normal, Happy, Head_0_1 등)
        if (IsDynamicObject(transform.name))
        {
            // Face 하위의 표정 오브젝트인지 확인
            if (IsFaceExpression(transform))
            {
                // Face/Normal -> Wi_Dorothy_6_Face_Normal
                string spriteName = variantName + "_Face_" + transform.name;
                Debug.Log($"🔍 Face 표정 오브젝트 '{transform.name}' -> 스프라이트 이름: '{spriteName}'");
                return spriteName;
            }
            else
            {
                // Head_0_1 -> Wi_Dorothy_6_Head_0_1
                string spriteName = variantName + "_" + transform.name;
                Debug.Log($"🔍 동적 오브젝트 '{transform.name}' -> 스프라이트 이름: '{spriteName}'");
                return spriteName;
            }
        }
        
        return null;
    }

    private bool IsFaceExpression(Transform transform)
    {
        // Face 오브젝트의 직접적인 자식인지 확인
        Transform parent = transform.parent;
        bool isFaceExpression = parent != null && parent.name == "Face";
        
        // 표정 오브젝트 이름들도 확인
        string[] faceExpressionNames = { "Normal", "Happy", "Attack", "Blank", "Sad", "Angry" };
        bool isFaceExpressionName = System.Array.Exists(faceExpressionNames, name => name == transform.name);
        
        bool result = isFaceExpression && isFaceExpressionName;
        Debug.Log($"🔍 IsFaceExpression 체크: '{transform.name}' (부모: {parent?.name}, Face자식: {isFaceExpression}, 표정이름: {isFaceExpressionName}) -> {result}");
        return result;
    }
    
    private bool IsFaceExpressionImage(Transform transform)
    {
        // Face/Normal/Image 구조에서 Image 오브젝트인지 확인
        if (transform.name != "Image") return false;
        
        Transform parent = transform.parent;
        if (parent == null) return false;
        
        Transform grandParent = parent.parent;
        if (grandParent == null) return false;
        
        // 표정 오브젝트 이름들 확인
        string[] faceExpressionNames = { "Normal", "Happy", "Attack", "Blank", "Sad", "Angry" };
        bool isFaceExpressionName = System.Array.Exists(faceExpressionNames, name => name == parent.name);
        
        bool result = grandParent.name == "Face" && isFaceExpressionName;
        Debug.Log($"🔍 IsFaceExpressionImage 체크: '{transform.name}' (부모: {parent?.name}, 할아버지: {grandParent?.name}, 표정이름: {isFaceExpressionName}) -> {result}");
        return result;
    }
    
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

    private Dictionary<string, List<Sprite>> GroupSpritesByVariantName(string imagePath)
    {
        Dictionary<string, List<Sprite>> variantSprites = new Dictionary<string, List<Sprite>>();
        string[] spritePaths = Directory.GetFiles(imagePath, "*.*", SearchOption.AllDirectories);

        if (spritePaths.Length == 0)
        {
            Debug.LogWarning("지정된 이미지 폴더에 파일이 없습니다.");
            return variantSprites;
        }

        foreach (string path in spritePaths)
        {
            string assetPath = path.Replace("\\", "/").Replace(Application.dataPath, "Assets");
            if (assetPath.EndsWith(".png") || assetPath.EndsWith(".jpg"))
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    string[] parts = fileName.Split('_');
                    if (parts.Length >= 3)
                    {
                        // 첫 3개 부분을 베리언트 이름으로 사용 (예: "Wa_Leon_4")
                        string variantName = parts[0] + "_" + parts[1] + "_" + parts[2];
                        if (!variantSprites.ContainsKey(variantName))
                        {
                            variantSprites[variantName] = new List<Sprite>();
                        }
                        variantSprites[variantName].Add(sprite);
                        Debug.Log($"📁 스프라이트 '{fileName}'을 베리언트 '{variantName}'에 추가");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ 파일명 '{fileName}'이 예상 형식과 다릅니다. (최소 3개의 '_' 구분자 필요)");
                    }
                }
            }
        }
        return variantSprites;
    }

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
}