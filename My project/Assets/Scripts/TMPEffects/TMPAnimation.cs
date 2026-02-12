using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace CAT.UI
{
    /// <summary>
    /// TMP 텍스트 글자별 애니메이션
    /// - 각 글자를 독립적으로 애니메이션 (Appear, Loop, Disappear)
    /// - DOTween 기반 시퀀스 관리
    /// - 프리셋 시스템 지원
    /// - 모바일 최적화: Sequence 재사용, GC Alloc 최소화
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
        private Ease _appearEase = Ease.OutBack;

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
        private Ease _disappearEase = Ease.InBack;

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
        // 캐싱
        // ─────────────────────────────────────────────

        private TMP_Text _tmpText;
        private CanvasGroup _canvasGroup;
        private Sequence[] _sequences;
        private Vector3[] _originalPositions;
        private Vector3[][] _originalVertices;
        private Vector3[][] _originalVerticesSecondFace;
        private Vector3[][] _originalVerticesInnerGlow;  // InnerGlow 원본 정점
        private Color32[][] _originalColors;
        private Color32[][] _originalColorsSecondFace;
        private Color32[][] _originalColorsInnerGlow;  // InnerGlow 원본 색상
        private bool _isPlaying = false;
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
        public Ease AppearEase => _appearEase;
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
        public Ease LoopEase => _loopEase;
        public bool LoopUseCustomCurve => _loopUseCustomCurve;
        public AnimationCurve LoopCustomCurve => _loopCustomCurve;
        public int LoopCount => _loopCount;
        public LoopType LoopType => _loopType;
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
        public Ease DisappearEase => _disappearEase;
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
                KillAllSequences();
                RestoreOriginalMesh();
            }

            // 상태 초기화
            _sequences = null;
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

        private Tween AnimateCharacter(int charIndex,
            Vector3 fromPos, Vector3 toPos,
            Vector3 fromScale, Vector3 toScale,
            Vector3 fromRot, Vector3 toRot,
            float fromAlpha, float toAlpha,
            float duration, Ease ease,
            AnimationCurve customCurve = null,
            bool useCurrentAsFrom = false,
            bool usePositionCurve = false,
            Vector2 positionCurveOffset = default)
        {
            // 시작 위치 캡처용 변수 (블렌딩 시 현재 위치에서 시작)
            Vector3 capturedFromPos = fromPos;
            Vector3 capturedFromScale = fromScale;
            Vector3 capturedFromRot = fromRot;
            float capturedFromAlpha = fromAlpha;
            bool hasCaptured = false;

            var tween = DOTween.To(() => 0f, (t) =>
            {
                // 첫 프레임에서 현재 상태 캡처 (블렌딩용)
                if (!hasCaptured && useCurrentAsFrom && _currentCharPos != null && charIndex < _currentCharPos.Length)
                {
                    capturedFromPos = _currentCharPos[charIndex];
                    capturedFromScale = _currentCharScale[charIndex];
                    capturedFromRot = _currentCharRot[charIndex];
                    capturedFromAlpha = _currentCharAlpha[charIndex];
                    hasCaptured = true;
                }
                else if (!hasCaptured)
                {
                    hasCaptured = true;
                }

                float easedT = customCurve != null ? customCurve.Evaluate(t) : t;

                // Position 커브 적용: Quadratic Bezier Curve (시작점→중간점→도착점)
                Vector3 currentPos;
                if (usePositionCurve)
                {
                    // 중간 제어점 계산: 시작점과 도착점의 중간 + 오프셋
                    Vector3 midPoint = (capturedFromPos + toPos) * 0.5f;
                    midPoint.x += positionCurveOffset.x;
                    midPoint.y += positionCurveOffset.y;

                    // Quadratic Bezier: P(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
                    float oneMinusT = 1f - easedT;
                    float posX = oneMinusT * oneMinusT * capturedFromPos.x
                               + 2f * oneMinusT * easedT * midPoint.x
                               + easedT * easedT * toPos.x;
                    float posY = oneMinusT * oneMinusT * capturedFromPos.y
                               + 2f * oneMinusT * easedT * midPoint.y
                               + easedT * easedT * toPos.y;
                    float posZ = Mathf.Lerp(capturedFromPos.z, toPos.z, easedT);
                    currentPos = new Vector3(posX, posY, posZ);
                }
                else
                {
                    currentPos = Vector3.Lerp(capturedFromPos, toPos, easedT);
                }

                // Scale, Rotation, Alpha는 기존 Curve(easedT)에 영향을 받음
                Vector3 currentScale = Vector3.Lerp(capturedFromScale, toScale, easedT);
                Vector3 currentRot = Vector3.Lerp(capturedFromRot, toRot, easedT);
                float currentAlpha = Mathf.Lerp(capturedFromAlpha, toAlpha, easedT);

                // 현재 상태 업데이트 (다음 애니메이션 블렌딩용)
                if (_currentCharPos != null && charIndex < _currentCharPos.Length)
                {
                    _currentCharPos[charIndex] = currentPos;
                    _currentCharScale[charIndex] = currentScale;
                    _currentCharRot[charIndex] = currentRot;
                    _currentCharAlpha[charIndex] = currentAlpha;
                }

                TransformCharacterVertices(charIndex, currentPos, currentScale, currentRot, currentAlpha);

                _tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

                var outlineEffect = GetComponent<TMPOutlineEffect>();
                if (outlineEffect != null)
                {
                    // Second Face 처리 (Shadow 없음)
                    if (outlineEffect.EnableSecondFace)
                    {
                        var secondFaceText = outlineEffect.GetSecondFaceText();
                        if (secondFaceText != null)
                        {
                            secondFaceText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                        }
                    }

                    // 메인 텍스트의 Shadow 처리
                    if (outlineEffect.EnableShadow)
                    {
                        ApplyShadowMesh(_tmpText, outlineEffect);
                    }
                }

                // Inner Glow 처리
                var glowEffect = GetComponent<TMPOutGlow>();
                if (glowEffect != null)
                {
                    var innerGlowText = glowEffect.GetInnerGlowText();
                    if (innerGlowText != null)
                    {
                        innerGlowText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                    }
                }
            }, 1f, duration);

            // 에디터 모드에서는 Manual 업데이트 사용
            if (!Application.isPlaying)
            {
                tween.SetUpdate(UpdateType.Manual);
            }

            if (customCurve == null)
            {
                tween.SetEase(ease);
            }
            else
            {
                tween.SetEase(Ease.Linear);
            }

            return tween;
        }

        private Sequence CreateCharacterSequence(int charIndex)
        {
            var seq = DOTween.Sequence();

            // 에디터 모드에서는 Manual 업데이트 사용
            if (!Application.isPlaying)
            {
                seq.SetUpdate(UpdateType.Manual);
            }

            float delay = charIndex * _characterDelay;

            Vector3 originalPos = _originalPositions[charIndex];
            Vector3 originalScale = Vector3.one;
            Vector3 originalRot = Vector3.zero;
            float originalAlpha = 1f;

            // 시간 추적 (Insert 위치 계산용)
            float currentTime = 0f;

            AnimationCurve curve;

            // ─────────────────────────────────────────────
            // Appear
            // ─────────────────────────────────────────────
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

                curve = _appearUseCustomCurve ? _appearCustomCurve : null;
                seq.Insert(currentTime, AnimateCharacter(charIndex,
                    appearFromPos, appearToPos,
                    appearFromScale, appearToScale,
                    appearFromRot, appearToRot,
                    appearFromAlpha, appearToAlpha,
                    _appearDuration, _appearEase, curve, false,
                    _appearUsePositionCurve, _appearPositionCurveOffset));

                // 다음 애니메이션 시작 시간 (블렌딩 적용 - 비율 기반)
                // 블렌드 비율 0.25 = Appear 마지막 25% 시점에서 Loop 시작
                float appearBlendTime = _appearDuration * _appearToLoopBlend;
                currentTime += _appearDuration - appearBlendTime;
                if (currentTime < 0) currentTime = 0;
            }

            // ─────────────────────────────────────────────
            // Loop (0이면 건너뜀)
            // Loop Count: 0=비활성화, 1=1회, 2=2회, -1=무한
            // ─────────────────────────────────────────────
            if (_enableLoop && _loopCount != 0)
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

                curve = _loopUseCustomCurve ? _loopCustomCurve : null;

                // 블렌딩 사용 여부 (Appear와 오버랩되면 현재 위치에서 시작)
                bool useBlendFromAppear = _enableAppear && _appearToLoopBlend > 0;

                // 무한 루프인 경우
                if (_loopCount == -1)
                {
                    if (_loopType == LoopType.Yoyo)
                    {
                        // Yoyo: Forward + Backward를 묶어서 무한 반복
                        var loopSeq = DOTween.Sequence();
                        if (!Application.isPlaying) loopSeq.SetUpdate(UpdateType.Manual);

                        loopSeq.Append(AnimateCharacter(charIndex,
                            loopFromPos, loopToPos,
                            loopFromScale, loopToScale,
                            loopFromRot, loopToRot,
                            loopFromAlpha, loopToAlpha,
                            _loopDuration, _loopEase, curve, useBlendFromAppear,
                            _loopUsePositionCurve, _loopPositionCurveOffset));
                        loopSeq.Append(AnimateCharacter(charIndex,
                            loopToPos, loopFromPos,
                            loopToScale, loopFromScale,
                            loopToRot, loopFromRot,
                            loopToAlpha, loopFromAlpha,
                            _loopDuration, _loopEase, curve, false,
                            _loopUsePositionCurve, _loopPositionCurveOffset));
                        loopSeq.SetLoops(-1, LoopType.Restart);
                        seq.Insert(currentTime, loopSeq);
                    }
                    else
                    {
                        // Restart: Forward만 무한 반복
                        var forwardTween = AnimateCharacter(charIndex,
                            loopFromPos, loopToPos,
                            loopFromScale, loopToScale,
                            loopFromRot, loopToRot,
                            loopFromAlpha, loopToAlpha,
                            _loopDuration, _loopEase, curve, useBlendFromAppear,
                            _loopUsePositionCurve, _loopPositionCurveOffset);
                        forwardTween.SetLoops(-1, LoopType.Restart);
                        seq.Insert(currentTime, forwardTween);
                    }

                    // 무한 루프면 여기서 종료 (Disappear 없음)
                    seq.SetDelay(delay);
                    seq.SetAutoKill(false);
                    return seq;
                }

                // 유한 루프: 직접 Loop 횟수만큼 애니메이션 추가
                for (int i = 0; i < _loopCount; i++)
                {
                    // 첫 번째 Loop만 블렌딩 적용
                    bool useBlend = (i == 0) && useBlendFromAppear;

                    // Forward 애니메이션
                    seq.Insert(currentTime, AnimateCharacter(charIndex,
                        loopFromPos, loopToPos,
                        loopFromScale, loopToScale,
                        loopFromRot, loopToRot,
                        loopFromAlpha, loopToAlpha,
                        _loopDuration, _loopEase, curve, useBlend,
                        _loopUsePositionCurve, _loopPositionCurveOffset));
                    currentTime += _loopDuration;

                    if (_loopType == LoopType.Yoyo)
                    {
                        // Backward 애니메이션
                        seq.Insert(currentTime, AnimateCharacter(charIndex,
                            loopToPos, loopFromPos,
                            loopToScale, loopFromScale,
                            loopToRot, loopFromRot,
                            loopToAlpha, loopFromAlpha,
                            _loopDuration, _loopEase, curve, false,
                            _loopUsePositionCurve, _loopPositionCurveOffset));
                        currentTime += _loopDuration;
                    }
                }

                // Loop → Disappear 블렌딩 적용 (비율 기반)
                // 블렌드 비율 0.25 = Loop 마지막 25% 시점에서 Disappear 시작
                float loopBlendTime = _loopDuration * _loopToDisappearBlend;
                currentTime -= loopBlendTime;
                if (currentTime < 0) currentTime = 0;
            }

            // ─────────────────────────────────────────────
            // Disappear (블렌딩 시 현재 위치에서 시작)
            // ─────────────────────────────────────────────
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

                // 블렌딩 사용 여부
                bool useBlend = (_enableLoop && _loopCount != 0 && _loopToDisappearBlend > 0) ||
                                (_enableAppear && !_enableLoop && _appearToLoopBlend > 0);

                curve = _disappearUseCustomCurve ? _disappearCustomCurve : null;
                seq.Insert(currentTime, AnimateCharacter(charIndex,
                    disappearFromPos, disappearToPos,
                    disappearFromScale, disappearToScale,
                    disappearFromRot, disappearToRot,
                    disappearFromAlpha, disappearToAlpha,
                    _disappearDuration, _disappearEase, curve, useBlend,
                    _disappearUsePositionCurve, _disappearPositionCurveOffset));
            }

            seq.SetDelay(delay);
            seq.SetAutoKill(false);
            return seq;
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

        private void KillAllSequences()
        {
            if (_sequences != null)
            {
                foreach (var seq in _sequences)
                {
                    if (seq != null && seq.IsActive()) seq.Kill();
                }
                _sequences = null;
            }
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

            KillAllSequences();

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

            _sequences = new Sequence[charCount];
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
                _sequences[i] = CreateCharacterSequence(i);
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
            KillAllSequences();
            _isPlaying = false;
            RestoreOriginalMesh();
        }

        public void Pause()
        {
            if (_sequences == null) return;

            foreach (var seq in _sequences)
            {
                if (seq != null && seq.IsActive()) seq.Pause();
            }
        }

        public void Resume()
        {
            if (_sequences == null) return;

            foreach (var seq in _sequences)
            {
                if (seq != null && seq.IsActive()) seq.Play();
            }
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
