using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;

namespace CAT.UI
{
    /// <summary>
    /// 로비 하단 메인 버튼들의 활성화/비활성화 상태를 관리하고 Activation 이미지의 위치를 제어하는 스크립트
    /// </summary>
    public class LobbyMainButton : MonoBehaviour
    {
        [System.Serializable]
        public class ButtonData
        {
            // [Header("버튼 정보")]
            public Button button;
            public Animator animator;
            
            public bool IsActive { get; set; } = false;
        }

        [Header("버튼 설정")]
        [SerializeField] private List<ButtonData> buttons = new List<ButtonData>();
        [SerializeField] private HorizontalLayoutGroup horizontalLayoutGroup;
        
        // [Header("Activation 이미지")]
        [SerializeField] private RectTransform activationImage;
        
        [Header("애니메이션 설정")]
        [SerializeField] private float moveDuration = 0.2f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;
        
        [Header("애니메이션 트리거 (공통)")]
        [SerializeField] private string activeTrigger = "Active";
        [SerializeField] private string deactiveTrigger = "Deactive";
        
        [Header("초기 활성화 버튼 Idx 설정")]
        [SerializeField] private int defaultActiveButtonIndex = 2; // Btn_Battle이 기본값 (0: Store, 1: Eq, 2: Battle, 3: Rest, 4: Plaza)
        
        [Header("이벤트")]
        public UnityEvent<int> OnButtonActivated;
        public UnityEvent<int> OnButtonDeactivated;
        
        private int currentActiveButtonIndex = -1;
        private Tween activationMoveTween;

        private void Awake()
        {
            InitializeButtons();
        }

        private void Start()
        {
            SetDefaultActiveButton();
        }

        /// <summary>
        /// 버튼들을 초기화하고 이벤트를 연결합니다
        /// </summary>
        private void InitializeButtons()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                int buttonIndex = i; // 클로저를 위한 로컬 변수
                
                if (buttons[i].button != null)
                {
                    buttons[i].button.onClick.AddListener(() => OnButtonClicked(buttonIndex));
                }
                
