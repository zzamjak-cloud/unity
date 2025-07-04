using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    internal class DrawCallback
    {
        public Action<Rect> AfterDraw;
        public Action<Rect> BeforeDraw;
    }

    internal class FR2_TabView
    {
        private readonly List<float> labelWidths = new List<float>();
        public DrawCallback callback;
        public bool canDeselectAll; // Can there be no active tabs
        public int current;

        public bool flexibleWidth = true;
        public GUIContent[] labels;
        private float labelTotalWidth;
        public float offsetFirst;
        public float offsetLast;
        public Action onTabChange;
        public float padding = 4f;

        // Cached Rects for layout to avoid GC
        private Rect toolbarRect;
        public IWindow window;

        public FR2_TabView(IWindow w, bool canDeselectAll)
        {
            window = w;
            this.canDeselectAll = canDeselectAll;
        }

        public bool DrawLayout()
        {
            var result = false;

            // Define the toolbar rect
            GUIStyle style = EditorStyles.toolbarButton;

            // Set up label rects if not already done
            if (labelTotalWidth == 0 || labelWidths.Count != labels.Length)
            {
                labelTotalWidth = 0;
                labelWidths.Clear();
                for (var i = 0; i < labels.Length; i++)
                {
                    float w = style.CalcSize(labels[i]).x;
                    labelWidths.Add(w);
                    labelTotalWidth += w;
                }
            }

            toolbarRect = GUILayoutUtility.GetRect(0, Screen.width, 20f, 20f);
            GUI.Box(toolbarRect, GUIContent.none, EditorStyles.toolbar);
            if (!flexibleWidth) toolbarRect.width = labelTotalWidth + labels.Length * padding;

            // Call before draw action if available
            callback?.BeforeDraw?.Invoke(toolbarRect);

            // Draw each tab
            Rect tabRect = toolbarRect;
            tabRect.xMin += offsetFirst;
            tabRect.xMax -= offsetLast;

            // float ratio = flexibleWidth ? ((toolbarRect.width - offsetFirst - offsetLast) / labelTotalWidth) : 1;
            float flexSpace = Mathf.Max(0, tabRect.width - labelTotalWidth) / labels.Length;
            float pad = flexibleWidth ? flexSpace : padding;

            for (var i = 0; i < labels.Length; i++)
            {
                bool isActive = i == current;
                GUIContent lb = labels[i];

                // Define the toggle rect
                float w = labelWidths[i] + pad;
                tabRect.width = w;

                // Draw the toggle (or button) for the tab
                bool clicked = lb.image != null
                    ? GUI2.ToolbarToggle(ref isActive, lb.image, Vector2.zero, lb.tooltip, tabRect)
                    : GUI2.Toggle(ref isActive, lb, EditorStyles.toolbarButton, tabRect);

                tabRect.x += w;
                if (!clicked)
                {

                    continue;
                }

                // Update the current tab and handle tab change logic
                current = !isActive && canDeselectAll ? -1 : i;
                result = true;

                onTabChange?.Invoke();
                if (window != null)
                {
                    window.OnSelectionChange(); // Force refresh tabs
                    window.WillRepaint = true;
                }
            }

            // Call after draw action if available
            callback?.AfterDraw?.Invoke(toolbarRect);
            return result;
        }

        public static FR2_TabView FromEnum(Type enumType, IWindow w, bool canDeselectAll = false)
        {
            Array values = Enum.GetValues(enumType);
            var labels = new List<GUIContent>();

            foreach (object item in values)
            {
                labels.Add(FR2_GUIContent.FromString(item.ToString()));
            }

            return new FR2_TabView(w, canDeselectAll) { current = 0, labels = labels.ToArray() };
        }

        public static GUIContent GetGUIContent(object tex)
        {
            if (tex is GUIContent content) return content;
            if (tex is Texture texture) return FR2_GUIContent.FromTexture(texture);
            if (tex is string s) return FR2_GUIContent.FromString(s);
            return GUIContent.none;
        }

        public static FR2_TabView Create(IWindow w, bool canDeselectAll = false, params object[] titles)
        {
            var labels = new List<GUIContent>();
            foreach (object item in titles)
            {
                labels.Add(GetGUIContent(item));
            }
            return new FR2_TabView(w, canDeselectAll) { current = 0, labels = labels.ToArray() };
        }
    }
}
