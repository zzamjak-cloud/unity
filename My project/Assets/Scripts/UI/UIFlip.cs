using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CAT.UI
{
    [RequireComponent(typeof(RectTransform)),
     RequireComponent(typeof(Graphic)),
     DisallowMultipleComponent,
     AddComponentMenu("CAT/UIEffect/UIFlip")]
    public class UIFlip : MonoBehaviour, IMeshModifier
    {
        [SerializeField]
        private bool m_Horizontal = false;
        [SerializeField]
        private bool m_Veritical = false;

        public bool horizontal
        {
            get { return this.m_Horizontal; }
            set { this.m_Horizontal = value; this.GetComponent<Graphic>().SetVerticesDirty(); }
        }
        public bool vertical
        {
            get { return this.m_Veritical; }
            set { this.m_Veritical = value; this.GetComponent<Graphic>().SetVerticesDirty(); }
        }

        protected void OnValidate()
        {
            this.GetComponent<Graphic>().SetVerticesDirty();
        }

        public void ModifyVertices(List<UIVertex> verts)
        {
            if (verts.Count == 0) return;

            RectTransform rt = this.transform as RectTransform;
            Rect rect = rt.rect;

            // Calculate bounds of vertices
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < verts.Count; ++i)
            {
                Vector2 pos = verts[i].position;
                min.x = Mathf.Min(min.x, pos.x);
                min.y = Mathf.Min(min.y, pos.y);
                max.x = Mathf.Max(max.x, pos.x);
                max.y = Mathf.Max(max.y, pos.y);
            }

            Vector2 size = max - min;
            Vector2 center = min + (size * 0.5f);

            for (int i = 0; i < verts.Count; ++i)
            {
                UIVertex v = verts[i];
                Vector3 position = v.position;

                // Transform relative to bounds center
                position -= new Vector3(center.x, center.y, 0);

                if (this.m_Horizontal)
                {
                    position.x = -position.x;
                }

                if (this.m_Veritical)
                {
                    position.y = -position.y;
                }

                // Transform back
                position += new Vector3(center.x, center.y, 0);

                v.position = position;
                verts[i] = v;
            }
        }

        public void ModifyMesh(Mesh mesh) { }

        public void ModifyMesh(VertexHelper verts)
        {
            List<UIVertex> buffer = new List<UIVertex>();
            verts.GetUIVertexStream(buffer);
            ModifyVertices(buffer);
            verts.Clear();
            verts.AddUIVertexTriangleStream(buffer);
        }
    }
}