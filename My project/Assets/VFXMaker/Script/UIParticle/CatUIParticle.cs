using System.Collections.Generic;
using CAT.VFX.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CAT.VFX
{
    /// <summary>
    /// 월드 기반 ParticleSystem을 Canvas UI 뎁스 구조에 따라 렌더링하는 컴포넌트
    /// Camera/RenderTexture 없이 메시 베이킹으로 UI 파이프라인에 직접 통합
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/CAT UI Particle")]
    public class CatUIParticle : MaskableGraphic
    {
        public enum AutoScalingMode
        {
            /// <summary>
            /// 자동 스케일 보정 없음
            /// </summary>
            None,

            /// <summary>
            /// UIParticle.scale을 조정하여 Canvas 스케일 보정
            /// </summary>
            UIParticle,

            /// <summary>
            /// Transform.localScale을 (1,1,1)로 설정하여 Canvas 스케일 무시
            /// </summary>
            Transform
        }

        public enum PositionMode
        {
            /// <summary>
            /// 파티클이 UIParticle 기준 상대 위치에서 방출
            /// </summary>
            Relative,

            /// <summary>
            /// 파티클이 월드 좌표 절대 위치에서 방출
            /// </summary>
            Absolute
        }

        [Tooltip("렌더링 파티클의 스케일. 3D 토글 활성화 시 (x,y,z) 개별 스케일 지원")]
        [SerializeField]
        private Vector3 m_Scale3D = new Vector3(100, 100, 100);

        [Tooltip("AnimationClip에서 머티리얼 프로퍼티를 제어할 때 사용")]
        [SerializeField]
        internal AnimatableProperty[] m_AnimatableProperties = new AnimatableProperty[0];

        [Tooltip("파티클 시스템 목록")]
        [SerializeField]
        private List<ParticleSystem> m_Particles = new List<ParticleSystem>();

        [Tooltip("방출 위치 모드\nRelative: 스케일된 상대 위치에서 방출\nAbsolute: 월드 절대 위치에서 방출")]
        [SerializeField]
        private PositionMode m_PositionMode = PositionMode.Relative;

        [Tooltip("Canvas 스케일 변경 시 자동 보정 방식\nNone: 보정 없음\nTransform: lossyScale을 (1,1,1)로 설정\nUIParticle: scale을 조정")]
        [SerializeField]
        private AutoScalingMode m_AutoScalingMode = AutoScalingMode.Transform;

        [Tooltip("커스텀 뷰 사용 여부 (min/max 파티클 크기 문제 해결)")]
        [SerializeField]
        private bool m_UseCustomView;

        [Tooltip("커스텀 뷰 크기")]
        [SerializeField]
        private float m_CustomViewSize = 10;

        [Tooltip("시간 스케일 배수")]
        [SerializeField]
        private float m_TimeScaleMultiplier = 1;

        private readonly List<CatUIParticleRenderer> _renderers = new List<CatUIParticleRenderer>();
        private Camera _bakeCamera;
        private bool _isScaleStored;
        private Vector3 _storedScale;
        private DrivenRectTransformTracker _tracker;

        /// <summary>
        /// 레이캐스트 대상에서 제외
        /// </summary>
        public override bool raycastTarget
        {
            get => false;
            set { }
        }

        /// <summary>
        /// 방출 위치 모드
        /// </summary>
        public PositionMode positionMode
        {
            get => m_PositionMode;
            set => m_PositionMode = value;
        }

        /// <summary>
        /// Canvas 스케일 자동 보정 방식
        /// </summary>
        public AutoScalingMode autoScalingMode
        {
            get => m_AutoScalingMode;
            set
            {
                if (m_AutoScalingMode == value) return;
                m_AutoScalingMode = value;

                if (autoScalingMode != AutoScalingMode.Transform && _isScaleStored)
                {
                    transform.localScale = _storedScale;
                    _isScaleStored = false;
                }
            }
        }

        /// <summary>
        /// 커스텀 뷰 사용 여부
        /// </summary>
        public bool useCustomView
        {
            get => m_UseCustomView;
            set => m_UseCustomView = value;
        }

        /// <summary>
        /// 커스텀 뷰 크기
        /// </summary>
        public float customViewSize
        {
            get => m_CustomViewSize;
            set => m_CustomViewSize = Mathf.Max(0.1f, value);
        }

        /// <summary>
        /// 시간 스케일 배수
        /// </summary>
        public float timeScaleMultiplier
        {
            get => m_TimeScaleMultiplier;
            set => m_TimeScaleMultiplier = value;
        }

        /// <summary>
        /// 파티클 이펙트 스케일 (균등)
        /// </summary>
        public float scale
        {
            get => m_Scale3D.x;
            set => m_Scale3D = new Vector3(value, value, value);
        }

        /// <summary>
        /// 파티클 이펙트 스케일 (3D)
        /// </summary>
        public Vector3 scale3D
        {
            get => m_Scale3D;
            set => m_Scale3D = value;
        }

        /// <summary>
        /// 계산용 스케일 (AutoScalingMode에 따라 Canvas 스케일 반영)
        /// </summary>
        public Vector3 scale3DForCalc => autoScalingMode == AutoScalingMode.Transform
            ? m_Scale3D
            : m_Scale3D.GetScaled(canvasScale, transform.localScale);

        /// <summary>
        /// 파티클 시스템 목록
        /// </summary>
        public List<ParticleSystem> particles => m_Particles;

        /// <summary>
        /// 일시정지 상태
        /// </summary>
        public bool isPaused { get; private set; }

        /// <summary>
        /// 부모 월드 스케일
        /// </summary>
        public Vector3 parentScale { get; private set; }

        /// <summary>
        /// Canvas 루트 스케일의 역수
        /// </summary>
        public Vector3 canvasScale { get; private set; }

        /// <summary>
        /// 애니메이터블 프로퍼티 배열
        /// </summary>
        internal AnimatableProperty[] animatableProperties => m_AnimatableProperties;

        protected override void OnEnable()
        {
            _isScaleStored = false;
            CatUIParticleUpdater.Register(this);
            RegisterDirtyMaterialCallback(UpdateRendererMaterial);

            if (0 < particles.Count)
            {
                RefreshParticles(particles);
            }
            else
            {
                RefreshParticles();
            }

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _tracker.Clear();
            if (autoScalingMode == AutoScalingMode.Transform && _isScaleStored)
            {
                transform.localScale = _storedScale;
            }

            _isScaleStored = false;
            CatUIParticleUpdater.Unregister(this);
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i]) _renderers[i].Reset();
            }
            UnregisterDirtyMaterialCallback(UpdateRendererMaterial);

            base.OnDisable();
        }

        protected override void OnDidApplyAnimationProperties()
        {
        }

        // --- 공개 API ---

        /// <summary>
        /// 파티클 재생
        /// </summary>
        public void Play()
        {
            particles.Exec(p => p.Simulate(0, false, true));
            isPaused = false;
        }

        /// <summary>
        /// 일시정지
        /// </summary>
        public void Pause()
        {
            particles.Exec(p => p.Pause());
            isPaused = true;
        }

        /// <summary>
        /// 재개
        /// </summary>
        public void Resume()
        {
            isPaused = false;
        }

        /// <summary>
        /// 정지
        /// </summary>
        public void Stop()
        {
            particles.Exec(p => p.Stop());
            isPaused = true;
        }

        /// <summary>
        /// 방출 시작
        /// </summary>
        public void StartEmission()
        {
            particles.Exec(p =>
            {
                var emission = p.emission;
                emission.enabled = true;
            });
        }

        /// <summary>
        /// 방출 정지
        /// </summary>
        public void StopEmission()
        {
            particles.Exec(p =>
            {
                var emission = p.emission;
                emission.enabled = false;
            });
        }

        /// <summary>
        /// 파티클 초기화
        /// </summary>
        public void Clear()
        {
            particles.Exec(p => p.Clear());
            isPaused = true;
        }

        /// <summary>
        /// 렌더링에 사용되는 기본 머티리얼 목록을 가져온다
        /// </summary>
        public void GetMaterials(List<Material> result)
        {
            if (result == null) return;

            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (!r || !r.material) continue;
                result.Add(r.material);
            }
        }

        /// <summary>
        /// ParticleSystem 인스턴스를 설정하여 UIParticle을 갱신
        /// </summary>
        public void SetParticleSystemInstance(GameObject instance, bool destroyOldParticles = true)
        {
            if (!instance) return;

            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var go = transform.GetChild(i).gameObject;
                if (go.TryGetComponent<Camera>(out var cam) && cam == _bakeCamera) continue;
                if (go.TryGetComponent<CatUIParticleRenderer>(out _)) continue;

                go.SetActive(false);
                if (destroyOldParticles)
                {
                    Destroy(go);
                }
            }

            var tr = instance.transform;
            tr.SetParent(transform, false);
            tr.localPosition = Vector3.zero;

            RefreshParticles(instance);
        }

        /// <summary>
        /// 프리팹으로부터 인스턴스를 생성하여 설정
        /// </summary>
        public void SetParticleSystemPrefab(GameObject prefab)
        {
            if (!prefab) return;

            SetParticleSystemInstance(Instantiate(prefab.gameObject), true);
        }

        /// <summary>
        /// 하위 ParticleSystem을 수집하여 렌더러를 갱신
        /// </summary>
        public void RefreshParticles()
        {
            RefreshParticles(gameObject);
        }

        /// <summary>
        /// ParticleSystem 리스트로 렌더러를 갱신
        /// </summary>
        public void RefreshParticles(List<ParticleSystem> particleSystems)
        {
            // 자식 CatUIParticleRenderer 수집
            _renderers.Clear();
            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.TryGetComponent(out CatUIParticleRenderer uiParticleRenderer))
                {
                    _renderers.Add(uiParticleRenderer);
                }
            }

            // 렌더러 초기화
            for (var i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].Reset(i);
            }

            // ParticleSystem을 렌더러에 연결 (Trail 포함)
            var j = 0;
            for (var i = 0; i < particleSystems.Count; i++)
            {
                var ps = particleSystems[i];
                if (!ps) continue;

                var mainEmitter = ps.GetMainEmitter(particleSystems);
                GetRenderer(j++).Set(this, ps, false, mainEmitter);

                // Trail이 활성화되어 있으면 추가 렌더러 생성
                if (ps.trails.enabled)
                {
                    GetRenderer(j++).Set(this, ps, true, mainEmitter);
                }
            }
        }

        // --- 내부 메서드 ---

        /// <summary>
        /// Canvas 스케일 변경에 따른 Transform 스케일 보정
        /// </summary>
        internal void UpdateTransformScale()
        {
            _tracker.Clear();
            canvasScale = canvas.rootCanvas.transform.localScale.Inverse();
            parentScale = transform.parent.lossyScale;
            if (autoScalingMode != AutoScalingMode.Transform)
            {
                if (_isScaleStored)
                {
                    transform.localScale = _storedScale;
                }

                _isScaleStored = false;
                return;
            }

            var currentScale = transform.localScale;
            if (!_isScaleStored)
            {
                _storedScale = currentScale.IsVisible() ? currentScale : Vector3.one;
                _isScaleStored = true;
            }

            _tracker.Add(this, rectTransform, DrivenTransformProperties.Scale);
            var newScale = parentScale.Inverse();
            if (currentScale != newScale)
            {
                transform.localScale = newScale;
            }
        }

        /// <summary>
        /// 모든 렌더러의 메시를 업데이트
        /// </summary>
        internal void UpdateRenderers()
        {
            if (!isActiveAndEnabled) return;

            // 파괴된 렌더러가 있으면 갱신
            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r) continue;

                RefreshParticles(particles);
                break;
            }

            var bakeCamera = GetBakeCamera();
            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (!r) continue;

                r.UpdateMesh(bakeCamera);
            }
        }

        protected override void UpdateMaterial()
        {
        }

        protected override void UpdateGeometry()
        {
        }

        /// <summary>
        /// Stencil Mask 변경 시 자식 렌더러에 전파
        /// </summary>
        public override void RecalculateMasking()
        {
            base.RecalculateMasking();
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i]) _renderers[i].RecalculateMasking();
            }
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i]) _renderers[i].RecalculateMasking();
            }
        }

        private void UpdateRendererMaterial()
        {
            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (!r) continue;
                r.maskable = maskable;
                r.SetMaterialDirty();
            }
        }

        private void RefreshParticles(GameObject root)
        {
            if (!root) return;
            root.GetComponentsInChildren(true, particles);
            for (var i = particles.Count - 1; 0 <= i; i--)
            {
                var ps = particles[i];
                if (!ps || ps.GetComponentInParent<CatUIParticle>(true) != this)
                {
                    particles.RemoveAt(i);
                }
            }

            for (var i = 0; i < particles.Count; i++)
            {
                var ps = particles[i];
                var tsa = ps.textureSheetAnimation;
                if (tsa.mode == ParticleSystemAnimationMode.Sprites && tsa.uvChannelMask == 0)
                {
                    tsa.uvChannelMask = UVChannelFlags.UV0;
                }
            }

            RefreshParticles(particles);
        }

        internal CatUIParticleRenderer GetRenderer(int index)
        {
            if (_renderers.Count <= index)
            {
                _renderers.Add(CatUIParticleRenderer.AddRenderer(this, index));
            }

            if (!_renderers[index])
            {
                _renderers[index] = CatUIParticleRenderer.AddRenderer(this, index);
            }

            return _renderers[index];
        }

        /// <summary>
        /// 메시 베이킹용 카메라를 가져오거나 생성
        /// </summary>
        private Camera GetBakeCamera()
        {
            if (!canvas) return Camera.main;
            if (!useCustomView && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.rootCanvas.worldCamera)
            {
                return canvas.rootCanvas.worldCamera;
            }

            if (_bakeCamera)
            {
                _bakeCamera.orthographicSize = useCustomView ? customViewSize : 10;
                return _bakeCamera;
            }

            // 기존 베이킹 카메라 검색
            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                if (transform.GetChild(i).TryGetComponent<Camera>(out var cam)
                    && cam.name == "[generated] CatUIParticle BakingCamera")
                {
                    _bakeCamera = cam;
                    break;
                }
            }

            // 베이킹 카메라 생성
            if (!_bakeCamera)
            {
                var go = new GameObject("[generated] CatUIParticle BakingCamera");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                _bakeCamera = go.AddComponent<Camera>();
            }

            // 베이킹 카메라 설정
            _bakeCamera.enabled = false;
            _bakeCamera.orthographicSize = useCustomView ? customViewSize : 10;
            _bakeCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -1000), Quaternion.identity);
            _bakeCamera.orthographic = true;
            _bakeCamera.farClipPlane = 2000f;
            _bakeCamera.clearFlags = CameraClearFlags.Nothing;
            _bakeCamera.cullingMask = 0;
            _bakeCamera.allowHDR = false;
            _bakeCamera.allowMSAA = false;
            _bakeCamera.renderingPath = RenderingPath.Forward;
            _bakeCamera.useOcclusionCulling = false;

            _bakeCamera.gameObject.SetActive(false);
            _bakeCamera.gameObject.hideFlags = CatUIParticleSettings.GlobalHideFlags;

            return _bakeCamera;
        }
    }
}
