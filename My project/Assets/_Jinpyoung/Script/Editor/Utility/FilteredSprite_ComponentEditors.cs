using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Reflection;

namespace CAT.Utility
{
    // 1. Image 컴포넌트용 에디터 (정상 작동, 변경 없음)
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

    // 2. RawImage 컴포넌트용 에디터 (정상 작동, 변경 없음)
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


    // 3. SpriteRenderer 컴포넌트용 에디터 (오류 최종 해결)
    [CustomEditor(typeof(SpriteRenderer), true)]
    [CanEditMultipleObjects]
    public class FilteredSpriteRendererEditor : Editor
    {
        private FilteredSpriteFinderDrawer drawer;
        // Unity의 숨겨진 기본 SpriteRenderer 에디터를 담을 변수
        private Editor defaultEditor;

        private void OnEnable()
        {
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();

            // --- 리플렉션을 사용해 Unity의 내부 에디터를 가져오는 부분 ---
            // 1. 현재 선택된 SpriteRenderer 컴포넌트를 가져옵니다.
            var targets = serializedObject.targetObjects;
            // 2. UnityEditor 어셈블리에서 내부 클래스인 "SpriteRendererEditor" 타입을 찾습니다.
            var editorType = Type.GetType("UnityEditor.SpriteRendererEditor, UnityEditor");
            // 3. 찾은 타입으로 기본 에디터 인스턴스를 생성합니다.
            defaultEditor = CreateEditor(targets, editorType);
        }

        private void OnDisable()
        {
            // 메모리 누수를 방지하기 위해, 생성했던 기본 에디터 인스턴스를 파괴합니다.
            // MethodInfo.Invoke를 사용해 내부 메서드인 OnDisable을 호출해야 할 수도 있지만,
            // DestroyImmediate가 더 안전하고 일반적인 방법입니다.
            if (defaultEditor != null)
            {
                DestroyImmediate(defaultEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            // --- 생성해둔 기본 에디터의 UI 그리기 메서드를 실행 ---
            // 이렇게 하면 Unity의 원래 SpriteRenderer 인스펙터가 100% 동일하게 그려집니다.
            defaultEditor.OnInspectorGUI();

            // 그 아래에 우리가 만든 커스텀 필터 UI를 그립니다.
            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }
}