using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class BezierFollower : MonoBehaviour
{
    [Header("Settings")]
    public BezierPath path;
    public float duration = 5f; // 이동에 걸리는 시간
    public AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1); // EaseIn/Out 제어
    [Range(0f, 1f)] public float startOffset = 0f; // 시작점 오프셋 (0~1)
    public bool followRotation = true; // 방향에 따라 회전할지 여부

    public enum LoopType { None, Restart, Yoyo }
    public LoopType loopType = LoopType.Restart;

    [Header("State")]
    [Range(0f, 1f)] public float progress = 0f;
    public bool isPlaying = true;

    private float _timer = 0f;
    private bool _isForward = true;
#if UNITY_EDITOR
    [HideInInspector] public float _lastEditorUpdateTime = 0f;
    [HideInInspector] public bool isTestMode = false;
    [HideInInspector] public float testStartTime = 0f;
    [HideInInspector] public float testDuration = 10f;
    
    // Editor에서 접근하기 위한 프로퍼티
    public float Timer { get => _timer; set => _timer = value; }
    public bool IsForward { get => _isForward; set => _isForward = value; }
#endif

    private void Reset()
    {
        UpdatePosition();
    }

    private void OnValidate()
    {
        if (path != null)
        {
            UpdatePosition();
        }
    }

#if UNITY_EDITOR
    public void StartTest(float testDurationSeconds = 10f)
    {
        isTestMode = true;
        testDuration = testDurationSeconds;
        testStartTime = (float)EditorApplication.timeSinceStartup;
        _timer = 0f;
        _isForward = true;
        isPlaying = true;
        _lastEditorUpdateTime = testStartTime;
        UpdatePosition();
    }
    
    public void StopTest()
    {
        isTestMode = false;
        isPlaying = false;
        _timer = 0f;
        _isForward = true;
        UpdatePosition();
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying) return; // 에디터 모드에서는 EditorUpdate 사용
        
        if (path == null || !isPlaying) return;

        // 1. 시간 진행 계산
        if (_isForward) _timer += Time.deltaTime / duration;
        else _timer -= Time.deltaTime / duration;

        // 2. Loop 및 경계 처리
        if (_timer >= 1f)
        {
            if (loopType == LoopType.Restart)
            {
                _timer = 0f;
            }
            else if (loopType == LoopType.Yoyo)
            {
                _timer = 1f;
                _isForward = false;
            }
            else // None
            {
                _timer = 1f;
                isPlaying = false;
            }
        }
        else if (_timer <= 0f)
        {
            if (loopType == LoopType.Yoyo)
            {
                _timer = 0f;
                _isForward = true;
            }
        }

        UpdatePosition();
    }

    public void UpdatePosition()
    {
        if (path == null) return;

        // Animation Curve를 통한 Easing 적용
        float baseProgress = movementCurve.Evaluate(_timer);
        
        // 시작점 오프셋 적용 (루프 활성화 시 래핑 처리)
        float rawProgress = baseProgress + startOffset;
        if (path.isLoop && rawProgress >= 1f)
        {
            progress = rawProgress % 1f; // 루프 래핑
        }
        else
        {
            progress = Mathf.Clamp01(rawProgress); // 일반 클램프
        }

        // 위치 업데이트
        Vector3 targetPos = path.GetPointAt(progress);
        transform.position = targetPos;

        // 회전 업데이트 (2D 기준 Z축 회전) - followRotation이 활성화된 경우에만
        if (followRotation)
        {
            Vector3 dir = path.GetDirectionAt(progress);
            if (dir != Vector3.zero)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }
    }
}