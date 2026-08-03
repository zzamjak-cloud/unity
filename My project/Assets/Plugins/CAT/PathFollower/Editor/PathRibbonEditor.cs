using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using CAT.Utility;

/// <summary>
/// PathRibbon 인스펙터.
/// - 모드 / 자식 렌더러 / 샘플 개수 / 타일 길이 등 런타임 상태 표시
/// - 자식 설정 오류 시 경고 출력
/// - Rebuild 버튼 제공
/// </summary>
[CustomEditor(typeof(PathRibbon))]
[CanEditMultipleObjects]
public class PathRibbonEditor : Editor
{
        private SerializedProperty _scrollSpeedProp;
        private SerializedProperty _samplesPerUnitProp;
        private SerializedProperty _overrideSamplesProp;
        private SerializedProperty _manualSamplesProp;
        private SerializedProperty _autoSubCanvasProp;
        private SerializedProperty _flipXProp;
        private SerializedProperty _flipYProp;

        private static readonly GUIContent LabelScrollSpeed   = new GUIContent("Scroll Speed", "Loop 경로에서 UV 스크롤 속도 (units/sec). 음수 = 역방향");
        private static readonly GUIContent LabelSamplesPerU   = new GUIContent("Samples / Unit", "경로 1유닛당 정점 개수 (자동 모드)");
        private static readonly GUIContent LabelOverride      = new GUIContent("Override Samples", "샘플 개수를 수동으로 지정");
        private static readonly GUIContent LabelManualSamples = new GUIContent("Manual Samples", "수동 샘플 개수");
        private static readonly GUIContent LabelAutoSubCanvas = new GUIContent("Auto Sub Canvas", "UI 모드에서 서브 Canvas 를 자동 추가하여 상위 Canvas rebuild 격리 (UV 스크롤/모핑 사용 시 권장)");
        private static readonly GUIContent LabelFlipX         = new GUIContent("Flip X", "가로(경로 방향) 반전. Sprite 모드에서는 자식 SpriteRenderer.flipX 와 XOR 결합");
        private static readonly GUIContent LabelFlipY         = new GUIContent("Flip Y", "세로(리본 두께) 반전. Sprite 모드에서는 자식 SpriteRenderer.flipY 와 XOR 결합");

        private void OnEnable()
        {
            _scrollSpeedProp    = serializedObject.FindProperty(nameof(PathRibbon.scrollSpeed));
            _samplesPerUnitProp = serializedObject.FindProperty(nameof(PathRibbon.samplesPerUnit));
            _overrideSamplesProp= serializedObject.FindProperty(nameof(PathRibbon.overrideSamples));
            _manualSamplesProp  = serializedObject.FindProperty(nameof(PathRibbon.manualSamples));
            _autoSubCanvasProp  = serializedObject.FindProperty(nameof(PathRibbon.autoCreateSubCanvas));
            _flipXProp          = serializedObject.FindProperty(nameof(PathRibbon.flipX));
            _flipYProp          = serializedObject.FindProperty(nameof(PathRibbon.flipY));
        }

        public override void OnInspectorGUI()
        {
            var ribbon = (PathRibbon)target;

            serializedObject.Update();

            // ── 기본 설정 ──
            EditorGUILayout.LabelField("Ribbon", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_scrollSpeedProp, LabelScrollSpeed);

            // Flip 가로/세로 — 가로형 토글 레이아웃
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_flipXProp, LabelFlipX);
                EditorGUILayout.PropertyField(_flipYProp, LabelFlipY);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Mesh Resolution", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_overrideSamplesProp, LabelOverride);
            using (new EditorGUI.DisabledScope(_overrideSamplesProp.boolValue))
            {
                EditorGUILayout.PropertyField(_samplesPerUnitProp, LabelSamplesPerU);
            }
            using (new EditorGUI.DisabledScope(!_overrideSamplesProp.boolValue))
            {
                EditorGUILayout.PropertyField(_manualSamplesProp, LabelManualSamples);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Performance (Mobile)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoSubCanvasProp, LabelAutoSubCanvas);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            DrawRuntimeInfo(ribbon);

