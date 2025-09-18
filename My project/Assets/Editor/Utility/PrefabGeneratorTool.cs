using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class PrefabGeneratorTool : EditorWindow
{
    private GameObject baseModelPrefab;
    private GameObject referencePrefab;
    private DefaultAsset imageFolder;
    private DefaultAsset outputFolder;

    [MenuItem("Tools/Generate Prefab From Hierarchy")]
    public static void ShowWindow()
    {
        GetWindow<PrefabGeneratorTool>("Prefab Generator Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Prefab Generator Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        baseModelPrefab = (GameObject)EditorGUILayout.ObjectField("Base Model Prefab", baseModelPrefab, typeof(GameObject), false);
        referencePrefab = (GameObject)EditorGUILayout.ObjectField("Reference Prefab", referencePrefab, typeof(GameObject), false);
        imageFolder = (DefaultAsset)EditorGUILayout.ObjectField("Image Folder", imageFolder, typeof(DefaultAsset), false);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Generate Prefab", GUILayout.Height(30)))
        {
            GeneratePrefabFromHierarchy();
        }
    }

    private void GeneratePrefabFromHierarchy()
    {
        if (baseModelPrefab == null || referencePrefab == null || imageFolder == null || outputFolder == null)
        {
            Debug.LogError("모든 필드를 지정해주세요.");
            return;
        }

        string imagePath = AssetDatabase.GetAssetPath(imageFolder);
        string outputPath = AssetDatabase.GetAssetPath(outputFolder);
        var variantSprites = GroupSpritesByVariantName(imagePath);

        Debug.Log($"총 {variantSprites.Count}개의 베리언트 프리팹을 생성합니다.");
        
        if (variantSprites.Count == 0)
        {
            Debug.LogError("생성할 베리언트가 없습니다. 이미지 폴더를 확인해주세요.");
            return;
        }

        foreach (var entry in variantSprites)
        {
            string variantName = entry.Key;
            List<Sprite> spritesForVariant = entry.Value;
            
            Debug.Log($"🔄 베리언트 '{variantName}' 생성 시작 (스프라이트 {spritesForVariant.Count}개)");

            // 1. 기본 모델을 먼저 로드
            GameObject baseModelInstance = PrefabUtility.InstantiatePrefab(baseModelPrefab) as GameObject;
            if (baseModelInstance == null)
            {
                Debug.LogError("기본 모델 프리팹을 로드하는 데 실패했습니다.");
                continue;
            }
            
            // 2. 참조 베리언트를 로드
            GameObject referenceInstance = PrefabUtility.InstantiatePrefab(referencePrefab) as GameObject;
            if (referenceInstance == null)
            {
                Debug.LogError("참조 베리언트 프리팹을 로드하는 데 실패했습니다.");
                GameObject.DestroyImmediate(baseModelInstance);
                continue;
            }
            
            // 3. 프리팹 인스턴스들을 완전히 언팩
            PrefabUtility.UnpackPrefabInstance(baseModelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            PrefabUtility.UnpackPrefabInstance(referenceInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 4. Pivot 하위 구조 찾기
            Transform basePivot = baseModelInstance.transform.Find("Pivot/Animator/Pivot");
            Transform referencePivot = referenceInstance.transform.Find("Pivot/Animator/Pivot");

            if (basePivot == null || referencePivot == null)
            {
                Debug.LogError("하위 경로 'Pivot/Animator/Pivot'를 찾을 수 없습니다. 프리팹 구조를 확인해주세요.");
                GameObject.DestroyImmediate(baseModelInstance);
                GameObject.DestroyImmediate(referenceInstance);
                continue;
            }
            
            Debug.Log($"📋 기본 모델 Pivot 하위 오브젝트 수: {basePivot.childCount}");
            Debug.Log($"📋 참조 베리언트 Pivot 하위 오브젝트 수: {referencePivot.childCount}");
            
            // 5. 참조 베리언트의 Pivot 하위 오브젝트들을 기본 모델에 복제
            CopyReferenceHierarchy(basePivot, referencePivot);

            // 6. 이미지 리소스에 맞춰 오브젝트 구조 동적 생성
            CreateDynamicObjectsFromSprites(baseModelInstance.transform, spritesForVariant, variantName);

            // 7. 이미지 교체
            ReplaceSprites(baseModelInstance.transform, spritesForVariant, variantName);

            // 8. 베리언트 프리팹으로 저장
            string newPrefabPath = Path.Combine(outputPath, variantName + ".prefab").Replace("\\", "/");
            
            // 베리언트 프리팹 생성
            GameObject variantPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                baseModelInstance, 
                newPrefabPath, 
                InteractionMode.AutomatedAction, 
                out bool success
            );
            
            if (success && variantPrefab != null)
            {
                // 베리언트 프리팹인지 확인
                PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(variantPrefab);
                if (prefabType == PrefabAssetType.Variant)
                {
                    Debug.Log($"✅ 베리언트 프리팹 '{variantName}'이 성공적으로 생성되었습니다. (타입: {prefabType})");
                }
                else
                {
                    Debug.LogWarning($"⚠️ 프리팹 '{variantName}'이 생성되었지만 베리언트가 아닙니다. (타입: {prefabType})");
                }
            }
            else
            {
                Debug.LogError($"❌ 베리언트 프리팹 '{variantName}' 생성에 실패했습니다.");
            }

            Debug.Log($"✅ 베리언트 프리팹 '{variantName}'이 생성되었습니다.");

            GameObject.DestroyImmediate(baseModelInstance);
            GameObject.DestroyImmediate(referenceInstance);
        }

        Debug.Log("🎉 모든 베리언트 생성이 완료되었습니다.");
    }

    private void CopyReferenceHierarchy(Transform basePivot, Transform referencePivot)
    {
        Debug.Log($"🔄 참조 베리언트의 Pivot 하위 오브젝트들을 기본 모델에 복제 시작");
        
        // 참조 베리언트의 모든 자식 오브젝트들을 기본 모델에 복제
        foreach (Transform referenceChild in referencePivot)
        {
            // 기본 모델에 같은 이름의 오브젝트가 있는지 확인
            Transform existingChild = basePivot.Find(referenceChild.name);
            
            if (existingChild == null)
            {
                // 존재하지 않으면 복제
                GameObject newChild = Instantiate(referenceChild.gameObject);
                newChild.transform.SetParent(basePivot, false);
                newChild.name = referenceChild.name;
                
                Debug.Log($"✨ '{referenceChild.name}' 오브젝트를 기본 모델에 복제했습니다.");
            }
            else
            {
                // 이미 존재하면 하위 구조만 복제
                Debug.Log($"ℹ️ '{referenceChild.name}' 오브젝트가 이미 존재합니다. 하위 구조를 복제합니다.");
                CopyChildHierarchy(existingChild, referenceChild);
            }
        }
        
        Debug.Log($"📋 복제 완료 후 기본 모델 Pivot 하위 오브젝트 수: {basePivot.childCount}");
    }

    private void CopyChildHierarchy(Transform baseChild, Transform referenceChild)
    {
        // 참조 오브젝트의 모든 자식을 기본 오브젝트에 복제
        foreach (Transform refSubChild in referenceChild)
        {
            Transform existingSubChild = baseChild.Find(refSubChild.name);
            
            if (existingSubChild == null)
            {
                GameObject newSubChild = Instantiate(refSubChild.gameObject);
                newSubChild.transform.SetParent(baseChild, false);
                newSubChild.name = refSubChild.name;
                
                Debug.Log($"✨ '{baseChild.name}' 하위에 '{refSubChild.name}' 오브젝트를 복제했습니다.");
            }
            else
            {
                // 재귀적으로 하위 구조 복제
                CopyChildHierarchy(existingSubChild, refSubChild);
            }
        }
    }
    
    private void AddMissingChildren(Transform baseParent, Transform referenceParent)
    {
        Dictionary<string, Transform> baseChildrenMap = new Dictionary<string, Transform>();
        foreach (Transform child in baseParent)
        {
            baseChildrenMap[child.name] = child;
        }

        foreach (Transform referenceChild in referenceParent)
        {
            if (baseChildrenMap.ContainsKey(referenceChild.name))
            {
                // 이미 존재하는 자식이면 재귀적으로 하위 자식들을 확인
                AddMissingChildren(baseChildrenMap[referenceChild.name], referenceChild);
            }
            else
            {
                // 새로운 자식을 복사하여 추가
                GameObject newChild = Instantiate(referenceChild.gameObject);
                if (newChild != null)
                {
                    newChild.transform.SetParent(baseParent, false);
                    newChild.name = referenceChild.name; // 이름 유지
                    Debug.Log($"✨ '{referenceChild.name}' 오브젝트를 기본 모델에 추가했습니다.");
                    
                    // 복사된 자식의 하위 구조도 재귀적으로 복사
                    AddMissingChildren(newChild.transform, referenceChild);
                }
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
                        if (parts.Length >= 2)
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

        // 각 부모 오브젝트에 대해 변형 오브젝트 생성 (Head_0_1, Head_0_2, Normal, Happy 등)
        foreach (var entry in parentToVariants)
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
            Debug.LogWarning($"❌ 부모 이름 '{parentName}'이 예상 형식과 다릅니다.");
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
                            Debug.LogWarning($"⚠️ 스프라이트 '{targetSpriteName}'을 찾을 수 없습니다.");
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
            return variantName + "_" + transform.parent.name + "_" + transform.name;
        }
        
        // Image 오브젝트의 경우 부모 이름을 사용
        if (transform.name == "Image")
        {
            // Face 하위의 Image인지 확인
            if (IsFaceExpression(transform))
            {
                return variantName + "_Face_" + transform.parent.name;
            }
            else
            {
                return variantName + "_" + transform.parent.name;
            }
        }
        
        // 동적으로 생성된 오브젝트의 경우 (Normal, Happy, Head_0_1 등)
        if (IsDynamicObject(transform.name))
        {
            // Face 하위의 표정 오브젝트인지 확인
            if (IsFaceExpression(transform))
            {
                // Face/Normal -> Wa_Leon_4_Face_Normal
                return variantName + "_Face_" + transform.name;
            }
            else
            {
                // Head_0_1 -> Wa_Leon_4_Head_0_1
                return variantName + "_" + transform.name;
            }
        }
        
        return null;
    }

    private bool IsFaceExpression(Transform transform)
    {
        // Face 오브젝트의 직접적인 자식인지 확인
        Transform parent = transform.parent;
        return parent != null && parent.name == "Face";
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