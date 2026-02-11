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

        [Tooltip("Appear → Loop 블렌드 비율 (0~1). 0.25 = Appear 마지막 25%와 Loop 시작이 오버랩")]
        [SerializeField, Range(0f, 0.5f)]
        private float _appearToLoopBlend = 0f;

        [Tooltip("Position 커브 사용 (시작점→중간점→도착점 베지어 곡선 이동)")]
        [SerializeField]
        private bool _appearUsePositionCurve = false;

        [Tooltip("중간 보정 위치 (시작점과 도착점 사이의 커브 제어점)")]
        [SerializeField]
        private Vector2 _appearPositionCurveOffset = Vector2.zero;

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

        [Tooltip("반복 횟수 (0 = Loop 비활성화, 1 = 1회, 2 = 2회, -1 = 무한)")]
        [SerializeField]
        private int _loopCount = 1;

        [Tooltip("반복 타입 (Yoyo: 왕복 반복, Restart: 처음부터 반복)")]
        [SerializeField]
        private LoopType _loopType = LoopType.Yoyo;

        [Tooltip("Loop → Disappear 블렌드 비율 (0~1). 0.25 = Loop 마지막 25%와 Disappear 시작이 오버랩")]
        [SerializeField, Range(0f, 0.5f)]
        private float _loopToDisappearBlend = 0f;

        [Tooltip("Position 커브 사용 (시작점→중간점→도착점 베지어 곡선 이동)")]
        [SerializeField]
        private bool _loopUsePositionCurve = false;

        [Tooltip("중간 보정 위치 (시작점과 도착점 사이의 커브 제어점)")]
        [SerializeField]
        private Vector2 _loopPositionCurveOffset = Vector2.zero;

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

        [Tooltip("Position 커브 사용 (시작점→중간점→도착점 베지어 곡선 이동)")]
        [SerializeField]
        private bool _disappearUsePositionCurve = false;

        [Tooltip("중간 보정 위치 (시작점과 도착점 사이의 커브 제어점)")]
        [SerializeField]
        private Vector2 _disappearPositionCurveOffset = Vector2.zero;

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
        public float AppearToLoopBlend => _appearToLoopBlend;
        public bool AppearUsePositionCurve => _appearUsePositionCurve;
        public Vector2 AppearPositionCurveOffset => _appearPositionCurveOffset;

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
        public float LoopToDisappearBlend => _loopToDisappearBlend;
        public bool LoopUsePositionCurve => _loopUsePositionCurve;
        public Vector2 LoopPositionCurveOffset => _loopPositionCurveOffset;

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
        public bool DisappearUsePositionCurve => _disappearUsePositionCurve;
        public Vector2 DisappearPositionCurveOffset => _disappearPositionCurveOffset;

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        // ─────────────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────────────

        /// <summary>
        /// TMPCharacterAnimation 컴포넌트의 현재 값을 이 프리셋으로 복사
        /// </summary>
        public void CopyFrom(TMPCharacterAnimation source)
        {
            if (source == null) return;

            _characterDelay = source.CharacterDelay;

            _enableAppear = source.EnableAppear;
            _appearRelative = source.AppearRelative;
            _appearPosition = source.AppearPosition;
            _appearScale = source.AppearScale;
            _appearRotation = source.AppearRotation;
            _appearAlpha = source.AppearAlpha;
            _appearDuration = source.AppearDuration;
            _appearEase = source.AppearEase;
            _appearUseCustomCurve = source.AppearUseCustomCurve;
            _appearCustomCurve = new AnimationCurve(source.AppearCustomCurve.keys);
            _appearToLoopBlend = source.AppearToLoopBlend;
            _appearUsePositionCurve = source.AppearUsePositionCurve;
            _appearPositionCurveOffset = source.AppearPositionCurveOffset;

            _enableLoop = source.EnableLoop;
            _loopRelative = source.LoopRelative;
            _loopPosition = source.LoopPosition;
            _loopScale = source.LoopScale;
            _loopRotation = source.LoopRotation;
            _loopDuration = source.LoopDuration;
            _loopEase = source.LoopEase;
            _loopUseCustomCurve = source.LoopUseCustomCurve;
            _loopCustomCurve = new AnimationCurve(source.LoopCustomCurve.keys);
            _loopCount = source.LoopCount;
            _loopType = source.LoopType;
            _loopToDisappearBlend = source.LoopToDisappearBlend;
            _loopUsePositionCurve = source.LoopUsePositionCurve;
            _loopPositionCurveOffset = source.LoopPositionCurveOffset;

            _enableDisappear = source.EnableDisappear;
            _disappearRelative = source.DisappearRelative;
            _disappearPosition = source.DisappearPosition;
            _disappearScale = source.DisappearScale;
            _disappearRotation = source.DisappearRotation;
            _disappearAlpha = source.DisappearAlpha;
            _disappearDuration = source.DisappearDuration;
            _disappearEase = source.DisappearEase;
            _disappearUseCustomCurve = source.DisappearUseCustomCurve;
            _disappearCustomCurve = new AnimationCurve(source.DisappearCustomCurve.keys);
            _disappearUsePositionCurve = source.DisappearUsePositionCurve;
            _disappearPositionCurveOffset = source.DisappearPositionCurveOffset;
        }

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
