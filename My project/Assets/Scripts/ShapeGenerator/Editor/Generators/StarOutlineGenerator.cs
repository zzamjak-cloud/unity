using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 별 외곽선(Star Outline) 생성기
    /// </summary>
    public class StarOutlineGenerator : BaseShapeGenerator
    {
        private int _size = 20;
        private int _points = 5;
        private float _innerRatio = 0.5f;
        private int _outlineWidth = 2;
        private int _cornerRadius = 0;
        private float _rotation = 0f;

        public override string ShapeName => "Star Outline";

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

            int maxWidth = _size / 4;
            _outlineWidth = EditorGUILayout.IntSlider(
                new GUIContent("Width", "외곽선 두께 (Inner/Outer 동일 비율)"),
                _outlineWidth, 1, maxWidth);

            int maxRadius = _size / 8;
            _cornerRadius = EditorGUILayout.IntSlider(
                new GUIContent("Corner Radius", "모서리 둥글기"),
                _cornerRadius, 0, maxRadius);

            _rotation = EditorGUILayout.Slider(
                new GUIContent("Rotation", "회전 각도"),
                _rotation, 0f, 360f);

            int innerWidth = _outlineWidth / 2;
            int outerWidth = _outlineWidth - innerWidth;
            EditorGUILayout.HelpBox($"Inner: {innerWidth}, Outer: {outerWidth}", MessageType.None);
        }

        public override Vector2Int GetTextureSize()
        {
            int outerWidth = _outlineWidth - (_outlineWidth / 2);
            int size = _size + outerWidth * 2;
            return new Vector2Int(size, size);
        }

        public override string GetFileName()
        {
            int innerInt = Mathf.RoundToInt(_innerRatio * 100);
            string rotStr = _rotation > 0 ? $"_Rot{Mathf.RoundToInt(_rotation)}" : "";
            return $"Star{_points}_S{_size}_Out{_outlineWidth}_I{innerInt}_R{_cornerRadius}{rotStr}.png";
        }

        public override Texture2D Generate()
        {
            int innerWidthPx = _outlineWidth / 2;
            int outerWidthPx = _outlineWidth - innerWidthPx;

            int texSize = _size + outerWidthPx * 2;
            var (texture, pixels) = CreateTextureWithPixels(texSize, texSize);

            float center = texSize * 0.5f;
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

            for (int y = 0; y < texSize; y++)
            {
                int rowOffset = y * texSize;
                float py = y + 0.5f;

                for (int x = 0; x < texSize; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, py);
                    float baseSDF = RoundedPolygonSDF(point, vertices, _cornerRadius);

                    float outerSDF = baseSDF - outerWidthPx;
                    float innerSDF = baseSDF + innerWidthPx;

                    float outerAlpha = CalculateAntiAliasedAlpha(outerSDF);
                    float innerAlpha = CalculateAntiAliasedAlpha(-innerSDF);
                    float alpha = Mathf.Min(outerAlpha, innerAlpha);

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