                if (buttons[i].animator == null)
                {
                    string buttonName = buttons[i].button != null ? buttons[i].button.name : $"Index {i}";
                    Debug.LogWarning($"Button {buttonName}의 Animator가 설정되지 않았습니다.");
                }
            }
        }

        /// <summary>
        /// 기본 활성화 버튼을 설정합니다
        /// </summary>
        private void SetDefaultActiveButton()
        {
            if (defaultActiveButtonIndex >= 0 && defaultActiveButtonIndex < buttons.Count)
            {
                SetActiveButton(defaultActiveButtonIndex, false);
            }
            else
            {
                Debug.LogWarning($"기본 활성화 버튼 인덱스 {defaultActiveButtonIndex}가 유효하지 않습니다.");
            }
        }

        /// <summary>
        /// 버튼 클릭 이벤트 처리
        /// </summary>
        /// <param name="buttonIndex">클릭된 버튼의 인덱스</param>
        private void OnButtonClicked(int buttonIndex)
        {
            if (buttonIndex == currentActiveButtonIndex)
            {
                // 이미 활성화된 버튼을 클릭한 경우 아무것도 하지 않음
                return;
            }

            SetActiveButton(buttonIndex, true);
        }

        /// <summary>
        /// 특정 버튼을 활성화하고 나머지는 비활성화합니다
        /// </summary>
        /// <param name="buttonIndex">활성화할 버튼의 인덱스</param>
        /// <param name="animateActivation">Activation 이미지 애니메이션 여부</param>
        public void SetActiveButton(int buttonIndex, bool animateActivation = true)
        {
            if (buttonIndex < 0 || buttonIndex >= buttons.Count)
            {
                Debug.LogWarning($"버튼 인덱스 {buttonIndex}가 유효하지 않습니다.");
                return;
            }

            // 이전 활성화된 버튼이 있다면 비활성화
            if (currentActiveButtonIndex >= 0 && currentActiveButtonIndex < buttons.Count)
            {
                DeactivateButton(currentActiveButtonIndex);
            }

            // 새 버튼 활성화
            ActivateButton(buttonIndex);
            
            // Activation 이미지 위치 업데이트
            if (animateActivation)
            {
                MoveActivationImage(buttonIndex);
            }
            else
            {
                SetActivationImagePosition(buttonIndex);
            }

            currentActiveButtonIndex = buttonIndex;
        }

        /// <summary>
        /// 버튼을 활성화합니다
        /// </summary>
        /// <param name="buttonIndex">활성화할 버튼의 인덱스</param>
        private void ActivateButton(int buttonIndex)
        {
            var buttonData = buttons[buttonIndex];
            
            if (buttonData.animator != null)
            {
                buttonData.animator.SetTrigger(activeTrigger);
            }
            
            buttonData.IsActive = true;
            OnButtonActivated?.Invoke(buttonIndex);
            
            string buttonName = buttonData.button != null ? buttonData.button.name : $"Index {buttonIndex}";
            Debug.Log($"버튼 {buttonName}이 활성화되었습니다.");
        }

        /// <summary>
        /// 버튼을 비활성화합니다
        /// </summary>
        /// <param name="buttonIndex">비활성화할 버튼의 인덱스</param>
        private void DeactivateButton(int buttonIndex)
        {
            var buttonData = buttons[buttonIndex];
            
            if (buttonData.animator != null)
            {
                buttonData.animator.SetTrigger(deactiveTrigger);
            }
            
            buttonData.IsActive = false;
            OnButtonDeactivated?.Invoke(buttonIndex);
            
            string buttonName = buttonData.button != null ? buttonData.button.name : $"Index {buttonIndex}";
            Debug.Log($"버튼 {buttonName}이 비활성화되었습니다.");
        }

        /// <summary>
        /// Horizontal Layout Group을 기반으로 활성화된 버튼의 중심점을 계산합니다
        /// </summary>
        /// <param name="activeButtonIndex">활성화된 버튼의 인덱스</param>
        /// <returns>계산된 중심점 X 좌표</returns>
        private float CalculateButtonCenterPosition(int activeButtonIndex)
        {
            if (activeButtonIndex < 0 || activeButtonIndex >= buttons.Count)
            {
                Debug.LogWarning($"버튼 인덱스 {activeButtonIndex}가 유효하지 않습니다.");
                return 0f;
            }

            // 활성화된 버튼의 RectTransform 가져오기
            RectTransform activeButtonRect = buttons[activeButtonIndex].button.GetComponent<RectTransform>();
            if (activeButtonRect == null)
            {
                Debug.LogWarning($"버튼 인덱스 {activeButtonIndex}의 RectTransform을 찾을 수 없습니다.");
                return 0f;
            }

            // Activation 이미지의 RectTransform 가져오기
            if (activationImage == null)
            {
                Debug.LogWarning("Activation 이미지가 설정되지 않았습니다.");
                return 0f;
            }

            // 중앙 버튼(Battle, 인덱스 2)을 기준으로 계산
            int centerButtonIndex = 2; // Battle 버튼
            RectTransform centerButtonRect = buttons[centerButtonIndex].button.GetComponent<RectTransform>();
            
            if (centerButtonRect == null)
            {
                Debug.LogWarning("중앙 버튼의 RectTransform을 찾을 수 없습니다.");
                return 0f;
            }

            // 중앙 버튼의 위치
            float centerX = centerButtonRect.anchoredPosition.x;
            
            // 활성화된 버튼의 위치
            float activeX = activeButtonRect.anchoredPosition.x;
            
            // 중앙을 기준으로 한 상대적 위치 계산
            float relativePosition = activeX - centerX;
            
            Debug.Log($"버튼 {activeButtonIndex} 위치 계산 (중앙 기준):");
            Debug.Log($"  - 중앙 버튼({centerButtonIndex}) X: {centerX}");
            Debug.Log($"  - 활성화 버튼({activeButtonIndex}) X: {activeX}");
            Debug.Log($"  - 상대적 위치: {relativePosition}");
            
            return relativePosition;
        }

        /// <summary>
        /// Activation 이미지를 애니메이션과 함께 이동시킵니다
        /// </summary>
        /// <param name="buttonIndex">이동할 버튼의 인덱스</param>
        private void MoveActivationImage(int buttonIndex)
        {
            if (activationImage == null)
            {
                Debug.LogWarning("Activation 이미지가 설정되지 않았습니다.");
                return;
            }

            var targetButton = buttons[buttonIndex];
            if (targetButton.button == null)
            {
                Debug.LogWarning($"버튼 인덱스 {buttonIndex}의 Button이 설정되지 않았습니다.");
                return;
            }

            // Layout Group 기반으로 중심점 계산
            float targetXPosition = CalculateButtonCenterPosition(buttonIndex);
            
            // 기존 애니메이션 중지
            activationMoveTween?.Kill();

            // 새 애니메이션 시작 - X 좌표만 애니메이션
            activationMoveTween = activationImage.DOAnchorPosX(targetXPosition, moveDuration)
                .SetEase(moveEase)
                .OnComplete(() => {
                    string buttonName = targetButton.button != null ? targetButton.button.name : $"Index {buttonIndex}";
                    Debug.Log($"Activation 이미지가 버튼 {buttonName} 위치로 이동했습니다. (X: {targetXPosition})");
                });
        }

        /// <summary>
        /// Activation 이미지 위치를 즉시 설정합니다 (애니메이션 없음)
        /// </summary>
        /// <param name="buttonIndex">설정할 버튼의 인덱스</param>
        private void SetActivationImagePosition(int buttonIndex)
        {
            if (activationImage == null)
            {
                Debug.LogWarning("Activation 이미지가 설정되지 않았습니다.");
                return;
            }

            var targetButton = buttons[buttonIndex];
            if (targetButton.button == null)
            {
                Debug.LogWarning($"버튼 인덱스 {buttonIndex}의 Button이 설정되지 않았습니다.");
                return;
            }

            // Layout Group 기반으로 중심점 계산
            float targetXPosition = CalculateButtonCenterPosition(buttonIndex);
            
            // X 좌표만 즉시 설정 (Y는 유지)
            Vector2 currentPosition = activationImage.anchoredPosition;
            currentPosition.x = targetXPosition;
            activationImage.anchoredPosition = currentPosition;
        }

        /// <summary>
        /// 현재 활성화된 버튼의 인덱스를 반환합니다
        /// </summary>
        /// <returns>활성화된 버튼의 인덱스, 없으면 -1</returns>
        public int GetCurrentActiveButtonIndex()
        {
            return currentActiveButtonIndex;
        }

        /// <summary>
        /// 특정 버튼이 활성화 상태인지 확인합니다
        /// </summary>
        /// <param name="buttonIndex">확인할 버튼의 인덱스</param>
        /// <returns>활성화 상태 여부</returns>
        public bool IsButtonActive(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= buttons.Count)
                return false;
                
            return buttons[buttonIndex].IsActive;
        }

        /// <summary>
        /// 이동 애니메이션 지속 시간을 설정합니다
        /// </summary>
        /// <param name="duration">새로운 지속 시간</param>
        public void SetMoveDuration(float duration)
        {
            moveDuration = Mathf.Max(0f, duration);
        }

        /// <summary>
        /// 이동 애니메이션 이징을 설정합니다
        /// </summary>
        /// <param name="ease">새로운 이징</param>
        public void SetMoveEase(Ease ease)
        {
            moveEase = ease;
        }

        /// <summary>
        /// 애니메이션 트리거 이름을 설정합니다
        /// </summary>
        /// <param name="active">활성화 트리거 이름</param>
        /// <param name="deactive">비활성화 트리거 이름</param>
        public void SetAnimationTriggers(string active, string deactive)
        {
            activeTrigger = active;
            deactiveTrigger = deactive;
        }

        private void OnDestroy()
        {
            // 애니메이션 정리
            activationMoveTween?.Kill();
            
            // 버튼 이벤트 정리
            foreach (var buttonData in buttons)
            {
                if (buttonData.button != null)
                {
                    buttonData.button.onClick.RemoveAllListeners();
                }
            }
        }

        #if UNITY_EDITOR
        [Header("디버그 정보")]
        [SerializeField, ReadOnly] private int debugCurrentActiveIndex = -1;
        
        private void OnValidate()
        {
            debugCurrentActiveIndex = currentActiveButtonIndex;
            
            // 에디터에서 기본 활성화 버튼 인덱스 유효성 검사
            if (defaultActiveButtonIndex < 0 || defaultActiveButtonIndex >= buttons.Count)
            {
                Debug.LogWarning($"기본 활성화 버튼 인덱스 {defaultActiveButtonIndex}가 유효하지 않습니다. 버튼 개수: {buttons.Count}");
            }
        }
        #endif
    }
}