using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 원 외곽선(Circle Outline) 생성기
    /// Round, Width 값을 기반으로 외곽선만 있는 원 텍스처 생성
    /// 외곽선은 원 경계의 중앙을 기준으로 Inner/Outer 동일 비율로 확장
    /// </summary>
    public class CircleOutlineGenerator : BaseShapeGenerator
    {
        private int _round = 10;
        private int _width = 2;

        public override string ShapeName => "Circle Outline";

        public int Round
        {
            get => _round;
            set => _round = Mathf.Clamp(value, 1, 256);
        }

        public int Width
        {
            get => _width;
            set => _width = Mathf.Clamp(value, 1, _round * 2);
        }

        public override void DrawSettingsGUI()
        {
            _round = EditorGUILayout.IntSlider(
                new GUIContent("Round", "원의 반지름 (텍스처 크기 = Round * 2)"),
                _round, 1, 256);

            int maxWidth = Mathf.Max(1, _round * 2);
            _width = EditorGUILayout.IntSlider(
                new GUIContent("Width", "외곽선 두께"),
                _width, 1, maxWidth);
        }

        public override Vector2Int GetTextureSize()
        {
            int outerWidth = _width - (_width / 2);
            int size = (_round + outerWidth) * 2;
            return new Vector2Int(size, size);
        }

        public override string GetFileName()
        {
            return $"Circle_R{_round}_Out{_width}.png";
        }

        public override Vector4 GetSpriteBorder()
        {
            var size = GetTextureSize();
            float border = size.x * 0.5f;
            return new Vector4(border, border, border, border);
        }

        public override Texture2D Generate()
        {
            int innerWidthPx = _width / 2;
            int outerWidthPx = _width - innerWidthPx;

            int size = (_round + outerWidthPx) * 2;
            var (texture, pixels) = CreateTextureWithPixels(size, size);

            float center = size * 0.5f - 0.5f;
            float baseRadius = _round;
            float outerRadius = baseRadius + outerWidthPx;
            float innerRadius = baseRadius - innerWidthPx;

            for (int y = 0; y < size; y++)
            {
                int rowOffset = y * size;
                float dy = y - center;
                float dySq = dy * dy;

                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float distance = Mathf.Sqrt(dx * dx + dySq);

                    float outerSD = distance - outerRadius;
                    float innerSD = innerRadius - distance;

                    float outerAlpha = CalculateAntiAliasedAlpha(outerSD);
                    float innerAlpha = CalculateAntiAliasedAlpha(innerSD);
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
