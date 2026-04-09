using System;
using System.Collections.Generic;
using CAT.VFX.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.VFX
{
    /// <summary>
    /// ParticleSystem별 숨겨진 렌더러
    /// 메시 베이킹, 시뮬레이션, 좌표 변환을 담당하여 Canvas UI 뎁스 순서에 따라 렌더링
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("")]
    internal class CatUIParticleRenderer : MaskableGraphic
    {
        private static readonly CombineInstance[] s_CombineInstances = { new CombineInstance() };
        private static readonly List<Material> s_Materials = new List<Material>(2);
        private static MaterialPropertyBlock s_Mpb;

        private bool _delay;
        private int _index;
        private bool _isPrevStored;
        private bool _isTrail;
        private Material _materialForRendering;
        private Material _modifiedMaterial;
        private CatUIParticle _parent;
        private ParticleSystem _particleSystem;
        private float _prevCanvasScale;
        private Vector3 _prevPsPos;
        private Vector3 _prevScale;
        private Vector2Int _prevScreenSize;
        private bool _preWarm;
        private ParticleSystemRenderer _renderer;
        private ParticleSystem _mainEmitter;

        // IMeshModifier 캐싱 (매 프레임 GetComponents 호출 방지)
        private readonly List<IMeshModifier> _meshModifiers = new List<IMeshModifier>(2);
        private bool _meshModifiersDirty = true;

        public override Texture mainTexture => _isTrail ? null : _particleSystem.GetTextureForSprite();

        public override bool raycastTarget => false;

        public override Material materialForRendering
        {
            get
            {
                if (!_materialForRendering)
                {
                    _materialForRendering = base.materialForRendering;
                }

                return _materialForRendering;
            }
        }

        public void Reset(int index = -1)
        {
            if (_renderer)
            {
                _renderer.enabled = true;
            }

            _parent = null;
            _particleSystem = null;
            _renderer = null;
            _mainEmitter = null;
            if (0 <= index)
            {
                _index = index;
            }

            if (this && isActiveAndEnabled)
            {
                material = null;
                canvasRenderer.Clear();
                enabled = false;
            }
            else
            {
                MaterialRepository.Release(ref _modifiedMaterial);
                _materialForRendering = null;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            hideFlags = CatUIParticleSettings.GlobalHideFlags;
            if (!s_CombineInstances[0].mesh)
            {
                s_CombineInstances[0].mesh = new Mesh
                {
                    name = "[CatUIParticleRenderer] Combine Instance Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            MaterialRepository.Release(ref _modifiedMaterial);
            _materialForRendering = null;
            _isPrevStored = false;
        }

        /// <summary>
        /// 새 렌더러 자식 오브젝트 생성
        /// </summary>
        public static CatUIParticleRenderer AddRenderer(CatUIParticle parent, int index)
        {
            var go = new GameObject("[generated] CatUIParticleRenderer", typeof(CatUIParticleRenderer))
            {
                hideFlags = CatUIParticleSettings.GlobalHideFlags,
                layer = parent.gameObject.layer
            };

            var transform = go.transform;
            transform.SetParent(parent.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            var renderer = go.GetComponent<CatUIParticleRenderer>();
            renderer._parent = parent;
            renderer._index = index;

            return renderer;
        }

        /// <summary>
        /// 머티리얼 수정 (마스킹, 텍스처, 애니메이터블 프로퍼티 적용)
        /// </summary>
        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!IsActive() || !_parent)
            {
                MaterialRepository.Release(ref _modifiedMaterial);
                return baseMaterial;
            }

            var modifiedMaterial = base.GetModifiedMaterial(baseMaterial);

            var texture = mainTexture;
            if (texture == null && _parent.animatableProperties.Length == 0)
            {
                MaterialRepository.Release(ref _modifiedMaterial);
                return modifiedMaterial;
            }

            var hash = new Hash128(
                modifiedMaterial ? (uint)modifiedMaterial.GetInstanceID() : 0,
                texture ? (uint)texture.GetInstanceID() : 0,
                0 < _parent.animatableProperties.Length ? (uint)GetInstanceID() : 0,
#if UNITY_EDITOR
                (uint)EditorJsonUtility.ToJson(modifiedMaterial).GetHashCode()
#else
                0
#endif
            );
            if (!MaterialRepository.Valid(hash, _modifiedMaterial))
            {
                MaterialRepository.Get(hash, ref _modifiedMaterial, x => new Material(x.mat)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = x.texture ? x.texture : x.mat.mainTexture
                }, (mat: modifiedMaterial, texture));
            }

            return _modifiedMaterial;
        }

        /// <summary>
        /// ParticleSystem과 연결 설정
        /// </summary>
        public void Set(CatUIParticle parent, ParticleSystem ps, bool isTrail, ParticleSystem mainEmitter)
        {
            _parent = parent;
            maskable = parent.maskable;

            gameObject.layer = parent.gameObject.layer;

            _particleSystem = ps;
            _preWarm = _particleSystem.main.prewarm;

#if UNITY_EDITOR
            if (Application.isPlaying)
#endif
            {
                if (_particleSystem.isPlaying || _preWarm)
                {
                    _particleSystem.Clear();
                    _particleSystem.Pause();
                }
            }

            ps.TryGetComponent(out _renderer);
            _renderer.enabled = false;
            _isTrail = isTrail;
            _renderer.GetSharedMaterials(s_Materials);
            material = s_Materials[isTrail ? 1 : 0];
            s_Materials.Clear();

            // 스프라이트 시트 UV 채널 설정
            var tsa = ps.textureSheetAnimation;
            if (tsa.mode == ParticleSystemAnimationMode.Sprites && tsa.uvChannelMask == 0)
            {
                tsa.uvChannelMask = UVChannelFlags.UV0;
            }

            _prevScale = GetWorldScale();
            _prevPsPos = _particleSystem.transform.position;
            _prevScreenSize = new Vector2Int(Screen.width, Screen.height);
            _prevCanvasScale = canvas ? canvas.scaleFactor : 1f;
            _delay = true;
            _mainEmitter = mainEmitter;

            canvasRenderer.SetTexture(null);

            enabled = true;

            // Stencil Mask에 참여하도록 등록
            RecalculateMasking();
        }

        /// <summary>
        /// 메시 업데이트 - 시뮬레이션, 베이킹, 좌표 변환, CanvasRenderer에 설정
        /// </summary>
        public void UpdateMesh(Camera bakeCamera)
        {
            // 렌더링할 파티클이 없으면 메시 초기화
            if (
                !isActiveAndEnabled || !_particleSystem || !_parent
                || !canvasRenderer || !canvas || !bakeCamera
                || !transform.lossyScale.GetScaled(_parent.scale3DForCalc).IsVisible()
                || (!_particleSystem.IsAlive() && !_particleSystem.isPlaying)
                || (_isTrail && !_particleSystem.trails.enabled)
                || canvasRenderer.GetInheritedAlpha() < 0.01f
            )
            {
                workerMesh.Clear();
                canvasRenderer.SetMesh(workerMesh);
                return;
            }

            var main = _particleSystem.main;
            var scale = GetWorldScale();
            var psPos = _particleSystem.transform.position;

            // 파티클 시뮬레이션
            if (!_isTrail && !_mainEmitter)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    SimulateForEditor(psPos - _prevPsPos, scale);
                }
                else
#endif
                {
                    ResolveResolutionChange(psPos, scale);
                    Simulate(scale, _parent.isPaused || _delay);

                    if (_delay && !_parent.isPaused)
                    {
                        Simulate(scale, _parent.isPaused);
                    }

                    // 시뮬레이션 완료 시 정지
                    if (!main.loop
                        && main.duration <= _particleSystem.time
                        && (_particleSystem.IsAlive() || _particleSystem.particleCount == 0))
                    {
                        _particleSystem.Stop(false);
                    }
                }

                _prevScale = scale;
                _prevPsPos = psPos;
                _delay = false;
            }

            // 메시 베이킹
            s_CombineInstances[0].mesh.Clear(false);

            if (_isTrail && 0 < _particleSystem.particleCount)
            {
                _renderer.BakeTrailsMesh(s_CombineInstances[0].mesh, bakeCamera,
                    ParticleSystemBakeMeshOptions.BakeRotationAndScale);
            }
            else if (!_isTrail && _renderer.CanBakeMesh())
            {
                _particleSystem.ValidateShape();
                _renderer.BakeMesh(s_CombineInstances[0].mesh, bakeCamera,
                    ParticleSystemBakeMeshOptions.BakeRotationAndScale);
            }

            // 버텍스 제한 초과 체크
            if (65535 <= s_CombineInstances[0].mesh.vertexCount)
            {
                Debug.LogErrorFormat(this,
                    "Too many vertices to render. index={0}, isTrail={1}, vertexCount={2}(>=65535)",
                    _index, _isTrail, s_CombineInstances[0].mesh.vertexCount);
                s_CombineInstances[0].mesh.Clear(false);
            }

            // 좌표 변환: ParticleSystem 공간 → Canvas 로컬 공간
            if (_parent.positionMode == CatUIParticle.PositionMode.Absolute)
            {
                s_CombineInstances[0].transform =
                    canvasRenderer.transform.worldToLocalMatrix
                    * GetWorldMatrix(psPos, scale);
            }
            else
            {
                var diff = _particleSystem.transform.position - _parent.transform.position;
                s_CombineInstances[0].transform =
                    canvasRenderer.transform.worldToLocalMatrix
                    * Matrix4x4.Translate(diff.GetScaled(scale - Vector3.one))
                    * GetWorldMatrix(psPos, scale);
            }

            workerMesh.CombineMeshes(s_CombineInstances, true, true);

            workerMesh.RecalculateBounds();
            var bounds = workerMesh.bounds;
            var center = bounds.center;
            center.z = 0;
            bounds.center = center;
            var extents = bounds.extents;
            extents.z = 0;
            bounds.extents = extents;
            workerMesh.bounds = bounds;

            // IMeshModifier 적용 (Stencil Mask 등)
            if (_meshModifiersDirty)
            {
                _meshModifiers.Clear();
                GetComponents(_meshModifiers);
                _meshModifiersDirty = false;
            }

            for (var i = 0; i < _meshModifiers.Count; i++)
            {
#pragma warning disable CS0618
                _meshModifiers[i].ModifyMesh(workerMesh);
#pragma warning restore CS0618
            }

            // 애니메이터블 머티리얼 프로퍼티 업데이트
            UpdateMaterialProperties();

            // CanvasRenderer에 메시 설정
            canvasRenderer.SetMesh(workerMesh);
        }

        public override void SetMaterialDirty()
        {
            _materialForRendering = null;
            _meshModifiersDirty = true;
            base.SetMaterialDirty();
        }

        protected override void UpdateGeometry()
        {
        }

        /// <summary>
        /// 월드 스케일 계산 (Canvas 스케일, 부모 스케일, UIParticle 스케일 합산)
        /// </summary>
        private Vector3 GetWorldScale()
        {
            var scale = _parent.scale3DForCalc.GetScaled(_parent.parentScale);

            if (_parent.autoScalingMode == CatUIParticle.AutoScalingMode.UIParticle
                && _particleSystem.main.scalingMode == ParticleSystemScalingMode.Local
                && _parent.canvas)
            {
                scale = scale.GetScaled(_parent.canvas.rootCanvas.transform.localScale);
            }

            return scale;
        }

        /// <summary>
        /// 시뮬레이션 공간에 따른 월드 변환 행렬 계산
        /// </summary>
        private Matrix4x4 GetWorldMatrix(Vector3 psPos, Vector3 scale)
        {
            var space = _particleSystem.GetActualSimulationSpace();
            if (_isTrail && _particleSystem.trails.worldSpace)
            {
                space = ParticleSystemSimulationSpace.World;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                switch (space)
                {
                    case ParticleSystemSimulationSpace.World:
                        return Matrix4x4.Translate(psPos)
                               * Matrix4x4.Scale(scale)
                               * Matrix4x4.Translate(-psPos);
                }
            }
#endif

            switch (space)
            {
                case ParticleSystemSimulationSpace.Local:
                    return Matrix4x4.Translate(psPos)
                           * Matrix4x4.Scale(scale);
                case ParticleSystemSimulationSpace.World:
                    if (_isTrail)
                    {
                        return Matrix4x4.Translate(psPos)
                               * Matrix4x4.Scale(scale)
                               * Matrix4x4.Translate(-psPos);
                    }

                    if (_mainEmitter)
                    {
                        if (_mainEmitter.IsLocalSpace())
                        {
                            return Matrix4x4.Translate(psPos)
                                   * Matrix4x4.Scale(scale)
                                   * Matrix4x4.Translate(-psPos);
                        }
                        else
                        {
                            psPos = _particleSystem.transform.position - _mainEmitter.transform.position;
                            return Matrix4x4.Translate(psPos)
                                   * Matrix4x4.Scale(scale)
                                   * Matrix4x4.Translate(-psPos);
                        }
                    }

                    return Matrix4x4.Scale(scale);
                case ParticleSystemSimulationSpace.Custom:
                    return Matrix4x4.Translate(_particleSystem.main.customSimulationSpace.position.GetScaled(scale))
                           * Matrix4x4.Scale(scale);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// 해상도 변경 시 월드 스페이스 파티클 위치 보정
        /// </summary>
        private void ResolveResolutionChange(Vector3 psPos, Vector3 scale)
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var isWorldSpace = _particleSystem.IsWorldSpace();
            var canvasScale = _parent.canvas ? _parent.canvas.scaleFactor : 1f;
            var resolutionChanged = _prevScreenSize != screenSize
                                    || !Mathf.Approximately(_prevCanvasScale, canvasScale);
            if (resolutionChanged && isWorldSpace && _isPrevStored)
            {
                var size = _particleSystem.particleCount;
                var particles = ParticleSystemExtensions.GetParticleArray(size);
                _particleSystem.GetParticles(particles, size);

                var modifier = psPos.GetScaled(
                    scale.Inverse(),
                    _prevPsPos.Inverse(),
                    _prevScale);
                for (var i = 0; i < size; i++)
                {
                    var particle = particles[i];
                    particle.position = particle.position.GetScaled(modifier);
                    particles[i] = particle;
                }

                _particleSystem.SetParticles(particles, size);

                _delay = true;
                _prevScale = scale;
                _prevPsPos = psPos;
                _isPrevStored = true;
            }

            _prevCanvasScale = canvas ? canvas.scaleFactor : 1f;
            _prevScreenSize = screenSize;
        }

        /// <summary>
        /// 런타임 파티클 시뮬레이션 (시간 스케일, Pre-warm, Rate-over-distance 대응)
        /// </summary>
        private void Simulate(Vector3 scale, bool paused)
        {
            var main = _particleSystem.main;
            var deltaTime = paused
                ? 0
                : main.useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;
            deltaTime *= _parent.timeScaleMultiplier;

            // Pre-warm: 첫 프레임에 duration만큼 추가 시뮬레이션
            if (0 < deltaTime && _preWarm)
            {
                deltaTime += main.duration;
                _preWarm = false;
            }

            var isLocalSpace = _particleSystem.IsLocalSpace();
            var psTransform = _particleSystem.transform;
            var originLocalPosition = psTransform.localPosition;
            var originLocalRotation = psTransform.localRotation;
            var originWorldPosition = psTransform.position;
            var originWorldRotation = psTransform.rotation;
            var emission = _particleSystem.emission;
            var rateOverDistance = emission.enabled
                                   && 0 < emission.rateOverDistance.constant
                                   && 0 < emission.rateOverDistanceMultiplier;

            // Rate-over-distance: 이전 위치로 이동 후 시뮬레이션 (dt=0)
            if (rateOverDistance && !paused && _isPrevStored)
            {
                var prevScaledPos = isLocalSpace
                    ? _prevPsPos
                    : _prevPsPos.GetScaled(_prevScale.Inverse());
                psTransform.SetPositionAndRotation(prevScaledPos, originWorldRotation);
                _particleSystem.Simulate(0, false, false, false);
            }

            // 스케일 보정된 위치로 이동 후 시뮬레이션
            var scaledPos = isLocalSpace
                ? originWorldPosition
                : originWorldPosition.GetScaled(scale.Inverse());
            psTransform.SetPositionAndRotation(scaledPos, originWorldRotation);
            _particleSystem.Simulate(deltaTime, false, false, false);
            psTransform.localPosition = originLocalPosition;
            psTransform.localRotation = originLocalRotation;

            _isPrevStored = true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 EditMode에서 파티클 시뮬레이션 (월드 스페이스 이동 보정)
        /// </summary>
        private void SimulateForEditor(Vector3 diffPos, Vector3 scale)
        {
            var isWorldSpace = _particleSystem.IsWorldSpace();
            if (isWorldSpace && 0 < Vector3.SqrMagnitude(diffPos))
            {
                diffPos.x *= 1f - 1f / Mathf.Max(0.001f, scale.x);
                diffPos.y *= 1f - 1f / Mathf.Max(0.001f, scale.y);
                diffPos.z *= 1f - 1f / Mathf.Max(0.001f, scale.z);

                var size = _particleSystem.particleCount;
                var particles = ParticleSystemExtensions.GetParticleArray(size);
                _particleSystem.GetParticles(particles, size);
                for (var i = 0; i < size; i++)
                {
                    var p = particles[i];
                    p.position += diffPos;
                    particles[i] = p;
                }

                _particleSystem.SetParticles(particles, size);
            }
        }
#endif

        /// <summary>
        /// 애니메이터블 프로퍼티를 MaterialPropertyBlock에서 Material로 동기화
        /// </summary>
        private void UpdateMaterialProperties()
        {
            if (_parent.animatableProperties.Length == 0) return;

            if (s_Mpb == null)
            {
                s_Mpb = new MaterialPropertyBlock();
            }

            _renderer.GetPropertyBlock(s_Mpb);
            if (s_Mpb.isEmpty) return;

            if (!materialForRendering) return;

            for (var i = 0; i < _parent.animatableProperties.Length; i++)
            {
                var ap = _parent.animatableProperties[i];
                ap.UpdateMaterialProperties(materialForRendering, s_Mpb);
            }

            s_Mpb.Clear();
        }
    }
}
