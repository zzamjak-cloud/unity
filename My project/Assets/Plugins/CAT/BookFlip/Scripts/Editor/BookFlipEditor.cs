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
        // ─────────────────────────────────────────────
        // SerializedProperty 캐싱
        // ─────────────────────────────────────────────

        private SerializedProperty _canvasProp;
        private SerializedProperty _bookPanelProp;
        private SerializedProperty _pageContainerProp;
        private SerializedProperty _backgroundProp;
        private SerializedProperty _pagesProp;
        private SerializedProperty _interactableProp;
        private SerializedProperty _enableShadowEffectProp;
        private SerializedProperty _currentPageProp;

        // 애니메이션 설정
        private SerializedProperty _flipDurationProp;
        private SerializedProperty _flipCurveProp;

        private SerializedProperty _clippingPlaneProp;
        private SerializedProperty _nextPageClipProp;
        private SerializedProperty _shadowProp;
        private SerializedProperty _shadowLTRProp;
        private SerializedProperty _ringOverlayProp;
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
        private bool _showEvents     = false;

        // ─────────────────────────────────────────────
        // 상수
        // ─────────────────────────────────────────────

        private const float LINE_H    = 18f; // EditorGUIUtility.singleLineHeight
        private const float SPACING   = 2f;
        private const float LINE_STEP = LINE_H + SPACING;
        private const float PADDING   = 4f;

        // ─────────────────────────────────────────────
        // OnEnable
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            _canvasProp            = serializedObject.FindProperty("_canvas");
            _bookPanelProp         = serializedObject.FindProperty("_bookPanel");
            _pageContainerProp     = serializedObject.FindProperty("_pageContainer");
            _backgroundProp        = serializedObject.FindProperty("_background");
            _pagesProp             = serializedObject.FindProperty("_pages");
            _interactableProp      = serializedObject.FindProperty("_interactable");
            _enableShadowEffectProp = serializedObject.FindProperty("_enableShadowEffect");
            _currentPageProp       = serializedObject.FindProperty("_currentPage");

            _flipDurationProp      = serializedObject.FindProperty("_flipDuration");
            _flipCurveProp         = serializedObject.FindProperty("_flipCurve");

            _clippingPlaneProp     = serializedObject.FindProperty("_clippingPlane");
            _nextPageClipProp      = serializedObject.FindProperty("_nextPageClip");
            _shadowProp            = serializedObject.FindProperty("_shadow");
            _shadowLTRProp         = serializedObject.FindProperty("_shadowLTR");
            _ringOverlayProp       = serializedObject.FindProperty("_ringOverlay");
            _leftProp              = serializedObject.FindProperty("_left");
            _leftNextProp          = serializedObject.FindProperty("_leftNext");
            _rightProp             = serializedObject.FindProperty("_right");
            _rightNextProp         = serializedObject.FindProperty("_rightNext");

            _hotSpotContainerProp  = serializedObject.FindProperty("_hotSpotContainer");
            _leftHotSpotProp       = serializedObject.FindProperty("_leftHotSpot");
            _rightHotSpotProp      = serializedObject.FindProperty("_rightHotSpot");

            _onFlipProp            = serializedObject.FindProperty("OnFlip");
            _onPageChangedProp     = serializedObject.FindProperty("OnPageChanged");
            _onFlipStartProp       = serializedObject.FindProperty("OnFlipStart");
            _onFlipEndProp         = serializedObject.FindProperty("OnFlipEnd");

            SetupPagesList();
        }

        // ─────────────────────────────────────────────
        // 페이지 ReorderableList 구성
        // ─────────────────────────────────────────────

        private void SetupPagesList()
        {
            _pagesList = new ReorderableList(serializedObject, _pagesProp, true, true, true, true);

            _pagesList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, $"페이지 목록 (총 {_pagesProp.arraySize}개)");
            };

            _pagesList.drawElementCallback = DrawPageElement;

            _pagesList.elementHeightCallback = GetPageElementHeight;

            _pagesList.onAddCallback = (ReorderableList list) =>
            {
                int idx = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                list.index = idx;

                SerializedProperty el = list.serializedProperty.GetArrayElementAtIndex(idx);
                el.FindPropertyRelative("_type").enumValueIndex        = 0; // Sprite
                el.FindPropertyRelative("_sourceMode").enumValueIndex  = 0; // Direct
                el.FindPropertyRelative("_sprite").objectReferenceValue      = null;
                el.FindPropertyRelative("_prefab").objectReferenceValue      = null;
                el.FindPropertyRelative("_gameObject").objectReferenceValue  = null;
                el.FindPropertyRelative("_resourcePath").stringValue         = string.Empty;
                el.FindPropertyRelative("_persistInstance").boolValue        = false;
            };
        }

        private void DrawPageElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty el = _pagesProp.GetArrayElementAtIndex(index);
            if (el == null) return;

            float y = rect.y + SPACING;

            // ── 첫 번째 행: [인덱스] | PageType ──────────────────
            Rect indexRect = new Rect(rect.x, y, 30, LINE_H);
            Rect typeRect  = new Rect(rect.x + 34, y, rect.width - 34, LINE_H);
            EditorGUI.LabelField(indexRect, $"[{index}]", EditorStyles.boldLabel);

            SerializedProperty typeProp       = el.FindPropertyRelative("_type");
            SerializedProperty sourceModeProp = el.FindPropertyRelative("_sourceMode");

            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
            y += LINE_STEP;

            // ── 두 번째 행: SourceMode ───────────────────────────
            Rect sourceModeRect = new Rect(rect.x + 15, y, rect.width - 15, LINE_H);
            EditorGUI.PropertyField(sourceModeRect, sourceModeProp, new GUIContent("소스 방식"));
            y += LINE_STEP;

            BookFlipPage.PageType   pageType   = (BookFlipPage.PageType)typeProp.enumValueIndex;
            BookFlipPage.SourceMode sourceMode = (BookFlipPage.SourceMode)sourceModeProp.enumValueIndex;

            // ── 세 번째 행: 소스 필드 (SourceMode에 따라 다름) ───
            Rect fieldRect = new Rect(rect.x + 15, y, rect.width - 15, LINE_H);

            if (sourceMode == BookFlipPage.SourceMode.Direct)
            {
                switch (pageType)
                {
                    case BookFlipPage.PageType.Sprite:
                        EditorGUI.PropertyField(fieldRect, el.FindPropertyRelative("_sprite"), new GUIContent("Sprite"));
                        break;
                    case BookFlipPage.PageType.Prefab:
                        EditorGUI.PropertyField(fieldRect, el.FindPropertyRelative("_prefab"), new GUIContent("Prefab"));
                        break;
                    case BookFlipPage.PageType.GameObject:
                        EditorGUI.PropertyField(fieldRect, el.FindPropertyRelative("_gameObject"), new GUIContent("GameObject"));
                        break;
                }
            }
            else
            {
                // ResourcesPath / CustomAsync: 경로 입력
                SerializedProperty pathProp = el.FindPropertyRelative("_resourcePath");
                string label = sourceMode == BookFlipPage.SourceMode.ResourcesPath
                    ? "Resources 경로"
                    : "Addressable 키";
                EditorGUI.PropertyField(fieldRect, pathProp, new GUIContent(label));
            }

            y += LINE_STEP;

            // ── 네 번째 행: PersistInstance (Prefab / GameObject 타입에만 표시) ──
            if (pageType == BookFlipPage.PageType.Prefab || pageType == BookFlipPage.PageType.GameObject)
            {
                Rect persistRect = new Rect(rect.x + 15, y, rect.width - 15, LINE_H);
                EditorGUI.PropertyField(persistRect, el.FindPropertyRelative("_persistInstance"), new GUIContent("인스턴스 유지 (PersistInstance)"));
            }
        }

        private float GetPageElementHeight(int index)
        {
            SerializedProperty el = _pagesProp.GetArrayElementAtIndex(index);
            if (el == null) return LINE_STEP * 3 + PADDING;

            SerializedProperty typeProp       = el.FindPropertyRelative("_type");
            SerializedProperty sourceModeProp = el.FindPropertyRelative("_sourceMode");
            BookFlipPage.PageType pageType = (BookFlipPage.PageType)typeProp.enumValueIndex;
            BookFlipPage.SourceMode sourceMode = (BookFlipPage.SourceMode)sourceModeProp.enumValueIndex;

            // Type + SourceMode + 소스필드 = 3행 (기본)
            int lines = 3;

            // Prefab / GameObject 타입은 PersistInstance 행 추가
            if (pageType == BookFlipPage.PageType.Prefab || pageType == BookFlipPage.PageType.GameObject)
                lines++;

            return LINE_STEP * lines + PADDING;
        }

        // ─────────────────────────────────────────────
        // OnInspectorGUI
        // ─────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BookFlip bookFlip = (BookFlip)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("BookFlip — 고도화된 책넘기기 시스템", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ── Canvas 설정 ──────────────────────────────────────
            EditorGUILayout.LabelField("Canvas 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_canvasProp,        new GUIContent("Canvas"));
            EditorGUILayout.PropertyField(_bookPanelProp,     new GUIContent("Book Panel"));
            EditorGUILayout.HelpBox("Page Container를 사용하면 모든 페이지 요소를 격리하여 HotSpot과 분리할 수 있습니다.", MessageType.Info);
            EditorGUILayout.PropertyField(_pageContainerProp, new GUIContent("Page Container (선택)"));
            EditorGUILayout.Space();

            // ── 페이지 설정 ──────────────────────────────────────
            EditorGUILayout.LabelField("페이지 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_backgroundProp, new GUIContent("배경 스프라이트"));

            _pagesList.DoLayoutList();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"현재 페이지: {bookFlip.CurrentPage} / {bookFlip.TotalPageCount}", EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // ── 옵션 ────────────────────────────────────────────
            EditorGUILayout.LabelField("옵션", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_interactableProp,       new GUIContent("상호작용 가능"));
            EditorGUILayout.PropertyField(_enableShadowEffectProp, new GUIContent("그림자 효과"));
            EditorGUILayout.Space();

            // ── 애니메이션 설정 ──────────────────────────────────
            EditorGUILayout.LabelField("애니메이션 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_flipDurationProp, new GUIContent("넘김 시간 (초)"));
            EditorGUILayout.PropertyField(_flipCurveProp,    new GUIContent("이징 곡선"));
            EditorGUILayout.Space();

            // ── UI 요소 (접기) ───────────────────────────────────
            _showUIElements = EditorGUILayout.Foldout(_showUIElements, "UI 요소 (고급)", true);
            if (_showUIElements)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_clippingPlaneProp, new GUIContent("Clipping Plane"));
                EditorGUILayout.PropertyField(_nextPageClipProp,  new GUIContent("Next Page Clip"));
                EditorGUILayout.PropertyField(_shadowProp,        new GUIContent("Shadow"));
                EditorGUILayout.PropertyField(_shadowLTRProp,     new GUIContent("Shadow LTR"));
                EditorGUILayout.PropertyField(_ringOverlayProp,   new GUIContent("Ring Overlay (선택)"));
                EditorGUILayout.PropertyField(_leftProp,          new GUIContent("Left"));
                EditorGUILayout.PropertyField(_leftNextProp,      new GUIContent("Left Next"));
                EditorGUILayout.PropertyField(_rightProp,         new GUIContent("Right"));
                EditorGUILayout.PropertyField(_rightNextProp,     new GUIContent("Right Next"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("핫스팟 (선택사항)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("HotSpot Container를 사용하면 핫스팟이 항상 최상위 레이어에 격리됩니다.", MessageType.Info);
                EditorGUILayout.PropertyField(_hotSpotContainerProp, new GUIContent("HotSpot Container"));
                EditorGUILayout.PropertyField(_leftHotSpotProp,      new GUIContent("Left HotSpot"));
                EditorGUILayout.PropertyField(_rightHotSpotProp,     new GUIContent("Right HotSpot"));

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            // ── 이벤트 (접기) ────────────────────────────────────
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

            // ── 런타임 컨트롤 ────────────────────────────────────
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("런타임 컨트롤", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("◀ 이전 페이지")) bookFlip.PreviousPage();
                if (GUILayout.Button("다음 페이지 ▶")) bookFlip.NextPage();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("페이지 이동:", GUILayout.Width(80));
                int targetPage = EditorGUILayout.IntField(bookFlip.CurrentPage);
                if (GUILayout.Button("이동", GUILayout.Width(50)))
                    bookFlip.GoToPage(targetPage);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if (GUILayout.Button("모든 페이지 새로고침 (RefreshAllPages)"))
                    bookFlip.RefreshAllPages();

                EditorGUILayout.HelpBox(
                    "소스(Sprite/Prefab/GO) 변경 후 새로고침하면 현재 표시 슬롯에 즉시 반영됩니다.",
                    MessageType.Info);
            }
            EditorGUILayout.Space();

            // ── 유효성 검사 ──────────────────────────────────────
            DrawValidationSection(bookFlip);

            serializedObject.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────
        // 유효성 검사
        // ─────────────────────────────────────────────

        private void DrawValidationSection(BookFlip bookFlip)
        {
            EditorGUILayout.LabelField("유효성 검사", EditorStyles.boldLabel);

            bool hasErrors = false;

            if (bookFlip.GetComponent<RectTransform>() == null)
            {
                EditorGUILayout.HelpBox("BookFlip은 UI GameObject에 추가되어야 합니다.", MessageType.Error);
                hasErrors = true;
            }

            // 페이지 유효성
            int invalidCount = 0;
            for (int i = 0; i < _pagesProp.arraySize; i++)
            {
                SerializedProperty el         = _pagesProp.GetArrayElementAtIndex(i);
                int                typeIdx    = el.FindPropertyRelative("_type").enumValueIndex;
                int                sourceModeIdx = el.FindPropertyRelative("_sourceMode").enumValueIndex;

                BookFlipPage.PageType   pageType   = (BookFlipPage.PageType)typeIdx;
                BookFlipPage.SourceMode sourceMode = (BookFlipPage.SourceMode)sourceModeIdx;

                bool isValid = false;

                if (sourceMode == BookFlipPage.SourceMode.Direct)
                {
                    switch (pageType)
                    {
                        case BookFlipPage.PageType.Sprite:
                            isValid = el.FindPropertyRelative("_sprite").objectReferenceValue != null;
                            break;
                        case BookFlipPage.PageType.Prefab:
                            isValid = el.FindPropertyRelative("_prefab").objectReferenceValue != null;
                            break;
                        case BookFlipPage.PageType.GameObject:
                            isValid = el.FindPropertyRelative("_gameObject").objectReferenceValue != null;
                            break;
                    }
                }
                else
                {
                    // ResourcesPath / CustomAsync: 경로가 비어 있지 않으면 유효
                    string path = el.FindPropertyRelative("_resourcePath").stringValue;
                    isValid = !string.IsNullOrEmpty(path);
                }

                if (!isValid) invalidCount++;
            }

            if (invalidCount > 0)
            {
                EditorGUILayout.HelpBox($"{invalidCount}개의 페이지가 유효하지 않습니다. (참조 또는 경로 누락)", MessageType.Warning);
                hasErrors = true;
            }

            // UI 요소 검사
            if (_clippingPlaneProp.objectReferenceValue == null ||
                _nextPageClipProp.objectReferenceValue  == null ||
                _leftProp.objectReferenceValue          == null ||
                _leftNextProp.objectReferenceValue      == null ||
                _rightProp.objectReferenceValue         == null ||
                _rightNextProp.objectReferenceValue     == null)
            {
                EditorGUILayout.HelpBox("일부 필수 UI 요소가 설정되지 않았습니다.", MessageType.Warning);
                hasErrors = true;
            }

            if (!hasErrors)
                EditorGUILayout.HelpBox("모든 설정이 올바릅니다!", MessageType.Info);
        }

        // ─────────────────────────────────────────────
        // Scene View Gizmo
        // ─────────────────────────────────────────────

        private void OnSceneGUI()
        {
            BookFlip bookFlip = (BookFlip)target;
            if (bookFlip == null) return;

            Handles.color = Color.cyan;

            Vector3 ebl = bookFlip.transform.TransformPoint(bookFlip.EndBottomLeft);
            Vector3 ebr = bookFlip.transform.TransformPoint(bookFlip.EndBottomRight);
            Vector3 etl = ebl + bookFlip.transform.up * bookFlip.Height;
            Vector3 etr = ebr + bookFlip.transform.up * bookFlip.Height;

            Handles.DrawLine(ebl, ebr);
            Handles.DrawLine(ebr, etr);
            Handles.DrawLine(etr, etl);
            Handles.DrawLine(etl, ebl);

            Vector3 center    = (ebl + ebr) / 2;
            Vector3 centerTop = center + bookFlip.transform.up * bookFlip.Height;
            Handles.color = Color.yellow;
            Handles.DrawDottedLine(center, centerTop, 5f);

            Handles.Label(center, $"BookFlip\nPage: {bookFlip.CurrentPage}/{bookFlip.TotalPageCount}");
        }
    }
}
