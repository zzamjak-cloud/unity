using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BezierPath))]
public class BezierPathEditor : Editor
{
    private BezierPath path;
    private Transform handleTransform;
    private Quaternion handleRotation;
    private int selectedPointIndex = -1; // 선택된 정점 인덱스
    private bool isEditingPosition = false; // 위치 편집 모드

    private void OnEnable()
    {
        path = (BezierPath)target;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (path == null) return;

        // SerializedProperty를 사용하여 직접 접근
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        
        if (pointsProp == null || !pointsProp.isArray || pointsProp.arraySize == 0)
        {
            serializedObject.Update();
            path.Initialize();
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        path = (BezierPath)target;
        
        EnsureInitialized();

        DrawDefaultInspector();

        // 현재 포인트 개수 표시 (디버깅용)
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        if (pointsProp != null)
        {
            EditorGUILayout.HelpBox($"Points Count: {pointsProp.arraySize}", MessageType.Info);
        }

        // 선택된 정점의 위치 편집 UI
        DrawSelectedPointEditor(pointsProp);

        GUILayout.Space(10);
        
        if (GUILayout.Button("Add Point", GUILayout.Height(30)))
        {
            Undo.RecordObject(path, "Add Point");
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
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
            PrefabUtility.RecordPrefabInstancePropertyModifications(path);
            Repaint(); // Inspector 갱신
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Reset Path", GUILayout.Height(20)))
        {
            Undo.RecordObject(path, "Reset Path");
            serializedObject.Update();
            path.Initialize();
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
            PrefabUtility.RecordPrefabInstancePropertyModifications(path);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        DrawPresetShapes(pointsProp);
        
        serializedObject.ApplyModifiedProperties();
    }

    private int _polygonSides = 4;
    private bool _useHandles = true;

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
                
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(path);
                PrefabUtility.RecordPrefabInstancePropertyModifications(path);
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Selection"))
            {
                selectedPointIndex = -1;
            }
            if (selectedPointIndex > 0 && GUILayout.Button("◀ Previous", GUILayout.Width(100)))
            {
                selectedPointIndex--;
            }
            if (selectedPointIndex < pointsProp.arraySize - 1 && GUILayout.Button("Next ▶", GUILayout.Width(100)))
            {
                selectedPointIndex++;
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

        // 원 프리셋
        if (GUILayout.Button("Circle 생성"))
        {
            CreateCircle(pointsProp, _useHandles);
        }

        GUILayout.Space(5);

        // 별 프리셋
        if (GUILayout.Button("Star 생성"))
        {
            CreateStar(pointsProp, _useHandles);
        }

        GUILayout.Space(5);

        // 나선 프리셋
        if (GUILayout.Button("나선 생성"))
        {
            CreateSpiral(pointsProp, _useHandles);
        }
    }

    private void CreatePolygon(SerializedProperty pointsProp, int sides, bool useHandles)
    {
        Undo.RecordObject(path, $"Create {sides}-sided Polygon");
        serializedObject.Update();

        pointsProp.arraySize = 0;

        float radius = 5f;
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
                float handleLength = (distToPrev + distToNext) * 0.3f;
                
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

        path.isLoop = true;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
    }

    private void CreateCircle(SerializedProperty pointsProp, bool useHandles)
    {
        Undo.RecordObject(path, "Create Circle");
        serializedObject.Update();

        pointsProp.arraySize = 0;

        float radius = 5f;
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
                // 이전 정점과 다음 정점 사이를 보간하는 형태로 핸들 설정
                int prevIndex = (i - 1 + pointCount) % pointCount; // Loop 처리
                int nextIndex = (i + 1) % pointCount; // Loop 처리
                
                float prevAngle = (prevIndex * 90f - 90f) * Mathf.Deg2Rad;
                float nextAngle = (nextIndex * 90f - 90f) * Mathf.Deg2Rad;
                
                Vector3 prevPos = new Vector3(Mathf.Cos(prevAngle) * radius, Mathf.Sin(prevAngle) * radius, 0f);
                Vector3 nextPos = new Vector3(Mathf.Cos(nextAngle) * radius, Mathf.Sin(nextAngle) * radius, 0f);
                
                // 이전 정점과 다음 정점 사이의 방향
                Vector3 direction = (nextPos - prevPos).normalized;
                
                // 핸들 길이는 이전-현재 또는 현재-다음 거리의 평균
                float distToPrev = Vector3.Distance(position, prevPos);
                float distToNext = Vector3.Distance(position, nextPos);
                float handleLength = (distToPrev + distToNext) * 0.3f;
                
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

        path.isLoop = true;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
    }

    private void CreateStar(SerializedProperty pointsProp, bool useHandles)
    {
        Undo.RecordObject(path, "Create Star");
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
                float handleLength = (distToPrev + distToNext) * 0.3f;
                
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

        path.isLoop = true;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
    }

    private void CreateSpiral(SerializedProperty pointsProp, bool useHandles)
    {
        Undo.RecordObject(path, "Create Spiral");
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
                float handleLength = (distToPrev + distToNext) * 0.3f;
                
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

        path.isLoop = false; // 나선은 열린 경로
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
    }

    private void OnSceneGUI()
    {
        path = (BezierPath)target;
        if (path == null) return;
        
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        if (pointsProp == null || pointsProp.arraySize == 0) return;
        
        handleTransform = path.transform;
        handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;

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

        // Ctrl + 왼쪽 클릭으로 커브에 정점 추가
        HandleCurveClick(pointsProp);

        // 1. 정점 및 핸들 그리기 및 조작
        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            ShowPoint(i, pointsProp);
        }

        // 2. 곡선 그리기 (Bezier Line)
        Handles.color = Color.white;
        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            if (!path.isLoop && i == pointsProp.arraySize - 1) break;

            SerializedProperty p0Prop = pointsProp.GetArrayElementAtIndex(i);
            SerializedProperty p1Prop = (path.isLoop && i == pointsProp.arraySize - 1) 
                ? pointsProp.GetArrayElementAtIndex(0) 
                : pointsProp.GetArrayElementAtIndex(i + 1);
            
            BezierPoint p0 = GetBezierPointFromProperty(p0Prop);
            BezierPoint p1 = GetBezierPointFromProperty(p1Prop);

            Vector3 p0Pos = handleTransform.TransformPoint(p0.position);
            Vector3 p0Out = handleTransform.TransformPoint(p0.handleOut);
            Vector3 p1In = handleTransform.TransformPoint(p1.handleIn);
            Vector3 p1Pos = handleTransform.TransformPoint(p1.position);

            Handles.DrawBezier(p0Pos, p1Pos, p0Out, p1In, Color.white, null, 3f);
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

    private void ShowPoint(int index, SerializedProperty pointsProp)
    {
        SerializedProperty pointProp = pointsProp.GetArrayElementAtIndex(index);
        
        Vector3 position = pointProp.FindPropertyRelative("position").vector3Value;
        Vector3 handleIn = pointProp.FindPropertyRelative("handleIn").vector3Value;
        Vector3 handleOut = pointProp.FindPropertyRelative("handleOut").vector3Value;
        bool isBroken = pointProp.FindPropertyRelative("isBroken").boolValue;

        // 좌표 변환 (Local -> World)
        Vector3 p = handleTransform.TransformPoint(position);
        Vector3 hIn = handleTransform.TransformPoint(handleIn);
        Vector3 hOut = handleTransform.TransformPoint(handleOut);

        // --------------------------
        // 1. 메인 정점(Anchor) 제어
        // --------------------------
        
        // 선택된 정점 강조 표시
        if (selectedPointIndex == index)
        {
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(p, handleTransform.forward, 0.25f);
            Handles.color = Color.white;
        }
        
        // 정점 이동 및 선택 처리
        EditorGUI.BeginChangeCheck();
        p = Handles.FreeMoveHandle(p, 0.2f, Vector3.zero, Handles.DotHandleCap);
        
        // 정점이 이동되었거나 클릭되었을 때 선택 처리
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Vector3 guiPos = HandleUtility.WorldToGUIPoint(p);
            Vector2 mousePos = Event.current.mousePosition;
            
            // 포인트 근처에서 클릭했는지 확인 (반경 20픽셀)
            if (Vector2.Distance(mousePos, guiPos) < 20f)
            {
                // Ctrl/Cmd 키가 눌려있지 않으면 선택 수행
                if (!Event.current.control && !Event.current.command)
                {
                    selectedPointIndex = index;
                    isEditingPosition = false;
                    Repaint(); // Inspector 갱신
                }
            }
        }
        
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Point");
            serializedObject.Update();
            Vector3 delta = p - handleTransform.TransformPoint(position); // 이동량 계산
            
            Vector3 newPos = handleTransform.InverseTransformPoint(p);
            pointProp.FindPropertyRelative("position").vector3Value = newPos;
            pointProp.FindPropertyRelative("handleIn").vector3Value = handleIn + handleTransform.InverseTransformVector(delta);
            pointProp.FindPropertyRelative("handleOut").vector3Value = handleOut + handleTransform.InverseTransformVector(delta);
            
            // 이동 시에도 선택 상태 유지
            selectedPointIndex = index;
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }
        
        // Enter 키 입력 처리 (편집 모드 전환)
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                if (selectedPointIndex == index)
                {
                    isEditingPosition = !isEditingPosition;
                    Event.current.Use();
                    Repaint(); // Inspector 갱신
                }
            }
        }

