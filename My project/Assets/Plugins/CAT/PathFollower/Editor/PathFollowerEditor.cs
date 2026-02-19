using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using CAT.Utility;

/// <summary>
/// PathFollower 컴포넌트의 커스텀 에디터.
/// SceneView에서 포인트/핸들 편집, 박스 선택, 세그먼트 클릭 추가, 핸들 회전 등 편의 기능을 제공한다.
/// </summary>
[CustomEditor(typeof(PathFollower))]
[CanEditMultipleObjects]
public class PathFollowerEditor : Editor
{
    #region 상수

    private const float CLICK_RADIUS      = 20f;   // 포인트/핸들 클릭 판정 반경 (픽셀)
    private const float HANDLE_SIZE       = 0.12f;  // 핸들 크기 배율
    private const float POINT_SIZE        = 0.18f;  // 포인트 크기 배율
    private const float TEST_DURATION     = 10f;    // 에디터 테스트 재생 시간 (초)
    private const float MAX_ADD_DIST_MULT = 0.5f;   // 세그먼트 추가 허용 최대 거리 배율

    private static readonly Color ColorPath         = new Color(0.2f, 1f, 0.2f);
    private static readonly Color ColorPoint        = new Color(1f, 1f, 1f);
    private static readonly Color ColorPointSel     = new Color(1f, 0.8f, 0f);
    private static readonly Color ColorHandleIn     = new Color(0.4f, 0.8f, 1f);
    private static readonly Color ColorHandleOut    = new Color(1f, 0.5f, 0.2f);
    private static readonly Color ColorHandleLine   = new Color(0.7f, 0.7f, 0.7f, 0.6f);
    private static readonly Color ColorRotMode      = new Color(1f, 1f, 0f, 0.9f);
    private static readonly Color ColorBoxSel       = new Color(0.4f, 0.6f, 1f, 0.25f);
    private static readonly Color ColorBoxSelBorder = new Color(0.4f, 0.6f, 1f, 0.8f);

    #endregion

    #region 내부 상태

    private PathFollower _follower;
    private bool _isMultiSelect;   // OnSceneGUI에서 targets 사용 금지 → OnEnable에서 캐싱
    private bool _isUIMode;        // Canvas 자식이면 Z=0 고정 모드

    // 포인트 선택
    private int _selectedIndex = -1;
    private readonly HashSet<int> _selectedIndices = new HashSet<int>();

    // 핸들 회전 모드
    private bool _rotationMode = false;

    // 박스 선택
    private bool _isBoxSelecting = false;
    private bool _boxAddMode     = false;  // Shift: 추가, 아닐 시: 교체
    private Vector2 _boxStart;
    private Vector2 _boxEnd;

    // 에디터 테스트
    private bool   _isTestPlaying = false;
    private double _testStartTime;

    // 경로 도구 (Circle / Expand)
    private float _circleRadius   = 5f;
    private int   _circleSegments = 4;
    private float _expandAmount   = 1f;

    #endregion

    #region Editor 생명주기

    private void OnEnable()
    {
        _follower      = (PathFollower)targets[0];
        _isMultiSelect = targets.Length > 1;
        _isUIMode      = _follower != null && _follower.GetComponentInParent<Canvas>() != null;
        EditorApplication.update -= OnEditorUpdate;
        _isTestPlaying = false;
        // UI 모드 여부에 따라 기본 반지름 설정
        _circleRadius  = _isUIMode ? 200f : 5f;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        _isTestPlaying = false;
    }

    #endregion

    #region 인스펙터 GUI

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPathSection();
        EditorGUILayout.Space(4);
        DrawMovementSection();

        if (!Application.isPlaying && !_isMultiSelect)
        {
            EditorGUILayout.Space(4);
            DrawPathToolsSection();
        }

        EditorGUILayout.Space(4);

