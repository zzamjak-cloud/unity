#if UNITY_EDITOR // [빌드 안전장치] 이 전처리기가 없으면 빌드 시 에러가 납니다.

using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

[InitializeOnLoad]
public class AnimSyncTool
{
    // [설정] 로그가 너무 시끄러우면 false로 끄세요.
    // 문제가 생겼을 때만 켜서 확인하면 됩니다.
    private static bool showLogs = false; 

    static AnimSyncTool()
    {
        // 중복 등록 방지
        Selection.selectionChanged -= OnSelectionChanged;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private static void OnSelectionChanged()
    {
        // 1. 하이어라키 창 활성화 여부 체크 (애니메이션 창 간섭 방지)
        if (EditorWindow.focusedWindow == null || 
            EditorWindow.focusedWindow.GetType().Name != "SceneHierarchyWindow")
        {
            return;
        }

        GameObject selectedGo = Selection.activeGameObject;
        if (selectedGo == null) return;

        Type winType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
        var wins = Resources.FindObjectsOfTypeAll(winType);
        if (wins == null || wins.Length == 0) return;
        EditorWindow win = wins[0] as EditorWindow;

        try
        {
            // 2. 내부 구조 접근 (Reflection)
            object editor = GetFieldDeep(win, "m_AnimEditor");
            if (editor == null) return;

            object state = GetFieldDeep(editor, "m_State");
            if (state == null) return;
            
            var rootProp = state.GetType().GetProperty("activeRootGameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            GameObject rootGo = rootProp?.GetValue(state) as GameObject;
            
            if (rootGo == null) return;

            string targetPath = AnimationUtility.CalculateTransformPath(selectedGo.transform, rootGo.transform);

            // 3. 데이터 확보
            object hd = GetFieldDeep(state, "hierarchyData");
            if (hd == null) return;

            MethodInfo getRows = hd.GetType().GetMethod("GetRows", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IEnumerable rows = getRows?.Invoke(hd, null) as IEnumerable;
            if (rows == null) return;

            List<int> matchingIDs = new List<int>();

            // 4. 경로 일치 항목 수집
            foreach (object item in rows)
            {
                if (item == null) continue;
                FieldInfo pathField = item.GetType().GetField("path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                string nodePath = pathField?.GetValue(item) as string;

                if (nodePath != null && (nodePath == targetPath || nodePath.EndsWith("/" + selectedGo.name)))
                {
                    PropertyInfo idProp = item.GetType().GetProperty("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    int id = (int)idProp.GetValue(item);
                    matchingIDs.Add(id);

                    // 그룹 펼치기
                    state.GetType().GetMethod("SetExpanded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         ?.Invoke(state, new object[] { id, true });
                }
            }

            // 5. 선택 및 스크롤 실행
            if (matchingIDs.Count > 0)
            {
                if(showLogs) Debug.Log($"[AnimSync] {selectedGo.name} -> {matchingIDs.Count}개 동기화.");

                FieldInfo hierarchyStateField = state.GetType().GetField("hierarchyState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object hierarchyState = hierarchyStateField.GetValue(state);
                PropertyInfo selectedIDsProp = hierarchyState.GetType().GetProperty("selectedIDs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                List<int> selectedIDs = selectedIDsProp.GetValue(hierarchyState) as List<int>;

                if (selectedIDs != null)
                {
                    selectedIDs.Clear();
                    selectedIDs.AddRange(matchingIDs);

                    // A. 데이터 갱신 알림
                    try {
                        MethodInfo onSelChanged = state.GetType().GetMethod("OnHierarchySelectionChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (onSelChanged != null) {
                            ParameterInfo[] p = onSelChanged.GetParameters();
                            if (p.Length == 0) onSelChanged.Invoke(state, null);
                            else if (p.Length == 1) onSelChanged.Invoke(state, new object[] { selectedIDs });
                        }
                    } catch { }

                    win.Repaint();

                    // B. [스마트 스크롤] 버전 호환성 자동 대응
                    EditorApplication.delayCall += () => 
                    {
                        EditorApplication.delayCall += () => 
                        {
                            if (win == null) return;
                            SmartScrollToID(editor, matchingIDs[0]); // <-- 여기가 핵심
                            win.Repaint();
                        };
                    };
                }
            }
        }
        catch (Exception e)
        {
            // 치명적이지 않은 오류는 로그를 껐을 때 무시
            if(showLogs) Debug.LogWarning($"[AnimSync Warning] {e.Message}");
        }
    }

    // [버전 호환성 해결] Unity 6와 Unity 2022.x를 모두 지원하는 스크롤 함수
    private static void SmartScrollToID(object animEditor, int id)
    {
        try
        {
            object hierarchy = GetFieldDeep(animEditor, "m_Hierarchy");
            if (hierarchy == null) return;

            object treeView = GetFieldDeep(hierarchy, "m_TreeView");
            if (treeView == null) treeView = GetFieldDeep(hierarchy, "treeView");
            if (treeView == null) return;

            Type treeType = treeView.GetType();

            // 1순위 시도: Unity 6 방식 (Frame)
            // 인자: (int id, bool frame, bool ping, bool animated)
            MethodInfo frameMethod = treeType.GetMethod("Frame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, 
                new Type[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) }, null);

            if (frameMethod != null)
            {
                // Unity 6용 호출
                frameMethod.Invoke(treeView, new object[] { id, true, false, true });
                return;
            }

            // 2순위 시도: Unity 2022.x 방식 (FrameItem)
            // 인자: (int id)
            MethodInfo frameItemMethod = treeType.GetMethod("FrameItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (frameItemMethod != null)
            {
                // Unity 2022용 호출
                frameItemMethod.Invoke(treeView, new object[] { id });
                return;
            }

            // 3순위: 알 수 없는 버전 (로그가 켜져있을 때만 경고)
            if(showLogs) Debug.LogWarning("[AnimSync] 호환되는 스크롤 함수(Frame/FrameItem)를 찾을 수 없습니다.");
        }
        catch (Exception ex)
        {
            if(showLogs) Debug.LogWarning($"스크롤 실패: {ex.Message}");
        }
    }

    private static object GetFieldDeep(object obj, string name)
    {
        if (obj == null) return null;
        Type type = obj.GetType();
        while (type != null)
        {
            FieldInfo f = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) return f.GetValue(obj);
            type = type.BaseType;
        }
        return null;
    }
}
#endif