        // 우클릭 컨텍스트 메뉴 처리
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            Vector3 guiPos = HandleUtility.WorldToGUIPoint(p);
            Vector2 mousePos = Event.current.mousePosition;
            
            // 포인트 근처에서 우클릭했는지 확인 (반경 20픽셀)
            if (Vector2.Distance(mousePos, guiPos) < 20f)
            {
                Event.current.Use();
                ShowPointContextMenu(index, pointProp, position, handleIn, handleOut, isBroken);
            }
        }

        // --------------------------
        // 2. 핸들(In/Out) 제어
        // --------------------------
        Handles.color = Color.grey;
        Handles.DrawLine(p, hIn);
        Handles.DrawLine(p, hOut);

        // Handle In 제어
        // 핸들 이동 전에 Alt 키 상태 확인
        bool isAltPressedBefore = Event.current.alt;
        
        EditorGUI.BeginChangeCheck();
        hIn = Handles.FreeMoveHandle(hIn, 0.1f, Vector3.zero, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Handle In");
            serializedObject.Update();
            Vector3 newHandleIn = handleTransform.InverseTransformPoint(hIn);
            pointProp.FindPropertyRelative("handleIn").vector3Value = newHandleIn;
            
            // Alt 키를 누른 상태에서 핸들을 이동하면 자동으로 Break 활성화
            bool isAltPressed = Event.current.alt || isAltPressedBefore;
            if (isAltPressed && !isBroken)
            {
                pointProp.FindPropertyRelative("isBroken").boolValue = true;
                isBroken = true; // 로컬 변수 업데이트
            }
            
            // Break가 아닐 경우, 반대쪽 핸들(Out)을 맞은편으로 자동 이동 (미러링)
            if (!isBroken && !isAltPressed)
            {
                Vector3 localP = position;
                Vector3 dir = localP - newHandleIn;
                pointProp.FindPropertyRelative("handleOut").vector3Value = localP + dir;
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        // Handle Out 제어
        // 핸들 이동 전에 Alt 키 상태 확인
        bool isAltPressedBeforeOut = Event.current.alt;
        
        EditorGUI.BeginChangeCheck();
        hOut = Handles.FreeMoveHandle(hOut, 0.1f, Vector3.zero, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Handle Out");
            serializedObject.Update();
            Vector3 newHandleOut = handleTransform.InverseTransformPoint(hOut);
            pointProp.FindPropertyRelative("handleOut").vector3Value = newHandleOut;

            // Alt 키를 누른 상태에서 핸들을 이동하면 자동으로 Break 활성화
            bool isAltPressed = Event.current.alt || isAltPressedBeforeOut;
            if (isAltPressed && !isBroken)
            {
                pointProp.FindPropertyRelative("isBroken").boolValue = true;
                isBroken = true; // 로컬 변수 업데이트
            }

            // Break가 아닐 경우, 반대쪽 핸들(In)을 맞은편으로 자동 이동 (미러링)
            if (!isBroken && !isAltPressed)
            {
                Vector3 localP = position;
                Vector3 dir = localP - newHandleOut;
                pointProp.FindPropertyRelative("handleIn").vector3Value = localP + dir;
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

    }

    private void ShowPointContextMenu(int pointIndex, SerializedProperty pointProp, Vector3 position, Vector3 handleIn, Vector3 handleOut, bool isBroken)
    {
        GenericMenu menu = new GenericMenu();
        
        SerializedProperty pointsProp = serializedObject.FindProperty("points");
        bool canDelete = pointsProp != null && pointsProp.arraySize > 2; // 최소 2개는 유지
        
        if (isBroken)
        {
            menu.AddItem(new GUIContent("Link Handles"), false, () => {
                Undo.RecordObject(path, "Link Handles");
                serializedObject.Update();
                pointProp.FindPropertyRelative("isBroken").boolValue = false;
                
                // Link(연결)할 때 핸들을 직선으로 정렬 (현재 HandleOut 기준으로 정렬)
                Vector3 localP = position;
                Vector3 dir = handleOut - localP;
                pointProp.FindPropertyRelative("handleIn").vector3Value = localP - dir;
                
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(path);
            });
        }
        else
        {
            menu.AddItem(new GUIContent("Break Handles"), false, () => {
                Undo.RecordObject(path, "Break Handles");
                serializedObject.Update();
                pointProp.FindPropertyRelative("isBroken").boolValue = true;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(path);
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
        
        if (path.isLoop)
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
        Undo.RecordObject(path, "X flat Handles");
        serializedObject.Update();
        
        Vector3 localP = position;
        
        // 진행 방향이 주로 수평인지 확인 (X 성분이 Y 성분보다 큼)
        bool isHorizontal = Mathf.Abs(curveDirection.x) > Mathf.Abs(curveDirection.y);
        
        // Handle In 처리
        Vector3 dirIn = handleIn - localP;
        float lengthIn = dirIn.magnitude;
        if (lengthIn > 0.001f)
        {
            Vector3 newHandleIn;
            if (isHorizontal)
            {
                // 진행 방향이 수평이면: Y를 정점과 동일하게, X 방향 유지
                Vector3 horizontalDir = new Vector3(Mathf.Sign(curveDirection.x), 0f, 0f);
                newHandleIn = localP + horizontalDir * lengthIn;
                newHandleIn.y = localP.y;
            }
            else
            {
                // 진행 방향이 수직이면: 역방향으로 수평 조정
                Vector3 horizontalDir = new Vector3(Mathf.Sign(dirIn.x), 0f, 0f);
                if (Mathf.Abs(dirIn.x) < 0.001f)
                {
                    horizontalDir = new Vector3(Mathf.Sign(curveDirection.x), 0f, 0f);
                }
                newHandleIn = localP + horizontalDir * lengthIn;
                newHandleIn.y = localP.y;
            }
            pointProp.FindPropertyRelative("handleIn").vector3Value = newHandleIn;
        }
        else
        {
            pointProp.FindPropertyRelative("handleIn").vector3Value = localP + Vector3.left;
        }
        
        // Handle Out 처리
        Vector3 dirOut = handleOut - localP;
        float lengthOut = dirOut.magnitude;
        if (lengthOut > 0.001f)
        {
            Vector3 newHandleOut;
            if (isHorizontal)
            {
                // 진행 방향이 수평이면: Y를 정점과 동일하게, X 방향 유지
                Vector3 horizontalDir = new Vector3(Mathf.Sign(curveDirection.x), 0f, 0f);
                newHandleOut = localP + horizontalDir * lengthOut;
                newHandleOut.y = localP.y;
            }
            else
            {
                // 진행 방향이 수직이면: 역방향으로 수평 조정
                Vector3 horizontalDir = new Vector3(Mathf.Sign(dirOut.x), 0f, 0f);
                if (Mathf.Abs(dirOut.x) < 0.001f)
                {
                    horizontalDir = new Vector3(Mathf.Sign(curveDirection.x), 0f, 0f);
                }
                newHandleOut = localP + horizontalDir * lengthOut;
                newHandleOut.y = localP.y;
            }
            pointProp.FindPropertyRelative("handleOut").vector3Value = newHandleOut;
        }
        else
        {
            pointProp.FindPropertyRelative("handleOut").vector3Value = localP + Vector3.right;
        }
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
    }

    /// <summary>
    /// Y flat 적용: 핸들을 수직으로 조정 (진행 방향에 맞게)
    /// </summary>
    private void ApplyYFlat(SerializedProperty pointProp, Vector3 position, Vector3 handleIn, Vector3 handleOut, Vector3 curveDirection)
    {
        Undo.RecordObject(path, "Y flat Handles");
        serializedObject.Update();
        
        Vector3 localP = position;
        
        // 진행 방향이 주로 수직인지 확인 (Y 성분이 X 성분보다 큼)
        bool isVertical = Mathf.Abs(curveDirection.y) > Mathf.Abs(curveDirection.x);
        
        // Handle In 처리
        Vector3 dirIn = handleIn - localP;
        float lengthIn = dirIn.magnitude;
        if (lengthIn > 0.001f)
        {
            Vector3 newHandleIn;
            if (isVertical)
            {
                // 진행 방향이 수직이면: X를 정점과 동일하게, Y 방향 유지
                Vector3 verticalDir = new Vector3(0f, Mathf.Sign(curveDirection.y), 0f);
                newHandleIn = localP + verticalDir * lengthIn;
                newHandleIn.x = localP.x;
            }
            else
            {
                // 진행 방향이 수평이면: 역방향으로 수직 조정
                Vector3 verticalDir = new Vector3(0f, Mathf.Sign(dirIn.y), 0f);
                if (Mathf.Abs(dirIn.y) < 0.001f)
                {
                    verticalDir = new Vector3(0f, Mathf.Sign(curveDirection.y), 0f);
                }
                newHandleIn = localP + verticalDir * lengthIn;
                newHandleIn.x = localP.x;
            }
            pointProp.FindPropertyRelative("handleIn").vector3Value = newHandleIn;
        }
        else
        {
            pointProp.FindPropertyRelative("handleIn").vector3Value = localP + Vector3.down;
        }
        
        // Handle Out 처리
        Vector3 dirOut = handleOut - localP;
        float lengthOut = dirOut.magnitude;
        if (lengthOut > 0.001f)
        {
            Vector3 newHandleOut;
            if (isVertical)
            {
                // 진행 방향이 수직이면: X를 정점과 동일하게, Y 방향 유지
                Vector3 verticalDir = new Vector3(0f, Mathf.Sign(curveDirection.y), 0f);
                newHandleOut = localP + verticalDir * lengthOut;
                newHandleOut.x = localP.x;
            }
            else
            {
                // 진행 방향이 수평이면: 역방향으로 수직 조정
                Vector3 verticalDir = new Vector3(0f, Mathf.Sign(dirOut.y), 0f);
                if (Mathf.Abs(dirOut.y) < 0.001f)
                {
                    verticalDir = new Vector3(0f, Mathf.Sign(curveDirection.y), 0f);
                }
                newHandleOut = localP + verticalDir * lengthOut;
                newHandleOut.x = localP.x;
            }
            pointProp.FindPropertyRelative("handleOut").vector3Value = newHandleOut;
        }
        else
        {
            pointProp.FindPropertyRelative("handleOut").vector3Value = localP + Vector3.up;
        }
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
    }

    /// <summary>
    /// Ctrl + 왼쪽 클릭으로 커브에 정점 추가 처리
    /// </summary>
    private void HandleCurveClick(SerializedProperty pointsProp)
    {
        Event e = Event.current;
        
        // Ctrl (또는 Cmd) + 왼쪽 클릭 체크
        bool isCtrlPressed = e.control || e.command;
        bool isLeftClick = e.type == EventType.MouseDown && e.button == 0;
        
        if (!isCtrlPressed || !isLeftClick) return;
        
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
        float clickRadius = 20f; // 픽셀 단위 클릭 반경
        
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
        
        int numSegments = path.isLoop ? pointsProp.arraySize : pointsProp.arraySize - 1;
        
        for (int i = 0; i < numSegments; i++)
        {
            SerializedProperty p0Prop = pointsProp.GetArrayElementAtIndex(i);
            SerializedProperty p1Prop = (path.isLoop && i == pointsProp.arraySize - 1) 
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
        Undo.RecordObject(path, "Add Point on Curve");
        serializedObject.Update();
        
        SerializedProperty p0Prop = pointsProp.GetArrayElementAtIndex(segmentIndex);
        SerializedProperty p1Prop = (path.isLoop && segmentIndex == pointsProp.arraySize - 1) 
            ? pointsProp.GetArrayElementAtIndex(0) 
            : pointsProp.GetArrayElementAtIndex(segmentIndex + 1);
        
        BezierPoint p0 = GetBezierPointFromProperty(p0Prop);
        BezierPoint p1 = GetBezierPointFromProperty(p1Prop);
        
        // 새로운 정점의 위치 계산
        Vector3 newPosition = GetCubicBezierPointLocal(p0, p1, t);
        
        // 새로운 정점의 핸들 계산 (베지어 커브의 접선 방향 사용)
        Vector3 tangent = GetBezierTangentLocal(p0, p1, t);
        float handleLength = CalculateHandleLength(p0, p1, t);
        
        Vector3 newHandleIn = newPosition - tangent.normalized * handleLength * 0.3f;
        Vector3 newHandleOut = newPosition + tangent.normalized * handleLength * 0.3f;
        
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
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
        Repaint(); // Inspector 갱신
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

        Undo.RecordObject(path, "Delete Point");
        serializedObject.Update();

        // 제거할 정점의 정보 가져오기
        SerializedProperty deletePointProp = pointsProp.GetArrayElementAtIndex(pointIndex);
        BezierPoint deletePoint = GetBezierPointFromProperty(deletePointProp);

        // 이전/다음 정점 인덱스 계산
        int prevIndex = -1;
        int nextIndex = -1;

        if (path.isLoop)
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

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(path);
        PrefabUtility.RecordPrefabInstancePropertyModifications(path);
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
}