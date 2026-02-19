using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// ColorReplace 프리셋 (ScriptableObject)
    /// - Material 공유를 위한 공통 설정 저장
    /// - 여러 오브젝트에 동일한 HSV 설정 적용
    /// - 프로젝트 전역에서 재사용 가능
    /// </summary>
    [CreateAssetMenu(fileName = "ColorReplacePreset", menuName = "CAT/Effects/Color Replace Preset")]
    public class ColorReplacePreset : ScriptableObject, IColorReplaceSettings
    {
        [Header("HSV Range")]
        [SerializeField, Range(0f, 1f)] private float _hsvRangeMin = 0f;
        [SerializeField, Range(0f, 1f)] private float _hsvRangeMax = 1f;

        [Header("HSV Adjust")]
        [SerializeField] private Vector4 _hsvAdjust = Vector4.zero;

        [Header("Preset Info")]
        [SerializeField, TextArea] private string _description = "";

        // ─────────────────────────────────────────────
        // Public Properties (IColorReplaceSettings)
        // ─────────────────────────────────────────────

        public float HSVRangeMin => _hsvRangeMin;
        public float HSVRangeMax => _hsvRangeMax;
        public Vector4 HSVAdjust => _hsvAdjust;
        public string Description => _description;

        // ─────────────────────────────────────────────
        // Hash for Material Sharing
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 공유를 위한 최적화된 해시 계산
        /// FNV-1a 알고리즘 기반
        /// </summary>
        public int GetMaterialHash()
        {
            unchecked
            {
                const uint FNV_PRIME = 16777619;
                const uint FNV_OFFSET = 2166136261;
                uint hash = FNV_OFFSET;

                // HSV Range → int 비트 패턴
                int minBits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvRangeMin), 0);
                int maxBits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvRangeMax), 0);

                hash = (hash ^ (uint)minBits) * FNV_PRIME;
                hash = (hash ^ (uint)maxBits) * FNV_PRIME;

                // HSV Adjust (x, y, z, w)
                int adjustX = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.x), 0);
                int adjustY = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.y), 0);
                int adjustZ = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.z), 0);
                int adjustW = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.w), 0);

                hash = (hash ^ (uint)adjustX) * FNV_PRIME;
                hash = (hash ^ (uint)adjustY) * FNV_PRIME;
                hash = (hash ^ (uint)adjustZ) * FNV_PRIME;
                hash = (hash ^ (uint)adjustW) * FNV_PRIME;

                return (int)hash;
            }
        }

        // ─────────────────────────────────────────────
        // Apply / Copy Methods
        // ─────────────────────────────────────────────

        /// <summary>
        /// 프리셋 설정을 ColorReplace에 적용
        /// </summary>
        public void ApplyTo(ColorReplace effect)
        {
            if (effect == null) return;

            effect.HSVRangeMin = _hsvRangeMin;
            effect.HSVRangeMax = _hsvRangeMax;
            effect.HSVAdjust = _hsvAdjust;
        }

        /// <summary>
        /// ColorReplace 설정을 프리셋으로 복사
        /// </summary>
        public void CopyFrom(ColorReplace effect)
        {
            if (effect == null) return;

            _hsvRangeMin = effect.HSVRangeMin;
            _hsvRangeMax = effect.HSVRangeMax;
            _hsvAdjust = effect.HSVAdjust;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // ─────────────────────────────────────────────
        // Built-in Presets (Factory Methods)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 빨간색 범위 프리셋 (Hue 0~0.05 또는 0.95~1)
        /// </summary>
        public static ColorReplacePreset CreateRedRange()
        {
            var preset = CreateInstance<ColorReplacePreset>();
            preset.name = "Red Range";
            preset._description = "빨간색 계열 색상 범위 (Hue: 0~0.05)";
            preset._hsvRangeMin = 0.95f;
            preset._hsvRangeMax = 0.05f;  // wrap-around
            preset._hsvAdjust = Vector4.zero;
            return preset;
        }

        /// <summary>
        /// 녹색 범위 프리셋 (Hue 0.25~0.42)
        /// </summary>
        public static ColorReplacePreset CreateGreenRange()
        {
            var preset = CreateInstance<ColorReplacePreset>();
            preset.name = "Green Range";
            preset._description = "녹색 계열 색상 범위 (Hue: 0.25~0.42)";
            preset._hsvRangeMin = 0.25f;
            preset._hsvRangeMax = 0.42f;
            preset._hsvAdjust = Vector4.zero;
            return preset;
        }

        /// <summary>
        /// 파란색 범위 프리셋 (Hue 0.55~0.72)
        /// </summary>
        public static ColorReplacePreset CreateBlueRange()
        {
            var preset = CreateInstance<ColorReplacePreset>();
            preset.name = "Blue Range";
            preset._description = "파란색 계열 색상 범위 (Hue: 0.55~0.72)";
            preset._hsvRangeMin = 0.55f;
            preset._hsvRangeMax = 0.72f;
            preset._hsvAdjust = Vector4.zero;
            return preset;
        }

        /// <summary>
        /// 그레이스케일 변환 프리셋
        /// </summary>
        public static ColorReplacePreset CreateGrayscale()
        {
            var preset = CreateInstance<ColorReplacePreset>();
            preset.name = "Grayscale";
            preset._description = "전체 색상을 그레이스케일로 변환 (Saturation -1)";
            preset._hsvRangeMin = 0f;
            preset._hsvRangeMax = 1f;
            preset._hsvAdjust = new Vector4(0f, -1f, 0f, 0f);
            return preset;
        }
    }
}
