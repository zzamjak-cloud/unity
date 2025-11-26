using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(BezierPath))]
public class BezierPathEditor : Editor
{
    #region Constants
    private const float CLICK_RADIUS = 20f;                 // 정점/핸들 클릭 반경
    private const float EPSILON = 0.001f;                   // 거의 0인 값
    private const float SELECTED_POINT_RADIUS = 0.25f;      // 선택된 정점 반경
    private const float HANDLE_LENGTH_FACTOR = 0.3f;        // 핸들 길이 계수
    private const float CIRCLE_HANDLE_FACTOR = 0.552f;      // 4/3 * tan(π/8)    // 원 핸들 계수
    private const float HANDLE_SIZE = 0.1f;                 // 핸들 크기
    private const float POINT_HANDLE_SIZE = 0.2f;           // 정점 핸들 크기
    private const float CURVE_LINE_WIDTH = 3f;              // 곡선 선 너비
    private const float DEFAULT_RADIUS = 5f;                // 기본 반경
    private const int SPIRAL_POINT_COUNT = 10;              // 나선 정점 개수
    private const int MIN_POINTS_REQUIRED = 2;              // 최소 정점 개수
    #endregion

    #region Enums
    private enum DraggingElementType
    {
        None = 0,
        Point = 1,
        HandleIn = 2,
        HandleOut = 3
    }

