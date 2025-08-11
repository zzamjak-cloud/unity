// 필수 네임스페이스들
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace CAT.Utility
{
    /// <summary>
    /// Unity 툴바에 씬 관리 드롭다운과 "Open Scene"버튼을 추가하는 에디터 스크립트입니다.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneToolbar
    {
        private static int selectedSceneIndex = 0;
        private static string[] sceneNames;
        private static string[] scenePaths;

        static SceneToolbar()
        {
            // 에디터가 시작될 때와 빌드 설정이 변경될 때 씬 목록을 갱신합니다.
            UpdateSceneList();
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
            // 빌드 설정 변경 시에도 목록을 갱신할 수 있으나, 이제는 모든 씬을 다루므로
            // 이 줄은 필수는 아니지만, 만일을 위해 남겨둘 수 있습니다.
            EditorBuildSettings.sceneListChanged += UpdateSceneList;
        }

        private static void OnToolbarGUI()
        {
            GUILayout.BeginHorizontal();

            if (selectedSceneIndex >= sceneNames.Length)
            {
                selectedSceneIndex = 0;
            }

            selectedSceneIndex = EditorGUILayout.Popup(selectedSceneIndex, sceneNames, GUILayout.Width(150));

            if (GUILayout.Button("Open Scene", GUILayout.Width(90)))
            {
                if (scenePaths != null && selectedSceneIndex >= 0 && selectedSceneIndex < scenePaths.Length)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(scenePaths[selectedSceneIndex]);
                    }
                }
            }

            // Play 모드 시작 씬 설정은 유효한 경로가 있을 때만 작동합니다.
            if (scenePaths != null && scenePaths.Length > 0 && selectedSceneIndex < scenePaths.Length)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePaths[selectedSceneIndex]);
                EditorSceneManager.playModeStartScene = sceneAsset;
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// [수정됨] 프로젝트 전체에서 씬 목록을 가져오되, 'Plugins' 폴더는 제외합니다.
        /// </summary>
        private static void UpdateSceneList()
        {
            // "t:Scene" 필터를 사용하여 'Assets' 폴더 내 모든 씬 에셋의 GUID를 찾습니다.
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

            // GUID를 실제 에셋 경로로 변환하고 "Plugins" 폴더에 있는 씬을 제외합니다.
            scenePaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                              .Where(path => !path.StartsWith("Assets/Plugins/"))
                              .ToArray();

            // 경로에서 씬 이름만 추출합니다.
            sceneNames = scenePaths
                .Select(path => System.IO.Path.GetFileNameWithoutExtension(path))
                .ToArray();

            // 프로젝트에 씬이 하나도 없을 경우를 대비합니다.
            if (sceneNames.Length == 0)
            {
                sceneNames = new string[] { "No Scenes in Project" };
                scenePaths = null;
            }
        }
    }


    /// <summary>
    /// Unity 툴바 확장을 위한 헬퍼 클래스 (수정 불필요)
    /// </summary>
    [InitializeOnLoad]
    public static class ToolbarExtender
    {
        private static readonly Type m_toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        private static ScriptableObject m_currentToolbar;

        public static readonly List<Action> LeftToolbarGUI = new List<Action>();

        static ToolbarExtender()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (m_currentToolbar == null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(m_toolbarType);
                m_currentToolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;
            }

            if (m_currentToolbar != null)
            {
                var root = m_currentToolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m_currentToolbar) as VisualElement;
                var toolbarZone = root.Q("ToolbarZoneLeftAlign");

                var container = toolbarZone?.Q<IMGUIContainer>("SceneToolbarContainer");

                if (container == null)
                {
                    container = new IMGUIContainer();
                    container.name = "SceneToolbarContainer";
                    container.onGUIHandler = () =>
                    {
                        foreach (var handler in LeftToolbarGUI)
                        {
                            handler();
                        }
                    };
                    toolbarZone.Add(container);
                }

                EditorApplication.update -= OnUpdate;
            }
        }
    }
}