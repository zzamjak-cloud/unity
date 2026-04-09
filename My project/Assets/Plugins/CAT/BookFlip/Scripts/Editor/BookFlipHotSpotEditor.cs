using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace CAT.BookFlip.Editor
{
    /// <summary>
    /// BookFlipHotSpot 커스텀 에디터
    /// 핫스팟 설정과 하이어라키 배치를 돕는 인터페이스 제공
    /// </summary>
    [CustomEditor(typeof(BookFlipHotSpot))]
    public class BookFlipHotSpotEditor : UnityEditor.Editor
    {
        private SerializedProperty _typeProp;
        private SerializedProperty _bookFlipProp;

        private void OnEnable()
        {
            _typeProp = serializedObject.FindProperty("_type");
            _bookFlipProp = serializedObject.FindProperty("_bookFlip");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BookFlipHotSpot hotSpot = (BookFlipHotSpot)target;

            // 헤더
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("BookFlipHotSpot - 페이지 넘김 핫스팟", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("UI 버튼이 있는 페이지에서 페이지 넘김을 위해 항상 최상위에 위치하는 핫스팟입니다.", MessageType.Info);
            EditorGUILayout.Space();

            // Type 설정
            EditorGUILayout.PropertyField(_typeProp, new GUIContent("핫스팟 타입"));
            EditorGUILayout.PropertyField(_bookFlipProp, new GUIContent("BookFlip"));

            EditorGUILayout.Space();

            // 설정 확인
            DrawValidationSection(hotSpot);

            EditorGUILayout.Space();

            // 자동 설정 버튼
            if (GUILayout.Button("자동 설정"))
            {
                AutoSetup(hotSpot);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 유효성 검사 및 경고 표시
        /// </summary>
        private void DrawValidationSection(BookFlipHotSpot hotSpot)
        {
            EditorGUILayout.LabelField("설정 확인", EditorStyles.boldLabel);

            bool hasErrors = false;

            // Image 컴포넌트 확인
            Image image = hotSpot.GetComponent<Image>();
            if (image == null)
            {
                EditorGUILayout.HelpBox("Image 컴포넌트가 필요합니다. (Alpha를 0으로 설정하여 투명하게)", MessageType.Error);
                hasErrors = true;
            }
            else
            {
                if (!image.raycastTarget)
                {
                    EditorGUILayout.HelpBox("Image의 Raycast Target이 비활성화되어 있습니다. 클릭 이벤트를 받을 수 없습니다.", MessageType.Warning);
                    hasErrors = true;
                }

                Color color = image.color;
                if (color.a > 0.1f)
                {
                    EditorGUILayout.HelpBox("Image의 Alpha가 너무 높습니다. 0으로 설정하여 투명하게 만드는 것을 권장합니다.", MessageType.Info);
                }
            }

            // 하이어라키 위치 확인
            Transform parent = hotSpot.transform.parent;
            if (parent != null)
            {
                int siblingIndex = hotSpot.transform.GetSiblingIndex();
                int lastIndex = parent.childCount - 1;

                if (siblingIndex != lastIndex)
                {
                    EditorGUILayout.HelpBox(
                        $"핫스팟이 하이어라키 최하위(마지막)에 위치하지 않습니다.\n현재: {siblingIndex + 1}/{parent.childCount}, 렌더링 순서상 최상위에 있어야 클릭 이벤트를 정상적으로 받을 수 있습니다.",
                        MessageType.Warning);
                    hasErrors = true;

                    if (GUILayout.Button("최상위로 이동 (SetAsLastSibling)"))
                    {
                        Undo.RecordObject(hotSpot.transform, "Move HotSpot to Last Sibling");
                        hotSpot.transform.SetAsLastSibling();
                        EditorUtility.SetDirty(hotSpot.transform);
                    }
                }
            }

            // BookFlip 참조 확인
            if (_bookFlipProp.objectReferenceValue == null)
            {
                BookFlip bookFlip = hotSpot.GetComponentInParent<BookFlip>();
                if (bookFlip != null)
                {
                    EditorGUILayout.HelpBox("BookFlip 참조가 설정되지 않았지만, 부모에서 자동으로 찾을 수 있습니다.", MessageType.Info);

                    if (GUILayout.Button("자동 연결"))
                    {
                        Undo.RecordObject(hotSpot, "Auto Connect BookFlip");
                        _bookFlipProp.objectReferenceValue = bookFlip;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(hotSpot);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("BookFlip 컴포넌트를 찾을 수 없습니다. BookFlip의 자식으로 배치하세요.", MessageType.Error);
                    hasErrors = true;
                }
            }

            if (!hasErrors)
            {
                EditorGUILayout.HelpBox("모든 설정이 올바릅니다!", MessageType.Info);
            }
        }

        /// <summary>
        /// 자동 설정
        /// </summary>
        private void AutoSetup(BookFlipHotSpot hotSpot)
        {
            Undo.RecordObject(hotSpot, "Auto Setup HotSpot");

            // Image 컴포넌트 확인 및 추가
            Image image = hotSpot.GetComponent<Image>();
            if (image == null)
            {
                image = Undo.AddComponent<Image>(hotSpot.gameObject);
            }

            // Image 설정
            Undo.RecordObject(image, "Setup HotSpot Image");
            image.color = new Color(1, 1, 1, 0); // 완전 투명
            image.raycastTarget = true;

            // RectTransform 설정
            RectTransform rt = hotSpot.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Setup HotSpot RectTransform");

                BookFlipHotSpot.HotSpotType type = (BookFlipHotSpot.HotSpotType)_typeProp.enumValueIndex;

                // 부모의 전체 영역 중 절반 영역 차지
                rt.anchorMin = type == BookFlipHotSpot.HotSpotType.Left ? new Vector2(0, 0) : new Vector2(0.5f, 0);
                rt.anchorMax = type == BookFlipHotSpot.HotSpotType.Left ? new Vector2(0.5f, 1) : new Vector2(1, 1);
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }

            // BookFlip 자동 연결
            if (_bookFlipProp.objectReferenceValue == null)
            {
                BookFlip bookFlip = hotSpot.GetComponentInParent<BookFlip>();
                if (bookFlip != null)
                {
                    _bookFlipProp.objectReferenceValue = bookFlip;
                }
            }

            // 최상위로 이동
            hotSpot.transform.SetAsLastSibling();

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(hotSpot);

            Debug.Log("[BookFlipHotSpot] 자동 설정 완료!");
        }
    }
}
