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
    /// Unity 툴바에 씬 관리 드롭다운과 버튼을 추가하는 에디터 스크립트입니다.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneToolbar
    {
        private static int selectedSceneIndex = 0;
        private static string[] sceneNames;
        private static string[] scenePaths;
        private const string SelectedSceneIndexKey = "SceneToolbar.SelectedSceneIndex";

        static SceneToolbar()
        {
            // 에디터가 로드될 때 EditorPrefs에서 저장된 인덱스를 불러옵니다.
            selectedSceneIndex = EditorPrefs.GetInt(SelectedSceneIndexKey, 0);

            UpdateSceneList();
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
            EditorBuildSettings.sceneListChanged += UpdateSceneList;
        }

        private static void OnToolbarGUI()
        {
            GUILayout.BeginHorizontal();

            // 선택된 인덱스가 범위를 벗어날 경우 0으로 초기화
            if (selectedSceneIndex >= sceneNames.Length)
            {
                selectedSceneIndex = 0;
            }

            // 드롭다운의 값이 변경되는지 감지하고 값이 변경되면 EditorPrefs에 저장합니다.
            EditorGUI.BeginChangeCheck();
            selectedSceneIndex = EditorGUILayout.Popup(selectedSceneIndex, sceneNames, GUILayout.Width(120));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(SelectedSceneIndexKey, selectedSceneIndex);
            }

            if (GUILayout.Button("Open", GUILayout.Width(90)))
            {
                if (scenePaths != null && selectedSceneIndex >= 0 && selectedSceneIndex < scenePaths.Length)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(scenePaths[selectedSceneIndex]);
                    }
                }
            }

            if (scenePaths != null && scenePaths.Length > 0 && selectedSceneIndex < scenePaths.Length)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePaths[selectedSceneIndex]);
                EditorSceneManager.playModeStartScene = sceneAsset;
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 프로젝트 전체에서 씬 목록을 가져오되, 'Plugins' 폴더는 제외합니다.
        /// </summary>
        private static void UpdateSceneList()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

            scenePaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                              .Where(path => !path.StartsWith("Assets/Plugins/"))
                              .ToArray();

            sceneNames = scenePaths
                .Select(path => System.IO.Path.GetFileNameWithoutExtension(path))
                .ToArray();

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
                var toolbarZone = root.Q("ToolbarZonePlayMode");
                //var toolbarZone = root.Q("ToolbarZoneLeftAlign");

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