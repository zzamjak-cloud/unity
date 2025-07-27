using UnityEngine;
using TMPro;
using UnityEditor;

namespace CAT.UI
{
    [AddComponentMenu("CAT/TMP/UITMPFollow")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class UITMPFollow : MonoBehaviour
    {
        public TextMeshProUGUI parent;
        private TextMeshProUGUI child;

        void OnEnable()
        {
            if (!child) child = this.GetComponent<TextMeshProUGUI>();
            if (!parent) return;

            UpdateTextProperties();
        }

        void Update()
        {
            if (!child || !parent) return;

            UpdateTextProperties();
        }

        private void UpdateTextProperties()
        {
            child.font = parent.font;
            child.fontStyle = parent.fontStyle;
            child.fontWeight = parent.fontWeight;
            child.alignment = parent.alignment;
            child.characterSpacing = parent.characterSpacing;
            child.extraPadding = parent.extraPadding;
            child.text = parent.text;
            child.fontSize = parent.fontSize;

            // Scene View ?????? ????
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(child);
            }
        }

        private void OnValidate()
        {
            if (!child) child = this.GetComponent<TextMeshProUGUI>();
        }
    }
}