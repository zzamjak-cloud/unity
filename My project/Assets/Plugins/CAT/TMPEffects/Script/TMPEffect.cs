using UnityEngine;
using UnityEngine.UI;

namespace CAT.UI
{
    /// <summary>
    /// TextMeshPro 텍스트 효과 베이스 클래스
    /// - 모바일 게임 최적화: 더티 체크, Material 공유
    /// - SoftMask 패턴 기반
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public abstract class TMPEffect : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // 캐싱
        // ─────────────────────────────────────────────

        protected Graphic _graphic;
        protected bool _isDirty = true;

        // ─────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────

        protected virtual void Awake()
        {
            CacheComponents();
        }

        protected virtual void OnEnable()
        {
            CacheComponents();
            SetDirty();
        }

        protected virtual void OnDisable()
        {
            SetDirty();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            CacheComponents();
            SetDirty();
        }

        protected virtual void Reset()
        {
            CacheComponents();
            SetDirty();
        }
#endif

        // ─────────────────────────────────────────────
        // 내부 메서드
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_graphic == null)
            {
                _graphic = GetComponent<Graphic>();
            }
        }

        /// <summary>
        /// 더티 플래그 설정 및 Graphic 업데이트 요청
        /// </summary>
        protected void SetDirty()
        {
            _isDirty = true;

            if (_graphic != null)
            {
                _graphic.SetVerticesDirty();
                _graphic.SetMaterialDirty();
            }
        }

        /// <summary>
        /// Material 업데이트 요청 (셰이더 효과용)
        /// </summary>
        protected void SetMaterialDirty()
        {
            if (_graphic != null)
            {
                _graphic.SetMaterialDirty();
            }
        }

        /// <summary>
        /// 메시 업데이트 요청 (메시 효과용)
        /// </summary>
        protected void SetVerticesDirty()
        {
            if (_graphic != null)
            {
                _graphic.SetVerticesDirty();
            }
        }
    }
}
