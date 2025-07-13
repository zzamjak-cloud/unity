using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

public static class PrefabDuplicator
{
    private const string MENU_BASE_PATH = "CAT/";
    private const string CONTEXT_MENU_BASE_PATH = "GameObject/CAT/";
    private const string PREFAB_PATH = "Assets/_Art/Prefab/";
    private const string MENU_ITEM_PATH = "CAT/[Refresh Menu]";

    private const string METHOD_START_MARKER = "// === Start";
    private const string METHOD_END_MARKER = "// === End ===";

    [MenuItem(MENU_ITEM_PATH)]
    public static void RefreshPrefabMenu()
    {
        try
        {
            string scriptPath = GetCurrentScriptPath();
            if (string.IsNullOrEmpty(scriptPath)) return;

            string currentContent = File.ReadAllText(scriptPath);
            string generatedMethods = GenerateMethodsForPrefabs();

            string pattern = $"{Regex.Escape(METHOD_START_MARKER + " Gen ===")}(.*?)(?={Regex.Escape(METHOD_END_MARKER)})";
            string replacement = $"{METHOD_START_MARKER + " Gen ==="}\n\n{generatedMethods}";

            string updatedContent = Regex.Replace(
                currentContent,
                pattern,
                replacement,
                RegexOptions.Singleline
            );

            if (currentContent != updatedContent)
            {
                File.WriteAllText(scriptPath, updatedContent);
                AssetDatabase.Refresh();
                Debug.Log("Successfully updated prefab menu items!");
            }
            else
            {
                Debug.Log("No changes were necessary in the prefab menu items.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error refreshing prefab menu: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static string GetCurrentScriptPath()
    {
        string[] guids = AssetDatabase.FindAssets("t:Script PrefabDuplicator");
        if (guids.Length == 0)
        {
            Debug.LogError("PrefabDuplicator script not found!");
            return null;
        }
        return AssetDatabase.GUIDToAssetPath(guids[0]);
    }

    private static string GenerateMethodsForPrefabs()
    {
        StringBuilder methods = new StringBuilder();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_PATH });

        if (prefabGuids.Length == 0)
        {
            Debug.Log($"No prefabs found in path: {PREFAB_PATH}");
            return "";
        }

        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            string safePrefabName = MakeSafeMethodName(prefabName);

            // 수동 메서드 체크
            string manualMethodName = $"Create_{safePrefabName}_Manual";
            if (HasMethod(manualMethodName))
            {
                Debug.Log($"Manual method '{manualMethodName}' already exists. Skipping auto-generation.");
                continue;
            }

            // 폴더 구조를 메뉴 경로로 변환
            string relativePath = GetRelativePath(prefabPath);

            // 상단 메뉴 아이템 생성
            string menuPath = MENU_BASE_PATH + relativePath + prefabName;
            methods.AppendLine($"    [MenuItem(\"{menuPath}\")]");
            methods.AppendLine($"    static void Create_{safePrefabName}()");
            methods.AppendLine("    {");
            methods.AppendLine($"        string path = \"{prefabPath}\";");
            methods.AppendLine($"        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);");
            methods.AppendLine($"        if (prefab != null) CreatePrefabInstance(prefab);");
            methods.AppendLine("    }");
            methods.AppendLine();

            // 컨텍스트 메뉴 아이템 생성
            string contextMenuPath = CONTEXT_MENU_BASE_PATH + relativePath + prefabName;
            methods.AppendLine($"    [MenuItem(\"{contextMenuPath}\", false, 10)]");
            methods.AppendLine($"    static void CreateContext_{safePrefabName}()");
            methods.AppendLine("    {");
            methods.AppendLine($"        string path = \"{prefabPath}\";");
            methods.AppendLine($"        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);");
            methods.AppendLine($"        if (prefab != null) CreatePrefabInstance(prefab);");
            methods.AppendLine("    }");
            methods.AppendLine();
        }

        return methods.ToString();
    }

    private static string GetRelativePath(string fullPath)
    {
        string relativePath = fullPath.Replace(PREFAB_PATH, "").Replace(Path.GetFileName(fullPath), "");
        relativePath = relativePath.TrimStart('/');

        if (string.IsNullOrEmpty(relativePath))
            return "";

        string[] folders = relativePath.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", folders) + "/";
    }

    private static bool HasMethod(string methodName)
    {
        System.Reflection.MethodInfo[] methods = typeof(PrefabDuplicator).GetMethods(
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
        );

        foreach (var method in methods)
        {
            if (method.Name == methodName)
            {
                return true;
            }
        }

        return false;
    }

    private static string MakeSafeMethodName(string input)
    {
        string safe = Regex.Replace(input, @"[^\w_]", "_");
        if (char.IsDigit(safe[0]))
            safe = "_" + safe;
        return safe;
    }

    private static void CreatePrefabInstance(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("Cannot create instance: Prefab is null");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance != null)
        {
            Undo.RegisterCreatedObjectUndo(instance, "Create Prefab Instance");

            if (Selection.activeTransform != null)
            {
                instance.transform.SetParent(Selection.activeTransform, false);
                instance.transform.localPosition = Vector3.zero;
            }
            instance.transform.localScale = Vector3.one;

            Selection.activeGameObject = instance;
        }
        else
        {
            Debug.LogError($"Failed to instantiate prefab: {prefab.name}");
        }
    }

    // === Start Gen ===
    // === End ===
}