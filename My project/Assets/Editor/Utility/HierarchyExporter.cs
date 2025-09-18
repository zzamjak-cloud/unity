using UnityEngine;
using UnityEditor;
using System.Text;

public class HierarchyExporter : EditorWindow
{
    private GameObject selectedPrefab;

    [MenuItem("CAT/Utility/Export Hierarchy")]
    public static void ShowWindow()
    {
        GetWindow<HierarchyExporter>("Hierarchy Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Select a Prefab to Export", EditorStyles.boldLabel);

        selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", selectedPrefab, typeof(GameObject), false);

        if (selectedPrefab != null && GUILayout.Button("Export Hierarchy"))
        {
            ExportPrefabHierarchy();
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