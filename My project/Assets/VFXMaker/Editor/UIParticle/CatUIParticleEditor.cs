using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UI;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.VFX.Editor
{
    /// <summary>
    /// CatUIParticle 커스텀 인스펙터
    /// </summary>
    [CustomEditor(typeof(CatUIParticle))]
    [CanEditMultipleObjects]
    internal class CatUIParticleEditor : GraphicEditor
    {
        internal class State : ScriptableSingleton<State>
        {
            public bool is3DScaleMode;
        }

        private static readonly GUIContent[] s_ContentMaterials = new[]
        {
            new GUIContent("Material"),
            new GUIContent("Trail Material")
        };

        private static readonly GUIContent s_ContentRenderingOrder = new GUIContent("Rendering Order");
        private static readonly GUIContent s_ContentRefresh = new GUIContent("Refresh");
        private static readonly GUIContent s_Content3D = new GUIContent("3D");
        private static readonly GUIContent s_ContentScale = new GUIContent("Scale");
        private static readonly Regex s_RegexBuiltInGuid = new Regex(@"^0{16}.0{15}$", RegexOptions.Compiled);
        private static readonly List<Material> s_TempMaterials = new List<Material>();

        private SerializedProperty _maskable;
        private SerializedProperty _scale3D;
        private SerializedProperty _animatableProperties;
        private SerializedProperty _positionMode;
        private SerializedProperty _autoScalingMode;
        private SerializedProperty _useCustomView;
        private SerializedProperty _customViewSize;
        private SerializedProperty _timeScaleMultiplier;
        private ReorderableList _ro;
        private bool _is3DScaleMode;

        private static readonly HashSet<Shader> s_Shaders = new HashSet<Shader>();
        private static readonly List<string> s_MaskablePropertyNames = new List<string>
        {
            "_Stencil",
            "_StencilComp",
            "_StencilOp",
            "_StencilWriteMask",
            "_StencilReadMask",
            "_ColorMask"
        };

        protected override void OnEnable()
        {
            base.OnEnable();

            _maskable = serializedObject.FindProperty("m_Maskable");
            _scale3D = serializedObject.FindProperty("m_Scale3D");
            _animatableProperties = serializedObject.FindProperty("m_AnimatableProperties");
            _positionMode = serializedObject.FindProperty("m_PositionMode");
            _autoScalingMode = serializedObject.FindProperty("m_AutoScalingMode");
            _useCustomView = serializedObject.FindProperty("m_UseCustomView");
            _customViewSize = serializedObject.FindProperty("m_CustomViewSize");
            _timeScaleMultiplier = serializedObject.FindProperty("m_TimeScaleMultiplier");

            var sp = serializedObject.FindProperty("m_Particles");
            _ro = new ReorderableList(sp.serializedObject, sp, true, true, true, true)
            {
                elementHeightCallback = index =>
                {
                    var ps = sp.GetArrayElementAtIndex(index).objectReferenceValue as ParticleSystem;
                    var materialCount = 0;
                    if (ps && ps.TryGetComponent<ParticleSystemRenderer>(out var psr))
                    {
                        materialCount = psr.sharedMaterials.Length;
                    }

                    return (materialCount + 1) * (EditorGUIUtility.singleLineHeight + 2);
                },
                drawElementCallback = (rect, index, _, __) =>
                {
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    var p = sp.GetArrayElementAtIndex(index);
                    EditorGUI.ObjectField(rect, p, GUIContent.none);
                    var ps = p.objectReferenceValue as ParticleSystem;
                    if (!ps || !ps.TryGetComponent<ParticleSystemRenderer>(out var psr)) return;

                    rect.x += 15;
                    rect.width -= 15;
                    var materials = new SerializedObject(psr).FindProperty("m_Materials");
                    var count = Mathf.Min(materials.arraySize, 2);
                    for (var i = 0; i < count; i++)
                    {
                        rect.y += rect.height + 2;
                        EditorGUI.PropertyField(rect, materials.GetArrayElementAtIndex(i), s_ContentMaterials[i]);
                    }

                    if (materials.serializedObject.hasModifiedProperties)
                    {
                        materials.serializedObject.ApplyModifiedProperties();
                    }
                },
                drawHeaderCallback = rect =>
                {
                    var pos = new Rect(rect.x, rect.y, 150, rect.height);
                    EditorGUI.LabelField(pos, s_ContentRenderingOrder);

                    pos = new Rect(rect.width - 35, rect.y, 60, rect.height);
                    if (GUI.Button(pos, s_ContentRefresh, EditorStyles.miniButton))
                    {
                        foreach (var uip in targets.OfType<CatUIParticle>())
                        {
                            uip.RefreshParticles();
                            EditorUtility.SetDirty(uip);
                        }
                    }
                }
            };

            // 선택 시 파티클 갱신
            if (!Application.isPlaying)
            {
                foreach (var uip in targets.OfType<CatUIParticle>())
                {
                    if (PrefabUtility.GetPrefabAssetType(uip) != PrefabAssetType.NotAPrefab) continue;
                    uip.RefreshParticles(uip.particles);
                }
            }

            // 3D 스케일 모드 초기화
            _is3DScaleMode = State.instance.is3DScaleMode;
            if (!_is3DScaleMode)
            {
                var x = _scale3D.FindPropertyRelative("x");
                var y = _scale3D.FindPropertyRelative("y");
                var z = _scale3D.FindPropertyRelative("z");
                _is3DScaleMode = !Mathf.Approximately(x.floatValue, y.floatValue) ||
                                 !Mathf.Approximately(y.floatValue, z.floatValue) ||
                                 y.hasMultipleDifferentValues ||
                                 z.hasMultipleDifferentValues;
            }
        }

        public override void OnInspectorGUI()
        {
            var current = target as CatUIParticle;
            if (!current) return;

            serializedObject.Update();

            // 리플레이 버튼
            DrawReplayButton(current);

            EditorGUILayout.Space(2);

            // Maskable
            EditorGUILayout.PropertyField(_maskable);

            // Scale
            if (DrawFloatOrVector3Field(_scale3D, _is3DScaleMode) != _is3DScaleMode)
            {
                State.instance.is3DScaleMode = _is3DScaleMode = !_is3DScaleMode;
            }

            // AnimatableProperties
            current.GetMaterials(s_TempMaterials);
            AnimatablePropertyEditor.Draw(_animatableProperties, s_TempMaterials);

            // Position Mode
            EditorGUILayout.PropertyField(_positionMode);

            // Auto Scaling
            EditorGUILayout.PropertyField(_autoScalingMode);

            // Custom View Size
            EditorGUILayout.PropertyField(_useCustomView);
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(!_useCustomView.boolValue);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_customViewSize);
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                _customViewSize.floatValue = Mathf.Max(0.1f, _customViewSize.floatValue);
            }

            // Time Scale Multiplier
            EditorGUILayout.PropertyField(_timeScaleMultiplier);

            // 파티클 시스템 리스트
            EditorGUI.BeginChangeCheck();
            _ro.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                EditorApplication.QueuePlayerLoopUpdate();
                foreach (var uip in targets.OfType<CatUIParticle>())
                {
                    uip.RefreshParticles(uip.particles);
                }
            }

            // Built-in 셰이더 경고 (UI 셰이더가 아닌 경우)
            foreach (var mat in s_TempMaterials)
            {
                if (!mat || !mat.shader) continue;
                var shader = mat.shader;
                if (IsBuiltInObject(shader) && !shader.name.StartsWith("UI/"))
                {
                    EditorGUILayout.HelpBox(
                        $"Built-in 셰이더 '{shader.name}' ({mat.name})은 지원되지 않습니다.\n" +
                        "UI 셰이더를 사용하세요.",
                        MessageType.Error);
                }
            }

            // 마스크 프로퍼티 경고
            if (current.maskable && current.GetComponentInParent<Mask>(false))
            {
                foreach (var mat in s_TempMaterials)
                {
                    if (!mat || !mat.shader) continue;
                    var shader = mat.shader;
                    if (!s_Shaders.Add(shader)) continue;

                    foreach (var propName in s_MaskablePropertyNames)
                    {
                        if (mat.HasProperty(propName)) continue;

                        EditorGUILayout.HelpBox(
                            $"셰이더 '{shader.name}'에 '{propName}' 프로퍼티가 없습니다.\n" +
                            "마스킹이 적용되지 않을 수 있습니다.",
                            MessageType.Warning);
                        break;
                    }
                }
            }

            s_TempMaterials.Clear();
            s_Shaders.Clear();
        }

        // --- 에디터 리플레이 시스템 ---
        private static CatUIParticle s_ReplayTarget;
        private static double s_LastReplayTime;

        private void DrawReplayButton(CatUIParticle uiParticle)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("▶ Replay", GUILayout.Width(80), GUILayout.Height(24)))
                {
                    StartReplay(uiParticle);
                }
            }
        }

        private void StartReplay(CatUIParticle uiParticle)
        {
            // 기존 리플레이 즉시 정지 (콜백 해제만, 파티클 정지는 아래에서 처리)
            EditorApplication.update -= EditorReplayUpdate;

            s_ReplayTarget = uiParticle;
            s_LastReplayTime = EditorApplication.timeSinceStartup;

            // 자식 포함 파티클 재수집
            uiParticle.RefreshParticles();

            // 즉시 정지 → 초기화 → 재시작
            foreach (var ps in uiParticle.particles)
            {
                if (!ps) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.Simulate(0, false, true);
                ps.Play(true);
            }

            uiParticle.Resume();

            EditorApplication.update += EditorReplayUpdate;
        }

        /// <summary>
        /// 에디터 EditMode에서 매 프레임 파티클 시뮬레이션을 구동
        /// </summary>
        private static void EditorReplayUpdate()
        {
            if (!s_ReplayTarget || !s_ReplayTarget.isActiveAndEnabled)
            {
                EditorApplication.update -= EditorReplayUpdate;
                s_ReplayTarget = null;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - s_LastReplayTime);
            s_LastReplayTime = now;

            bool anyAlive = false;
            foreach (var ps in s_ReplayTarget.particles)
            {
                if (!ps) continue;
                if (ps.IsAlive(true))
                {
                    anyAlive = true;
                    ps.Simulate(dt, false, false, false);
                }
            }

            if (!anyAlive)
            {
                EditorApplication.update -= EditorReplayUpdate;
                s_ReplayTarget = null;
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            if (s_ReplayTarget && target is CatUIParticle uip && s_ReplayTarget == uip)
            {
                EditorApplication.update -= EditorReplayUpdate;
                s_ReplayTarget = null;
            }
        }

        private static bool IsBuiltInObject(Object obj)
        {
            return AssetDatabase.IsMainAsset(obj)
                   && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _)
                   && s_RegexBuiltInGuid.IsMatch(guid);
        }

        private static bool DrawFloatOrVector3Field(SerializedProperty sp, bool showXyz)
        {
            var x = sp.FindPropertyRelative("x");
            var y = sp.FindPropertyRelative("y");
            var z = sp.FindPropertyRelative("z");

            showXyz |= !Mathf.Approximately(x.floatValue, y.floatValue) ||
                       !Mathf.Approximately(y.floatValue, z.floatValue) ||
                       y.hasMultipleDifferentValues ||
                       z.hasMultipleDifferentValues;

            EditorGUILayout.BeginHorizontal();
            if (showXyz)
            {
                EditorGUILayout.PropertyField(sp);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(x, s_ContentScale);
                if (EditorGUI.EndChangeCheck())
                {
                    y.floatValue = z.floatValue = x.floatValue;
                }
            }

            EditorGUI.BeginChangeCheck();
            showXyz = GUILayout.Toggle(showXyz, s_Content3D, EditorStyles.miniButton, GUILayout.Width(30));
            if (EditorGUI.EndChangeCheck() && !showXyz)
            {
                z.floatValue = y.floatValue = x.floatValue;
            }

            EditorGUILayout.EndHorizontal();

            return showXyz;
        }
    }
}
