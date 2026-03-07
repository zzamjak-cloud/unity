using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    [CustomEditor(typeof(Windable))]
    public class WindableEditor : Editor
    {
        private Windable _target;
        private bool _isPlaying = false;
        private double _startTime;
        private const float DURATION = 60.0f; // 테스트 재생 시간

        // GUI 스타일
        private GUIStyle _headerStyle;
        private GUIStyle _warningStyle;

        private void OnEnable()
        {
            _target = (Windable)target;
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
            
            // 컴포넌트 유효성 검사
            ValidateTarget();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            
            serializedObject.Update();

            // 헤더
            DrawComponentHeader();

            // 타입별 경고 메시지
            DrawTypeValidation();

            // 타입 선택 (읽기 전용, 자동 감지됨)
            DrawTypeInfo();

            EditorGUILayout.Space();

            // 기본 프로퍼티들 그리기
            DrawWindProperties();

            // 변경사항이 있으면 머티리얼 업데이트
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorApplication.delayCall += () => {
                    if (_target != null)
                    {
                        _target.UpdateMaterialProperties();
                    }
                };
            }

            EditorGUILayout.Space();

            // 테스트 버튼
            DrawTestButtons();
        }

        /// <summary>
        /// GUI 스타일 초기화
        /// </summary>
        private void InitializeStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (_warningStyle == null)
            {
                _warningStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    normal = { textColor = Color.yellow }
                };
            }
        }

        /// <summary>
        /// 컴포넌트 헤더 그리기
        /// </summary>
        private void DrawComponentHeader()
        {
            EditorGUILayout.LabelField("🍃 Windable Component", _headerStyle);
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// 타입 정보 표시
        /// </summary>
        private void DrawTypeInfo()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.EnumPopup("감지된 타입", _target.WindableTypeValue);
            EditorGUI.EndDisabledGroup();

            // 컴포넌트 정보 표시
            string componentInfo = GetComponentInfo();
            if (!string.IsNullOrEmpty(componentInfo))
            {
                EditorGUILayout.HelpBox(componentInfo, MessageType.Info);
            }
        }

        /// <summary>
        /// 타입별 유효성 검사 및 경고
        /// </summary>
        private void DrawTypeValidation()
        {
            var spriteRenderer = _target.GetComponent<SpriteRenderer>();
            var graphic = _target.GetComponent<Graphic>();

            if (spriteRenderer == null && graphic == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ SpriteRenderer 또는 UI Graphic 컴포넌트가 필요합니다!", 
                    MessageType.Error
                );
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("SpriteRenderer 추가"))
                {
                    Undo.AddComponent<SpriteRenderer>(_target.gameObject);
                }
                if (GUILayout.Button("Image 추가"))
                {
                    Undo.AddComponent<Image>(_target.gameObject);
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (spriteRenderer != null && graphic != null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ SpriteRenderer와 UI Graphic이 모두 있습니다. SpriteRenderer를 우선 사용합니다.", 
                    MessageType.Warning
                );
            }

            // Sprite 타입 특별 검사
            if (_target.WindableTypeValue == WindableType.Sprite && spriteRenderer?.sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ SpriteRenderer에 스프라이트가 할당되지 않았습니다.", 
                    MessageType.Warning
                );
            }

            // UI 타입 특별 검사
            if (_target.WindableTypeValue == WindableType.UI && graphic is Image img && img.sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Image에 스프라이트가 할당되지 않았습니다.", 
                    MessageType.Warning
                );
            }
        }

        /// <summary>
        /// 바람 효과 프로퍼티 그리기
        /// </summary>
        private void DrawWindProperties()
        {
            EditorGUILayout.LabelField("바람 효과 설정", EditorStyles.boldLabel);

            // _MainTex와 _ClipRect는 숨김 처리되어 있으므로 제외
            DrawPropertyField("_NoiseTex", "노이즈 텍스처");
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("기본 설정", EditorStyles.miniBoldLabel);
            DrawPropertyField("_RotateUV", "UV 회전");
            DrawPropertyField("_WindSpeed", "바람 속도");
            DrawPropertyField("_WindStrength", "바람 강도");
            DrawPropertyField("_WindFrequency", "바람 주파수");
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("고급 설정", EditorStyles.miniBoldLabel);
            DrawPropertyField("_WindDirection", "바람 방향");
            DrawPropertyField("_WindScale", "바람 스케일");
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("이미지 오프셋", EditorStyles.miniBoldLabel);
            DrawPropertyField("_ImageOffsetX", "X 오프셋");
            DrawPropertyField("_ImageOffsetY", "Y 오프셋");
            DrawPropertyField("_ImageScale", "이미지 스케일");
        }

        /// <summary>
        /// 프로퍼티 필드 그리기 도우미 메서드
        /// </summary>
        private void DrawPropertyField(string propertyName, string displayName = null)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(displayName ?? property.displayName));
            }
        }

        /// <summary>
        /// 테스트 버튼들 그리기
        /// </summary>
        private void DrawTestButtons()
        {
            EditorGUILayout.LabelField("테스트", EditorStyles.boldLabel);

            // 재생 상태에 따른 버튼 색상 변경
            if (_isPlaying)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button($"⏹️ 테스트 중지 (남은 시간: {DURATION - (EditorApplication.timeSinceStartup - _startTime):F1}s)", GUILayout.Height(30)))
                {
                    StopTest();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button($"▶️ 바람 효과 테스트 ({DURATION}초)", GUILayout.Height(30)))
                {
                    StartTest();
                }
                GUI.backgroundColor = Color.white;
            }

            // 즉시 업데이트 버튼
            if (GUILayout.Button("🔄 즉시 업데이트"))
            {
                _target.UpdateMaterialProperties();
            }

            // 리셋 버튼
            if (GUILayout.Button("🔄 효과 리셋"))
            {
                _target.UpdateMaterialProperties(0);
            }
        }

        /// <summary>
        /// 컴포넌트 정보 문자열 생성
        /// </summary>
        private string GetComponentInfo()
        {
            var spriteRenderer = _target.GetComponent<SpriteRenderer>();
            var graphic = _target.GetComponent<Graphic>();

            if (_target.WindableTypeValue == WindableType.Sprite && spriteRenderer != null)
            {
                return $"SpriteRenderer 사용 중 | 스프라이트: {(spriteRenderer.sprite ? spriteRenderer.sprite.name : "없음")}";
            }
            else if (_target.WindableTypeValue == WindableType.UI && graphic != null)
            {
                string graphicType = graphic.GetType().Name;
                if (graphic is Image img)
                {
                    return $"UI {graphicType} 사용 중 | 스프라이트: {(img.sprite ? img.sprite.name : "없음")}";
                }
                return $"UI {graphicType} 사용 중";
            }

            return "컴포넌트 정보를 읽을 수 없음";
        }

        /// <summary>
        /// 타겟 유효성 검사
        /// </summary>
        private void ValidateTarget()
        {
            if (_target == null) return;

            // 필요한 컴포넌트가 없는 경우 경고 로그
            var spriteRenderer = _target.GetComponent<SpriteRenderer>();
            var graphic = _target.GetComponent<Graphic>();

            if (spriteRenderer == null && graphic == null)
            {
                Debug.LogWarning($"[Windable] {_target.name}: SpriteRenderer 또는 UI Graphic 컴포넌트가 필요합니다.");
            }
        }

        /// <summary>
        /// 테스트 시작
        /// </summary>
        private void StartTest()
        {
            if (_isPlaying) return;

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
            _isPlaying = true;

            Debug.Log($"[Windable] {_target.name}: 바람 효과 테스트 시작 ({DURATION}초)");
        }

        /// <summary>
        /// 테스트 중지
        /// </summary>
        private void StopTest()
        {
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
            if (_target != null)
                _target.UpdateMaterialProperties(0);
            Repaint();
            SceneView.RepaintAll();

            Debug.Log($"[Windable] {_target.name}: 바람 효과 테스트 중지");
        }

        /// <summary>
        /// 에디터 업데이트 루프: EditorAdvance()를 직접 호출하여 애니메이션 진행.
        /// [ExecuteAlways] Update()에 의존하지 않는다.
        /// </summary>
        private void EditorUpdate()
        {
            if (_target == null)
            {
                StopTest();
                return;
            }

            double elapsedTime = EditorApplication.timeSinceStartup - _startTime;

            if (elapsedTime >= DURATION)
            {
                StopTest();
                return;
            }

            // 경과 시간을 EditorAdvance로 전달하여 애니메이션 효과
            _target.EditorAdvance((float)elapsedTime);

            // 인스펙터 + 씬뷰 업데이트
            Repaint();
            SceneView.RepaintAll();
        }
    }
}