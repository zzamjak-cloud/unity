using UnityEngine;
using UnityEditor;
using DG.Tweening;
using System;
using System.Collections.Generic;

namespace CAT.Utility
{
    // 정적 딕셔너리를 추가하여 이징 텍스처 캐싱
    public class DOTweenEaseWindow : EditorWindow
    {
        // 타겟 애니메이션 컴포넌트
        private DG.Tweening.DOTweenAnimation targetAnimation;

        // 이징 타입과 해당 그래프 미리보기 텍스처를 저장할 딕셔너리 (정적으로 변경하여 인스턴스 간 공유)
        private static Dictionary<Ease, Texture2D> easePreviewTextures = new Dictionary<Ease, Texture2D>();

        // 텍스처가 이미 생성되었는지 확인하는 플래그
        private static bool texturesGenerated = false;
        // 그래프 미리보기 크기
        private const int PREVIEW_WIDTH = 200;
        private const int PREVIEW_HEIGHT = 128;

        // 스크롤 위치
        private Vector2 scrollPosition;

        // 검색어
        private string searchText = "";

        // 필터링 옵션
        private bool showInEases = true;
        private bool showOutEases = true;
        private bool showInOutEases = true;
        private bool showOtherEases = true;

        // 윈도우 열기 (메뉴 아이템)
        [MenuItem("Window/DOTween/Easing Selector")]
        public static void ShowWindow()
        {
            var window = GetWindow<DOTweenEaseWindow>("DOTween 이징 선택기");
            window.minSize = new Vector2(400, 300);
        }

        // 선택된 게임 오브젝트에서 윈도우 열기 (컨텍스트 메뉴)
        [MenuItem("CONTEXT/DOTweenAnimation/이징 그래프 선택기 열기")]
        static void OpenEasingSelectorFromContext(MenuCommand command)
        {
            var animation = command.context as DG.Tweening.DOTweenAnimation;
            if (animation != null)
            {
                var window = GetWindow<DOTweenEaseWindow>("DOTween 이징 선택기");
                window.minSize = new Vector2(400, 300);
                window.targetAnimation = animation;
            }
        }

        // 윈도우가 처음 열릴 때 초기화
        private void OnEnable()
        {
            // 현재 선택된, 타겟 DOTweenAnimation 컴포넌트 체크
            if (targetAnimation == null && Selection.activeGameObject != null)
            {
                targetAnimation = Selection.activeGameObject.GetComponent<DG.Tweening.DOTweenAnimation>();
            }

            // 텍스처가 아직 생성되지 않았으면 생성
            if (!texturesGenerated)
            {
                GenerateEasePreviewTextures();
                texturesGenerated = true;
            }
        }

        // 윈도우가 닫힐 때 정리 (텍스처를 더 이상 해제하지 않음)
        private void OnDisable()
        {
            // 텍스처는 정적 딕셔너리에 캐싱하므로 해제하지 않음
        }

