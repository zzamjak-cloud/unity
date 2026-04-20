#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace CAT.Water2D
{
    [CustomEditor(typeof(Water2D))]
    [CanEditMultipleObjects]
    public class Water2DEditor : Editor
    {
        private Water2D _target;

        // SerializedProperty 캐시
        private SerializedProperty _widthProp;
        private SerializedProperty _depthProp;
        private SerializedProperty _pointCountProp;
        private SerializedProperty _springConstantProp;
        private SerializedProperty _dampingProp;
        private SerializedProperty _spreadProp;
        private SerializedProperty _velocityMultiplierProp;
        private SerializedProperty _massMultiplierProp;
        private SerializedProperty _maxImpulseProp;
        private SerializedProperty _buoyancyEnabledProp;
        private SerializedProperty _buoyancyForceProp;
        private SerializedProperty _linearDragProp;
        private SerializedProperty _angularDragProp;
        private SerializedProperty _sortingLayerIDProp;
        private SerializedProperty _sortingOrderProp;
        private SerializedProperty _onSplashProp;

        private static readonly Color BoundsWireColor = new Color(0.4f, 0.7f, 1f, 0.8f);
        private static readonly Color SurfaceLineColor = new Color(0.2f, 0.9f, 1f, 1f);
        private static readonly Color PointDotColor = new Color(1f, 1f, 1f, 0.9f);

        private void OnEnable()
        {
            _target = target as Water2D;

            _widthProp = serializedObject.FindProperty("_width");
            _depthProp = serializedObject.FindProperty("_depth");
            _pointCountProp = serializedObject.FindProperty("_pointCount");
            _springConstantProp = serializedObject.FindProperty("_springConstant");
            _dampingProp = serializedObject.FindProperty("_damping");
            _spreadProp = serializedObject.FindProperty("_spread");
            _velocityMultiplierProp = serializedObject.FindProperty("_velocityMultiplier");
            _massMultiplierProp = serializedObject.FindProperty("_massMultiplier");
            _maxImpulseProp = serializedObject.FindProperty("_maxImpulse");
            _buoyancyEnabledProp = serializedObject.FindProperty("_buoyancyEnabled");
            _buoyancyForceProp = serializedObject.FindProperty("_buoyancyForce");
            _linearDragProp = serializedObject.FindProperty("_linearDrag");
            _angularDragProp = serializedObject.FindProperty("_angularDrag");
            _sortingLayerIDProp = serializedObject.FindProperty("_sortingLayerID");
            _sortingOrderProp = serializedObject.FindProperty("_sortingOrder");
            _onSplashProp = serializedObject.FindProperty("_onSplash");
        }

        public override void OnInspectorGUI()
        {
            if (_target == null) return;
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(4);

            DrawSize();
            DrawMeshSection();
            DrawSpringSection();
            DrawInteractionSection();
            DrawBuoyancySection();
            DrawRenderingSection();
            DrawEventsSection();

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                foreach (Object o in targets)
                {
                    if (o is Water2D w) w.RebuildMeshIfDirty();
                }
            }

            EditorGUILayout.Space(8);
            DrawEditorPreview();
            EditorGUILayout.Space(4);
            DrawTestButtons();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🌊 Water2D", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"포인트: {_target.PointCount}개, 폭 {_target.Width:F2} × 깊이 {_target.Depth:F2}",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawSize()
        {
            EditorGUILayout.LabelField("크기", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_widthProp);
            EditorGUILayout.PropertyField(_depthProp);
        }

        private void DrawMeshSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("메시", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_pointCountProp);
            if (_pointCountProp.intValue > 64)
            {
                EditorGUILayout.HelpBox(
                    "포인트가 64개를 초과하면 모바일 성능에 영향이 있을 수 있습니다.",
                    MessageType.Warning);
            }
        }

        private void DrawSpringSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("스프링 물리", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_springConstantProp);
            EditorGUILayout.PropertyField(_dampingProp);
            EditorGUILayout.PropertyField(_spreadProp);
        }

        private void DrawInteractionSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("상호작용", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_velocityMultiplierProp);
            EditorGUILayout.PropertyField(_massMultiplierProp);
            EditorGUILayout.PropertyField(_maxImpulseProp);
        }

        private void DrawBuoyancySection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("부력 (Buoyancy)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_buoyancyEnabledProp);
            using (new EditorGUI.DisabledScope(!_buoyancyEnabledProp.boolValue))
            {
                EditorGUILayout.PropertyField(_buoyancyForceProp);
                EditorGUILayout.PropertyField(_linearDragProp);
                EditorGUILayout.PropertyField(_angularDragProp);
            }
            if (_buoyancyEnabledProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "물에 잠긴 Rigidbody2D 는 매 FixedUpdate 에서 자동으로 부력·드래그를 받습니다.\n" +
                    "가라앉게 하려면 바디의 mass 증가 또는 Buoyancy Force 감소.",
                    MessageType.Info);
            }
        }

        private void DrawRenderingSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("렌더링", EditorStyles.boldLabel);

            // Sorting Layer 드롭다운 (SpriteRenderer 와 동일한 UX)
            SortingLayer[] layers = SortingLayer.layers;
            string[] names = new string[layers.Length];
            int currentIndex = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                names[i] = layers[i].name;
                if (layers[i].id == _sortingLayerIDProp.intValue) currentIndex = i;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(
                new GUIContent("Sorting Layer", "SpriteRenderer 와 공유되는 Sorting Layer"),
                currentIndex, names);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < layers.Length)
            {
                _sortingLayerIDProp.intValue = layers[newIndex].id;
            }

            EditorGUILayout.PropertyField(_sortingOrderProp,
                new GUIContent("Order in Layer", "같은 Sorting Layer 내에서 앞뒤 정렬. 큰 값일수록 앞쪽."));
        }

        private void DrawEventsSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("이벤트", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_onSplashProp);
        }

        private void DrawEditorPreview()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("에디터 프리뷰", EditorStyles.boldLabel);
                bool prev = _target._editorPreview;
                bool next = EditorGUILayout.Toggle(
                    new GUIContent("Play 없이 시뮬레이션", "체크 시 Play 모드 없이도 출렁임 프리뷰 실행"),
                    prev);
                if (next != prev)
                {
                    foreach (Object o in targets)
                    {
                        if (o is Water2D w) w._editorPreview = next;
                    }
                }
                if (next)
                {
                    EditorGUILayout.HelpBox("프리뷰 실행 중. 아래 🌊 Random Splash 버튼으로 파동 테스트.", MessageType.Info);
                }
            }
        }

        private void DrawTestButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🌊 Random Splash", GUILayout.Height(28)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is Water2D w)
                        {
                            float lx = Random.Range(-w.Width * 0.5f, w.Width * 0.5f);
                            float f = Random.Range(-3f, -1f);
                            w.Splash(lx, f);
                        }
                    }
                }
                if (GUILayout.Button("⏹ Reset Surface", GUILayout.Height(28)))
                {
                    foreach (Object o in targets)
                    {
                        if (o is Water2D w) w.ResetSurface();
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            if (_target == null) return;

            Transform t = _target.transform;
            float w = _target.Width;
            float d = _target.Depth;

            // bounds wire
            Vector3 c = t.TransformPoint(new Vector3(0f, -d * 0.5f, 0f));
            Handles.color = BoundsWireColor;
            Handles.matrix = Matrix4x4.TRS(c, t.rotation, t.lossyScale);
            Handles.DrawWireCube(Vector3.zero, new Vector3(w, d, 0f));
            Handles.matrix = Matrix4x4.identity;

            // 표면 라인 (런타임 점 기반)
            WaterPoint[] points = _target.EditorGetPoints();
            if (points == null || points.Length < 2) return;

            Handles.color = SurfaceLineColor;
            float halfW = w * 0.5f;
            float dx = w / (points.Length - 1);
            Vector3 prev = t.TransformPoint(new Vector3(-halfW, points[0].Height, 0f));
            for (int i = 1; i < points.Length; i++)
            {
                float x = -halfW + i * dx;
                Vector3 cur = t.TransformPoint(new Vector3(x, points[i].Height, 0f));
                Handles.DrawLine(prev, cur);
                prev = cur;
            }

            // 포인트 도트
            Handles.color = PointDotColor;
            for (int i = 0; i < points.Length; i++)
            {
                float x = -halfW + i * dx;
                Vector3 p = t.TransformPoint(new Vector3(x, points[i].Height, 0f));
                float size = HandleUtility.GetHandleSize(p) * 0.04f;
                Handles.DotHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);
            }
        }
    }
}
#endif
