﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_WindowAll : FR2_WindowBase, IHasCustomMenu
    {
        [SerializeField] internal PanelSettings settings = new PanelSettings();

        [MenuItem("Window/Find Reference 2")]
        private static void ShowWindow()
        {
            var _window = CreateInstance<FR2_WindowAll>();
            _window.InitIfNeeded();
            FR2_Unity.SetWindowTitle(_window, "FR2");
            _window.Show();
        }

        [NonSerialized] internal FR2_Bookmark bookmark;
        [NonSerialized] internal FR2_Selection selection;
        [NonSerialized] internal FR2_UsedInBuild UsedInBuild;
        [NonSerialized] internal FR2_DuplicateTree2 Duplicated;
        [NonSerialized] internal FR2_RefDrawer RefUnUse;
        [NonSerialized] internal FR2_MissingReference MissingReference;
        [NonSerialized] internal FR2_AssetOrganizer AssetOrganizer;
        [NonSerialized] internal FR2_DeleteEmptyFolder DeleteEmptyFolder;

        [NonSerialized] internal FR2_RefDrawer UsesDrawer; // [Selected Assets] are [USING] (depends on / contains reference to) ---> those assets
        [NonSerialized] internal FR2_RefDrawer UsedByDrawer; // [Selected Assets] are [USED BY] <---- those assets 
        [NonSerialized] internal FR2_RefDrawer SceneToAssetDrawer; // [Selected GameObjects in current Scene] are [USING] ---> those assets
        [NonSerialized] internal FR2_AddressableDrawer AddressableDrawer;


        [NonSerialized] internal FR2_RefDrawer RefInScene; // [Selected Assets] are [USED BY] <---- those components in current Scene 
        [NonSerialized] internal FR2_RefDrawer SceneUsesDrawer; // [Selected GameObjects] are [USING] ---> those components / GameObjects in current scene
        [NonSerialized] internal FR2_RefDrawer RefSceneInScene; // [Selected GameObjects] are [USED BY] <---- those components / GameObjects in current scene


        internal int level;
        private Vector2 scrollPos;
        private string tempGUID;
        private string tempFileID;
        private UnityObject tempObject;

        protected bool lockSelection => (selection != null) && selection.isLock;

        private void OnEnable()
        {
            Repaint();
        }

        protected void InitIfNeeded()
        {
            if (UsesDrawer != null) return;

            UsesDrawer = new FR2_RefDrawer(this, () => settings.sortMode, () => settings.groupMode)
            {
                messageEmpty = "[Selected Assets] are not [USING] (depends on / contains reference to) any other assets!"
            };

            UsedByDrawer = new FR2_RefDrawer(this, () => settings.sortMode, () => settings.groupMode)
            {
                messageEmpty = "[Selected Assets] are not [USED BY] any other assets!"
            };

            AddressableDrawer = new FR2_AddressableDrawer(this, () => settings.sortMode, () => settings.groupMode);

            Duplicated = new FR2_DuplicateTree2(this, () => settings.sortMode, () => settings.toolGroupMode);
            SceneToAssetDrawer = new FR2_RefDrawer(this, () => settings.sortMode, () => settings.groupMode)
            {
                messageEmpty = "[Selected GameObjects] (in current open scenes) are not [USING] any assets!"
            };

            RefUnUse = new FR2_RefDrawer(this, () => settings.sortMode, () => settings.toolGroupMode)
            {
                groupDrawer =
                {
                    hideGroupIfPossible = true
                }
            };

            UsedInBuild = new FR2_UsedInBuild(this, () => settings.sortMode, () => settings.toolGroupMode);
            MissingReference = new FR2_MissingReference(this, () => settings.sortMode, () => settings.toolGroupMode);
            AssetOrganizer = new FR2_AssetOrganizer(this, () => settings.sortMode, () => settings.toolGroupMode);
            DeleteEmptyFolder = new FR2_DeleteEmptyFolder(this, () => settings.sortMode, () => settings.toolGroupMode);
            bookmark = new FR2_Bookmark(this, () => settings.sortMode, () => settings.groupMode);
            selection = new FR2_Selection(this, () => settings.sortMode, () => settings.groupMode);

            SceneUsesDrawer = new FR2_RefDrawer(this, () => settings.sortMode, () => settings.groupMode)
            {
                messageEmpty = "[Selected GameObjects] are not [USING] any other GameObjects in scenes"
            };

            RefInScene = new FR2_RefDrawer(this, () => settings.sortMode, () => settings.groupMode)
            {
                messageEmpty = "[Selected Assets] are not [USED BY] any GameObjects in opening scenes!"
            };

            RefSceneInScene = new FR2_RefDrawer(this, () => settings.sortMode, () => settings.groupMode)
            {
                messageEmpty = "[Selected GameObjects] are not [USED BY] by any GameObjects in opening scenes!"
            };

#if UNITY_2018_OR_NEWER
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
#elif UNITY_2017_OR_NEWER
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged -= OnSceneChanged;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnSceneChanged;
#endif

            FR2_Cache.onReady -= OnReady;
            FR2_Cache.onReady += OnReady;

            FR2_Setting.OnIgnoreChange -= OnIgnoreChanged;
            FR2_Setting.OnIgnoreChange += OnIgnoreChanged;

            int idx = settings.historyIndex;
            if ((idx != -1) && (settings.history.Count > idx))
            {
                SelectHistory h = settings.history[idx];
                Selection.objects = h.selection;
                settings.historyIndex = idx;
                RefreshOnSelectionChange();
                Repaint();
            }

            RefreshShowFullPath();
            RefreshShowFileSize();
            RefreshShowFileExtension();
            RefreshShowUsageType();
            Repaint();
        }

#if UNITY_2018_OR_NEWER
        private void OnSceneChanged(Scene arg0, Scene arg1)
        {
            if (IsFocusingFindInScene || IsFocusingSceneToAsset || IsFocusingSceneInScene)
            {
                OnSelectionChange();
            }
        }
#endif
        protected void OnIgnoreChanged()
        {
            RefUnUse.ResetUnusedAsset();
            UsedInBuild.SetDirty();
            OnSelectionChange();
        }
        protected void OnCSVClick()
        {
            FR2_Ref[] csvSource = null;
            FR2_RefDrawer drawer = GetAssetDrawer();

            if (drawer != null) csvSource = drawer.source;

            if (isFocusingUnused && (csvSource == null)) csvSource = RefUnUse.source;

            //if (csvSource != null) Debug.Log("d : " + csvSource.Length);
            if (isFocusingUsedInBuild && (csvSource == null)) csvSource = FR2_Ref.FromDict(UsedInBuild.refs);

            //if (csvSource != null) Debug.Log("e : " + csvSource.Length);
            if (isFocusingDuplicate && (csvSource == null)) csvSource = FR2_Ref.FromList(Duplicated.list);

            //if (csvSource != null) Debug.Log("f : " + csvSource.Length);
            FR2_Export.ExportCSV(csvSource);
        }

        protected void OnReady()
        {
            OnSelectionChange();
        }

        private void AddHistory()
        {
            UnityObject[] objects = Selection.objects;

            // Check if the same set of selection has already existed
            RefreshHistoryIndex(objects);
            if (settings.historyIndex != -1) return;

            // Add newly selected objects to the selection
            const int MAX_HISTORY_LENGTH = 10;
            settings.history.Add(new SelectHistory { selection = Selection.objects });
            settings.historyIndex = settings.history.Count - 1;
            if (settings.history.Count > MAX_HISTORY_LENGTH)
            {
                settings.history.RemoveRange(0, settings.history.Count - MAX_HISTORY_LENGTH);
            }
            EditorUtility.SetDirty(this);
        }

        private void RefreshHistoryIndex(UnityObject[] objects)
        {
            if (this == null) return;

            settings.historyIndex = -1;
            if (objects == null || objects.Length == 0) return;
            List<SelectHistory> history = settings.history;
            for (var i = 0; i < history.Count; i++)
            {
                SelectHistory h = history[i];
                if (!h.IsTheSame(objects)) continue;
                settings.historyIndex = i;
            }

            EditorUtility.SetDirty(this);
        }

        private bool isScenePanelVisible
        {
            get
            {
                if (isFocusingAddressable) return false;

                if (selection.isSelectingAsset && isFocusingUses) // Override
                {
                    return false;
                }

                if (!selection.isSelectingAsset && isFocusingUsedBy)
                {
                    return true;
                }

                return settings.scene;
            }
        }

        private bool isAssetPanelVisible
        {
            get
            {
                if (isFocusingAddressable) return false;

                if (selection.isSelectingAsset && isFocusingUses) // Override
                {
                    return true;
                }

                if (!selection.isSelectingAsset && isFocusingUsedBy)
                {
                    return false;
                }

                return settings.asset;
            }
        }

        private void RefreshPanelVisible()
        {
            if (sp1 == null || sp2 == null) return;
            sp2.splits[0].visible = isScenePanelVisible;
            sp2.splits[1].visible = isAssetPanelVisible;
            sp2.splits[2].visible = isFocusingAddressable;
            sp2.CalculateWeight();
        }

        private void RefreshOnSelectionChange()
        {
            ids = FR2_Unity.Selection_AssetGUIDs;
            selection.Clear();

            GameObject[] gameObjects = Selection.gameObjects;

            //ignore selection on asset when selected any object in scene
            if ((gameObjects.Length > 0) && !FR2_Unity.IsInAsset(gameObjects[0]))
            {
                ids = Array.Empty<string>();
                selection.AddRange(gameObjects);
            } else
            {
                selection.AddRange(ids);
            }

            level = 0;
            RefreshPanelVisible();

            if (selection.isSelectingAsset)
            {
                UsesDrawer.Reset(ids, true);
                UsedByDrawer.Reset(ids, false);
                RefInScene.Reset(ids, this as IWindow);
                AddressableDrawer.RefreshView();

            } else
            {
                RefSceneInScene.ResetSceneInScene(gameObjects);
                SceneToAssetDrawer.Reset(gameObjects, true, true);
                SceneUsesDrawer.ResetSceneUseSceneObjects(gameObjects);
            }
        }

        public override void OnSelectionChange()
        {
            Repaint();
            if (!FR2_Cache.isReady) return;

            if (focusedWindow == null) return;
            if (SceneUsesDrawer == null) InitIfNeeded();
            if (UsesDrawer == null) InitIfNeeded();

            if (!lockSelection)
            {
                RefreshOnSelectionChange();
                RefreshHistoryIndex(Selection.objects);
            }

            if (isFocusingGUIDs)
            {
                //guidObjs = new Object[ids.Length];
                guidObjs = new Dictionary<string, UnityObject>();
                UnityObject[] objects = Selection.objects;
                for (var i = 0; i < objects.Length; i++)
                {
                    UnityObject item = objects[i];

#if UNITY_2018_1_OR_NEWER
                    {
                        var guid = "";
                        long fileid = -1;
                        try
                        {
                            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out guid, out fileid))
                            {
                                guidObjs.Add(guid + "/" + fileid, objects[i]);
                            }

                            //Debug.Log("guid: " + guid + "  fileID: " + fileid);
                        } catch { }
                    }
#else
					{
						var path = AssetDatabase.GetAssetPath(item);
                        if (string.IsNullOrEmpty(path)) continue;
                        var guid = AssetDatabase.AssetPathToGUID(path);
                        System.Reflection.PropertyInfo inspectorModeInfo =
                        typeof(SerializedObject).GetProperty("inspectorMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        SerializedObject serializedObject = new SerializedObject(item);
                        inspectorModeInfo.SetValue(serializedObject, InspectorMode.Debug, null);

                        SerializedProperty localIdProp =
                            serializedObject.FindProperty("m_LocalIdentfierInFile");   //note the misspelling!

                        var localId = localIdProp.longValue;
                        if (localId <= 0)
                        {
                            localId = localIdProp.intValue;
                        }
                        if (localId <= 0)
                        {
                            continue;
                        }
                        if (!string.IsNullOrEmpty(guid)) guidObjs.Add(guid + "/" + localId, objects[i]);
					}
#endif
                }
            }

            if (isFocusingUnused)
            {
                RefUnUse.ResetUnusedAsset();
            }

            if (!Application.isPlaying)
            {
                FR2_SceneCache.Api.refreshCache(this, false);
            }

            EditorApplication.delayCall -= Repaint;
            EditorApplication.delayCall += Repaint;
        }


        [NonSerialized] public FR2_SplitView sp1; // container : Selection / sp2 / Bookmark 
        [NonSerialized] public FR2_SplitView sp2; // Scene / Assets
        [NonSerialized] public FR2_SplitView sp3; // Addressable

        private void DrawHistory(Rect rect)
        {
            Color c = GUI.backgroundColor;
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();

            for (var i = 0; i < settings.history.Count; i++)
            {
                SelectHistory h = settings.history[i];
                int idx = i;
                GUI.backgroundColor = i == settings.historyIndex ? GUI2.darkBlue : c;

                var content = new GUIContent($"{i + 1}", "RightClick to delete!");
                if (GUILayout.Button(content, EditorStyles.miniButton, GUI2.GLW_24))
                {
                    // Debug.Log($"Button: {Event.current.button}");

                    if (Event.current.button == 0) // left click
                    {
                        Selection.objects = h.selection;
                        settings.historyIndex = idx;
                        RefreshOnSelectionChange();
                        Repaint();
                    }

                    if (Event.current.button == 1) // right click
                    {
                        bool isActive = i == settings.historyIndex;
                        settings.history.RemoveAt(idx);

                        if (isActive && (settings.history.Count > 0))
                        {
                            int idx2 = settings.history.Count - 1;
                            Selection.objects = settings.history[idx2].selection;
                            settings.historyIndex = idx2;
                            RefreshOnSelectionChange();
                            Repaint();
                        }
                    }
                }


            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.backgroundColor = c;
        }

        private void InitPanes()
        {
            sp2 = new FR2_SplitView(this)
            {
                isHorz = false,
                splits = new List<FR2_SplitView.Info>
                {
                    new FR2_SplitView.Info
                        { title = new GUIContent("Scene", FR2_Icon.Scene.image), draw = DrawScene, visible = settings.scene },
                    new FR2_SplitView.Info
                        { title = new GUIContent("Assets", FR2_Icon.Asset.image), draw = DrawAsset, visible = settings.asset },
                    new FR2_SplitView.Info
                        { title = null, draw = rect => AddressableDrawer.Draw(rect), visible = false }
                }
            };

            sp2.CalculateWeight();

            sp1 = new FR2_SplitView(this)
            {
                isHorz = true,
                splits = new List<FR2_SplitView.Info>
                {
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Selection", FR2_Icon.Selection.image),
                        weight = 0.4f,
                        visible = settings.selection,
                        draw = rect =>
                        {
                            Rect historyRect = rect;
                            historyRect.yMin = historyRect.yMax - 16f;

                            rect.yMax -= 16f;
                            selection.Draw(rect);
                            DrawHistory(historyRect);
                        }
                    },
                    new FR2_SplitView.Info
                    {
                        draw = r =>
                        {
                            sp2.Draw(r);
                        }
                    },
                    new FR2_SplitView.Info
                    {
                        title = new GUIContent("Asset Detail", FR2_Icon.Details.image), weight = 0.4f, visible = settings.details, draw = rect =>
                        {
                            FR2_RefDrawer assetDrawer = GetAssetDrawer();
                            if (assetDrawer != null) assetDrawer.DrawDetails(rect);
                        }
                    },
                    new FR2_SplitView.Info
                        { title = new GUIContent("Bookmark", FR2_Icon.Favorite.image), weight = 0.4f, visible = settings.bookmark, draw = rect => bookmark.Draw(rect) }
                }
            };

            sp1.CalculateWeight();
        }

        private FR2_TabView tabs;
        private FR2_TabView toolTabs;
        private FR2_TabView bottomTabs;
        private FR2_SearchView search;

        private void DrawScene(Rect rect)
        {
            FR2_RefDrawer drawer = isFocusingUses
                ? selection.isSelectingAsset ? null : SceneUsesDrawer
                : selection.isSelectingAsset ? RefInScene : RefSceneInScene;
            if (drawer == null) return;

            if (!FR2_SceneCache.ready)
            {
                Rect rr = rect;
                rr.height = 16f;

                int cur = FR2_SceneCache.Api.current, total = FR2_SceneCache.Api.total;
                EditorGUI.ProgressBar(rr, cur * 1f / total, $"{cur} / {total}");
                WillRepaint = true;
                return;
            }

            drawer.Draw(rect);

            var refreshRect = new Rect(rect.xMax - 16f, rect.yMin - 14f, 18f, 18f);
            if (GUI2.ColorIconButton(refreshRect, FR2_Icon.Refresh.image,
                FR2_SceneCache.Api.Dirty ? GUI2.lightRed : Color.white))
            {
                FR2_SceneCache.Api.refreshCache(drawer.window,true);
            }
        }



        private FR2_RefDrawer GetAssetDrawer()
        {
            if (isFocusingUses) return selection.isSelectingAsset ? UsesDrawer : SceneToAssetDrawer;
            if (isFocusingUsedBy) return selection.isSelectingAsset ? UsedByDrawer : null;
            if (isFocusingAddressable) return AddressableDrawer.drawer;
            return null;
        }

        private void DrawAsset(Rect rect)
        {
            FR2_RefDrawer drawer = GetAssetDrawer();
            if (drawer == null) return;
            drawer.Draw(rect);

            if (!drawer.showDetail) return;

            settings.details = true;
            drawer.showDetail = false;
            sp1.splits[2].visible = settings.details;
            sp1.CalculateWeight();
            Repaint();
        }

        private void DrawSearch()
        {
            if (search == null) search = new FR2_SearchView();
            search.DrawLayout();
        }

        protected override void OnGUI()
        {
            // UnityEngine.Profiling.Profiler.BeginSample("FR2-OnGUI");
            // {
            OnGUI2();

            // }
            // UnityEngine.Profiling.Profiler.EndSample();
        }

        protected void DrawScanProject()
        {
            bool writeImportLog = settings.writeImportLog;
            settings.writeImportLog = EditorGUILayout.Toggle("Write Import Log", settings.writeImportLog);
            if (writeImportLog != settings.writeImportLog)
            {
                EditorUtility.SetDirty(this);
            }

            if (GUILayout.Button("Scan project"))
            {
                FR2_Asset.shouldWriteImportLog = writeImportLog;
                FR2_Cache.DeleteCache();
                FR2_Cache.CreateCache();
            }
        }

        protected bool CheckDrawImport()
        {
            if (FR2_Unity.isEditorCompiling)
            {
                EditorGUILayout.HelpBox("Compiling scripts, please wait!", MessageType.Warning);
                Repaint();
                return false;
            }

            if (FR2_Unity.isEditorUpdating)
            {
                EditorGUILayout.HelpBox("Importing assets, please wait!", MessageType.Warning);
                Repaint();
                return false;
            }

            InitIfNeeded();

            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorGUILayout.HelpBox("FR2 requires serialization mode set to FORCE TEXT!", MessageType.Warning);
                if (GUILayout.Button("FORCE TEXT")) EditorSettings.serializationMode = SerializationMode.ForceText;

                return false;
            }

            if (FR2_Cache.hasCache && !FR2_Cache.CheckSameVersion())
            {
                EditorGUILayout.HelpBox("Incompatible cache version found!!!\nFR2 will need a full refresh and according to your project's size this process may take several minutes to complete finish!",
                    MessageType.Warning);

                DrawScanProject();
                return false;
            }

            if (FR2_Cache.isReady) return DrawEnable();

            if (!FR2_Cache.hasCache)
            {
                EditorGUILayout.HelpBox(
                    "FR2 cache not found!\nA first scan is needed to build the cache for all asset references.\nDepending on the size of your project, this process may take a few minutes to complete but once finished, searching for asset references will be incredibly fast!",
                    MessageType.Warning);

                // FR2_Cache.DrawPriorityGUI();

                DrawScanProject();
                return false;
            }

            // FR2_Cache.DrawPriorityGUI();
            if (!DrawEnable()) return false;

            FR2_Cache api = FR2_Cache.Api;
            if (api.workCount > 0)
            {
                string text = "Refreshing ... " + (int)(api.progress * api.workCount) + " / " + api.workCount;

                // Show current asset being processed
                if (!string.IsNullOrEmpty(api.currentAssetName))
                {
                    EditorGUILayout.LabelField(api.currentAssetName, EditorStyles.miniLabel);
                }

                Rect rect = GUILayoutUtility.GetRect(1f, Screen.width, 18f, 18f);
                EditorGUI.ProgressBar(rect, api.progress, text);
                Repaint();
            } else
            {
                // Debug.LogWarning("DONE????");
                api.workCount = 0;
                api.ready = true;
            }

            return false;
        }

        protected bool isFocusingUses => (tabs != null) && (tabs.current == 0);
        protected bool isFocusingUsedBy => (tabs != null) && (tabs.current == 1);
        protected bool isFocusingAddressable => (tabs != null) && (tabs.current == 2);

        // 
        protected bool isFocusingDuplicate => (toolTabs != null) && (toolTabs.current == 0);
        protected bool isFocusingGUIDs => (toolTabs != null) && (toolTabs.current == 1);
        protected bool isFocusingUnused => (toolTabs != null) && (toolTabs.current == 2);
        protected bool isFocusingUsedInBuild => (toolTabs != null) && (toolTabs.current == 3);
        protected bool isFocusingOthers => (toolTabs != null) && (toolTabs.current == 4);

        private static readonly HashSet<FR2_RefDrawer.Mode> allowedModes = new HashSet<FR2_RefDrawer.Mode>
        {
            FR2_RefDrawer.Mode.Type,
            FR2_RefDrawer.Mode.Extension,
            FR2_RefDrawer.Mode.Folder
        };

        private void OnTabChange()
        {
            if (isFocusingUnused || isFocusingUsedInBuild)
            {
                if (!allowedModes.Contains(settings.groupMode))
                {
                    settings.groupMode = FR2_RefDrawer.Mode.Type;
                }
            }

            if (deleteUnused != null) deleteUnused.hasConfirm = false;
            if (UsedInBuild != null) UsedInBuild.SetDirty();
        }

        private void InitTabs()
        {
            bottomTabs = FR2_TabView.Create(this, true,
                new GUIContent(FR2_Icon.Setting.image, "Settings"),
                new GUIContent(FR2_Icon.Ignore.image, "Ignore"),
                new GUIContent(FR2_Icon.Filter.image, "Filter by Type")
            );
            bottomTabs.current = -1;
            bottomTabs.flexibleWidth = false;

            toolTabs = FR2_TabView.Create(this, false, "Duplicate", "GUID", "Unused", "In Build", "Others");
            toolTabs.current = settings.toolTabIndex;
            toolTabs.onTabChange = () => { settings.toolTabIndex = toolTabs.current; };

            if (FR2_Addressable.asmStatus == FR2_Addressable.ASMStatus.AsmNotFound)
            { // No Addressable
                tabs = FR2_TabView.Create(this, false, // , "Tools"
                    "Uses", "Used By"
                );
            } else
            {
                tabs = FR2_TabView.Create(this, false, // , "Tools"
                    "Uses", "Used By", "Addressables"
                );
            }
            tabs.current = settings.mainTabIndex;
            tabs.onTabChange = () => { settings.mainTabIndex = tabs.current; OnTabChange(); };

            const float IconW = 24f;
            tabs.offsetFirst = IconW;
            tabs.offsetLast = IconW * 5;

            tabs.callback = new DrawCallback
            {
                BeforeDraw = rect =>
                {
                    rect.width = IconW;
                    if (GUI2.ToolbarToggle(ref selection.isLock,
                        selection.isLock ? FR2_Icon.Lock.image : FR2_Icon.Unlock.image,
                        Vector2.zero, "Lock Selection", rect))
                    {
                        WillRepaint = true;
                        OnSelectionChange();
                        if (selection.isLock) AddHistory();
                    }
                },

                AfterDraw = rect =>
                {
                    rect.xMin = rect.xMax - IconW * 5;
                    rect.width = IconW;

                    if (GUI2.ToolbarToggle(ref settings.selection,
                        FR2_Icon.Selection.image,
                        Vector2.zero, "Show / Hide Selection", rect))
                    {
                        sp1.splits[0].visible = settings.selection;
                        sp1.CalculateWeight();
                        Repaint();
                    }

                    rect.x += IconW;
                    if (GUI2.ToolbarToggle(ref settings.scene, FR2_Icon.Scene.image, Vector2.zero, "Show / Hide Scene References", rect))
                    {
                        if ((settings.asset == false) && (settings.scene == false))
                        {
                            settings.asset = true;
                            sp2.splits[1].visible = settings.asset;
                        }

                        RefreshPanelVisible();
                        Repaint();
                    }

                    rect.x += IconW;
                    if (GUI2.ToolbarToggle(ref settings.asset, FR2_Icon.Asset.image, Vector2.zero, "Show / Hide Asset References", rect))
                    {
                        if ((settings.asset == false) && (settings.scene == false))
                        {
                            settings.scene = true;
                            sp2.splits[0].visible = settings.scene;
                        }

                        RefreshPanelVisible();
                        Repaint();
                    }

                    rect.x += IconW;
                    if (GUI2.ToolbarToggle(ref settings.details, FR2_Icon.Details.image, Vector2.zero, "Show / Hide Details", rect))
                    {
                        sp1.splits[2].visible = settings.details;
                        sp1.CalculateWeight();
                        Repaint();
                    }

                    rect.x += IconW;
                    if (GUI2.ToolbarToggle(ref settings.bookmark, FR2_Icon.Favorite.image, Vector2.zero, "Show / Hide Bookmarks", rect))
                    {
                        sp1.splits[3].visible = settings.bookmark;
                        sp1.CalculateWeight();
                        Repaint();
                    }
                }
            };
        }

        protected bool DrawFooter()
        {
            bottomTabs.DrawLayout();
            Rect bottomBar = GUILayoutUtility.GetLastRect();
            bottomBar.xMin += 100f; // offset for left buttons

            var (fullPathRect, flex1) = bottomBar.ExtractLeft(24f);
            var (fileSizeRect, flex2) = flex1.ExtractLeft(24f);
            var (extensionRect, flex3) = flex2.ExtractLeft(24f);

            var (buttonRect, _) = flex3.ExtractRight(24f);
            bottomBar = flex3;

            Rect viewModeRect = bottomBar;
            viewModeRect.xMax -= 24f;
            viewModeRect.xMin = viewModeRect.xMax - 200f;

            DrawViewModes(viewModeRect);
            DrawButton(buttonRect, ref settings.toolMode, FR2_Icon.CustomTool);
            if (DrawButton(fullPathRect, ref settings.showFullPath, FR2_Icon.FullPath))
            {
                RefreshShowFullPath();
            }
            if (DrawButton(fileSizeRect, ref settings.showFileSize, FR2_Icon.Filesize))
            {
                RefreshShowFileSize();
            }
            if (DrawButton(extensionRect, ref settings.showFileExtension, FR2_Icon.FileExtension))
            {
                RefreshShowFileExtension();
            }

            return false;
        }


        private bool DrawButton(Rect rect, ref bool show, GUIContent icon)
        {
            var changed = false;
            Color oColor = GUI.color;
            if (show) GUI.color = new Color(0.7f, 1f, 0.7f, 1f);
            {
                if (GUI.Button(rect, icon, EditorStyles.toolbarButton))
                {
                    show = !show;
                    EditorUtility.SetDirty(this);
                    WillRepaint = true;
                    changed = true;
                }
            }
            GUI.color = oColor;
            return changed;
        }

        private void DrawAssetViewSettings()
        {
            bool isDisable = !sp2.splits[1].visible;
            EditorGUI.BeginDisabledGroup(isDisable);
            {
                GUI2.ToolbarToggle(ref FR2_Setting.s.displayAssetBundleName, FR2_Icon.AssetBundle.image, Vector2.zero, "Show / Hide Assetbundle Names");
#if UNITY_2017_1_OR_NEWER
                GUI2.ToolbarToggle(ref FR2_Setting.s.displayAtlasName, FR2_Icon.Atlas.image, Vector2.zero, "Show / Hide Atlas packing tags");
#endif
                GUI2.ToolbarToggle(ref FR2_Setting.s.showUsedByClassed, FR2_Icon.Material.image, Vector2.zero, "Show / Hide usage icons");

                // GUI2.ToolbarToggle(ref FR2_Setting.s.displayFileSize, FR2_Icon.Filesize.image, Vector2.zero, "Show / Hide file size");

                if (GUILayout.Button("CSV", EditorStyles.toolbarButton)) OnCSVClick();
            }
            EditorGUI.EndDisabledGroup();
        }

        private FR2_EnumDrawer groupModeED;
        private FR2_EnumDrawer toolModeED;
        private FR2_EnumDrawer sortModeED;

        private void DrawViewModes(Rect rect)
        {
            Rect rect1 = rect;
            rect1.width = rect.width / 2f;

            Rect rect2 = rect1;
            rect2.x += rect1.width;

            if (toolModeED == null)
            {
                toolModeED = new FR2_EnumDrawer
                {
                    fr2_enum = new FR2_EnumDrawer.EnumInfo(
                        FR2_RefDrawer.Mode.Type,
                        FR2_RefDrawer.Mode.Folder,
                        FR2_RefDrawer.Mode.Extension
                    )
                };
            }
            if (groupModeED == null) groupModeED = new FR2_EnumDrawer { tooltip = "Group By" };
            if (sortModeED == null) sortModeED = new FR2_EnumDrawer { tooltip = "Sort By" };

            if (settings.toolMode)
            {
                FR2_RefDrawer.Mode tMode = settings.toolGroupMode;
                if (toolModeED.Draw(rect1, ref tMode))
                {
                    settings.toolGroupMode = tMode;
                    markDirty();
                    RefreshSort();
                }
            } else
            {
                FR2_RefDrawer.Mode gMode = settings.groupMode;
                if (groupModeED.Draw(rect1, ref gMode))
                {
                    // Debug.Log($"GroupMode: {gMode}");
                    settings.groupMode = gMode;
                    markDirty();
                    RefreshSort();
                }
            }

            // GUILayout.Space(16f);
            FR2_RefDrawer.Sort sMode = settings.sortMode;
            if (sortModeED.Draw(rect2, ref sMode))
            {
                // Debug.Log($"sortMode: {sMode}");
                settings.sortMode = sMode;
                RefreshSort();
            }


        }

        // Save status to temp variable so the result will be consistent between Layout & Repaint
        internal static int delayRepaint;
        internal static bool checkDrawImportResult;


        protected void OnGUI2()
        {
            if (Event.current.type == EventType.Layout)
            {
                FR2_Unity.RefreshEditorStatus();
            }

            if (FR2_SettingExt.disable)
            {
                DrawEnable();
                return;
            }

            // GUILayout.Label($"OnGUI2: \ndisable={FR2_SettingExt.disable} | \nInited={FR2_CacheHelper.inited} | \nisReady={FR2_Cache.isReady}");
            if (!FR2_CacheHelper.inited)
            {
                FR2_CacheHelper.InitHelper();
            }

            if (tabs == null) InitTabs();
            if (sp1 == null) InitPanes();

            bool result = CheckDrawImport();
            if (Event.current.type == EventType.Layout)
            {
                checkDrawImportResult = result;
            }

            if (!checkDrawImportResult)
            {
                return;
            }

            if (settings.toolMode)
            {
                if (!FR2_SettingExt.hideToolsWarning)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox(FR2_GUIContent.From("Tools are POWERFUL & DANGEROUS! Only use if you know what you are doing!!!", FR2_Icon.Warning.image));
                    if (GUILayout.Button("  x", EditorStyles.label, GUILayout.Width(20), GUILayout.Height(38)))
                    {
                        FR2_SettingExt.hideToolsWarning = true;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                DrawGitWarningIfNeeded();
                
                toolTabs.DrawLayout();
                DrawTools();
            } else
            {
                DrawGitWarningIfNeeded();
                
                tabs.DrawLayout();
                sp1.DrawLayout();
            }

            DrawSettings();
            DrawFooter();
            if (!WillRepaint) return;
            WillRepaint = false;
            Repaint();
        }
        
        private void DrawGitWarningIfNeeded()
        {
            if (!FR2_SettingExt.isGitProject || FR2_SettingExt.gitIgnoreAdded || FR2_SettingExt.hideGitIgnoreWarning) return;
            
            EditorGUILayout.BeginHorizontal();
            
            // Left side: Warning message
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.HelpBox("You should add **/FR2_Cache.asset* to your .gitignore file to avoid committing cache files.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            
            // Right side: Buttons stacked vertically
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            
            if (GUILayout.Button("Apply", GUILayout.Height(19)))
            {
                FR2_GitUtil.AddFR2CacheToGitIgnore();
                FR2_SettingExt.gitIgnoreAdded = true;
            }
            
            if (GUILayout.Button("Ignore", GUILayout.Height(19)))
            {
                FR2_SettingExt.hideGitIgnoreWarning = true;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }

        private FR2_DeleteButton deleteUnused;

        // [NonSerialized] private int _othersTab = 0; // 0: Missing, 1: Organizer, 2: DeleteEmptyFolder

        private void DrawTools()
        {
            if (isFocusingDuplicate)
            {
                Duplicated.DrawLayout();
                GUILayout.FlexibleSpace();
                return;
            }

            if (isFocusingUnused)
            {
                if ((RefUnUse.refs != null) && (RefUnUse.refs.Count == 0))
                {
                    EditorGUILayout.HelpBox("Clean! Your project does not has have any unused assets!", MessageType.Info);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.HelpBox("Your deleted assets was backup at Library/FR2/ just in case you want your assets back!", MessageType.Info);
                } else
                {
                    RefUnUse.DrawLayout();

                    if (deleteUnused == null)
                    {
                        deleteUnused = new FR2_DeleteButton
                        {
                            warningMessage = "A backup (.unitypackage) will be created so you can reimport the deleted assets later!",
                            deleteLabel = FR2_GUIContent.From("DELETE ASSETS", FR2_Icon.Delete.image),
                            confirmMessage = "Create backup at Library/FR2/"
                        };
                    }

                    GUILayout.BeginHorizontal();
                    {
                        deleteUnused.Draw(() => { FR2_Unity.BackupAndDeleteAssets(RefUnUse.source); });
                    }
                    GUILayout.EndHorizontal();
                }
                return;
            }

            if (isFocusingUsedInBuild)
            {
                UsedInBuild.DrawLayout();
                return;
            }

            if (isFocusingOthers)
            {
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                // Left: Vertical tab bar
                EditorGUILayout.BeginVertical(GUILayout.Width(160));
                var tabStyle = new GUIStyle(EditorStyles.toolbarButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = 32f,
                    fontStyle = FontStyle.Bold
                };
                Color origColor = GUI.backgroundColor;
                for (var i = 0; i < 3; i++)
                {
                    string label = i == 0 ? "Missing Scripts" : i == 1 ? "Organize Assets" : "Delete Empty Folders";
                    GUI.backgroundColor = (settings.othersTabIndex == i) ? new Color(0.7f, 0.9f, 1f, 1f) : origColor;
                    if (GUILayout.Toggle(settings.othersTabIndex == i, label, tabStyle))
                    {
                        if (settings.othersTabIndex != i) { settings.othersTabIndex = i; WillRepaint = true; }
                    }
                }
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = origColor;
                EditorGUILayout.EndVertical();
                // Right: Tool content
                EditorGUILayout.BeginVertical();
                if (settings.othersTabIndex == 0) { MissingReference.DrawLayout(); }
                else if (settings.othersTabIndex == 1) { AssetOrganizer.DrawLayout(); }
                else { DeleteEmptyFolder.DrawLayout(); }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (isFocusingGUIDs)
            {
                DrawGUIDs();
            }
        }

        private void DrawSettings()
        {
            if (bottomTabs.current == -1) return;

            GUILayout.BeginVertical(GUILayout.Height(100f));
            {
                GUILayout.Space(2f);
                switch (bottomTabs.current)
                {
                case 0:
                    {
                        FR2_Setting.s.DrawSettings();

                        // Add the Write Import Log toggle in the settings
                        GUILayout.Space(5f);
                        EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);

                        bool writeLog = settings.writeImportLog;
                        settings.writeImportLog = EditorGUILayout.Toggle("Write Import Log", settings.writeImportLog);
                        if (writeLog != settings.writeImportLog)
                        {
                            EditorUtility.SetDirty(this);
                        }
                        
                        // Add Git settings if applicable
                        if (FR2_SettingExt.isGitProject)
                        {
                            GUILayout.Space(5f);
                            EditorGUILayout.LabelField("Git Settings", EditorStyles.boldLabel);
                            
                            if (FR2_SettingExt.gitIgnoreAdded)
                            {
                                EditorGUILayout.HelpBox("FR2_Cache.asset* is already in your .gitignore file.", MessageType.Info);
                            }
                            else
                            {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Add FR2_Cache.asset* to .gitignore");
                                if (GUILayout.Button("Apply", GUILayout.Width(100)))
                                {
                                    FR2_GitUtil.AddFR2CacheToGitIgnore();
                                    FR2_SettingExt.gitIgnoreAdded = true;
                                    FR2_SettingExt.hideGitIgnoreWarning = true;
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        break;
                    }

                case 1:
                    {
                        if (FR2_AssetGroupDrawer.DrawIgnoreFolder()) markDirty();
                        break;
                    }

                case 2:
                    {
                        if (FR2_AssetGroupDrawer.DrawSearchFilter()) markDirty();
                        break;
                    }
                }
            }
            GUILayout.EndVertical();

            Rect rect = GUILayoutUtility.GetLastRect();
            rect.height = 1f;
            GUI2.Rect(rect, Color.black, 0.4f);
        }

        protected void markDirty()
        {
            UsedByDrawer.SetDirty();
            UsesDrawer.SetDirty();
            Duplicated.SetDirty();
            AddressableDrawer.RefreshSort();
            SceneToAssetDrawer.SetDirty();
            RefUnUse.SetDirty();

            RefInScene.SetDirty();
            RefSceneInScene.SetDirty();
            SceneUsesDrawer.SetDirty();
            UsedInBuild.SetDirty();
            WillRepaint = true;
        }

        protected void RefreshShowFileExtension()
        {
            RefUnUse.drawExtension = settings.showFileExtension;
            UsesDrawer.drawExtension = settings.showFileExtension;
            UsedByDrawer.drawExtension = settings.showFileExtension;
            SceneToAssetDrawer.drawExtension = settings.showFileExtension;
            RefInScene.drawExtension = settings.showFileExtension;
            SceneUsesDrawer.drawExtension = settings.showFileExtension;
            RefSceneInScene.drawExtension = settings.showFileExtension;
        }

        protected void RefreshShowFullPath()
        {
            RefUnUse.drawFullPath = settings.showFullPath;
            UsesDrawer.drawFullPath = settings.showFullPath;
            UsedByDrawer.drawFullPath = settings.showFullPath;
            SceneToAssetDrawer.drawFullPath = settings.showFullPath;
            RefInScene.drawFullPath = settings.showFullPath;
            SceneUsesDrawer.drawFullPath = settings.showFullPath;
            RefSceneInScene.drawFullPath = settings.showFullPath;
        }

        protected void RefreshShowFileSize()
        {
            RefUnUse.drawFileSize = settings.showFileSize;
            UsesDrawer.drawFileSize = settings.showFileSize;
            UsedByDrawer.drawFileSize = settings.showFileSize;
            SceneToAssetDrawer.drawFileSize = settings.showFileSize;
            RefInScene.drawFileSize = settings.showFileSize;
            SceneUsesDrawer.drawFileSize = settings.showFileSize;
            RefSceneInScene.drawFileSize = settings.showFileSize;
        }

        protected void RefreshShowUsageType()
        {
            RefUnUse.drawUsageType = settings.showUsageType;
            UsesDrawer.drawUsageType = settings.showUsageType;
            UsedByDrawer.drawUsageType = settings.showUsageType;
            SceneToAssetDrawer.drawUsageType = settings.showUsageType;
            RefInScene.drawUsageType = settings.showUsageType;
            SceneUsesDrawer.drawUsageType = settings.showUsageType;
            RefSceneInScene.drawUsageType = settings.showUsageType;
        }



        protected void RefreshSort()
        {
            UsedByDrawer.RefreshSort();
            UsesDrawer.RefreshSort();
            AddressableDrawer.RefreshSort();

            Duplicated.RefreshSort();
            SceneToAssetDrawer.RefreshSort();
            RefUnUse.RefreshSort();
            UsedInBuild.RefreshSort();
        }

        // public bool isExcludeByFilter;

        // protected bool checkNoticeFilter()
        // {
        //     var rsl = false;
        //
        //     if (IsFocusingUsedBy && !rsl) rsl = UsedByDrawer.isExclueAnyItem();
        //
        //     if (IsFocusingDuplicate) return Duplicated.isExclueAnyItem();
        //
        //     if (IsFocusingUses && (rsl == false)) rsl = UsesDrawer.isExclueAnyItem();
        //
        //     //tab use by
        //     return rsl;
        // }
        //
        // protected bool checkNoticeIgnore()
        // {
        //     bool rsl = isNoticeIgnore;
        //     return rsl;
        // }


        private Dictionary<string, UnityObject> guidObjs;
        private string[] ids;

        private void DrawGUIDs()
        {
            GUILayout.Label("GUID to Object", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            {
                string guid = EditorGUILayout.TextField(tempGUID ?? string.Empty);
                string fileId = EditorGUILayout.TextField(tempFileID ?? string.Empty);
                EditorGUILayout.ObjectField(tempObject, typeof(UnityObject), false, GUI2.GLW_160);

                if (GUILayout.Button("Paste", EditorStyles.miniButton, GUI2.GLW_70))
                {
                    string[] split = EditorGUIUtility.systemCopyBuffer.Split('/');
                    guid = split[0];
                    fileId = split.Length == 2 ? split[1] : string.Empty;
                }

                if ((guid != tempGUID || fileId != tempFileID) && !string.IsNullOrEmpty(guid))
                {
                    tempGUID = guid;
                    tempFileID = fileId;
                    string fullId = string.IsNullOrEmpty(fileId) ? tempGUID : tempGUID + "/" + tempFileID;

                    tempObject = FR2_Unity.LoadAssetAtPath<UnityObject>
                    (
                        AssetDatabase.GUIDToAssetPath(fullId)
                    );
                }

                if (GUILayout.Button("Set FileID"))
                {
                    var newDict = new Dictionary<string, UnityObject>();
                    foreach (KeyValuePair<string, UnityObject> kvp in guidObjs)
                    {
                        string key = kvp.Key.Split('/')[0];
                        if (!string.IsNullOrEmpty(fileId)) key = key + "/" + fileId;

                        var value = FR2_Unity.LoadAssetAtPath<UnityObject>
                        (
                            AssetDatabase.GUIDToAssetPath(key)
                        );
                        newDict.Add(key, value);
                    }

                    guidObjs = newDict;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
            if (guidObjs == null) // || ids == null)
            {
                return;
            }

            //GUILayout.Label("Selection", EditorStyles.boldLabel);
            //if (ids.Length == guidObjs.Count)
            {
                scrollPos = GUILayout.BeginScrollView(scrollPos);
                {
                    //for (var i = 0; i < ids.Length; i++)
                    foreach (KeyValuePair<string, UnityObject> item in guidObjs)
                    {
                        //if (!guidObjs.ContainsKey(ids[i])) continue;

                        GUILayout.BeginHorizontal();
                        {
                            //var obj = guidObjs[ids[i]];
                            UnityObject obj = item.Value;

                            EditorGUILayout.ObjectField(obj, typeof(UnityObject), false, GUI2.GLW_150);
                            string idi = item.Key;
                            GUILayout.TextField(idi, GUI2.GLW_320);
                            if (GUILayout.Button(FR2_GUIContent.FromString("Copy"), EditorStyles.miniButton, GUI2.GLW_50))
                            {
                                tempObject = obj;

                                //EditorGUIUtility.systemCopyBuffer = tempGUID = item.Key;
                                string[] arr = item.Key.Split('/');
                                tempGUID = arr[0];
                                tempFileID = arr[1];

                                //string guid = "";
                                //long file = -1;
                                //if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out file))
                                //{
                                //    EditorGUIUtility.systemCopyBuffer = tempGUID = idi + "/" + file;

                                //    if (!string.IsNullOrEmpty(tempGUID))
                                //    {
                                //        tempObject = obj;
                                //    }
                                //}  
                            }

                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Merge Selection To"))
            {
                string fullId = string.IsNullOrEmpty(tempFileID) ? tempGUID : tempGUID + "/" + tempFileID;
                FR2_Export.MergeDuplicate(fullId);
            }

            EditorGUILayout.ObjectField(tempObject, typeof(UnityObject), false, GUI2.GLW_120);
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }
    }
}
