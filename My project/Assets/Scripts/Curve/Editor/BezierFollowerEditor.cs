using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BezierFollower))]
public class BezierFollowerEditor : Editor
{
    private BezierFollower follower;
    private bool _isPlaying = false;
    private double _startTime;
    private const float DURATION = 10.0f; // 테스트 재생 시간

    private void OnEnable()
    {
        follower = (BezierFollower)target;
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
        DrawDefaultInspector();

        if (Application.isPlaying) return; // Play 모드에서는 표시하지 않음

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Test", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(follower.path == null);
        
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
        
        if (follower.path == null)
        {
            EditorGUILayout.HelpBox("Path is not assigned. Please assign a BezierPath.", MessageType.Warning);
        }
    }

    private void StartTest()
    {
        if (_isPlaying) return;
        if (follower.path == null) return;

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
        
        follower.isTestMode = false;
        follower.isPlaying = false;
        follower.Timer = 0f;
        follower.IsForward = true;
        
        Undo.RecordObject(follower.transform, "Stop Test");
        follower.UpdatePosition();
        
        Repaint();
        SceneView.RepaintAll();
        
        Debug.Log($"[BezierFollower] {follower.name}: 테스트 중지");
    }

    private void EditorUpdate()
    {
        if (follower == null || follower.path == null)
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
        follower.UpdatePosition();
        
        // SceneView와 Inspector 갱신
        SceneView.RepaintAll();
        Repaint();
    }
}

