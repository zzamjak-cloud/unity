﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal class FR2_Selection : IRefDraw
    {
        private static readonly Color PRO = new Color(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color INDIE = new Color(0.1f, 0.1f, 0.1f, 1f);
        private readonly FR2_RefDrawer drawer;
        internal readonly HashSet<string> guidSet = new HashSet<string>();
        internal readonly HashSet<string> instSet = new HashSet<string>(); // Do not reference directly to SceneObject (which might be destroyed anytime)

        // ------------ instance

        private bool dirty;
        internal bool isLock;
        internal Dictionary<string, FR2_Ref> refs;

        public FR2_Selection(IWindow window, Func<FR2_RefDrawer.Sort> getSortMode, Func<FR2_RefDrawer.Mode> getGroupMode)
        {
            this.window = window;
            drawer = new FR2_RefDrawer(window, getSortMode, getGroupMode)
            {
                groupDrawer =
                {
                    hideGroupIfPossible = true
                },
                forceHideDetails = true,
                level0Group = string.Empty
            };

            dirty = true;
            drawer.SetDirty();
        }

        public int Count => guidSet.Count + instSet.Count;

        public bool isSelectingAsset => instSet.Count == 0;

        public IWindow window { get; set; }

        public int ElementCount()
        {
            return refs?.Count ?? 0;
        }

        public bool DrawLayout()
        {
            if (dirty) RefreshView();
            return drawer.DrawLayout();
        }

        public bool Draw(Rect rect)
        {
            if (dirty) RefreshView();
            if (refs == null) return false;

            // DrawLock(new Rect(rect.xMax - 12f, rect.yMin - 12f, 16f, 16f));
            // var btnRect = rect;
            // btnRect.yMin = btnRect.yMax - 16f;

            // if (GUI.Button(btnRect, "Re-select"))
            // {
            //     Debug.LogWarning("Should re-select!");
            // }
            rect.yMax -= 16f;
            return drawer.Draw(rect);
        }

        public bool Contains(string guidOrInstID)
        {
            return guidSet.Contains(guidOrInstID) || instSet.Contains(guidOrInstID);
        }

        public bool Contains(UnityObject sceneObject)
        {
            var id = sceneObject.GetInstanceID().ToString();
            return instSet.Contains(id);
        }

        public void Add(UnityObject sceneObject)
        {
            if (sceneObject == null) return;
            var id = sceneObject.GetInstanceID().ToString();
            instSet.Add(id); // hashset does not need to check exist before add
            dirty = true;
        }

        public void AddRange(params UnityObject[] sceneObjects)
        {
            foreach (UnityObject go in sceneObjects)
            {
                var id = go.GetInstanceID().ToString();
                instSet.Add(id); // hashset does not need to check exist before add	
            }

            dirty = true;
        }

        public void Add(string guid)
        {
            if (guidSet.Contains(guid)) return;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("Invalid GUID: " + guid);
                return;
            }

            guidSet.Add(guid);
            dirty = true;
        }

        public void AddRange(params string[] guids)
        {
            foreach (string id in guids)
            {
                Add(id);
            }
            dirty = true;
        }

        public void Remove(UnityObject sceneObject)
        {
            if (sceneObject == null) return;
            var id = sceneObject.GetInstanceID().ToString();
            instSet.Remove(id);
            dirty = true;
        }

        public void Remove(string guidOrInstID)
        {
            guidSet.Remove(guidOrInstID);
            instSet.Remove(guidOrInstID);

            dirty = true;
        }

        public void Clear()
        {
            guidSet.Clear();
            instSet.Clear();
            dirty = true;
        }

        public void Add(FR2_Ref rf)
        {
            if (rf.isSceneRef)
            {
                Add(rf.component);
            } else
            {
                Add(rf.asset.guid);
            }
        }

        public void Remove(FR2_Ref rf)
        {
            if (rf.isSceneRef)
            {
                Remove(rf.component);
            } else
            {
                Remove(rf.asset.guid);
            }
        }

        public void SetDirty()
        {
            drawer.SetDirty();
        }

        // public void DrawLock(Rect rect)
        // {
        //     GUI2.ContentColor(() =>
        //     {
        //         GUIContent icon = isLock ? FR2_Icon.Lock : FR2_Icon.Unlock;
        //         if (GUI2.Toggle(rect, ref isLock, icon))
        //         {
        //             Debug.Log("Toggle: OnSelectionChanged!");
        //             window.WillRepaint = true;
        //             window.OnSelectionChange();
        //         }
        //         
        //     }, GUI2.Theme(PRO, INDIE));
        // }

        public void RefreshView()
        {
            if (refs == null) refs = new Dictionary<string, FR2_Ref>();
            refs.Clear();

            if (instSet.Count > 0)
            {
                foreach (string instId in instSet)
                {
                    refs.Add(instId, new FR2_SceneRef(0, EditorUtility.InstanceIDToObject(int.Parse(instId))));
                }
            } else
            {
                foreach (string guid in guidSet)
                {
                    FR2_Asset asset = FR2_Cache.Api.Get(guid);
                    refs.Add(guid, new FR2_Ref(0, 0, asset, null)
                    {
                        isSceneRef = false
                    });
                }
            }

            drawer.SetRefs(refs);
            dirty = false;
        }
    }
}
