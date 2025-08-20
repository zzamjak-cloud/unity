using UnityEngine;
using UnityEditor;
using DG.Tweening;

namespace CAT.Utility
{
    [InitializeOnLoad]
    public static class DOTweenSceneViewOverlay
    {
        // 오버레이 표시 여부를 저장하는 EditorPrefs 키
        private const string SHOW_OVERLAY_PREF_KEY = "DOTweenSceneViewOverlay_ShowOverlay";

        // 오버레이 표시 여부
        private static bool showOverlay;

        static DOTweenSceneViewOverlay()
        {
            // EditorPrefs에서 오버레이 표시 여부 불러오기 (기본값: true)
            showOverlay = EditorPrefs.GetBool(SHOW_OVERLAY_PREF_KEY, true);

            // Scene 뷰 GUI 이벤트에 콜백 등록
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // 선택된 오브젝트가 없으면 아무 것도 하지 않음
            if (Selection.activeGameObject == null)
                return;

            // DOTweenAnimation 컴포넌트 확인
            DOTweenAnimation[] animations = Selection.activeGameObject.GetComponents<DOTweenAnimation>();
            if (animations.Length == 0)
                return;

            // 항상 표시되는 토글 버튼 (오버레이 토글)
            Handles.BeginGUI();

            // 오른쪽 상단에 위치한 작은 토글 버튼
            Rect toggleRect = new Rect(sceneView.position.width - 150, 10, 130, 20);
            bool newShowOverlay = GUI.Toggle(toggleRect, showOverlay, " DOTween 오버레이");

            // 토글 값이 변경되면 저장
            if (newShowOverlay != showOverlay)
            {
                showOverlay = newShowOverlay;
                EditorPrefs.SetBool(SHOW_OVERLAY_PREF_KEY, showOverlay);
            }

            // 오버레이를 표시하지 않으면 여기서 종료
            if (!showOverlay)
            {
                Handles.EndGUI();
                return;
            }

            // 화면 오른쪽 상단에 버튼 배치
            // 반투명 배경 박스 - 애니메이션 개수에 따라 크기 조정
            int boxHeight = 40 + (animations.Length * 30);
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTexture(300, boxHeight, new Color(0.1f, 0.1f, 0.1f, 0.7f));

            GUILayout.BeginArea(new Rect(sceneView.position.width - 320, 40, 300, boxHeight), boxStyle);

            GUILayout.Label($"DOTween Animation 컴포넌트 ({animations.Length}개 발견)", EditorStyles.boldLabel);

            // 버튼 스타일 정의
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 12;

            // 각 DOTween Animation 컴포넌트에 대한 버튼 생성
            for (int i = 0; i < animations.Length; i++)
            {
                GUILayout.BeginHorizontal();

                // 애니메이션 타입 표시
                string animType = animations[i].animationType.ToString();
                string easingType = animations[i].easeType.ToString();

                GUILayout.Label($"{i + 1}. {animType} ({easingType})", GUILayout.Width(180));

                // 이 컴포넌트의 이징 선택기 열기 버튼
                if (GUILayout.Button("이징 선택", buttonStyle, GUILayout.Height(22)))
                {
                    OpenEasingSelector(animations[i]);
                }

                GUILayout.EndHorizontal();
            }

            // 하단에 단축키 정보
            GUILayout.Space(5);
            GUIStyle infoStyle = new GUIStyle(EditorStyles.miniLabel);
            infoStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("단축키: Ctrl+Shift+E (선택된 컴포넌트용)", infoStyle);

            GUILayout.EndArea();

            Handles.EndGUI();
        }

        // 이징 선택기 창 열기
        private static void OpenEasingSelector(DOTweenAnimation targetAnimation)
        {
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

        // 단색 텍스처 생성 (박스 배경용)
        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
    }
}