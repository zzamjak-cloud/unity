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

public class HierarchyExporter : EditorWindow
{
    private GameObject selectedPrefab;
    private string saveFolderPath = "Assets/ExportedHierarchies";

    [MenuItem("CAT/Utility/Export Hierarchy")]
    public static void ShowWindow()
    {
        GetWindow<HierarchyExporter>("Hierarchy Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Select a Prefab to Export Pivot Children", EditorStyles.boldLabel);

        selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", selectedPrefab, typeof(GameObject), false);

        GUILayout.Space(10);
        GUILayout.Label("Save Folder Path", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        saveFolderPath = EditorGUILayout.TextField("Folder Path", saveFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Convert absolute path to relative path if it's within the project
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
        
        if (selectedPrefab != null && GUILayout.Button("Export Pivot Children to JSON"))
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

        // Find Pivot GameObject
        Transform pivotTransform = FindPivotTransform(selectedPrefab.transform);
        if (pivotTransform == null)
        {
            Debug.LogError($"No GameObject named 'Pivot' found in prefab '{selectedPrefab.name}'.");
            EditorUtility.DisplayDialog("Export Failed", $"No GameObject named 'Pivot' found in prefab '{selectedPrefab.name}'.", "OK");
            return;
        }

        // Create flat hierarchy data structure for Pivot's children only
        List<GameObjectData> gameObjectsList = new List<GameObjectData>();
        CollectGameObjectData(pivotTransform, "", gameObjectsList);
        
        HierarchyData hierarchyData = new HierarchyData
        {
            prefabName = selectedPrefab.name,
            exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            gameObjects = gameObjectsList.ToArray()
        };

        // Convert to JSON
        string jsonString = JsonUtility.ToJson(hierarchyData, true);

        // Ensure save folder exists
        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
        }

        // Generate filename with timestamp
        string fileName = $"{selectedPrefab.name}_pivot_children_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(saveFolderPath, fileName);

        // Write to file
        File.WriteAllText(filePath, jsonString);

        Debug.Log($"Pivot children hierarchy exported to: {filePath}");
        EditorUtility.DisplayDialog("Export Complete", $"Pivot children hierarchy exported successfully to:\n{filePath}", "OK");
        
        // Refresh asset database to show the new file
        AssetDatabase.Refresh();
    }

    private Transform FindPivotTransform(Transform root)
    {
        // Check if current transform is named "Pivot"
        if (root.name == "Pivot")
        {
            return root;
        }

        // Search in children
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

        // Collect component information
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

        // Add to flat list
        gameObjectsList.Add(gameObjectData);

        // Recursively process children
        foreach (Transform child in transform)
        {
            CollectGameObjectData(child, currentPath, gameObjectsList);
        }
    }

    private void ExportPrefabHierarchy()
    {
        if (selectedPrefab == null)
        {
            Debug.LogError("No prefab selected.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Prefab Hierarchy for: " + selectedPrefab.name);
        sb.AppendLine("------------------------------------");

        // Start the recursive traversal from the selected prefab's root
        TraverseHierarchy(selectedPrefab.transform, sb, 0);

        string hierarchyText = sb.ToString();
        Debug.Log(hierarchyText);

        // Optional: Copy to clipboard for easy use
        EditorGUIUtility.systemCopyBuffer = hierarchyText;
        Debug.Log("Hierarchy copied to clipboard!");
    }

    private void TraverseHierarchy(Transform currentTransform, StringBuilder sb, int depth)
    {
        // Indentation for visual hierarchy
        string indentation = new string(' ', depth * 4);
        sb.AppendLine(indentation + "- " + currentTransform.name);

        // Recursively call for each child
        foreach (Transform child in currentTransform)
        {
            TraverseHierarchy(child, sb, depth + 1);
        }
    }
}