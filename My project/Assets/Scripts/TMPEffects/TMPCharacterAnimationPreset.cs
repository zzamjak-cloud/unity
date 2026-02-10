using UnityEngine;
using DG.Tweening;

namespace CAT.UI
{
    /// <summary>
    /// TMP 글자별 애니메이션 프리셋
    /// - ScriptableObject 기반 설정 저장/재사용
    /// - 빌트인 프리셋 Factory Methods 제공
    /// </summary>
    [CreateAssetMenu(fileName = "TMPCharacterAnimationPreset",
        menuName = "CAT/UI/TMP Character Animation Preset")]
    public class TMPCharacterAnimationPreset : ScriptableObject
    {
        // ─────────────────────────────────────────────
        // Inspector 설정
        // ─────────────────────────────────────────────

        [Header("Timing")]
        [Tooltip("각 글자 간 딜레이 (초)")]
        [SerializeField, Range(0f, 0.5f)]
        private float _characterDelay = 0.05f;

        [Header("Appear Animation")]
        [Tooltip("등장 애니메이션 활성화")]
        [SerializeField]
        private bool _enableAppear = true;

        [Tooltip("상대 위치 사용 (오프셋 위치에서 시작 → 원래 위치로 이동)")]
        [SerializeField]
        private bool _appearRelative = true;

        [Tooltip("시작 위치 오프셋")]
        [SerializeField]
        private Vector3 _appearPosition = new Vector3(0, 50, 0);

        [Tooltip("스케일값")]
        [SerializeField]
        private Vector3 _appearScale = new Vector3(0.5f, 0.5f, 1);

        [Tooltip("회전값")]
        [SerializeField]
        private Vector3 _appearRotation = Vector3.zero;

        [Tooltip("알파값 (0~1)")]
        [SerializeField, Range(0f, 1f)]
        private float _appearAlpha = 0f;

        [Tooltip("등장 애니메이션 시간 (초)")]
        [SerializeField]
        private float _appearDuration = 0.5f;

        [Tooltip("등장 이징 타입")]
        [SerializeField]
        private Ease _appearEase = Ease.OutBack;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _appearUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선")]
        [SerializeField]
        private AnimationCurve _appearCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Loop Animation")]
        [Tooltip("반복 애니메이션 활성화")]
        [SerializeField]
        private bool _enableLoop = false;

        [Tooltip("상대 위치 사용")]
        [SerializeField]
        private bool _loopRelative = true;

        [Tooltip("위치값")]
        [SerializeField]
        private Vector3 _loopPosition = new Vector3(0, 20, 0);

        [Tooltip("스케일값")]
        [SerializeField]
        private Vector3 _loopScale = Vector3.one;

        [Tooltip("회전값")]
        [SerializeField]
        private Vector3 _loopRotation = Vector3.zero;

        [Tooltip("반복 애니메이션 시간 (초)")]
        [SerializeField]
        private float _loopDuration = 1f;

        [Tooltip("반복 이징 타입")]
        [SerializeField]
        private Ease _loopEase = Ease.InOutSine;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _loopUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선")]
        [SerializeField]
        private AnimationCurve _loopCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("반복 횟수 (-1 = 무한)")]
        [SerializeField]
        private int _loopCount = -1;

        [Tooltip("반복 타입 (Yoyo: 왕복 반복, Restart: 처음부터 반복)")]
        [SerializeField]
        private LoopType _loopType = LoopType.Yoyo;

        [Header("Disappear Animation")]
        [Tooltip("사라짐 애니메이션 활성화")]
        [SerializeField]
        private bool _enableDisappear = false;

        [Tooltip("상대 위치 사용 (원래 위치에서 시작 → 오프셋 위치로 이동)")]
        [SerializeField]
        private bool _disappearRelative = true;

        [Tooltip("목표 위치 오프셋")]
        [SerializeField]
        private Vector3 _disappearPosition = new Vector3(0, -50, 0);

