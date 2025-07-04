using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal class FR2_ObjectDrawer
    {
        private static readonly Dictionary<UnityObject, GUIContent> contentMap = new Dictionary<UnityObject, GUIContent>();
        private static GUIStyle objectFieldStyle;

        public void DrawOnly(Rect rect, UnityObject target)
        {
            GUIContent content;

            if (target == null)
            {
                content = FR2_GUIContent.From("(none)", AssetPreview.GetMiniTypeThumbnail(typeof(GameObject)));
            } else if (!contentMap.TryGetValue(target, out content))
            {
                content = FR2_GUIContent.From(target.name, AssetPreview.GetMiniTypeThumbnail(target.GetType()));
                contentMap.Add(target, content);
            }

            if (objectFieldStyle == null)
            {
                objectFieldStyle = new GUIStyle(EditorStyles.objectField)
                {
                    margin = new RectOffset(16, 0, 0, 0)
                };
            }

            EditorGUIUtility.SetIconSize(new Vector2(12f, 12f));
            GUI.Label(rect, content, objectFieldStyle);
        }
    }
}
