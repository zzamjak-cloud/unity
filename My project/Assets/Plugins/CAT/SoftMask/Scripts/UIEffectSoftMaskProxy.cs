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

        /// <summary>
        /// SoftMask.cs가 프로퍼티 전파 시 참조하는 프록시 머티리얼
        /// </summary>
        public Material ProxyMaterial => _proxiedMaterial;

        /// <summary>
        /// SoftMask.ApplyMaskToUIEffect()에서 호출하여 부모 SoftMask 참조를 주입
        /// </summary>
        public void Initialize(SoftMask mask)
        {
            _softMask = mask;
        }

        /// <summary>
        /// IMaterialModifier 구현
        /// UIEffect가 생성한 머티리얼(baseMaterial)을 복제/동기화하고
        /// _CAT_SOFTMASK 키워드 활성화 + SoftMask 마스크 프로퍼티를 적용
        ///
        /// 핵심: UIEffect는 매 캔버스 리빌드마다 CopyPropertiesFromMaterial + ApplyContextToMaterial로
        /// 자신의 머티리얼 속성을 갱신함. 프록시도 매번 동기화해야 애니메이션/동적 속성이 정상 작동함.
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (_softMask == null || !_softMask.enabled)
                return baseMaterial;

            // UIEffect가 머티리얼 인스턴스를 교체했으면 새 프록시 머티리얼 생성
            if (baseMaterial != _lastInputMaterial)
            {
                DestroyProxyMaterial();
                _lastInputMaterial = baseMaterial;

                if (baseMaterial != null)
                {
                    _proxiedMaterial = new Material(baseMaterial)
                    {
                        // 에디터 하이어라키/인스펙터에서 숨김, 씬 저장 제외
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
            else if (_proxiedMaterial != null && baseMaterial != null)
            {
                // UIEffect가 매 캔버스 리빌드마다 _material 속성을 갱신하므로
                // 프록시도 동기화 (애니메이션, 블렌드 모드, 좌표 변환 행렬 등)
                _proxiedMaterial.CopyPropertiesFromMaterial(baseMaterial);
            }

            // _CAT_SOFTMASK 키워드 활성화 + SoftMask 마스크 프로퍼티 적용
            // CopyPropertiesFromMaterial이 키워드를 초기화하므로 매번 재활성화 필요
            if (_proxiedMaterial != null)
            {
                _proxiedMaterial.EnableKeyword("_CAT_SOFTMASK");
                _softMask.ApplyMaskPropertiesToMaterial(_proxiedMaterial);
            }

            return _proxiedMaterial != null ? _proxiedMaterial : baseMaterial;
        }

        /// <summary>
        /// SoftMask.RestoreChildrenMaterials()에서 호출하여 프록시 정리 및 컴포넌트 제거
        /// </summary>
        public void Cleanup()
        {
            DestroyProxyMaterial();
            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
        }

        private void OnDestroy()
        {
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
