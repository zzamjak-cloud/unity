using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;

namespace CAT.Utility
{
    /// <summary>
    /// 하이어라키 창 하단에 UI를 주입하여 선택된 오브젝트의 이름을 변경합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyRenamerInjector
    {
        // --- 데이터 변수 ---
        private static string inputText = "";
        private static string replaceText = "";
        private static int numberPadding = 2;

        static HierarchyRenamerInjector()
        {
            // [수정] EditorApplication.update를 제거하고, 에디터 초기화 후 단 한 번만 실행되도록 변경
            EditorApplication.delayCall += InjectUI;
        }

        /// <summary>
        /// 하이어라키 창을 찾아 UI를 주입하는 핵심 로직. 에디터 시작 시 한 번만 호출됩니다.
        /// </summary>
        private static void InjectUI()
        {
            // "UnityEditor.SceneHierarchyWindow"가 하이어라키 창의 내부 이름
            var editorAssembly = typeof(Editor).Assembly;
            var hierarchyWindows = Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.SceneHierarchyWindow"));
            
            if (hierarchyWindows.Length == 0)
            {
                // 하이어라키 창이 없으면 아무것도 하지 않음
                return;
            }

            var hierarchyWindow = (EditorWindow)hierarchyWindows[0];
            var rawRoot = hierarchyWindow.rootVisualElement;
            if (rawRoot == null) return;

            // 스크립트 리컴파일 시 UI가 중복으로 추가되는 것을 방지
            if (rawRoot.Q<VisualElement>("HierarchyRenamerContainer") != null)
            {
                return;
            }

            var parentContainer = new VisualElement
            {
                name = "HierarchyRenamerContainer", // 중복 주입 방지를 위한 이름 지정
                style =
                {
                    position = Position.Absolute,
                    bottom = 5f, // 하단 상태바 바로 위에 위치
                    left = 33f,
                    right = 5f,
                    height = 50f, // UI 높이
                    flexDirection = FlexDirection.Column // 세로 정렬
                }
            };
            
            // 배경색과 테두리를 추가하여 다른 UI와 구분
            parentContainer.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
            parentContainer.style.borderTopWidth = 1;
            parentContainer.style.borderTopColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            var imguiContainer = new IMGUIContainer(OnInjectedGUI);
            imguiContainer.style.flexGrow = 1;

            parentContainer.Add(imguiContainer);
            rawRoot.Add(parentContainer);
        }

        // 하이어라키 창에 Renamer UI를 그립니다.
        private static void OnInjectedGUI()
        {
            // 상단 여백
            EditorGUILayout.Space(2);

            // --- 입력 필드 ---
            EditorGUILayout.BeginHorizontal();
            inputText = EditorGUILayout.TextField(inputText, GUILayout.ExpandWidth(true));
            replaceText = EditorGUILayout.TextField(replaceText, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(2);

            // --- 이름 변경 버튼들 ---
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rn", GUILayout.Height(20)))   { RenameObjects(RenameAction.Rename); }
            if (GUILayout.Button("Rp", GUILayout.Height(20)))  { RenameObjects(RenameAction.Replace); }
            if (GUILayout.Button("T_", GUILayout.Height(20)))   { RenameObjects(RenameAction.Prefix); }
            if (GUILayout.Button("_T", GUILayout.Height(20)))   { RenameObjects(RenameAction.Suffix); }
            
            // --- 넘버링 버튼 ---
            if (GUILayout.Button("Num", GUILayout.Height(20))) { RenameObjects(RenameAction.Number); }
            numberPadding = EditorGUILayout.IntField(numberPadding, GUILayout.Height(20), GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();
        }

        private enum RenameAction
        {
            Rename, Replace, Prefix, Suffix, Number
        }

        private static void RenameObjects(RenameAction action)
        {
            GUI.FocusControl(null);

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("[Renamer] 변경할 오브젝트가 선택되지 않았습니다.");
                return;
            }

            // [수정] Numbering 액션을 제외한 나머지 액션에 대해서만 입력 필드 유효성 검사
            if (action != RenameAction.Number && string.IsNullOrEmpty(inputText))
            {
                Debug.LogWarning("[Renamer] 입력 필드가 비어있습니다.");
                return;
            }

            Undo.RecordObjects(selectedObjects, "Rename Object(s)");

            int counter = 0;
            foreach (GameObject obj in selectedObjects)
            {
                switch (action)
                {
                    case RenameAction.Rename: obj.name = inputText; break;
                    case RenameAction.Replace: obj.name = obj.name.Replace(inputText, replaceText); break;
                    case RenameAction.Prefix: obj.name = inputText + obj.name; break;
                    case RenameAction.Suffix: obj.name = obj.name + inputText; break;
                    case RenameAction.Number:
                        if (numberPadding == 0)
                        {
                            obj.name = counter.ToString("D1");
                        }
                        else
                        {
                            string numberStr = counter.ToString("D" + numberPadding);
                            string baseName;
                            
                            // 입력 필드가 비어있으면, 기존 이름 전체를 그대로 사용합니다.
                            // 더 이상 Regex로 마지막 숫자를 제거하지 않습니다.
                            if (string.IsNullOrEmpty(inputText))
                            {
                                baseName = obj.name;
                            }
                            else
                            {
                                baseName = inputText;
                            }
                            obj.name = $"{baseName}_{numberStr}";
                        }
                        break;
                }
                counter++;
            }
        }
    }
}
