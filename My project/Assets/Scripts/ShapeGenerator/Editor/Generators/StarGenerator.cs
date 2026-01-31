using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 별(Star) 생성기
    /// </summary>
    public class StarGenerator : BaseShapeGenerator
    {
        private readonly StarSettings _settings;

        public override string ShapeName => "Star";

        public StarGenerator() : this(new StarSettings()) { }

        public StarGenerator(StarSettings settings)
        {
            _settings = settings;
        }

        public override void DrawSettingsGUI()
        {
            _settings.Size = EditorGUILayout.IntSlider(
                new GUIContent("Size", "별 크기"),
                _settings.Size, 4, 256);

            _settings.Points = EditorGUILayout.IntSlider(
                new GUIContent("Points", "꼭지점 개수 (3~12)"),
                _settings.Points, 3, 12);

            _settings.InnerRatio = EditorGUILayout.Slider(
                new GUIContent("Inner Ratio", "내부 반지름 비율 (0.1~0.9)"),
                _settings.InnerRatio, 0.1f, 0.9f);

            int maxRadius = _settings.Size / 8;
            _settings.CornerRadius = EditorGUILayout.IntSlider(
                new GUIContent("Corner Radius", "모서리 둥글기"),
                _settings.CornerRadius, 0, maxRadius);

            _settings.Rotation = EditorGUILayout.Slider(
                new GUIContent("Rotation", "회전 각도"),
                _settings.Rotation, 0f, 360f);
        }

        public override Vector2Int GetTextureSize()
        {
            return new Vector2Int(_settings.Size, _settings.Size);
        }

        public override string GetFileName()
        {
            int innerInt = Mathf.RoundToInt(_settings.InnerRatio * 100);
            string rotStr = _settings.Rotation > 0 ? $"_Rot{Mathf.RoundToInt(_settings.Rotation)}" : "";
            return $"Star{_settings.Points}_S{_settings.Size}_I{innerInt}_R{_settings.CornerRadius}{rotStr}.png";
        }

        public override Texture2D Generate()
        {
            var (texture, pixels) = CreateTextureWithPixels(_settings.Size, _settings.Size);

            float center = _settings.Size * 0.5f;
            float outerRadius = _settings.Size * 0.45f;
            float innerRadius = outerRadius * _settings.InnerRatio;

            int vertexCount = _settings.Points * 2;
            Vector2[] vertices = new Vector2[vertexCount];
            float angleStep = 360f / vertexCount;
            float startAngle = -90f + _settings.Rotation;

            for (int i = 0; i < vertexCount; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
                vertices[i] = new Vector2(
                    center + radius * Mathf.Cos(angle),
                    center + radius * Mathf.Sin(angle)
                );
            }

            for (int y = 0; y < _settings.Size; y++)
            {
                int rowOffset = y * _settings.Size;
                float py = y + 0.5f;

                for (int x = 0; x < _settings.Size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, py);
                    float sdf = RoundedPolygonSDF(point, vertices, _settings.CornerRadius);
                    float alpha = CalculateAntiAliasedAlpha(sdf);

                    if (alpha > 0f)
                    {
                        pixels[rowOffset + x] = new Color32(255, 255, 255, AlphaToByte(alpha));
                    }
                }
            }

            ApplyPixels(texture, pixels);
            return texture;
        }
    }
}
