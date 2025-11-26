using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 베지어 경로 데이터를 저장하는 ScriptableObject
/// 여러 오브젝트가 동일한 경로를 공유할 수 있도록 데이터와 로직을 분리
/// </summary>
[CreateAssetMenu(fileName = "BezierPathData", menuName = "Path/Bezier Path Data", order = 1)]
public class BezierPathData : ScriptableObject
{
    [SerializeField]
    private List<BezierPoint> points = new List<BezierPoint>();
    
    [SerializeField]
    private bool isLoop = false;
    
    [SerializeField]
    private Vector3 savedPosition = Vector3.zero;
    
    [SerializeField]
    private Quaternion savedRotation = Quaternion.identity;
    
    [SerializeField]
    private Vector3 savedScale = Vector3.one;

    /// <summary>
    /// 베지어 포인트 리스트 (런타임에서는 항상 초기화되어 있어야 함)
    /// </summary>
    public List<BezierPoint> Points
    {
        get
        {
            // 안전성을 위한 null 체크 (런타임에서는 거의 발생하지 않음)
            if (points == null)
            {
                points = new List<BezierPoint>();
            }
            return points;
        }
    }

    public bool IsLoop => isLoop;
    
    public Vector3 SavedPosition => savedPosition;
    public Quaternion SavedRotation => savedRotation;
    public Vector3 SavedScale => savedScale;

    /// <summary>
    /// BezierPath 컴포넌트에서 데이터를 복사 (Transform 정보 포함)
    /// </summary>
    public void CopyFrom(BezierPath path)
    {
        if (path == null) return;
        
        points = new List<BezierPoint>();
        foreach (var point in path.Points)
        {
            points.Add(new BezierPoint(point.position)
            {
                handleIn = point.handleIn,
                handleOut = point.handleOut,
                isBroken = point.isBroken
            });
        }
        isLoop = path.IsLoop;
        
        // Transform 정보 저장
        if (path.transform != null)
        {
            savedPosition = path.transform.position;
            savedRotation = path.transform.rotation;
            savedScale = path.transform.localScale;
        }
    }

    /// <summary>
    /// 데이터 초기화
    /// </summary>
    public void Initialize()
    {
        if (points == null)
        {
            points = new List<BezierPoint>();
        }
        points.Clear();
        points.Add(new BezierPoint(Vector3.zero));
        points.Add(new BezierPoint(Vector3.right * 5));
        isLoop = false;
        
        // Transform 정보 초기화
        savedPosition = Vector3.zero;
        savedRotation = Quaternion.identity;
        savedScale = Vector3.one;
    }

    /// <summary>
    /// 데이터 유효성 검사
    /// </summary>
    public bool IsValid()
    {
        return points != null && points.Count >= 2;
    }
}

