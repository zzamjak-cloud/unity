#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CAT.UI;

/// <summary>
/// MultiSliceImage 생성 메뉴 아이템
/// GameObject > UI > Multi Slice Image
/// </summary>
public static class MultiSliceImageMenuItems
{
    [MenuItem("GameObject/UI/Multi Slice Image", false, 2007)]
    static void CreateMultiSliceImage(MenuCommand menuCommand)
    {
        // Canvas가 없으면 먼저 Canvas 생성
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem이 없으면 생성
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }

        // GameObject 생성
        GameObject go = new GameObject("MultiSliceImage");

        // 선택된 GameObject가 있으면 그 자식으로, 없으면 Canvas 자식으로
        GameObject parent = menuCommand.context as GameObject;
        if (parent == null)
        {
            parent = canvas.gameObject;
        }

        // Undo 등록 및 부모 설정 (worldPositionStays = false로 로컬 좌표 유지)
        Undo.RegisterCreatedObjectUndo(go, "Create Multi Slice Image");
        GameObjectUtility.SetParentAndAlign(go, parent);
        Undo.RegisterCompleteObjectUndo(go, "Create Multi Slice Image");

        // RectTransform 설정
        RectTransform rectTransform = go.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = go.AddComponent<RectTransform>();
        }

        // 초기값 명시적 설정
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(100, 100);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.anchoredPosition3D = Vector3.zero;
        rectTransform.localPosition = Vector3.zero;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;

        // MultiSliceImage 컴포넌트 추가
        MultiSliceImage multiSliceImage = go.AddComponent<MultiSliceImage>();
        multiSliceImage.color = Color.white;
        multiSliceImage.raycastTarget = true;

        // 선택 및 이름 편집 모드로 전환
        Selection.activeGameObject = go;
        EditorApplication.delayCall += () =>
        {
            EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
        };
    }

    // Canvas 내부에서만 활성화
    [MenuItem("GameObject/UI/Multi Slice Image", true)]
    static bool ValidateCreateMultiSliceImage()
    {
        return true;
    }
}
#endif
