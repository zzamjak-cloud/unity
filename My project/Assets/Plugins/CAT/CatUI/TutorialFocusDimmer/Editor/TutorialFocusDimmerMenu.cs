#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.UI.Editors
{
    public static class TutorialFocusDimmerMenu
    {
        private const string SHADER_NAME = "CAT/UI/FocusDimmer";

        [MenuItem("CAT/UI/TutorialFocusDimmer", false, 10)]
        private static void CreateTutorialFocusDimmer()
        {
            // 선택된 오브젝트의 Canvas 또는 씬 내 Canvas 찾기
            Canvas canvas = null;
            if (Selection.activeGameObject != null)
                canvas = Selection.activeGameObject.GetComponentInParent<Canvas>();

            if (canvas == null)
                canvas = Object.FindAnyObjectByType<Canvas>();

            if (canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "TutorialFocusDimmer",
                    "씬에 Canvas가 없습니다. Canvas를 먼저 생성하세요.",
                    "확인");
                return;
            }

            // 셰이더 찾기
            Shader shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    "TutorialFocusDimmer",
                    $"{SHADER_NAME} 셰이더를 찾을 수 없습니다.",
                    "확인");
                return;
            }

            // TutorialFocusDimmer 게임오브젝트 생성
            GameObject dimmerObj = new GameObject("TutorialFocusDimmer");
            Undo.RegisterCreatedObjectUndo(dimmerObj, "Create TutorialFocusDimmer");
            GameObjectUtility.SetParentAndAlign(dimmerObj, canvas.gameObject);

            // RectTransform Stretch All 설정
            RectTransform dimmerRect = dimmerObj.AddComponent<RectTransform>();
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;

            // TutorialFocusDimmer 컴포넌트 추가 및 설정
            TutorialFocusDimmer dimmer = dimmerObj.AddComponent<TutorialFocusDimmer>();

            // TempTarget 자식 오브젝트 생성
            GameObject tempTargetObj = new GameObject("TempTarget");
            Undo.RegisterCreatedObjectUndo(tempTargetObj, "Create TempTarget");
            GameObjectUtility.SetParentAndAlign(tempTargetObj, dimmerObj);

            RectTransform tempTargetRect = tempTargetObj.AddComponent<RectTransform>();
            tempTargetRect.sizeDelta = new Vector2(240f, 120f);

            // SerializedObject로 private 필드 설정
            SerializedObject so = new SerializedObject(dimmer);

            // Tint 컬러: RGBA (0, 0, 0, 0.9)
            so.FindProperty("m_Color").colorValue = new Color(0f, 0f, 0f, 0.9f);

            // Padding: (30, 30)
            so.FindProperty("padding").vector2Value = new Vector2(30f, 30f);

            // Hole Corner Radius: 30
            so.FindProperty("_holeCornerRadius").floatValue = 30f;

            // Hole Softness: 40
            so.FindProperty("_holeSoftness").floatValue = 40f;

            // Expansion Margin: 200
            so.FindProperty("expansionMargin").floatValue = 200f;

            // 셰이더 직접 할당 (빌드 누락 방지)
            so.FindProperty("_focusDimmerShader").objectReferenceValue = shader;

            // FocusTargets 리스트에 TempTarget 추가
            SerializedProperty targetsProperty = so.FindProperty("_focusTargets");
            targetsProperty.arraySize = 1;
            targetsProperty.GetArrayElementAtIndex(0).objectReferenceValue = tempTargetRect;

            so.ApplyModifiedProperties();

            // Raycast Target 활성화 (딤 영역 클릭 차단)
            dimmer.raycastTarget = true;

            Selection.activeGameObject = dimmerObj;
        }
    }
}
#endif
