using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace CAT.Utility
{
    /// <summary>
    /// 프리팹의 구조/컴포넌트/프로퍼티를 JSON으로 추출하는 에디터 윈도우.
    /// </summary>
    public class UIMakerWithJSON : EditorWindow
    {
        const string EditorPrefsKey_FolderGuid = "CAT_UIMaker_FolderGUID";

        GameObject _prefab;
        string _saveFolderPath;
        Vector2 _scrollPos;

        [MenuItem("CAT/Utility/Extract Prefab Info")]
        static void ShowWindow()
        {
            var window = GetWindow<UIMakerWithJSON>("Prefab JSON 추출기");
            window.minSize = new Vector2(400, 200);
        }

        void OnEnable()
        {
            // 저장 폴더 기본값: EditorPrefs GUID 캐싱 → 폴더 이동 대응
            _saveFolderPath = LoadSaveFolderPath();
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("프리팹 JSON 추출기", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // 프리팹 ObjectField
            _prefab = (GameObject)EditorGUILayout.ObjectField(
                "프리팹", _prefab, typeof(GameObject), false);

            EditorGUILayout.Space(4);

            // 저장 폴더
            EditorGUILayout.BeginHorizontal();
            _saveFolderPath = EditorGUILayout.TextField("저장 폴더", _saveFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string initialPath = _saveFolderPath;
                if (!Directory.Exists(initialPath))
                    initialPath = Application.dataPath;

                string selected = EditorUtility.OpenFolderPanel("JSON 저장 폴더 선택", initialPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    _saveFolderPath = selected;
                    SaveFolderPathToPrefs(selected);
                }
            }
            EditorGUILayout.EndHorizontal();

            // 프리팹이 선택되었을 때 정보 표시
            if (_prefab != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"저장 파일: {_prefab.name}.json",
                    MessageType.Info);
            }

            EditorGUILayout.Space(8);

            // 추출 버튼
            EditorGUI.BeginDisabledGroup(_prefab == null);
            if (GUILayout.Button("JSON 추출", GUILayout.Height(30)))
            {
                ExtractAndSave();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);

            // 드래그 앤 드롭 영역
            DrawDropArea();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 프리팹을 JSON으로 추출하여 저장한다.
        /// </summary>
        void ExtractAndSave()
        {
            if (_prefab == null)
            {
                EditorUtility.DisplayDialog("오류", "프리팹을 선택해주세요.", "확인");
                return;
            }

            // 프리팹 에셋인지 확인
            string prefabPath = AssetDatabase.GetAssetPath(_prefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                EditorUtility.DisplayDialog("오류",
                    "씬 오브젝트가 아닌 프로젝트의 프리팹 에셋을 선택해주세요.", "확인");
                return;
            }

            // 저장 폴더 확인 및 생성
            if (string.IsNullOrEmpty(_saveFolderPath))
            {
                EditorUtility.DisplayDialog("오류", "저장 폴더를 지정해주세요.", "확인");
                return;
            }

            if (!Directory.Exists(_saveFolderPath))
            {
                Directory.CreateDirectory(_saveFolderPath);
            }

            // 프리팹 인스턴스 로드 (프로퍼티 접근을 위해)
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                // 추출
                var root = new PrefabJsonRoot
                {
                    prefabName = _prefab.name,
                    exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    root = SerializedPropertyExtractor.ExtractGameObject(prefabRoot)
                };

                // JSON 생성
                string json = root.ToJson();

                // 저장
                string filePath = Path.Combine(_saveFolderPath, $"{_prefab.name}.json");
                File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

                // Assets 폴더 내부이면 AssetDatabase 갱신
                // Path.Combine은 백슬래시, Application.dataPath는 슬래시를 사용하므로
                // 정규화 후 비교해야 한다.
                string normalizedFile = Path.GetFullPath(filePath);
                string normalizedData = Path.GetFullPath(Application.dataPath);
                if (normalizedFile.StartsWith(normalizedData, StringComparison.OrdinalIgnoreCase))
                {
                    string relativePath = "Assets" + normalizedFile
                        .Substring(normalizedData.Length).Replace('\\', '/');
                    AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                }

                Debug.Log($"[UIMaker] JSON 추출 완료: {filePath}");
                EditorUtility.DisplayDialog("완료",
                    $"JSON 추출이 완료되었습니다.\n{filePath}", "확인");
            }
            finally
            {
                // 임시 인스턴스 정리
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// 드래그 앤 드롭 영역을 그린다.
        /// </summary>
        void DrawDropArea()
        {
            var dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "프리팹을 여기에 드래그 앤 드롭", EditorStyles.helpBox);

            var evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go)
                        {
                            string path = AssetDatabase.GetAssetPath(go);
                            if (!string.IsNullOrEmpty(path) &&
                                (path.EndsWith(".prefab") || PrefabUtility.IsPartOfPrefabAsset(go)))
                            {
                                _prefab = go;
                                Repaint();
                                break;
                            }
                        }
                    }
                }

                evt.Use();
            }
        }

        /// <summary>
        /// 저장 폴더 경로를 EditorPrefs에서 로드한다.
        /// GUID 기반으로 폴더 이동에 대응한다.
        /// </summary>
        string LoadSaveFolderPath()
        {
            // 1. EditorPrefs에서 GUID로 복원 시도
            string savedGuid = EditorPrefs.GetString(EditorPrefsKey_FolderGuid, "");
            if (!string.IsNullOrEmpty(savedGuid))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(savedGuid);
                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath))
                {
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                }
            }

            // 2. 기본값: 스크립트 위치 기준 ../JSON/
            return GetDefaultSavePath();
        }

        /// <summary>
        /// MonoScript 기반으로 스크립트 위치를 찾아 기본 저장 경로를 계산한다.
        /// </summary>
        string GetDefaultSavePath()
        {
            var script = MonoScript.FromScriptableObject(this);
            if (script != null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(script);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    // Editor/ 폴더의 부모 → JSON/ 폴더
                    string editorDir = Path.GetDirectoryName(scriptPath);
                    string parentDir = Path.GetDirectoryName(editorDir);
                    string jsonDir = Path.Combine(parentDir, "JSON");
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", jsonDir));
                }
            }

            // 폴백: 프로젝트 루트의 JSON 폴더
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "Plugins", "CAT", "UIMaker", "JSON"));
        }

        /// <summary>
        /// 저장 폴더 경로를 EditorPrefs에 GUID로 저장한다.
        /// </summary>
        void SaveFolderPathToPrefs(string absolutePath)
        {
            string relativePath = ToAssetsRelativePath(absolutePath);
            if (relativePath != null && AssetDatabase.IsValidFolder(relativePath))
            {
                string guid = AssetDatabase.AssetPathToGUID(relativePath);
                if (!string.IsNullOrEmpty(guid))
                {
                    EditorPrefs.SetString(EditorPrefsKey_FolderGuid, guid);
                    return;
                }
            }

            // Assets 외부 경로인 경우 GUID 저장 불가 — 빈 문자열로 초기화
            EditorPrefs.SetString(EditorPrefsKey_FolderGuid, "");
        }

        /// <summary>
        /// 절대 경로를 Assets/ 상대 경로로 변환한다.
        /// Assets 외부이면 null 반환.
        /// </summary>
        static string ToAssetsRelativePath(string absolutePath)
        {
            string dataPath = Path.GetFullPath(Application.dataPath);
            string fullPath = Path.GetFullPath(absolutePath);

            if (fullPath.StartsWith(dataPath))
            {
                string relative = "Assets" + fullPath.Substring(dataPath.Length);
                return relative.Replace('\\', '/');
            }
            return null;
        }
    }
}
