using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 다각형(Polygon) 생성기
    /// N각형으로 생성하며, 모서리 R값(둥글기) 조정 가능
    /// </summary>
    public class PolygonGenerator : BaseShapeGenerator
    {
        private int _size = 20;
        private int _sides = 6;
        private int _cornerRadius = 0;
        private float _rotation = 0f;

        public override string ShapeName => "Polygon";

        public override void DrawSettingsGUI()
        {
            _size = EditorGUILayout.IntSlider(
                new GUIContent("Size", "다각형 크기"),
                _size, 4, 256);

            _sides = EditorGUILayout.IntSlider(
                new GUIContent("Sides", "변의 개수 (3~12)"),
                _sides, 3, 12);

            int maxRadius = _size / 4;
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
            string rotStr = _rotation > 0 ? $"_Rot{Mathf.RoundToInt(_rotation)}" : "";
            return $"Polygon{_sides}_S{_size}_R{_cornerRadius}{rotStr}.png";
        }

        public override Texture2D Generate()
        {
            var (texture, pixels) = CreateTextureWithPixels(_size, _size);

            float center = _size * 0.5f;
            float radius = _size * 0.45f;

            Vector2[] vertices = new Vector2[_sides];
            float angleStep = 360f / _sides;
            float startAngle = -90f + _rotation;

            for (int i = 0; i < _sides; i++)
            {
                float angle = (startAngle + angleStep * i) * Mathf.Deg2Rad;
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