    private enum AlignDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }
    #endregion

    #region Private Fields
    private BezierPath path;
    private Transform handleTransform;
    private Quaternion handleRotation;
    private int selectedPointIndex = -1;
    private bool isEditingPosition = false;
    
    // Shift 키 축 고정을 위한 변수
    private Vector3 dragStartPosition = Vector3.zero;
    private Vector3 dragStartHandleIn = Vector3.zero;
    private Vector3 dragStartHandleOut = Vector3.zero;
    private bool isDragging = false;
    private DraggingElementType draggingElementType = DraggingElementType.None;
    
    // 다중 선택 정점 이동을 위한 변수
    private Dictionary<int, Vector3> selectedPointsStartPositions = new Dictionary<int, Vector3>();
    
    // 여러 정점 선택을 위한 변수
    private HashSet<int> selectedPointIndices = new HashSet<int>();
    private bool isBoxSelecting = false;
    private Vector2 boxSelectStart = Vector2.zero;
    private Vector2 boxSelectEnd = Vector2.zero;
    private bool isBoxSelectAddMode = false;
    private bool isBoxSelectSubtractMode = false;
    
    // UI 상태
    private int _polygonSides = 4;
    private bool _useHandles = true;
    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        path = (BezierPath)target;
        EnsureInitialized();
    }

    // 초기화 확인 (SerializedProperty가 없으면 초기화)
    private void EnsureInitialized()
    {
        if (path == null) return;

        SerializedProperty pointsProp = serializedObject.FindProperty("points");

        if (pointsProp == null || !pointsProp.isArray || pointsProp.arraySize == 0)
        {
            serializedObject.Update();
            path.Initialize();
            SaveChanges();
        }
    }
    #endregion

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        path = (BezierPath)target;
        
        EnsureInitialized();

        DrawDefaultInspector();

        // isLoop 체크박스 표시
        SerializedProperty isLoopProp = serializedObject.FindProperty("isLoop");
        if (isLoopProp != null)
        {
            EditorGUI.BeginChangeCheck();
            bool newIsLoop = EditorGUILayout.Toggle("Is Loop", path.IsLoop);
            if (EditorGUI.EndChangeCheck())
            {
                path.IsLoop = newIsLoop;
                SaveChanges("Toggle Is Loop");
            }
        }

        // 현재 포인트 개수 표시 (디버깅용)
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        if (pointsProp != null)
        {
            EditorGUILayout.HelpBox($"Points Count: {pointsProp.arraySize}", MessageType.Info);
        }

        // ScriptableObject 관리 (필수)
        GUILayout.Space(10);
        EditorGUILayout.LabelField("ScriptableObject", EditorStyles.boldLabel);
        
        SerializedProperty pathDataProp = serializedObject.FindProperty("pathData");
        
        // 현재 연결 상태 표시
        if (pathDataProp != null && pathDataProp.objectReferenceValue != null)
        {
            EditorGUILayout.HelpBox($"✓ ScriptableObject 연결됨: {pathDataProp.objectReferenceValue.name}\n✓ Update 버튼 클릭 시 동기화됩니다.\n✓ 런타임에서 최적화된 성능으로 사용됩니다.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("⚠ ScriptableObject가 필요합니다!\n\n에디터에서 편집한 데이터는 ScriptableObject에 저장되며,\n런타임에서는 ScriptableObject만 사용하여 최적화된 성능을 제공합니다.", MessageType.Warning);
        }
        
        EditorGUILayout.BeginHorizontal();
        bool initClicked = GUILayout.Button("Init", GUILayout.Height(30));
        bool updateClicked = GUILayout.Button("Update", GUILayout.Height(30));
        bool exportClicked = GUILayout.Button("Export", GUILayout.Height(30));
        EditorGUILayout.EndHorizontal();
        
        // 버튼 클릭은 레이아웃이 끝난 후에 처리
        if (initClicked)
        {
            InitFromScriptableObject();
        }
        if (updateClicked)
        {
            CreateOrUpdateScriptableObject();
        }
        if (exportClicked)
        {
            ExportToScriptableObject();
        }

        // 선택된 정점의 위치 편집 UI
        DrawSelectedPointEditor(pointsProp);

        GUILayout.Space(10);
        
        if (GUILayout.Button("Add Point", GUILayout.Height(30)))
        {
            serializedObject.Update();
            
            // pointsProp는 이미 위에서 선언되었으므로 재사용
            if (pointsProp == null)
            {
                pointsProp = serializedObject.FindProperty("points");
            }
            
            if (pointsProp == null || pointsProp.arraySize == 0)
            {
                path.Initialize();
            }
            else
            {
                // 마지막 포인트의 위치 가져오기
                SerializedProperty lastPoint = pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 1);
                SerializedProperty lastPos = lastPoint.FindPropertyRelative("position");
                Vector3 lastPosition = lastPos.vector3Value;
                
                // 새 포인트 추가
                pointsProp.arraySize++;
                SerializedProperty newPoint = pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 1);
                Vector3 newPos = lastPosition + Vector3.right * 2 + Vector3.up * 1;
                
                newPoint.FindPropertyRelative("position").vector3Value = newPos;
                newPoint.FindPropertyRelative("handleIn").vector3Value = newPos + Vector3.left;
                newPoint.FindPropertyRelative("handleOut").vector3Value = newPos + Vector3.right;
                newPoint.FindPropertyRelative("isBroken").boolValue = false;
                
                // 새로 추가된 정점 선택
                selectedPointIndex = pointsProp.arraySize - 1;
            }
            
            SaveChanges();
            Repaint();
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Reset Path", GUILayout.Height(20)))
        {
            serializedObject.Update();
            path.Initialize();
            SaveChanges("Reset Path");
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        DrawPresetShapes(pointsProp);
        
        GUILayout.Space(10);
        DrawArraySection(pointsProp);
        
        serializedObject.ApplyModifiedProperties();
    }

    #region Helper Methods
    
    /// <summary>
    /// 변경사항을 저장하는 헬퍼 메서드
    /// </summary>
    private void SaveChanges(string undoName = null)
    {
        if (!string.IsNullOrEmpty(undoName))
        {
            Undo.RecordObject(path, undoName);
        }
        serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
    }
    

    /// <summary>
    /// 정점 인덱스가 유효한지 확인
    /// </summary>
    private bool IsValidPointIndex(int index, SerializedProperty pointsProp)
    {
        return pointsProp != null && index >= 0 && index < pointsProp.arraySize;
    }

    /// <summary>
    /// 정점과 핸들을 함께 이동
    /// </summary>
    private void MovePointWithHandles(SerializedProperty pointProp, Vector3 newPosition, Vector3 delta)
    {
        pointProp.FindPropertyRelative("position").vector3Value = newPosition;
        
        Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
        Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
        pointProp.FindPropertyRelative("handleIn").vector3Value = handleIn + delta;
        pointProp.FindPropertyRelative("handleOut").vector3Value = handleOut + delta;
    }

    /// <summary>
    /// Shift 키 축 고정 적용
    /// </summary>
    private Vector3 ApplyAxisLock(Vector3 newPos, Vector3 startPos, Vector3 delta)
    {
        float absDeltaX = Mathf.Abs(delta.x);
        float absDeltaY = Mathf.Abs(delta.y);
        
        if (absDeltaX > absDeltaY)
        {
            // 수평 이동이 더 크면 Y축 고정
            newPos.y = startPos.y;
        }
        else
        {
            // 수직 이동이 더 크면 X축 고정
            newPos.x = startPos.x;
        }
        
        return newPos;
    }

    /// <summary>
    /// GUI 좌표로 변환된 정점 위치 가져오기
    /// </summary>
    private Vector2 GetGUIPosition(Vector3 worldPosition)
    {
        return HandleUtility.WorldToGUIPoint(worldPosition);
    }

    /// <summary>
    /// 마우스가 정점/핸들 근처에 있는지 확인
    /// </summary>
    private bool IsNearPointOrHandle(Vector2 mousePos, Vector2 guiPos, Vector2 guiHandleIn, Vector2 guiHandleOut)
    {
        return Vector2.Distance(mousePos, guiPos) < CLICK_RADIUS ||
               Vector2.Distance(mousePos, guiHandleIn) < CLICK_RADIUS ||
               Vector2.Distance(mousePos, guiHandleOut) < CLICK_RADIUS;
    }
    #endregion

    /// <summary>
    /// 선택된 정점의 위치를 편집할 수 있는 UI를 표시합니다.
    /// </summary>
    private void DrawSelectedPointEditor(SerializedProperty pointsProp)
    {
        if (pointsProp == null || pointsProp.arraySize == 0)
        {
            selectedPointIndex = -1;
            return;
        }

        // 선택된 정점 인덱스 유효성 검사
        if (selectedPointIndex < 0 || selectedPointIndex >= pointsProp.arraySize)
        {
            selectedPointIndex = -1;
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Selected Point Editor", EditorStyles.boldLabel);

        if (selectedPointIndex >= 0)
        {
            SerializedProperty selectedPointProp = pointsProp.GetArrayElementAtIndex(selectedPointIndex);
            SerializedProperty positionProp = selectedPointProp.FindPropertyRelative("position");

            EditorGUILayout.LabelField($"Point Index: {selectedPointIndex}", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = EditorGUILayout.Vector3Field("Position", positionProp.vector3Value);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(path, "Edit Point Position");
                serializedObject.Update();
                
                Vector3 oldPosition = positionProp.vector3Value;
                Vector3 delta = newPosition - oldPosition;
                
                // 위치 업데이트
                positionProp.vector3Value = newPosition;
                
                // 핸들도 함께 이동 (정점과 함께 이동)
                SerializedProperty handleInProp = selectedPointProp.FindPropertyRelative("handleIn");
                SerializedProperty handleOutProp = selectedPointProp.FindPropertyRelative("handleOut");
                
                handleInProp.vector3Value = handleInProp.vector3Value + delta;
                handleOutProp.vector3Value = handleOutProp.vector3Value + delta;
                
                SaveChanges("Edit Point Position");
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            // 우측 정렬을 위한 빈 공간
            GUILayout.FlexibleSpace();
            // Previous 버튼: 순환 구조 (0 -> 마지막 인덱스)
            if (GUILayout.Button("◀ Previous", GUILayout.Width(100)))
            {
                selectedPointIndex--;
                if (selectedPointIndex < 0)
                {
                    selectedPointIndex = pointsProp.arraySize - 1; // 마지막 인덱스로 순환
                }
                // 현재 선택된 정점만 표시하도록 다중 선택 초기화
                selectedPointIndices.Clear();
                selectedPointIndices.Add(selectedPointIndex);
                Repaint();
                SceneView.RepaintAll();
            }
            // Next 버튼: 순환 구조 (마지막 인덱스 -> 0)
            if (GUILayout.Button("Next ▶", GUILayout.Width(100)))
            {
                selectedPointIndex++;
                if (selectedPointIndex >= pointsProp.arraySize)
                {
                    selectedPointIndex = 0; // 첫 번째 인덱스로 순환
                }
                // 현재 선택된 정점만 표시하도록 다중 선택 초기화
                selectedPointIndices.Clear();
                selectedPointIndices.Add(selectedPointIndex);
                Repaint();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("정점을 클릭하여 선택하세요. (Enter 키로 편집 모드 전환)", MessageType.Info);
        }
    }
    
    private void DrawPresetShapes(SerializedProperty pointsProp)
    {
        EditorGUILayout.LabelField("Shape Presets", EditorStyles.boldLabel);
        
        // Is Handler 체크박스
        _useHandles = EditorGUILayout.Toggle("Is Handler", _useHandles);
        
        GUILayout.Space(5);
        
        // 다각형 프리셋 (3~8각형)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Polygon (3-8 sides):", GUILayout.Width(150));
        _polygonSides = EditorGUILayout.IntField(_polygonSides, GUILayout.Width(50));
        _polygonSides = Mathf.Clamp(_polygonSides, 3, 8);
        if (GUILayout.Button("생성", GUILayout.Width(60)))
        {
            CreatePolygon(pointsProp, _polygonSides, _useHandles);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 원, 별, 나선 프리셋을 한 줄에 배치
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Circle"))
        {
            CreateCircle(pointsProp, _useHandles);
        }
        if (GUILayout.Button("Star"))
        {
            CreateStar(pointsProp, _useHandles);
        }
        if (GUILayout.Button("Spiral"))
        {
            CreateSpiral(pointsProp, _useHandles);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void CreatePolygon(SerializedProperty pointsProp, int sides, bool useHandles)
    {
        serializedObject.Update();

        pointsProp.arraySize = 0;

        float radius = DEFAULT_RADIUS;
        float angleStep = 360f / sides;

        for (int i = 0; i < sides; i++)
        {
            float angle = (i * angleStep - 90f) * Mathf.Deg2Rad; // -90도로 시작하여 위쪽부터 시작
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            pointsProp.arraySize++;
            SerializedProperty point = pointsProp.GetArrayElementAtIndex(i);
            
            point.FindPropertyRelative("position").vector3Value = position;
            
            if (useHandles)
            {
                // 이전 정점과 다음 정점 사이를 보간하는 형태로 핸들 설정
                int prevIndex = (i - 1 + sides) % sides; // Loop 처리
                int nextIndex = (i + 1) % sides; // Loop 처리
                
                float prevAngle = (prevIndex * angleStep - 90f) * Mathf.Deg2Rad;
                float nextAngle = (nextIndex * angleStep - 90f) * Mathf.Deg2Rad;
                
                Vector3 prevPos = new Vector3(Mathf.Cos(prevAngle) * radius, Mathf.Sin(prevAngle) * radius, 0f);
                Vector3 nextPos = new Vector3(Mathf.Cos(nextAngle) * radius, Mathf.Sin(nextAngle) * radius, 0f);
                
                // 이전 정점과 다음 정점 사이의 방향
                Vector3 direction = (nextPos - prevPos).normalized;
                
                // 핸들 길이는 이전-현재 또는 현재-다음 거리의 평균
                float distToPrev = Vector3.Distance(position, prevPos);
                float distToNext = Vector3.Distance(position, nextPos);
                float handleLength = (distToPrev + distToNext) * HANDLE_LENGTH_FACTOR;
                
                point.FindPropertyRelative("handleOut").vector3Value = position + direction * handleLength;
                point.FindPropertyRelative("handleIn").vector3Value = position - direction * handleLength;
            }
            else
            {
                // Linear: 핸들을 정점 위치와 동일하게 설정 (직선)
                point.FindPropertyRelative("handleOut").vector3Value = position;
                point.FindPropertyRelative("handleIn").vector3Value = position;
            }
            point.FindPropertyRelative("isBroken").boolValue = false;
        }

        path.IsLoop = true;
        SaveChanges($"Create {sides}-sided Polygon");
    }

    private void CreateCircle(SerializedProperty pointsProp, bool useHandles)
    {
        serializedObject.Update();

        pointsProp.arraySize = 0;

        float radius = DEFAULT_RADIUS;
        int pointCount = 4; // 4개의 정점

        for (int i = 0; i < pointCount; i++)
        {
            float angle = (i * 90f - 90f) * Mathf.Deg2Rad; // 0, 90, 180, 270도
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            pointsProp.arraySize++;
            SerializedProperty point = pointsProp.GetArrayElementAtIndex(i);
            
            point.FindPropertyRelative("position").vector3Value = position;
            
            if (useHandles)
            {
                // 원의 접선 방향으로 핸들 설정 (완벽한 원을 위한 표준 방법)
                // 접선 벡터: (-sin(angle), cos(angle), 0)
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                
                // 베지어 곡선이 원에 가깝게 만들기 위한 핸들 길이
                // 공식: 4/3 * tan(π/2n) * radius, 여기서 n은 정점 개수
                // 4개 정점의 경우: 4/3 * tan(π/8) * radius ≈ 0.552 * radius
                float handleLength = radius * CIRCLE_HANDLE_FACTOR;
                
                point.FindPropertyRelative("handleOut").vector3Value = position + tangent * handleLength;
                point.FindPropertyRelative("handleIn").vector3Value = position - tangent * handleLength;
            }
            else
            {
                // Linear: 핸들을 정점 위치와 동일하게 설정 (직선)
                point.FindPropertyRelative("handleOut").vector3Value = position;
                point.FindPropertyRelative("handleIn").vector3Value = position;
            }
            point.FindPropertyRelative("isBroken").boolValue = false;
        }

        path.IsLoop = true;
        SaveChanges("Create Circle");
    }

    private void CreateStar(SerializedProperty pointsProp, bool useHandles)
    {
        serializedObject.Update();

        pointsProp.arraySize = 0;

        float outerRadius = 5f;
        float innerRadius = 2.5f;
        int points = 5; // 5개의 외부 정점

        for (int i = 0; i < points * 2; i++)
        {
            float angle = (i * 36f - 90f) * Mathf.Deg2Rad; // 36도 간격 (180/5)
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            pointsProp.arraySize++;
            SerializedProperty point = pointsProp.GetArrayElementAtIndex(i);
            
            point.FindPropertyRelative("position").vector3Value = position;
            
            if (useHandles)
            {
                // 이전 정점과 다음 정점 사이를 보간하는 형태로 핸들 설정
                int totalPoints = points * 2;
                int prevIndex = (i - 1 + totalPoints) % totalPoints; // Loop 처리
                int nextIndex = (i + 1) % totalPoints; // Loop 처리
                
                float prevAngle = (prevIndex * 36f - 90f) * Mathf.Deg2Rad;
                float nextAngle = (nextIndex * 36f - 90f) * Mathf.Deg2Rad;
                float prevRadius = (prevIndex % 2 == 0) ? outerRadius : innerRadius;
                float nextRadius = (nextIndex % 2 == 0) ? outerRadius : innerRadius;
                
                Vector3 prevPos = new Vector3(Mathf.Cos(prevAngle) * prevRadius, Mathf.Sin(prevAngle) * prevRadius, 0f);
                Vector3 nextPos = new Vector3(Mathf.Cos(nextAngle) * nextRadius, Mathf.Sin(nextAngle) * nextRadius, 0f);
                
                // 이전 정점과 다음 정점 사이의 방향
                Vector3 direction = (nextPos - prevPos).normalized;
                
                // 핸들 길이는 이전-현재 또는 현재-다음 거리의 평균
                float distToPrev = Vector3.Distance(position, prevPos);
                float distToNext = Vector3.Distance(position, nextPos);
                float handleLength = (distToPrev + distToNext) * HANDLE_LENGTH_FACTOR;
                
                point.FindPropertyRelative("handleOut").vector3Value = position + direction * handleLength;
                point.FindPropertyRelative("handleIn").vector3Value = position - direction * handleLength;
            }
            else
            {
                // Linear: 핸들을 정점 위치와 동일하게 설정 (직선)
                point.FindPropertyRelative("handleOut").vector3Value = position;
                point.FindPropertyRelative("handleIn").vector3Value = position;
            }
            point.FindPropertyRelative("isBroken").boolValue = false;
        }

        path.IsLoop = true;
        SaveChanges("Create Star");
    }

    private void CreateSpiral(SerializedProperty pointsProp, bool useHandles)
    {
        serializedObject.Update();

        pointsProp.arraySize = 0;

        int pointCount = 10; // 나선 포인트 개수
        float startRadius = 1f; // 시작 반지름
        float endRadius = 8f; // 끝 반지름
        float totalTurns = 3f; // 총 회전 수
        float startAngle = -90f * Mathf.Deg2Rad; // 시작 각도 (위쪽부터)

        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1); // 0 ~ 1
            float angle = startAngle + t * totalTurns * 2f * Mathf.PI; // 각도 증가
            float radius = Mathf.Lerp(startRadius, endRadius, t); // 반지름 증가
            
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            pointsProp.arraySize++;
            SerializedProperty point = pointsProp.GetArrayElementAtIndex(i);
            
            point.FindPropertyRelative("position").vector3Value = position;
            
            if (useHandles)
            {
                // 이전 정점과 다음 정점 사이를 보간하는 형태로 핸들 설정
                Vector3 prevPos, nextPos;
                
                if (i == 0)
                {
                    // 첫 번째 포인트: 이전 포인트는 없으므로 현재와 다음만 사용
                    float nextT = (float)(i + 1) / (pointCount - 1);
                    float nextAngle = startAngle + nextT * totalTurns * 2f * Mathf.PI;
                    float nextRadius = Mathf.Lerp(startRadius, endRadius, nextT);
                    nextPos = new Vector3(Mathf.Cos(nextAngle) * nextRadius, Mathf.Sin(nextAngle) * nextRadius, 0f);
                    prevPos = position; // 가상의 이전 포인트
                }
                else if (i == pointCount - 1)
                {
                    // 마지막 포인트: 다음 포인트는 없으므로 이전과 현재만 사용
                    float prevT = (float)(i - 1) / (pointCount - 1);
                    float prevAngle = startAngle + prevT * totalTurns * 2f * Mathf.PI;
                    float prevRadius = Mathf.Lerp(startRadius, endRadius, prevT);
                    prevPos = new Vector3(Mathf.Cos(prevAngle) * prevRadius, Mathf.Sin(prevAngle) * prevRadius, 0f);
                    nextPos = position; // 가상의 다음 포인트
                }
                else
                {
                    // 중간 포인트: 이전과 다음 모두 사용
                    float prevT = (float)(i - 1) / (pointCount - 1);
                    float nextT = (float)(i + 1) / (pointCount - 1);
                    
                    float prevAngle = startAngle + prevT * totalTurns * 2f * Mathf.PI;
                    float nextAngle = startAngle + nextT * totalTurns * 2f * Mathf.PI;
                    float prevRadius = Mathf.Lerp(startRadius, endRadius, prevT);
                    float nextRadius = Mathf.Lerp(startRadius, endRadius, nextT);
                    
                    prevPos = new Vector3(Mathf.Cos(prevAngle) * prevRadius, Mathf.Sin(prevAngle) * prevRadius, 0f);
                    nextPos = new Vector3(Mathf.Cos(nextAngle) * nextRadius, Mathf.Sin(nextAngle) * nextRadius, 0f);
                }
                
                // 이전 정점과 다음 정점 사이의 방향
                Vector3 direction = (nextPos - prevPos).normalized;
                
                // 핸들 길이는 이전-현재 또는 현재-다음 거리의 평균
                float distToPrev = Vector3.Distance(position, prevPos);
                float distToNext = Vector3.Distance(position, nextPos);
                float handleLength = (distToPrev + distToNext) * HANDLE_LENGTH_FACTOR;
                
                point.FindPropertyRelative("handleOut").vector3Value = position + direction * handleLength;
                point.FindPropertyRelative("handleIn").vector3Value = position - direction * handleLength;
            }
            else
            {
                // Linear: 핸들을 정점 위치와 동일하게 설정 (직선)
                point.FindPropertyRelative("handleOut").vector3Value = position;
                point.FindPropertyRelative("handleIn").vector3Value = position;
            }
            point.FindPropertyRelative("isBroken").boolValue = false;
        }

        path.IsLoop = false; // 나선은 열린 경로
        SaveChanges("Create Spiral");
    }

    private void OnSceneGUI()
    {
        path = (BezierPath)target;
        if (path == null) return;
        
        // 최신 데이터 가져오기
        serializedObject.Update();
        
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        if (pointsProp == null || pointsProp.arraySize == 0) return;
        
        handleTransform = path.transform;
        handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;

        // 드래그 종료 감지 (전역)
        if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
        {
            bool wasDragging = isDragging;
            DraggingElementType wasDraggingType = draggingElementType;
            
            isDragging = false;
            draggingElementType = DraggingElementType.None;
            
            // 다중 선택 정점 이동 초기화
            selectedPointsStartPositions.Clear();
            
            // 드래그가 끝났을 때만 SaveChanges 호출 (성능 최적화)
            if (wasDragging)
            {
                string undoName = wasDraggingType == DraggingElementType.Point ? 
                    (selectedPointIndices.Count > 1 ? "Move Points" : "Move Point") :
                                 wasDraggingType == DraggingElementType.HandleIn ? "Move Handle In" :
                                 wasDraggingType == DraggingElementType.HandleOut ? "Move Handle Out" : null;
                if (!string.IsNullOrEmpty(undoName))
                {
                    SaveChanges(undoName);
                }
                Repaint();
            }
            
            // 박스 선택 종료
            if (isBoxSelecting)
            {
                isBoxSelecting = false;
                HandleBoxSelection(pointsProp);
                Event.current.Use();
            }
        }

        // Enter 키 전역 처리 (편집 모드 전환)
        if (Event.current.type == EventType.KeyDown)
        {
            if ((Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && selectedPointIndex >= 0)
            {
                isEditingPosition = !isEditingPosition;
                Event.current.Use();
                Repaint();
            }
        }

        // Alt + 왼쪽 클릭으로 커브에 정점 추가 (박스 선택보다 먼저 처리)
        HandleCurveClick(pointsProp);

        // 박스 선택 처리
        HandleBoxSelectionInput(pointsProp);

        // 여러 정점 선택 시 우클릭 메뉴 처리
        HandleMultiSelectionContextMenu(pointsProp);

        // 1. 정점 및 핸들 그리기 및 조작
        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            ShowPoint(i, pointsProp);
        }

        // 2. 곡선 그리기 (Bezier Line)
        Handles.color = Color.white;
        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            if (!path.IsLoop && i == pointsProp.arraySize - 1) break;

            SerializedProperty p0Prop = pointsProp.GetArrayElementAtIndex(i);
            SerializedProperty p1Prop = (path.IsLoop && i == pointsProp.arraySize - 1) 
                ? pointsProp.GetArrayElementAtIndex(0) 
                : pointsProp.GetArrayElementAtIndex(i + 1);
            
            BezierPoint p0 = GetBezierPointFromProperty(p0Prop);
            BezierPoint p1 = GetBezierPointFromProperty(p1Prop);

            Vector3 p0Pos = handleTransform.TransformPoint(p0.position);
            Vector3 p0Out = handleTransform.TransformPoint(p0.handleOut);
            Vector3 p1In = handleTransform.TransformPoint(p1.handleIn);
            Vector3 p1Pos = handleTransform.TransformPoint(p1.position);

            Handles.DrawBezier(p0Pos, p1Pos, p0Out, p1In, Color.white, null, CURVE_LINE_WIDTH);
        }
    }

    private BezierPoint GetBezierPointFromProperty(SerializedProperty prop)
    {
        return new BezierPoint(prop.FindPropertyRelative("position").vector3Value)
        {
            handleIn = prop.FindPropertyRelative("handleIn").vector3Value,
            handleOut = prop.FindPropertyRelative("handleOut").vector3Value,
            isBroken = prop.FindPropertyRelative("isBroken").boolValue
        };
    }

    #region Scene GUI - Point Rendering and Interaction
    
    private void ShowPoint(int index, SerializedProperty pointsProp)
    {
        SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(index);
        
        Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
        Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
        Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
        bool isBroken = pointProp.FindPropertyRelative("isBroken").boolValue;

        // 좌표 변환 (Local -> World)
        Vector3 worldPoint = handleTransform.TransformPoint(position);
        Vector3 worldHandleIn = handleTransform.TransformPoint(handleIn);
        Vector3 worldHandleOut = handleTransform.TransformPoint(handleOut);

        // 핸들 그리기
        DrawHandles(worldPoint, worldHandleIn, worldHandleOut);

        // 핸들 제어 (정점보다 먼저 처리하여 우선순위 확보)
        HandleHandleDrag(pointProp, index, position, handleIn, handleOut, worldHandleIn, 
                        DraggingElementType.HandleIn, isBroken, ref isBroken);
        HandleHandleDrag(pointProp, index, position, handleIn, handleOut, worldHandleOut, 
                        DraggingElementType.HandleOut, isBroken, ref isBroken);

        // 정점 제어
        DrawSelectedPointHighlight(worldPoint, index);
        HandlePointDrag(pointProp, index, position, handleIn, handleOut, worldPoint, 
                       worldHandleIn, worldHandleOut, isBroken);
        HandlePointContextMenu(index, pointProp, position, handleIn, handleOut, worldPoint, isBroken);
    }

    /// <summary>
    /// 핸들 그리기
    /// </summary>
    private void DrawHandles(Vector3 point, Vector3 handleIn, Vector3 handleOut)
    {
        Handles.color = Color.grey;
        Handles.DrawLine(point, handleIn);
        Handles.DrawLine(point, handleOut);
    }

    /// <summary>
    /// 선택된 정점 강조 표시
    /// </summary>
    private void DrawSelectedPointHighlight(Vector3 worldPoint, int index)
    {
        // 드래그 중일 때는 하이라이트 표시하지 않음 (성능 및 시각적 혼란 방지)
        if (isDragging) return;
        
        if (selectedPointIndex == index || selectedPointIndices.Contains(index))
        {
            Handles.color = Color.green;
            Handles.DrawWireDisc(worldPoint, handleTransform.forward, SELECTED_POINT_RADIUS);
            Handles.color = Color.white;
        }
    }

    /// <summary>
    /// 핸들 드래그 처리 (공통 로직)
    /// </summary>
    private void HandleHandleDrag(SerializedProperty pointProp, int index, Vector3 position, 
                                  Vector3 handleIn, Vector3 handleOut, Vector3 worldHandle,
                                  DraggingElementType elementType, bool isBroken, ref bool isBrokenRef)
    {
        bool isAltPressedBefore = Event.current.alt;
        
        // 드래그 시작 감지
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Vector2 guiHandle = GetGUIPosition(worldHandle);
            Vector2 mousePos = Event.current.mousePosition;
            if (Vector2.Distance(mousePos, guiHandle) < CLICK_RADIUS)
            {
                isDragging = true;
                draggingElementType = elementType;
                if (elementType == DraggingElementType.HandleIn)
                    dragStartHandleIn = handleIn;
                else
                    dragStartHandleOut = handleOut;
            }
        }
        
        EditorGUI.BeginChangeCheck();
        worldHandle = Handles.FreeMoveHandle(worldHandle, HANDLE_SIZE, Vector3.zero, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.Update();
            Vector3 newHandle = handleTransform.InverseTransformPoint(worldHandle);
            Vector3 startHandle = (elementType == DraggingElementType.HandleIn) ? dragStartHandleIn : dragStartHandleOut;
            
            // Shift 키가 눌려있으면 축 고정
            if (Event.current.shift && isDragging && draggingElementType == elementType)
            {
                Vector3 delta = newHandle - startHandle;
                newHandle = ApplyAxisLock(newHandle, startHandle, delta);
            }
            
            // 핸들 업데이트
            string handleProperty = (elementType == DraggingElementType.HandleIn) ? "handleIn" : "handleOut";
            pointProp.FindPropertyRelative(handleProperty).vector3Value = newHandle;
            
            // Alt 키를 누른 상태에서 핸들을 이동하면 자동으로 Break 활성화
            bool isAltPressed = Event.current.alt || isAltPressedBefore;
            if (isAltPressed && !isBroken)
            {
                pointProp.FindPropertyRelative("isBroken").boolValue = true;
                isBrokenRef = true;
            }
            
            // Break가 아닐 경우, 반대쪽 핸들을 맞은편으로 자동 이동 (미러링)
            if (!isBroken && !isAltPressed)
            {
                Vector3 localP = position;
                Vector3 dir = localP - newHandle;
                string oppositeHandle = (elementType == DraggingElementType.HandleIn) ? "handleOut" : "handleIn";
                pointProp.FindPropertyRelative(oppositeHandle).vector3Value = localP + dir;
            }
            
            // 드래그 중에는 SaveChanges 호출하지 않음 (성능 최적화)
            // 드래그 종료 시 OnSceneGUI의 MouseUp에서 처리
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }
    }

    /// <summary>
    /// 정점 드래그 처리
    /// </summary>
    private void HandlePointDrag(SerializedProperty pointProp, int index, Vector3 position, 
                                 Vector3 handleIn, Vector3 handleOut, Vector3 worldPoint,
                                 Vector3 worldHandleIn, Vector3 worldHandleOut, bool isBroken)
    {
        // 드래그 시작 감지 (정점) - Handles.FreeMoveHandle 전에 처리해야 함
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Vector2 guiPos = GetGUIPosition(worldPoint);
            Vector2 mousePos = Event.current.mousePosition;
            
            Vector2 guiHandleIn = GetGUIPosition(worldHandleIn);
            Vector2 guiHandleOut = GetGUIPosition(worldHandleOut);
            bool isNearHandle = IsNearPointOrHandle(mousePos, guiPos, guiHandleIn, guiHandleOut);
            
            if (Vector2.Distance(mousePos, guiPos) < CLICK_RADIUS && !isNearHandle)
            {
                // Alt 키가 눌려있으면 정점 추가 기능을 위해 이벤트를 사용하지 않음
                if (!Event.current.alt)
                {
                    // 선택된 정점이 없거나, 클릭한 정점이 선택되지 않은 경우 단일 선택
                    if (selectedPointIndices.Count == 0 || !selectedPointIndices.Contains(index))
                    {
                        selectedPointIndex = index;
                        selectedPointIndices.Clear();
                        selectedPointIndices.Add(index);
                    }
                    // 클릭한 정점이 이미 선택되어 있으면 그대로 유지 (다중 선택 유지)
                    
                    isEditingPosition = false;
                    isDragging = true;
                    draggingElementType = DraggingElementType.Point;
                    dragStartPosition = position;
                    dragStartHandleIn = handleIn;
                    dragStartHandleOut = handleOut;
                    
                    // 다중 선택된 모든 정점의 초기 위치 저장 (드래그 시작 시점의 위치)
                    selectedPointsStartPositions.Clear();
                    SerializedProperty pointsProp = serializedObject.FindProperty("points");
                    if (pointsProp != null && selectedPointIndices.Count > 0)
                    {
                        foreach (int selectedIdx in selectedPointIndices)
                        {
                            if (selectedIdx >= 0 && selectedIdx < pointsProp.arraySize)
                            {
                                SerializedProperty selectedPointProp = pointsProp.GetArrayElementAtIndex(selectedIdx);
                                Vector3 startPos = selectedPointProp.FindPropertyRelative("position").vector3Value;
                                selectedPointsStartPositions[selectedIdx] = startPos;
                            }
                        }
                    }
                    // 드래그 시작 시에는 Repaint 불필요 (SceneView만 업데이트)
                }
            }
        }
        
        // Handles.FreeMoveHandle 전에 드래그 상태 확인 및 초기 위치 저장
        bool wasDraggingBefore = isDragging;
        
        // 다중 선택된 경우, MouseDown 이벤트에서 초기 위치 미리 저장 (Handles가 이벤트를 소비하기 전에)
        if (!wasDraggingBefore && selectedPointIndices.Count > 1 && selectedPointsStartPositions.Count == 0 && 
            Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Vector2 guiPos = GetGUIPosition(worldPoint);
            Vector2 mousePos = Event.current.mousePosition;
            if (Vector2.Distance(mousePos, guiPos) < CLICK_RADIUS)
            {
                selectedPointsStartPositions.Clear();
                SerializedProperty pointsProp = serializedObject.FindProperty("points");
                if (pointsProp != null)
                {
                    serializedObject.Update(); // 최신 데이터 가져오기
                    foreach (int selectedIdx in selectedPointIndices)
                    {
                        if (selectedIdx >= 0 && selectedIdx < pointsProp.arraySize)
                        {
                            SerializedProperty selectedPointProp = pointsProp.GetArrayElementAtIndex(selectedIdx);
                            Vector3 startPos = selectedPointProp.FindPropertyRelative("position").vector3Value;
                            selectedPointsStartPositions[selectedIdx] = startPos;
                        }
                    }
                }
            }
        }
        
        EditorGUI.BeginChangeCheck();
        worldPoint = Handles.FreeMoveHandle(worldPoint, POINT_HANDLE_SIZE, Vector3.zero, Handles.DotHandleCap);
        bool handleChanged = EditorGUI.EndChangeCheck();
        
        // Handles가 드래그를 시작했지만 아직 초기 위치가 저장되지 않은 경우 (백업)
        if (handleChanged && !wasDraggingBefore && selectedPointIndices.Count > 1 && selectedPointsStartPositions.Count == 0)
        {
            selectedPointsStartPositions.Clear();
            SerializedProperty pointsProp = serializedObject.FindProperty("points");
            if (pointsProp != null)
            {
                serializedObject.Update();
                foreach (int selectedIdx in selectedPointIndices)
                {
                    if (selectedIdx >= 0 && selectedIdx < pointsProp.arraySize)
                    {
                        SerializedProperty selectedPointProp = pointsProp.GetArrayElementAtIndex(selectedIdx);
                        Vector3 startPos = selectedPointProp.FindPropertyRelative("position").vector3Value;
                        selectedPointsStartPositions[selectedIdx] = startPos;
                    }
                }
            }
        }
        
        if (handleChanged)
        {
            serializedObject.Update();
            
            Vector3 newPos = handleTransform.InverseTransformPoint(worldPoint);
            
            // 드래그 중인 정점의 초기 위치 가져오기
            Vector3 dragStartPos = position; // 기본값: 현재 위치
            if (selectedPointsStartPositions.TryGetValue(index, out Vector3 savedStartPos))
            {
                dragStartPos = savedStartPos;
            }
            
            // 드래그 중인 정점이 이동한 offset 계산
            Vector3 delta = newPos - dragStartPos;
            
            // Shift 키가 눌려있으면 축 고정
            if (Event.current.shift && isDragging && draggingElementType == DraggingElementType.Point)
            {
                Vector3 shiftDelta = newPos - dragStartPos;
                newPos = ApplyAxisLock(newPos, dragStartPos, shiftDelta);
                delta = newPos - dragStartPos; // 축 고정 후 델타 재계산
            }
            
            // 다중 선택된 정점들을 동시에 이동
            SerializedProperty pointsProp = serializedObject.FindProperty("points");
            
            if (pointsProp != null && selectedPointIndices.Count > 1 && selectedPointsStartPositions.Count > 0)
            {
                // 모든 선택된 정점에 동일한 offset 적용
                foreach (int selectedIdx in selectedPointIndices)
                {
                    if (selectedIdx >= 0 && selectedIdx < pointsProp.arraySize)
                    {
                        SerializedProperty selectedPointProp = pointsProp.GetArrayElementAtIndex(selectedIdx);
                        
                        // 초기 위치에서 델타만큼 이동
                        if (selectedPointsStartPositions.TryGetValue(selectedIdx, out Vector3 startPos))
                        {
                            Vector3 targetPos = startPos + delta;
                            
                            // Shift 키가 눌려있으면 축 고정 적용
                            if (Event.current.shift)
                            {
                                Vector3 shiftDelta = targetPos - startPos;
                                targetPos = ApplyAxisLock(targetPos, startPos, shiftDelta);
                            }
                            
                            Vector3 currentPos = selectedPointProp.FindPropertyRelative("position").vector3Value;
                            Vector3 pointDelta = targetPos - currentPos;
                            MovePointWithHandles(selectedPointProp, targetPos, pointDelta);
                        }
                    }
                }
            }
            else
            {
                // 단일 정점 이동
                MovePointWithHandles(pointProp, newPos, delta);
            }
            
            // 이동 시에도 선택 상태 유지
            selectedPointIndex = index;
            
            // 드래그 중에는 SaveChanges 호출하지 않음 (성능 최적화)
            // 드래그 종료 시 OnSceneGUI의 MouseUp에서 처리
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        // Enter 키 입력 처리 (편집 모드 전환)
        if (Event.current.type == EventType.KeyDown)
        {
            if ((Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) 
                && selectedPointIndex == index)
            {
                isEditingPosition = !isEditingPosition;
                Event.current.Use();
                Repaint();
            }
        }
    }

    /// <summary>
    /// 정점 우클릭 컨텍스트 메뉴 처리
    /// </summary>
    private void HandlePointContextMenu(int index, SerializedProperty pointProp, Vector3 position,
                                       Vector3 handleIn, Vector3 handleOut, Vector3 worldPoint, bool isBroken)
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            Vector2 guiPos = GetGUIPosition(worldPoint);
            Vector2 mousePos = Event.current.mousePosition;
            
            if (Vector2.Distance(mousePos, guiPos) < CLICK_RADIUS)
            {
                Event.current.Use();
                ShowPointContextMenu(index, pointProp, position, handleIn, handleOut, isBroken);
            }
        }
    }
    #endregion

    private void ShowPointContextMenu(int pointIndex, SerializedProperty pointProp, Vector3 position, Vector3 handleIn, Vector3 handleOut, bool isBroken)
    {
        GenericMenu menu = new GenericMenu();
        
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        bool canDelete = pointsProp != null && pointsProp.arraySize > MIN_POINTS_REQUIRED;
        
        if (isBroken)
        {
            menu.AddItem(new GUIContent("Link Handles"), false, () => {
                serializedObject.Update();
                pointProp.FindPropertyRelative("isBroken").boolValue = false;
            
            // Link(연결)할 때 핸들을 직선으로 정렬 (현재 HandleOut 기준으로 정렬)
                Vector3 localP = position;
                Vector3 dir = handleOut - localP;
                pointProp.FindPropertyRelative("handleIn").vector3Value = localP - dir;
                
                SaveChanges();
            });
        }
        else
        {
            menu.AddItem(new GUIContent("Break Handles"), false, () => {
                serializedObject.Update();
                pointProp.FindPropertyRelative("isBroken").boolValue = true;
                SaveChanges("Break Handles");
            });
        }
        
        menu.AddSeparator("");
        
        // 커브의 진행 방향 계산
        Vector3 curveDirection = GetCurveDirection(pointsProp, pointIndex, position);
        
        // X flat: 핸들을 수평으로 조정 (진행 방향에 맞게, 길이 유지)
        menu.AddItem(new GUIContent("X flat"), false, () => {
            ApplyXFlat(pointProp, position, handleIn, handleOut, curveDirection);
        });
        
        // Y flat: 핸들을 수직으로 조정 (진행 방향에 맞게, 길이 유지)
        menu.AddItem(new GUIContent("Y flat"), false, () => {
            ApplyYFlat(pointProp, position, handleIn, handleOut, curveDirection);
        });
        
        menu.AddSeparator("");
        
        if (canDelete)
        {
            menu.AddItem(new GUIContent("Delete Point"), false, () => {
                DeletePoint(pointIndex);
            });
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Delete Point (Minimum 2 points required)"));
        }
        
        menu.ShowAsContext();
    }

    /// <summary>
    /// 정점에서의 커브 진행 방향을 계산합니다.
    /// </summary>
    private Vector3 GetCurveDirection(SerializedProperty pointsProp, int pointIndex, Vector3 currentPosition)
    {
        Vector3 direction = Vector3.zero;
        
        // 이전 정점과 다음 정점 찾기
        Vector3 prevPos = Vector3.zero;
        Vector3 nextPos = Vector3.zero;
        bool hasPrev = false;
        bool hasNext = false;
        
        if (path.IsLoop)
        {
            // Loop인 경우
            int prevIndex = (pointIndex - 1 + pointsProp.arraySize) % pointsProp.arraySize;
            int nextIndex = (pointIndex + 1) % pointsProp.arraySize;
            
            SerializedProperty prevProp = pointsProp.GetArrayElementAtIndex(prevIndex);
            SerializedProperty nextProp = pointsProp.GetArrayElementAtIndex(nextIndex);
            
            prevPos = prevProp.FindPropertyRelative("position").vector3Value;
            nextPos = nextProp.FindPropertyRelative("position").vector3Value;
            hasPrev = true;
            hasNext = true;
        }
        else
        {
            // Loop가 아닌 경우
            if (pointIndex > 0)
            {
                SerializedProperty prevProp = pointsProp.GetArrayElementAtIndex(pointIndex - 1);
                prevPos = prevProp.FindPropertyRelative("position").vector3Value;
                hasPrev = true;
            }
            
            if (pointIndex < pointsProp.arraySize - 1)
            {
                SerializedProperty nextProp = pointsProp.GetArrayElementAtIndex(pointIndex + 1);
                nextPos = nextProp.FindPropertyRelative("position").vector3Value;
                hasNext = true;
            }
        }
        
        // 진행 방향 계산
        if (hasPrev && hasNext)
        {
            // 이전과 다음이 모두 있으면 평균 방향 사용
            Vector3 dirToPrev = (currentPosition - prevPos).normalized;
            Vector3 dirToNext = (nextPos - currentPosition).normalized;
            direction = (dirToPrev + dirToNext).normalized;
        }
        else if (hasNext)
        {
            // 다음만 있으면 다음 방향 사용
            direction = (nextPos - currentPosition).normalized;
        }
        else if (hasPrev)
        {
            // 이전만 있으면 이전 방향 사용 (역방향)
            direction = (currentPosition - prevPos).normalized;
        }
        else
        {
            // 둘 다 없으면 기본 방향 (오른쪽)
            direction = Vector3.right;
        }
        
        return direction;
    }

    /// <summary>
    /// X flat 적용: 핸들을 수평으로 조정 (진행 방향에 맞게)
    /// </summary>
    private void ApplyXFlat(SerializedProperty pointProp, Vector3 position, Vector3 handleIn, Vector3 handleOut, Vector3 curveDirection)
    {
        serializedObject.Update();
        
        Vector3 localP = position;
        
        // 진행 방향이 주로 수평인지 확인 (X 성분이 Y 성분보다 큼)
        bool isHorizontal = Mathf.Abs(curveDirection.x) > Mathf.Abs(curveDirection.y);
        
        // Handle In과 Handle Out의 방향 결정
        Vector3 dirIn = handleIn - localP;
        Vector3 dirOut = handleOut - localP;
        float lengthIn = dirIn.magnitude;
        float lengthOut = dirOut.magnitude;
        
        Vector3 horizontalDir;
        if (isHorizontal)
        {
            // 진행 방향이 수평이면: 진행 방향 사용
            horizontalDir = new Vector3(Mathf.Sign(curveDirection.x), 0f, 0f);
        }
        else
        {
            // 진행 방향이 수직이면: Handle Out의 X 방향 사용 (없으면 Handle In의 X 방향)
            if (Mathf.Abs(dirOut.x) > EPSILON)
            {
                horizontalDir = new Vector3(Mathf.Sign(dirOut.x), 0f, 0f);
            }
            else if (Mathf.Abs(dirIn.x) > EPSILON)
            {
                horizontalDir = new Vector3(Mathf.Sign(dirIn.x), 0f, 0f);
            }
            else
            {
                horizontalDir = new Vector3(Mathf.Sign(curveDirection.x), 0f, 0f);
            }
        }
        
        // Handle In 처리 (정점으로 들어오는 방향이므로 반대 방향)
        if (lengthIn > EPSILON)
        {
            Vector3 newHandleIn = localP - horizontalDir * lengthIn;
            newHandleIn.y = localP.y;
            pointProp.FindPropertyRelative("handleIn").vector3Value = newHandleIn;
        }
        else
        {
            pointProp.FindPropertyRelative("handleIn").vector3Value = localP - horizontalDir;
        }
        
        // Handle Out 처리 (정점에서 나가는 방향)
        if (lengthOut > EPSILON)
        {
            Vector3 newHandleOut = localP + horizontalDir * lengthOut;
            newHandleOut.y = localP.y;
            pointProp.FindPropertyRelative("handleOut").vector3Value = newHandleOut;
        }
        else
        {
            pointProp.FindPropertyRelative("handleOut").vector3Value = localP + horizontalDir;
        }
        
        SaveChanges("X flat Handles");
    }

    /// <summary>
    /// Y flat 적용: 핸들을 수직으로 조정 (진행 방향에 맞게)
    /// </summary>
    private void ApplyYFlat(SerializedProperty pointProp, Vector3 position, Vector3 handleIn, Vector3 handleOut, Vector3 curveDirection)
    {
        serializedObject.Update();
        
        Vector3 localP = position;
        
        // 진행 방향이 주로 수직인지 확인 (Y 성분이 X 성분보다 큼)
        bool isVertical = Mathf.Abs(curveDirection.y) > Mathf.Abs(curveDirection.x);
        
        // Handle In과 Handle Out의 방향 결정
        Vector3 dirIn = handleIn - localP;
        Vector3 dirOut = handleOut - localP;
        float lengthIn = dirIn.magnitude;
        float lengthOut = dirOut.magnitude;
        
        Vector3 verticalDir;
        if (isVertical)
        {
            // 진행 방향이 수직이면: 진행 방향 사용
            verticalDir = new Vector3(0f, Mathf.Sign(curveDirection.y), 0f);
        }
        else
        {
            // 진행 방향이 수평이면: Handle Out의 Y 방향 사용 (없으면 Handle In의 Y 방향)
            if (Mathf.Abs(dirOut.y) > EPSILON)
            {
                verticalDir = new Vector3(0f, Mathf.Sign(dirOut.y), 0f);
            }
            else if (Mathf.Abs(dirIn.y) > EPSILON)
            {
                verticalDir = new Vector3(0f, Mathf.Sign(dirIn.y), 0f);
            }
            else
            {
                verticalDir = new Vector3(0f, Mathf.Sign(curveDirection.y), 0f);
            }
        }
        
        // Handle In 처리 (정점으로 들어오는 방향이므로 반대 방향)
        if (lengthIn > EPSILON)
        {
            Vector3 newHandleIn = localP - verticalDir * lengthIn;
            newHandleIn.x = localP.x;
            pointProp.FindPropertyRelative("handleIn").vector3Value = newHandleIn;
        }
        else
        {
            pointProp.FindPropertyRelative("handleIn").vector3Value = localP - verticalDir;
        }
        
        // Handle Out 처리 (정점에서 나가는 방향)
        if (lengthOut > EPSILON)
        {
            Vector3 newHandleOut = localP + verticalDir * lengthOut;
            newHandleOut.x = localP.x;
            pointProp.FindPropertyRelative("handleOut").vector3Value = newHandleOut;
        }
        else
        {
            pointProp.FindPropertyRelative("handleOut").vector3Value = localP + verticalDir;
        }
        
        SaveChanges("Y flat Handles");
    }

    /// <summary>
    /// Alt + 왼쪽 클릭으로 커브에 정점 추가 처리
    /// </summary>
    private void HandleCurveClick(SerializedProperty pointsProp)
    {
        Event e = Event.current;
        
        // Alt + 왼쪽 클릭 체크
        bool isAltPressed = e.alt;
        bool isLeftClick = e.type == EventType.MouseDown && e.button == 0;
        
        if (!isAltPressed || !isLeftClick) return;
        
        // 핸들이나 정점 근처를 클릭했는지 확인 (더 정교한 클릭 처리)
        if (IsClickNearHandleOrPoint(pointsProp, e.mousePosition))
        {
            return; // 핸들이나 정점 근처를 클릭한 경우 정점 추가하지 않음
        }
        
        // 클릭한 위치를 월드 좌표로 변환
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane plane = new Plane(handleTransform.forward, handleTransform.position);
        
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 worldClickPos = ray.GetPoint(distance);
            Vector3 localClickPos = handleTransform.InverseTransformPoint(worldClickPos);
            
            // 가장 가까운 세그먼트와 t 값 찾기
            if (FindClosestSegmentAndT(pointsProp, localClickPos, out int segmentIndex, out float t))
            {
                // 정점 추가
                AddPointOnCurve(pointsProp, segmentIndex, t);
                e.Use();
            }
        }
    }

    /// <summary>
    /// 클릭한 위치가 핸들이나 정점 근처인지 확인합니다.
    /// </summary>
    private bool IsClickNearHandleOrPoint(SerializedProperty pointsProp, Vector2 mousePosition)
    {
        float clickRadius = CLICK_RADIUS;
        
        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(i);
            Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
            Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
            Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
            
            // 월드 좌표로 변환
            Vector3 worldPos = handleTransform.TransformPoint(position);
            Vector3 worldHandleIn = handleTransform.TransformPoint(handleIn);
            Vector3 worldHandleOut = handleTransform.TransformPoint(handleOut);
            
            // GUI 좌표로 변환
            Vector2 guiPos = HandleUtility.WorldToGUIPoint(worldPos);
            Vector2 guiHandleIn = HandleUtility.WorldToGUIPoint(worldHandleIn);
            Vector2 guiHandleOut = HandleUtility.WorldToGUIPoint(worldHandleOut);
            
            // 정점 근처 체크
            if (Vector2.Distance(mousePosition, guiPos) < clickRadius)
            {
                return true;
            }
            
            // 핸들 근처 체크
            if (Vector2.Distance(mousePosition, guiHandleIn) < clickRadius)
            {
                return true;
            }
            
            if (Vector2.Distance(mousePosition, guiHandleOut) < clickRadius)
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// 클릭한 위치에 가장 가까운 세그먼트와 t 값을 찾습니다.
    /// </summary>
    private bool FindClosestSegmentAndT(SerializedProperty pointsProp, Vector3 clickPos, out int segmentIndex, out float closestT)
    {
        segmentIndex = -1;
        closestT = 0f;
        float minDistance = float.MaxValue;
        
        int numSegments = path.IsLoop ? pointsProp.arraySize : pointsProp.arraySize - 1;
        
        for (int i = 0; i < numSegments; i++)
        {
            SerializedProperty p0Prop = pointsProp.GetArrayElementAtIndex(i);
            SerializedProperty p1Prop = (path.IsLoop && i == pointsProp.arraySize - 1) 
                ? pointsProp.GetArrayElementAtIndex(0) 
                : pointsProp.GetArrayElementAtIndex(i + 1);
            
            BezierPoint p0 = GetBezierPointFromProperty(p0Prop);
            BezierPoint p1 = GetBezierPointFromProperty(p1Prop);
            
            // 베지어 커브에서 가장 가까운 t 값 찾기
            if (FindClosestTOnBezierSegment(p0, p1, clickPos, out float t, out float distance))
            {
                if (distance < minDistance)
                {
                    minDistance = distance;
                    segmentIndex = i;
                    closestT = t;
                }
            }
        }
        
        // 최대 허용 거리 체크 (화면 픽셀 기준 약 20픽셀)
        float maxDistance = HandleUtility.GetHandleSize(handleTransform.TransformPoint(clickPos)) * 0.5f;
        return segmentIndex >= 0 && minDistance < maxDistance;
    }

    /// <summary>
    /// 베지어 세그먼트에서 클릭한 위치에 가장 가까운 t 값을 찾습니다.
    /// </summary>
    private bool FindClosestTOnBezierSegment(BezierPoint p0, BezierPoint p1, Vector3 clickPos, out float closestT, out float minDistance)
    {
        closestT = 0f;
        minDistance = float.MaxValue;
        
        // 샘플링을 통해 가장 가까운 점 찾기 (더 정확한 방법은 Newton-Raphson 등을 사용할 수 있음)
        int samples = 50;
        
        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 pointOnCurve = GetCubicBezierPointLocal(p0, p1, t);
            float distance = Vector3.Distance(clickPos, pointOnCurve);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                closestT = t;
            }
        }
        
        // 더 정밀한 검색 (이분법으로 근사)
        float refineRange = 1f / samples;
        for (int refine = 0; refine < 3; refine++)
        {
            float startT = Mathf.Clamp01(closestT - refineRange);
            float endT = Mathf.Clamp01(closestT + refineRange);
            int refineSamples = 20;
            
            for (int i = 0; i <= refineSamples; i++)
            {
                float t = Mathf.Lerp(startT, endT, (float)i / refineSamples);
                Vector3 pointOnCurve = GetCubicBezierPointLocal(p0, p1, t);
                float distance = Vector3.Distance(clickPos, pointOnCurve);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestT = t;
                }
            }
            
            refineRange *= 0.5f;
        }
        
        return true;
    }

    /// <summary>
    /// 로컬 좌표계에서 베지어 점을 계산합니다.
    /// </summary>
    private Vector3 GetCubicBezierPointLocal(BezierPoint p0, BezierPoint p1, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        // B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
        Vector3 p = uuu * p0.position;
        p += 3f * uu * t * p0.handleOut;
        p += 3f * u * tt * p1.handleIn;
        p += ttt * p1.position;

        return p;
    }

    /// <summary>
    /// 커브의 특정 세그먼트와 t 값에 새로운 정점을 추가합니다.
    /// </summary>
    private void AddPointOnCurve(SerializedProperty pointsProp, int segmentIndex, float t)
    {
        serializedObject.Update();
        
        SerializedProperty p0Prop = pointsProp.GetArrayElementAtIndex(segmentIndex);
        SerializedProperty p1Prop = (path.IsLoop && segmentIndex == pointsProp.arraySize - 1) 
            ? pointsProp.GetArrayElementAtIndex(0) 
            : pointsProp.GetArrayElementAtIndex(segmentIndex + 1);
        
        BezierPoint p0 = GetBezierPointFromProperty(p0Prop);
        BezierPoint p1 = GetBezierPointFromProperty(p1Prop);
        
        // 새로운 정점의 위치 계산
        Vector3 newPosition = GetCubicBezierPointLocal(p0, p1, t);
        
        // 새로운 정점의 핸들 계산 (베지어 커브의 접선 방향 사용)
        Vector3 tangent = GetBezierTangentLocal(p0, p1, t);
        float handleLength = CalculateHandleLength(p0, p1, t);
        
        Vector3 newHandleIn = newPosition - tangent.normalized * handleLength * HANDLE_LENGTH_FACTOR;
        Vector3 newHandleOut = newPosition + tangent.normalized * handleLength * HANDLE_LENGTH_FACTOR;
        
        // 정점 삽입
        int insertIndex = segmentIndex + 1;
        pointsProp.InsertArrayElementAtIndex(insertIndex);
        
        SerializedProperty newPointProp = pointsProp.GetArrayElementAtIndex(insertIndex);
        newPointProp.FindPropertyRelative("position").vector3Value = newPosition;
        newPointProp.FindPropertyRelative("handleIn").vector3Value = newHandleIn;
        newPointProp.FindPropertyRelative("handleOut").vector3Value = newHandleOut;
        newPointProp.FindPropertyRelative("isBroken").boolValue = false;
        
        // 새로 추가된 정점 선택
        selectedPointIndex = insertIndex;
        
        SaveChanges("Add Point on Curve");
        Repaint();
    }

    /// <summary>
    /// 베지어 커브의 특정 t 값에서의 접선 벡터를 계산합니다.
    /// </summary>
    private Vector3 GetBezierTangentLocal(BezierPoint p0, BezierPoint p1, float t)
    {
        float u = 1f - t;
        float uu = u * u;
        float tt = t * t;
        
        // 베지어 커브의 미분: B'(t) = -3(1-t)^2 P0 + 3(1-t)(1-3t) P1 + 3t(2-3t) P2 + 3t^2 P3
        // 하지만 우리는 P0, P0Out, P1In, P1을 사용하므로:
        // B'(t) = -3(1-t)^2 P0 + 3(1-t)^2 P0Out - 6(1-t)t P0Out + 6(1-t)t P1In - 3t^2 P1In + 3t^2 P1
        Vector3 tangent = -3f * uu * p0.position;
        tangent += 3f * uu * p0.handleOut;
        tangent += -6f * u * t * p0.handleOut;
        tangent += 6f * u * t * p1.handleIn;
        tangent += -3f * tt * p1.handleIn;
        tangent += 3f * tt * p1.position;
        
        return tangent;
    }

    /// <summary>
    /// 핸들 길이를 계산합니다 (세그먼트 길이에 비례).
    /// </summary>
    private float CalculateHandleLength(BezierPoint p0, BezierPoint p1, float t)
    {
        // 세그먼트의 대략적인 길이 계산
        float segmentLength = Vector3.Distance(p0.position, p1.position);
        float handle0Length = Vector3.Distance(p0.position, p0.handleOut);
        float handle1Length = Vector3.Distance(p1.position, p1.handleIn);
        
        // 평균 핸들 길이 사용
        return (segmentLength + handle0Length + handle1Length) / 3f;
    }

    /// <summary>
    /// 정점을 제거하고 주변 정점의 베지어 커브를 재조정합니다.
    /// </summary>
    private void DeletePoint(int pointIndex)
    {
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        if (pointsProp == null || pointsProp.arraySize <= 2)
        {
            Debug.LogWarning("Cannot delete point: Minimum 2 points required.");
            return;
        }

        serializedObject.Update();

        // 제거할 정점의 정보 가져오기
        SerializedProperty deletePointProp = pointsProp.GetArrayElementAtIndex(pointIndex);
        BezierPoint deletePoint = GetBezierPointFromProperty(deletePointProp);

        // 이전/다음 정점 인덱스 계산
        int prevIndex = -1;
        int nextIndex = -1;

        if (path.IsLoop)
        {
            prevIndex = (pointIndex - 1 + pointsProp.arraySize) % pointsProp.arraySize;
            nextIndex = (pointIndex + 1) % pointsProp.arraySize;
        }
        else
        {
            prevIndex = pointIndex > 0 ? pointIndex - 1 : -1;
            nextIndex = pointIndex < pointsProp.arraySize - 1 ? pointIndex + 1 : -1;
        }

        // 이전/다음 정점이 모두 존재하는 경우에만 재조정
        if (prevIndex >= 0 && nextIndex >= 0)
        {
            SerializedProperty prevPointProp = pointsProp.GetArrayElementAtIndex(prevIndex);
            SerializedProperty nextPointProp = pointsProp.GetArrayElementAtIndex(nextIndex);

            BezierPoint prevPoint = GetBezierPointFromProperty(prevPointProp);
            BezierPoint nextPoint = GetBezierPointFromProperty(nextPointProp);

            // 제거될 정점을 통과하는 커브의 방향을 계산하여 재조정
            RecalculateHandlesAfterDeletion(prevPointProp, nextPointProp, prevPoint, deletePoint, nextPoint);
        }
        else if (prevIndex >= 0)
        {
            // 마지막 정점 제거 시: 이전 정점의 handleOut만 조정
            SerializedProperty prevPointProp = pointsProp.GetArrayElementAtIndex(prevIndex);
            BezierPoint prevPoint = GetBezierPointFromProperty(prevPointProp);
            
            // 이전 정점에서 제거될 정점 방향으로 핸들 조정
            Vector3 direction = (deletePoint.position - prevPoint.position).normalized;
            float handleLength = Vector3.Distance(prevPoint.position, prevPoint.handleOut);
            prevPointProp.FindPropertyRelative("handleOut").vector3Value = prevPoint.position + direction * handleLength;
        }
        else if (nextIndex >= 0)
        {
            // 첫 번째 정점 제거 시: 다음 정점의 handleIn만 조정
            SerializedProperty nextPointProp = pointsProp.GetArrayElementAtIndex(nextIndex);
            BezierPoint nextPoint = GetBezierPointFromProperty(nextPointProp);
            
            // 제거될 정점에서 다음 정점 방향으로 핸들 조정
            Vector3 direction = (nextPoint.position - deletePoint.position).normalized;
            float handleLength = Vector3.Distance(nextPoint.position, nextPoint.handleIn);
            nextPointProp.FindPropertyRelative("handleIn").vector3Value = nextPoint.position - direction * handleLength;
        }

        // 정점 제거
        pointsProp.DeleteArrayElementAtIndex(pointIndex);

        // 선택된 정점 인덱스 조정
        if (selectedPointIndex == pointIndex)
        {
            selectedPointIndex = -1; // 선택 해제
        }
        else if (selectedPointIndex > pointIndex)
        {
            selectedPointIndex--; // 인덱스 조정
        }

        SaveChanges("Delete Point");
    }

    /// <summary>
    /// 정점 제거 후 이전/다음 정점의 핸들을 재조정합니다.
    /// </summary>
    private void RecalculateHandlesAfterDeletion(
        SerializedProperty prevPointProp, 
        SerializedProperty nextPointProp,
        BezierPoint prevPoint, 
        BezierPoint deletePoint, 
        BezierPoint nextPoint)
    {
        // 제거될 정점을 통과하는 커브의 방향을 근사 계산
        // 이전 정점 -> 제거될 정점 -> 다음 정점의 방향을 고려

        // 이전 정점에서 제거될 정점으로의 방향
        Vector3 dirToDelete = (deletePoint.position - prevPoint.position).normalized;
        
        // 제거될 정점에서 다음 정점으로의 방향
        Vector3 dirFromDelete = (nextPoint.position - deletePoint.position).normalized;
        
        // 평균 방향 (부드러운 전환)
        Vector3 avgDirection = (dirToDelete + dirFromDelete).normalized;

        // 이전 정점의 handleOut 재조정
        float prevHandleLength = Vector3.Distance(prevPoint.position, prevPoint.handleOut);
        // 제거될 정점 방향과 평균 방향의 중간값 사용
        Vector3 prevHandleDir = (dirToDelete + avgDirection * 0.5f).normalized;
        Vector3 newPrevHandleOut = prevPoint.position + prevHandleDir * prevHandleLength;
        prevPointProp.FindPropertyRelative("handleOut").vector3Value = newPrevHandleOut;

        // 다음 정점의 handleIn 재조정
        float nextHandleLength = Vector3.Distance(nextPoint.position, nextPoint.handleIn);
        // 제거될 정점에서의 방향과 평균 방향의 중간값 사용
        Vector3 nextHandleDir = (-dirFromDelete - avgDirection * 0.5f).normalized;
        Vector3 newNextHandleIn = nextPoint.position + nextHandleDir * nextHandleLength;
        nextPointProp.FindPropertyRelative("handleIn").vector3Value = newNextHandleIn;
    }

    /// <summary>
    /// 박스 선택 입력 처리
    /// </summary>
    private void HandleBoxSelectionInput(SerializedProperty pointsProp)
    {
        Event e = Event.current;
        
        // Ctrl/Cmd + 왼쪽 마우스 드래그로 박스 선택 시작
        bool isCtrlPressed = e.control || e.command;
        bool isShiftPressed = e.shift;
        bool isAltPressed = e.alt;
        
        // 정점 추가 기능이 이미 이벤트를 사용했는지 확인
        // (HandleCurveClick이 먼저 실행되므로, 정점 추가가 성공하면 이벤트가 이미 사용됨)
        if (e.type == EventType.Used) return;
        
        // Ctrl + Shift + Alt + 드래그: 제외 선택 모드
        if (e.type == EventType.MouseDown && e.button == 0 && isCtrlPressed && isShiftPressed && isAltPressed)
        {
            isBoxSelecting = true;
            isBoxSelectAddMode = false;
            isBoxSelectSubtractMode = true; // 제외 모드
            boxSelectStart = e.mousePosition;
            boxSelectEnd = e.mousePosition;
            e.Use();
        }
        // Ctrl + Shift + 드래그: 추가 선택 모드
        else if (e.type == EventType.MouseDown && e.button == 0 && isCtrlPressed && isShiftPressed && !isAltPressed)
        {
            isBoxSelecting = true;
            isBoxSelectAddMode = true;
            isBoxSelectSubtractMode = false;
            boxSelectStart = e.mousePosition;
            boxSelectEnd = e.mousePosition;
            e.Use();
        }
        // Ctrl + 드래그: 일반 선택 모드
        else if (e.type == EventType.MouseDown && e.button == 0 && isCtrlPressed && !isShiftPressed && !isAltPressed)
        {
            isBoxSelecting = true;
            isBoxSelectAddMode = false;
            isBoxSelectSubtractMode = false;
            boxSelectStart = e.mousePosition;
            boxSelectEnd = e.mousePosition;
            e.Use();
        }
        
        // 박스 선택 중 - 키 상태 업데이트
        if (isBoxSelecting && e.type == EventType.MouseDrag)
        {
            // 드래그 중에 키 상태 업데이트
            isBoxSelectAddMode = e.shift && !e.alt;
            isBoxSelectSubtractMode = e.shift && e.alt;
            boxSelectEnd = e.mousePosition;
            e.Use();
            // 박스 선택 중에는 Repaint 필요
            SceneView.RepaintAll();
        }
        
        // 박스 선택 그리기
        if (isBoxSelecting)
        {
            Rect selectionRect = new Rect(
                Mathf.Min(boxSelectStart.x, boxSelectEnd.x),
                Mathf.Min(boxSelectStart.y, boxSelectEnd.y),
                Mathf.Abs(boxSelectEnd.x - boxSelectStart.x),
                Mathf.Abs(boxSelectEnd.y - boxSelectStart.y)
            );
            
            Handles.BeginGUI();
            // 제외 모드: 붉은색, 추가 모드: 초록색, 일반 모드: 파란색
            if (isBoxSelectSubtractMode)
            {
                GUI.color = new Color(1f, 0.5f, 0.5f, 0.2f); // 붉은색
            }
            else if (isBoxSelectAddMode)
            {
                GUI.color = new Color(0.5f, 1f, 0.5f, 0.2f); // 초록색
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 1f, 0.2f); // 파란색
            }
            GUI.DrawTexture(selectionRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        Handles.EndGUI();
    }
    }

    /// <summary>
    /// 박스 선택으로 정점 선택 처리
    /// </summary>
    private void HandleBoxSelection(SerializedProperty pointsProp)
    {
        if (pointsProp == null) return;
        
        Rect selectionRect = new Rect(
            Mathf.Min(boxSelectStart.x, boxSelectEnd.x),
            Mathf.Min(boxSelectStart.y, boxSelectEnd.y),
            Mathf.Abs(boxSelectEnd.x - boxSelectStart.x),
            Mathf.Abs(boxSelectEnd.y - boxSelectStart.y)
        );
        
        // 제외 모드: 선택 영역 내의 정점들을 선택에서 제거
        if (isBoxSelectSubtractMode)
        {
            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(i);
                Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
                Vector3 worldPos = handleTransform.TransformPoint(position);
                Vector2 guiPos = HandleUtility.WorldToGUIPoint(worldPos);
                
                if (selectionRect.Contains(guiPos))
                {
                    selectedPointIndices.Remove(i);
                    if (selectedPointIndex == i)
                    {
                        selectedPointIndex = -1;
                    }
                }
            }
        }
        // 추가 모드: 선택 영역 내의 정점들을 선택에 추가
        else if (isBoxSelectAddMode)
        {
            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(i);
                Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
                Vector3 worldPos = handleTransform.TransformPoint(position);
                Vector2 guiPos = HandleUtility.WorldToGUIPoint(worldPos);
                
                if (selectionRect.Contains(guiPos))
                {
                    selectedPointIndices.Add(i);
                }
            }
        }
        // 일반 모드: 기존 선택 초기화 후 새로 선택
        else
        {
            selectedPointIndices.Clear();
            
            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(i);
                Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
                Vector3 worldPos = handleTransform.TransformPoint(position);
                Vector2 guiPos = HandleUtility.WorldToGUIPoint(worldPos);
                
                if (selectionRect.Contains(guiPos))
                {
                    selectedPointIndices.Add(i);
                }
            }
        }
        
        if (selectedPointIndices.Count > 0)
        {
            // 첫 번째 선택된 정점을 메인 선택으로 설정
            foreach (int idx in selectedPointIndices)
            {
                selectedPointIndex = idx;
                break;
            }
        }
        else
        {
            selectedPointIndex = -1;
        }
        
        Repaint();
    }

    /// <summary>
    /// Array 섹션 UI 그리기
    /// </summary>
    private void DrawArraySection(SerializedProperty pointsProp)
    {
        EditorGUILayout.LabelField("Array", EditorStyles.boldLabel);
        
        // 선택된 정점 개수 표시
        int selectedCount = selectedPointIndices.Count;
        if (selectedCount > 0)
        {
            EditorGUILayout.HelpBox($"Selected: {selectedCount} point(s)", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Select points in Scene view (Ctrl+Click or Ctrl+Drag)", MessageType.None);
        }
        
        EditorGUI.BeginDisabledGroup(selectedCount < 2);
        
        // 첫 번째 줄: Left, Right, Top, Bottom
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("L", GUILayout.Height(25)))
        {
            AlignPoints(pointsProp, AlignDirection.Left);
        }
        if (GUILayout.Button("R", GUILayout.Height(25)))
        {
            AlignPoints(pointsProp, AlignDirection.Right);
        }
        if (GUILayout.Button("T", GUILayout.Height(25)))
        {
            AlignPoints(pointsProp, AlignDirection.Top);
        }
        if (GUILayout.Button("B", GUILayout.Height(25)))
        {
            AlignPoints(pointsProp, AlignDirection.Bottom);
        }
        EditorGUILayout.EndHorizontal();
        
        // 두 번째 줄: X Distribute, Y Distribute
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("X dist", GUILayout.Height(25)))
        {
            DistributePointsX(pointsProp);
        }
        if (GUILayout.Button("Y dist", GUILayout.Height(25)))
        {
            DistributePointsY(pointsProp);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// 선택된 정점들을 정렬 (Left, Right, Top, Bottom)
    /// </summary>
    private void AlignPoints(SerializedProperty pointsProp, AlignDirection direction)
    {
        if (selectedPointIndices.Count < 2) return;
        
        serializedObject.Update();
        
        float targetValue;
        bool useX = (direction == AlignDirection.Left || direction == AlignDirection.Right);
        bool findMin = (direction == AlignDirection.Left || direction == AlignDirection.Bottom);
        
        // 최소/최대값 찾기
        if (findMin)
        {
            targetValue = useX ? float.MaxValue : float.MaxValue;
            foreach (int idx in selectedPointIndices)
            {
                SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(idx);
                Vector3 pos = pointProp.FindPropertyRelative("position").vector3Value;
                float value = useX ? pos.x : pos.y;
                if (value < targetValue) targetValue = value;
            }
        }
        else
        {
            targetValue = useX ? float.MinValue : float.MinValue;
            foreach (int idx in selectedPointIndices)
            {
                SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(idx);
                Vector3 pos = pointProp.FindPropertyRelative("position").vector3Value;
                float value = useX ? pos.x : pos.y;
                if (value > targetValue) targetValue = value;
            }
        }
        
        // 정렬 적용
        foreach (int idx in selectedPointIndices)
        {
            SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(idx);
            Vector3 pos = pointProp.FindPropertyRelative("position").vector3Value;
            Vector3 delta;
            
            if (useX)
            {
                delta = new Vector3(targetValue - pos.x, 0f, 0f);
                pos.x = targetValue;
            }
            else
            {
                delta = new Vector3(0f, targetValue - pos.y, 0f);
                pos.y = targetValue;
            }
            
            pointProp.FindPropertyRelative("position").vector3Value = pos;
            
            Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
            Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
            pointProp.FindPropertyRelative("handleIn").vector3Value = handleIn + delta;
            pointProp.FindPropertyRelative("handleOut").vector3Value = handleOut + delta;
        }
        
        string undoName = $"Align Points {direction}";
        SaveChanges(undoName);
    }

    /// <summary>
    /// 선택된 정점들을 X축으로 균등 분배
    /// 첫 번째와 마지막 정점의 위치는 유지하고, 그 사이의 정점들을 균일한 간격으로 배치
    /// </summary>
    private void DistributePointsX(SerializedProperty pointsProp)
    {
        if (selectedPointIndices.Count < 2) return;
        
        serializedObject.Update();
        
        // 선택된 정점들을 X 좌표로 정렬
        List<int> sortedIndices = new List<int>(selectedPointIndices);
        sortedIndices.Sort((a, b) =>
        {
            Vector3 posA = pointsProp.GetArrayElementAtIndex(a).FindPropertyRelative("position").vector3Value;
            Vector3 posB = pointsProp.GetArrayElementAtIndex(b).FindPropertyRelative("position").vector3Value;
            return posA.x.CompareTo(posB.x);
        });
        
        if (sortedIndices.Count < 2) return;
        
        // 첫 번째와 마지막 정점의 X 좌표 (이 위치는 유지)
        float firstX = pointsProp.GetArrayElementAtIndex(sortedIndices[0]).FindPropertyRelative("position").vector3Value.x;
        float lastX = pointsProp.GetArrayElementAtIndex(sortedIndices[sortedIndices.Count - 1]).FindPropertyRelative("position").vector3Value.x;
        
        // 첫 번째와 마지막 사이의 거리
        float totalDistance = lastX - firstX;
        
        // 정점이 2개인 경우는 이미 첫 번째와 마지막만 있으므로 처리할 필요 없음
        if (sortedIndices.Count == 2)
        {
            SaveChanges("Distribute Points X");
            return;
        }
        
        // 중간 정점들 사이의 간격 계산
        float step = totalDistance / (sortedIndices.Count - 1);
        
        // 모든 정점을 균일한 간격으로 배치
        for (int i = 0; i < sortedIndices.Count; i++)
        {
            int idx = sortedIndices[i];
            SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(idx);
            Vector3 pos = pointProp.FindPropertyRelative("position").vector3Value;
            
            // 첫 번째와 마지막은 원래 위치 유지, 중간 정점들은 균일한 간격으로 배치
            float newX = firstX + step * i;
            Vector3 delta = new Vector3(newX - pos.x, 0f, 0f);
            
            pos.x = newX;
            pointProp.FindPropertyRelative("position").vector3Value = pos;
            
            Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
            Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
            pointProp.FindPropertyRelative("handleIn").vector3Value = handleIn + delta;
            pointProp.FindPropertyRelative("handleOut").vector3Value = handleOut + delta;
        }
        
        SaveChanges("Distribute Points X");
    }

    /// <summary>
    /// 선택된 정점들을 Y축으로 균등 분배
    /// 첫 번째와 마지막 정점의 위치는 유지하고, 그 사이의 정점들을 균일한 간격으로 배치
    /// </summary>
    private void DistributePointsY(SerializedProperty pointsProp)
    {
        if (selectedPointIndices.Count < 2) return;
        
        serializedObject.Update();
        
        // 선택된 정점들을 Y 좌표로 정렬
        List<int> sortedIndices = new List<int>(selectedPointIndices);
        sortedIndices.Sort((a, b) =>
        {
            Vector3 posA = pointsProp.GetArrayElementAtIndex(a).FindPropertyRelative("position").vector3Value;
            Vector3 posB = pointsProp.GetArrayElementAtIndex(b).FindPropertyRelative("position").vector3Value;
            return posA.y.CompareTo(posB.y);
        });
        
        if (sortedIndices.Count < 2) return;
        
        // 첫 번째와 마지막 정점의 Y 좌표 (이 위치는 유지)
        float firstY = pointsProp.GetArrayElementAtIndex(sortedIndices[0]).FindPropertyRelative("position").vector3Value.y;
        float lastY = pointsProp.GetArrayElementAtIndex(sortedIndices[sortedIndices.Count - 1]).FindPropertyRelative("position").vector3Value.y;
        
        // 첫 번째와 마지막 사이의 거리
        float totalDistance = lastY - firstY;
        
        // 정점이 2개인 경우는 이미 첫 번째와 마지막만 있으므로 처리할 필요 없음
        if (sortedIndices.Count == 2)
        {
            SaveChanges("Distribute Points Y");
            return;
        }
        
        // 중간 정점들 사이의 간격 계산
        float step = totalDistance / (sortedIndices.Count - 1);
        
        // 모든 정점을 균일한 간격으로 배치
        for (int i = 0; i < sortedIndices.Count; i++)
        {
            int idx = sortedIndices[i];
            SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(idx);
            Vector3 pos = pointProp.FindPropertyRelative("position").vector3Value;
            
            // 첫 번째와 마지막은 원래 위치 유지, 중간 정점들은 균일한 간격으로 배치
            float newY = firstY + step * i;
            Vector3 delta = new Vector3(0f, newY - pos.y, 0f);
            
            pos.y = newY;
            pointProp.FindPropertyRelative("position").vector3Value = pos;
            
            Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
            Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
            pointProp.FindPropertyRelative("handleIn").vector3Value = handleIn + delta;
            pointProp.FindPropertyRelative("handleOut").vector3Value = handleOut + delta;
        }
        
        SaveChanges("Distribute Points Y");
    }

    /// <summary>
    /// 여러 정점 선택 시 우클릭 메뉴 처리
    /// </summary>
    private void HandleMultiSelectionContextMenu(SerializedProperty pointsProp)
    {
        Event e = Event.current;
        
        // 우클릭 이벤트이고 여러 정점이 선택된 경우
        if (e.type == EventType.MouseDown && e.button == 1 && selectedPointIndices.Count > 1)
        {
            // 정점이나 핸들 근처를 클릭했는지 확인
            bool isNearPointOrHandle = false;
            for (int i = 0; i < pointsProp.arraySize; i++)
            {
                SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(i);
                Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
                Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
                Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
                
                Vector3 worldPos = handleTransform.TransformPoint(position);
                Vector3 worldHandleIn = handleTransform.TransformPoint(handleIn);
                Vector3 worldHandleOut = handleTransform.TransformPoint(handleOut);
                
                Vector2 guiPos = HandleUtility.WorldToGUIPoint(worldPos);
                Vector2 guiHandleIn = HandleUtility.WorldToGUIPoint(worldHandleIn);
                Vector2 guiHandleOut = HandleUtility.WorldToGUIPoint(worldHandleOut);
                
                if (Vector2.Distance(e.mousePosition, guiPos) < 20f ||
                    Vector2.Distance(e.mousePosition, guiHandleIn) < 20f ||
                    Vector2.Distance(e.mousePosition, guiHandleOut) < 20f)
                {
                    isNearPointOrHandle = true;
                    break;
                }
            }
            
            // 정점이나 핸들 근처가 아닌 곳을 우클릭한 경우에만 메뉴 표시
            if (!isNearPointOrHandle)
            {
                ShowMultiSelectionContextMenu(pointsProp);
                e.Use();
            }
        }
    }

    /// <summary>
    /// 여러 정점 선택 시 우클릭 메뉴 표시
    /// </summary>
    private void ShowMultiSelectionContextMenu(SerializedProperty pointsProp)
    {
        GenericMenu menu = new GenericMenu();
        
        // X Flat: 선택된 모든 정점에 적용
        menu.AddItem(new GUIContent("X Flat (All Selected)"), false, () => {
            ApplyXFlatToSelected(pointsProp);
        });
        
        // Y Flat: 선택된 모든 정점에 적용
        menu.AddItem(new GUIContent("Y Flat (All Selected)"), false, () => {
            ApplyYFlatToSelected(pointsProp);
        });
        
        menu.ShowAsContext();
    }

    /// <summary>
    /// 선택된 모든 정점에 X Flat 적용
    /// </summary>
    private void ApplyXFlatToSelected(SerializedProperty pointsProp)
    {
        if (selectedPointIndices.Count == 0) return;
        
        serializedObject.Update();
        
        foreach (int index in selectedPointIndices)
        {
            if (!IsValidPointIndex(index, pointsProp)) continue;
            
            SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(index);
            Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
            Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
            Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
            
            // 커브의 진행 방향 계산
            Vector3 curveDirection = GetCurveDirection(pointsProp, index, position);
            
            // X Flat 적용
            ApplyXFlat(pointProp, position, handleIn, handleOut, curveDirection);
        }
        
        SaveChanges("X Flat (All Selected)");
    }

    /// <summary>
    /// 선택된 모든 정점에 Y Flat 적용
    /// </summary>
    private void ApplyYFlatToSelected(SerializedProperty pointsProp)
    {
        if (selectedPointIndices.Count == 0) return;
        
        serializedObject.Update();
        
        foreach (int index in selectedPointIndices)
        {
            if (!IsValidPointIndex(index, pointsProp)) continue;
            
            SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(index);
            Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
            Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
            Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
            
            // 커브의 진행 방향 계산
            Vector3 curveDirection = GetCurveDirection(pointsProp, index, position);
            
            // Y Flat 적용
            ApplyYFlat(pointProp, position, handleIn, handleOut, curveDirection);
        }
        
        SaveChanges("Y Flat (All Selected)");
    }

    #region Runtime Component & ScriptableObject Management
    
    /// <summary>
    /// ScriptableObject의 데이터를 에디터의 points로 초기화
    /// </summary>
    private void InitFromScriptableObject()
    {
        if (path == null)
        {
            EditorUtility.DisplayDialog("Init Failed", "BezierPath 컴포넌트를 찾을 수 없습니다.", "OK");
            return;
        }

        // 현재 연결 상태 확인
        serializedObject.Update();
        SerializedProperty pathDataProp = serializedObject.FindProperty("pathData");
        BezierPathData pathData = null;
        
        if (pathDataProp != null && pathDataProp.objectReferenceValue != null)
        {
            pathData = pathDataProp.objectReferenceValue as BezierPathData;
        }
        
        // ScriptableObject가 없으면 오류 메시지
        if (pathData == null || !pathData.IsValid())
        {
            EditorUtility.DisplayDialog("Init Failed", 
                "ScriptableObject가 연결되지 않았거나 유효하지 않습니다.\n\n" +
                "먼저 Export 버튼을 사용하여 ScriptableObject를 생성하거나,\n" +
                "유효한 ScriptableObject를 연결해주세요.", 
                "OK");
            return;
        }

        // ScriptableObject의 데이터를 points로 복사
        Undo.RecordObject(path, "Init Path from ScriptableObject");
        
        serializedObject.Update();
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        SerializedProperty isLoopProp = serializedObject.FindProperty("isLoop");
        
        if (pointsProp != null)
        {
            pointsProp.ClearArray();
            
            foreach (var point in pathData.Points)
            {
                int index = pointsProp.arraySize;
                pointsProp.InsertArrayElementAtIndex(index);
                SerializedProperty newPointProp = pointsProp.GetArrayElementAtIndex(index);
                
                newPointProp.FindPropertyRelative("position").vector3Value = point.position;
                newPointProp.FindPropertyRelative("handleIn").vector3Value = point.handleIn;
                newPointProp.FindPropertyRelative("handleOut").vector3Value = point.handleOut;
                newPointProp.FindPropertyRelative("isBroken").boolValue = point.isBroken;
            }
        }
        
        if (isLoopProp != null)
        {
            isLoopProp.boolValue = pathData.IsLoop;
        }
        
        serializedObject.ApplyModifiedProperties();
        
        // path.IsLoop도 직접 설정 (프로퍼티 동기화)
        path.IsLoop = pathData.IsLoop;
        
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
        
        // 선택 초기화
        selectedPointIndex = -1;
        selectedPointIndices.Clear();
        
        // SceneView 및 Inspector 갱신
        SceneView.RepaintAll();
        Repaint();
        
        EditorUtility.DisplayDialog("Init Success", 
            $"ScriptableObject의 데이터가 성공적으로 초기화되었습니다!\n\n" +
            $"✓ ScriptableObject: {pathData.name}\n" +
            $"✓ 정점 개수: {pathData.Points.Count}\n" +
            $"✓ Is Loop: {pathData.IsLoop}", 
            "OK");
    }
    
    /// <summary>
    /// ScriptableObject 업데이트
    /// </summary>
    private void CreateOrUpdateScriptableObject()
    {
        if (path == null || path.Points == null || path.Points.Count < 2)
        {
            EditorUtility.DisplayDialog("Update Failed", "경로 데이터가 유효하지 않습니다. 최소 2개의 정점이 필요합니다.", "OK");
            return;
        }

        // 현재 연결 상태 확인
        serializedObject.Update();
        SerializedProperty pathDataProp = serializedObject.FindProperty("pathData");
        BezierPathData pathData = null;
        
        if (pathDataProp != null && pathDataProp.objectReferenceValue != null)
        {
            pathData = pathDataProp.objectReferenceValue as BezierPathData;
        }
        
        // ScriptableObject가 없으면 오류 메시지
        if (pathData == null)
        {
            EditorUtility.DisplayDialog("Update Failed", "ScriptableObject가 연결되지 않았습니다.\n\n먼저 Export 버튼을 사용하여 ScriptableObject를 생성하세요.", "OK");
            return;
        }

        // 기존 ScriptableObject 업데이트
        Undo.RecordObject(pathData, "Update Path Data");
        pathData.CopyFrom(path);
        EditorUtility.SetDirty(pathData);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Update Success", 
            $"ScriptableObject가 성공적으로 업데이트되었습니다!\n\n" +
            $"✓ ScriptableObject 업데이트: {pathData.name}\n" +
            $"✓ 런타임에서 최적화된 성능 제공", 
            "OK");

        // 업데이트된 에셋 선택
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = pathData;
    }
    
    /// <summary>
    /// BezierPath 데이터를 ScriptableObject로 내보내기
    /// </summary>
    private void ExportToScriptableObject()
    {
        if (path == null || path.Points == null || path.Points.Count < 2)
        {
            EditorUtility.DisplayDialog("Export Failed", "경로 데이터가 유효하지 않습니다. 최소 2개의 정점이 필요합니다.", "OK");
            return;
        }

        string pathName = path.name;
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Export Bezier Path Data",
            pathName + "_PathData",
            "asset",
            "ScriptableObject로 저장할 경로를 선택하세요."
        );

        if (string.IsNullOrEmpty(assetPath))
        {
            return; // 사용자가 취소
        }

        // ScriptableObject 생성
        BezierPathData pathData = ScriptableObject.CreateInstance<BezierPathData>();
        pathData.CopyFrom(path);

        // 에셋 저장
        AssetDatabase.CreateAsset(pathData, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 자동으로 ScriptableObject 연결
        serializedObject.Update();
        SerializedProperty pathDataProp = serializedObject.FindProperty("pathData");
        if (pathDataProp != null)
        {
            pathDataProp.objectReferenceValue = pathData;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        EditorUtility.DisplayDialog("Export Success", 
            $"경로 데이터가 성공적으로 내보내졌습니다!\n\n경로: {assetPath}\n\nScriptableObject가 자동으로 연결되었습니다.", 
            "OK");

        // 생성된 에셋을 선택
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = pathData;
    }
    
    #endregion
}