        // Selection 변경 감지
        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null)
            {
                targetAnimation = Selection.activeGameObject.GetComponent<DG.Tweening.DOTweenAnimation>();
                Repaint();
            }
        }

        // GUI 렌더링
        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space();

            // 타겟 애니메이션이 없는 경우
            if (targetAnimation == null)
            {
                EditorGUILayout.HelpBox("DOTweenAnimation 컴포넌트가 있는 게임 오브젝트를 선택해주세요.", MessageType.Info);

                // 선택된 오브젝트에서 DOTweenAnimation 찾기
                if (Selection.activeGameObject != null)
                {
                    var animations = Selection.activeGameObject.GetComponents<DG.Tweening.DOTweenAnimation>();
                    if (animations.Length > 0)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField("발견된 DOTweenAnimation 컴포넌트:", EditorStyles.boldLabel);

                        foreach (var anim in animations)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.ObjectField(anim, typeof(DG.Tweening.DOTweenAnimation), true);
                            if (GUILayout.Button("선택", GUILayout.Width(60)))
                            {
                                targetAnimation = anim;
                                GUI.FocusControl(null);
                            }
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndVertical();
                    }
                }

                return;
            }

            // 현재 선택된 컴포넌트 정보
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("선택된 DOTweenAnimation:", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(targetAnimation, typeof(DG.Tweening.DOTweenAnimation), true);
            EditorGUI.EndDisabledGroup();

            // 현재 애니메이션 타입 및 이징 정보
            EditorGUILayout.LabelField($"애니메이션 타입: {targetAnimation.animationType}");
            EditorGUILayout.LabelField($"현재 이징: {targetAnimation.easeType}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // 필터링 및 검색
            DrawFilterOptions();

            EditorGUILayout.Space();

            // 이징 그래프 그리드 표시
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DisplayEaseTypesGrid();
            EditorGUILayout.EndScrollView();
        }

        // 툴바 표시
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("모든 이징 보기", EditorStyles.toolbarButton))
            {
                showInEases = showOutEases = showInOutEases = showOtherEases = true;
            }

            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton))
            {
                OnSelectionChange();
            }

            // 그래프 텍스처 재생성 버튼 추가
            if (GUILayout.Button("그래프 다시 생성", EditorStyles.toolbarButton))
            {
                // 기존 텍스처 정리
                foreach (var texture in easePreviewTextures.Values)
                {
                    DestroyImmediate(texture);
                }
                easePreviewTextures.Clear();
                texturesGenerated = false;

                // 텍스처 다시 생성
                GenerateEasePreviewTextures();
                texturesGenerated = true;

                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        // 필터 옵션 표시
        private void DrawFilterOptions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("필터 옵션", EditorStyles.boldLabel);

            // 검색
            searchText = EditorGUILayout.TextField("검색:", searchText);

            EditorGUILayout.BeginHorizontal();

            // 카테고리별 필터링
            showInEases = EditorGUILayout.ToggleLeft("In", showInEases, GUILayout.Width(60));
            showOutEases = EditorGUILayout.ToggleLeft("Out", showOutEases, GUILayout.Width(60));
            showInOutEases = EditorGUILayout.ToggleLeft("InOut", showInOutEases, GUILayout.Width(80));
            showOtherEases = EditorGUILayout.ToggleLeft("기타", showOtherEases, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // 이징 타입을 그리드로 표시
        private void DisplayEaseTypesGrid()
        {
            // 사용 가능한 모든 Ease 타입
            Ease[] easeTypes = (Ease[])Enum.GetValues(typeof(Ease));

            // 필터링된 이징 목록
            List<Ease> filteredEases = new List<Ease>();
            foreach (Ease easeType in easeTypes)
            {
                // 카테고리별 필터링
                bool shouldShow = false;
                string easeName = easeType.ToString();

                if (easeName.StartsWith("In") && !easeName.Contains("InOut") && showInEases)
                    shouldShow = true;
                else if (easeName.StartsWith("Out") && showOutEases)
                    shouldShow = true;
                else if (easeName.Contains("InOut") && showInOutEases)
                    shouldShow = true;
                else if ((!easeName.StartsWith("In") && !easeName.StartsWith("Out") && !easeName.Contains("InOut")) && showOtherEases)
                    shouldShow = true;

                // 검색어 필터링
                if (!string.IsNullOrEmpty(searchText) && !easeName.ToLower().Contains(searchText.ToLower()))
                    shouldShow = false;

                if (shouldShow)
                    filteredEases.Add(easeType);
            }

            // 그리드 열 개수 (고정값)
            int columns = 4; // 항상 4열로 표시

            for (int i = 0; i < filteredEases.Count; i += columns)
            {
                EditorGUILayout.BeginHorizontal();

                for (int j = 0; j < columns && i + j < filteredEases.Count; j++)
                {
                    Ease easeType = filteredEases[i + j];

                    // 현재 선택된 이징 하이라이트
                    bool isSelected = targetAnimation != null && targetAnimation.easeType == easeType;

                    // 이징 타입 버튼 + 그래프 미리보기
                    if (DisplayEaseTypeButton(easeType, isSelected))
                    {
                        if (targetAnimation != null)
                        {
                            // 언두/리두 지원
                            Undo.RecordObject(targetAnimation, "Change DOTween Ease Type");

                            // 이징 타입 변경
                            targetAnimation.easeType = easeType;

                            // 변경사항 저장
                            EditorUtility.SetDirty(targetAnimation);

                            // 이징 선택 후 창 닫기
                            Close();
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        // 이징 타입 버튼 표시
        private bool DisplayEaseTypeButton(Ease easeType, bool isSelected)
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(5, 5, 5, 5);
            boxStyle.margin = new RectOffset(5, 5, 5, 5);

            if (isSelected)
            {
                boxStyle.normal.background = EditorGUIUtility.whiteTexture;
                Color backgroundColor = new Color(0.6f, 0.8f, 1f);
                GUI.backgroundColor = backgroundColor;
            }

            GUILayout.BeginVertical(boxStyle, GUILayout.Width(PREVIEW_WIDTH + 10), GUILayout.Height(PREVIEW_HEIGHT + 40));

            // 이징 이름
            EditorGUILayout.LabelField(easeType.ToString(), EditorStyles.boldLabel);

            // 그래프 미리보기
            if (easePreviewTextures.TryGetValue(easeType, out Texture2D previewTexture))
            {
                GUILayout.Box(previewTexture, GUILayout.Width(PREVIEW_WIDTH), GUILayout.Height(PREVIEW_HEIGHT));

                // 그래프에서 0과 1 위치에 작은 라벨 추가
                Rect boxRect = GUILayoutUtility.GetLastRect();
                Rect label0Rect = new Rect(boxRect.x + 2, boxRect.y + boxRect.height * 0.75f - 8, 15, 16);
                Rect label1Rect = new Rect(boxRect.x + 2, boxRect.y + boxRect.height * 0.25f - 8, 15, 16);

                GUI.Label(label0Rect, "0", EditorStyles.miniLabel);
                GUI.Label(label1Rect, "1", EditorStyles.miniLabel);
            }

            // 선택 버튼 크기 30% 줄이기
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // 가운데 정렬을 위한 빈 공간
            bool clicked = GUILayout.Button("선택", GUILayout.Width((PREVIEW_WIDTH + 10) * 0.7f), GUILayout.Height(24));
            GUILayout.FlexibleSpace(); // 가운데 정렬을 위한 빈 공간
            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // 배경색 복원
            GUI.backgroundColor = Color.white;

            return clicked;
        }

        // 모든 이징 타입에 대한 미리보기 텍스처 생성
        private void GenerateEasePreviewTextures()
        {
            // 이미 텍스처가 캐싱되어 있으면 다시 생성하지 않음
            if (easePreviewTextures.Count > 0)
                return;

            // 사용 가능한 모든 Ease 타입
            Ease[] easeTypes = (Ease[])Enum.GetValues(typeof(Ease));

            foreach (Ease easeType in easeTypes)
            {
                // 이징 타입에 대한 텍스처 생성
                Texture2D texture = new Texture2D(PREVIEW_WIDTH, PREVIEW_HEIGHT, TextureFormat.RGBA32, false);

                // 텍스처를 흰색으로 초기화
                Color[] pixels = new Color[PREVIEW_WIDTH * PREVIEW_HEIGHT];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(0.95f, 0.95f, 0.95f, 1);
                }
                texture.SetPixels(pixels);

                // 이징 그래프 그리기
                DrawEaseGraph(texture, easeType);

                texture.Apply();
                easePreviewTextures[easeType] = texture;
            }
        }

        // 특정 이징 타입에 대한 그래프 그리기
        private void DrawEaseGraph(Texture2D texture, Ease easeType)
        {
            // 그래프 라인 색상
            Color lineColor = new Color(0.2f, 0.6f, 1f, 1);

            // 그리드 라인 색상
            Color gridColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);

            // 0과 1 라인 색상
            Color referenceLineColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

            // 배경 그리기
            Color backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1);
            for (int x = 0; x < PREVIEW_WIDTH; x++)
            {
                for (int y = 0; y < PREVIEW_HEIGHT; y++)
                {
                    texture.SetPixel(x, y, backgroundColor);
                }
            }

            // 테두리 그리기
            for (int x = 0; x < PREVIEW_WIDTH; x++)
            {
                texture.SetPixel(x, 0, gridColor);
                texture.SetPixel(x, PREVIEW_HEIGHT - 1, gridColor);
            }

            for (int y = 0; y < PREVIEW_HEIGHT; y++)
            {
                texture.SetPixel(0, y, gridColor);
                texture.SetPixel(PREVIEW_WIDTH - 1, y, gridColor);
            }

            // 중앙 수평선 (0.5 위치) 그리기
            int midY = PREVIEW_HEIGHT / 2;
            for (int x = 0; x < PREVIEW_WIDTH; x++)
            {
                texture.SetPixel(x, midY, gridColor);
            }

            // 중앙 수직선 (0.5 위치) 그리기
            int midX = PREVIEW_WIDTH / 2;
            for (int y = 0; y < PREVIEW_HEIGHT; y++)
            {
                texture.SetPixel(midX, y, gridColor);
            }

            // 0 라인과 1 라인 그리기 (전체 그래프 범위를 -0.5에서 1.5로 확장)
            int zeroY = PREVIEW_HEIGHT * 3 / 4; // 0은 높이의 3/4 위치
            int oneY = PREVIEW_HEIGHT / 4;      // 1은 높이의 1/4 위치

            for (int x = 0; x < PREVIEW_WIDTH; x++)
            {
                texture.SetPixel(x, zeroY, referenceLineColor);
                texture.SetPixel(x, oneY, referenceLineColor);
            }

            // 이징 함수를 계산하고 값의 범위 확인
            float[] values = new float[PREVIEW_WIDTH];
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;

            for (int x = 0; x < PREVIEW_WIDTH; x++)
            {
                float t = (float)x / PREVIEW_WIDTH;
                float value = CalculateEaseValue(t, easeType);
                values[x] = value;

                if (value < minValue) minValue = value;
                if (value > maxValue) maxValue = value;
            }

            // 그래프의 전체 값 범위를 보여주기 위한 스케일 설정
            float valueRange = maxValue - minValue;

            // 값 범위가 너무 작은 경우 (거의 직선인 경우) 기본 0-1 범위 사용
            if (valueRange < 0.1f)
            {
                minValue = -0.1f;
                maxValue = 1.1f;
                valueRange = 1.2f;
            }
            else
            {
                // 약간의 여백 추가
                minValue -= valueRange * 0.1f;
                maxValue += valueRange * 0.1f;
                valueRange = maxValue - minValue;
            }

            // 이징 함수를 사용하여 그래프 그리기
            int lastY = 0;
            bool isFirstPoint = true;

            for (int x = 0; x < PREVIEW_WIDTH; x++)
            {
                float value = values[x];

                // 정규화된 값을 스케일링하여 전체 그래프가 보이도록 함
                float normalizedValue = (value - minValue) / valueRange;

                // Y값 반전 (0이 아래쪽)
                int y = PREVIEW_HEIGHT - 1 - Mathf.RoundToInt(normalizedValue * (PREVIEW_HEIGHT - 1));

                // 범위 체크
                y = Mathf.Clamp(y, 0, PREVIEW_HEIGHT - 1);

                // 첫 번째 점이 아니면 선 그리기
                if (!isFirstPoint)
                {
                    DrawLine(texture, x - 1, lastY, x, y, lineColor);
                }
                else
                {
                    isFirstPoint = false;
                }

                lastY = y;
            }
        }

        // 특정 이징 타입에 대한 정보를 표시하는 함수
        private void DisplayEaseInfo(Ease easeType, float minValue, float maxValue)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"이징 타입: {easeType}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"최소값: {minValue:F2}, 최대값: {maxValue:F2}, 범위: {maxValue - minValue:F2}");
            EditorGUILayout.EndVertical();
        }

        // 직접 이징 값 계산 (DOVirtual.EasedValue 사용하지 않음)
        private float CalculateEaseValue(float t, Ease easeType)
        {
            // t는 0과 1 사이의 값
            switch (easeType)
            {
                case Ease.Linear:
                    return t;

                case Ease.InSine:
                    return 1 - Mathf.Cos((t * Mathf.PI) / 2);

                case Ease.OutSine:
                    return Mathf.Sin((t * Mathf.PI) / 2);

                case Ease.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;

                case Ease.InQuad:
                    return t * t;

                case Ease.OutQuad:
                    return 1 - (1 - t) * (1 - t);

                case Ease.InOutQuad:
                    return t < 0.5 ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;

                case Ease.InCubic:
                    return t * t * t;

                case Ease.OutCubic:
                    return 1 - Mathf.Pow(1 - t, 3);

                case Ease.InOutCubic:
                    return t < 0.5 ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;

                case Ease.InQuart:
                    return t * t * t * t;

                case Ease.OutQuart:
                    return 1 - Mathf.Pow(1 - t, 4);

                case Ease.InOutQuart:
                    return t < 0.5 ? 8 * t * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 4) / 2;

                case Ease.InQuint:
                    return t * t * t * t * t;

                case Ease.OutQuint:
                    return 1 - Mathf.Pow(1 - t, 5);

                case Ease.InOutQuint:
                    return t < 0.5 ? 16 * t * t * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 5) / 2;

                case Ease.InExpo:
                    return t == 0 ? 0 : Mathf.Pow(2, 10 * t - 10);

                case Ease.OutExpo:
                    return t == 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);

                case Ease.InOutExpo:
                    return t == 0 ? 0 : t == 1 ? 1 : t < 0.5 ? Mathf.Pow(2, 20 * t - 10) / 2 : (2 - Mathf.Pow(2, -20 * t + 10)) / 2;

                case Ease.InCirc:
                    return 1 - Mathf.Sqrt(1 - Mathf.Pow(t, 2));

                case Ease.OutCirc:
                    return Mathf.Sqrt(1 - Mathf.Pow(t - 1, 2));

                case Ease.InOutCirc:
                    return t < 0.5 ? (1 - Mathf.Sqrt(1 - Mathf.Pow(2 * t, 2))) / 2 : (Mathf.Sqrt(1 - Mathf.Pow(-2 * t + 2, 2)) + 1) / 2;

                case Ease.InElastic:
                    {
                        const float c4 = (2 * Mathf.PI) / 3;
                        return t == 0 ? 0 : t == 1 ? 1 : -Mathf.Pow(2, 10 * t - 10) * Mathf.Sin((t * 10 - 10.75f) * c4);
                    }

                case Ease.OutElastic:
                    {
                        const float c4 = (2 * Mathf.PI) / 3;
                        return t == 0 ? 0 : t == 1 ? 1 : Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * c4) + 1;
                    }

                case Ease.InOutElastic:
                    {
                        const float c5 = (2 * Mathf.PI) / 4.5f;
                        return t == 0 ? 0 : t == 1 ? 1 : t < 0.5 ?
                            -(Mathf.Pow(2, 20 * t - 10) * Mathf.Sin((20 * t - 11.125f) * c5)) / 2 :
                            (Mathf.Pow(2, -20 * t + 10) * Mathf.Sin((20 * t - 11.125f) * c5)) / 2 + 1;
                    }

                case Ease.InBack:
                    {
                        const float c1 = 1.70158f;
                        const float c3 = c1 + 1;
                        return c3 * t * t * t - c1 * t * t;
                    }

                case Ease.OutBack:
                    {
                        const float c1 = 1.70158f;
                        const float c3 = c1 + 1;
                        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                    }

                case Ease.InOutBack:
                    {
                        const float c1 = 1.70158f;
                        const float c2 = c1 * 1.525f;
                        return t < 0.5 ?
                            (Mathf.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2)) / 2 :
                            (Mathf.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2;
                    }

                case Ease.InBounce:
                    return 1 - CalculateOutBounce(1 - t);

                case Ease.OutBounce:
                    return CalculateOutBounce(t);

                case Ease.InOutBounce:
                    return t < 0.5 ?
                        (1 - CalculateOutBounce(1 - 2 * t)) / 2 :
                        (1 + CalculateOutBounce(2 * t - 1)) / 2;

                // 기본값으로 선형 반환
                default:
                    return t;
            }
        }

        // OutBounce 이징 계산 (다른 바운스 이징에서 재사용)
        private float CalculateOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1 / d1)
            {
                return n1 * t * t;
            }
            else if (t < 2 / d1)
            {
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            }
            else if (t < 2.5 / d1)
            {
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            }
            else
            {
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
            }
        }

        // 두 점 사이에 선 그리기 (두꺼운 선으로 그리기 위해 수정된 Bresenham 알고리즘)
        private void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // 기본 픽셀 설정 (원래 선)
                if (x0 >= 0 && x0 < PREVIEW_WIDTH && y0 >= 0 && y0 < PREVIEW_HEIGHT)
                {
                    texture.SetPixel(x0, y0, color);

                    // 선을 두껍게 만들기 위해 주변 픽셀도 설정 (원래 픽셀 주변 픽셀 추가)
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            int nx = x0 + i;
                            int ny = y0 + j;

                            // 유효 범위 내에 있는지 확인
                            if (nx >= 0 && nx < PREVIEW_WIDTH && ny >= 0 && ny < PREVIEW_HEIGHT)
                            {
                                // 코너 픽셀은 약간 투명하게 하여 안티앨리어싱 효과 추가
                                if (i != 0 && j != 0)
                                {
                                    Color fadeColor = new Color(color.r, color.g, color.b, 0.5f);
                                    texture.SetPixel(nx, ny, Color.Lerp(texture.GetPixel(nx, ny), fadeColor, 0.5f));
                                }
                                else
                                {
                                    texture.SetPixel(nx, ny, color);
                                }
                            }
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
    }
}