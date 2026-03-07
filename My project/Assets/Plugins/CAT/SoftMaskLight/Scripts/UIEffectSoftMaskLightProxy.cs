using UnityEngine;
using UnityEngine.UI;

namespace SoftMaskLight
{
    /// <summary>
    /// UIEffect 자식에 SoftMaskLight를 적용하기 위한 프록시 컴포넌트
    /// IMaterialModifier 체인에 삽입되어 UIEffect가 생성한 머티리얼을 복제한 후
    /// _CAT_SOFTMASK 키워드를 활성화하고 마스크 프로퍼티를 적용한 프록시 머티리얼을 반환
    ///
    /// 동작 순서:
    /// 1. UIEffect.GetModifiedMaterial(base) → UIEffectMat 생성 (매 캔버스 리빌드마다 프로퍼티 갱신)
    /// 2. UIEffectSoftMaskLightProxy.GetModifiedMaterial(UIEffectMat) → 복제 + 키워드 활성화 + 마스크 프로퍼티
    /// 3. materialForRendering = 프록시 머티리얼
    ///
    /// 핵심: 셰이더를 교체하지 않음 — UIEffect의 shader_feature 변형을 보존하기 위해
    /// 원본 UIEffect 셰이더에 내장된 _CAT_SOFTMASK multi_compile 키워드만 활성화
    /// </summary>
    [ExecuteAlways]
    [HideInInspector]
    internal sealed class UIEffectSoftMaskLightProxy : MonoBehaviour, IMaterialModifier
    {
        private const string SOFTMASK_KEYWORD = "_CAT_SOFTMASK";

        private SoftMaskLight _softMask;
        // 현재 적용 중인 프록시 머티리얼 (SoftMaskLight.cs가 프로퍼티 전파에 사용)
        private Material _proxiedMaterial;
        // Cleanup() 호출 여부 (zombie 프록시 감지용)
        private bool _isCleanedUp;

        /// <summary>
        /// SoftMaskLight.cs가 프로퍼티 전파 시 참조하는 프록시 머티리얼
        /// </summary>
        public Material ProxyMaterial => _proxiedMaterial;

        /// <summary>
        /// Cleanup() 호출 후 Destroy 대기 중인 zombie 프록시 여부
        /// SoftMaskLight.ApplyMaskToUIEffect()에서 재사용 방지용
        /// </summary>
        internal bool IsCleanedUp => _isCleanedUp;

        /// <summary>
        /// SoftMaskLight.ApplyMaskToUIEffect()에서 호출하여 부모 SoftMaskLight 참조를 주입
        /// </summary>
        public void Initialize(SoftMaskLight mask)
        {
            _softMask = mask;
        }

        /// <summary>
        /// GO 재활성화 시 IMaterialModifier 체인 재빌드 보장
        /// </summary>
        private void OnEnable()
        {
            if (_isCleanedUp || _softMask == null) return;

            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.SetMaterialDirty();
        }

        /// <summary>
        /// IMaterialModifier 구현
        /// UIEffect가 생성한 머티리얼(baseMaterial)을 복제/동기화하고
        /// _CAT_SOFTMASK 키워드를 활성화 + SoftMaskLight 마스크 프로퍼티를 적용
        /// 셰이더를 교체하지 않아 UIEffect의 shader_feature 변형이 보존됨
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // 정리 완료 또는 SoftMaskLight 비활성 → 패스스루
            if (_isCleanedUp || _softMask == null || !_softMask.enabled)
                return baseMaterial;

            if (baseMaterial == null)
                return baseMaterial;

            // 프록시 머티리얼이 없으면 최초 생성
            if (_proxiedMaterial == null)
            {
                _proxiedMaterial = new Material(baseMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            else
            {
                // UIEffect가 매 캔버스 리빌드마다 프로퍼티를 갱신하므로 프록시도 동기화
                _proxiedMaterial.CopyPropertiesFromMaterial(baseMaterial);
                _proxiedMaterial.shaderKeywords = baseMaterial.shaderKeywords;
            }

            // _CAT_SOFTMASK 키워드 활성화 (셰이더 변경 없음)
            _proxiedMaterial.EnableKeyword(SOFTMASK_KEYWORD);

            // SoftMaskLight 마스크 프로퍼티 적용
            _softMask.ApplyMaskPropertiesToMaterial(_proxiedMaterial);

            return _proxiedMaterial;
        }

        /// <summary>
        /// SoftMaskLight.RestoreChildrenMaterials()에서 호출하여 프록시 정리 및 컴포넌트 제거
        /// </summary>
        public void Cleanup()
        {
            _isCleanedUp = true;
            DestroyProxyMaterial();

            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.SetMaterialDirty();

            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
        }

        private void OnDestroy()
        {
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
        }
    }
}
