using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Reflection;

namespace CAT.Utility
{
    [CustomEditor(typeof(Image), true)]
    [CanEditMultipleObjects]
    public class FilteredImageEditor : UnityEditor.UI.ImageEditor
    {
        private FilteredSpriteFinderDrawer drawer;

        protected override void OnEnable()
        {
            base.OnEnable();
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    [CustomEditor(typeof(RawImage), true)]
    [CanEditMultipleObjects]
    public class FilteredRawImageEditor : UnityEditor.UI.RawImageEditor
    {
        private FilteredSpriteFinderDrawer drawer;

        protected override void OnEnable()
        {
            base.OnEnable();
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Texture").objectReferenceValue = sprite != null ? sprite.texture : null;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    [CustomEditor(typeof(SpriteRenderer), true)]
    [CanEditMultipleObjects]
    public class FilteredSpriteRendererEditor : Editor
    {
        private FilteredSpriteFinderDrawer drawer;
        private Editor defaultEditor;

        private void OnEnable()
        {
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();

            var targets = serializedObject.targetObjects;
            var editorType = Type.GetType("UnityEditor.SpriteRendererEditor, UnityEditor");
            defaultEditor = CreateEditor(targets, editorType);
        }

        private void OnDisable()
        {
            if (defaultEditor != null)
            {
                DestroyImmediate(defaultEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            defaultEditor.OnInspectorGUI();

            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }
}