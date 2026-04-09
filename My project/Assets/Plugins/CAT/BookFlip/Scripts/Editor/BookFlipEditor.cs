using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace CAT.BookFlip.Editor
{
    /// <summary>
    /// BookFlip 커스텀 에디터
    /// 직관적인 페이지 관리 인터페이스 제공
    /// </summary>
    [CustomEditor(typeof(BookFlip))]
    public class BookFlipEditor : UnityEditor.Editor
    {
        private SerializedProperty _canvasProp;
        private SerializedProperty _bookPanelProp;
        private SerializedProperty _pageContainerProp;
        private SerializedProperty _backgroundProp;
        private SerializedProperty _pagesProp;
        private SerializedProperty _interactableProp;
        private SerializedProperty _enableShadowEffectProp;
        private SerializedProperty _currentPageProp;

        private SerializedProperty _clippingPlaneProp;
        private SerializedProperty _nextPageClipProp;
        private SerializedProperty _shadowProp;
        private SerializedProperty _shadowLTRProp;
        private SerializedProperty _leftProp;
        private SerializedProperty _leftNextProp;
        private SerializedProperty _rightProp;
        private SerializedProperty _rightNextProp;

        private SerializedProperty _hotSpotContainerProp;
        private SerializedProperty _leftHotSpotProp;
        private SerializedProperty _rightHotSpotProp;

        private SerializedProperty _onFlipProp;
        private SerializedProperty _onPageChangedProp;
        private SerializedProperty _onFlipStartProp;
        private SerializedProperty _onFlipEndProp;

        private ReorderableList _pagesList;
        private bool _showUIElements = false;
        private bool _showEvents = false;

        private void OnEnable()
        {
            // 프로퍼티 초기화
            _canvasProp = serializedObject.FindProperty("_canvas");
            _bookPanelProp = serializedObject.FindProperty("_bookPanel");
            _pageContainerProp = serializedObject.FindProperty("_pageContainer");
            _backgroundProp = serializedObject.FindProperty("_background");
            _pagesProp = serializedObject.FindProperty("_pages");
            _interactableProp = serializedObject.FindProperty("_interactable");
            _enableShadowEffectProp = serializedObject.FindProperty("_enableShadowEffect");
            _currentPageProp = serializedObject.FindProperty("_currentPage");

            _clippingPlaneProp = serializedObject.FindProperty("_clippingPlane");
            _nextPageClipProp = serializedObject.FindProperty("_nextPageClip");
            _shadowProp = serializedObject.FindProperty("_shadow");
            _shadowLTRProp = serializedObject.FindProperty("_shadowLTR");
            _leftProp = serializedObject.FindProperty("_left");
            _leftNextProp = serializedObject.FindProperty("_leftNext");
            _rightProp = serializedObject.FindProperty("_right");
            _rightNextProp = serializedObject.FindProperty("_rightNext");

            _hotSpotContainerProp = serializedObject.FindProperty("_hotSpotContainer");
            _leftHotSpotProp = serializedObject.FindProperty("_leftHotSpot");
            _rightHotSpotProp = serializedObject.FindProperty("_rightHotSpot");

            _onFlipProp = serializedObject.FindProperty("OnFlip");
            _onPageChangedProp = serializedObject.FindProperty("OnPageChanged");
            _onFlipStartProp = serializedObject.FindProperty("OnFlipStart");
            _onFlipEndProp = serializedObject.FindProperty("OnFlipEnd");

            // ReorderableList 설정
            SetupPagesList();
        }

        /// <summary>
        /// 페이지 리스트 설정
        /// </summary>
        private void SetupPagesList()
        {
            _pagesList = new ReorderableList(serializedObject, _pagesProp, true, true, true, true);

            // 헤더 그리기
            _pagesList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, $"페이지 목록 (총 {_pagesProp.arraySize}개)");
            };

            // 요소 그리기
            _pagesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty element = _pagesProp.GetArrayElementAtIndex(index);
                if (element == null) return;

                rect.y += 2;
                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = 2f;

                // 인덱스 레이블
                Rect indexRect = new Rect(rect.x, rect.y, 30, lineHeight);
                EditorGUI.LabelField(indexRect, $"[{index}]", EditorStyles.boldLabel);

                // Type 필드
                SerializedProperty typeProp = element.FindPropertyRelative("_type");
                Rect typeRect = new Rect(rect.x + 35, rect.y, rect.width - 35, lineHeight);
                EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

                // Type에 따른 필드 표시
                BookFlipPage.PageType pageType = (BookFlipPage.PageType)typeProp.enumValueIndex;
                rect.y += lineHeight + spacing;

                switch (pageType)
                {
                    case BookFlipPage.PageType.Sprite:
                        SerializedProperty spriteProp = element.FindPropertyRelative("_sprite");
                        Rect spriteRect = new Rect(rect.x + 15, rect.y, rect.width - 15, lineHeight);
                        EditorGUI.PropertyField(spriteRect, spriteProp, new GUIContent("Sprite"));
                        break;

                    case BookFlipPage.PageType.Prefab:
                        SerializedProperty prefabProp = element.FindPropertyRelative("_prefab");
                        Rect prefabRect = new Rect(rect.x + 15, rect.y, rect.width - 15, lineHeight);
                        EditorGUI.PropertyField(prefabRect, prefabProp, new GUIContent("Prefab"));
                        break;

                    case BookFlipPage.PageType.GameObject:
                        SerializedProperty gameObjectProp = element.FindPropertyRelative("_gameObject");
                        Rect goRect = new Rect(rect.x + 15, rect.y, rect.width - 15, lineHeight);
                        EditorGUI.PropertyField(goRect, gameObjectProp, new GUIContent("GameObject"));
                        break;
                }
            };

            // 요소 높이 계산
            _pagesList.elementHeightCallback = (int index) =>
            {
                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = 2f;
                return (lineHeight + spacing) * 2 + 4; // Type + 실제 필드 + 여백
            };

            // 요소 추가
            _pagesList.onAddCallback = (ReorderableList list) =>
            {
                int index = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                list.index = index;

                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("_type").enumValueIndex = 0; // Sprite 타입으로 초기화
                element.FindPropertyRelative("_sprite").objectReferenceValue = null;
                element.FindPropertyRelative("_prefab").objectReferenceValue = null;
                element.FindPropertyRelative("_gameObject").objectReferenceValue = null;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BookFlip bookFlip = (BookFlip)target;

            // 로고/헤더
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("BookFlip - 고도화된 책넘기기 시스템", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Canvas 설정
            EditorGUILayout.LabelField("Canvas 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_canvasProp, new GUIContent("Canvas"));
            EditorGUILayout.PropertyField(_bookPanelProp, new GUIContent("Book Panel"));
            EditorGUILayout.HelpBox("Page Container를 사용하면 모든 페이지 요소를 격리하여 HotSpot과 분리할 수 있습니다.", MessageType.Info);
            EditorGUILayout.PropertyField(_pageContainerProp, new GUIContent("Page Container (선택)"));
            EditorGUILayout.Space();

            // 페이지 설정
            EditorGUILayout.LabelField("페이지 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_backgroundProp, new GUIContent("배경 스프라이트"));

            // 페이지 목록 (ReorderableList)
            _pagesList.DoLayoutList();

            EditorGUILayout.Space();

            // 현재 페이지 정보
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"현재 페이지: {bookFlip.CurrentPage} / {bookFlip.TotalPageCount}", EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 옵션
            EditorGUILayout.LabelField("옵션", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_interactableProp, new GUIContent("상호작용 가능"));
            EditorGUILayout.PropertyField(_enableShadowEffectProp, new GUIContent("그림자 효과"));
            EditorGUILayout.Space();

            // UI 요소 (접기 가능)
            _showUIElements = EditorGUILayout.Foldout(_showUIElements, "UI 요소 (고급)", true);
            if (_showUIElements)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_clippingPlaneProp, new GUIContent("Clipping Plane"));
                EditorGUILayout.PropertyField(_nextPageClipProp, new GUIContent("Next Page Clip"));
                EditorGUILayout.PropertyField(_shadowProp, new GUIContent("Shadow"));
                EditorGUILayout.PropertyField(_shadowLTRProp, new GUIContent("Shadow LTR"));
                EditorGUILayout.PropertyField(_leftProp, new GUIContent("Left"));
                EditorGUILayout.PropertyField(_leftNextProp, new GUIContent("Left Next"));
                EditorGUILayout.PropertyField(_rightProp, new GUIContent("Right"));
                EditorGUILayout.PropertyField(_rightNextProp, new GUIContent("Right Next"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("핫스팟 (선택사항)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("HotSpot Container를 사용하면 핫스팟이 항상 최상위 레이어에 격리됩니다.", MessageType.Info);
                EditorGUILayout.PropertyField(_hotSpotContainerProp, new GUIContent("HotSpot Container"));
                EditorGUILayout.PropertyField(_leftHotSpotProp, new GUIContent("Left HotSpot"));
                EditorGUILayout.PropertyField(_rightHotSpotProp, new GUIContent("Right HotSpot"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 이벤트 (접기 가능)
            _showEvents = EditorGUILayout.Foldout(_showEvents, "이벤트", true);
            if (_showEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_onFlipProp);
                EditorGUILayout.PropertyField(_onPageChangedProp);
                EditorGUILayout.PropertyField(_onFlipStartProp);
                EditorGUILayout.PropertyField(_onFlipEndProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 런타임 컨트롤
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("런타임 컨트롤", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("◀ 이전 페이지"))
                {
                    bookFlip.PreviousPage();
                }
                if (GUILayout.Button("다음 페이지 ▶"))
                {
                    bookFlip.NextPage();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("페이지 이동:", GUILayout.Width(80));
                int targetPage = EditorGUILayout.IntField(bookFlip.CurrentPage);
                if (GUILayout.Button("이동", GUILayout.Width(50)))
                {
                    bookFlip.GoToPage(targetPage);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            // 유효성 검사
            DrawValidationSection(bookFlip);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 유효성 검사 섹션 그리기
        /// </summary>
        private void DrawValidationSection(BookFlip bookFlip)
        {
            EditorGUILayout.LabelField("유효성 검사", EditorStyles.boldLabel);

            bool hasErrors = false;

            // Canvas 검사
            if (bookFlip.GetComponent<RectTransform>() == null)
            {
                EditorGUILayout.HelpBox("BookFlip은 UI GameObject에 추가되어야 합니다.", MessageType.Error);
                hasErrors = true;
            }

            // 페이지 검사
            int invalidPageCount = 0;
            for (int i = 0; i < _pagesProp.arraySize; i++)
            {
                SerializedProperty element = _pagesProp.GetArrayElementAtIndex(i);
                BookFlipPage.PageType pageType = (BookFlipPage.PageType)element.FindPropertyRelative("_type").enumValueIndex;

                bool isValid = false;
                switch (pageType)
                {
                    case BookFlipPage.PageType.Sprite:
                        isValid = element.FindPropertyRelative("_sprite").objectReferenceValue != null;
                        break;
                    case BookFlipPage.PageType.Prefab:
                        isValid = element.FindPropertyRelative("_prefab").objectReferenceValue != null;
                        break;
                    case BookFlipPage.PageType.GameObject:
                        isValid = element.FindPropertyRelative("_gameObject").objectReferenceValue != null;
                        break;
                }

                if (!isValid)
                    invalidPageCount++;
            }

            if (invalidPageCount > 0)
            {
                EditorGUILayout.HelpBox($"{invalidPageCount}개의 페이지가 유효하지 않습니다. (참조가 null)", MessageType.Warning);
                hasErrors = true;
            }

            // UI 요소 검사
            if (_clippingPlaneProp.objectReferenceValue == null ||
                _nextPageClipProp.objectReferenceValue == null ||
                _leftProp.objectReferenceValue == null ||
                _leftNextProp.objectReferenceValue == null ||
                _rightProp.objectReferenceValue == null ||
                _rightNextProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("일부 필수 UI 요소가 설정되지 않았습니다.", MessageType.Warning);
                hasErrors = true;
            }

            if (!hasErrors)
            {
                EditorGUILayout.HelpBox("모든 설정이 올바릅니다!", MessageType.Info);
            }
        }

        /// <summary>
        /// Scene View에서 Gizmo 그리기
        /// </summary>
        private void OnSceneGUI()
        {
            BookFlip bookFlip = (BookFlip)target;
            if (bookFlip == null) return;

            // 페이지 범위 표시
            Handles.color = Color.cyan;

            Vector3 ebl = bookFlip.transform.TransformPoint(bookFlip.EndBottomLeft);
            Vector3 ebr = bookFlip.transform.TransformPoint(bookFlip.EndBottomRight);

            Vector3 etl = ebl + bookFlip.transform.up * bookFlip.Height;
            Vector3 etr = ebr + bookFlip.transform.up * bookFlip.Height;

            // 외곽선 그리기
            Handles.DrawLine(ebl, ebr);
            Handles.DrawLine(ebr, etr);
            Handles.DrawLine(etr, etl);
            Handles.DrawLine(etl, ebl);

            // 중앙선 그리기
            Vector3 center = (ebl + ebr) / 2;
            Vector3 centerTop = center + bookFlip.transform.up * bookFlip.Height;
            Handles.color = Color.yellow;
            Handles.DrawDottedLine(center, centerTop, 5f);

            // 레이블 표시
            Handles.Label(center, $"BookFlip\nPage: {bookFlip.CurrentPage}/{bookFlip.TotalPageCount}");
        }
    }
}