        if (!Application.isPlaying)
            DrawTestSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPathSection()
    {
        EditorGUILayout.LabelField("Path Settings", EditorStyles.boldLabel);

        var loopProp = serializedObject.FindProperty("_isLoop");
        EditorGUILayout.PropertyField(loopProp, new GUIContent("Is Loop"));

        if (_isUIMode)
            EditorGUILayout.HelpBox("UI 모드: Canvas 자식 오브젝트 - Z축 고정(0), XY만 편집 가능합니다.", MessageType.Info);

        EditorGUILayout.Space(4);

        if (_isMultiSelect)
        {
            EditorGUILayout.HelpBox("다중 선택 시 포인트 편집은 지원되지 않습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Points: {_follower.PointCount}", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 포인트 추가", GUILayout.Height(22)))
            AddPointAtEnd();

        if (GUILayout.Button("경로 초기화", GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog("경로 초기화", "모든 포인트를 초기화하시겠습니까?", "초기화", "취소"))
            {
                Undo.RecordObject(_follower, "Reset Path");
                _follower.ClearPoints();
                ClearSelection();
                EditorUtility.SetDirty(_follower);
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (_selectedIndex >= 0 && _selectedIndex < _follower.PointCount)
            DrawPointInfoBox(_selectedIndex);
        else if (_selectedIndices.Count > 1)
            EditorGUILayout.HelpBox($"{_selectedIndices.Count}개 포인트 선택됨. SceneView에서 이동 가능.", MessageType.None);
        else
            EditorGUILayout.HelpBox(
                "SceneView 조작 방법:\n" +
                "  클릭 - 선택 / Shift+클릭 - 추가 선택\n" +
                "  Alt+클릭(곡선) - 그 위치에 포인트 삽입\n" +
                "  Ctrl+드래그 - 박스 선택\n" +
                "  우클릭(포인트) - 컨텍스트 메뉴\n" +
                "  R키 - 핸들 회전 모드 토글",
                MessageType.None);
    }

    private void DrawPointInfoBox(int index)
    {
        PathPoint point = _follower.GetPoint(index);
        if (point == null) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Point [{index}]", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        Vector3 worldPos    = _follower.GetPointWorldPosition(index);
        Vector3 newWorldPos = EditorGUILayout.Vector3Field("World Position", worldPos);
        Vector3 newHIn      = EditorGUILayout.Vector3Field("Handle In (Local)", point.handleIn);
        Vector3 newHOut     = EditorGUILayout.Vector3Field("Handle Out (Local)", point.handleOut);
        bool    newBroken   = EditorGUILayout.Toggle("Is Broken", point.isBroken);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_follower, "Edit PathPoint");
            if (newWorldPos != worldPos)
            {
                _follower.SetPointPosition(index, newWorldPos);
            }
            else
            {
                point.handleIn  = newHIn;
                point.handleOut = newHOut;
                point.isBroken  = newBroken;
                _follower.SetPoint(index, point);
            }
            EditorUtility.SetDirty(_follower);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("앞에 삽입"))
            InsertPointBefore(index);

        if (GUILayout.Button("뒤에 삽입"))
            InsertPointAfter(index);

        if (_follower.PointCount > 2)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("삭제"))
            {
                Undo.RecordObject(_follower, "Remove PathPoint");
                _follower.RemovePoint(index);
                ClearSelection();
                EditorUtility.SetDirty(_follower);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prevBg;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawMovementSection()
    {
        EditorGUILayout.LabelField("Movement Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("duration"),        new GUIContent("Duration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("movementCurve"),   new GUIContent("Movement Curve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startOffset"),     new GUIContent("Start Offset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("followRotation"),  new GUIContent("Follow Rotation"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("loopType"),        new GUIContent("Loop Type"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("progress"),   new GUIContent("Progress"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isPlaying"),  new GUIContent("Is Playing"));
    }

    private void DrawPathToolsSection()
    {
        EditorGUILayout.LabelField("Path Tools", EditorStyles.boldLabel);

        // ── 원형 프리셋 ──────────────────────────────────────
        EditorGUILayout.LabelField("원형 프리셋", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("반지름", GUILayout.Width(44));
        _circleRadius = EditorGUILayout.FloatField(_circleRadius, GUILayout.Width(56));
        EditorGUILayout.LabelField("정점 수", GUILayout.Width(44));
        _circleSegments = EditorGUILayout.IntField(Mathf.Max(3, _circleSegments), GUILayout.Width(36));
        if (GUILayout.Button("원형 생성"))
        {
            if (EditorUtility.DisplayDialog("원형 경로 생성",
                $"반지름 {_circleRadius}, 정점 {_circleSegments}개의 원형 경로를 생성합니다.\n기존 경로는 초기화됩니다.",
                "생성", "취소"))
            {
                Undo.RecordObject(_follower, "Set Circle Path");
                _follower.SetCircle(_circleRadius, _circleSegments);
                // IsLoop 변경이 SerializedObject에 반영되도록 동기화
                serializedObject.Update();
                ClearSelection();
                EditorUtility.SetDirty(_follower);
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── 확대/축소 ─────────────────────────────────────────
        EditorGUILayout.LabelField("확대 / 축소 (Expand)", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("크기", GUILayout.Width(28));
        _expandAmount = EditorGUILayout.FloatField(_expandAmount, GUILayout.Width(56));

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("확대 (+)"))
        {
            Undo.RecordObject(_follower, "Expand Path");
            _follower.ExpandPath(_expandAmount);
            EditorUtility.SetDirty(_follower);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = new Color(1f, 0.7f, 0.6f);
        if (GUILayout.Button("축소 (-)"))
        {
            Undo.RecordObject(_follower, "Contract Path");
            _follower.ExpandPath(-_expandAmount);
            EditorUtility.SetDirty(_follower);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── 핸들 자동 조정 (Relax) ────────────────────────────
        EditorGUILayout.LabelField("핸들 자동 조정 (Relax)", EditorStyles.miniLabel);
        if (GUILayout.Button("전체 Relax"))
        {
            Undo.RecordObject(_follower, "Relax Path");
            _follower.RelaxPath();
            EditorUtility.SetDirty(_follower);
            SceneView.RepaintAll();
        }
    }

    private void DrawTestSection()
    {
        EditorGUILayout.LabelField("Editor Test", EditorStyles.boldLabel);

        if (_isMultiSelect)
        {
            EditorGUILayout.HelpBox("테스트 기능은 단일 오브젝트 선택 시에만 사용 가능합니다.", MessageType.Info);
            return;
        }

        if (_isTestPlaying)
        {
            float remaining = TEST_DURATION - (float)(EditorApplication.timeSinceStartup - _testStartTime);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button($"⏹ 테스트 중지 (남은: {remaining:F1}s)", GUILayout.Height(30)))
                StopEditorTest();
            GUI.backgroundColor = prevBg;
        }
        else
        {
            EditorGUI.BeginDisabledGroup(_follower == null || _follower.PointCount < 2);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button($"▶ {TEST_DURATION:F0}초 테스트", GUILayout.Height(30)))
                StartEditorTest();
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            if (_follower != null && _follower.PointCount < 2)
                EditorGUILayout.HelpBox("포인트가 2개 이상 필요합니다.", MessageType.Warning);
        }
    }

    #endregion

    #region SceneView GUI

    private void OnSceneGUI()
    {
        if (_follower == null || _follower.PointCount < 2) return;
        if (_isMultiSelect) return;

        // 단축키 처리 (R키 회전 모드, Delete/Esc)
        HandleKeyboard();

        // Alt+클릭: 세그먼트 위에 포인트 삽입
        HandleAltClickInsert();

        // 우클릭 컨텍스트 메뉴
        HandleRightClickMenu();

        // Ctrl+드래그: 박스 선택
        HandleBoxSelectionInput();

        // 경로 그리기
        DrawPath();

        // 포인트 / 핸들 그리기 및 드래그
        DrawAndHandlePoints();

        // 회전 모드 핸들
        if (_rotationMode)
            DrawRotationHandle();

        // 박스 선택 사각형 그리기
        if (_isBoxSelecting)
            DrawBoxRect();
    }

    #endregion

    #region 경로 그리기

    private void DrawPath()
    {
        int count = _follower.PointCount;
        int segs  = _follower.IsLoop ? count : count - 1;

        Handles.color = ColorPath;
        for (int i = 0; i < segs; i++)
        {
            int ni = (_follower.IsLoop && i == count - 1) ? 0 : i + 1;
            PathPoint p0 = _follower.GetPoint(i);
            PathPoint p1 = _follower.GetPoint(ni);
            if (p0 == null || p1 == null) continue;

            Vector3 wp0  = _follower.PathToWorld(p0.position);
            Vector3 wp0o = _follower.PathToWorld(p0.handleOut);
            Vector3 wp1i = _follower.PathToWorld(p1.handleIn);
            Vector3 wp1  = _follower.PathToWorld(p1.position);
            Handles.DrawBezier(wp0, wp1, wp0o, wp1i, ColorPath, null, 2f);
        }
    }

    #endregion

    #region 포인트 / 핸들 드래그

    private void DrawAndHandlePoints()
    {
        int count = _follower.PointCount;
        Event e   = Event.current;

        for (int i = 0; i < count; i++)
        {
            PathPoint point = _follower.GetPoint(i);
            if (point == null) continue;

            Vector3 wPos  = _follower.PathToWorld(point.position);
            Vector3 wHIn  = _follower.PathToWorld(point.handleIn);
            Vector3 wHOut = _follower.PathToWorld(point.handleOut);
            bool isSel    = _selectedIndices.Contains(i);

            // --- 핸들선 + 핸들 드래그 ---
            if (isSel && !_rotationMode)
            {
                Handles.color = ColorHandleLine;
                Handles.DrawLine(wPos, wHIn);
                Handles.DrawLine(wPos, wHOut);

                // handleIn
                Handles.color = ColorHandleIn;
                float szIn = HandleUtility.GetHandleSize(wHIn) * HANDLE_SIZE;
                EditorGUI.BeginChangeCheck();
                Vector3 newWHIn = Handles.FreeMoveHandle(wHIn, szIn, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_follower, "Move Handle In");
                    PathPoint up = _follower.GetPoint(i);
                    Vector3 localNew = InvTransform(newWHIn);
                    up.handleIn = localNew;
                    if (!up.isBroken)
                        up.handleOut = up.position - (localNew - up.position);
                    _follower.SetPoint(i, up);
                    EditorUtility.SetDirty(_follower);
                }

                // handleOut
                Handles.color = ColorHandleOut;
                float szOut = HandleUtility.GetHandleSize(wHOut) * HANDLE_SIZE;
                EditorGUI.BeginChangeCheck();
                Vector3 newWHOut = Handles.FreeMoveHandle(wHOut, szOut, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_follower, "Move Handle Out");
                    PathPoint up = _follower.GetPoint(i);
                    Vector3 localNew = InvTransform(newWHOut);
                    up.handleOut = localNew;
                    if (!up.isBroken)
                        up.handleIn = up.position - (localNew - up.position);
                    _follower.SetPoint(i, up);
                    EditorUtility.SetDirty(_follower);
                }
            }

            // --- 포인트 버튼 (클릭 선택) ---
            Handles.color = isSel ? ColorPointSel : ColorPoint;
            float ptSz = HandleUtility.GetHandleSize(wPos) * POINT_SIZE;

            if (Handles.Button(wPos, Quaternion.identity, ptSz, ptSz * 1.5f, Handles.SphereHandleCap))
            {
                if (e.shift)
                {
                    // Shift: 다중 선택 토글
                    if (_selectedIndices.Contains(i)) _selectedIndices.Remove(i);
                    else                               _selectedIndices.Add(i);
                    _selectedIndex = i;
                }
                else
                {
                    // 단독 선택
                    _selectedIndices.Clear();
                    _selectedIndices.Add(i);
                    _selectedIndex = i;
                }
                _rotationMode = false;
                Repaint();
            }

            // --- 포인트 이동 핸들 (선택됐을 때, 회전 모드 아닐 때) ---
            if (isSel && !_rotationMode)
            {
                Quaternion rot = Tools.pivotRotation == PivotRotation.Global
                    ? Quaternion.identity
                    : _follower.transform.rotation;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = _isUIMode
                    ? MoveHandle2D(wPos, rot)
                    : Handles.PositionHandle(wPos, rot);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_follower, "Move PathPoint");

                    Vector3 localOld = _follower.WorldToPath(wPos);
                    Vector3 localNew = _follower.WorldToPath(newPos);
                    if (_isUIMode) localNew.z = 0f;
                    Vector3 delta = localNew - localOld;

                    if (_selectedIndices.Count > 1)
                    {
                        foreach (int idx in _selectedIndices)
                        {
                            PathPoint mp = _follower.GetPoint(idx);
                            if (mp == null) continue;
                            mp.position  += delta;
                            mp.handleIn  += delta;
                            mp.handleOut += delta;
                            _follower.SetPoint(idx, mp);
                        }
                    }
                    else
                    {
                        Vector3 worldSnapped = _follower.PathToWorld(localNew);
                        _follower.SetPointPosition(i, worldSnapped);
                    }

                    EditorUtility.SetDirty(_follower);
                    Repaint();
                }
            }
        }
    }

    /// <summary>Canvas(UI 모드)용 2D 이동 핸들 (Z축 잠금)</summary>
    private Vector3 MoveHandle2D(Vector3 worldPos, Quaternion rot)
    {
        Vector3 normal = _follower.transform.forward;
        Vector3 right  = _follower.transform.right;
        Vector3 up     = _follower.transform.up;
        float   sz     = HandleUtility.GetHandleSize(worldPos) * POINT_SIZE * 1.5f;
        return Handles.Slider2D(worldPos, normal, right, up, sz, Handles.RectangleHandleCap, 0f);
    }

    #endregion

    #region 핸들 회전 모드

    private void DrawRotationHandle()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _follower.PointCount) return;

        PathPoint point = _follower.GetPoint(_selectedIndex);
        if (point == null) return;

        Vector3 wPos    = _follower.PathToWorld(point.position);
        Vector3 wHOut   = _follower.PathToWorld(point.handleOut);
        Vector3 outDir  = wHOut - wPos;
        float   outLen  = outDir.magnitude;

        if (outLen < 0.0001f) return;

        // 현재 각도 계산 (Z축 기준 2D 회전)
        float currentAngle = Mathf.Atan2(outDir.y, outDir.x) * Mathf.Rad2Deg;
        Quaternion discRot = Quaternion.AngleAxis(currentAngle, _follower.transform.forward);
        float discSz       = HandleUtility.GetHandleSize(wPos) * 1.8f;

        // 핸들 선 표시
        Handles.color = ColorHandleLine;
        Handles.DrawLine(wPos, wHOut);
        Vector3 wHIn = _follower.PathToWorld(point.handleIn);
        Handles.DrawLine(wPos, wHIn);

        Handles.color = ColorRotMode;
        EditorGUI.BeginChangeCheck();
        Quaternion newRot = Handles.Disc(discRot, wPos, _follower.transform.forward, discSz, false, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Quaternion delta = newRot * Quaternion.Inverse(discRot);
            Undo.RecordObject(_follower, "Rotate PathPoint Handles");

            PathPoint up = _follower.GetPoint(_selectedIndex);
            Vector3 wIn  = _follower.PathToWorld(up.handleIn);
            Vector3 wOut = _follower.PathToWorld(up.handleOut);

            Vector3 newWOut = wPos + delta * (wOut - wPos);
            Vector3 newWIn  = wPos + delta * (wIn  - wPos);

            Vector3 lOut = InvTransform(newWOut);
            Vector3 lIn  = InvTransform(newWIn);
            if (_isUIMode) { lOut.z = 0f; lIn.z = 0f; }

            up.handleOut = lOut;
            up.handleIn  = lIn;
            _follower.SetPoint(_selectedIndex, up);
            EditorUtility.SetDirty(_follower);
        }
    }

