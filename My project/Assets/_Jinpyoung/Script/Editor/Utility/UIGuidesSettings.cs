using UnityEngine;
using UnityEditor;
using System.IO;


namespace CAT.Utility
{
    // UI 가이드 설정을 저장하는 ScriptableObject
    [System.Serializable]
    public class UIGuidesSettings : ScriptableObject
    {
        public bool showGuides = true;

        // Rows 설정
        public int rowNumber = 0;
        public float rowGutter = 0f;

        // Columns 설정
        public int columnNumber = 0;
        public float columnGutter = 0f;

        // Margin 설정
        public bool useMargin = false;
        public float marginTop = 0f;
        public float marginBottom = 0f;
        public float marginLeft = 0f;
        public float marginRight = 0f;

        // 색상 설정
        public Color guideColor = new Color(0f, 1f, 1f, 0.5f);
        public Color marginColor = new Color(0f, 1f, 0f, 1f);

        // 카메라 경로 (재시작 시 다시 찾기 위함)
        public string uiCameraPath = "";
        public string targetCanvasPath = "";
    }

    public class UIGuidesWindow : EditorWindow
    {
        private const string SETTINGS_PATH = "Assets/_Jinpyoung/Script/Editor/Utility/UIGuidesSettings.asset";
        private UIGuidesSettings settings;

        // UI 카메라 참조
        private Camera uiCamera;
        private Canvas targetCanvas;

        // 그리드 정보
        private Vector2 gridCellSize = Vector2.zero;
        private Vector2 effectiveCanvasSize = Vector2.zero;

        [MenuItem("CAT/Utility/UI Guides")]
        public static void ShowWindow()
        {
            UIGuidesWindow window = GetWindow<UIGuidesWindow>("UI Guides");
            window.minSize = new Vector2(300, 450);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadSettings();
            RestoreReferences();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SaveSettings();
        }

        private void LoadSettings()
        {
            // 설정 파일 로드 또는 생성
            settings = AssetDatabase.LoadAssetAtPath<UIGuidesSettings>(SETTINGS_PATH);

            if (settings == null)
            {
                // 설정 파일이 없으면 새로 생성
                settings = ScriptableObject.CreateInstance<UIGuidesSettings>();

                // Editor 폴더가 없으면 생성
                string editorPath = Path.GetDirectoryName(SETTINGS_PATH);
                if (!AssetDatabase.IsValidFolder(editorPath))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                    {
                        AssetDatabase.CreateFolder("Assets", "Editor");
                    }
                }

                AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
                AssetDatabase.SaveAssets();
            }
        }

        private void SaveSettings()
        {
            if (settings != null)
            {
                // 현재 참조 경로 저장
                if (uiCamera != null)
                {
                    settings.uiCameraPath = GetGameObjectPath(uiCamera.gameObject);
                }
                if (targetCanvas != null)
                {
                    settings.targetCanvasPath = GetGameObjectPath(targetCanvas.gameObject);
                }

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return "";

            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string[] parts = path.Split('/');
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                if (root.name == parts[0])
                {
                    Transform current = root.transform;

                    for (int i = 1; i < parts.Length; i++)
                    {
                        current = current.Find(parts[i]);
                        if (current == null) break;
                    }

                    if (current != null)
                    {
                        return current.gameObject;
                    }
                }
            }

            return null;
        }

        private void RestoreReferences()
        {
            // 저장된 경로로부터 참조 복원
            if (!string.IsNullOrEmpty(settings.uiCameraPath))
            {
                GameObject cameraObj = FindGameObjectByPath(settings.uiCameraPath);
                if (cameraObj != null)
                {
                    uiCamera = cameraObj.GetComponent<Camera>();
                }
            }

            if (!string.IsNullOrEmpty(settings.targetCanvasPath))
            {
                GameObject canvasObj = FindGameObjectByPath(settings.targetCanvasPath);
                if (canvasObj != null)
                {
                    targetCanvas = canvasObj.GetComponent<Canvas>();
                }
            }

            // 참조가 없으면 자동으로 찾기 시도
            if (uiCamera == null || targetCanvas == null)
            {
                FindUICamera();
            }
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                LoadSettings();
                return;
            }

            EditorGUILayout.Space(10);