        [Tooltip("스케일값")]
        [SerializeField]
        private Vector3 _disappearScale = new Vector3(0.5f, 0.5f, 1);

        [Tooltip("회전값")]
        [SerializeField]
        private Vector3 _disappearRotation = Vector3.zero;

        [Tooltip("알파값 (0~1)")]
        [SerializeField, Range(0f, 1f)]
        private float _disappearAlpha = 0f;

        [Tooltip("사라짐 애니메이션 시간 (초)")]
        [SerializeField]
        private float _disappearDuration = 0.5f;

        [Tooltip("사라짐 이징 타입")]
        [SerializeField]
        private Ease _disappearEase = Ease.InBack;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _disappearUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선")]
        [SerializeField]
        private AnimationCurve _disappearCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Preset Info")]
        [Tooltip("프리셋 설명")]
        [SerializeField, TextArea]
        private string _description = "";

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        public float CharacterDelay => _characterDelay;

        public bool EnableAppear => _enableAppear;
        public bool AppearRelative => _appearRelative;
        public Vector3 AppearPosition => _appearPosition;
        public Vector3 AppearScale => _appearScale;
        public Vector3 AppearRotation => _appearRotation;
        public float AppearAlpha => _appearAlpha;
        public float AppearDuration => _appearDuration;
        public Ease AppearEase => _appearEase;
        public bool AppearUseCustomCurve => _appearUseCustomCurve;
        public AnimationCurve AppearCustomCurve => _appearCustomCurve;

        public bool EnableLoop => _enableLoop;
        public bool LoopRelative => _loopRelative;
        public Vector3 LoopPosition => _loopPosition;
        public Vector3 LoopScale => _loopScale;
        public Vector3 LoopRotation => _loopRotation;
        public float LoopDuration => _loopDuration;
        public Ease LoopEase => _loopEase;
        public bool LoopUseCustomCurve => _loopUseCustomCurve;
        public AnimationCurve LoopCustomCurve => _loopCustomCurve;
        public int LoopCount => _loopCount;
        public LoopType LoopType => _loopType;

        public bool EnableDisappear => _enableDisappear;
        public bool DisappearRelative => _disappearRelative;
        public Vector3 DisappearPosition => _disappearPosition;
        public Vector3 DisappearScale => _disappearScale;
        public Vector3 DisappearRotation => _disappearRotation;
        public float DisappearAlpha => _disappearAlpha;
        public float DisappearDuration => _disappearDuration;
        public Ease DisappearEase => _disappearEase;
        public bool DisappearUseCustomCurve => _disappearUseCustomCurve;
        public AnimationCurve DisappearCustomCurve => _disappearCustomCurve;

        public string Description => _description;

        // ─────────────────────────────────────────────
        // Factory Methods (Built-in Presets)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 빌트인 프리셋: 튕기며 등장
        /// </summary>
        public static TMPCharacterAnimationPreset CreateBounceAppear()
        {
            var preset = CreateInstance<TMPCharacterAnimationPreset>();
            preset.name = "Bounce Appear";
            preset._description = "글자가 위에서 튕기며 등장합니다.";
            preset._characterDelay = 0.05f;

            preset._enableAppear = true;
            preset._appearRelative = true;
            preset._appearPosition = new Vector3(0, 100, 0);
            preset._appearScale = new Vector3(0.5f, 0.5f, 1);
            preset._appearRotation = Vector3.zero;
            preset._appearAlpha = 0f;
            preset._appearDuration = 0.6f;
            preset._appearEase = Ease.OutBounce;
            preset._appearUseCustomCurve = false;

            preset._enableLoop = false;
            preset._enableDisappear = false;

            return preset;
        }

