using UnityEngine;
using UnityEngine.UI;

namespace SoftMaskLight
{
    /// <summary>
    /// UIEffect 자식에 SoftMaskLight를 적용하기 위한 프록시 컴포넌트
    /// IMaterialModifier 체인에 삽입되어 UIEffect가 생성한 머티리얼에 대응하는
    /// 공유 프록시 머티리얼(SoftMaskLight 소유)을 체인에 반환한다.
    ///
    /// 동작 순서:
    /// 1. UIEffect.GetModifiedMaterial(base) → UIEffectMat 생성 (매 캔버스 리빌드마다 프로퍼티 갱신)
    /// 2. UIEffectSoftMaskLightProxy.GetModifiedMaterial(UIEffectMat)
    ///    → SoftMaskLight.GetOrCreateUIEffectProxyMaterial()로 공유 프록시 조회/생성
    ///    (같은 UIEffectMat을 쓰는 자식끼리 프록시 공유 → 배칭 유지)
    /// 3. materialForRendering = 공유 프록시 머티리얼
    ///
    /// 프록시 머티리얼의 소유권/파괴는 SoftMaskLight가 담당한다 (이 컴포넌트는 파괴하지 않음).
    /// </summary>
    [ExecuteAlways]
    [HideInInspector]
    internal sealed class UIEffectSoftMaskLightProxy : MonoBehaviour, IMaterialModifier
    {
        // 마스킹 불가 경고 1회 제한용
        private bool _warned;

        private SoftMaskLight _softMask;
        // 마지막으로 체인에 반환한 공유 프록시 머티리얼 (재생성 감지용 — 소유권 없음)
        private Material _proxiedMaterial;
        // Cleanup() 호출 여부 (zombie 프록시 감지용)
        private bool _isCleanedUp;
        // Graphic 참조 캐시 (매 프레임 GetComponent 방지)
        private Graphic _graphic;

        /// <summary>
        /// 마지막으로 체인에 반환한 공유 프록시 머티리얼 (재생성 감지용)
        /// </summary>
        public Material ProxyMaterial => _proxiedMaterial;

        /// <summary>
        /// 캐시된 Graphic 참조 (SoftMaskLight가 SetMaterialDirty 트리거에 사용)
        /// </summary>
        internal Graphic OwnerGraphic
        {
            get
            {
                if (_graphic == null) _graphic = GetComponent<Graphic>();
                return _graphic;
            }
        }

        /// <summary>
        /// Cleanup() 호출 후 Destroy 대기 중인 zombie 프록시 여부
        /// SoftMaskLight.ApplyMaskToUIEffect()에서 재사용 방지용
        /// </summary>
        internal bool IsCleanedUp => _isCleanedUp;

        /// <summary>
        /// 연결된 SoftMaskLight 참조 (소유권 확인용)
        /// </summary>
        internal SoftMaskLight SoftMask => _softMask;

        /// <summary>
        /// SoftMaskLight.ApplyMaskToUIEffect()에서 호출하여 부모 SoftMaskLight 참조를 주입
        /// </summary>
        public void Initialize(SoftMaskLight mask)
        {
            _softMask = mask;
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
        /// </summary>
        private void OnTransformParentChanged()
        {
            if (_isCleanedUp || _softMask == null) return;

            var graphic = OwnerGraphic;
            if (graphic != null) _softMask.NotifyChildMovedOut(graphic);
        }

        /// <summary>
        /// IMaterialModifier 구현
        /// SoftMaskLight가 소유한 공유 프록시 머티리얼을 조회/생성하여 체인에 반환.
        /// 패키지 UIEffect 셰이더인 경우 SoftMaskLight가 오버라이드 셰이더로 교체한다
        /// (키워드는 머티리얼에 보존되므로 UIEffect의 shader_feature 변형 유지).
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // 정리 완료 또는 SoftMaskLight 비활성 → 패스스루
            if (_isCleanedUp || _softMask == null || !_softMask.enabled)
                return baseMaterial;

            if (baseMaterial == null)
                return baseMaterial;

            Material proxy = _softMask.GetOrCreateUIEffectProxyMaterial(baseMaterial);
            if (proxy == null)
            {
                // 오버라이드 셰이더 부재 → 마스킹 불가, 1회만 경고 후 패스스루
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning(
                        $"[SoftMaskLight] '{name}'의 UIEffect 머티리얼에 마스킹을 적용할 수 없습니다 " +
                        $"(shader: {(baseMaterial.shader != null ? baseMaterial.shader.name : "null")}). " +
                        "SoftMaskLightSettings의 UIEffect 오버라이드 셰이더 참조가 비어 있습니다. " +
                        "Tools > SoftMaskLight > Refresh Settings 실행 후 확인하세요.", this);
                }
                return baseMaterial;
            }

            _proxiedMaterial = proxy;
            return proxy;
        }

        /// <summary>
        /// SoftMaskLight.RestoreChildrenMaterials()에서 호출하여 프록시 정리 및 컴포넌트 제거
        /// 공유 프록시 머티리얼의 파괴는 SoftMaskLight가 담당하므로 여기서는 참조만 해제
        /// </summary>
        public void Cleanup()
        {
            _isCleanedUp = true;
            _proxiedMaterial = null;

            var graphic = OwnerGraphic;
            if (graphic != null) graphic.SetMaterialDirty();

            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
        }
    }
}
