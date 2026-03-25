using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using CAT.HierarchyUtility;

namespace CAT.Utility
{
    /// <summary>
    /// 하이어라키 창 상단에 JSON 프리팹 생성 드롭다운 버튼을 추가하는 모듈.
    /// HierarchyPresetMenuModule(UIOrder=20) 왼쪽에 배치된다.
    /// </summary>
    public class HierarchyJsonPrefabModule : IHierarchyToolModule
    {
        // Preset 버튼(60px + 4px 여백) 왼쪽에 배치
        const float PresetButtonWidth = 60f;
        const float PresetButtonMargin = 4f;
        const float ButtonWidth = 40f;
        const float ButtonGap = 2f;

        public string ModuleName => "HierarchyJsonPrefab";
        public int UIOrder => 19; // HierarchyPresetMenuModule(20) 바로 앞

        GUIContent _buttonContent;

        public void Initialize(HierarchyWindowAccessor accessor)
        {
            _buttonContent = new GUIContent("UI");
        }

        public void InitUI(VisualElement container) { }
        public void OnUpdate() { }
        public void OnSelectionChanged() { }
        public void OnHierarchyChanged() { }
        public void Dispose() { }

        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                DrawButton();
            }
            else if (selectionRect.y < 20 && selectionRect.x < 50)
            {
                DrawButton();
            }
        }

        void DrawButton()
        {
            // Preset 버튼 왼쪽에 배치
            float presetButtonX = EditorGUIUtility.currentViewWidth - PresetButtonWidth - PresetButtonMargin;
            float buttonX = presetButtonX - ButtonWidth - ButtonGap;
            Rect buttonRect = new Rect(buttonX, 0, ButtonWidth, 20f);

            if (EditorGUI.DropdownButton(buttonRect, _buttonContent, FocusType.Passive))
            {
                string jsonPath = UIDesignMaker.JsonBasePath;
                var dropdown = new JsonPrefabDropdown(new AdvancedDropdownState(), jsonPath);
                dropdown.Show(buttonRect);
            }
        }
    }
}
