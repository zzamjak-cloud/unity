using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CAT.UI
{
    /// <summary>
    /// TMP 텍스트 글자별 애니메이션
    /// - 각 글자를 독립적으로 애니메이션 (Appear, Loop, Disappear)
    /// - Update 기반 자체 시간 관리 (DOTween 의존성 제거)
    /// - 프리셋 시스템 지원
    /// - 모바일 최적화: 배열 재사용, GC Alloc 최소화
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(20)]  // TMPCurve(10) 이후 실행
    [RequireComponent(typeof(TMP_Text))]
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("CAT/UI/TMP Animation")]
    public class TMPAnimation : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector 설정
        // ─────────────────────────────────────────────

        [Header("Animation Settings")]
        [Tooltip("애니메이션 프리셋")]
        [SerializeField]
        private TMPAnimationPreset _preset;

        [Tooltip("OnEnable 시 자동 재생")]
        [SerializeField]
        private bool _playOnEnable = true;

        [Tooltip("최초 실행 딜레이 (초) - 0이면 1프레임 후 자동 실행")]
        [SerializeField, Range(0f, 5f)]
        private float _initialDelay = 0f;

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

        [Tooltip("시작 위치 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _appearPosition = new Vector3(0, 50, 0);

        [Tooltip("시작 스케일 (Relative: 현재×값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _appearScale = new Vector3(0.5f, 0.5f, 1);

        [Tooltip("시작 회전 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _appearRotation = Vector3.zero;

        [Tooltip("시작 알파값 (0~1, 원래 위치에서는 1.0)")]
        [SerializeField, Range(0f, 1f)]
        private float _appearAlpha = 0f;

        [Tooltip("등장 애니메이션 시간 (초)")]
        [SerializeField]
        private float _appearDuration = 0.5f;

        [Tooltip("등장 이징 타입")]
        [SerializeField]
        private TMPEaseType _appearEase = TMPEaseType.OutBack;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _appearUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선 (Use Custom Curve 활성화 시)")]
        [SerializeField]
        private AnimationCurve _appearCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Appear → Loop 블렌드 비율 (0~1). 0.25 = Appear 마지막 25%와 Loop 시작이 오버랩")]
        [SerializeField, Range(0f, 0.5f)]
        private float _appearToLoopBlend = 0f;

        [Header("Appear Position Curve")]
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

        [Tooltip("상대 위치 사용 (현재 위치 기준)")]
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
        private TMPEaseType _loopEase = TMPEaseType.InOutSine;

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
        private TMPLoopMode _loopType = TMPLoopMode.Yoyo;

        [Tooltip("Loop → Disappear 블렌드 비율 (0~1). 0.25 = Loop 마지막 25%와 Disappear 시작이 오버랩")]
        [SerializeField, Range(0f, 0.5f)]
        private float _loopToDisappearBlend = 0f;

        [Header("Loop Position Curve")]
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

        [Tooltip("목표 위치 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _disappearPosition = new Vector3(0, -50, 0);

        [Tooltip("목표 스케일 (Relative: 현재×값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _disappearScale = new Vector3(0.5f, 0.5f, 1);

        [Tooltip("목표 회전 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
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
        private TMPEaseType _disappearEase = TMPEaseType.InBack;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _disappearUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선")]
        [SerializeField]
        private AnimationCurve _disappearCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Disappear Position Curve")]
        [Tooltip("Position 커브 사용 (시작점→중간점→도착점 베지어 곡선 이동)")]
        [SerializeField]
        private bool _disappearUsePositionCurve = false;

        [Tooltip("중간 보정 위치 (시작점과 도착점 사이의 커브 제어점)")]
        [SerializeField]
        private Vector2 _disappearPositionCurveOffset = Vector2.zero;

        // ─────────────────────────────────────────────
        // 글자별 애니메이션 상태 (DOTween Sequence 대체)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 각 글자의 애니메이션 타임라인 및 상태를 추적하는 구조체.
        /// DOTween Sequence를 대체하여 Update 기반 시간 관리를 수행합니다.
        /// </summary>
        private struct CharAnimState
        {
            public float delay;           // 글자별 지연 시간
            public float elapsedTime;     // 경과 시간 (delay 포함)
            public bool isActive;

            // Appear 타임라인
            public float appearStart, appearEnd;
            public Vector3 appearFromPos, appearToPos;
            public Vector3 appearFromScale, appearToScale;
            public Vector3 appearFromRot, appearToRot;
            public float appearFromAlpha, appearToAlpha;
            public bool appearUsePosCurve;
            public Vector2 appearPosCurveOffset;
            public bool appearUseCustomCurve;
            public AnimationCurve appearCustomCurve;
            public TMPEaseType appearEase;

            // Loop 타임라인
            public float loopStart;       // 루프 시작 시간 (delay 이후 기준)
            public float loopSingleDuration;
            public int loopCount;         // -1 = 무한
            public TMPLoopMode loopMode;
            public Vector3 loopFromPos, loopToPos;
            public Vector3 loopFromScale, loopToScale;
            public Vector3 loopFromRot, loopToRot;
            public float loopFromAlpha, loopToAlpha;
            public bool loopUsePosCurve;
            public Vector2 loopPosCurveOffset;
            public bool loopUseCustomCurve;
            public AnimationCurve loopCustomCurve;
            public TMPEaseType loopEase;
            public bool loopEnabled;

            // Disappear 타임라인
            public float disappearStart, disappearEnd;
            public Vector3 disappearFromPos, disappearToPos;
            public Vector3 disappearFromScale, disappearToScale;
            public Vector3 disappearFromRot, disappearToRot;
            public float disappearFromAlpha, disappearToAlpha;
            public bool disappearUsePosCurve;
            public Vector2 disappearPosCurveOffset;
            public bool disappearUseCustomCurve;
            public AnimationCurve disappearCustomCurve;
            public TMPEaseType disappearEase;
            public bool disappearEnabled;

            // 블렌딩 캡처 상태
            public bool appearToLoopBlendActive;
            public bool loopToDisappearBlendActive;
            public bool hasLoopCaptured;       // Loop 시작 시 캡처 완료 여부
            public bool hasDisappearCaptured;  // Disappear 시작 시 캡처 완료 여부

            // 전체 타임라인 끝 시간 (무한 루프가 아닌 경우)
            public float totalDuration;
            public bool isInfiniteLoop;
        }

        // ─────────────────────────────────────────────
        // 캐싱
        // ─────────────────────────────────────────────

        private TMP_Text _tmpText;
        private CanvasGroup _canvasGroup;
        private CharAnimState[] _charStates;
        private Vector3[] _originalPositions;
        private Vector3[][] _originalVertices;
        private Vector3[][] _originalVerticesSecondFace;
        private Vector3[][] _originalVerticesInnerGlow;  // InnerGlow 원본 정점
        private Color32[][] _originalColors;
        private Color32[][] _originalColorsSecondFace;
        private Color32[][] _originalColorsInnerGlow;  // InnerGlow 원본 색상
        private bool _isPlaying = false;
        private bool _isPaused = false;
        private bool _isPlayingInProgress = false;
        private bool _hasStarted = false;

        // 각 글자의 현재 애니메이션 상태 (블렌딩용)
        private Vector3[] _currentCharPos;
        private Vector3[] _currentCharScale;
        private Vector3[] _currentCharRot;
        private float[] _currentCharAlpha;

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        public bool IsPlaying => _isPlaying;

        public TMPAnimationPreset Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                if (value != null)
                {
                    ApplyPreset(value);
                }
            }
        }

        // Timing
        public float CharacterDelay => _characterDelay;

        // Appear Animation
        public bool EnableAppear => _enableAppear;
        public bool AppearRelative => _appearRelative;
        public Vector3 AppearPosition => _appearPosition;
        public Vector3 AppearScale => _appearScale;
        public Vector3 AppearRotation => _appearRotation;
        public float AppearAlpha => _appearAlpha;
        public float AppearDuration => _appearDuration;
        public TMPEaseType AppearEase => _appearEase;
        public bool AppearUseCustomCurve => _appearUseCustomCurve;
        public AnimationCurve AppearCustomCurve => _appearCustomCurve;
        public float AppearToLoopBlend => _appearToLoopBlend;
        public bool AppearUsePositionCurve => _appearUsePositionCurve;
        public Vector2 AppearPositionCurveOffset => _appearPositionCurveOffset;

        // Loop Animation
        public bool EnableLoop => _enableLoop;
        public bool LoopRelative => _loopRelative;
        public Vector3 LoopPosition => _loopPosition;
        public Vector3 LoopScale => _loopScale;
        public Vector3 LoopRotation => _loopRotation;
        public float LoopDuration => _loopDuration;
        public TMPEaseType LoopEase => _loopEase;
        public bool LoopUseCustomCurve => _loopUseCustomCurve;
        public AnimationCurve LoopCustomCurve => _loopCustomCurve;
        public int LoopCount => _loopCount;
        public TMPLoopMode LoopType => _loopType;
        public float LoopToDisappearBlend => _loopToDisappearBlend;
        public bool LoopUsePositionCurve => _loopUsePositionCurve;
        public Vector2 LoopPositionCurveOffset => _loopPositionCurveOffset;

        // Disappear Animation
        public bool EnableDisappear => _enableDisappear;
        public bool DisappearRelative => _disappearRelative;
        public Vector3 DisappearPosition => _disappearPosition;
        public Vector3 DisappearScale => _disappearScale;
        public Vector3 DisappearRotation => _disappearRotation;
        public float DisappearAlpha => _disappearAlpha;
        public float DisappearDuration => _disappearDuration;
        public TMPEaseType DisappearEase => _disappearEase;
        public bool DisappearUseCustomCurve => _disappearUseCustomCurve;
        public AnimationCurve DisappearCustomCurve => _disappearCustomCurve;
        public bool DisappearUsePositionCurve => _disappearUsePositionCurve;
        public Vector2 DisappearPositionCurveOffset => _disappearPositionCurveOffset;

        // ─────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────

        private void Awake()
        {
            // CanvasGroup 자동 추가 (없으면)
            if (GetComponent<CanvasGroup>() == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            CacheComponents();

            // 깜빡임 방지: 초기 alpha = 0
            if (Application.isPlaying && _playOnEnable && _enableAppear && _canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private IEnumerator Start()
        {
            if (_playOnEnable && Application.isPlaying)
            {
                enabled = false;

                if (_initialDelay > 0f)
                {
                    yield return new WaitForSeconds(_initialDelay);
                }
                else
                {
                    yield return null;
                }

                _hasStarted = true;
                enabled = true;
            }
            else
            {
                _hasStarted = true;
            }
        }

        private void OnEnable()
        {
            CacheComponents();

            // Play 모드에서만 이벤트 등록 및 애니메이션 처리
            if (Application.isPlaying)
            {
                // CanvasGroup alpha = 0 보장 (깜빡임 방지)
                if (_canvasGroup != null && _playOnEnable && _enableAppear)
                {
                    _canvasGroup.alpha = 0f;
                }

                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

                if (_playOnEnable)
                {
                    if (!_hasStarted) return;
                    Canvas.willRenderCanvases += PlayOnce;
                }
            }
            // 에디터 모드에서는 TMP에 영향을 주지 않음
        }

        private void PlayOnce()
        {
            Canvas.willRenderCanvases -= PlayOnce;
            Play();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= PlayOnce;

            // CanvasGroup alpha = 0 (다음 활성화 시 깜빡임 방지)
            if (_canvasGroup != null && Application.isPlaying && _playOnEnable && _enableAppear)
            {
                _canvasGroup.alpha = 0f;
            }

            // Play 모드에서만 정리 작업 수행
            if (Application.isPlaying || _isPlaying)
            {
                TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
                ResetAllCharStates();
                RestoreOriginalMesh();
            }

            // 상태 초기화
            _charStates = null;
            _originalPositions = null;
            _originalVertices = null;
            _originalVerticesSecondFace = null;
            _originalVerticesInnerGlow = null;
            _originalColors = null;
            _originalColorsSecondFace = null;
            _originalColorsInnerGlow = null;
            _currentCharPos = null;
            _currentCharScale = null;
            _currentCharRot = null;
            _currentCharAlpha = null;
            _isPlaying = false;
            _isPaused = false;

            // Shadow Mesh 정리
            if (_shadowMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_shadowMesh);
                else
                    DestroyImmediate(_shadowMesh);
                _shadowMesh = null;
            }
        }

        private void Update()
        {
            if (!_isPlaying || _isPaused) return;
            if (!Application.isPlaying) return; // 에디터 모드에서는 AdvanceAnimation으로 직접 호출
            AdvanceAnimation(Time.deltaTime);
        }

        private void OnTextChanged(Object obj)
        {
            if (_isPlayingInProgress) return;
            if (obj != _tmpText) return;

            // 애니메이션 재생 중이면 재시작
            if (_isPlaying)
            {
                Restart();
                return;
            }

            // 애니메이션 재생 중이 아니어도 playOnEnable이면 재시작
            if (_playOnEnable && _hasStarted)
            {
                Play();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheComponents();

            // Loop Count가 -1(무한)이면 Disappear 자동 비활성화
            if (_enableLoop && _loopCount == -1 && _enableDisappear)
            {
                _enableDisappear = false;
            }
        }
#endif

        // ─────────────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_tmpText == null) _tmpText = GetComponent<TMP_Text>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }


        private Vector3 GetCharacterCenter(TMP_CharacterInfo charInfo)
        {
            if (!charInfo.isVisible) return Vector3.zero;

            int vertexIndex = charInfo.vertexIndex;
            int materialIndex = charInfo.materialReferenceIndex;
            Vector3[] vertices = _tmpText.textInfo.meshInfo[materialIndex].vertices;

            return new Vector3(
                (vertices[vertexIndex].x + vertices[vertexIndex + 2].x) / 2f,
                charInfo.baseLine,
                0f
            );
        }

        private void TransformCharacterVertices(int charIndex, Vector3 position,
            Vector3 scale, Vector3 rotation, float alpha)
        {
            TransformCharacterVerticesInternal(_tmpText, charIndex, position, scale, rotation, alpha, false);

            var outlineEffect = GetComponent<TMPOutlineEffect>();
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                var secondFaceText = outlineEffect.GetSecondFaceText();
                if (secondFaceText != null)
                {
                    TransformCharacterVerticesInternal(secondFaceText, charIndex, position, scale, rotation, alpha, true);
                }
            }

            // Inner Glow도 변환 (InnerGlow 전용 원본 데이터 사용)
            var glowEffect = GetComponent<TMPOutGlow>();
            if (glowEffect != null)
            {
                var innerGlowText = glowEffect.GetInnerGlowText();
                if (innerGlowText != null)
                {
                    TransformCharacterVerticesInnerGlow(innerGlowText, charIndex, position, scale, rotation, alpha);
                }
            }
        }

        private void TransformCharacterVerticesInternal(TMP_Text tmpText, int charIndex,
            Vector3 position, Vector3 scale, Vector3 rotation, float alpha, bool isSecondFace)
        {
            if (tmpText == null) return;

            var charInfo = tmpText.textInfo.characterInfo[charIndex];
            if (!charInfo.isVisible) return;

            int vertexIndex = charInfo.vertexIndex;
            int materialIndex = charInfo.materialReferenceIndex;
            Vector3[] vertices = tmpText.textInfo.meshInfo[materialIndex].vertices;
            Color32[] colors = tmpText.textInfo.meshInfo[materialIndex].colors32;

            Quaternion rot = Quaternion.Euler(rotation);

            Vector3[][] originalVertices = isSecondFace ? _originalVerticesSecondFace : _originalVertices;
            Color32[][] originalColors = isSecondFace ? _originalColorsSecondFace : _originalColors;

            if (originalVertices == null || materialIndex >= originalVertices.Length) return;
            if (originalVertices[materialIndex] == null) return;
            if (originalColors == null || materialIndex >= originalColors.Length) return;
            if (originalColors[materialIndex] == null) return;

            Vector3 center = new Vector3(
                (originalVertices[materialIndex][vertexIndex].x + originalVertices[materialIndex][vertexIndex + 2].x) / 2f,
                charInfo.baseLine,
                0f
            );

            for (int i = 0; i < 4; i++)
            {
                int idx = vertexIndex + i;
                Vector3 v = originalVertices[materialIndex][idx] - center;

                if (rotation != Vector3.zero) v = rot * v;
                v = Vector3.Scale(v, scale);
                v += position;
                vertices[idx] = v + center;

                if (idx < originalColors[materialIndex].Length)
                {
                    Color32 originalColor = originalColors[materialIndex][idx];
                    Color32 c = colors[idx];
                    c.r = originalColor.r;
                    c.g = originalColor.g;
                    c.b = originalColor.b;
                    c.a = (byte)(originalColor.a * alpha);
                    colors[idx] = c;
                }
            }
        }

        /// <summary>
        /// InnerGlow 전용 문자 정점 변환 (InnerGlow 원본 데이터 사용)
        /// </summary>
        private void TransformCharacterVerticesInnerGlow(TMP_Text tmpText, int charIndex,
            Vector3 position, Vector3 scale, Vector3 rotation, float alpha)
        {
            if (tmpText == null) return;

            var charInfo = tmpText.textInfo.characterInfo[charIndex];
            if (!charInfo.isVisible) return;

            int vertexIndex = charInfo.vertexIndex;
            int materialIndex = charInfo.materialReferenceIndex;
            Vector3[] vertices = tmpText.textInfo.meshInfo[materialIndex].vertices;
            Color32[] colors = tmpText.textInfo.meshInfo[materialIndex].colors32;

            Quaternion rot = Quaternion.Euler(rotation);

            // InnerGlow 전용 원본 데이터 사용
            if (_originalVerticesInnerGlow == null || materialIndex >= _originalVerticesInnerGlow.Length) return;
            if (_originalVerticesInnerGlow[materialIndex] == null) return;
            if (_originalColorsInnerGlow == null || materialIndex >= _originalColorsInnerGlow.Length) return;
            if (_originalColorsInnerGlow[materialIndex] == null) return;

            Vector3 center = new Vector3(
                (_originalVerticesInnerGlow[materialIndex][vertexIndex].x + _originalVerticesInnerGlow[materialIndex][vertexIndex + 2].x) / 2f,
                charInfo.baseLine,
                0f
            );

            for (int i = 0; i < 4; i++)
            {
                int idx = vertexIndex + i;
                Vector3 v = _originalVerticesInnerGlow[materialIndex][idx] - center;

                if (rotation != Vector3.zero) v = rot * v;
                v = Vector3.Scale(v, scale);
                v += position;
                vertices[idx] = v + center;

                if (idx < _originalColorsInnerGlow[materialIndex].Length)
                {
                    Color32 originalColor = _originalColorsInnerGlow[materialIndex][idx];
                    Color32 c = colors[idx];
                    c.r = originalColor.r;
                    c.g = originalColor.g;
                    c.b = originalColor.b;
                    c.a = (byte)(originalColor.a * alpha);
                    colors[idx] = c;
                }
            }
        }

        // Shadow Mesh 캐시 (GC 방지)
        private static readonly System.Collections.Generic.List<UIVertex> s_shadowVertexCache =
            new System.Collections.Generic.List<UIVertex>(512);

        // 삼각형 인덱스 (GC 방지)
        private static readonly int[] s_triangleIndices = { 0, 1, 2, 2, 3, 0 };

        // Shadow용 임시 Mesh (TMP의 원본 mesh를 보존하기 위해)
        private Mesh _shadowMesh;

        /// <summary>
        /// Shadow 메시를 CanvasRenderer에 직접 적용
        /// TMPOutlineEffect의 IMeshModifier가 UpdateVertexData() 이후에 호출되지 않으므로
        /// 직접 Shadow 정점을 추가하여 적용해야 함
        /// </summary>
        private void ApplyShadowMesh(TMP_Text tmpText, TMPOutlineEffect outlineEffect)
        {
            if (tmpText == null || outlineEffect == null) return;
            if (!outlineEffect.EnableShadow) return;

            // UI 전용 (TextMeshProUGUI만 지원)
            var tmpUGUI = tmpText as TextMeshProUGUI;
            if (tmpUGUI == null) return;

            var canvasRenderer = tmpUGUI.canvasRenderer;
            if (canvasRenderer == null) return;

            // 현재 메시 정보 가져오기
            var meshInfo = tmpText.textInfo.meshInfo;
            if (meshInfo == null || meshInfo.Length == 0) return;

            // Shadow 설정 가져오기
            Vector2 shadowOffset = outlineEffect.ShadowOffset;
            float shadowAlpha = outlineEffect.ShadowAlpha;
            Color underlayColor = outlineEffect.UnderlayColor;
            float fontSize = tmpText.fontSize;

            // Shadow 색상 계산
            Color32 shadowColor = new Color32(
                (byte)(underlayColor.r * 255f),
                (byte)(underlayColor.g * 255f),
                (byte)(underlayColor.b * 255f),
                (byte)(underlayColor.a * shadowAlpha * 255f)
            );

            // 정점 캐시 초기화
            s_shadowVertexCache.Clear();

            // 각 materialIndex의 메시 처리
            for (int m = 0; m < meshInfo.Length; m++)
            {
                var info = meshInfo[m];
                if (info.vertices == null || info.vertices.Length == 0) continue;

                int vertexCount = info.vertexCount;
                if (vertexCount == 0) continue;

                // Quad(4 정점) → Triangle(6 정점) 변환
                // TMP는 각 글자를 4개의 정점(Quad)으로 표현
                // 삼각형 인덱스 순서: 0, 1, 2, 2, 3, 0
                int quadCount = vertexCount / 4;

                for (int q = 0; q < quadCount; q++)
                {
                    int baseIdx = q * 4;

                    // Shadow 삼각형 (먼저 그려짐) - 메인 텍스트의 알파를 Shadow에 적용
                    AddQuadAsTriangles(s_shadowVertexCache, info, baseIdx, shadowOffset * fontSize, shadowColor, true);

                    // 원본 삼각형 (나중에 그려짐)
                    AddQuadAsTriangles(s_shadowVertexCache, info, baseIdx, Vector2.zero, default, false);
                }
            }

            // CanvasRenderer에 메시 설정
            if (s_shadowVertexCache.Count > 0)
            {
                // 임시 Mesh 생성 (TMP의 원본 mesh를 보존)
                if (_shadowMesh == null)
                {
                    _shadowMesh = new Mesh();
                    _shadowMesh.name = "TMPAnimation_Shadow";
                    _shadowMesh.hideFlags = HideFlags.DontSave;
                }

                // VertexHelper를 사용하여 메시 생성
                using (var vh = new VertexHelper())
                {
                    vh.AddUIVertexTriangleStream(s_shadowVertexCache);
                    vh.FillMesh(_shadowMesh);
                    canvasRenderer.SetMesh(_shadowMesh);
                }
            }
        }

        /// <summary>
        /// Quad(4 정점)를 2개의 삼각형(6 정점)으로 변환하여 리스트에 추가
        /// </summary>
        /// <param name="applyMainAlphaToShadow">true이면 메인 텍스트의 알파값을 Shadow에 곱함</param>
        private void AddQuadAsTriangles(System.Collections.Generic.List<UIVertex> list,
            TMP_MeshInfo meshInfo, int baseIdx, Vector2 offset, Color32 colorOverride, bool applyMainAlphaToShadow)
        {
            // 범위 검사: 4개의 정점이 모두 유효해야 함
            if (baseIdx + 3 >= meshInfo.vertices.Length) return;
            if (meshInfo.colors32 == null || baseIdx + 3 >= meshInfo.colors32.Length) return;
            if (meshInfo.uvs0 == null || baseIdx + 3 >= meshInfo.uvs0.Length) return;

            // 삼각형 인덱스 순서: 0, 1, 2, 2, 3, 0 (static 재사용)

            bool useColorOverride = colorOverride.a > 0;

            for (int i = 0; i < 6; i++)
            {
                int idx = baseIdx + s_triangleIndices[i];

                Color32 finalColor;
                if (useColorOverride)
                {
                    if (applyMainAlphaToShadow)
                    {
                        // 메인 텍스트의 현재 알파값을 Shadow 알파에 곱함
                        float mainAlphaNormalized = meshInfo.colors32[idx].a / 255f;
                        finalColor = new Color32(
                            colorOverride.r,
                            colorOverride.g,
                            colorOverride.b,
                            (byte)(colorOverride.a * mainAlphaNormalized)
                        );
                    }
                    else
                    {
                        finalColor = colorOverride;
                    }
                }
                else
                {
                    finalColor = meshInfo.colors32[idx];
                }

                // UIVertex 구조체 직접 초기화 (GC Alloc 방지)
                UIVertex vertex;
                vertex.position = meshInfo.vertices[idx] + new Vector3(offset.x, offset.y, 0);
                vertex.color = finalColor;
                vertex.uv0 = meshInfo.uvs0[idx];
                vertex.uv1 = meshInfo.uvs2 != null && idx < meshInfo.uvs2.Length ? meshInfo.uvs2[idx] : Vector2.zero;
                vertex.uv2 = Vector4.zero;
                vertex.uv3 = Vector4.zero;
                vertex.normal = Vector3.back;
                vertex.tangent = Vector4.zero;

                list.Add(vertex);
            }
        }

        // ─────────────────────────────────────────────
        // Update 기반 애니메이션 시스템
        // ─────────────────────────────────────────────

        /// <summary>
        /// 프레임 단위 애니메이션 진행 (에디터에서도 호출 가능)
        /// </summary>
        public void AdvanceAnimation(float deltaTime)
        {
            if (_charStates == null) return;

            bool anyActive = false;
            for (int i = 0; i < _charStates.Length; i++)
            {
                if (!_charStates[i].isActive) continue;
                _charStates[i].elapsedTime += deltaTime;
                EvaluateCharacter(i);
                anyActive = true;
            }

            if (anyActive)
            {
                UpdateAllVertexData();

                // Shadow 처리 (프레임당 1회)
                var outlineEffect = GetComponent<TMPOutlineEffect>();
                if (outlineEffect != null && outlineEffect.EnableShadow)
                {
                    ApplyShadowMesh(_tmpText, outlineEffect);
                }
            }
            else if (_isPlaying)
            {
                _isPlaying = false;
            }
        }

        /// <summary>
        /// 개별 글자의 현재 시간에 따른 애니메이션 상태를 계산하고 정점에 적용합니다.
        /// </summary>
        private void EvaluateCharacter(int index)
        {
            ref CharAnimState state = ref _charStates[index];

            // delay 이전 시간 계산
            float time = state.elapsedTime - state.delay;
            if (time < 0f)
            {
                // 아직 시작 안 됨 — Appear 시작 상태 유지
                if (state.appearEnd > 0f) // appear가 활성화된 경우
                {
                    ApplyCharState(index, state.appearFromPos, state.appearFromScale, state.appearFromRot, state.appearFromAlpha);
                }
                return;
            }

            // ── Appear 구간 ──
            if (state.appearEnd > 0f && time < state.appearEnd)
            {
                if (time >= state.appearStart)
                {
                    float duration = state.appearEnd - state.appearStart;
                    float rawT = duration > 0f ? Mathf.Clamp01((time - state.appearStart) / duration) : 1f;
                    float easedT = EvaluateEasing(rawT, state.appearUseCustomCurve, state.appearCustomCurve, state.appearEase);

                    InterpolateAndApply(index,
                        state.appearFromPos, state.appearToPos,
                        state.appearFromScale, state.appearToScale,
                        state.appearFromRot, state.appearToRot,
                        state.appearFromAlpha, state.appearToAlpha,
                        easedT, state.appearUsePosCurve, state.appearPosCurveOffset,
                        false, false);
                }
                else
                {
                    ApplyCharState(index, state.appearFromPos, state.appearFromScale, state.appearFromRot, state.appearFromAlpha);
                }

                // Appear 구간 내 Loop 블렌딩 (오버랩 구간 체크)
                // Loop는 loopStart에서 시작하므로 Appear 끝나기 전에 Loop가 시작될 수 있음
                if (state.loopEnabled && state.appearToLoopBlendActive && time >= state.loopStart)
                {
                    EvaluateLoop(index, ref state, time);
                }
                return;
            }

            // Appear 종료 후 → Loop 또는 Disappear 또는 완료
            // ── Loop 구간 ──
            if (state.loopEnabled && time >= state.loopStart)
            {
                // Loop 시작 시 캡처 (블렌딩용)
                if (!state.hasLoopCaptured && state.appearToLoopBlendActive)
                {
                    CaptureLoopFrom(index, ref state);
                    state.hasLoopCaptured = true;
                }

                // Disappear 블렌딩 체크: Loop 내에서 Disappear가 겹칠 수 있음
                if (state.disappearEnabled && time >= state.disappearStart)
                {
                    EvaluateDisappear(index, ref state, time);
                    return;
                }

                EvaluateLoop(index, ref state, time);

                // 유한 루프: 종료 여부 확인
                if (!state.isInfiniteLoop)
                {
                    float loopTotalDuration = CalculateLoopTotalDuration(ref state);
                    float loopEndTime = state.loopStart + loopTotalDuration;
                    if (time >= loopEndTime && !state.disappearEnabled)
                    {
                        // 루프 완료, Disappear 없음 → 최종 상태 적용 및 비활성화
                        ApplyLoopFinalState(index, ref state);
                        state.isActive = false;
                    }
                }
                return;
            }

            // ── Disappear 구간 ── (Loop 없이 직접 도달)
            if (state.disappearEnabled && time >= state.disappearStart)
            {
                EvaluateDisappear(index, ref state, time);
                return;
            }

            // 모든 구간 종료 — 최종 상태 적용
            if (!state.isInfiniteLoop)
            {
                state.isActive = false;
            }
        }

        /// <summary>
        /// Loop 구간의 애니메이션 평가
        /// </summary>
        private void EvaluateLoop(int index, ref CharAnimState state, float time)
        {
            float loopTime = time - state.loopStart;
            if (loopTime < 0f) return;

            float singleDuration = state.loopSingleDuration;
            if (singleDuration <= 0f) return;

            // Yoyo의 경우: 한 사이클 = forward + backward = singleDuration * 2
            // Restart의 경우: 한 사이클 = singleDuration
            float cycleDuration;
            if (state.loopMode == TMPLoopMode.Yoyo)
            {
                cycleDuration = singleDuration * 2f;
            }
            else
            {
                cycleDuration = singleDuration;
            }

            // 유한 루프 완료 여부
            if (!state.isInfiniteLoop && state.loopCount > 0)
            {
                float totalLoopTime = cycleDuration * state.loopCount;
                if (loopTime >= totalLoopTime)
                {
                    loopTime = totalLoopTime - 0.0001f; // 마지막 프레임 값 고정
                }
            }

            // 현재 사이클 내 위치
            float cycleTime = loopTime % cycleDuration;

            float rawT;
            Vector3 fromPos, toPos, fromScale, toScale, fromRot, toRot;
            float fromAlpha, toAlpha;
            bool usePosCurve = state.loopUsePosCurve;
            Vector2 posCurveOffset = state.loopPosCurveOffset;

            if (state.loopMode == TMPLoopMode.Yoyo)
            {
                if (cycleTime < singleDuration)
                {
                    // Forward
                    rawT = cycleTime / singleDuration;
                    fromPos = state.loopFromPos;
                    toPos = state.loopToPos;
                    fromScale = state.loopFromScale;
                    toScale = state.loopToScale;
                    fromRot = state.loopFromRot;
                    toRot = state.loopToRot;
                    fromAlpha = state.loopFromAlpha;
                    toAlpha = state.loopToAlpha;
                }
                else
                {
                    // Backward
                    rawT = (cycleTime - singleDuration) / singleDuration;
                    fromPos = state.loopToPos;
                    toPos = state.loopFromPos;
                    fromScale = state.loopToScale;
                    toScale = state.loopFromScale;
                    fromRot = state.loopToRot;
                    toRot = state.loopFromRot;
                    fromAlpha = state.loopToAlpha;
                    toAlpha = state.loopFromAlpha;
                }
            }
            else
            {
                // Restart
                rawT = cycleTime / singleDuration;
                fromPos = state.loopFromPos;
                toPos = state.loopToPos;
                fromScale = state.loopFromScale;
                toScale = state.loopToScale;
                fromRot = state.loopFromRot;
                toRot = state.loopToRot;
                fromAlpha = state.loopFromAlpha;
                toAlpha = state.loopToAlpha;
            }

            rawT = Mathf.Clamp01(rawT);
            float easedT = EvaluateEasing(rawT, state.loopUseCustomCurve, state.loopCustomCurve, state.loopEase);

            // 첫 Loop의 첫 forward에서 블렌딩 적용 (캡처된 상태에서 시작)
            bool isFirstForward = (loopTime < singleDuration) && state.appearToLoopBlendActive && state.hasLoopCaptured;

            InterpolateAndApply(index,
                fromPos, toPos,
                fromScale, toScale,
                fromRot, toRot,
                fromAlpha, toAlpha,
                easedT, usePosCurve, posCurveOffset,
                isFirstForward, false);
        }

        /// <summary>
        /// Disappear 구간의 애니메이션 평가
        /// </summary>
        private void EvaluateDisappear(int index, ref CharAnimState state, float time)
        {
            // 첫 프레임 캡처 (블렌딩용)
            if (!state.hasDisappearCaptured && state.loopToDisappearBlendActive)
            {
                CaptureDisappearFrom(index, ref state);
                state.hasDisappearCaptured = true;
            }

            float duration = state.disappearEnd - state.disappearStart;
            if (time >= state.disappearEnd)
            {
                // Disappear 완료 — 최종 상태 적용
                ApplyCharState(index, state.disappearToPos, state.disappearToScale, state.disappearToRot, state.disappearToAlpha);
                state.isActive = false;
                return;
            }

            float rawT = duration > 0f ? Mathf.Clamp01((time - state.disappearStart) / duration) : 1f;
            float easedT = EvaluateEasing(rawT, state.disappearUseCustomCurve, state.disappearCustomCurve, state.disappearEase);

            bool useBlend = state.loopToDisappearBlendActive && state.hasDisappearCaptured;

            InterpolateAndApply(index,
                state.disappearFromPos, state.disappearToPos,
                state.disappearFromScale, state.disappearToScale,
                state.disappearFromRot, state.disappearToRot,
                state.disappearFromAlpha, state.disappearToAlpha,
                easedT, state.disappearUsePosCurve, state.disappearPosCurveOffset,
                useBlend, false);
        }

        /// <summary>
        /// Loop 전체 소요 시간 계산 (유한 루프)
        /// </summary>
        private float CalculateLoopTotalDuration(ref CharAnimState state)
        {
            float cycleDuration = state.loopMode == TMPLoopMode.Yoyo
                ? state.loopSingleDuration * 2f
                : state.loopSingleDuration;
            return cycleDuration * state.loopCount;
        }

        /// <summary>
        /// Loop 최종 상태 적용 (유한 루프 완료 시)
        /// </summary>
        private void ApplyLoopFinalState(int index, ref CharAnimState state)
        {
            // Yoyo: 원래 위치로 돌아옴, Restart: toPos에서 끝남
            if (state.loopMode == TMPLoopMode.Yoyo)
            {
                ApplyCharState(index, state.loopFromPos, state.loopFromScale, state.loopFromRot, state.loopFromAlpha);
            }
            else
            {
                ApplyCharState(index, state.loopToPos, state.loopToScale, state.loopToRot, state.loopToAlpha);
            }
        }

        /// <summary>
        /// Loop 시작 시 현재 상태를 loopFrom으로 캡처 (Appear→Loop 블렌딩)
        /// </summary>
        private void CaptureLoopFrom(int index, ref CharAnimState state)
        {
            if (_currentCharPos != null && index < _currentCharPos.Length)
            {
                state.loopFromPos = _currentCharPos[index];
                state.loopFromScale = _currentCharScale[index];
                state.loopFromRot = _currentCharRot[index];
                state.loopFromAlpha = _currentCharAlpha[index];
            }
        }

        /// <summary>
        /// Disappear 시작 시 현재 상태를 disappearFrom으로 캡처 (Loop→Disappear 블렌딩)
        /// </summary>
        private void CaptureDisappearFrom(int index, ref CharAnimState state)
        {
            if (_currentCharPos != null && index < _currentCharPos.Length)
            {
                state.disappearFromPos = _currentCharPos[index];
                state.disappearFromScale = _currentCharScale[index];
                state.disappearFromRot = _currentCharRot[index];
                state.disappearFromAlpha = _currentCharAlpha[index];
            }
        }

        /// <summary>
        /// 이징 값 평가 (커스텀 곡선 또는 TMPEasing)
        /// </summary>
        private static float EvaluateEasing(float rawT, bool useCustomCurve, AnimationCurve customCurve, TMPEaseType ease)
        {
            if (useCustomCurve && customCurve != null)
            {
                return customCurve.Evaluate(rawT);
            }
            return TMPEasing.Evaluate(ease, rawT);
        }

        /// <summary>
        /// 보간 + 정점 적용 (블렌딩 캡처 지원)
        /// </summary>
        private void InterpolateAndApply(int index,
            Vector3 fromPos, Vector3 toPos,
            Vector3 fromScale, Vector3 toScale,
            Vector3 fromRot, Vector3 toRot,
            float fromAlpha, float toAlpha,
            float easedT, bool usePositionCurve, Vector2 positionCurveOffset,
            bool useCurrentAsFrom, bool _unused)
        {
            // 블렌딩 캡처: 현재 상태에서 시작
            Vector3 actualFromPos = fromPos;
            Vector3 actualFromScale = fromScale;
            Vector3 actualFromRot = fromRot;
            float actualFromAlpha = fromAlpha;

            if (useCurrentAsFrom && _currentCharPos != null && index < _currentCharPos.Length)
            {
                actualFromPos = _currentCharPos[index];
                actualFromScale = _currentCharScale[index];
                actualFromRot = _currentCharRot[index];
                actualFromAlpha = _currentCharAlpha[index];
            }

            // Position 커브 적용: Quadratic Bezier Curve (시작점→중간점→도착점)
            Vector3 currentPos;
            if (usePositionCurve)
            {
                // 중간 제어점 계산: 시작점과 도착점의 중간 + 오프셋
                Vector3 midPoint = (actualFromPos + toPos) * 0.5f;
                midPoint.x += positionCurveOffset.x;
                midPoint.y += positionCurveOffset.y;

                // Quadratic Bezier: P(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
                float oneMinusT = 1f - easedT;
                float posX = oneMinusT * oneMinusT * actualFromPos.x
                           + 2f * oneMinusT * easedT * midPoint.x
                           + easedT * easedT * toPos.x;
                float posY = oneMinusT * oneMinusT * actualFromPos.y
                           + 2f * oneMinusT * easedT * midPoint.y
                           + easedT * easedT * toPos.y;
                float posZ = Mathf.Lerp(actualFromPos.z, toPos.z, easedT);
                currentPos = new Vector3(posX, posY, posZ);
            }
            else
            {
                currentPos = Vector3.Lerp(actualFromPos, toPos, easedT);
            }

            // Scale, Rotation, Alpha 보간
            Vector3 currentScale = Vector3.Lerp(actualFromScale, toScale, easedT);
            Vector3 currentRot = Vector3.Lerp(actualFromRot, toRot, easedT);
            float currentAlpha = Mathf.Lerp(actualFromAlpha, toAlpha, easedT);

            ApplyCharState(index, currentPos, currentScale, currentRot, currentAlpha);
        }

        /// <summary>
        /// 글자의 현재 상태를 설정하고 정점 변환 적용
        /// </summary>
        private void ApplyCharState(int index, Vector3 pos, Vector3 scale, Vector3 rot, float alpha)
        {
            // 현재 상태 업데이트 (다음 애니메이션 블렌딩용)
            if (_currentCharPos != null && index < _currentCharPos.Length)
            {
                _currentCharPos[index] = pos;
                _currentCharScale[index] = scale;
                _currentCharRot[index] = rot;
                _currentCharAlpha[index] = alpha;
            }

            TransformCharacterVertices(index, pos, scale, rot, alpha);
        }

        /// <summary>
        /// 글자별 애니메이션 상태 초기화 (DOTween CreateCharacterSequence 대체)
        /// </summary>
        private CharAnimState InitCharacterState(int charIndex)
        {
            CharAnimState state = default;
            state.isActive = true;
            state.elapsedTime = 0f;
            state.delay = charIndex * _characterDelay;

            Vector3 originalPos = _originalPositions[charIndex];
            Vector3 originalScale = Vector3.one;
            Vector3 originalRot = Vector3.zero;
            float originalAlpha = 1f;

            // 시간 추적
            float currentTime = 0f;

            // ── Appear ──
            if (_enableAppear)
            {
                Vector3 appearFromPos, appearToPos, appearFromScale, appearToScale, appearFromRot, appearToRot;
                float appearFromAlpha, appearToAlpha;

                CalculateFromTo(true, _appearRelative,
                    _appearPosition, originalPos,
                    _appearScale, originalScale,
                    _appearRotation, originalRot,
                    _appearAlpha, originalAlpha,
                    out appearFromPos, out appearToPos,
                    out appearFromScale, out appearToScale,
                    out appearFromRot, out appearToRot,
                    out appearFromAlpha, out appearToAlpha);

                state.appearStart = currentTime;
                state.appearEnd = currentTime + _appearDuration;
                state.appearFromPos = appearFromPos;
                state.appearToPos = appearToPos;
                state.appearFromScale = appearFromScale;
                state.appearToScale = appearToScale;
                state.appearFromRot = appearFromRot;
                state.appearToRot = appearToRot;
                state.appearFromAlpha = appearFromAlpha;
                state.appearToAlpha = appearToAlpha;
                state.appearUsePosCurve = _appearUsePositionCurve;
                state.appearPosCurveOffset = _appearPositionCurveOffset;
                state.appearUseCustomCurve = _appearUseCustomCurve;
                state.appearCustomCurve = _appearUseCustomCurve ? _appearCustomCurve : null;
                state.appearEase = _appearEase;

                // 다음 애니메이션 시작 시간 (블렌딩 적용)
                float appearBlendTime = _appearDuration * _appearToLoopBlend;
                currentTime += _appearDuration - appearBlendTime;
                if (currentTime < 0f) currentTime = 0f;
            }

            // ── Loop ──
            state.loopEnabled = _enableLoop && _loopCount != 0;
            if (state.loopEnabled)
            {
                Vector3 loopFromPos, loopToPos, loopFromScale, loopToScale, loopFromRot, loopToRot;
                float loopFromAlpha, loopToAlpha;

                CalculateFromTo(false, _loopRelative,
                    _loopPosition, originalPos,
                    _loopScale, originalScale,
                    _loopRotation, originalRot,
                    1f, originalAlpha,
                    out loopFromPos, out loopToPos,
                    out loopFromScale, out loopToScale,
                    out loopFromRot, out loopToRot,
                    out loopFromAlpha, out loopToAlpha);

                state.loopStart = currentTime;
                state.loopSingleDuration = _loopDuration;
                state.loopCount = _loopCount;
                state.loopMode = _loopType;
                state.loopFromPos = loopFromPos;
                state.loopToPos = loopToPos;
                state.loopFromScale = loopFromScale;
                state.loopToScale = loopToScale;
                state.loopFromRot = loopFromRot;
                state.loopToRot = loopToRot;
                state.loopFromAlpha = loopFromAlpha;
                state.loopToAlpha = loopToAlpha;
                state.loopUsePosCurve = _loopUsePositionCurve;
                state.loopPosCurveOffset = _loopPositionCurveOffset;
                state.loopUseCustomCurve = _loopUseCustomCurve;
                state.loopCustomCurve = _loopUseCustomCurve ? _loopCustomCurve : null;
                state.loopEase = _loopEase;

                // 블렌딩 플래그
                state.appearToLoopBlendActive = _enableAppear && _appearToLoopBlend > 0f;
                state.hasLoopCaptured = false;

                // 무한 루프 여부
                state.isInfiniteLoop = (_loopCount == -1);

                if (state.isInfiniteLoop)
                {
                    // 무한 루프: Disappear 없음, 종료 시간 없음
                    state.disappearEnabled = false;
                    state.totalDuration = float.MaxValue;
                    return state;
                }

                // 유한 루프: 시간 진행
                float cycleDuration = _loopType == TMPLoopMode.Yoyo
                    ? _loopDuration * 2f
                    : _loopDuration;
                currentTime += cycleDuration * _loopCount;

                // Loop → Disappear 블렌딩 적용 (비율 기반)
                float loopBlendTime = _loopDuration * _loopToDisappearBlend;
                currentTime -= loopBlendTime;
                if (currentTime < 0f) currentTime = 0f;
            }

            // ── Disappear ──
            state.disappearEnabled = _enableDisappear;
            if (_enableDisappear)
            {
                Vector3 disappearFromPos, disappearToPos, disappearFromScale, disappearToScale, disappearFromRot, disappearToRot;
                float disappearFromAlpha, disappearToAlpha;

                CalculateFromTo(false, _disappearRelative,
                    _disappearPosition, originalPos,
                    _disappearScale, originalScale,
                    _disappearRotation, originalRot,
                    _disappearAlpha, originalAlpha,
                    out disappearFromPos, out disappearToPos,
                    out disappearFromScale, out disappearToScale,
                    out disappearFromRot, out disappearToRot,
                    out disappearFromAlpha, out disappearToAlpha);

                state.disappearStart = currentTime;
                state.disappearEnd = currentTime + _disappearDuration;
                state.disappearFromPos = disappearFromPos;
                state.disappearToPos = disappearToPos;
                state.disappearFromScale = disappearFromScale;
                state.disappearToScale = disappearToScale;
                state.disappearFromRot = disappearFromRot;
                state.disappearToRot = disappearToRot;
                state.disappearFromAlpha = disappearFromAlpha;
                state.disappearToAlpha = disappearToAlpha;
                state.disappearUsePosCurve = _disappearUsePositionCurve;
                state.disappearPosCurveOffset = _disappearPositionCurveOffset;
                state.disappearUseCustomCurve = _disappearUseCustomCurve;
                state.disappearCustomCurve = _disappearUseCustomCurve ? _disappearCustomCurve : null;
                state.disappearEase = _disappearEase;

                // 블렌딩 플래그
                state.loopToDisappearBlendActive = (state.loopEnabled && _loopToDisappearBlend > 0f) ||
                                                   (_enableAppear && !state.loopEnabled && _appearToLoopBlend > 0f);
                state.hasDisappearCaptured = false;

                state.totalDuration = state.disappearEnd;
            }
            else
            {
                state.totalDuration = currentTime;
            }

            return state;
        }

        private void CalculateFromTo(bool from, bool relative,
            Vector3 inputPos, Vector3 originalPos,
            Vector3 inputScale, Vector3 originalScale,
            Vector3 inputRot, Vector3 originalRot,
            float inputAlpha, float originalAlpha,
            out Vector3 fromPos, out Vector3 toPos,
            out Vector3 fromScale, out Vector3 toScale,
            out Vector3 fromRot, out Vector3 toRot,
            out float fromAlpha, out float toAlpha)
        {
            if (from)
            {
                if (relative)
                {
                    fromPos = inputPos;
                    fromScale = Vector3.Scale(originalScale, inputScale);
                    fromRot = originalRot + inputRot;
                }
                else
                {
                    fromPos = inputPos - originalPos;
                    fromScale = inputScale;
                    fromRot = inputRot;
                }
                fromAlpha = inputAlpha;

                toPos = Vector3.zero;
                toScale = originalScale;
                toRot = originalRot;
                toAlpha = originalAlpha;
            }
            else
            {
                fromPos = Vector3.zero;
                fromScale = originalScale;
                fromRot = originalRot;
                fromAlpha = originalAlpha;

                if (relative)
                {
                    toPos = inputPos;
                    toScale = Vector3.Scale(originalScale, inputScale);
                    toRot = originalRot + inputRot;
                }
                else
                {
                    toPos = inputPos - originalPos;
                    toScale = inputScale;
                    toRot = inputRot;
                }
                toAlpha = inputAlpha;
            }
        }

        private void UpdateAllVertexData()
        {
            _tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

            var outlineEffect = GetComponent<TMPOutlineEffect>();
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                var secondFaceText = outlineEffect.GetSecondFaceText();
                if (secondFaceText != null)
                {
                    secondFaceText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                }
            }

            // Inner Glow도 업데이트
            var glowEffect = GetComponent<TMPOutGlow>();
            if (glowEffect != null)
            {
                var innerGlowText = glowEffect.GetInnerGlowText();
                if (innerGlowText != null)
                {
                    innerGlowText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                }
            }
        }

        /// <summary>
        /// 모든 글자 애니메이션 상태 초기화 (DOTween KillAllSequences 대체)
        /// </summary>
        private void ResetAllCharStates()
        {
            _charStates = null;
        }

        private void RestoreOriginalMesh()
        {
            if (_tmpText == null) return;

            // 원본 정점과 색상 복원
            if (_originalVertices != null && _originalColors != null)
            {
                for (int i = 0; i < _tmpText.textInfo.meshInfo.Length; i++)
                {
                    if (i < _originalVertices.Length && _originalVertices[i] != null)
                    {
                        var vertices = _tmpText.textInfo.meshInfo[i].vertices;
                        for (int j = 0; j < vertices.Length && j < _originalVertices[i].Length; j++)
                        {
                            vertices[j] = _originalVertices[i][j];
                        }
                    }

                    if (i < _originalColors.Length && _originalColors[i] != null)
                    {
                        var colors = _tmpText.textInfo.meshInfo[i].colors32;
                        for (int j = 0; j < colors.Length && j < _originalColors[i].Length; j++)
                        {
                            colors[j] = _originalColors[i][j];
                        }
                    }
                }

                _tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            }

            // CanvasGroup alpha 리셋 (Editor 테스트 후 잔상 방지)
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            // TMP의 원본 렌더링 복원
            var tmpUGUI = _tmpText as TextMeshProUGUI;
            if (tmpUGUI != null)
            {
                var canvasRenderer = tmpUGUI.canvasRenderer;
                if (canvasRenderer != null && tmpUGUI.mesh != null)
                {
                    canvasRenderer.SetMesh(tmpUGUI.mesh);
                }
            }

            // Second Face도 복원
            var outlineEffect = GetComponent<TMPOutlineEffect>();
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                var secondFaceText = outlineEffect.GetSecondFaceText();
                if (secondFaceText != null)
                {
                    if (_originalVerticesSecondFace != null && _originalColorsSecondFace != null)
                    {
                        for (int i = 0; i < secondFaceText.textInfo.meshInfo.Length; i++)
                        {
                            if (i < _originalVerticesSecondFace.Length && _originalVerticesSecondFace[i] != null)
                            {
                                var vertices = secondFaceText.textInfo.meshInfo[i].vertices;
                                for (int j = 0; j < vertices.Length && j < _originalVerticesSecondFace[i].Length; j++)
                                {
                                    vertices[j] = _originalVerticesSecondFace[i][j];
                                }
                            }

                            if (i < _originalColorsSecondFace.Length && _originalColorsSecondFace[i] != null)
                            {
                                var colors = secondFaceText.textInfo.meshInfo[i].colors32;
                                for (int j = 0; j < colors.Length && j < _originalColorsSecondFace[i].Length; j++)
                                {
                                    colors[j] = _originalColorsSecondFace[i][j];
                                }
                            }
                        }

                        secondFaceText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                    }

                    var secondFaceUGUI = secondFaceText as TextMeshProUGUI;
                    if (secondFaceUGUI != null)
                    {
                        var canvasRenderer = secondFaceUGUI.canvasRenderer;
                        if (canvasRenderer != null && secondFaceUGUI.mesh != null)
                        {
                            canvasRenderer.SetMesh(secondFaceUGUI.mesh);
                        }

                        // SecondFace의 TMPAnimation도 Stop (자식 애니메이션 정지)
                        TMPAnimation secondFaceAnimation = secondFaceUGUI.GetComponent<TMPAnimation>();
                        if (secondFaceAnimation != null)
                        {
                            secondFaceAnimation.Stop();
                        }

                        // SecondFace의 CanvasGroup alpha도 리셋
                        CanvasGroup secondFaceCanvasGroup = secondFaceUGUI.GetComponent<CanvasGroup>();
                        if (secondFaceCanvasGroup != null)
                        {
                            secondFaceCanvasGroup.alpha = 1f;
                        }
                    }
                }
            }

            // InnerGlow도 복원 (TMPOutGlow 컴포넌트가 있는 경우)
            var glowEffect = GetComponent<TMPOutGlow>();
            if (glowEffect != null)
            {
                var innerGlowText = glowEffect.GetInnerGlowText();
                if (innerGlowText != null)
                {
                    // InnerGlow 원본 정점과 색상 복원
                    if (_originalVerticesInnerGlow != null && _originalColorsInnerGlow != null)
                    {
                        for (int i = 0; i < innerGlowText.textInfo.meshInfo.Length; i++)
                        {
                            if (i < _originalVerticesInnerGlow.Length && _originalVerticesInnerGlow[i] != null)
                            {
                                var vertices = innerGlowText.textInfo.meshInfo[i].vertices;
                                for (int j = 0; j < vertices.Length && j < _originalVerticesInnerGlow[i].Length; j++)
                                {
                                    vertices[j] = _originalVerticesInnerGlow[i][j];
                                }
                            }

                            if (i < _originalColorsInnerGlow.Length && _originalColorsInnerGlow[i] != null)
                            {
                                var colors = innerGlowText.textInfo.meshInfo[i].colors32;
                                for (int j = 0; j < colors.Length && j < _originalColorsInnerGlow[i].Length; j++)
                                {
                                    colors[j] = _originalColorsInnerGlow[i][j];
                                }
                            }
                        }

                        innerGlowText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                    }

                    var innerGlowUGUI = innerGlowText as TextMeshProUGUI;
                    if (innerGlowUGUI != null)
                    {
                        var canvasRenderer = innerGlowUGUI.canvasRenderer;
                        if (canvasRenderer != null && innerGlowUGUI.mesh != null)
                        {
                            canvasRenderer.SetMesh(innerGlowUGUI.mesh);
                        }

                        // InnerGlow의 TMPAnimation도 Stop (자식 애니메이션 정지)
                        TMPAnimation innerGlowAnimation = innerGlowUGUI.GetComponent<TMPAnimation>();
                        if (innerGlowAnimation != null)
                        {
                            innerGlowAnimation.Stop();
                        }

                        // InnerGlow의 CanvasGroup alpha 리셋
                        CanvasGroup innerGlowCanvasGroup = innerGlowUGUI.GetComponent<CanvasGroup>();
                        if (innerGlowCanvasGroup != null)
                        {
                            innerGlowCanvasGroup.alpha = 1f;
                        }
                    }

                    // InnerGlow 메시를 강제로 동기화 및 업데이트 (Editor 테스트 후 완전 복원)
                    glowEffect.ForceUpdateInnerGlow();
                }
            }
        }

        // ─────────────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────────────

        public void Play()
        {
            if (_tmpText == null) return;

            if (_isPlayingInProgress)
            {
                return;
            }

            _isPlayingInProgress = true;

            ResetAllCharStates();

            _tmpText.SetVerticesDirty();
            _tmpText.ForceMeshUpdate();

            var outlineEffect = GetComponent<TMPOutlineEffect>();
            TMP_Text secondFaceText = null;
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                // Second Face 강제 동기화 (텍스트 변경 시 즉시 반영)
                outlineEffect.ForceSyncSecondFace();

                secondFaceText = outlineEffect.GetSecondFaceText();
                if (secondFaceText != null)
                {
                    // 추가 안전장치: 텍스트 내용 재확인 및 동기화
                    if (secondFaceText.text != _tmpText.text)
                    {
                        secondFaceText.text = _tmpText.text;
                    }
                    secondFaceText.SetVerticesDirty();
                    secondFaceText.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();

                }
            }

            // Inner Glow도 동기화 (TMPOutGlow가 있는 경우)
            var glowEffect = GetComponent<TMPOutGlow>();
            TMP_Text innerGlowText = null;
            if (glowEffect != null)
            {
                innerGlowText = glowEffect.GetInnerGlowText();
                if (innerGlowText != null)
                {
                    // 텍스트 내용 동기화
                    if (innerGlowText.text != _tmpText.text)
                    {
                        innerGlowText.text = _tmpText.text;
                    }
                    innerGlowText.SetVerticesDirty();
                    innerGlowText.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();
                }
            }

            int charCount = _tmpText.textInfo.characterCount;
            if (charCount == 0)
            {
                _isPlayingInProgress = false;
                return;
            }

            _isPlaying = true;
            _isPaused = false;

            _charStates = new CharAnimState[charCount];
            _originalPositions = new Vector3[charCount];

            // 현재 상태 배열 초기화 (블렌딩용)
            _currentCharPos = new Vector3[charCount];
            _currentCharScale = new Vector3[charCount];
            _currentCharRot = new Vector3[charCount];
            _currentCharAlpha = new float[charCount];
            for (int i = 0; i < charCount; i++)
            {
                _currentCharPos[i] = Vector3.zero;
                _currentCharScale[i] = Vector3.one;
                _currentCharRot[i] = Vector3.zero;
                _currentCharAlpha[i] = 1f;
            }

            _originalVertices = new Vector3[_tmpText.textInfo.meshInfo.Length][];
            for (int i = 0; i < _tmpText.textInfo.meshInfo.Length; i++)
            {
                Vector3[] vertices = _tmpText.textInfo.meshInfo[i].vertices;
                _originalVertices[i] = new Vector3[vertices.Length];
                for (int j = 0; j < vertices.Length; j++)
                {
                    _originalVertices[i][j] = vertices[j];
                }
            }

            _originalColors = new Color32[_tmpText.textInfo.meshInfo.Length][];
            for (int i = 0; i < _tmpText.textInfo.meshInfo.Length; i++)
            {
                Color32[] colors = _tmpText.textInfo.meshInfo[i].colors32;
                _originalColors[i] = new Color32[colors.Length];
                for (int j = 0; j < colors.Length; j++)
                {
                    _originalColors[i][j] = colors[j];
                }
            }

            if (secondFaceText != null)
            {
                _originalVerticesSecondFace = new Vector3[secondFaceText.textInfo.meshInfo.Length][];
                for (int i = 0; i < secondFaceText.textInfo.meshInfo.Length; i++)
                {
                    Vector3[] vertices = secondFaceText.textInfo.meshInfo[i].vertices;
                    _originalVerticesSecondFace[i] = new Vector3[vertices.Length];
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        _originalVerticesSecondFace[i][j] = vertices[j];
                    }
                }

                _originalColorsSecondFace = new Color32[secondFaceText.textInfo.meshInfo.Length][];
                for (int i = 0; i < secondFaceText.textInfo.meshInfo.Length; i++)
                {
                    Color32[] colors = secondFaceText.textInfo.meshInfo[i].colors32;
                    _originalColorsSecondFace[i] = new Color32[colors.Length];
                    for (int j = 0; j < colors.Length; j++)
                    {
                        _originalColorsSecondFace[i][j] = colors[j];
                    }
                }
            }

            // InnerGlow 원본 메시 저장
            if (innerGlowText != null)
            {
                _originalVerticesInnerGlow = new Vector3[innerGlowText.textInfo.meshInfo.Length][];
                for (int i = 0; i < innerGlowText.textInfo.meshInfo.Length; i++)
                {
                    Vector3[] vertices = innerGlowText.textInfo.meshInfo[i].vertices;
                    _originalVerticesInnerGlow[i] = new Vector3[vertices.Length];
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        _originalVerticesInnerGlow[i][j] = vertices[j];
                    }
                }

                _originalColorsInnerGlow = new Color32[innerGlowText.textInfo.meshInfo.Length][];
                for (int i = 0; i < innerGlowText.textInfo.meshInfo.Length; i++)
                {
                    Color32[] colors = innerGlowText.textInfo.meshInfo[i].colors32;
                    _originalColorsInnerGlow[i] = new Color32[colors.Length];
                    for (int j = 0; j < colors.Length; j++)
                    {
                        _originalColorsInnerGlow[i][j] = colors[j];
                    }
                }
            }

            for (int i = 0; i < charCount; i++)
            {
                var charInfo = _tmpText.textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                _originalPositions[i] = GetCharacterCenter(charInfo);
                _charStates[i] = InitCharacterState(i);
            }

            if (_enableAppear)
            {
                for (int i = 0; i < charCount; i++)
                {
                    var charInfo = _tmpText.textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    Vector3 originalPos = _originalPositions[i];
                    Vector3 originalScale = Vector3.one;
                    Vector3 originalRot = Vector3.zero;
                    float originalAlpha = 1f;

                    Vector3 appearFromPos, appearToPos, appearFromScale, appearToScale, appearFromRot, appearToRot;
                    float appearFromAlpha, appearToAlpha;

                    CalculateFromTo(true, _appearRelative,
                        _appearPosition, originalPos,
                        _appearScale, originalScale,
                        _appearRotation, originalRot,
                        _appearAlpha, originalAlpha,
                        out appearFromPos, out appearToPos,
                        out appearFromScale, out appearToScale,
                        out appearFromRot, out appearToRot,
                        out appearFromAlpha, out appearToAlpha);

                    TransformCharacterVertices(i, appearFromPos, appearFromScale, appearFromRot, appearFromAlpha);
                }

                UpdateAllVertexData();
            }

            // 정점 초기화 완료 후 CanvasGroup 표시
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            _isPlayingInProgress = false;
        }

        public void Stop()
        {
            ResetAllCharStates();
            _isPlaying = false;
            _isPaused = false;
            RestoreOriginalMesh();
        }

        public void Pause()
        {
            if (_charStates == null) return;
            _isPaused = true;
        }

        public void Resume()
        {
            if (_charStates == null) return;
            _isPaused = false;
        }

        public void Restart()
        {
            Stop();
            Play();
        }

        public void ApplyPreset(TMPAnimationPreset preset)
        {
            if (preset == null) return;

            _preset = preset;
            _characterDelay = preset.CharacterDelay;

            _enableAppear = preset.EnableAppear;
            _appearRelative = preset.AppearRelative;
            _appearPosition = preset.AppearPosition;
            _appearScale = preset.AppearScale;
            _appearRotation = preset.AppearRotation;
            _appearAlpha = preset.AppearAlpha;
            _appearDuration = preset.AppearDuration;
            _appearEase = preset.AppearEase;
            _appearUseCustomCurve = preset.AppearUseCustomCurve;
            _appearCustomCurve = preset.AppearCustomCurve;
            _appearToLoopBlend = preset.AppearToLoopBlend;
            _appearUsePositionCurve = preset.AppearUsePositionCurve;
            _appearPositionCurveOffset = preset.AppearPositionCurveOffset;

            _enableLoop = preset.EnableLoop;
            _loopRelative = preset.LoopRelative;
            _loopPosition = preset.LoopPosition;
            _loopScale = preset.LoopScale;
            _loopRotation = preset.LoopRotation;
            _loopDuration = preset.LoopDuration;
            _loopEase = preset.LoopEase;
            _loopUseCustomCurve = preset.LoopUseCustomCurve;
            _loopCustomCurve = preset.LoopCustomCurve;
            _loopCount = preset.LoopCount;
            _loopType = preset.LoopType;
            _loopToDisappearBlend = preset.LoopToDisappearBlend;
            _loopUsePositionCurve = preset.LoopUsePositionCurve;
            _loopPositionCurveOffset = preset.LoopPositionCurveOffset;

            _enableDisappear = preset.EnableDisappear;
            _disappearRelative = preset.DisappearRelative;
            _disappearPosition = preset.DisappearPosition;
            _disappearScale = preset.DisappearScale;
            _disappearRotation = preset.DisappearRotation;
            _disappearAlpha = preset.DisappearAlpha;
            _disappearDuration = preset.DisappearDuration;
            _disappearEase = preset.DisappearEase;
            _disappearUseCustomCurve = preset.DisappearUseCustomCurve;
            _disappearCustomCurve = preset.DisappearCustomCurve;
            _disappearUsePositionCurve = preset.DisappearUsePositionCurve;
            _disappearPositionCurveOffset = preset.DisappearPositionCurveOffset;

            if (_isPlaying) Restart();
        }
    }
}
