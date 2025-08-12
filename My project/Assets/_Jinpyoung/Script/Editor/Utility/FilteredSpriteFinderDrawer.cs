using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace CAT.Utility
{
    public class FilteredSpriteFinderDrawer
    {
        private const string FOLDERS_JSON_PATH = "ProjectSettings/FilteredSpriteFolders.json";
        private const string FOLDOUT_STATE_KEY = "FilteredSpriteFinder_Foldout";

        [Serializable]
        private class FolderData
        {
            public List<string> folderGUIDs = new List<string>();
        }

        private static List<DefaultAsset> searchFolders = new List<DefaultAsset>();
        private static bool isInitialized = false;
        private static bool isFoldedOut = true;

        public void Initialize()
        {
            if (!isInitialized)
            {
                LoadFoldersFromJson();
                isFoldedOut = EditorPrefs.GetBool(FOLDOUT_STATE_KEY, true);
                isInitialized = true;
            }
        }

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
                        SaveFoldersToJson();
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Registered Folders", EditorStyles.miniBoldLabel);

                if (searchFolders.RemoveAll(f => f == null) > 0)
                {
                    SaveFoldersToJson();
                }

                for (int i = 0; i < searchFolders.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(searchFolders[i], typeof(DefaultAsset), false);

                    if (GUILayout.Button("Find", GUILayout.Width(50)))
                    {
                        string path = AssetDatabase.GetAssetPath(searchFolders[i]);
                        // 변경된 부분: 경로를 리스트가 아닌 단일 문자열로 전달합니다.
                        FilteredSpriteSelector.ShowWindow(path, onSpriteSelectedAction);
                    }

                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        searchFolders.RemoveAt(i);
                        SaveFoldersToJson();
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        private void SaveFoldersToJson()
        {
            FolderData data = new FolderData();
            data.folderGUIDs = searchFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f)))
                .ToList();

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FOLDERS_JSON_PATH, json);
            AssetDatabase.Refresh();
        }

        private void LoadFoldersFromJson()
        {
            searchFolders.Clear();
            if (File.Exists(FOLDERS_JSON_PATH))
            {
                string json = File.ReadAllText(FOLDERS_JSON_PATH);
                FolderData data = JsonUtility.FromJson<FolderData>(json);

                if (data != null)
                {
                    foreach (var guid in data.folderGUIDs)
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
    }
}