namespace CAT.Water2D
{
    /// <summary>
    /// 물 표면의 한 포인트. 스프링(Hooke's Law)처럼 거동하여
    /// TargetHeight 로 복원되며 좌·우 이웃 포인트로 파동을 전파한다.
    /// </summary>
    [System.Serializable]
    public struct WaterPoint
    {
        // 현재 높이 (로컬 y 오프셋, TargetHeight 기준)
        public float Height;

        // 수직 속도
        public float Velocity;

        // 복원 목표 높이 (기본 0)
        public float TargetHeight;
    }
}
