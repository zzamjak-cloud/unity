using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace vietlabs.fr2
{
    internal class FR2_SceneRef : FR2_Ref
    {
        internal static readonly Dictionary<string, Type> CacheType = new Dictionary<string, Type>();


        // ------------------------- Ref in scene
        private static Action<Dictionary<string, FR2_Ref>> onFindRefInSceneComplete;
        private static Dictionary<string, FR2_Ref> refs = new Dictionary<string, FR2_Ref>();
        private static string[] cacheAssetGuids;
        private readonly GUIContent assetNameGC;

        public Func<bool> drawFullPath;
        public string sceneFullPath = "";
        public string scenePath = "";
        public string targetType;
        public HashSet<string> usingType = new HashSet<string>();

        public FR2_SceneRef(int index, int depth, FR2_Asset asset, FR2_Asset by) : base(index, depth, asset, by)
        {
            isSceneRef = false;
        }

        //		public override string ToString()
        //		{
        //			return "SceneRef: " + sceneFullPath;
        //		}

        public FR2_SceneRef(int depth, Object target) : base(0, depth, null, null)
        {
            component = target;
            this.depth = depth;
            isSceneRef = true;
            var obj = target as GameObject;
            if (obj == null)
            {
                var com = target as Component;
                if (com != null) obj = com.gameObject;
            }

            scenePath = FR2_Unity.GetGameObjectPath(obj, false);
            if (component == null) return;

            string cName = component.name ?? "(empty)"; // some components are hidden

            sceneFullPath = scenePath + cName;
            targetType = component.GetType().Name;
            assetNameGC = FR2_GUIContent.FromString(cName);
        }

        public static IWindow window { get; set; }

        public override bool isSelected()
        {
            return (component != null) && FR2_Bookmark.Contains(component);
        }

        public void Draw(Rect r, IWindow window, FR2_RefDrawer.Mode groupMode, bool showDetails)
        {
            bool selected = isSelected();
            DrawToogleSelect(r);

            var margin = 2;
            var left = new Rect(r);
            left.width = r.width / 3f;

            var right = new Rect(r);
            right.xMin += left.width + margin;

            //Debug.Log("draw scene "+ selected);
            if ( /* FR2_Setting.PingRow && */ (Event.current.type == EventType.MouseDown) && (Event.current.button == 0))
            {
                Rect pingRect = FR2_Setting.PingRow
                    ? new Rect(0, r.y, r.x + r.width, r.height)
                    : left;

                if (pingRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.control || Event.current.command)
                    {
                        if (selected)
                        {
                            FR2_Bookmark.Remove(this);
                        } else
                        {
                            FR2_Bookmark.Add(this);
                        }
                        if (window != null) window.Repaint();
                    } else
                    {
                        EditorGUIUtility.PingObject(component);
                    }

                    Event.current.Use();
                }
            }

            EditorGUI.ObjectField(showDetails ? left : r, GUIContent.none, component, typeof(GameObject), true);
            if (!showDetails) return;

            bool drawPath = groupMode != FR2_RefDrawer.Mode.Folder;
            float pathW = drawPath && !string.IsNullOrEmpty(scenePath) ? EditorStyles.miniLabel.CalcSize(FR2_GUIContent.FromString(scenePath)).x : 0;

            // string assetName = component.name;

            // if(usingType!= null && usingType.Count > 0)
            // {
            // 	assetName += " -> ";
            // 	foreach(var item in usingType)
            // 	{
            // 		assetName += item + " - ";
            // 	}
            // 	assetName = assetName.Substring(0, assetName.Length - 3);
            // }
            Color cc = FR2_Cache.Api.setting.SelectedColor;

            var lableRect = new Rect(
                right.x,
                right.y,
                pathW + EditorStyles.boldLabel.CalcSize(assetNameGC).x,
                right.height);

            if (selected)
            {
                Color c = GUI.color;
                GUI.color = cc;
                GUI.DrawTexture(lableRect, EditorGUIUtility.whiteTexture);
                GUI.color = c;
            }

            if (drawPath)
            {
                GUI.Label(LeftRect(pathW, ref right), scenePath, EditorStyles.miniLabel);
                right.xMin -= 4f;
                GUI.Label(right, assetNameGC, EditorStyles.boldLabel);
            } else
            {
                GUI.Label(right, assetNameGC);
            }


            if (!FR2_Setting.ShowUsedByClassed || usingType == null) return;

            float sub = 10;
            var re = new Rect(r.x + r.width - sub, r.y, 20, r.height);
            Type t = null;
            foreach (string item in usingType)
            {
                string name = item;
                if (!CacheType.TryGetValue(item, out t))
                {
                    t = FR2_Unity.GetType(name);

                    // if (t == null)
                    // {
                    // 	continue;
                    // } 
                    CacheType.Add(item, t);
                }

                GUIContent content;
                var width = 0.0f;
                if (!FR2_Asset.cacheImage.TryGetValue(name, out content))
                {
                    if (t == null)
                    {
                        content = FR2_GUIContent.FromString(name);
                    } else
                    {
                        Texture text = EditorGUIUtility.ObjectContent(null, t).image;
                        if (text == null)
                        {
                            content = FR2_GUIContent.FromString(name);
                        } else
                        {
                            content = FR2_GUIContent.FromTexture(text, name);
                        }
                    }


                    FR2_Asset.cacheImage.Add(name, content);
                }

                if (content.image == null)
                {
                    width = EditorStyles.label.CalcSize(content).x;
                } else
                {
                    width = 20;
                }

                re.x -= width;
                re.width = width;

                GUI.Label(re, content);
                re.x -= margin; // margin;
            }


            // var nameW = EditorStyles.boldLabel.CalcSize(new GUIContent(assetName)).x;
        }

        private Rect LeftRect(float w, ref Rect rect)
        {
            rect.x += w;
            rect.width -= w;
            return new Rect(rect.x - w, rect.y, w, rect.height);
        }

        // ------------------------- Scene use scene objects
        public static Dictionary<string, FR2_Ref> FindSceneUseSceneObjects(GameObject[] targets)
        {
            var results = new Dictionary<string, FR2_Ref>();
            GameObject[] objs = Selection.gameObjects;
            for (var i = 0; i < objs.Length; i++)
            {
                if (FR2_Unity.IsInAsset(objs[i])) continue;

                var key = objs[i].GetInstanceID().ToString();
                if (!results.ContainsKey(key)) results.Add(key, new FR2_SceneRef(0, objs[i]));

                Component[] coms = objs[i].GetComponents<Component>();
                Dictionary<Component, HashSet<FR2_SceneCache.HashValue>> SceneCache = FR2_SceneCache.Api.cache;
                for (var j = 0; j < coms.Length; j++)
                {
                    HashSet<FR2_SceneCache.HashValue> hash = null;
                    if (coms[j] == null) continue; // missing component

                    if (SceneCache.TryGetValue(coms[j], out hash))
                    {
                        foreach (FR2_SceneCache.HashValue item in hash)
                        {
                            if (item.isSceneObject)
                            {
                                Object obj = item.target;
                                var key1 = obj.GetInstanceID().ToString();
                                if (!results.ContainsKey(key1)) results.Add(key1, new FR2_SceneRef(1, obj));
                            }
                        }
                    }
                }
            }

            return results;
        }

        // ------------------------- Scene in scene
        public static Dictionary<string, FR2_Ref> FindSceneInScene(GameObject[] targets)
        {
            var results = new Dictionary<string, FR2_Ref>();
            GameObject[] objs = Selection.gameObjects;
            for (var i = 0; i < objs.Length; i++)
            {
                if (FR2_Unity.IsInAsset(objs[i])) continue;

                var key = objs[i].GetInstanceID().ToString();
                if (!results.ContainsKey(key)) results.Add(key, new FR2_SceneRef(0, objs[i]));


                foreach (KeyValuePair<Component, HashSet<FR2_SceneCache.HashValue>> item in FR2_SceneCache.Api.cache)
                foreach (FR2_SceneCache.HashValue item1 in item.Value)
                {
                    // if(item.Key.gameObject.name == "ScenesManager")
                    // Debug.Log(item1.objectReferenceValue);
                    GameObject ob = null;
                    if (item1.target is GameObject)
                    {
                        ob = item1.target as GameObject;
                    } else
                    {
                        var com = item1.target as Component;
                        if (com == null) continue;

                        ob = com.gameObject;
                    }

                    if (ob == null) continue;

                    if (ob != objs[i]) continue;

                    key = item.Key.GetInstanceID().ToString();
                    if (!results.ContainsKey(key)) results.Add(key, new FR2_SceneRef(1, item.Key));

                    (results[key] as FR2_SceneRef).usingType.Add(item1.target.GetType().FullName);
                }
            }

            return results;
        }

        public static Dictionary<string, FR2_Ref> FindRefInScene(
            string[] assetGUIDs, bool depth,
            Action<Dictionary<string, FR2_Ref>> onComplete, IWindow win)
        {
            // var watch = new System.Diagnostics.Stopwatch();
            // watch.Start();
            window = win;
            cacheAssetGuids = assetGUIDs;
            onFindRefInSceneComplete = onComplete;
            if (FR2_SceneCache.ready)
            {
                FindRefInScene();
            } else
            {
                FR2_SceneCache.onReady -= FindRefInScene;
                FR2_SceneCache.onReady += FindRefInScene;
            }

            return refs;
        }

        private static void FindRefInScene()
        {
            refs = new Dictionary<string, FR2_Ref>();
            for (var i = 0; i < cacheAssetGuids.Length; i++)
            {
                FR2_Asset asset = FR2_Cache.Api.Get(cacheAssetGuids[i]);
                if (asset == null) continue;

                Add(refs, asset, 0);

                ApplyFilter(refs, asset);
            }

            if (onFindRefInSceneComplete != null) onFindRefInSceneComplete(refs);

            FR2_SceneCache.onReady -= FindRefInScene;

            //    UnityEngine.Debug.Log("Time find ref in scene " + watch.ElapsedMilliseconds);
        }

        private static void FilterAll(Dictionary<string, FR2_Ref> refs, Object obj, string targetPath)
        {
            // ApplyFilter(refs, obj, targetPath);
        }

        private static void ApplyFilter(Dictionary<string, FR2_Ref> refs, FR2_Asset asset)
        {
            string targetPath = AssetDatabase.GUIDToAssetPath(asset.guid);
            if (string.IsNullOrEmpty(targetPath)) return; // asset not found - might be deleted!

            //asset being moved!
            if (targetPath != asset.assetPath) asset.MarkAsDirty();

            Object target = AssetDatabase.LoadAssetAtPath(targetPath, typeof(Object));
            if (target == null)

                //Debug.LogWarning("target is null");
            {
                return;
            }

            bool targetIsGameobject = target is GameObject;

            if (targetIsGameobject)
            {
                foreach (GameObject item in FR2_Unity.getAllObjsInCurScene())
                {
                    if (FR2_Unity.CheckIsPrefab(item))
                    {
                        string itemGUID = FR2_Unity.GetPrefabParent(item);

                        // Debug.Log(item.name + " itemGUID: " + itemGUID);
                        // Debug.Log(target.name + " asset.guid: " + asset.guid);
                        if (itemGUID == asset.guid) Add(refs, item, 1);
                    }
                }
            }

            string dir = Path.GetDirectoryName(targetPath);
            if (FR2_SceneCache.Api.folderCache.ContainsKey(dir))
            {
                foreach (Component item in FR2_SceneCache.Api.folderCache[dir])
                {
                    if (FR2_SceneCache.Api.cache.ContainsKey(item))
                    {
                        foreach (FR2_SceneCache.HashValue item1 in FR2_SceneCache.Api.cache[item])
                        {
                            if (targetPath == AssetDatabase.GetAssetPath(item1.target)) Add(refs, item, 1);
                        }
                    }
                }
            }
        }

        private static void Add(Dictionary<string, FR2_Ref> refs, FR2_Asset asset, int depth)
        {
            string targetId = asset.guid;
            if (!refs.ContainsKey(targetId)) refs.Add(targetId, new FR2_Ref(0, depth, asset, null));
        }

        private static void Add(Dictionary<string, FR2_Ref> refs, Object target, int depth)
        {
            var targetId = target.GetInstanceID().ToString();
            if (!refs.ContainsKey(targetId)) refs.Add(targetId, new FR2_SceneRef(depth, target));
        }
    }
}
