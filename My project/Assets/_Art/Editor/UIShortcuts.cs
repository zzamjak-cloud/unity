using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace CAT.UI
{
    public class UIShortcuts
    {
        // Image 생성 (Mac: Command+Option+I, Windows: Ctrl+Alt+I)
        [MenuItem("GameObject/UI/Custom Image %&i", false, 0)]
        static void CreateImage()
        {
            CreateUIElement<Image>("Image");
        }

        // Raw Image 생성 (Mac: Command+Option+R, Windows: Ctrl+Alt+R)
        [MenuItem("GameObject/UI/Custom Raw Image %&r", false, 0)]
        static void CreateRawImage()
        {
            CreateUIElement<RawImage>("Raw Image");
        }

        // TextMesh Pro 생성 (Mac: Command+Option+T, Windows: Ctrl+Alt+T)
        [MenuItem("GameObject/UI/Custom TextMeshPro Text %&t", false, 0)]
        static void CreateTextMeshPro()
        {
            CreateTextMeshProElement();
        }

        // 제네릭 메소드로 UI 요소 생성
        private static void CreateUIElement<T>(string elementName) where T : Graphic
        {
            // 새 게임오브젝트 생성
            GameObject gameObject = new GameObject(elementName);

            // RectTransform 추가
            RectTransform rectTransform = gameObject.AddComponent<RectTransform>();

            // 요청된 그래픽 컴포넌트 추가
            T graphic = gameObject.AddComponent<T>();

            // Raycast Target 비활성화
            graphic.raycastTarget = false;

            // 현재 선택된 오브젝트 확인
            GameObject parentObject = Selection.activeGameObject;

            SetupParentAndTransform(gameObject, parentObject, rectTransform);

            // 새로 생성된 오브젝트 선택
            Selection.activeGameObject = gameObject;

            // 변경사항 저장
            EditorUtility.SetDirty(gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // TextMeshPro 요소 생성을 위한 특별 메소드
        private static void CreateTextMeshProElement()
        {
            // 새 게임오브젝트 생성
            GameObject gameObject = new GameObject("Desc (TMP)");

            // RectTransform 추가
            RectTransform rectTransform = gameObject.AddComponent<RectTransform>();

            // TextMeshProUGUI 컴포넌트 추가
            TextMeshProUGUI tmpText = gameObject.AddComponent<TextMeshProUGUI>();

            // Raycast Target 비활성화
            tmpText.raycastTarget = false;

            // 기본 텍스트 설정
            tmpText.text = "New Text";

            // 텍스트 정렬 설정 - 수평 및 수직 중앙 정렬
            tmpText.alignment = TextAlignmentOptions.Center;

            // 현재 선택된 오브젝트 확인
            GameObject parentObject = Selection.activeGameObject;

            // TextMesh Pro 기본 사이즈는 200, 40으로 설정
            SetupParentAndTransform(gameObject, parentObject, rectTransform, new Vector2(200, 40));

            // 새로 생성된 오브젝트 선택
            Selection.activeGameObject = gameObject;

            // 변경사항 저장
            EditorUtility.SetDirty(gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // 부모 설정 및 트랜스폼 초기화를 위한 공통 메소드 (크기 파라미터 추가)
        private static void SetupParentAndTransform(GameObject gameObject, GameObject parentObject, RectTransform rectTransform, Vector2 size = default)
        {
            // 부모 설정 로직
            if (parentObject != null)
            {
                // 부모가 캔버스이거나 캔버스를 포함한 계층 구조에 있는지 확인
                Canvas canvas = parentObject.GetComponentInParent<Canvas>();
                RectTransform parentRectTransform = parentObject.GetComponent<RectTransform>();

                if (canvas != null && parentRectTransform != null)
                {
                    // 선택된 오브젝트의 자식으로 설정
                    gameObject.transform.SetParent(parentObject.transform, false);
                }
                else
                {
                    // 선택된 오브젝트가 UI 계층에 없으면 씬에서 캔버스 찾기
                    canvas = Object.FindObjectOfType<Canvas>();
                    if (canvas != null)
                    {
                        gameObject.transform.SetParent(canvas.transform, false);
                    }
                    else
                    {
                        // 캔버스가 없으면 생성
                        GameObject canvasObject = new GameObject("Canvas");
                        canvas = canvasObject.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvasObject.AddComponent<CanvasScaler>();
                        canvasObject.AddComponent<GraphicRaycaster>();
                        gameObject.transform.SetParent(canvasObject.transform, false);
                    }
                }
            }
            else
            {
                // 선택된 오브젝트가 없으면 캔버스 찾거나 생성
                Canvas canvas = Object.FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    gameObject.transform.SetParent(canvas.transform, false);
                }
                else
                {
                    // 캔버스가 없으면 생성
                    GameObject canvasObject = new GameObject("Canvas");
                    canvas = canvasObject.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObject.AddComponent<CanvasScaler>();
                    canvasObject.AddComponent<GraphicRaycaster>();
                    gameObject.transform.SetParent(canvasObject.transform, false);
                }
            }

            // RectTransform 초기화
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.anchoredPosition = Vector2.zero;

            // 크기 설정 (기본값 또는 지정된 값)
            if (size == default)
            {
                // 기본 크기 설정 (128x128)
                rectTransform.sizeDelta = new Vector2(128, 128);
            }
            else
            {
                // 지정된 크기 설정
                rectTransform.sizeDelta = size;
            }
        }
    }
}