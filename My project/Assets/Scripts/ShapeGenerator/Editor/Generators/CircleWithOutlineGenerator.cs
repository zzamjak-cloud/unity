using UnityEngine;
using UnityEditor;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// Circle + Outline 생성기
    /// 내부는 흰색으로 채우고, 외곽선은 그레이스케일로 설정
    /// </summary>
    public class CircleWithOutlineGenerator : BaseShapeGenerator
    {
        private int _round = 10;
        private int _outlineWidth = 2;
        private float _outlineGray = 0.5f;

        public override string ShapeName => "Circle + Outline";

        public override void DrawSettingsGUI()
        {
            _round = EditorGUILayout.IntSlider(
                new GUIContent("Round", "원의 반지름"),
                _round, 1, 256);

            int maxWidth = Mathf.Max(1, _round * 2);
            _outlineWidth = EditorGUILayout.IntSlider(
                new GUIContent("Outline Width", "외곽선 두께 (Inner/Outer 동일 비율)"),
                _outlineWidth, 1, maxWidth);

            _outlineGray = EditorGUILayout.Slider(
                new GUIContent("Outline Gray", "외곽선 그레이스케일 (0=검정, 1=흰색)"),
                _outlineGray, 0f, 1f);
        }

        public override Vector2Int GetTextureSize()
        {
            int outerWidth = _outlineWidth - (_outlineWidth / 2);
            int size = (_round + outerWidth) * 2;
            return new Vector2Int(size, size);
        }

        public override string GetFileName()
        {
            int grayInt = Mathf.RoundToInt(_outlineGray * 100);
            return $"Circle_R{_round}_OutF{_outlineWidth}_G{grayInt}.png";
        }

        public override Vector4 GetSpriteBorder()
        {
            var size = GetTextureSize();
            float border = size.x * 0.5f;
            return new Vector4(border, border, border, border);
        }

        public override Texture2D Generate()
        {
            int innerWidth = _outlineWidth / 2;
            int outerWidth = _outlineWidth - innerWidth;

            int size = (_round + outerWidth) * 2;
            var (texture, pixels) = CreateTextureWithPixels(size, size);

            float center = size * 0.5f - 0.5f;
            float baseRadius = _round;
            float fillRadius = baseRadius - innerWidth;
            float outlineOuterRadius = baseRadius + outerWidth;

            byte outlineGrayByte = (byte)(_outlineGray * 255f + 0.5f);

            for (int y = 0; y < size; y++)
            {
                int rowOffset = y * size;
                float dy = y - center;
                float dySq = dy * dy;

                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float distance = Mathf.Sqrt(dx * dx + dySq);

                    float outerSD = distance - outlineOuterRadius;
                    float outerAlpha = CalculateAntiAliasedAlpha(outerSD);

                    if (outerAlpha <= 0f) continue;

                    float fillSD = distance - fillRadius;
                    float fillAlpha = CalculateAntiAliasedAlpha(fillSD);

                    byte a = AlphaToByte(outerAlpha);

                    if (fillAlpha >= 1f)
                    {
                        pixels[rowOffset + x] = new Color32(255, 255, 255, a);
                    }
                    else if (fillAlpha <= 0f)
                    {
                        pixels[rowOffset + x] = new Color32(outlineGrayByte, outlineGrayByte, outlineGrayByte, a);
                    }
                    else
                    {
                        byte blended = (byte)(outlineGrayByte + (255 - outlineGrayByte) * fillAlpha + 0.5f);
                        pixels[rowOffset + x] = new Color32(blended, blended, blended, a);
                    }
                }
            }

            ApplyPixels(texture, pixels);
            return texture;
        }
    }
}
