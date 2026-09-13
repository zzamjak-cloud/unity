using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects.EditorTools
{
    /// <summary>NeonGlow 커스텀 인스펙터. 프리셋과 SDF 텍스처 설정 검증을 제공한다.</summary>
    [CustomEditor(typeof(NeonGlow))]
    [CanEditMultipleObjects]
    public class NeonGlowEditor : Editor
    {
        private static readonly string[] SectionKeys =
        {
            "CAT.NeonGlow.Tube", "CAT.NeonGlow.Inner", "CAT.NeonGlow.Outer",
            "CAT.NeonGlow.Global", "CAT.NeonGlow.Flicker", "CAT.NeonGlow.Batching",
            "CAT.NeonGlow.Advanced"
        };

        private SerializedProperty tubeColor, coreColor, tubeWidth, tubeShading, coreWidth, coreSoftness, tubeIntensity, tubeOpacity;
        private SerializedProperty innerGlowColor, innerGlowRadius, innerGlowFalloff, innerGlowIntensity;
        private SerializedProperty outerGlowColor, outerGlowRadius, outerGlowFalloff, outerGlowIntensity;
        private SerializedProperty exposure, tint;
        private SerializedProperty flickerMode, flickerSpeed, flickerAmount, flickerGlowOnly, warmupDuration;
        private SerializedProperty useSharedMaterial, sharedMaterialAsset;
        private SerializedProperty spriteShader, uiShader;
        private bool batchingChanged;

        private void OnEnable()
        {
            EditorApplication.update += RepaintWhilePreviewing;

            // 도메인 리로드 직후에는 대상이 아직 복구되지 않은 채로 불릴 수 있다.
            // 이때 serializedObject 에 접근하면 SerializedObjectNotCreatableException 이 난다.
            if (targets == null || targets.Length == 0 || targets[0] == null) return;

            tubeColor = serializedObject.FindProperty("tubeColor");
            coreColor = serializedObject.FindProperty("coreColor");
            tubeWidth = serializedObject.FindProperty("tubeWidth");
            tubeShading = serializedObject.FindProperty("tubeShading");
            coreWidth = serializedObject.FindProperty("coreWidth");
            coreSoftness = serializedObject.FindProperty("coreSoftness");
            tubeIntensity = serializedObject.FindProperty("tubeIntensity");
            tubeOpacity = serializedObject.FindProperty("tubeOpacity");

            innerGlowColor = serializedObject.FindProperty("innerGlowColor");
            innerGlowRadius = serializedObject.FindProperty("innerGlowRadius");
            innerGlowFalloff = serializedObject.FindProperty("innerGlowFalloff");
            innerGlowIntensity = serializedObject.FindProperty("innerGlowIntensity");

            outerGlowColor = serializedObject.FindProperty("outerGlowColor");
            outerGlowRadius = serializedObject.FindProperty("outerGlowRadius");
            outerGlowFalloff = serializedObject.FindProperty("outerGlowFalloff");
            outerGlowIntensity = serializedObject.FindProperty("outerGlowIntensity");

            exposure = serializedObject.FindProperty("exposure");
            tint = serializedObject.FindProperty("tint");

            flickerMode = serializedObject.FindProperty("flickerMode");
            flickerSpeed = serializedObject.FindProperty("flickerSpeed");
            flickerAmount = serializedObject.FindProperty("flickerAmount");
            flickerGlowOnly = serializedObject.FindProperty("flickerGlowOnly");
            warmupDuration = serializedObject.FindProperty("warmupDuration");

            useSharedMaterial = serializedObject.FindProperty("useSharedMaterial");
            sharedMaterialAsset = serializedObject.FindProperty("sharedMaterialAsset");
            spriteShader = serializedObject.FindProperty("spriteShader");
            uiShader = serializedObject.FindProperty("uiShader");
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintWhilePreviewing;
        }

        /// <summary>프리뷰 중에는 남은 시간 표시를 위해 인스펙터를 계속 다시 그린다.</summary>
        private void RepaintWhilePreviewing()
        {
            if (targets != null && targets.Length == 1 && targets[0] != null
                && NeonGlowPreviewDriver.IsRunning((NeonGlow)targets[0]))
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            if (tubeColor == null) return;

            serializedObject.Update();

            DrawTextureDiagnostics();
            DrawPresetBar();

            if (Section(0, "유리관"))
            {
                EditorGUILayout.PropertyField(tubeColor, new GUIContent("관 색상"));
                EditorGUILayout.PropertyField(coreColor, new GUIContent("코어 색상"));
                EditorGUILayout.PropertyField(tubeWidth, new GUIContent("관 두께"));
                EditorGUILayout.PropertyField(tubeShading, new GUIContent("단면 음영"));
                EditorGUILayout.PropertyField(coreWidth, new GUIContent("코어 폭"));
                EditorGUILayout.PropertyField(coreSoftness, new GUIContent("코어 부드러움"));
                EditorGUILayout.PropertyField(tubeIntensity, new GUIContent("관 밝기"));
                EditorGUILayout.PropertyField(tubeOpacity, new GUIContent("관 불투명도"));
            }

            if (Section(1, "내부 글로우"))
            {
                EditorGUILayout.PropertyField(innerGlowColor, new GUIContent("색상"));
                EditorGUILayout.PropertyField(innerGlowRadius, new GUIContent("반경"));
                EditorGUILayout.PropertyField(innerGlowFalloff, new GUIContent("감쇠"));
                EditorGUILayout.PropertyField(innerGlowIntensity, new GUIContent("강도"));
            }

            if (Section(2, "외부 글로우"))
            {
                EditorGUILayout.PropertyField(outerGlowColor, new GUIContent("색상"));
                EditorGUILayout.PropertyField(outerGlowRadius, new GUIContent("반경"));
                EditorGUILayout.PropertyField(outerGlowFalloff, new GUIContent("감쇠"));
                EditorGUILayout.PropertyField(outerGlowIntensity, new GUIContent("강도"));
            }

            if (Section(3, "전역"))
            {
                EditorGUILayout.PropertyField(exposure, new GUIContent("Exposure (HDR)"));
                EditorGUILayout.PropertyField(tint, new GUIContent("틴트"));

                if (exposure.floatValue <= 1.01f)
                {
                    EditorGUILayout.HelpBox(
                        "Exposure 가 1 이하면 Bloom 임계값을 넘지 못해 화면으로 번지지 않습니다.", MessageType.None);
                }
            }

            if (Section(4, "깜빡임"))
            {
                EditorGUILayout.PropertyField(flickerMode, new GUIContent("모드"));
                using (new EditorGUI.DisabledScope(flickerMode.enumValueIndex == 0))
                {
                    EditorGUILayout.PropertyField(flickerSpeed, new GUIContent("속도"));
                    EditorGUILayout.PropertyField(flickerAmount, new GUIContent("세기"));
                    EditorGUILayout.PropertyField(flickerGlowOnly, new GUIContent("글로우만 깜빡임"));
                    if (flickerMode.enumValueIndex == (int)NeonFlickerMode.Warmup)
                        EditorGUILayout.PropertyField(warmupDuration, new GUIContent("안정화 시간"));
                }

                EditorGUILayout.Space(4f);
                DrawPreviewControls();
            }

            if (Section(5, "배칭 (모바일)"))
            {
                DrawBatchingSection();
            }

            if (Section(6, "고급"))
            {
                EditorGUILayout.PropertyField(spriteShader, new GUIContent("Sprite 셰이더"));
                EditorGUILayout.PropertyField(uiShader, new GUIContent("UI 셰이더"));
                EditorGUILayout.HelpBox(
                    "비워두면 이름으로 찾습니다. 빌드에서 셰이더가 스트리핑되지 않도록 직접 지정해 두는 편이 안전합니다.",
                    MessageType.None);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (Object t in targets)
                {
                    var neon = (NeonGlow)t;
                    // 공유 머티리얼 전환은 머티리얼 자체를 갈아끼우므로 재구성이 필요하다.
                    if (batchingChanged) neon.Rebuild();
                    else neon.UpdateShaderProperties();
                }
            }
            batchingChanged = false;
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("씬 뷰 테스트", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서는 깜빡임이 그대로 재생됩니다.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox("테스트는 한 번에 하나의 오브젝트만 지원합니다.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            var neon = (NeonGlow)target;

            if (flickerMode.enumValueIndex == (int)NeonFlickerMode.None)
            {
                EditorGUILayout.HelpBox("깜빡임 모드를 None 이외로 바꾸면 테스트할 수 있습니다.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            if (neon.IsUsingSharedMaterial)
            {
                EditorGUILayout.HelpBox(
                    "공유 머티리얼 모드에서는 테스트를 지원하지 않습니다.\n같은 머티리얼을 쓰는 다른 간판까지 영향을 받기 때문입니다.",
                    MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            bool previewing = NeonGlowPreviewDriver.IsRunning(neon);
            float max = NeonGlowPreviewDriver.MaxPreviewSeconds;
            float remaining = NeonGlowPreviewDriver.RemainingSeconds;

            string label = previewing
                ? $"테스트 중지  ({remaining:F0}초 남음)"
                : $"씬 뷰에서 {max:F0}초 테스트";

            if (GUILayout.Button(label, GUILayout.Height(26f)))
            {
                if (previewing) NeonGlowPreviewDriver.Stop();
                else NeonGlowPreviewDriver.Start(neon);
            }

            if (previewing)
            {
                Rect bar = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(bar, max > 0f ? remaining / max : 0f, string.Empty);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "씬 뷰에서 바로 재생됩니다. 다른 오브젝트를 선택하면 자동으로 멈춥니다.",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBatchingSection()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(useSharedMaterial, new GUIContent("공유 머티리얼 사용"));
            using (new EditorGUI.DisabledScope(!useSharedMaterial.boolValue))
            {
                EditorGUILayout.PropertyField(sharedMaterialAsset, new GUIContent("머티리얼 에셋"));
            }
            if (EditorGUI.EndChangeCheck()) batchingChanged = true;

            bool sharing = useSharedMaterial.boolValue && sharedMaterialAsset.objectReferenceValue != null;

            if (sharing)
            {
                EditorGUILayout.HelpBox(
                    "공유 머티리얼을 사용 중입니다. 위 파라미터 대신 머티리얼 에셋의 값이 적용됩니다.\n" +
                    "같은 머티리얼을 쓰는 간판끼리 드로우콜이 합쳐집니다.",
                    MessageType.Info);

                if (targets.Length == 1 && GUILayout.Button("현재 인스펙터 값을 머티리얼에 덮어쓰기"))
                    OverwriteSharedMaterial();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "오브젝트마다 머티리얼 인스턴스가 생성되어 간판 1개당 드로우콜 1개가 나갑니다.\n" +
                    "같은 설정의 간판이 여러 개면 아래 버튼으로 머티리얼을 만들어 공유하세요.",
                    MessageType.None);

                if (targets.Length == 1 && GUILayout.Button("현재 설정을 머티리얼로 저장하고 공유"))
                    SaveAsSharedMaterial();
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "모바일 체크리스트\n" +
                "· SDF 는 Neon SDF Baker 의 '모바일 프리셋' 으로 구우세요 (여유 16px, 1/2 해상도, 밉맵 off).\n" +
                "· Bloom 은 High Quality Filtering 끄고 Skip Iterations 를 올리세요.\n" +
                "· 저사양 티어는 Bloom 없이 외부 글로우 반경만 키워도 됩니다.\n" +
                "· 카메라 HDR 은 32bit(R11G11B10) 로 두세요. 64bit 는 대역폭을 2배 씁니다.",
                MessageType.None);
        }

        private void SaveAsSharedMaterial()
        {
            var neon = (NeonGlow)target;
            Shader shader = neon.Material != null ? neon.Material.shader : null;
            if (shader == null)
            {
                Debug.LogError("[NeonGlow] 머티리얼이 아직 만들어지지 않아 저장할 수 없습니다.", neon);
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "네온 머티리얼 저장", neon.name + "_Neon", "mat", "공유할 머티리얼 에셋을 저장합니다.");
            if (string.IsNullOrEmpty(path)) return;

            var mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            neon.ApplyTo(mat);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(neon, "Use Shared Neon Material");
            neon.sharedMaterialAsset = mat;
            neon.useSharedMaterial = true;
            EditorUtility.SetDirty(neon);
            neon.Rebuild();
            serializedObject.Update();
        }

        private void OverwriteSharedMaterial()
        {
            var neon = (NeonGlow)target;
            if (neon.sharedMaterialAsset == null) return;

            Undo.RecordObject(neon.sharedMaterialAsset, "Overwrite Neon Material");
            neon.ApplyTo(neon.sharedMaterialAsset);
            EditorUtility.SetDirty(neon.sharedMaterialAsset);
            AssetDatabase.SaveAssets();
        }

        private static bool Section(int index, string title)
        {
            bool state = EditorPrefs.GetBool(SectionKeys[index], true);
            bool next = EditorGUILayout.BeginFoldoutHeaderGroup(state, title);
            if (next != state) EditorPrefs.SetBool(SectionKeys[index], next);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return next;
        }

        private void DrawPresetBar()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("프리셋", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                PresetButton(NeonPreset.ClassicRed, "레드");
                PresetButton(NeonPreset.AmberOrange, "앰버");
                PresetButton(NeonPreset.ToxicGreen, "그린");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                PresetButton(NeonPreset.IceBlue, "블루");
                PresetButton(NeonPreset.HotPink, "핑크");
                PresetButton(NeonPreset.WarmWhite, "화이트");
            }

            EditorGUILayout.Space(4f);
        }

        private void PresetButton(NeonPreset preset, string label)
        {
            if (!GUILayout.Button(label, EditorStyles.miniButton)) return;

            foreach (Object t in targets)
            {
                var neon = (NeonGlow)t;
                Undo.RecordObject(neon, "Apply Neon Preset");
                neon.ApplyPreset(preset);
                EditorUtility.SetDirty(neon);
            }
            serializedObject.Update();
        }

        /// <summary>
        /// 붙어 있는 텍스처가 SDF 로 구워진 것인지, 임포트 설정이 거리장에 맞는지 확인한다.
        /// 잘못된 설정은 경계가 흐려지거나 글로우에 밴딩을 만든다.
        /// </summary>
        private void DrawTextureDiagnostics()
        {
            if (targets.Length != 1) return;

            var neon = (NeonGlow)target;
            Texture texture = FindTexture(neon.gameObject);

            if (texture == null)
            {
                EditorGUILayout.HelpBox(
                    "SpriteRenderer / Image / RawImage 에 텍스처가 없습니다.\n" +
                    "Tools > CAT > Neon SDF Baker 로 마스크를 구운 뒤 그 결과를 넣으세요.",
                    MessageType.Warning);
                if (GUILayout.Button("Neon SDF Baker 열기")) EditorApplication.ExecuteMenuItem("Tools/CAT/Neon SDF Baker");
                EditorGUILayout.Space(4f);
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;

            int spread = NeonSdfBaker.ReadSpread(texture);
            bool looksBaked = spread > 0;
            bool badSrgb = ti.sRGBTexture;
            bool badCompression = ti.textureCompression != TextureImporterCompression.Uncompressed;

            if (!looksBaked)
            {
                EditorGUILayout.HelpBox(
                    $"'{texture.name}' 은 SDF 로 구워진 텍스처가 아닌 것 같습니다.\n" +
                    "원본 마스크를 그대로 쓰면 글로우가 생기지 않습니다.",
                    MessageType.Warning);
                if (GUILayout.Button("Neon SDF Baker 열기")) EditorApplication.ExecuteMenuItem("Tools/CAT/Neon SDF Baker");
            }
            else if (badSrgb || badCompression)
            {
                EditorGUILayout.HelpBox(
                    "SDF 텍스처의 임포트 설정이 거리장에 맞지 않습니다.\n" +
                    (badSrgb ? "· sRGB 가 켜져 있어 경계 위치가 틀어집니다.\n" : "") +
                    (badCompression ? "· 블록 압축 때문에 글로우에 밴딩이 생깁니다.\n" : ""),
                    MessageType.Warning);

                if (GUILayout.Button("임포트 설정 자동 수정"))
                {
                    ti.sRGBTexture = false;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                }
            }
            else
            {
                EditorGUILayout.LabelField("SDF 여유", $"{spread} px", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4f);
        }

        private static Texture FindTexture(GameObject go)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) return sr.sprite != null ? sr.sprite.texture : null;

            var image = go.GetComponent<Image>();
            if (image != null) return image.sprite != null ? image.sprite.texture : null;

            var raw = go.GetComponent<RawImage>();
            if (raw != null) return raw.texture;

            return null;
        }
    }
}
