using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BezierPath))]
public class BezierPathEditor : Editor
{
    private BezierPath path;
    private Transform handleTransform;
    private Quaternion handleRotation;

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
            }
            
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
            PrefabUtility.RecordPrefabInstancePropertyModifications(path);
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
        EditorGUI.BeginChangeCheck();
        p = Handles.FreeMoveHandle(p, 0.2f, Vector3.zero, Handles.DotHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Point");
            serializedObject.Update();
            Vector3 delta = p - handleTransform.TransformPoint(position); // 이동량 계산
            
            Vector3 newPos = handleTransform.InverseTransformPoint(p);
            pointProp.FindPropertyRelative("position").vector3Value = newPos;
            pointProp.FindPropertyRelative("handleIn").vector3Value = handleIn + handleTransform.InverseTransformVector(delta);
            pointProp.FindPropertyRelative("handleOut").vector3Value = handleOut + handleTransform.InverseTransformVector(delta);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
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
        // 핸들 이동 전에 Ctrl 키 상태 확인
        bool isCtrlPressedBefore = Event.current.control || Event.current.command;
        
        EditorGUI.BeginChangeCheck();
        hIn = Handles.FreeMoveHandle(hIn, 0.1f, Vector3.zero, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Handle In");
            serializedObject.Update();
            Vector3 newHandleIn = handleTransform.InverseTransformPoint(hIn);
            pointProp.FindPropertyRelative("handleIn").vector3Value = newHandleIn;
            
            // Ctrl 키를 누른 상태에서 핸들을 이동하면 자동으로 Break 활성화
            bool isCtrlPressed = Event.current.control || Event.current.command || isCtrlPressedBefore;
            if (isCtrlPressed && !isBroken)
            {
                pointProp.FindPropertyRelative("isBroken").boolValue = true;
                isBroken = true; // 로컬 변수 업데이트
            }
            
            // Break가 아닐 경우, 반대쪽 핸들(Out)을 맞은편으로 자동 이동 (미러링)
            if (!isBroken && !isCtrlPressed)
            {
                Vector3 localP = position;
                Vector3 dir = localP - newHandleIn;
                pointProp.FindPropertyRelative("handleOut").vector3Value = localP + dir;
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        // Handle Out 제어
        // 핸들 이동 전에 Ctrl 키 상태 확인
        bool isCtrlPressedBeforeOut = Event.current.control || Event.current.command;
        
        EditorGUI.BeginChangeCheck();
        hOut = Handles.FreeMoveHandle(hOut, 0.1f, Vector3.zero, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Handle Out");
            serializedObject.Update();
            Vector3 newHandleOut = handleTransform.InverseTransformPoint(hOut);
            pointProp.FindPropertyRelative("handleOut").vector3Value = newHandleOut;

            // Ctrl 키를 누른 상태에서 핸들을 이동하면 자동으로 Break 활성화
            bool isCtrlPressed = Event.current.control || Event.current.command || isCtrlPressedBeforeOut;
            if (isCtrlPressed && !isBroken)
            {
                pointProp.FindPropertyRelative("isBroken").boolValue = true;
                isBroken = true; // 로컬 변수 업데이트
            }

            // Break가 아닐 경우, 반대쪽 핸들(In)을 맞은편으로 자동 이동 (미러링)
            if (!isBroken && !isCtrlPressed)
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
        
        menu.ShowAsContext();
    }
}