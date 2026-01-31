using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 별(Star) 생성기
    /// N각 별로 생성하며, 모서리 R값(둥글기) 조정 가능
    /// </summary>
    public class StarGenerator : BaseShapeGenerator
    {
        private int _size = 20;
        private int _points = 5;
        private float _innerRatio = 0.5f;
        private int _cornerRadius = 0;
        private float _rotation = 0f;

        public override string ShapeName => "Star";

        public override void DrawSettingsGUI()
        {
            _size = EditorGUILayout.IntSlider(
                new GUIContent("Size", "별 크기"),
                _size, 4, 256);

            _points = EditorGUILayout.IntSlider(
                new GUIContent("Points", "꼭지점 개수 (3~12)"),
                _points, 3, 12);

            _innerRatio = EditorGUILayout.Slider(
                new GUIContent("Inner Ratio", "내부 반지름 비율 (0.1~0.9)"),
                _innerRatio, 0.1f, 0.9f);

            int maxRadius = _size / 8;
            _cornerRadius = EditorGUILayout.IntSlider(
                new GUIContent("Corner Radius", "모서리 둥글기"),
                _cornerRadius, 0, maxRadius);

            _rotation = EditorGUILayout.Slider(
                new GUIContent("Rotation", "회전 각도"),
                _rotation, 0f, 360f);
        }

        public override Vector2Int GetTextureSize()
        {
            return new Vector2Int(_size, _size);
        }

        public override string GetFileName()
        {
            int innerInt = Mathf.RoundToInt(_innerRatio * 100);
            string rotStr = _rotation > 0 ? $"_Rot{Mathf.RoundToInt(_rotation)}" : "";
            return $"Star{_points}_S{_size}_I{innerInt}_R{_cornerRadius}{rotStr}.png";
        }

        public override Texture2D Generate()
        {
            var (texture, pixels) = CreateTextureWithPixels(_size, _size);

            float center = _size * 0.5f;
            float outerRadius = _size * 0.45f;
            float innerRadius = outerRadius * _innerRatio;

            int vertexCount = _points * 2;
            Vector2[] vertices = new Vector2[vertexCount];
            float angleStep = 360f / vertexCount;
            float startAngle = -90f + _rotation;

            for (int i = 0; i < vertexCount; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
                vertices[i] = new Vector2(
                    center + radius * Mathf.Cos(angle),
                    center + radius * Mathf.Sin(angle)
                );
            }

            for (int y = 0; y < _size; y++)
            {
                int rowOffset = y * _size;
                float py = y + 0.5f;

                for (int x = 0; x < _size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, py);
                    float sdf = RoundedPolygonSDF(point, vertices, _cornerRadius);
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
