using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace CAT.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI), typeof(LayoutElement))]
    public class UITMPLayoutLimiter : MonoBehaviour
    {
        [Tooltip("최대 너비. 이 값에 도달하면 크기가 고정됩니다.")]
        public float maxWidth = 300f;

        [Tooltip("최대 높이. 이 값에 도달하면 크기가 고정됩니다.")]
        public float maxHeight = 40f;

        private TextMeshProUGUI tmpText;
        private LayoutElement layoutElement;

        private string previousText = null;
        private bool isLayoutDirty = true; // 시작 시 또는 텍스트 변경 시 레이아웃을 업데이트해야 함을 표시

        void Awake()
        {
            tmpText = GetComponent<TextMeshProUGUI>();
            layoutElement = GetComponent<LayoutElement>();
        }

        // LateUpdate를 사용하여 다른 모든 Update 로직이 끝난 후, 최종적으로 UI 레이아웃을 결정합니다.
        // 이는 한 프레임 내의 여러 변경사항을 종합하여 처리하므로 더 안정적입니다.
        void LateUpdate()
        {
            // 텍스트가 변경되었는지 확인
            if (tmpText.text != previousText)
            {
                previousText = tmpText.text;
                isLayoutDirty = true;
            }

            // 레이아웃 업데이트가 필요할 때만 실행
            if (isLayoutDirty)
            {
                CheckAndUpdateLayout();
                isLayoutDirty = false; // 업데이트 완료 후 플래그 초기화
            }
        }

        private void CheckAndUpdateLayout()
        {
            // --- 핵심 로직: 깜빡임 방지를 위해 최대 폰트 크기일 때의 크기를 계산 ---
            // 1. 현재 폰트 크기를 보관합니다.
            float originalFontSize = tmpText.fontSize;

            // 2. 계산을 위해 일시적으로 폰트 크기를 Auto Size의 Max 값으로 설정합니다.
            tmpText.fontSize = tmpText.fontSizeMax;

            // 3. 현재 텍스트를 기준으로, 최대 크기일 때의 예상 너비/높이를 계산합니다.
            // 이 오버로드는 인자로 텍스트 문자열만 받습니다.
            Vector2 potentialSize = tmpText.GetPreferredValues(tmpText.text);

            // 4. 화면에 변경이 렌더링되기 전에, 원래 폰트 크기로 즉시 복원합니다.
            tmpText.fontSize = originalFontSize;
            // --- 핵심 로직 끝 ---

            // --- 너비(Width) 제어 ---
            if (potentialSize.x >= maxWidth)
            {
                if (layoutElement.preferredWidth != maxWidth)
                {
                    layoutElement.preferredWidth = maxWidth;
                }
            }
            else
            {
                if (layoutElement.preferredWidth != -1)
                {
                    layoutElement.preferredWidth = -1;
                }
            }

            // --- 높이(Height) 제어 ---
            if (potentialSize.y >= maxHeight)
            {
                if (layoutElement.preferredHeight != maxHeight)
                {
                    layoutElement.preferredHeight = maxHeight;
                }
            }
            else
            {
                if (layoutElement.preferredHeight != -1)
                {
                    layoutElement.preferredHeight = -1;
                }
            }
        }
    }
}