            // 타이틀
            EditorGUILayout.LabelField("UI 가이드 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 가이드 표시 토글
            settings.showGuides = EditorGUILayout.Toggle("가이드 보이기", settings.showGuides);

            EditorGUILayout.Space(10);

            // UI 카메라 선택
            EditorGUILayout.BeginHorizontal();
            uiCamera = (Camera)EditorGUILayout.ObjectField("UI 카메라", uiCamera, typeof(Camera), true);
            if (GUILayout.Button("자동 찾기", GUILayout.Width(80)))
            {
                FindUICamera();
            }
            EditorGUILayout.EndHorizontal();

            if (uiCamera != null)
            {
                targetCanvas = (Canvas)EditorGUILayout.ObjectField("타겟 Canvas", targetCanvas, typeof(Canvas), true);
            }

            EditorGUILayout.Space(10);

            // Canvas 크기 정보 표시
            if (targetCanvas != null)
            {
                RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    EditorGUILayout.LabelField("Canvas 정보", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Canvas 크기: {canvasRect.rect.width:F0} x {canvasRect.rect.height:F0} px");

                    if (settings.useMargin)
                    {
                        float effectiveWidth = canvasRect.rect.width - settings.marginLeft - settings.marginRight;
                        float effectiveHeight = canvasRect.rect.height - settings.marginTop - settings.marginBottom;
                        EditorGUILayout.LabelField($"작업 영역: {effectiveWidth:F0} x {effectiveHeight:F0} px");
                    }

                    // 그리드 셀 크기 표시
                    if ((settings.rowNumber > 0 || settings.columnNumber > 0) && gridCellSize != Vector2.zero)
                    {
                        EditorGUILayout.Space(5);
                        GUI.color = new Color(0.5f, 1f, 0.5f);
                        EditorGUILayout.LabelField($"그리드 셀 크기: {gridCellSize.x:F1} x {gridCellSize.y:F1} px", EditorStyles.boldLabel);
                        GUI.color = Color.white;
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(10);
                }
            }

            // Rows 섹션
            EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            settings.rowNumber = EditorGUILayout.IntSlider("Number", settings.rowNumber, 0, 20);
            if (settings.rowNumber > 0)
            {
                settings.rowGutter = EditorGUILayout.Slider("Gutter (px)", settings.rowGutter, 0f, 100f);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            // Columns 섹션
            EditorGUILayout.LabelField("Columns", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            settings.columnNumber = EditorGUILayout.IntSlider("Number", settings.columnNumber, 0, 20);
            if (settings.columnNumber > 0)
            {
                settings.columnGutter = EditorGUILayout.Slider("Gutter (px)", settings.columnGutter, 0f, 100f);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            // Margin 섹션
            settings.useMargin = EditorGUILayout.Toggle("Margin 사용", settings.useMargin);
            if (settings.useMargin)
            {
                EditorGUI.indentLevel++;
                settings.marginTop = EditorGUILayout.Slider("Top (px)", settings.marginTop, 0f, 200f);
                settings.marginBottom = EditorGUILayout.Slider("Bottom (px)", settings.marginBottom, 0f, 200f);
                settings.marginLeft = EditorGUILayout.Slider("Left (px)", settings.marginLeft, 0f, 200f);
                settings.marginRight = EditorGUILayout.Slider("Right (px)", settings.marginRight, 0f, 200f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 색상 설정
            EditorGUILayout.LabelField("색상 설정", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            settings.guideColor = EditorGUILayout.ColorField("가이드 색상", settings.guideColor);
            if (settings.useMargin)
            {
                settings.marginColor = EditorGUILayout.ColorField("마진 색상", settings.marginColor);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(20);

            // 버튼들
            EditorGUILayout.BeginHorizontal();

            // Clear Guides 버튼
            if (GUILayout.Button("Clear Guides", GUILayout.Height(30)))
            {
                ClearGuides();
            }

            // Save Settings 버튼
            GUI.color = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("설정 저장", GUILayout.Height(30), GUILayout.Width(80)))
            {
                SaveSettings();
                EditorUtility.DisplayDialog("설정 저장", "UI 가이드 설정이 저장되었습니다.", "확인");
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            // Scene View 다시 그리기
            if (GUI.changed)
            {
                SaveSettings();
                SceneView.RepaintAll();
            }
        }

        private void FindUICamera()
        {
            // UI 카메라 자동 찾기
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam.gameObject.layer == LayerMask.NameToLayer("UI") ||
                    cam.name.ToLower().Contains("ui"))
                {
                    uiCamera = cam;
                    break;
                }
            }

            // Canvas 자동 찾기
            if (uiCamera != null)
            {
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in canvases)
                {
                    if (canvas.worldCamera == uiCamera ||
                        (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == uiCamera))
                    {
                        targetCanvas = canvas;
                        break;
                    }
                }
            }
        }

        private void ClearGuides()
        {
            settings.rowNumber = 0;
            settings.columnNumber = 0;
            settings.rowGutter = 0f;
            settings.columnGutter = 0f;
            settings.useMargin = false;
            settings.marginTop = 0f;
            settings.marginBottom = 0f;
            settings.marginLeft = 0f;
            settings.marginRight = 0f;

            SaveSettings();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (settings == null || !settings.showGuides || targetCanvas == null)
                return;

            // Canvas의 RectTransform 가져오기
            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
                return;

            // Canvas 월드 좌표 계산
            Vector3[] corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);

            // 이전 색상 저장
            Color oldColor = Handles.color;

            // Canvas 픽셀 크기와 월드 크기 계산
            float canvasPixelWidth = canvasRect.rect.width;
            float canvasPixelHeight = canvasRect.rect.height;
            float canvasWorldWidth = Vector3.Distance(corners[0], corners[3]);
            float canvasWorldHeight = Vector3.Distance(corners[0], corners[1]);

            // 픽셀을 월드 좌표로 변환하는 비율
            float pixelToWorldX = canvasWorldWidth / canvasPixelWidth;
            float pixelToWorldY = canvasWorldHeight / canvasPixelHeight;

            Vector3 bottomLeft = corners[0];
            Vector3 bottomRight = corners[3];
            Vector3 topLeft = corners[1];
            Vector3 topRight = corners[2];

            // 작업 영역 픽셀 크기 계산
            float workAreaPixelWidth = canvasPixelWidth;
            float workAreaPixelHeight = canvasPixelHeight;

            // Margin 적용 (픽셀 단위)
            if (settings.useMargin)
            {
                float worldMarginLeft = settings.marginLeft * pixelToWorldX;
                float worldMarginRight = settings.marginRight * pixelToWorldX;
                float worldMarginTop = settings.marginTop * pixelToWorldY;
                float worldMarginBottom = settings.marginBottom * pixelToWorldY;

                Vector3 rightDir = (bottomRight - bottomLeft).normalized;
                Vector3 upDir = (topLeft - bottomLeft).normalized;

                bottomLeft += rightDir * worldMarginLeft + upDir * worldMarginBottom;
                bottomRight -= rightDir * worldMarginRight;
                bottomRight += upDir * worldMarginBottom;
                topLeft += rightDir * worldMarginLeft;
                topLeft -= upDir * worldMarginTop;
                topRight -= rightDir * worldMarginRight + upDir * worldMarginTop;

                // Margin 영역 표시
                Handles.color = settings.marginColor;

                // Margin 라인 그리기
                Vector3[] marginLines = new Vector3[]
                {
                corners[0] + rightDir * worldMarginLeft,
                corners[1] + rightDir * worldMarginLeft,
                corners[3] - rightDir * worldMarginRight,
                corners[2] - rightDir * worldMarginRight,
                corners[0] + upDir * worldMarginBottom,
                corners[3] + upDir * worldMarginBottom,
                corners[1] - upDir * worldMarginTop,
                corners[2] - upDir * worldMarginTop
                };

                Handles.DrawLine(marginLines[0], marginLines[1], 2f);
                Handles.DrawLine(marginLines[2], marginLines[3], 2f);
                Handles.DrawLine(marginLines[4], marginLines[5], 2f);
                Handles.DrawLine(marginLines[6], marginLines[7], 2f);

                // 작업 영역 업데이트
                workAreaPixelWidth = canvasPixelWidth - settings.marginLeft - settings.marginRight;
                workAreaPixelHeight = canvasPixelHeight - settings.marginTop - settings.marginBottom;
            }

            // 그리드 셀 크기 계산 (픽셀 단위)
            float cellPixelWidth = workAreaPixelWidth;
            float cellPixelHeight = workAreaPixelHeight;

            if (settings.columnNumber > 0)
            {
                // Gutter를 제외한 실제 컨텐츠 영역 계산
                float totalColumnGutter = settings.columnGutter * settings.columnNumber;
                float availableWidth = workAreaPixelWidth - totalColumnGutter;
                cellPixelWidth = availableWidth / (settings.columnNumber + 1);
            }

            if (settings.rowNumber > 0)
            {
                // Gutter를 제외한 실제 컨텐츠 영역 계산
                float totalRowGutter = settings.rowGutter * settings.rowNumber;
                float availableHeight = workAreaPixelHeight - totalRowGutter;
                cellPixelHeight = availableHeight / (settings.rowNumber + 1);
            }

            // 그리드 정보 업데이트
            gridCellSize = new Vector2(cellPixelWidth, cellPixelHeight);
            effectiveCanvasSize = new Vector2(workAreaPixelWidth, workAreaPixelHeight);

            // 작업 영역의 월드 크기 재계산
            float workAreaWorldWidth = Vector3.Distance(bottomLeft, bottomRight);
            float workAreaWorldHeight = Vector3.Distance(bottomLeft, topLeft);

            // 가이드 색상 설정
            Handles.color = settings.guideColor;

            // Row 가이드 그리기 (픽셀 단위 Gutter 적용)
            if (settings.rowNumber > 0)
            {
                Vector3 upDirection = (topLeft - bottomLeft).normalized;

                for (int i = 1; i <= settings.rowNumber; i++)
                {
                    // 픽셀 단위로 위치 계산
                    float pixelPos = (cellPixelHeight + settings.rowGutter) * i - settings.rowGutter / 2f;
                    float worldPos = pixelPos * (workAreaWorldHeight / workAreaPixelHeight);

                    if (settings.rowGutter > 0)
                    {
                        // Gutter를 픽셀 단위로 월드 좌표로 변환
                        float worldGutter = settings.rowGutter * pixelToWorldY;
                        float halfGutter = worldGutter / 2f;

                        // 위쪽 가이드라인
                        Vector3 lineStart1 = bottomLeft + upDirection * (worldPos - halfGutter);
                        Vector3 lineEnd1 = bottomRight + upDirection * (worldPos - halfGutter);
                        Handles.DrawLine(lineStart1, lineEnd1, 2f);

                        // 아래쪽 가이드라인
                        Vector3 lineStart2 = bottomLeft + upDirection * (worldPos + halfGutter);
                        Vector3 lineEnd2 = bottomRight + upDirection * (worldPos + halfGutter);
                        Handles.DrawLine(lineStart2, lineEnd2, 2f);

                        // Gutter 영역 시각화
                        Color gutterColor = settings.guideColor;
                        gutterColor.a *= 0.2f;
                        Handles.color = gutterColor;

                        for (int g = 0; g < 3; g++)
                        {
                            float gutterLinePos = worldPos - halfGutter + (worldGutter / 3f) * (g + 1);
                            Vector3 gutterLineStart = bottomLeft + upDirection * gutterLinePos;
                            Vector3 gutterLineEnd = bottomRight + upDirection * gutterLinePos;
                            Handles.DrawDottedLine(gutterLineStart, gutterLineEnd, 2f);
                        }

                        Handles.color = settings.guideColor;
                    }
                    else
                    {
                        // Gutter가 0이면 단일 라인만 그리기
                        Vector3 lineStart = bottomLeft + upDirection * worldPos;
                        Vector3 lineEnd = bottomRight + upDirection * worldPos;
                        Handles.DrawLine(lineStart, lineEnd, 2f);
                    }
                }
            }

            // Column 가이드 그리기 (픽셀 단위 Gutter 적용)
            if (settings.columnNumber > 0)
            {
                Vector3 rightDirection = (bottomRight - bottomLeft).normalized;

                for (int i = 1; i <= settings.columnNumber; i++)
                {
                    // 픽셀 단위로 위치 계산
                    float pixelPos = (cellPixelWidth + settings.columnGutter) * i - settings.columnGutter / 2f;
                    float worldPos = pixelPos * (workAreaWorldWidth / workAreaPixelWidth);

                    if (settings.columnGutter > 0)
                    {
                        // Gutter를 픽셀 단위로 월드 좌표로 변환
                        float worldGutter = settings.columnGutter * pixelToWorldX;
                        float halfGutter = worldGutter / 2f;

                        // 왼쪽 가이드라인
                        Vector3 lineStart1 = bottomLeft + rightDirection * (worldPos - halfGutter);
                        Vector3 lineEnd1 = topLeft + rightDirection * (worldPos - halfGutter);
                        Handles.DrawLine(lineStart1, lineEnd1, 2f);

                        // 오른쪽 가이드라인
                        Vector3 lineStart2 = bottomLeft + rightDirection * (worldPos + halfGutter);
                        Vector3 lineEnd2 = topLeft + rightDirection * (worldPos + halfGutter);
                        Handles.DrawLine(lineStart2, lineEnd2, 2f);

                        // Gutter 영역 시각화
                        Color gutterColor = settings.guideColor;
                        gutterColor.a *= 0.2f;
                        Handles.color = gutterColor;

                        for (int g = 0; g < 3; g++)
                        {
                            float gutterLinePos = worldPos - halfGutter + (worldGutter / 3f) * (g + 1);
                            Vector3 gutterLineStart = bottomLeft + rightDirection * gutterLinePos;
                            Vector3 gutterLineEnd = topLeft + rightDirection * gutterLinePos;
                            Handles.DrawDottedLine(gutterLineStart, gutterLineEnd, 2f);
                        }

                        Handles.color = settings.guideColor;
                    }
                    else
                    {
                        // Gutter가 0이면 단일 라인만 그리기
                        Vector3 lineStart = bottomLeft + rightDirection * worldPos;
                        Vector3 lineEnd = topLeft + rightDirection * worldPos;
                        Handles.DrawLine(lineStart, lineEnd, 2f);
                    }
                }
            }

            // 색상 복원
            Handles.color = oldColor;
        }
    }
}