    #endregion

    #region Alt+클릭: 세그먼트 위에 포인트 삽입

    private void HandleAltClickInsert()
    {
        Event e = Event.current;
        if (!e.alt || e.type != EventType.MouseDown || e.button != 0) return;

        // 기존 포인트/핸들 근처 클릭은 무시
        if (IsNearExistingPoint(e.mousePosition)) return;

        // 마우스 → 월드 좌표 변환
        Vector3 worldClick;
        if (!ScreenToWorld(e.mousePosition, out worldClick)) return;

        if (FindClosestSegment(worldClick, out int segIdx, out float t))
        {
            InsertPointOnSegment(segIdx, t);
            e.Use();
        }
    }

    /// <summary>스크린 좌표를 평면(forward = transform.forward)에 투영한 월드 좌표로 변환</summary>
    private bool ScreenToWorld(Vector2 screenPos, out Vector3 worldPos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(screenPos);
        Plane plane = new Plane(_follower.transform.forward, _follower.transform.position);
        if (plane.Raycast(ray, out float dist))
        {
            worldPos = ray.GetPoint(dist);
            return true;
        }
        worldPos = Vector3.zero;
        return false;
    }

    /// <summary>클릭 위치와 가장 가까운 세그먼트 및 t를 반환한다.</summary>
    private bool FindClosestSegment(Vector3 worldClick, out int bestSeg, out float bestT)
    {
        bestSeg = -1;
        bestT   = 0f;
        float minDist = float.MaxValue;

        int count = _follower.PointCount;
        int segs  = _follower.IsLoop ? count : count - 1;

        for (int s = 0; s < segs; s++)
        {
            int ni = (_follower.IsLoop && s == count - 1) ? 0 : s + 1;
            PathPoint p0 = _follower.GetPoint(s);
            PathPoint p1 = _follower.GetPoint(ni);
            if (p0 == null || p1 == null) continue;

            Vector3 wp0  = _follower.PathToWorld(p0.position);
            Vector3 wp0o = _follower.PathToWorld(p0.handleOut);
            Vector3 wp1i = _follower.PathToWorld(p1.handleIn);
            Vector3 wp1  = _follower.PathToWorld(p1.position);

            // 50샘플로 가장 가까운 t 탐색
            for (int k = 0; k <= 50; k++)
            {
                float t = k / 50f;
                Vector3 pt = EvalBezier(wp0, wp0o, wp1i, wp1, t);
                float dist = Vector3.Distance(pt, worldClick);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestSeg = s;
                    bestT   = t;
                }
            }
        }

