using UnityEngine;
using UnityEditor;

namespace CAT.UI
{
    /// <summary>
    /// 간단한 텍스트 입력 다이얼로그 (에디터용)
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string _title;
        private string _label;
        private string _inputText;
        private bool _initialized;

        private static string _result;
        private static bool _dialogClosed;

        /// <summary>
        /// 텍스트 입력 다이얼로그 표시
        /// </summary>
        /// <param name="title">창 제목</param>
        /// <param name="label">입력 필드 라벨</param>
        /// <param name="defaultText">기본 텍스트</param>
        /// <returns>입력된 텍스트 (취소 시 null)</returns>
        public static string Show(string title, string label, string defaultText = "")
        {
            _result = null;
            _dialogClosed = false;

            var window = CreateInstance<EditorInputDialog>();
            window._title = title;
            window._label = label;
            window._inputText = defaultText;
            window._initialized = false;

            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(300, 100);
            window.maxSize = new Vector2(400, 100);

            window.ShowModalUtility();

            return _result;
        }

        private void OnGUI()
        {
            if (!_initialized)
            {
                _initialized = true;
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_label, GUILayout.Width(80));

            GUI.SetNextControlName("InputField");
            _inputText = EditorGUILayout.TextField(_inputText);
            EditorGUILayout.EndHorizontal();

            // 첫 프레임에 포커스 설정
            if (!_dialogClosed)
            {
                EditorGUI.FocusTextInControl("InputField");
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Enter 키 처리
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                _result = _inputText;
                _dialogClosed = true;
                Close();
                Event.current.Use();
            }

            // ESC 키 처리
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _result = null;
                _dialogClosed = true;
                Close();
                Event.current.Use();
            }

            if (GUILayout.Button("확인", GUILayout.Width(80)))
            {
                _result = _inputText;
                _dialogClosed = true;
                Close();
            }

            if (GUILayout.Button("취소", GUILayout.Width(80)))
            {
                _result = null;
                _dialogClosed = true;
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            if (!_dialogClosed)
            {
                _result = null;
            }
            _dialogClosed = true;
        }
    }
}
