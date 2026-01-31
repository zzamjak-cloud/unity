using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 원(Circle) 생성기
    /// Round 값을 기반으로 2*Round 크기의 원 텍스처 생성
    /// </summary>
    public class CircleGenerator : BaseShapeGenerator
    {
        private int _round = 10;

        public override string ShapeName => "Circle";

        public int Round
        {
            get => _round;
            set => _round = Mathf.Clamp(value, 1, 256);
        }

        public override void DrawSettingsGUI()
        {
            _round = EditorGUILayout.IntSlider(
                new GUIContent("Round", "원의 반지름 (텍스처 크기 = Round * 2)"),
                _round, 1, 256);
        }

        public override Vector2Int GetTextureSize()
        {
            return new Vector2Int(_round * 2, _round * 2);
        }

        public override string GetFileName()
        {
            return $"Circle_R{_round}.png";
        }

        public override Vector4 GetSpriteBorder()
        {
            var size = GetTextureSize();
            float border = size.x * 0.5f;
            return new Vector4(border, border, border, border);
        }

        public override Texture2D Generate()
        {
            int size = _round * 2;
            var (texture, pixels) = CreateTextureWithPixels(size, size);

            float center = _round - 0.5f;
            float radius = _round;

            for (int y = 0; y < size; y++)
            {
                int rowOffset = y * size;
                float dy = y - center;
                float dySq = dy * dy;

                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float distance = Mathf.Sqrt(dx * dx + dySq);
                    float signedDistance = distance - radius;
                    float alpha = CalculateAntiAliasedAlpha(signedDistance);

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