        // 스크린 픽셀 기준 허용 거리
        float maxDist = HandleUtility.GetHandleSize(worldClick) * MAX_ADD_DIST_MULT;
        return bestSeg >= 0 && minDist < maxDist;
    }

    private static Vector3 EvalBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0
             + 3f * u * u * t * p1
             + 3f * u * t * t * p2
             + t * t * t * p3;
    }

    /// <summary>기존 포인트/핸들 근처에 마우스가 있는지 확인</summary>
    private bool IsNearExistingPoint(Vector2 mousePos)
    {
        int count = _follower.PointCount;
        for (int i = 0; i < count; i++)
        {
            PathPoint p = _follower.GetPoint(i);
            if (p == null) continue;

            if (GUIDist(mousePos, _follower.PathToWorld(p.position))  < CLICK_RADIUS) return true;
            if (GUIDist(mousePos, _follower.PathToWorld(p.handleIn))  < CLICK_RADIUS) return true;
            if (GUIDist(mousePos, _follower.PathToWorld(p.handleOut)) < CLICK_RADIUS) return true;
        }
        return false;
    }

    private static float GUIDist(Vector2 mousePos, Vector3 worldPos)
        => Vector2.Distance(mousePos, HandleUtility.WorldToGUIPoint(worldPos));

    #endregion

    #region 우클릭 컨텍스트 메뉴

    private void HandleRightClickMenu()
    {
        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 1) return;

        int count = _follower.PointCount;
        for (int i = 0; i < count; i++)
        {
            PathPoint p = _follower.GetPoint(i);
            if (p == null) continue;

            Vector3 wPos = _follower.PathToWorld(p.position);
            if (GUIDist(e.mousePosition, wPos) >= CLICK_RADIUS) continue;

            e.Use();
            int capturedIndex = i;
            bool isBroken     = p.isBroken;

            GenericMenu menu = new GenericMenu();

            // 핸들 연결/끊기
            if (isBroken)
            {
                menu.AddItem(new GUIContent("핸들 연결"), false, () =>
                {
                    Undo.RecordObject(_follower, "Link Handles");
                    PathPoint up = _follower.GetPoint(capturedIndex);
                    if (up == null) return;
                    up.isBroken = false;
                    // handleOut 기준으로 handleIn 미러링
                    Vector3 dir = up.handleOut - up.position;
                    up.handleIn = up.position - dir;
                    _follower.SetPoint(capturedIndex, up);
                    EditorUtility.SetDirty(_follower);
                    SceneView.RepaintAll();
                });
            }
            else
            {
                menu.AddItem(new GUIContent("핸들 끊기"), false, () =>
                {
                    Undo.RecordObject(_follower, "Break Handles");
                    PathPoint up = _follower.GetPoint(capturedIndex);
                    if (up == null) return;
                    up.isBroken = true;
                    _follower.SetPoint(capturedIndex, up);
                    EditorUtility.SetDirty(_follower);
                    SceneView.RepaintAll();
                });
            }

            // 핸들 자동 초기화 (이웃 정점 기반 Catmull-Rom 계산)
            menu.AddItem(new GUIContent("핸들 초기화 (Auto)"), false, () =>
            {
                Undo.RecordObject(_follower, "Reset Handles");
                _follower.RelaxPoint(capturedIndex);
                EditorUtility.SetDirty(_follower);
                SceneView.RepaintAll();
            });

            menu.AddSeparator("");

            // 앞/뒤 삽입
            menu.AddItem(new GUIContent("앞에 포인트 삽입"), false, () => InsertPointBefore(capturedIndex));
            menu.AddItem(new GUIContent("뒤에 포인트 삽입"), false, () => InsertPointAfter(capturedIndex));

            menu.AddSeparator("");

            // 삭제
            if (_follower.PointCount > 2)
            {
                menu.AddItem(new GUIContent("포인트 삭제"), false, () =>
                {
                    Undo.RecordObject(_follower, "Remove PathPoint");
                    _follower.RemovePoint(capturedIndex);
                    ClearSelection();
                    EditorUtility.SetDirty(_follower);
                    SceneView.RepaintAll();
                    Repaint();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("포인트 삭제 (최소 2개 필요)"));
            }

            menu.ShowAsContext();
            return;
        }
    }

    #endregion

    #region Ctrl+드래그: 박스 선택

    private void HandleBoxSelectionInput()
    {
        Event e          = Event.current;
        bool  isCtrl     = e.control || e.command;
        bool  isShift    = e.shift;

        // Ctrl+MouseDown → 박스 선택 시작
        if (e.type == EventType.MouseDown && e.button == 0 && isCtrl)
        {
            _isBoxSelecting = true;
            _boxAddMode     = isShift;
            _boxStart       = e.mousePosition;
            _boxEnd         = e.mousePosition;
            e.Use();
            return;
        }

        if (!_isBoxSelecting) return;

        if (e.type == EventType.MouseDrag)
        {
            _boxEnd     = e.mousePosition;
            _boxAddMode = isShift;
            e.Use();
            SceneView.RepaintAll();
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            _isBoxSelecting = false;
            ApplyBoxSelection();
            e.Use();
            Repaint();
        }
        // Escape 중단
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            _isBoxSelecting = false;
            e.Use();
            SceneView.RepaintAll();
        }
    }

    private void ApplyBoxSelection()
    {
        Rect rect = GetBoxRect();
        if (!_boxAddMode) _selectedIndices.Clear();

        int count = _follower.PointCount;
        for (int i = 0; i < count; i++)
        {
            PathPoint p = _follower.GetPoint(i);
            if (p == null) continue;

            Vector2 guiPos = HandleUtility.WorldToGUIPoint(_follower.PathToWorld(p.position));
            if (rect.Contains(guiPos))
            {
                _selectedIndices.Add(i);
                _selectedIndex = i;
            }
        }
    }

    private void DrawBoxRect()
    {
        Rect rect = GetBoxRect();
        Handles.BeginGUI();
        GUI.color = ColorBoxSel;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = ColorBoxSelBorder;
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        Handles.EndGUI();
    }

    private Rect GetBoxRect() => new Rect(
        Mathf.Min(_boxStart.x, _boxEnd.x),
        Mathf.Min(_boxStart.y, _boxEnd.y),
        Mathf.Abs(_boxEnd.x - _boxStart.x),
        Mathf.Abs(_boxEnd.y - _boxStart.y));

    #endregion

    #region 키보드 처리

    private void HandleKeyboard()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        // R: 회전 모드 토글
        if (e.keyCode == KeyCode.R && _selectedIndex >= 0)
        {
            _rotationMode = !_rotationMode;
            e.Use();
            SceneView.RepaintAll();
            Repaint();
            return;
        }

        // Delete/Backspace: 선택 포인트 삭제
        if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            && _selectedIndices.Count > 0
            && _follower.PointCount - _selectedIndices.Count >= 2)
        {
            Undo.RecordObject(_follower, "Delete PathPoints");
            List<int> toRemove = new List<int>(_selectedIndices);
            toRemove.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in toRemove)
            {
                if (_follower.PointCount > 2)
                    _follower.RemovePoint(idx);
            }
            ClearSelection();
            EditorUtility.SetDirty(_follower);
            SceneView.RepaintAll();
            Repaint();
            e.Use();
            return;
        }

        // Escape: 선택 해제 / 회전 모드 해제
        if (e.keyCode == KeyCode.Escape)
        {
            if (_rotationMode) _rotationMode = false;
            else               ClearSelection();
            e.Use();
            SceneView.RepaintAll();
            Repaint();
        }
    }

    #endregion

    #region 포인트 추가 헬퍼

    private void AddPointAtEnd()
    {
        Undo.RecordObject(_follower, "Add PathPoint");
        int count = _follower.PointCount;

        Vector3 lastWorld = _follower.GetPointWorldPosition(count - 1);
        Vector3 newWorld;

        if (count >= 2)
        {
            Vector3 prevWorld = _follower.GetPointWorldPosition(count - 2);
            Vector3 dir  = (lastWorld - prevWorld).normalized;
            float   dist = Vector3.Distance(lastWorld, prevWorld);
            newWorld = lastWorld + dir * Mathf.Max(dist, 1f);
        }
        else
        {
            newWorld = lastWorld + (_isUIMode ? Vector3.right * 200f : Vector3.right * 5f);
        }

        _follower.AddPoint(newWorld);
        SelectOnly(_follower.PointCount - 1);
        EditorUtility.SetDirty(_follower);
        SceneView.RepaintAll();
    }

    private void InsertPointBefore(int index)
    {
        Undo.RecordObject(_follower, "Insert PathPoint");
        int prev = index > 0 ? index - 1 : (_follower.IsLoop ? _follower.PointCount - 1 : 0);
        Vector3 pos = (_follower.GetPointWorldPosition(prev) + _follower.GetPointWorldPosition(index)) * 0.5f;
        _follower.InsertPoint(index, pos);
        SelectOnly(index);
        EditorUtility.SetDirty(_follower);
        SceneView.RepaintAll();
    }

    private void InsertPointAfter(int index)
    {
        Undo.RecordObject(_follower, "Insert PathPoint");
        int next = (index < _follower.PointCount - 1) ? index + 1 : (_follower.IsLoop ? 0 : index);
        Vector3 pos = (_follower.GetPointWorldPosition(index) + _follower.GetPointWorldPosition(next)) * 0.5f;
        int insertAt = Mathf.Min(index + 1, _follower.PointCount);
        _follower.InsertPoint(insertAt, pos);
        SelectOnly(insertAt);
        EditorUtility.SetDirty(_follower);
        SceneView.RepaintAll();
    }

    /// <summary>세그먼트 위의 t 위치에 포인트를 삽입한다.</summary>
    private void InsertPointOnSegment(int segIndex, float t)
    {
        Undo.RecordObject(_follower, "Insert PathPoint on Segment");
        int count  = _follower.PointCount;
        int niNext = (_follower.IsLoop && segIndex == count - 1) ? 0 : segIndex + 1;

        PathPoint p0 = _follower.GetPoint(segIndex);
        PathPoint p1 = _follower.GetPoint(niNext);

        // 드 카스텔조 알고리즘으로 분할점 계산 (로컬 좌표)
        Vector3 lp0  = p0.position;
        Vector3 lp0o = p0.handleOut;
        Vector3 lp1i = p1.handleIn;
        Vector3 lp1  = p1.position;

        Vector3 q0 = Vector3.Lerp(lp0,  lp0o, t);
        Vector3 q1 = Vector3.Lerp(lp0o, lp1i, t);
        Vector3 q2 = Vector3.Lerp(lp1i, lp1,  t);
        Vector3 r0 = Vector3.Lerp(q0, q1, t);
        Vector3 r1 = Vector3.Lerp(q1, q2, t);
        Vector3 mid = Vector3.Lerp(r0, r1, t);

        // 새 포인트의 핸들 업데이트
        PathPoint newPoint = new PathPoint(mid)
        {
            handleIn  = r0,
            handleOut = r1,
            isBroken  = false
        };

        // 기존 세그먼트의 핸들 조정
        PathPoint upP0 = _follower.GetPoint(segIndex);
        upP0.handleOut = q0;
        _follower.SetPoint(segIndex, upP0);

        PathPoint upP1 = _follower.GetPoint(niNext);
        upP1.handleIn = q2;
        _follower.SetPoint(niNext, upP1);

        // 새 포인트 삽입 (segIndex+1 위치)
        int insertAt = segIndex + 1;
        _follower.EditorInsertPoint(insertAt, newPoint);

        SelectOnly(insertAt);
        EditorUtility.SetDirty(_follower);
        SceneView.RepaintAll();
        Repaint();
    }

    #endregion

    #region 에디터 테스트 재생

    private void StartEditorTest()
    {
        if (_isTestPlaying || _follower == null) return;
        _testStartTime = EditorApplication.timeSinceStartup;
        _isTestPlaying = true;
        EditorApplication.update += OnEditorUpdate;
        _follower._lastEditorUpdateTime = (float)_testStartTime;
        Undo.RecordObject(_follower.transform, "Start PathFollower Test");
        _follower.StartEditorTest(TEST_DURATION);
        Debug.Log($"[PathFollower] {_follower.name}: 테스트 시작 ({TEST_DURATION}초)");
    }

    private void StopEditorTest()
    {
        EditorApplication.update -= OnEditorUpdate;
        _isTestPlaying = false;
        if (_follower != null)
        {
            Undo.RecordObject(_follower.transform, "Stop PathFollower Test");
            _follower.StopEditorTest();
        }
        Repaint();
        SceneView.RepaintAll();
        if (_follower != null)
            Debug.Log($"[PathFollower] {_follower.name}: 테스트 중지");
    }

    private void OnEditorUpdate()
    {
        if (_follower == null || _follower.PointCount < 2) { StopEditorTest(); return; }

        double elapsed = EditorApplication.timeSinceStartup - _testStartTime;
        if (elapsed >= TEST_DURATION) { StopEditorTest(); return; }

        float now   = (float)EditorApplication.timeSinceStartup;
        float dt    = now - _follower._lastEditorUpdateTime;
        _follower._lastEditorUpdateTime = now;

        if (_follower.EditorIsForward) _follower.EditorTimer += dt / _follower.duration;
        else                           _follower.EditorTimer -= dt / _follower.duration;

        if (_follower.EditorTimer >= 1f)
        {
            switch (_follower.loopType)
            {
                case PathFollower.LoopType.Restart: _follower.EditorTimer = 0f;                           break;
                case PathFollower.LoopType.Yoyo:    _follower.EditorTimer = 1f; _follower.EditorIsForward = false; break;
                default:                             _follower.EditorTimer = 0f;                           break;
            }
        }
        else if (_follower.EditorTimer <= 0f && _follower.loopType == PathFollower.LoopType.Yoyo)
        {
            _follower.EditorTimer = 0f;
            _follower.EditorIsForward = true;
        }

        Undo.RecordObject(_follower.transform, "PathFollower Test Update");
        _follower.EditorTransformDirty = true;
        _follower.UpdatePosition();
        SceneView.RepaintAll();
        Repaint();
    }

    #endregion

    #region 유틸리티

    private void ClearSelection()
    {
        _selectedIndices.Clear();
        _selectedIndex = -1;
        _rotationMode  = false;
    }

    private void SelectOnly(int index)
    {
        _selectedIndices.Clear();
        _selectedIndices.Add(index);
        _selectedIndex = index;
    }

    /// <summary>월드 → path 좌표 변환 (UI 모드 시 Z=0 클램프)</summary>
    private Vector3 InvTransform(Vector3 worldPos)
    {
        Vector3 local = _follower.WorldToPath(worldPos);
        if (_isUIMode) local.z = 0f;
        return local;
    }

    #endregion
}