        /// <summary>
        /// 빌트인 프리셋: Y축 웨이브 반복
        /// </summary>
        public static TMPCharacterAnimationPreset CreateWaveLoop()
        {
            var preset = CreateInstance<TMPCharacterAnimationPreset>();
            preset.name = "Wave Loop";
            preset._description = "글자가 Y축으로 춤추듯이 무한 반복합니다.";
            preset._characterDelay = 0.05f;

            preset._enableAppear = false;

            preset._enableLoop = true;
            preset._loopRelative = true;
            preset._loopPosition = new Vector3(0, 30, 0);
            preset._loopScale = Vector3.one;
            preset._loopRotation = Vector3.zero;
            preset._loopDuration = 0.8f;
            preset._loopEase = Ease.InOutSine;
            preset._loopUseCustomCurve = false;
            preset._loopCount = -1;
            preset._loopType = LoopType.Yoyo;

            preset._enableDisappear = false;

            return preset;
        }

        /// <summary>
        /// 빌트인 프리셋: 스케일 축소하며 사라짐
        /// </summary>
        public static TMPCharacterAnimationPreset CreateScaleDisappear()
        {
            var preset = CreateInstance<TMPCharacterAnimationPreset>();
            preset.name = "Scale Disappear";
            preset._description = "글자가 축소되며 사라집니다.";
            preset._characterDelay = 0.05f;

            preset._enableAppear = false;
            preset._enableLoop = false;

            preset._enableDisappear = true;
            preset._disappearRelative = true;
            preset._disappearPosition = Vector3.zero;
            preset._disappearScale = new Vector3(0, 0, 1);
            preset._disappearRotation = Vector3.zero;
            preset._disappearAlpha = 0f;
            preset._disappearDuration = 0.5f;
            preset._disappearEase = Ease.InBack;
            preset._disappearUseCustomCurve = false;

            return preset;
        }

        /// <summary>
        /// 빌트인 프리셋: 회전하며 등장
        /// </summary>
        public static TMPCharacterAnimationPreset CreateRotateAppear()
        {
            var preset = CreateInstance<TMPCharacterAnimationPreset>();
            preset.name = "Rotate Appear";
            preset._description = "글자가 회전하며 등장합니다.";
            preset._characterDelay = 0.05f;

            preset._enableAppear = true;
            preset._appearRelative = true;
            preset._appearPosition = Vector3.zero;
            preset._appearScale = new Vector3(0.5f, 0.5f, 1);
            preset._appearRotation = new Vector3(0, 0, 180);
            preset._appearAlpha = 0f;
            preset._appearDuration = 0.5f;
            preset._appearEase = Ease.OutBack;
            preset._appearUseCustomCurve = false;

            preset._enableLoop = false;
            preset._enableDisappear = false;

            return preset;
        }

        /// <summary>
        /// 빌트인 프리셋: 페이드 인/아웃
        /// </summary>
        public static TMPCharacterAnimationPreset CreateFadeInOut()
        {
            var preset = CreateInstance<TMPCharacterAnimationPreset>();
            preset.name = "Fade In Out";
            preset._description = "글자가 페이드 인 후 페이드 아웃합니다.";
            preset._characterDelay = 0.05f;

            preset._enableAppear = true;
            preset._appearRelative = true;
            preset._appearPosition = Vector3.zero;
            preset._appearScale = Vector3.one;
            preset._appearRotation = Vector3.zero;
            preset._appearAlpha = 0f;
            preset._appearDuration = 0.3f;
            preset._appearEase = Ease.OutQuad;
            preset._appearUseCustomCurve = false;

            preset._enableLoop = false;

            preset._enableDisappear = true;
            preset._disappearRelative = true;
            preset._disappearPosition = Vector3.zero;
            preset._disappearScale = Vector3.one;
            preset._disappearRotation = Vector3.zero;
            preset._disappearAlpha = 0f;
            preset._disappearDuration = 0.3f;
            preset._disappearEase = Ease.InQuad;
            preset._disappearUseCustomCurve = false;

            return preset;
        }
    }
}
