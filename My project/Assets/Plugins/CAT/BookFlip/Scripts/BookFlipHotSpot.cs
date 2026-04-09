using UnityEngine;
using UnityEngine.EventSystems;

namespace CAT.BookFlip
{
    /// <summary>
    /// 페이지 넘김 핫스팟 컴포넌트
    /// 항상 최상위에 위치하여 페이지 콘텐츠와 관계없이 드래그 이벤트를 받을 수 있습니다
    /// </summary>
    public class BookFlipHotSpot : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public enum HotSpotType
        {
            Left,
            Right
        }

        [SerializeField] private HotSpotType _type = HotSpotType.Right;
        [SerializeField] private BookFlip _bookFlip;

        private bool _isDragging = false;

        private void Start()
        {
            if (_bookFlip == null)
                _bookFlip = GetComponentInParent<BookFlip>();

            if (_bookFlip == null)
                Debug.LogError("[BookFlipHotSpot] BookFlip 컴포넌트를 찾을 수 없습니다.");
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_bookFlip == null || !_bookFlip.Interactable)
                return;

            _isDragging = true;

            if (_type == HotSpotType.Right)
                _bookFlip.OnMouseDragRightPage();
            else
                _bookFlip.OnMouseDragLeftPage();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _bookFlip == null || !_bookFlip.Interactable)
                return;

            // BookFlip의 Update에서 드래그 처리됨
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDragging)
                return;

            _isDragging = false;

            if (_bookFlip != null && _bookFlip.Interactable)
                _bookFlip.OnMouseRelease();

            // EventSystem 포커스 해제
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 에디터에서 핫스팟 영역 시각화
            RectTransform rt = GetComponent<RectTransform>();
            if (rt == null) return;

            Gizmos.color = _type == HotSpotType.Right ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);

            // 레이블
            Vector3 center = (corners[0] + corners[2]) / 2;
            UnityEditor.Handles.Label(center, _type == HotSpotType.Right ? "Right HotSpot" : "Left HotSpot");
        }
#endif
    }
}