            EditorGUILayout.Space(6);
            DrawWarnings(ribbon);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Rebuild Mesh"))
            {
                foreach (var t in targets)
                {
                    if (t is PathRibbon pr) pr.RebuildMesh();
                }
                SceneView.RepaintAll();
            }
        }

        private void DrawRuntimeInfo(PathRibbon ribbon)
        {
            EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("UI Mode (auto)", ribbon.IsUIMode);
                EditorGUILayout.IntField("Sample Count", ribbon.ActualSampleCount);
                EditorGUILayout.FloatField("Total Path Length", ribbon.TotalPathLength);
                EditorGUILayout.FloatField("Effective Tile Length", ribbon.EffectiveTileLength);
            }
        }

        private void DrawWarnings(PathRibbon ribbon)
        {
            // Scroll Speed 는 닫힌(Loop) 경로 + 플레이 모드에서만 동작
            if (!Mathf.Approximately(ribbon.scrollSpeed, 0f))
            {
                var follower = ribbon.GetComponent<PathFollower>();
                if (follower != null && !follower.IsLoop)
                {
                    EditorGUILayout.HelpBox(
                        "Scroll Speed(UV 스크롤)는 닫힌(Loop) 경로에서만 동작합니다. PathFollower 의 Loop 를 켜세요. " +
                        "(스크롤 애니메이션은 플레이 모드에서만 재생됩니다)",
                        MessageType.Warning);
                }
            }

            // 자식 렌더러 검사
            bool hasChildRenderer = false;
            SpriteRenderer foundSR = null;
            Image foundImg = null;

            int cc = ribbon.transform.childCount;
            for (int i = 0; i < cc; i++)
            {
                var c = ribbon.transform.GetChild(i);
                if (ribbon.IsUIMode)
                {
                    var img = c.GetComponent<Image>();
                    if (img != null) { foundImg = img; hasChildRenderer = true; break; }
                }
                else
                {
                    var sr = c.GetComponent<SpriteRenderer>();
                    if (sr != null) { foundSR = sr; hasChildRenderer = true; break; }
                }
            }

            if (!hasChildRenderer)
            {
                EditorGUILayout.HelpBox(
                    ribbon.IsUIMode
                        ? "자식 오브젝트에 Image (Type=Tiled) 컴포넌트를 배치하세요."
                        : "자식 오브젝트에 SpriteRenderer (Draw Mode=Tiled) 컴포넌트를 배치하세요.",
                    MessageType.Warning);
                return;
            }

            // Draw Mode / Type 검사
            if (foundSR != null && foundSR.drawMode != SpriteDrawMode.Tiled)
            {
                EditorGUILayout.HelpBox(
                    "자식 SpriteRenderer의 Draw Mode 가 Tiled 가 아닙니다. Size 필드가 필요하므로 Tiled 로 변경하세요.",
                    MessageType.Warning);
            }
            if (foundImg != null && foundImg.type != Image.Type.Tiled)
            {
                EditorGUILayout.HelpBox(
                    "자식 Image의 Type 이 Tiled 가 아닙니다. Type=Tiled 로 변경하세요.",
                    MessageType.Warning);
            }

            // URP 2D Sprite 셰이더는 MeshRenderer 비호환 → 폴백 material 자동 대체 안내
            if (foundSR != null && PathRibbon.IsSpriteOnlyShader(foundSR.sharedMaterial))
            {
                EditorGUILayout.HelpBox(
                    "자식 SpriteRenderer 의 material(URP 2D Sprite 셰이더)은 MeshRenderer 에서 렌더링되지 않으므로, " +
                    "PathRibbon 전용 폴백 material(CAT/PathFollower/Ribbon-Unlit)로 자동 대체됩니다.",
                    MessageType.Info);
            }

            // 텍스처 Wrap Mode 검사
            Texture tex = null;
            if (foundSR != null && foundSR.sprite != null) tex = foundSR.sprite.texture;
            if (foundImg != null && foundImg.sprite != null) tex = foundImg.sprite.texture;

            if (tex != null && tex.wrapMode != TextureWrapMode.Repeat && tex.wrapMode != TextureWrapMode.MirrorOnce && tex.wrapMode != TextureWrapMode.Mirror)
            {
                EditorGUILayout.HelpBox(
                    "Sprite의 Texture Wrap Mode 가 Repeat 이 아니면 타일링이 끊어질 수 있습니다. Texture Import 설정에서 Wrap Mode = Repeat 로 변경하세요.",
                    MessageType.Info);
            }
        }
    }
