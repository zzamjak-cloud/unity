using UnityEngine;
using System.Collections.Generic;

namespace CAT.UI
{
    /// <summary>
    /// TMP Effect 카테고리 설정 (ScriptableObject)
    /// - 사용자 정의 카테고리 관리
    /// - 기본 카테고리 + 사용자 추가 카테고리 지원
    /// - EditorPrefs 대신 프로젝트 에셋으로 저장
    /// </summary>
    [CreateAssetMenu(fileName = "TMPEffectCategorySettings", menuName = "CAT/UI/TMP Effect Category Settings")]
    public class TMPEffectCategorySettings : ScriptableObject
    {
        // ─────────────────────────────────────────────
        // 기본 카테고리 (삭제 불가)
        // ─────────────────────────────────────────────

        private static readonly string[] DEFAULT_CATEGORIES = new string[]
        {
            "Title",
            "Button",
            "Custom"
        };

        /// <summary>
        /// 기본 카테고리 이름 (삭제된 카테고리를 가진 프리셋의 폴백용)
        /// </summary>
        public const string FALLBACK_CATEGORY = "Custom";

        // ─────────────────────────────────────────────
        // 사용자 정의 카테고리
        // ─────────────────────────────────────────────

        [SerializeField]
        private List<string> _customCategories = new List<string>();

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 기본 카테고리 목록 (읽기 전용)
        /// </summary>
        public static IReadOnlyList<string> DefaultCategories => DEFAULT_CATEGORIES;

        /// <summary>
        /// 사용자 정의 카테고리 목록
        /// </summary>
        public IReadOnlyList<string> CustomCategories => _customCategories;

        /// <summary>
        /// 모든 카테고리 목록 (기본 + 사용자 정의)
        /// </summary>
        public string[] GetAllCategories()
        {
            var all = new List<string>(DEFAULT_CATEGORIES);
            all.AddRange(_customCategories);
            return all.ToArray();
        }

        /// <summary>
        /// "전체" 옵션을 포함한 카테고리 목록 (드롭다운용)
        /// </summary>
        public string[] GetCategoryOptions()
        {
            var options = new List<string> { "전체" };
            options.AddRange(DEFAULT_CATEGORIES);
            options.AddRange(_customCategories);
            return options.ToArray();
        }

        /// <summary>
        /// 카테고리가 기본 카테고리인지 확인
        /// </summary>
        public bool IsDefaultCategory(string category)
        {
            foreach (var def in DEFAULT_CATEGORIES)
            {
                if (def == category) return true;
            }
            return false;
        }

        /// <summary>
        /// 카테고리 존재 여부 확인
        /// </summary>
        public bool HasCategory(string category)
        {
            foreach (var def in DEFAULT_CATEGORIES)
            {
                if (def == category) return true;
            }
            foreach (var custom in _customCategories)
            {
                if (custom == category) return true;
            }
            return false;
        }

        /// <summary>
        /// 사용자 정의 카테고리 추가
        /// </summary>
        /// <returns>성공 여부</returns>
        public bool AddCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return false;
            if (HasCategory(category)) return false;

            _customCategories.Add(category);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            return true;
        }

        /// <summary>
        /// 사용자 정의 카테고리 이름 변경
        /// </summary>
        /// <returns>성공 여부</returns>
        public bool RenameCategory(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return false;
            if (IsDefaultCategory(oldName)) return false;  // 기본 카테고리는 수정 불가
            if (HasCategory(newName)) return false;  // 중복 이름 불가

            int index = _customCategories.IndexOf(oldName);
            if (index < 0) return false;

            _customCategories[index] = newName;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            return true;
        }

        /// <summary>
        /// 사용자 정의 카테고리 삭제
        /// </summary>
        /// <returns>성공 여부</returns>
        public bool RemoveCategory(string category)
        {
            if (IsDefaultCategory(category)) return false;  // 기본 카테고리는 삭제 불가

            bool removed = _customCategories.Remove(category);

            if (removed)
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            return removed;
        }

        /// <summary>
        /// 카테고리 인덱스 가져오기 (드롭다운용, "전체" 포함)
        /// </summary>
        public int GetCategoryIndex(string category)
        {
            var options = GetCategoryOptions();
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == category) return i;
            }
            return 0;  // 못 찾으면 "전체"
        }

        /// <summary>
        /// 인덱스로 카테고리 가져오기 (드롭다운용, "전체" 포함)
        /// </summary>
        public string GetCategoryByIndex(int index)
        {
            var options = GetCategoryOptions();
            if (index < 0 || index >= options.Length) return "전체";
            return options[index];
        }

        // ─────────────────────────────────────────────
        // Singleton Instance (프로젝트에 하나만 존재)
        // ─────────────────────────────────────────────

        private static TMPEffectCategorySettings _instance;

        /// <summary>
        /// 싱글톤 인스턴스 (없으면 기본값 반환)
        /// </summary>
        public static TMPEffectCategorySettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadOrCreateInstance();
                }
                return _instance;
            }
        }

        private static TMPEffectCategorySettings LoadOrCreateInstance()
        {
#if UNITY_EDITOR
            // 에셋 검색
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TMPEffectCategorySettings");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<TMPEffectCategorySettings>(path);
            }

            // 없으면 생성
            var instance = CreateInstance<TMPEffectCategorySettings>();
            string savePath = "Assets/Scripts/TMPEffects/TMPEffectCategorySettings.asset";

            // 폴더 확인
            string directory = System.IO.Path.GetDirectoryName(savePath);
            if (!UnityEditor.AssetDatabase.IsValidFolder(directory))
            {
                savePath = "Assets/TMPEffectCategorySettings.asset";
            }

            UnityEditor.AssetDatabase.CreateAsset(instance, savePath);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[TMPEffectCategorySettings] 새 설정 에셋 생성: {savePath}");
            return instance;
#else
            // 런타임에서는 Resources에서 로드
            var instance = Resources.Load<TMPEffectCategorySettings>("TMPEffectCategorySettings");
            if (instance == null)
            {
                instance = CreateInstance<TMPEffectCategorySettings>();
            }
            return instance;
#endif
        }
    }
}
