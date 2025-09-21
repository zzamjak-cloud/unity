using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ComponentInfo
{
    public string componentName;
    public string componentType;
    public int sortingOrder;
}

[System.Serializable]
public class GameObjectData
{
    public string name;
    public string path;
    public string parentPath;
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ;
    public float scaleX, scaleY, scaleZ;
    public ComponentInfo[] components;
}

[System.Serializable]
public class HierarchyData
{
    public string prefabName;
    public string exportDate;
    public GameObjectData[] gameObjects;
}

/// <summary>
/// JSONExportWindow 클래스는 프리팹의 하이어라키를 추출하여 JSON 파일로 저장하는 툴입니다.
/// 프리팹의 Pivot 오브젝트의 하위 오브젝트들의 정보를 추출하여 JSON 파일로 저장합니다.
/// 저장된 JSON 파일은 프로젝트 내의 지정된 폴더에 저장됩니다.
/// </summary>
public class JSONExportWindow : EditorWindow
{
    private GameObject selectedPrefab;
    private string saveFolderPath = "Assets/CQ_Renewal/JSON";

    [MenuItem("CAT/Utility/Export JSON")]
    public static void ShowWindow()
    {
        GetWindow<JSONExportWindow>("JSON Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        //GUILayout.Label("Select Unit Prefab", EditorStyles.boldLabel);
        selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Unit Prefab", selectedPrefab, typeof(GameObject), false);

        GUILayout.Space(10);
        //GUILayout.Label("Save Folder Path", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        saveFolderPath = EditorGUILayout.TextField("Save Folder", saveFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 프로젝트 내에 있는 경우, 절대 경로를 상대 경로로 변환합니다.
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    saveFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    saveFolderPath = selectedPath;
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        
        if (selectedPrefab != null && GUILayout.Button("Export JSON"))
        {
            ExportPrefabHierarchyToJSON();
        }
        
        GUILayout.Space(5);
        
        if (selectedPrefab != null && GUILayout.Button("Export Hierarchy to Console (Legacy)"))
        {
            ExportPrefabHierarchy();
        }
    }

    private void ExportPrefabHierarchyToJSON()
    {
        if (selectedPrefab == null)
        {
            Debug.LogError("No prefab selected.");
            return;
        }

        // Pivot 오브젝트 찾기
        Transform pivotTransform = FindPivotTransform(selectedPrefab.transform);
        if (pivotTransform == null)
        {
            Debug.LogError($"'{selectedPrefab.name}'프리팹에서 'Pivot'을 찾을 수 없습니다. 프리팹 구조를 확인해주세요.");
            EditorUtility.DisplayDialog("Export Failed", $"'{selectedPrefab.name}'프리팹에서 'Pivot'을 찾을 수 없습니다.", "OK");
            return;
        }

        // Pivot의 하위 오브젝트들의 정보를 추출하여 플랫한 계층 구조로 저장
        List<GameObjectData> gameObjectsList = new List<GameObjectData>();
        CollectGameObjectData(pivotTransform, "", gameObjectsList);
        
        HierarchyData hierarchyData = new HierarchyData
        {
            prefabName = selectedPrefab.name,
            exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            gameObjects = gameObjectsList.ToArray()
        };

        // JSON으로 변환
        string jsonString = JsonUtility.ToJson(hierarchyData, true);

        // 저장 폴더가 존재하는지 확인
        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
        }

        // 타임스탬프를 포함한 파일 이름 생성
        string fileName = $"{selectedPrefab.name}_pivot_children_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(saveFolderPath, fileName);

        // 파일에 쓰기
        File.WriteAllText(filePath, jsonString);

        Debug.Log($"Pivot 하위 오브젝트들의 정보를 추출하여 플랫한 계층 구조로 저장: {filePath}");
        EditorUtility.DisplayDialog("Export Complete", $"Pivot 하위 오브젝트들의 정보를 추출하여 플랫한 계층 구조로 저장: {filePath}", "OK");
        
        // 새로운 파일을 표시하기 위해 자산 데이터베이스 새로 고침
        AssetDatabase.Refresh();
    }

    private Transform FindPivotTransform(Transform root)
    {
        // 현재 변환이 "Pivot"인지 확인
        if (root.name == "Pivot")
        {
            return root;
        }

        // 자식들에서 검색
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

    private void CollectGameObjectData(Transform transform, string parentPath, List<GameObjectData> gameObjectsList)
    {
        string currentPath = string.IsNullOrEmpty(parentPath) ? transform.name : $"{parentPath}/{transform.name}";
        
        GameObjectData gameObjectData = new GameObjectData
        {
            name = transform.name,
            path = currentPath,
            parentPath = parentPath,
            posX = transform.localPosition.x,
            posY = transform.localPosition.y,
            posZ = transform.localPosition.z,
            rotX = transform.localEulerAngles.x,
            rotY = transform.localEulerAngles.y,
            rotZ = transform.localEulerAngles.z,
            scaleX = transform.localScale.x,
            scaleY = transform.localScale.y,
            scaleZ = transform.localScale.z
        };

        // 컴포넌트 정보 수집
        Component[] components = transform.GetComponents<Component>();
        List<ComponentInfo> componentList = new List<ComponentInfo>();
        foreach (Component component in components)
        {
            if (component != null)
            {
                ComponentInfo componentInfo = new ComponentInfo
                {
                    componentName = component.GetType().Name,
                    componentType = component.GetType().FullName,
                    sortingOrder = 0 // 기본값
                };
                
                // SpriteRenderer의 경우 SortingOrder 수집
                if (component is SpriteRenderer spriteRenderer)
                {
                    componentInfo.sortingOrder = spriteRenderer.sortingOrder;
                }
                
                componentList.Add(componentInfo);
            }
        }
        gameObjectData.components = componentList.ToArray();

        // 플랫한 계층 구조로 저장
        gameObjectsList.Add(gameObjectData);

        // 자식들에 대해 재귀적으로 처리
        foreach (Transform child in transform)
        {
            CollectGameObjectData(child, currentPath, gameObjectsList);
        }
    }

    private void ExportPrefabHierarchy()
    {
        if (selectedPrefab == null)
        {
            Debug.LogError("프리팹이 선택되지 않았습니다.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("프리팹 계층 구조: " + selectedPrefab.name);
        sb.AppendLine("------------------------------------");

        // 선택된 프리팹의 루트에서 시작하여 재귀적으로 순회
        TraverseHierarchy(selectedPrefab.transform, sb, 0);

        string hierarchyText = sb.ToString();
        Debug.Log(hierarchyText);

        // 선택사항: 클립보드에 복사하여 쉽게 사용
        EditorGUIUtility.systemCopyBuffer = hierarchyText;
        Debug.Log("Hierarchy copied to clipboard!");
    }

    private void TraverseHierarchy(Transform currentTransform, StringBuilder sb, int depth)
    {
        // 시각적 계층 구조를 위한 들여쓰기
        string indentation = new string(' ', depth * 4);
        sb.AppendLine(indentation + "- " + currentTransform.name);

        // 자식들에 대해 재귀적으로 호출
        foreach (Transform child in currentTransform)
        {
            TraverseHierarchy(child, sb, depth + 1);
        }
    }
}