using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace CAT.Utility
{
    /// <summary>
    /// UIMakerPrefabRegistry의 커스텀 Inspector.
    /// UICom 스캔, 스냅샷 생성, 프리팹 교체 UI를 제공한다.
    /// </summary>
    [CustomEditor(typeof(UIMakerPrefabRegistry))]
    public class UIMakerPrefabRegistryEditor : Editor
    {
        UIMakerPrefabRegistry _registry;
        Vector2 _scrollPos;

        // 접힘 상태 (엔트리별)
        bool[] _foldouts;

        // 스냅샷 존재 여부 캐시 (매 리페인트마다 File.Exists 호출 방지)
        bool[] _snapshotCache;
        DateTime[] _snapshotDates;
        bool _snapshotCacheDirty = true;

        /// <summary>
        /// 메뉴에서 레지스트리 에셋을 선택한다.
        /// </summary>
        [MenuItem("LCUP/UI/Prefab Registry")]
        static void SelectRegistry()
        {
            var registry = UIMakerPrefabRegistry.GetOrCreateInstance();
            if (registry != null)
            {
                Selection.activeObject = registry;
                EditorGUIUtility.PingObject(registry);
            }
        }

        void OnEnable()
        {
            _registry = (UIMakerPrefabRegistry)target;
            SyncFoldouts();
        }

        void SyncFoldouts()
        {
            int count = _registry.Entries.Count;
            if (_foldouts == null || _foldouts.Length != count)
                _foldouts = new bool[count];
        }

        void RebuildSnapshotCache()
        {
            int count = _registry.Entries.Count;
            _snapshotCache = new bool[count];
            _snapshotDates = new DateTime[count];
            for (int i = 0; i < count; i++)
            {
                string path = UIMakerPrefabRegistry.GetSnapshotPath(_registry.Entries[i].logicalName);
                _snapshotCache[i] = File.Exists(path);
                if (_snapshotCache[i])
                    _snapshotDates[i] = File.GetLastWriteTime(path);
            }
            _snapshotCacheDirty = false;
        }

        public override void OnInspectorGUI()
        {
            SyncFoldouts();

            // 스냅샷 캐시 갱신 (변경 시에만)
            if (_snapshotCacheDirty || _snapshotCache == null || _snapshotCache.Length != _registry.Entries.Count)
                RebuildSnapshotCache();

            // ── 헤더 ──────────────────────────────────────────────────
            EditorGUILayout.LabelField("UIMaker Prefab Registry", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 액션 버튼 ──────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("UICom 스캔", GUILayout.Height(28)))
            {
                Undo.RecordObject(_registry, "UICom 스캔");
                int added = _registry.ScanUIComFolder();
                SyncFoldouts();
                _snapshotCacheDirty = true;
                EditorUtility.SetDirty(_registry);
                AssetDatabase.SaveAssets();
                Debug.Log($"[UIMakerPrefabRegistry] 스캔 완료 — {added}개 추가, 총 {_registry.Entries.Count}개");
            }

            if (GUILayout.Button("스냅샷 생성", GUILayout.Height(28)))
            {
                int count = _registry.GenerateSnapshots();
                _snapshotCacheDirty = true;
                Debug.Log($"[UIMakerPrefabRegistry] 스냅샷 {count}개 생성 완료");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);

            // ── 엔트리 목록 ───────────────────────────────────────────
            var entries = _registry.Entries;
            int overrideCount = 0;
            int snapshotCount = 0;

            EditorGUILayout.LabelField($"등록된 프리팹 ({entries.Count}개)", EditorStyles.boldLabel);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(2);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool hasOverride = entry.overridePrefab != null;
                bool hasSnapshot = i < _snapshotCache.Length && _snapshotCache[i];
                bool isMissing = entry.defaultPrefab == null;

                if (hasOverride) overrideCount++;
                if (hasSnapshot) snapshotCount++;

                // 접힘 헤더
                EditorGUILayout.BeginHorizontal();

                // 상태 아이콘
                string statusIcon = isMissing ? "!" : hasOverride ? "*" : " ";
                string label = $"[{statusIcon}] {entry.logicalName}";

                _foldouts[i] = EditorGUILayout.Foldout(_foldouts[i], label, true);
                EditorGUILayout.EndHorizontal();

                if (!_foldouts[i]) continue;

                EditorGUI.indentLevel++;

                // GUID
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("GUID", entry.originalGuid ?? "(없음)");
                EditorGUI.EndDisabledGroup();

                // 기본 프리팹
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("기본", entry.defaultPrefab, typeof(GameObject), false);
                EditorGUI.EndDisabledGroup();

                if (isMissing)
                {
                    EditorGUILayout.HelpBox("기본 프리팹이 누락되었습니다. UICom 스캔을 실행하세요.", MessageType.Warning);
                }

                // 교체 프리팹
                EditorGUI.BeginChangeCheck();
                var newOverride = (GameObject)EditorGUILayout.ObjectField(
                    "교체", entry.overridePrefab, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_registry, "프리팹 교체");
                    entry.overridePrefab = newOverride;
                    _registry.RebuildCache();
                    EditorUtility.SetDirty(_registry);
                }

                // 스냅샷 상태 (캐시에서 읽기)
                if (hasSnapshot)
                {
                    EditorGUILayout.LabelField("스냅샷", $"있음 ({_snapshotDates[i]:yyyy-MM-dd})");
                }
                else
                {
                    EditorGUILayout.LabelField("스냅샷", "없음");
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            // ── 하단 상태 바 ──────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"등록: {entries.Count}개 | 교체: {overrideCount}개 | 스냅샷: {snapshotCount}개",
                EditorStyles.miniLabel);

            if (overrideCount > 0)
            {
                if (GUILayout.Button("모든 교체 초기화", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog("교체 초기화",
                        "모든 프리팹의 교체를 원본으로 되돌리시겠습니까?", "초기화", "취소"))
                    {
                        Undo.RecordObject(_registry, "모든 교체 초기화");
                        for (int i = 0; i < entries.Count; i++)
                            entries[i].overridePrefab = null;
                        _registry.RebuildCache();
                        EditorUtility.SetDirty(_registry);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
