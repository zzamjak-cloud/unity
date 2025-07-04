using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    [InitializeOnLoad]
    internal class FR2_CacheHelper : AssetPostprocessor
    {
        [NonSerialized] private static HashSet<string> scenes;
        [NonSerialized] private static HashSet<string> guidsIgnore;
        [NonSerialized] internal static bool inited = false;
        
        static FR2_CacheHelper()
        {
            try
            {
                EditorApplication.update -= InitHelper;
                EditorApplication.update += InitHelper;
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {

            FR2_Cache.DelayCheck4Changes();

            //Debug.Log("OnPostProcessAllAssets : " + ":" + importedAssets.Length + ":" + deletedAssets.Length + ":" + movedAssets.Length + ":" + movedFromAssetPaths.Length);

            if (!FR2_Cache.isReady)
            {
#if FR2_DEBUG
			Debug.Log("Not ready, will refresh anyway !");
#endif
                return;
            }

            // FR2 not yet ready
            if (FR2_Cache.Api.AssetMap == null) return;

            for (var i = 0; i < importedAssets.Length; i++)
            {
                if (importedAssets[i] == FR2_Cache.CachePath) continue;

                string guid = AssetDatabase.AssetPathToGUID(importedAssets[i]);
                if (!FR2_Asset.IsValidGUID(guid)) continue;

                if (FR2_Cache.Api.AssetMap.ContainsKey(guid))
                {
                    FR2_Cache.Api.RefreshAsset(guid, true);

#if FR2_DEBUG
				Debug.Log("Changed : " + importedAssets[i]);
#endif

                    continue;
                }

                FR2_Cache.Api.AddAsset(guid);
#if FR2_DEBUG
			Debug.Log("New : " + importedAssets[i]);
#endif
            }

            for (var i = 0; i < deletedAssets.Length; i++)
            {
                string guid = AssetDatabase.AssetPathToGUID(deletedAssets[i]);
                FR2_Cache.Api.RemoveAsset(guid);

#if FR2_DEBUG
			Debug.Log("Deleted : " + deletedAssets[i]);
#endif
            }

            for (var i = 0; i < movedAssets.Length; i++)
            {
                string guid = AssetDatabase.AssetPathToGUID(movedAssets[i]);
                FR2_Asset asset = FR2_Cache.Api.Get(guid);
                if (asset != null) asset.MarkAsDirty();
            }

#if FR2_DEBUG
		Debug.Log("Changes :: " + importedAssets.Length + ":" + FR2_Cache.Api.workCount);
#endif

            FR2_Cache.Api.Check4Work();
        }
        
        internal static void InitHelper()
        {
            if (FR2_Unity.isEditorCompiling || FR2_Unity.isEditorUpdating) return;
            if (!FR2_Cache.isReady) return;
            EditorApplication.update -= InitHelper;
            
            inited = true;
            InitListScene();
            InitIgnore();
            CheckGitStatus();

#if UNITY_2018_1_OR_NEWER
            EditorBuildSettings.sceneListChanged -= InitListScene;
            EditorBuildSettings.sceneListChanged += InitListScene;
#endif

            #if UNITY_2022_1_OR_NEWER
            EditorApplication.projectWindowItemInstanceOnGUI -= OnGUIProjectInstance;
            EditorApplication.projectWindowItemInstanceOnGUI += OnGUIProjectInstance;
            #else
            EditorApplication.projectWindowItemOnGUI -= OnGUIProjectItem;
            EditorApplication.projectWindowItemOnGUI += OnGUIProjectItem;
            #endif

            InitIgnore();
            // force repaint all project panels
            EditorApplication.RepaintProjectWindow();
        }
        
        private static void CheckGitStatus()
        {
            bool isGitProject = FR2_GitUtil.IsGitProject();
            FR2_SettingExt.isGitProject = isGitProject;
            
            if (isGitProject)
            {
                bool gitIgnoreAdded = FR2_GitUtil.CheckGitIgnoreContainsFR2Cache();
                FR2_SettingExt.gitIgnoreAdded = gitIgnoreAdded;
            }
        }
        
        public static void InitIgnore()
        {
            guidsIgnore = new HashSet<string>();
            foreach (string item in FR2_Setting.IgnoreAsset)
            {
                string guid = AssetDatabase.AssetPathToGUID(item);
                guidsIgnore.Add(guid);
            }
            
            // Debug.Log($"Init Ignore: {guidsIgnore.Count} items");
        }

        private static void InitListScene()
        {
            scenes = new HashSet<string>();

            // string[] scenes = new string[sceneCount];
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                string sce = AssetDatabase.AssetPathToGUID(scene.path);
                scenes.Add(sce);
            }
        }

        private static string lastGUID;
        private static void OnGUIProjectInstance(int instanceID, Rect selectionRect)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instanceID, out string guid, out long localId)) return;

            bool isMainAsset = guid != lastGUID;
            lastGUID = guid;

            if (isMainAsset)
            {
                DrawProjectItem(guid, selectionRect);
                return;
            }
            
            if (!FR2_Cache.Api.setting.showSubAssetFileId) return;
            var rect2 = selectionRect;
            var label = new GUIContent(localId.ToString());
            rect2.xMin = rect2.xMax - EditorStyles.miniLabel.CalcSize(label).x;

            var c = GUI.color;
            GUI.color = new Color(.5f, .5f, .5f, 0.5f);
            GUI.Label(rect2, label, EditorStyles.miniLabel);
            GUI.color = c;
        }

        private static void OnGUIProjectItem(string guid, Rect rect)
        {
            bool isMainAsset = guid != lastGUID;
            lastGUID = guid;
            if (isMainAsset) DrawProjectItem(guid, rect);
        }

        private static void DrawProjectItem(string guid, Rect rect)
        {
            var r = new Rect(rect.x, rect.y, 1f, 16f);
            if (scenes.Contains(guid))
                EditorGUI.DrawRect(r, GUI2.Theme(new Color32(72, 150, 191, 255), Color.blue));
            else if (guidsIgnore.Contains(guid))
            {
                var ignoreRect = new Rect(rect.x + 3f, rect.y + 6f, 2f, 2f);
                EditorGUI.DrawRect(ignoreRect, GUI2.darkRed);
            }

            if (!FR2_Cache.isReady) return; // not ready
            if (!FR2_Setting.ShowReferenceCount) return;

            FR2_Cache api = FR2_Cache.Api;
            if (FR2_Cache.Api.AssetMap == null) FR2_Cache.Api.Check4Changes(false);
            if (!api.AssetMap.TryGetValue(guid, out FR2_Asset item)) return;

            if (item == null || item.UsedByMap == null) return;

            if (item.UsedByMap.Count > 0)
            {
                var content = FR2_GUIContent.FromString(item.UsedByMap.Count.ToString());
                r.width = 0f;
                r.xMin -= 100f;
                GUI.Label(r, content, GUI2.miniLabelAlignRight);
            } else if (item.forcedIncludedInBuild)
            {
                var c = GUI.color;
                GUI.color = c.Alpha(0.2f);
                var content = FR2_GUIContent.FromString("+");
                r.width = 0f;
                r.xMin -= 100f;
                GUI.Label(r, content, GUI2.miniLabelAlignRight);
                GUI.color = c;
            }
        }
    }
}
