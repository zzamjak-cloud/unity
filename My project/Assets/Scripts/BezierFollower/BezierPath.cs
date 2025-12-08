using UnityEngine;
using System.Collections.Generic;

namespace CAT.Utility
{
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

    /// <summary>
    /// BezierPath 컴포넌트 (에디터 전용)
    /// - 에디터에서 경로를 편집하고 ScriptableObject로 저장하는 용도
    /// - 런타임에서는 BezierFollower가 ScriptableObject를 직접 사용
    /// </summary>
    public class BezierPath : MonoBehaviour
    {
        [Header("Path Data (Required)")]
        [Tooltip("ScriptableObject 경로 데이터 - Update/Export 버튼으로 저장됩니다")]
        [SerializeField]
        private BezierPathData pathData;

    #if UNITY_EDITOR
        [Header("Editor Only - Editing Data")]
        [Tooltip("에디터에서만 사용되는 편집용 데이터")]
        [SerializeField, HideInInspector]
        private List<BezierPoint> points = new List<BezierPoint>();
        
        [SerializeField, HideInInspector]
        private bool isLoop = false;

        // 에디터에서 사용할 수 있도록 프로퍼티 제공
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

        public bool IsLoop
        {
            get => isLoop;
            set => isLoop = value;
        }

        public BezierPathData PathData
        {
            get => pathData;
            set => pathData = value;
        }

        private void Reset()
        {
            InitializePoints();
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
            isLoop = false;
        }

        // 에디터에서 사용할 수 있도록 public 메서드 제공
        public void Initialize()
        {
            InitializePoints();
        }
    #endif
    }
}