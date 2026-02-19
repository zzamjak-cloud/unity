using UnityEngine;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// TMP Effect 타입 (Outline vs Glow)
    /// </summary>
    public enum TMPEffectType
    {
        /// <summary>Outline 효과 (TMPOutlineEffect 전용)</summary>
        Outline,

        /// <summary>Glow 효과 (TMPOutGlow 전용)</summary>
        Glow
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
        [Header("Outline Settings")]
        [SerializeField] private Color _underlayColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _underlayDilate = 0.15f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetX = 0f;
        [SerializeField, Range(-1f, 1f)] private float _underlayOffsetY = 0f;
        [SerializeField, Range(0f, 1f)] private float _underlaySoftness = 0.0f;

        [Header("Face Settings")]
        [SerializeField] private bool _enableFace = false;
        [SerializeField, Range(-1f, 1f)] private float _faceDilate = 0.0f;

        [Header("Shadow Settings")]
        [SerializeField] private bool _enableShadow = false;
        [SerializeField] private Vector2 _shadowOffset = new Vector2(0.1f, -0.1f);
        [SerializeField, Range(0f, 1f)] private float _shadowAlpha = 0.5f;

        [Header("Second Face Settings")]
        [SerializeField] private bool _enableSecondFace = false;
        [SerializeField] private Color _secondFaceColor = Color.white;
        [SerializeField] private bool _useSecondFaceGradient = false;
        [SerializeField] private VertexGradient _secondFaceGradient = new VertexGradient(Color.white);
        [SerializeField, Range(-1f, 0f)] private float _secondFaceDilate = -0.1f;
        [SerializeField, Range(-1f, 1f)] private float _secondFaceOffsetX = 0f;
        [SerializeField, Range(-1f, 1f)] private float _secondFaceOffsetY = 0f;

        [Header("Preset Info")]
        [SerializeField] private TMPEffectType _effectType = TMPEffectType.Outline;
        [SerializeField, TextArea] private string _description = "";

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        public TMPEffectType EffectType => _effectType;
        public Color UnderlayColor => _underlayColor;
        public float UnderlayDilate => _underlayDilate;
        public float UnderlayOffsetX => _underlayOffsetX;
        public float UnderlayOffsetY => _underlayOffsetY;
        public float UnderlaySoftness => _underlaySoftness;
        public bool EnableFace => _enableFace;
        public float FaceDilate => _faceDilate;
        public bool EnableShadow => _enableShadow;
        public Vector2 ShadowOffset => _shadowOffset;
        public float ShadowAlpha => _shadowAlpha;
        public bool EnableSecondFace => _enableSecondFace;
        public Color SecondFaceColor => _secondFaceColor;
        public bool UseSecondFaceGradient => _useSecondFaceGradient;
        public VertexGradient SecondFaceGradient => _secondFaceGradient;
        public float SecondFaceDilate => _secondFaceDilate;
        public float SecondFaceOffsetX => _secondFaceOffsetX;
        public float SecondFaceOffsetY => _secondFaceOffsetY;
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
            effect.EnableFace = _enableFace;
            effect.FaceDilate = _faceDilate;
            effect.EnableShadow = _enableShadow;
            effect.ShadowOffset = _shadowOffset;
            effect.ShadowAlpha = _shadowAlpha;
            effect.EnableSecondFace = _enableSecondFace;
            effect.SecondFaceColor = _secondFaceColor;
            effect.UseSecondFaceGradient = _useSecondFaceGradient;
            effect.SecondFaceGradient = _secondFaceGradient;
            effect.SecondFaceDilate = _secondFaceDilate;
            effect.SecondFaceOffsetX = _secondFaceOffsetX;
            effect.SecondFaceOffsetY = _secondFaceOffsetY;
        }

        /// <summary>
        /// 프리셋 설정을 TMPOutGlow에 적용
        /// </summary>
        public void ApplyTo(TMPOutGlow glow)
        {
            if (glow == null) return;

            // Glow는 Underlay 파라미터만 사용 (Offset은 0 고정, Intensity는 1f 고정, Face는 항상 활성화)
            glow.GlowColor = _underlayColor;
            glow.GlowRange = _underlayDilate;
            glow.FaceDilate = _faceDilate;
        }

        /// <summary>
        /// TMPOutlineEffect 설정을 프리셋으로 복사
        /// </summary>
        public void CopyFrom(TMPOutlineEffect effect)
        {
            if (effect == null) return;

            _effectType = TMPEffectType.Outline;  // 타입 자동 설정
            _underlayColor = effect.UnderlayColor;
            _underlayDilate = effect.UnderlayDilate;
            _underlayOffsetX = effect.UnderlayOffsetX;
            _underlayOffsetY = effect.UnderlayOffsetY;
            _underlaySoftness = effect.UnderlaySoftness;
            _enableFace = effect.EnableFace;
            _faceDilate = effect.FaceDilate;
            _enableShadow = effect.EnableShadow;
            _shadowOffset = effect.ShadowOffset;
            _shadowAlpha = effect.ShadowAlpha;
            _enableSecondFace = effect.EnableSecondFace;
            _secondFaceColor = effect.SecondFaceColor;
            _useSecondFaceGradient = effect.UseSecondFaceGradient;
            _secondFaceGradient = effect.SecondFaceGradient;
            _secondFaceDilate = effect.SecondFaceDilate;
            _secondFaceOffsetX = effect.SecondFaceOffsetX;
            _secondFaceOffsetY = effect.SecondFaceOffsetY;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// TMPOutGlow 설정을 프리셋으로 복사
        /// </summary>
        public void CopyFrom(TMPOutGlow glow)
        {
            if (glow == null) return;

            _effectType = TMPEffectType.Glow;  // 타입 자동 설정

            // Glow 파라미터 → Underlay 파라미터로 매핑
            _underlayColor = glow.GlowColor;
            _underlayDilate = glow.GlowRange;
            _underlaySoftness = 1f;  // Glow Intensity는 1f 고정
            _underlayOffsetX = 0f;  // Glow는 Offset 0 고정
            _underlayOffsetY = 0f;
            _enableFace = true;  // Face는 항상 활성화
            _faceDilate = glow.FaceDilate;

            // Glow에서 사용하지 않는 기능은 기본값으로
            _enableShadow = false;
            _enableSecondFace = false;

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
