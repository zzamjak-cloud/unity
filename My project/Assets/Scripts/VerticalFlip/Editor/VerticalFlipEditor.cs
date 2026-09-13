using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CAT.Effects
{
    /// <summary>
    /// 씬 뷰 프리뷰 구동기.
    /// 인스펙터 Editor 인스턴스는 에셋 임포트 등으로 수시로 재생성되므로,
    /// 프리뷰 상태를 Editor에 두면 재생 직후 꺼져 버린다. static으로 분리해 수명을 분리한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class VerticalFlipPreviewDriver
    {
        public const float MaxPreviewSeconds = 60f;

        private static VerticalFlip target;
        private static double lastUpdateTime;
        private static float elapsed;

        static VerticalFlipPreviewDriver()
        {
            EditorApplication.update += Update;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += _ => Stop();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (a, b) => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        public static bool IsRunning(VerticalFlip flip)
            => flip != null && target == flip && flip.IsEditorPreviewActive;

        public static float RemainingSeconds => Mathf.Max(0f, MaxPreviewSeconds - elapsed);

        public static void Start(VerticalFlip flip, float waitOverride)
        {
            Stop();

            if (flip == null || !flip.EditorPreviewBegin(waitOverride))
                return;

            target = flip;
            elapsed = 0f;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        /// <summary>정지 상태에서 진행도만 직접 지정한다. 생성된 머티리얼은 Stop에서 함께 정리된다</summary>
        public static void Scrub(VerticalFlip flip, float progress)
        {
            if (flip == null)
                return;

            if (target != null && target != flip)
                Stop();

            target = flip;
            flip.EditorPreviewScrub(progress);
            SceneView.RepaintAll();
        }

        public static void Stop()
        {
            if (target != null)
            {
                target.EditorPreviewEnd();
                SceneView.RepaintAll();
            }

            target = null;
            elapsed = 0f;
        }

        // 다른 오브젝트를 선택했을 때만 멈춘다(Editor 재생성과 구분)
        private static void OnSelectionChanged()
        {
            if (target != null && Selection.activeGameObject != target.gameObject)
                Stop();
        }

        private static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastUpdateTime);
            lastUpdateTime = now;

            if (target == null || !target.IsEditorPreviewActive)
                return;

            // 에디터가 멈췄다 재개될 때 한 번에 튀는 것을 막는다
            delta = Mathf.Min(delta, 0.1f);

            elapsed += delta;
            if (elapsed >= MaxPreviewSeconds)
            {
                Stop();
                return;
            }

            target.EditorPreviewTick(delta);
            SceneView.RepaintAll();
        }
    }

    [CustomEditor(typeof(VerticalFlip))]
    public class VerticalFlipEditor : Editor
    {
        // 실제 대기 간격(최대 10초)을 그대로 쓰면 프리뷰가 대부분 정지 화면이라 기본값을 짧게 잡는다
        private const float ShortPreviewWait = 0.6f;

        private float scrubProgress;
        private bool useRealInterval;

        // 스프라이트 임포트 경고 캐시 (AssetImporter 조회가 비싸다)
        private Sprite cachedWarningFirst;
        private Sprite cachedWarningSecond;
        private List<string> cachedWarnings;

        // 프로퍼티 캐싱
        private SerializedProperty firstSpriteProperty;
        private SerializedProperty secondSpriteProperty;
        private SerializedProperty sliceCountProperty;
        private SerializedProperty flipDurationProperty;
        private SerializedProperty flipOffsetBetweenSlicesProperty;
        private SerializedProperty timeBetweenFlipsProperty;
        private SerializedProperty showColumnLinesProperty;
        private SerializedProperty lineColorProperty;
        private SerializedProperty lineWidthProperty;
        private SerializedProperty useLowQualityOnMobileProperty;

        private void OnEnable()
        {
            firstSpriteProperty = serializedObject.FindProperty("firstSprite");
            secondSpriteProperty = serializedObject.FindProperty("secondSprite");
            sliceCountProperty = serializedObject.FindProperty("sliceCount");
            flipDurationProperty = serializedObject.FindProperty("flipDuration");
            flipOffsetBetweenSlicesProperty = serializedObject.FindProperty("flipOffsetBetweenSlices");
            timeBetweenFlipsProperty = serializedObject.FindProperty("timeBetweenFlips");
            showColumnLinesProperty = serializedObject.FindProperty("showColumnLines");
            lineColorProperty = serializedObject.FindProperty("lineColor");
            lineWidthProperty = serializedObject.FindProperty("lineWidth");
            useLowQualityOnMobileProperty = serializedObject.FindProperty("useLowQualityOnMobile");

            // 프리뷰 구동은 static 드라이버가 맡는다. 여기서는 인스펙터 갱신만 담당한다
            EditorApplication.update += RepaintWhilePreviewing;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintWhilePreviewing;
        }

        private bool IsPreviewing => VerticalFlipPreviewDriver.IsRunning(target as VerticalFlip);

        private void RepaintWhilePreviewing()
        {
            if (IsPreviewing)
                Repaint();
        }

        private void StartPreview(VerticalFlip flip)
        {
            VerticalFlipPreviewDriver.Start(flip, useRealInterval ? -1f : ShortPreviewWait);
        }

        private void StopPreview()
        {
            VerticalFlipPreviewDriver.Stop();
        }

        /// <summary>플립 UV 가정을 깨뜨리는 스프라이트 임포트 설정을 검사한다</summary>
        private List<string> CollectSpriteWarnings(VerticalFlip flipComponent, bool isUI)
        {
            List<string> warnings = new List<string>();

            Sprite[] sprites = { flipComponent.firstSprite, flipComponent.secondSprite };
            foreach (Sprite sprite in sprites)
            {
                if (sprite == null)
                    continue;

                if (sprite.packingRotation != SpritePackingRotation.None)
                {
                    warnings.Add($"'{sprite.name}'이(가) 회전 패킹되어 있습니다. Sprite Atlas의 Packing 설정에서 Allow Rotation을 꺼야 플립 UV가 맞습니다.");
                }

                // Tight 메시는 스프라이트 외곽만 폴리곤으로 만들기 때문에 플립으로 늘어난 영역이 잘려 나간다
                if (!isUI)
                {
                    string path = AssetDatabase.GetAssetPath(sprite);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        TextureImporterSettings settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        if (settings.spriteMeshType == SpriteMeshType.Tight)
                        {
                            warnings.Add($"'{sprite.name}'의 Mesh Type이 Tight입니다. Full Rect로 바꿔야 플립 중 잘리지 않습니다.");
                        }
                    }
                }
            }

            return warnings;
        }

        private void DrawPreviewControls(VerticalFlip flipComponent, bool hasSprites)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("씬 뷰 미리보기", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서는 컴포넌트가 직접 재생됩니다.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            bool previewing = IsPreviewing;

            using (new EditorGUI.DisabledScope(!hasSprites))
            {
                string label = previewing
                    ? $"정지  ({Mathf.CeilToInt(VerticalFlipPreviewDriver.RemainingSeconds)}초 남음)"
                    : "재생";

                if (GUILayout.Button(label, GUILayout.Height(26)))
                {
                    if (previewing)
                        StopPreview();
                    else
                        StartPreview(flipComponent);
                }
            }

            // 재생 중에도 대기 구간이면 화면이 멈춰 보이므로 현재 구간을 명시한다
            using (new EditorGUI.DisabledScope(previewing))
            {
                useRealInterval = EditorGUILayout.ToggleLeft(
                    $"실제 대기 간격 사용 ({flipComponent.timeBetweenFlips:0.#}초)", useRealInterval);
            }

            if (previewing)
            {
                Rect bar = EditorGUILayout.GetControlRect(false, 18f);
                string phase = flipComponent.EditorPreviewIsFlipping
                    ? $"플립 중  {flipComponent.EditorPreviewPhase * flipComponent.EditorFlipDuration:0.00} / {flipComponent.EditorFlipDuration:0.00}s"
                    : $"대기 중  ({flipComponent.EditorPreviewWait:0.#}초 간격)";
                EditorGUI.ProgressBar(bar, flipComponent.EditorPreviewPhase, phase);
            }

            // 정지 상태에서는 슬라이더로 플립 중간 상태를 직접 확인할 수 있다
            using (new EditorGUI.DisabledScope(!hasSprites || previewing))
            {
                EditorGUI.BeginChangeCheck();
                scrubProgress = EditorGUILayout.Slider("진행도", scrubProgress, 0f, flipComponent.EditorFlipDuration);
                if (EditorGUI.EndChangeCheck())
                {
                    VerticalFlipPreviewDriver.Scrub(flipComponent, scrubProgress);
                }
            }

            if (!hasSprites)
                EditorGUILayout.HelpBox("두 스프라이트를 모두 할당하면 미리보기를 재생할 수 있습니다.", MessageType.Info);
            else if (previewing)
                EditorGUILayout.HelpBox($"씬 뷰에서 재생 중입니다. {VerticalFlipPreviewDriver.MaxPreviewSeconds:0}초 후 자동으로 정지합니다.", MessageType.None);
            else
                EditorGUILayout.HelpBox("재생하면 씬 뷰의 실제 오브젝트가 플립합니다. 씬 뷰 창이 보이는 상태여야 합니다.", MessageType.None);

            EditorGUILayout.EndVertical();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            VerticalFlip flipComponent = (VerticalFlip)target;
            bool isUI = flipComponent.GetComponent<Image>() != null;
            bool hasSprites = flipComponent.firstSprite != null && flipComponent.secondSprite != null;

            EditorGUILayout.Space();
            DrawPreviewControls(flipComponent, hasSprites);
            EditorGUILayout.Space();

            // 스프라이트 설정 섹션
            EditorGUILayout.PropertyField(firstSpriteProperty);
            EditorGUILayout.PropertyField(secondSpriteProperty);
            EditorGUILayout.Space();

            // 애니메이션 설정 섹션
            EditorGUILayout.PropertyField(sliceCountProperty);
            EditorGUILayout.PropertyField(flipDurationProperty);
            EditorGUILayout.PropertyField(flipOffsetBetweenSlicesProperty);
            EditorGUILayout.PropertyField(timeBetweenFlipsProperty);
            EditorGUILayout.Space();

            // 라인 설정 섹션
            EditorGUILayout.PropertyField(showColumnLinesProperty);
            if (showColumnLinesProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(lineColorProperty);
                EditorGUILayout.PropertyField(lineWidthProperty);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            // 성능 최적화 섹션
            EditorGUILayout.PropertyField(useLowQualityOnMobileProperty, new GUIContent("모바일 저사양 모드"));
            EditorGUILayout.Space();

            // 스프라이트 임포트 설정 경고
            if (hasSprites)
            {
                if (cachedWarnings == null
                    || cachedWarningFirst != flipComponent.firstSprite
                    || cachedWarningSecond != flipComponent.secondSprite)
                {
                    cachedWarningFirst = flipComponent.firstSprite;
                    cachedWarningSecond = flipComponent.secondSprite;
                    cachedWarnings = CollectSpriteWarnings(flipComponent, isUI);
                }

                foreach (string warning in cachedWarnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            // 컴포넌트 정보 표시
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("컴포넌트 타입:", isUI ? "UI Image" : "Sprite Renderer", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("사용 중인 셰이더:", isUI ? VerticalFlip.UIShaderName : VerticalFlip.SpriteShaderName, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        // 씬 뷰에서 슬라이스 경계 표시
        [DrawGizmo(GizmoType.Selected)]
        static void DrawGizmo(VerticalFlip flipComponent, GizmoType gizmoType)
        {
            if (!flipComponent.showColumnLines)
                return;

            SpriteRenderer spriteRenderer = flipComponent.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                return;

            Bounds bounds = spriteRenderer.bounds;
            float sliceWidth = bounds.size.x / flipComponent.sliceCount;

            Gizmos.color = flipComponent.lineColor;

            for (int i = 1; i < flipComponent.sliceCount; i++)
            {
                float x = bounds.min.x + i * sliceWidth;
                Gizmos.DrawLine(new Vector3(x, bounds.min.y, bounds.center.z),
                                new Vector3(x, bounds.max.y, bounds.center.z));
            }
        }
    }
}
