using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects.EditorTools
{
    /// <summary>
    /// 흑백 마스크를 네온용 SDF 텍스처로 굽는 에디터 창.
    /// Tools > CAT > Neon SDF Baker
    /// </summary>
    public class NeonSdfBakerWindow : EditorWindow
    {
        private static readonly int[] DownscaleValues = { 1, 2, 4 };
        private static readonly string[] DownscaleLabels = { "1x (원본)", "2x 축소", "4x 축소" };

        private Texture2D source;
        private NeonSdfBakeSettings settings = NeonSdfBakeSettings.Default;
        private Texture2D result;
        private Material previewMaterial;
        private Vector2 scroll;

        [MenuItem("Tools/CAT/Neon SDF Baker", false, 100)]
        private static void Open()
        {
            var window = GetWindow<NeonSdfBakerWindow>("Neon SDF Baker");
            window.minSize = new Vector2(360f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            if (Selection.activeObject is Texture2D tex) source = tex;
        }

        private void OnDisable()
        {
            if (previewMaterial != null) DestroyImmediate(previewMaterial);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox(
                "흰색 = 네온 튜브, 검은색 = 배경인 마스크 이미지를 넣으세요.\n" +
                "SDF 로 구우면 유리관, 발광 코어, 내외곽 글로우가 모두 셰이더에서 자동 생성됩니다.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            source = (Texture2D)EditorGUILayout.ObjectField("마스크 이미지", source, typeof(Texture2D), false);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("기본 프리셋", EditorStyles.miniButtonLeft))
                    settings = NeonSdfBakeSettings.Default;
                if (GUILayout.Button("모바일 프리셋", EditorStyles.miniButtonRight))
                    settings = NeonSdfBakeSettings.Mobile;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("마스크 해석", EditorStyles.boldLabel);
            settings.channel = (NeonSdfChannel)EditorGUILayout.EnumPopup(
                new GUIContent("채널", "형태로 읽을 채널. 보통 Luminance 로 충분합니다."), settings.channel);
            settings.invert = EditorGUILayout.Toggle(
                new GUIContent("반전", "검은색이 튜브인 마스크라면 켜세요."), settings.invert);
            settings.threshold = EditorGUILayout.Slider(
                new GUIContent("경계 임계값", "이 값보다 밝은 영역을 튜브 내부로 봅니다."), settings.threshold, 0.01f, 0.99f);
            settings.subpixel = EditorGUILayout.Toggle(
                new GUIContent("서브픽셀 보정", "안티앨리어싱된 마스크의 경계를 부드럽게 다듬습니다."), settings.subpixel);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("거리장", EditorStyles.boldLabel);
            settings.padding = EditorGUILayout.IntSlider(
                new GUIContent("글로우 여유 (px)", "글로우가 퍼질 수 있는 최대 거리. 셰이더 반경 1.0 이 이 값에 해당합니다."),
                settings.padding, 4, 256);
            settings.downscale = DownscaleValues[EditorGUILayout.Popup(
                new GUIContent("해상도", "거리장은 축소에 강합니다. 용량이 부담되면 줄이세요."),
                System.Array.IndexOf(DownscaleValues, Mathf.Max(1, settings.downscale)), DownscaleLabels)];

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("임포트", EditorStyles.boldLabel);
            settings.importAsSprite = EditorGUILayout.Toggle(
                new GUIContent("스프라이트로 임포트", "SpriteRenderer / UI Image 에 바로 넣으려면 켜두세요."), settings.importAsSprite);
            settings.generateMipmaps = EditorGUILayout.Toggle(
                new GUIContent("밉맵 생성", "UI 전용이면 꺼두세요. 메모리를 25% 아낍니다."), settings.generateMipmaps);
            using (new EditorGUI.DisabledScope(!settings.importAsSprite))
            {
                settings.pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", settings.pixelsPerUnit);
            }

            DrawSizeInfo();

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(source == null))
            {
                if (GUILayout.Button("SDF 굽기", GUILayout.Height(32f)))
                    BakeNow();
            }

            if (result != null)
            {
                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField(result, typeof(Texture2D), false);
                DrawPreview();

                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Sprite 네온 생성")) CreateNeonObject(result, false);
                    if (GUILayout.Button("UI 네온 생성")) CreateNeonObject(result, true);
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "번짐이 약하면 카메라 HDR 과 Volume 의 Bloom 을 켜고 NeonGlow 의 Exposure 를 1 이상으로 올리세요.\n" +
                "관 바로 주변의 발광은 셰이더가, 화면으로 퍼지는 빛은 Bloom 이 담당합니다.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void DrawSizeInfo()
        {
            if (source == null) return;

            int pad = Mathf.Max(1, settings.padding);
            int down = Mathf.Max(1, settings.downscale);
            int w = Mathf.Max(1, (source.width + pad * 2) / down);
            int h = Mathf.Max(1, (source.height + pad * 2) / down);

            // R8 무압축 기준. 셰이더가 R 채널만 읽으므로 RGB 로 둘 이유가 없다.
            float kb = w * h * (settings.generateMipmaps ? 1.3333f : 1f) / 1024f;
            float quadRatio = (float)(w * h * down * down) / (source.width * source.height);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("출력 크기", $"{source.width}x{source.height}  →  {w}x{h}");
            EditorGUILayout.LabelField("예상 메모리", $"{kb:F0} KB (R8 무압축)");
            EditorGUILayout.LabelField("투명 쿼드 면적", $"원본의 {quadRatio:F2}배");

            if (w > 4096 || h > 4096)
            {
                EditorGUILayout.HelpBox("출력이 4096 을 넘습니다. 해상도 축소를 사용하는 편이 좋습니다.", MessageType.Warning);
            }
        }

        private void BakeNow()
        {
            string path = NeonSdfBaker.Bake(source, settings);
            if (string.IsNullOrEmpty(path)) return;

            result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            EditorGUIUtility.PingObject(result);
            Debug.Log($"[NeonSdfBaker] SDF 생성 완료: {path}", result);
        }

        private void DrawPreview()
        {
            if (previewMaterial == null)
            {
                Shader shader = Shader.Find(NeonGlow.SpriteShaderName);
                if (shader == null) return;
                previewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            float aspect = (float)result.height / Mathf.Max(1, result.width);
            Rect rect = GUILayoutUtility.GetRect(position.width - 30f, (position.width - 30f) * aspect);
            EditorGUI.DrawRect(rect, new Color(0.06f, 0.06f, 0.08f));
            EditorGUI.DrawPreviewTexture(rect, result, previewMaterial, ScaleMode.ScaleToFit);
        }

        #region 오브젝트 생성
        [MenuItem("GameObject/CAT/Neon Sign (Sprite)", false, 10)]
        private static void CreateSpriteNeon(MenuCommand command)
        {
            CreateNeonObject(null, false, command.context as GameObject);
        }

        [MenuItem("GameObject/CAT/Neon Sign (UI)", false, 11)]
        private static void CreateUINeon(MenuCommand command)
        {
            CreateNeonObject(null, true, command.context as GameObject);
        }

        private static void CreateNeonObject(Texture2D sdf, bool asUI, GameObject parent = null)
        {
            var go = new GameObject(asUI ? "Neon Sign (UI)" : "Neon Sign");
            Sprite sprite = sdf != null ? AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(sdf)) : null;

            if (asUI)
            {
                var rect = go.AddComponent<RectTransform>();
                var image = go.AddComponent<Image>();
                image.sprite = sprite;
                if (sprite != null) rect.sizeDelta = sprite.rect.size;
            }
            else
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
            }

            go.AddComponent<NeonGlow>();

            GameObjectUtility.SetParentAndAlign(go, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create Neon Sign");
            Selection.activeGameObject = go;
        }
        #endregion
    }
}
