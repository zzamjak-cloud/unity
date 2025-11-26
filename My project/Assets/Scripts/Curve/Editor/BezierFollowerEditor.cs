using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BezierFollower))]
[CanEditMultipleObjects] // Multi Object Editing 지원
public class BezierFollowerEditor : Editor
{
    private BezierFollower follower; // 단일 오브젝트용 (테스트 기능)
    private bool _isPlaying = false;
    private double _startTime;
    private const float DURATION = 10.0f; // 테스트 재생 시간

    private void OnEnable()
    {
        // 첫 번째 선택된 오브젝트를 follower로 사용 (테스트 기능용)
        if (targets.Length > 0)
        {
            follower = (BezierFollower)targets[0];
        }
        else
        {
            follower = (BezierFollower)target;
        }
        
        EditorApplication.update -= EditorUpdate;
        _isPlaying = false;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        _isPlaying = false;
    }

    public override void OnInspectorGUI()
    {
        // serializedObject를 사용하여 Multi Object Editing 지원
        serializedObject.Update();
        
        // 기본 인스펙터는 serializedObject를 통해 자동으로 Multi Object Editing 지원
        DrawDefaultInspector();
        
        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying) return; // Play 모드에서는 표시하지 않음

        // 여러 오브젝트 선택 시 테스트 기능 비활성화
        bool isMultiSelection = targets.Length > 1;
        
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Test", EditorStyles.boldLabel);

        if (isMultiSelection)
        {
            EditorGUILayout.HelpBox("테스트 기능은 단일 오브젝트 선택 시에만 사용 가능합니다.", MessageType.Info);
        }
        else
        {
            EditorGUI.BeginDisabledGroup(follower == null || follower.pathData == null);
            
            if (_isPlaying)
            {
                float elapsedTime = (float)(EditorApplication.timeSinceStartup - _startTime);
                float remainingTime = DURATION - elapsedTime;
                
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button($"⏹️ 테스트 중지 (남은 시간: {remainingTime:F1}s)", GUILayout.Height(30)))
                {
                    StopTest();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button($"▶️ 10초 테스트", GUILayout.Height(30)))
                {
                    StartTest();
                }
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUI.EndDisabledGroup();
            
            if (follower != null && follower.pathData == null)
            {
                EditorGUILayout.HelpBox("BezierPathData가 할당되지 않았습니다. ScriptableObject를 할당해주세요.", MessageType.Warning);
            }
        }
    }

    private void StartTest()
    {
        if (_isPlaying) return;
        if (follower.pathData == null) return;

        _startTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += EditorUpdate;
        _isPlaying = true;
        
        follower.isTestMode = true;
        follower.testDuration = DURATION;
        follower.testStartTime = (float)_startTime;
        follower.Timer = 0f;
        follower.IsForward = true;
        follower.isPlaying = true;
        follower._lastEditorUpdateTime = (float)_startTime;
        
        Undo.RecordObject(follower.transform, "Start Test");
        follower.UpdatePosition();
        
        Debug.Log($"[BezierFollower] {follower.name}: 테스트 시작 ({DURATION}초)");
    }

    private void StopTest()
    {
        EditorApplication.update -= EditorUpdate;
        _isPlaying = false;
        
        if (follower != null)
        {
            follower.isTestMode = false;
            follower.isPlaying = false;
            follower.Timer = 0f;
            follower.IsForward = true;
            
            Undo.RecordObject(follower.transform, "Stop Test");
            follower.UpdatePosition();
        }
        
        Repaint();
        SceneView.RepaintAll();
        
        if (follower != null)
        {
            Debug.Log($"[BezierFollower] {follower.name}: 테스트 중지");
        }
    }

    private void EditorUpdate()
    {
        if (follower == null || follower.pathData == null)
        {
            StopTest();
            return;
        }

        double elapsedTime = EditorApplication.timeSinceStartup - _startTime;

        if (elapsedTime >= DURATION)
        {
            StopTest();
            return;
        }

        float currentTime = (float)EditorApplication.timeSinceStartup;
        float deltaTime = currentTime - follower._lastEditorUpdateTime;
        follower._lastEditorUpdateTime = currentTime;

        // 시간 진행 계산
        if (follower.IsForward) follower.Timer += deltaTime / follower.duration;
        else follower.Timer -= deltaTime / follower.duration;

        // Loop 및 경계 처리
        if (follower.Timer >= 1f)
        {
            if (follower.loopType == BezierFollower.LoopType.Restart)
            {
                follower.Timer = 0f;
            }
            else if (follower.loopType == BezierFollower.LoopType.Yoyo)
            {
                follower.Timer = 1f;
                follower.IsForward = false;
            }
            else // None
            {
                follower.Timer = 0f; // 테스트 모드에서는 Restart처럼 동작
            }
        }
        else if (follower.Timer <= 0f)
        {
            if (follower.loopType == BezierFollower.LoopType.Yoyo)
            {
                follower.Timer = 0f;
                follower.IsForward = true;
            }
        }

        Undo.RecordObject(follower.transform, "Update Test Position");
        // 부모 Transform 변경 감지를 위해 UpdatePosition() 전에 강제로 캐시 무효화
        // (Editor 모드에서는 Update()가 호출되지 않으므로 수동으로 처리)
        follower.TransformDirty = true;
        follower.UpdatePosition();

        // SceneView와 Inspector 갱신
        SceneView.RepaintAll();
        Repaint();
    }
}

