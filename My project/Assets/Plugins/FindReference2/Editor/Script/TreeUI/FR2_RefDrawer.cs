using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    internal partial class FR2_RefDrawer : IRefDraw
    {

        public static GUIStyle toolbarSearchField;
        public static GUIStyle toolbarSearchFieldCancelButton;
        public static GUIStyle toolbarSearchFieldCancelButtonEmpty;
        private readonly Dictionary<string, BookmarkInfo> gBookmarkCache = new Dictionary<string, BookmarkInfo>();
        private readonly Func<Mode> getGroupMode;

        private readonly Func<Sort> getSortMode;

        internal readonly FR2_TreeUI2.GroupDrawer groupDrawer;

        public readonly List<FR2_Asset> highlight = new List<FR2_Asset>();

        // FILTERING
        private readonly string searchTerm = string.Empty;
        private readonly bool showSearch = true;
        public Action<Rect, FR2_Ref> afterItemDraw;
        public Action<Rect, FR2_Ref> beforeItemDraw;
        public bool caseSensitive = false;

        public Action<Rect, string, int> customDrawGroupLabel;

        public Func<FR2_Ref, string> customGetGroup;

        // STATUS
        private bool dirty;
        public bool drawExtension;
        public bool drawFileSize;

        public bool drawFullPath;
        public bool drawUsageType;
        private int excludeCount;
        public bool forceHideDetails;

        public string level0Group;
        internal List<FR2_Ref> list;
        public string messageEmpty = "It's empty!";

        public string messageNoRefs = "Do select something!";
        internal Dictionary<string, FR2_Ref> refs;
        private bool selectFilter;
        public bool showDetail;
        private bool showIgnore;


        public FR2_RefDrawer(IWindow window, Func<Sort> getSortMode, Func<Mode> getGroupMode)
        {
            this.window = window;
            this.getSortMode = getSortMode;
            this.getGroupMode = getGroupMode;
            groupDrawer = new FR2_TreeUI2.GroupDrawer(DrawGroup, DrawAsset);
        }



        // ORIGINAL
        internal FR2_Ref[] source => FR2_Ref.FromList(list);

        public IWindow window { get; set; }
        public bool Draw(Rect rect)
        {
            if (refs == null || refs.Count == 0)
            {
                DrawEmpty(rect, messageNoRefs);
                return false;
            }

            if (dirty || list == null) ApplyFilter();

            if (!groupDrawer.hasChildren)
            {
                DrawEmpty(rect, messageEmpty);
            } else
            {
                groupDrawer.Draw(rect);
            }
            return false;
        }

        public bool DrawLayout()
        {
            if (refs == null || refs.Count == 0) return false;

            if (dirty || list == null) ApplyFilter();

            groupDrawer.DrawLayout();
            return false;
        }

        public int ElementCount()
        {
            if (refs == null) return 0;

            return refs.Count;

            // return refs.Where(x => x.Value.depth != 0).Count();
        }

        private void DrawEmpty(Rect rect, string text)
        {
            rect = GUI2.Padding(rect, 2f, 2f);
            rect.height = 45f;

            EditorGUI.HelpBox(rect, text, MessageType.Info);
        }
        public void SetRefs(Dictionary<string, FR2_Ref> dictRefs)
        {
            refs = dictRefs;
            dirty = true;
        }

        private void SetBookmarkGroup(string groupLabel, bool willbookmark)
        {
            string[] ids = groupDrawer.GetChildren(groupLabel);
            BookmarkInfo info = GetBMInfo(groupLabel);

            for (var i = 0; i < ids.Length; i++)
            {
                FR2_Ref rf;
                if (!refs.TryGetValue(ids[i], out rf)) continue;

                if (willbookmark)
                {
                    FR2_Bookmark.Add(rf);
                } else
                {
                    FR2_Bookmark.Remove(rf);
                }
            }

            info.count = willbookmark ? info.total : 0;
        }

        private BookmarkInfo GetBMInfo(string groupLabel)
        {
            BookmarkInfo info = null;
            if (!gBookmarkCache.TryGetValue(groupLabel, out info))
            {
                string[] ids = groupDrawer.GetChildren(groupLabel);

                info = new BookmarkInfo();
                for (var i = 0; i < ids.Length; i++)
                {
                    FR2_Ref rf;
                    if (!refs.TryGetValue(ids[i], out rf)) continue;
                    info.total++;

                    bool isBM = FR2_Bookmark.Contains(rf);
                    if (isBM) info.count++;
                }

                gBookmarkCache.Add(groupLabel, info);
            }

            return info;
        }

        private void DrawToggleGroup(Rect r, string groupLabel)
        {
            BookmarkInfo info = GetBMInfo(groupLabel);
            bool selectAll = info.count == info.total;
            r.width = 16f;
            if (GUI2.Toggle(r, ref selectAll)) SetBookmarkGroup(groupLabel, selectAll);

            if (!selectAll && (info.count > 0))
            {
                //GUI.DrawTexture(r, EditorStyles.
            }
        }

        private void DrawGroup(Rect r, string label, int childCount)
        {
            if (string.IsNullOrEmpty(label)) label = "(none)";
            DrawToggleGroup(r, label);
            r.xMin += 18f;

            Mode groupMode = getGroupMode();
            if (groupMode == Mode.Folder)
            {
                Texture tex = AssetDatabase.GetCachedIcon("Assets");
                GUI.DrawTexture(new Rect(r.x, r.y, 16f, 16f), tex);
                r.xMin += 16f;
            }

            if (customDrawGroupLabel != null)
            {
                customDrawGroupLabel.Invoke(r, label, childCount);
            } else
            {
                GUIContent lbContent = FR2_GUIContent.FromString(label);
                GUI.Label(r, lbContent, EditorStyles.boldLabel);

                Rect cRect = r;
                cRect.x += EditorStyles.boldLabel.CalcSize(lbContent).x;
                cRect.y += 1f;
                GUI.Label(cRect, FR2_GUIContent.FromString($"({childCount})"), EditorStyles.miniLabel);
            }

            bool hasMouse = (Event.current.type == EventType.MouseUp) && r.Contains(Event.current.mousePosition);
            if (hasMouse && (Event.current.button == 1))
            {
                var menu = new GenericMenu();
                menu.AddItem(FR2_GUIContent.FromString("Add Bookmark"), false, () => { SetBookmarkGroup(label, true); });
                menu.AddItem(FR2_GUIContent.FromString("Remove Bookmark"), false, () =>
                {
                    SetBookmarkGroup(label, false);
                });

                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        public void DrawDetails(Rect rect)
        {
            Rect r = rect;
            r.xMin += 18f;
            r.height = 18f;

            for (var i = 0; i < highlight.Count; i++)
            {
                highlight[i].Draw(r,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false, window, false);
                r.y += 18f;
                r.xMin += 18f;
            }
        }

        private void DrawAsset(Rect r, string guid)
        {
            if (!refs.TryGetValue(guid, out FR2_Ref rf)) return;

            if (rf.isSceneRef)
            {
                if (rf.component == null) return;
                if (!(rf is FR2_SceneRef re)) return;
                beforeItemDraw?.Invoke(r, rf);

                // r.x -= 16f;
                rf.DrawToogleSelect(r);
                r.xMin += 32f;
                re.Draw(r, window, getGroupMode(), !forceHideDetails);
            } else
            {
                beforeItemDraw?.Invoke(r, rf);

                // r.xMin -= 16f;
                rf.DrawToogleSelect(r);
                r.xMin += 32f;

                float w2 = (r.x + r.width) / 2f;
                var rRect = new Rect(w2, r.y, w2, r.height);
                bool isClick = (Event.current.type == EventType.MouseDown) && (Event.current.button == 0);

                if (isClick && rRect.Contains(Event.current.mousePosition))
                {
                    showDetail = true;
                    highlight.Clear();
                    highlight.Add(rf.asset);

                    FR2_Asset p = rf.addBy;
                    var cnt = 0;

                    while ((p != null) && refs.ContainsKey(p.guid))
                    {
                        highlight.Add(p);

                        FR2_Ref fr2_ref = refs[p.guid];
                        if (fr2_ref != null) p = fr2_ref.addBy;

                        if (++cnt > 100)
                        {
                            Debug.LogWarning("Break on depth 1000????");
                            break;
                        }
                    }

                    highlight.Sort((item1, item2) =>
                    {
                        int d1 = refs[item1.guid].depth;
                        int d2 = refs[item2.guid].depth;
                        return d1.CompareTo(d2);
                    });

                    // Debug.Log("Highlight: " + highlight.Count + "\n" + string.Join("\n", highlight.ToArray()));
                    Event.current.Use();
                }

                bool isHighlight = highlight.Contains(rf.asset);

                // if (isHighlight)
                // {
                //     var hlRect = new Rect(-20, r.y, 15f, r.height);
                //     GUI2.Rect(hlRect, GUI2.darkGreen);
                // }

                rf.asset.Draw(r,
                    isHighlight,
                    drawFullPath,
                    !forceHideDetails && drawFileSize,
                    !forceHideDetails && FR2_Setting.s.displayAssetBundleName,
                    !forceHideDetails && FR2_Setting.s.displayAtlasName,
                    !forceHideDetails && drawUsageType,
                    window,
                    !forceHideDetails && drawExtension
                );
            }

            afterItemDraw?.Invoke(r, rf);
        }

        private string GetGroup(FR2_Ref rf)
        {
            if (customGetGroup != null) return customGetGroup(rf);

            if (rf.depth == 0) return level0Group;

            if (getGroupMode() == Mode.None) return "(no group)";

            FR2_SceneRef sr = null;
            if (rf.isSceneRef)
            {
                sr = rf as FR2_SceneRef;
                if (sr == null) return null;
            }

            if (!rf.isSceneRef)
            {
                if (rf.asset.IsExcluded)
                {
                    return null; // "(ignored)"
                }
            }

            switch (getGroupMode())
            {
            case Mode.Extension:
                {
                    // if (!rf.isSceneRef) Debug.Log($"Extension: {rf.asset.assetPath} | {rf.asset.extension}");
                    return rf.isSceneRef ? sr.targetType
                        : string.IsNullOrEmpty(rf.asset.extension) ? "(no extension)" : rf.asset.extension;
                }
            case Mode.Type:
                {
                    return rf.isSceneRef ? sr.targetType : FR2_AssetGroupDrawer.FILTERS[rf.type].name;
                }

            case Mode.Folder: return rf.isSceneRef ? sr.scenePath : rf.asset.assetFolder;

            case Mode.Dependency:
                {
                    return rf.depth == 1 ? "Direct Usage" : "Indirect Usage";
                }

            case Mode.Depth:
                {
                    return "Level " + rf.depth;
                }

            case Mode.Atlas: return rf.isSceneRef ? "(not in atlas)" : string.IsNullOrEmpty(rf.asset.AtlasName) ? "(not in atlas)" : rf.asset.AtlasName;
            case Mode.AssetBundle: return rf.isSceneRef ? "(not in assetbundle)" : string.IsNullOrEmpty(rf.asset.AssetBundleName) ? "(not in assetbundle)" : rf.asset.AssetBundleName;
            }

            return "(others)";
        }

        private void SortGroup(List<string> groups)
        {
            groups.Sort((item1, item2) =>
            {
                if (item1.Contains("(")) return 1;
                if (item2.Contains("(")) return -1;

                return string.Compare(item1, item2, StringComparison.Ordinal);
            });
        }

        public FR2_RefDrawer Reset(string[] assetGUIDs, bool isUsage)
        {
            gBookmarkCache.Clear();

            refs = isUsage ? FR2_Ref.FindUsage(assetGUIDs) : FR2_Ref.FindUsedBy(assetGUIDs);
            dirty = true;
            if (list != null) list.Clear();
            return this;
        }

        public void Reset(Dictionary<string, FR2_Ref> newRefs)
        {
            if (refs == null) refs = new Dictionary<string, FR2_Ref>();
            refs.Clear();
            foreach (KeyValuePair<string, FR2_Ref> kvp in newRefs)
            {
                refs.Add(kvp.Key, kvp.Value);
            }
            dirty = true;
            if (list != null) list.Clear();
        }

        public FR2_RefDrawer Reset(GameObject[] objs, bool findDept, bool findPrefabInAsset)
        {
            refs = FR2_Ref.FindUsageScene(objs, findDept);

            var guidss = new List<string>();
            Dictionary<GameObject, HashSet<string>> dependent = FR2_SceneCache.Api.prefabDependencies;
            foreach (GameObject gameObject in objs)
            {
                if (!dependent.TryGetValue(gameObject, out HashSet<string> hash)) continue;
                foreach (string guid in hash)
                {
                    guidss.Add(guid);
                }
            }

            Dictionary<string, FR2_Ref> usageRefs1 = FR2_Ref.FindUsage(guidss.ToArray());
            foreach (KeyValuePair<string, FR2_Ref> kvp in usageRefs1)
            {
                if (refs.ContainsKey(kvp.Key)) continue;

                if (guidss.Contains(kvp.Key)) kvp.Value.depth = 1;

                refs.Add(kvp.Key, kvp.Value);
            }


            if (findPrefabInAsset)
            {
                var guids = new List<string>();
                for (var i = 0; i < objs.Length; i++)
                {
                    string guid = FR2_Unity.GetPrefabParent(objs[i]);
                    if (string.IsNullOrEmpty(guid)) continue;

                    guids.Add(guid);
                }

                Dictionary<string, FR2_Ref> usageRefs = FR2_Ref.FindUsage(guids.ToArray());
                foreach (KeyValuePair<string, FR2_Ref> kvp in usageRefs)
                {
                    if (refs.ContainsKey(kvp.Key)) continue;

                    if (guids.Contains(kvp.Key)) kvp.Value.depth = 1;

                    refs.Add(kvp.Key, kvp.Value);
                }
            }

            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        //ref in scene
        public FR2_RefDrawer Reset(string[] assetGUIDs, IWindow window)
        {
            refs = FR2_SceneRef.FindRefInScene(assetGUIDs, true, SetRefInScene, window);
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        private void SetRefInScene(Dictionary<string, FR2_Ref> data)
        {
            refs = data;
            dirty = true;
            if (list != null) list.Clear();
        }

        //scene in scene
        public FR2_RefDrawer ResetSceneInScene(GameObject[] objs)
        {
            refs = FR2_SceneRef.FindSceneInScene(objs);
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        public FR2_RefDrawer ResetSceneUseSceneObjects(GameObject[] objs)
        {
            refs = FR2_SceneRef.FindSceneUseSceneObjects(objs);
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        public FR2_RefDrawer ResetUnusedAsset()
        {
            List<FR2_Asset> lst = FR2_Cache.Api.ScanUnused();

            refs = lst.ToDictionary(x => x.guid, x => new FR2_Ref(0, 1, x, null));
            dirty = true;
            if (list != null) list.Clear();

            return this;
        }

        public void RefreshSort()
        {
            if (list == null) return;

            if ((list.Count > 0) && (list[0].isSceneRef == false) && (getSortMode() == Sort.Size))
            {
                list = list.OrderByDescending(x => x.asset?.fileSize ?? 0).ToList();
            } else
            {
                list.Sort((r1, r2) =>
                {
                    bool isMixed = r1.isSceneRef ^ r2.isSceneRef;
                    if (isMixed)
                    {
#if FR2_DEBUG
						var sb = new StringBuilder();
						sb.Append("r1: " + r1.ToString());
						sb.AppendLine();
						sb.Append("r2: " +r2.ToString());
						Debug.LogWarning("Mixed compared!\n" + sb.ToString());
#endif

                        int v1 = r1.isSceneRef ? 1 : 0;
                        int v2 = r2.isSceneRef ? 1 : 0;
                        return v2.CompareTo(v1);
                    }

                    if (r1.isSceneRef)
                    {
                        var rs1 = (FR2_SceneRef)r1;
                        var rs2 = (FR2_SceneRef)r2;

                        return SortAsset(rs1.sceneFullPath, rs2.sceneFullPath,
                            rs1.targetType, rs2.targetType,
                            getSortMode() == Sort.Path);
                    }

                    if (r1.asset == null) return -1;
                    if (r2.asset == null) return 1;

                    return SortAsset(
                        r1.asset.assetPath, r2.asset.assetPath,
                        r1.asset.extension, r2.asset.extension,
                        false
                    );
                });
            }

            // clean up list
            for (int i = list.Count - 1; i >= 0; i--)
            {
                FR2_Ref item = list[i];

                if (item.isSceneRef)
                {
                    if (string.IsNullOrEmpty(item.GetSceneObjId())) list.RemoveAt(i);

                    continue;
                }

                if (item.asset == null) list.RemoveAt(i);
            }

            groupDrawer.Reset(list,
                rf =>
                {
                    if (rf == null) return null;
                    return rf.isSceneRef ? rf.GetSceneObjId() : rf.asset?.guid;
                }, GetGroup, SortGroup);
        }

        public bool isExclueAnyItem()
        {
            return excludeCount > 0;
        }

        private void ApplyFilter()
        {
            dirty = false;

            if (refs == null) return;

            if (list == null)
            {
                list = new List<FR2_Ref>();
            } else
            {
                list.Clear();
            }

            int minScore = searchTerm.Length;

            string term1 = searchTerm;
            if (!caseSensitive) term1 = term1.ToLower();

            string term2 = term1.Replace(" ", string.Empty);

            excludeCount = 0;

            foreach (KeyValuePair<string, FR2_Ref> item in refs)
            {
                FR2_Ref r = item.Value;

                if (FR2_Setting.IsTypeExcluded(r.type))
                {
                    excludeCount++;
                    continue; //skip this one
                }

                if (!showSearch || string.IsNullOrEmpty(searchTerm))
                {
                    r.matchingScore = 0;
                    list.Add(r);
                    continue;
                }

                //calculate matching score
                string name1 = r.isSceneRef ? (r as FR2_SceneRef)?.sceneFullPath : r.asset.assetName;
                if (!caseSensitive) name1 = name1?.ToLower();

                string name2 = name1?.Replace(" ", string.Empty);

                int score1 = FR2_Unity.StringMatch(term1, name1);
                int score2 = FR2_Unity.StringMatch(term2, name2);

                r.matchingScore = Mathf.Max(score1, score2);
                if (r.matchingScore > minScore) list.Add(r);
            }

            RefreshSort();
        }

        public void SetDirty()
        {
            dirty = true;
        }

        private int SortAsset(string term11, string term12, string term21, string term22, bool swap)
        {
            //			if (term11 == null) term11 = string.Empty;
            //			if (term12 == null) term12 = string.Empty;
            //			if (term21 == null) term21 = string.Empty;
            //			if (term22 == null) term22 = string.Empty;
            int v1 = string.Compare(term11, term12, StringComparison.Ordinal);
            int v2 = string.Compare(term21, term22, StringComparison.Ordinal);
            return swap ? v1 == 0 ? v2 : v1 : v2 == 0 ? v1 : v2;
        }

        public Dictionary<string, FR2_Ref> getRefs()
        {
            return refs;
        }

        internal class BookmarkInfo
        {
            public int count;
            public int total;
        }
    }
}
