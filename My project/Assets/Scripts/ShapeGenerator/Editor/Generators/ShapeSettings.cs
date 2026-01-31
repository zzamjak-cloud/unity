namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// Circle 공통 설정 (Fill, Outline, FillOutline 간 공유)
    /// </summary>
    public class CircleSettings
    {
        public int Round = 10;
        public int Width = 2;
        public float OutlineGray = 0.5f;
    }

    /// <summary>
    /// Polygon 공통 설정 (Fill, Outline, FillOutline 간 공유)
    /// </summary>
    public class PolygonSettings
    {
        public int Size = 20;
        public int Sides = 6;
        public int OutlineWidth = 2;
        public int CornerRadius = 0;
        public float Rotation = 0f;
        public float OutlineGray = 0.5f;
    }

    /// <summary>
    /// Star 공통 설정 (Fill, Outline, FillOutline 간 공유)
    /// </summary>
    public class StarSettings
    {
        public int Size = 20;
        public int Points = 5;
        public float InnerRatio = 0.5f;
        public int OutlineWidth = 2;
        public int CornerRadius = 0;
        public float Rotation = 0f;
        public float OutlineGray = 0.5f;
    }
}
