using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace CAT.Utility
{
    /// <summary>
    /// 하이어라키 창 하단에 UI를 주입하여 선택된 오브젝트의 이름을 변경합니다.
    /// </summary>
    public static class HierarchyRenamerInjector
    {
        private static string inputText = "";
        private static string replaceText = "";
        private static int numberPadding = 2;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += InjectUI;
        }

        private static void InjectUI()
        {
            var editorAssembly = typeof(Editor).Assembly;
            var hierarchyWindows = Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.SceneHierarchyWindow"));
            if (hierarchyWindows.Length == 0) return;

            var hierarchyWindow = (EditorWindow)hierarchyWindows[0];
            var rawRoot = hierarchyWindow.rootVisualElement;
            if (rawRoot == null) return;

            if (rawRoot.Q<VisualElement>("HierarchyRenamerContainer") != null) return;

            var parentContainer = new VisualElement
            {
                name = "HierarchyRenamerContainer",
                style =
                {
                    position = Position.Absolute,
                    bottom = 5f,
                    left = 33f,
                    right = 5f,
                    height = 50f,
                    flexDirection = FlexDirection.Column
                }
            };

            parentContainer.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
            parentContainer.style.borderTopWidth = 1;
            parentContainer.style.borderTopColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            var imguiContainer = new IMGUIContainer(OnInjectedGUI);
            imguiContainer.style.flexGrow = 1;

            parentContainer.Add(imguiContainer);
            rawRoot.Add(parentContainer);
        }

        private static void OnInjectedGUI()
        {
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            inputText = EditorGUILayout.TextField(inputText, GUILayout.ExpandWidth(true));
            replaceText = EditorGUILayout.TextField(replaceText, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rn", GUILayout.Height(20))) { RenameObjects(RenameAction.Rename); }
            if (GUILayout.Button("Rp", GUILayout.Height(20))) { RenameObjects(RenameAction.Replace); }
            if (GUILayout.Button("T_", GUILayout.Height(20))) { RenameObjects(RenameAction.Prefix); }
            if (GUILayout.Button("_T", GUILayout.Height(20))) { RenameObjects(RenameAction.Suffix); }
            if (GUILayout.Button("Num", GUILayout.Height(20))) { RenameObjects(RenameAction.Number); }
            numberPadding = EditorGUILayout.IntField(numberPadding, GUILayout.Height(20), GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();
        }

        private enum RenameAction { Rename, Replace, Prefix, Suffix, Number }

        private static void RenameObjects(RenameAction action)
        {
            GUI.FocusControl(null);
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("[Renamer] 변경할 오브젝트가 선택되지 않았습니다.");
                return;
            }

            if (action != RenameAction.Number && string.IsNullOrEmpty(inputText))
            {
                Debug.LogWarning("[Renamer] 입력 필드가 비어있습니다.");
                return;
            }

            Undo.RecordObjects(selectedObjects, "Rename Object(s)");

            int counter = 0;
            foreach (var obj in selectedObjects)
            {
                switch (action)
                {
                    case RenameAction.Rename:
                        obj.name = inputText;
                        break;
                    case RenameAction.Replace:
                        obj.name = obj.name.Replace(inputText, replaceText);
                        break;
                    case RenameAction.Prefix:
                        obj.name = inputText + obj.name;
                        break;
                    case RenameAction.Suffix:
                        obj.name = obj.name + inputText;
                        break;
                    case RenameAction.Number:
                        if (numberPadding == 0)
                        {
                            obj.name = counter.ToString("D1");
                        }
                        else
                        {
                            string numberStr = counter.ToString("D" + numberPadding);
                            string baseName = string.IsNullOrEmpty(inputText) ? obj.name : inputText;
                            obj.name = $"{baseName}_{numberStr}";
                        }
                        break;
                }
                counter++;
            }
        }
    }
}


