using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CAT.Utility
{
    // 특정 에디터를 상속받지 않는, 순수 C# 헬퍼 클래스
    public class FilteredSpriteFinderDrawer
    {
        private const string FOLDERS_PREFS_KEY = "FilteredSpriteFinder_Folders";
        private const string FOLDOUT_STATE_KEY = "FilteredSpriteFinder_Foldout";

        private static List<DefaultAsset> searchFolders = new List<DefaultAsset>();
        private static bool isInitialized = false;
        private static bool isFoldedOut = true;

        // 에디터가 활성화될 때 한 번만 초기화
        public void Initialize()
        {
            if (!isInitialized)
            {
                LoadFolders();
                isFoldedOut = EditorPrefs.GetBool(FOLDOUT_STATE_KEY, true);
                isInitialized = true;
            }
        }

        // 인스펙터에 UI를 그리는 메인 메서드
        public void DrawInspectorGUI(Action<Sprite> onSpriteSelectedAction)
        {
            EditorGUILayout.Space();

            bool newFoldoutState = EditorGUILayout.Foldout(isFoldedOut, "Sprite Folder Filter", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != isFoldedOut)
            {
                isFoldedOut = newFoldoutState;
                EditorPrefs.SetBool(FOLDOUT_STATE_KEY, isFoldedOut);
            }

            if (isFoldedOut)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Add New Folder", EditorStyles.miniBoldLabel);
                DefaultAsset folderToAdd = (DefaultAsset)EditorGUILayout.ObjectField(GUIContent.none, null, typeof(DefaultAsset), false);

                if (folderToAdd != null)
                {
                    if (!searchFolders.Contains(folderToAdd))
                    {
                        searchFolders.Add(folderToAdd);
                        SaveFolders();
                    }
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Registered Folders", EditorStyles.miniBoldLabel);
                if (searchFolders.RemoveAll(f => f == null) > 0)
                {
                    SaveFolders();
                }

                for (int i = 0; i < searchFolders.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(searchFolders[i], typeof(DefaultAsset), false);

                    if (GUILayout.Button("Find", GUILayout.Width(50)))
                    {
                        string path = AssetDatabase.GetAssetPath(searchFolders[i]);
                        FilteredSpriteSelector.ShowWindow(new List<string> { path }, onSpriteSelectedAction);
                    }

                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        searchFolders.RemoveAt(i);
                        SaveFolders();
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        private void SaveFolders()
        {
            string data = string.Join(";", searchFolders.Where(f => f != null).Select(f => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f))));
            EditorPrefs.SetString(FOLDERS_PREFS_KEY, data);
        }

        private void LoadFolders()
        {
            searchFolders.Clear();
            string data = EditorPrefs.GetString(FOLDERS_PREFS_KEY, "");
            if (string.IsNullOrEmpty(data)) return;

            string[] guids = data.Split(';');
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                    if (folder != null)
                    {
                        searchFolders.Add(folder);
                    }
                }
            }
        }
    }
}