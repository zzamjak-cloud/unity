using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BezierPoint
{
    public Vector3 position;      // 정점 위치
    public Vector3 handleIn;      // 들어오는 핸들 (왼쪽)
    public Vector3 handleOut;     // 나가는 핸들 (오른쪽)
    public bool isBroken;         // 핸들 연결 끊기 여부 (Break 기능)

    public BezierPoint()
    {
        position = Vector3.zero;
        handleIn = Vector3.left;
        handleOut = Vector3.right;
        isBroken = false;
    }

    public BezierPoint(Vector3 pos)
    {
        position = pos;
        handleIn = pos + Vector3.left;
        handleOut = pos + Vector3.right;
        isBroken = false;
    }
}

public class BezierPath : MonoBehaviour
{
    [SerializeField, HideInInspector]
    private List<BezierPoint> points = new List<BezierPoint>();
    public bool isLoop = false;

    // 프로퍼티로 접근 (에디터에서 사용)
    public List<BezierPoint> Points
    {
        get
        {
            if (points == null)
            {
                points = new List<BezierPoint>();
            }
            return points;
        }
        set { points = value; }
    }

    private void Reset()
    {
        InitializePoints();
    }

    private void Awake()
    {
        if (points == null || points.Count == 0)
        {
            InitializePoints();
        }
    }

    private void InitializePoints()
    {
        if (points == null)
        {
            points = new List<BezierPoint>();
        }
        points.Clear();
        points.Add(new BezierPoint(Vector3.zero));
        points.Add(new BezierPoint(Vector3.right * 5));
    }

    // 에디터에서 사용할 수 있도록 public 메서드 제공
    public void Initialize()
    {
        InitializePoints();
    }

    /// <summary>
    /// t(0~1)에 따른 곡선 상의 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetPointAt(float t)
    {
        if (points == null || points.Count < 2) return transform.position;

        // Loop 처리 및 세그먼트 인덱스 계산
        int numSegments = isLoop ? points.Count : points.Count - 1;
        float segmentT = t * numSegments;
        int i = Mathf.FloorToInt(segmentT);
        
        if (i >= numSegments) // 마지막 지점 처리
        {
            i = numSegments - 1;
            segmentT = 1f;
        }
        else
        {
            segmentT -= i;
        }

        return GetCubicBezierPoint(i, segmentT);
    }

    /// <summary>
    /// 특정 세그먼트(i)와 로컬 t값으로 3차 베지어 좌표 계산
    /// </summary>
    private Vector3 GetCubicBezierPoint(int i, float t)
    {
        BezierPoint p0 = points[i];
        BezierPoint p1 = (isLoop && i == points.Count - 1) ? points[0] : points[i + 1];

        Vector3 p0Pos = transform.TransformPoint(p0.position);
        Vector3 p1Pos = transform.TransformPoint(p1.position);
        Vector3 p0Out = transform.TransformPoint(p0.handleOut);
        Vector3 p1In = transform.TransformPoint(p1.handleIn);

        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        // B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
        Vector3 p = uuu * p0Pos;
        p += 3f * uu * t * p0Out;
        p += 3f * u * tt * p1In;
        p += ttt * p1Pos;

        return p;
    }

    /// <summary>
    /// 곡선 상의 특정 위치에서의 이동 방향(접선)을 반환합니다. (회전용)
    /// </summary>
    public Vector3 GetDirectionAt(float t)
    {
        // 간단한 미분 근사치 사용 (현재 위치와 아주 조금 앞선 위치의 차이)
        Vector3 current = GetPointAt(t);
        Vector3 next = GetPointAt(Mathf.Clamp01(t + 0.01f));
        if (t >= 1f && isLoop) next = GetPointAt(0.01f); // Loop 끝 처리
        
        return (next - current).normalized;
    }

    // 에디터에서만 보이도록 Gizmos 그리기
    private void OnDrawGizmos()
    {
        if (points == null || points.Count < 2) return;
        
        // 곡선 미리보기 (간략하게)
        Gizmos.color = Color.green;
        Vector3 prev = GetPointAt(0);
        int steps = 20 * (points.Count - 1);
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 current = GetPointAt(t);
            Gizmos.DrawLine(prev, current);
            prev = current;
        }
    }
}