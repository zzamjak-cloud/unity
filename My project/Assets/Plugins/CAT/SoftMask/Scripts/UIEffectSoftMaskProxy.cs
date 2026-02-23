using UnityEngine;
using UnityEngine.UI;

namespace CAT.UI
{
    /// <summary>
    /// UIEffect 자식에 CAT SoftMask를 적용하기 위한 프록시 컴포넌트
    /// IMaterialModifier 체인에 삽입되어 UIEffect가 생성한 머티리얼을 복제한 후
    /// _CAT_SOFTMASK 키워드를 활성화하고 마스크 프로퍼티를 적용한 프록시 머티리얼을 반환
    ///
    /// 동작 순서:
    /// 1. UIEffect.GetModifiedMaterial(base) → UIEffectMat 생성 (매 캔버스 리빌드마다 프로퍼티 갱신)
    /// 2. UIEffectSoftMaskProxy.GetModifiedMaterial(UIEffectMat) → 동기화 + 키워드 + 마스크 프로퍼티
    /// 3. materialForRendering = 프록시 머티리얼
    /// </summary>
    [ExecuteAlways]
    [HideInInspector] // 하이어라키/인스펙터에서 내부 컴포넌트임을 표시
    internal sealed class UIEffectSoftMaskProxy : MonoBehaviour, IMaterialModifier
    {
        private SoftMask _softMask;
        // 현재 적용 중인 프록시 머티리얼 (SoftMask.cs가 프로퍼티 전파에 사용)
        private Material _proxiedMaterial;
        // UIEffect 입력 머티리얼 인스턴스 변경 감지용 캐시
        private Material _lastInputMaterial;
        // Cleanup() 호출 여부 (zombie 프록시 감지용)
        // Destroy(this)는 프레임 끝까지 지연되므로, 그 사이 OnEnable()에서
        // zombie 프록시를 재사용하면 OnDestroy()가 새 머티리얼을 파괴하는 문제 발생
        private bool _isCleanedUp;

        /// <summary>
        /// SoftMask.cs가 프로퍼티 전파 시 참조하는 프록시 머티리얼
        /// </summary>
        public Material ProxyMaterial => _proxiedMaterial;

        /// <summary>
        /// Cleanup() 호출 후 Destroy 대기 중인 zombie 프록시 여부
        /// SoftMask.ApplyMaskToUIEffect()에서 재사용 방지용
        /// </summary>
        internal bool IsCleanedUp => _isCleanedUp;

        /// <summary>
        /// SoftMask.ApplyMaskToUIEffect()에서 호출하여 부모 SoftMask 참조를 주입
        /// </summary>
        public void Initialize(SoftMask mask)
        {
            _softMask = mask;
        }

        /// <summary>
        /// GO 재활성화 시 IMaterialModifier 체인 재빌드 보장
        /// Graphic.OnEnable() → SetAllDirty()만으로는 프록시 머티리얼이 갱신되지 않는 경우 대비
        /// AddComponent 직후에는 _softMask가 null이므로 동작하지 않음 (Initialize 이후 활성)
        /// </summary>
        private void OnEnable()
        {
            // zombie 프록시 또는 미초기화 상태 → 무시
            if (_isCleanedUp || _softMask == null) return;

            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.SetMaterialDirty();
        }

        /// <summary>
        /// IMaterialModifier 구현
        /// UIEffect가 생성한 머티리얼(baseMaterial)을 복제/동기화하고
        /// _CAT_SOFTMASK 키워드 활성화 + SoftMask 마스크 프로퍼티를 적용
        ///
        /// 핵심: 프록시 머티리얼은 한 번 생성 후 컴포넌트 수명 동안 재사용.
        /// UIEffect가 입력 머티리얼을 교체해도 기존 프록시 머티리얼의 프로퍼티만 갱신.
        /// → Destroy() 지연 호출로 인한 타이밍 문제 원천 차단.
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // 정리 완료 또는 SoftMask 비활성 → 패스스루
            if (_isCleanedUp || _softMask == null || !_softMask.enabled)
                return baseMaterial;

            if (baseMaterial == null)
                return baseMaterial;

            // 프록시 머티리얼이 없으면 최초 생성 (컴포넌트 수명 동안 1회)
            if (_proxiedMaterial == null)
            {
                _proxiedMaterial = new Material(baseMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            else
            {
                // UIEffect 입력 머티리얼이 교체되었으면 셰이더도 동기화
                if (baseMaterial != _lastInputMaterial && baseMaterial.shader != _proxiedMaterial.shader)
                    _proxiedMaterial.shader = baseMaterial.shader;

                // UIEffect가 매 캔버스 리빌드마다 프로퍼티를 갱신하므로 프록시도 동기화
                _proxiedMaterial.CopyPropertiesFromMaterial(baseMaterial);
            }

            _lastInputMaterial = baseMaterial;

            // _CAT_SOFTMASK 키워드 활성화 + SoftMask 마스크 프로퍼티 적용
            // CopyPropertiesFromMaterial이 키워드를 초기화하므로 매번 재활성화 필요
            _proxiedMaterial.EnableKeyword("_CAT_SOFTMASK");
            _softMask.ApplyMaskPropertiesToMaterial(_proxiedMaterial);

            return _proxiedMaterial;
        }

        /// <summary>
        /// SoftMask.RestoreChildrenMaterials()에서 호출하여 프록시 정리 및 컴포넌트 제거
        /// _isCleanedUp 플래그로 zombie 상태 표시 → OnDestroy에서 새 머티리얼 파괴 방지
        /// </summary>
        public void Cleanup()
        {
            _isCleanedUp = true;
            DestroyProxyMaterial();

            // 프록시 제거 후 canvasRenderer가 파괴된 프록시 머티리얼을 참조하지 않도록
            // canvas 재빌드 트리거 (GO가 비활성이면 IsActive() false → no-op, 재활성화 시
            // Graphic.OnEnable() → SetAllDirty()에서 자동 처리됨)
            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.SetMaterialDirty();

            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
        }

        private void OnDestroy()
        {
            // Cleanup()을 통해 정리된 경우 이미 머티리얼 파괴 완료
            // Cleanup 없이 직접 파괴되는 경우에만 머티리얼 정리
            // (zombie 프록시가 OnEnable에서 재사용 후 새 머티리얼을 생성했을 때 파괴 방지)
            if (!_isCleanedUp)
                DestroyProxyMaterial();
        }

        private void DestroyProxyMaterial()
        {
            if (_proxiedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_proxiedMaterial);
                else
                    DestroyImmediate(_proxiedMaterial);
                _proxiedMaterial = null;
            }
            _lastInputMaterial = null;
        }
    }
}
