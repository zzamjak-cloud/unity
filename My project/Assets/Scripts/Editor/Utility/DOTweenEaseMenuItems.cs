using UnityEngine;
using UnityEditor;
using DG.Tweening;

namespace CAT.Utility
{
    public static class DOTweenEaseMenuItems
    {
        // DOTween Animation 컴포넌트가 선택됐을 때만 메뉴 활성화
        //[MenuItem("Tools/DOTween/이징 그래프 선택기 열기 %#e", true)]
        private static bool ValidateOpenEasingSelector()
        {
            // 선택된 게임 오브젝트 중 DOTweenAnimation 컴포넌트가 있는지 확인
            foreach (var obj in Selection.gameObjects)
            {
                if (obj.GetComponent<DOTweenAnimation>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        // 이징 그래프 선택기 열기 메뉴 아이템 (단축키: Ctrl+Shift+E 또는 Cmd+Shift+E)
        [MenuItem("Tools/DOTween/이징 그래프 선택기 열기 %#e", false, 100)]
        private static void OpenEasingSelector()
        {
            // 선택된 게임 오브젝트 가져오기
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
                return;

            // 게임 오브젝트에서 모든 DOTweenAnimation 컴포넌트 가져오기
            DOTweenAnimation[] animations = selectedObject.GetComponents<DOTweenAnimation>();
            if (animations.Length == 0)
                return;

            // 여러 개의 컴포넌트가 있을 경우 선택 메뉴 표시
            if (animations.Length > 1)
            {
                // 팝업 메뉴 생성
                GenericMenu menu = new GenericMenu();

                for (int i = 0; i < animations.Length; i++)
                {
                    DOTweenAnimation anim = animations[i];
                    string menuLabel = $"{i + 1}. {anim.animationType} ({anim.easeType})";

                    // 클로저 문제를 피하기 위해 로컬 변수 복사
                    int index = i;
                    menu.AddItem(new GUIContent(menuLabel), false, () => {
                        OpenEasingSelectorForAnimation(animations[index]);
                    });
                }

                // 마우스 위치에 메뉴 표시
                menu.ShowAsContext();
            }
            else
            {
                // 단일 컴포넌트인 경우 바로 선택기 열기
                OpenEasingSelectorForAnimation(animations[0]);
            }
        }

        // 특정 DOTweenAnimation에 대한 이징 선택기 열기
        private static void OpenEasingSelectorForAnimation(DOTweenAnimation targetAnimation)
        {
            if (targetAnimation != null)
            {
                // 이징 선택기 창 열기
                var window = EditorWindow.GetWindow<DOTweenEaseWindow>("DOTween 이징 선택기");
                window.minSize = new Vector2(850, 600);

                // 선택한 DOTweenAnimation 컴포넌트 전달
                var easingSelectorType = typeof(DOTweenEaseWindow);
                var targetAnimationProperty = easingSelectorType.GetField("targetAnimation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (targetAnimationProperty != null)
                {
                    targetAnimationProperty.SetValue(window, targetAnimation);
                }
            }
        }

        // 메인 메뉴에도 추가 (Component 메뉴 하위)
        [MenuItem("Component/DOTween/이징 그래프 선택기 열기", true)]
        private static bool ValidateComponentMenu()
        {
            return ValidateOpenEasingSelector();
        }

        [MenuItem("Component/DOTween/이징 그래프 선택기 열기", false, 10)]
        private static void ComponentMenu()
        {
            OpenEasingSelector();
        }

        // 컨텍스트 메뉴 (컴포넌트 우클릭 메뉴)에도 추가
        //[MenuItem("CONTEXT/DOTweenAnimation/이징 그래프 선택기 열기")]
        private static void ContextMenu(MenuCommand command)
        {
            DOTweenAnimation animation = command.context as DOTweenAnimation;
            if (animation != null)
            {
                OpenEasingSelectorForAnimation(animation);
            }
        }
    }
}