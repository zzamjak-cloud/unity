using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// 프리셋 카테고리 (조직화 및 필터링용)
    /// </summary>
    public enum PresetCategory
    {
        /// <summary>Outline 효과 (Offset = 0)</summary>
        Outline,

        /// <summary>Drop Shadow 효과 (Offset ≠ 0)</summary>
        DropShadow,

        /// <summary>Title 효과 (Outline + Shadow 복합)</summary>
        Title,

        /// <summary>UI 버튼용</summary>
        Button,

        /// <summary>게임 내 대사/스토리</summary>
        Dialogue,

        /// <summary>게임 UI (상태 표시 등)</summary>
        GameUI,

        /// <summary>사용자 정의</summary>
        Custom
    }

    /// <summary>
    /// TMP 효과 프리셋 (ScriptableObject)
    /// - Material 공유를 위한 공통 설정 저장
    /// - 여러 텍스트에 동일한 스타일 적용
    /// - 프로젝트 전역에서 재사용 가능
    /// </summary>
    [CreateAssetMenu(fileName = "TMPEffectPreset", menuName = "CAT/UI/TMP Effect Preset")]
    public class TMPEffectPreset : ScriptableObject, ITMPEffectSettings
    {
        [Header("Underlay Settings")]
        [SerializeField] private Color _underlayColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _underlayDilate = 0.15f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetX = 0f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetY = 0f;
        [SerializeField, Range(0f, 1f)] private float _underlaySoftness = 0.0f;

        [Header("Face Settings")]
        [SerializeField, Range(-1f, 1f)] private float _faceDilate = 0.0f;

        [Header("Shadow Settings")]
        [SerializeField] private bool _enableShadow = false;
        [SerializeField] private Vector2 _shadowOffset = new Vector2(0.1f, -0.1f);
        [SerializeField, Range(0f, 1f)] private float _shadowAlpha = 0.5f;

        [Header("Preset Info")]
        [SerializeField] private PresetCategory _category = PresetCategory.Custom;
        [SerializeField, TextArea] private string _description = "";

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        public Color UnderlayColor => _underlayColor;
        public float UnderlayDilate => _underlayDilate;
        public float UnderlayOffsetX => _underlayOffsetX;
        public float UnderlayOffsetY => _underlayOffsetY;
        public float UnderlaySoftness => _underlaySoftness;
        public float FaceDilate => _faceDilate;
        public bool EnableShadow => _enableShadow;
        public Vector2 ShadowOffset => _shadowOffset;
        public float ShadowAlpha => _shadowAlpha;
        public PresetCategory Category => _category;
        public string Description => _description;

        // ─────────────────────────────────────────────
        // Apply to Effect
        // ─────────────────────────────────────────────

        /// <summary>
        /// 프리셋 설정을 TMPOutlineEffect에 적용
        /// </summary>
        public void ApplyTo(TMPOutlineEffect effect)
        {
            if (effect == null) return;

            effect.UnderlayColor = _underlayColor;
            effect.UnderlayDilate = _underlayDilate;
            effect.UnderlayOffsetX = _underlayOffsetX;
            effect.UnderlayOffsetY = _underlayOffsetY;
            effect.UnderlaySoftness = _underlaySoftness;
            effect.FaceDilate = _faceDilate;
            effect.EnableShadow = _enableShadow;
            effect.ShadowOffset = _shadowOffset;
            effect.ShadowAlpha = _shadowAlpha;
        }

        /// <summary>
        /// TMPOutlineEffect 설정을 프리셋으로 복사
        /// </summary>
        public void CopyFrom(TMPOutlineEffect effect)
        {
            if (effect == null) return;

            _underlayColor = effect.UnderlayColor;
            _underlayDilate = effect.UnderlayDilate;
            _underlayOffsetX = effect.UnderlayOffsetX;
            _underlayOffsetY = effect.UnderlayOffsetY;
            _underlaySoftness = effect.UnderlaySoftness;
            _faceDilate = effect.FaceDilate;
            _enableShadow = effect.EnableShadow;
            _shadowOffset = effect.ShadowOffset;
            _shadowAlpha = effect.ShadowAlpha;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // ─────────────────────────────────────────────
        // Hash for Material Sharing (최적화)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 공유를 위한 최적화된 해시 계산
        /// - FNV-1a 기반 충돌 최소화
        /// - Color는 RGBA32로 변환하여 정확한 비교
        /// - Float는 비트 패턴 직접 사용
        /// </summary>
        public int GetMaterialHash()
        {
            unchecked
            {
                const uint FNV_PRIME = 16777619;
                const uint FNV_OFFSET = 2166136261;
                uint hash = FNV_OFFSET;

                // Color → RGBA32 (정확한 비트 패턴)
                Color32 c = _underlayColor;
                hash = (hash ^ c.r) * FNV_PRIME;
                hash = (hash ^ c.g) * FNV_PRIME;
                hash = (hash ^ c.b) * FNV_PRIME;
                hash = (hash ^ c.a) * FNV_PRIME;

                // Float → int 비트 패턴 (올바른 변환)
                int dilate = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlayDilate), 0);
                int offsetX = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlayOffsetX), 0);
                int offsetY = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlayOffsetY), 0);
                int softness = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_underlaySoftness), 0);
                int faceDilate = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_faceDilate), 0);

                hash = (hash ^ (uint)dilate) * FNV_PRIME;
                hash = (hash ^ (uint)offsetX) * FNV_PRIME;
                hash = (hash ^ (uint)offsetY) * FNV_PRIME;
                hash = (hash ^ (uint)softness) * FNV_PRIME;
                hash = (hash ^ (uint)faceDilate) * FNV_PRIME;

                return (int)hash;
            }
        }

        // ─────────────────────────────────────────────
        // Built-in Presets (Factory Methods)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 간단한 Outline 프리셋 생성
        /// </summary>
        public static TMPEffectPreset CreateSimpleOutline()
        {
            var preset = CreateInstance<TMPEffectPreset>();
            preset.name = "Simple Outline";
            preset._category = PresetCategory.Outline;
            preset._description = "간단한 검은색 외곽선";
            preset._underlayDilate = 0.15f;
            preset._underlayColor = Color.black;
            preset._underlayOffsetX = 0f;
            preset._underlayOffsetY = 0f;
            return preset;
        }

        /// <summary>
        /// Drop Shadow 프리셋 생성
        /// </summary>
        public static TMPEffectPreset CreateDropShadow()
        {
            var preset = CreateInstance<TMPEffectPreset>();
            preset.name = "Drop Shadow";
            preset._category = PresetCategory.DropShadow;
            preset._description = "오른쪽 아래 그림자";
            preset._underlayOffsetX = 0.1f;
            preset._underlayOffsetY = -0.1f;
            preset._underlayDilate = 0.1f;
            preset._underlayColor = new Color(0, 0, 0, 0.5f);
            return preset;
        }

        /// <summary>
        /// Title 프리셋 생성 (Outline + Shadow 복합)
        /// </summary>
        public static TMPEffectPreset CreateTitle()
        {
            var preset = CreateInstance<TMPEffectPreset>();
            preset.name = "Title";
            preset._category = PresetCategory.Title;
            preset._description = "타이틀용 고급 효과 (Outline + Shadow)";
            preset._underlayDilate = 0.25f;
            preset._underlayColor = new Color(0.2f, 0.1f, 0f);
            preset._faceDilate = 0.1f;
            preset._enableShadow = true;
            preset._shadowOffset = new Vector2(0.15f, -0.15f);
            preset._shadowAlpha = 0.6f;
            return preset;
        }
    }
}
