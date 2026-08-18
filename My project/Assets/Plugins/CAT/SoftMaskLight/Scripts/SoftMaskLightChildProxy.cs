using UnityEngine;
using UnityEngine.UI;

namespace SoftMaskLight
{
    /// <summary>
    /// 일반 자식 Graphic에 SoftMaskLight 마스킹을 적용하기 위한 프록시 컴포넌트
    /// IMaterialModifier 체인에 삽입되어 원본 셰이더의 Hidden 변형(Optional Shader)으로
    /// 교체한 프록시 머티리얼을 반환. graphic.m_Material은 건드리지 않음.
    ///
    /// 동작 순서:
    /// 1. SoftMaskLight.ApplyMaskToChildren() → 자식에 이 프록시 추가 + Initialize()
    /// 2. Canvas 리빌드 → GetModifiedMaterial(baseMaterial) 호출
    /// 3. baseMaterial의 셰이더에 대응하는 Hidden 변형 셰이더를 찾고
    ///    SoftMaskLight의 공유 캐시에서 프록시 Material을 조회/생성
    /// 4. 마스크 프로퍼티 적용 후 프록시 Material 반환
    /// 5. materialForRendering = 프록시 머티리얼 (baseMaterial 유지)
    ///
    /// 배칭: 동일한 baseMaterial을 가진 자식끼리 SoftMaskLight의 공유 캐시를 통해
    /// 같은 프록시 Material을 공유 (GetOrCreateProxyMaterial)
    /// </summary>
    [ExecuteAlways]
    [HideInInspector]
    internal sealed class SoftMaskLightChildProxy : MonoBehaviour, IMaterialModifier
    {
        private SoftMaskLight _softMask;
        private Material _currentProxyMaterial;
        private bool _isCleanedUp;
        // Graphic 참조 캐시 (반복 GetComponent 방지)
        private Graphic _graphic;

        private Graphic OwnerGraphic
        {
            get
            {
                if (_graphic == null) _graphic = GetComponent<Graphic>();
                return _graphic;
            }
        }

        /// <summary>
        /// 마지막으로 반환한 프록시 머티리얼 참조
        /// SoftMaskLight가 프록시 머티리얼 식별/정리에 사용
        /// </summary>
        public Material ProxyMaterial => _currentProxyMaterial;

        /// <summary>
        /// Cleanup() 호출 후 Destroy 대기 중인 zombie 프록시 여부
        /// </summary>
        internal bool IsCleanedUp => _isCleanedUp;

        /// <summary>
        /// 연결된 SoftMaskLight 참조
        /// </summary>
        internal SoftMaskLight SoftMask => _softMask;

        /// <summary>
        /// SoftMaskLight.ApplyMaskToChildren()에서 호출하여 부모 SoftMaskLight 참조를 주입
        /// </summary>
        public void Initialize(SoftMaskLight mask)
        {
            _softMask = mask;
            _isCleanedUp = false;
            // 씬/프리팹에 직렬화되지 않도록 설정 (플러그인 제거 시 missing script 방지)
            hideFlags = HideFlags.DontSave;
        }

        /// <summary>
        /// GO 재활성화 시 IMaterialModifier 체인 재빌드 보장
        /// </summary>
        private void OnEnable()
        {
            // 과거 버전에서 씬에 직렬화된 프록시 업그레이드 경로
            hideFlags = HideFlags.DontSave;

            if (_isCleanedUp || _softMask == null) return;

            var graphic = OwnerGraphic;
            if (graphic != null) graphic.SetMaterialDirty();
        }

        /// <summary>
        /// 부모 변경 시 마스크 밖으로 이동했는지 확인하고 마스크에 즉시 통보
        /// (플레이모드에서 SetParent로 이탈 시 원본 Material 즉시 복원)
        /// </summary>
        private void OnTransformParentChanged()
        {
            if (_isCleanedUp || _softMask == null) return;

            var graphic = OwnerGraphic;
            if (graphic != null) _softMask.NotifyChildMovedOut(graphic);
        }

        /// <summary>
        /// IMaterialModifier 구현
        /// baseMaterial(= graphic.m_Material)의 셰이더에 대응하는 Hidden 변형 셰이더를 찾고
        /// SoftMaskLight의 공유 캐시에서 프록시 Material을 생성/재사용
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // 정리 완료 또는 SoftMaskLight 비활성 → 패스스루
            if (_isCleanedUp || _softMask == null || !_softMask.enabled)
            {
                _currentProxyMaterial = null;
                return baseMaterial;
            }

            if (baseMaterial == null)
            {
                _currentProxyMaterial = null;
                return baseMaterial;
            }

            // baseMaterial의 셰이더에 대응하는 Hidden 변형 셰이더 탐색
            Shader optShader = SoftMaskLight.FindOptionalShader(baseMaterial.shader);
            if (optShader == null)
            {
                _currentProxyMaterial = null;
                return baseMaterial;
            }

            // SoftMaskLight의 공유 캐시에서 프록시 Material 조회/생성 (배칭 보장)
            Material proxy = _softMask.GetOrCreateProxyMaterial(baseMaterial, optShader);
            _currentProxyMaterial = proxy;
            return proxy ?? baseMaterial;
        }

        /// <summary>
        /// SoftMaskLight.RestoreChildrenMaterials()에서 호출하여 프록시 정리 및 컴포넌트 제거
        /// </summary>
        public void Cleanup()
        {
            _isCleanedUp = true;
            _currentProxyMaterial = null;

            var graphic = OwnerGraphic;
            if (graphic != null) graphic.SetMaterialDirty();

            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
        }

        private void OnDestroy()
        {
            _currentProxyMaterial = null;
        }
    }
}
