using System;
using System.Collections.Generic;
using System.Text;

namespace CAT.Utility
{
    /// <summary>
    /// 프리팹 JSON 직렬화용 데이터 클래스.
    /// JsonUtility 미사용 — 수동 JSON 조합 방식.
    /// </summary>

    // 최상위 루트
    [Serializable]
    public class PrefabJsonRoot
    {
        public string prefabName;
        public string exportDate;
        public GameObjectNode root;

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.AppendFormat("  \"prefabName\": {0},\n", JsonStringEncode(prefabName));
            sb.AppendFormat("  \"exportDate\": {0},\n", JsonStringEncode(exportDate));
            sb.Append("  \"root\": ");
            root.WriteJson(sb, 2);
            sb.Append("\n}");
            return sb.ToString();
        }

        internal static string JsonStringEncode(string value)
        {
            if (value == null) return "null";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        internal static void WriteIndent(StringBuilder sb, int indent)
        {
            for (int i = 0; i < indent; i++)
                sb.Append("  ");
        }
    }

    // 게임오브젝트 노드
    [Serializable]
    public class GameObjectNode
    {
        public string name;
        public bool active;
        public string tag;
        public int layer;
        public TransformData transform;
        public List<ComponentData> components = new List<ComponentData>();
        public List<GameObjectNode> children = new List<GameObjectNode>();

        // 중첩 프리팹 참조 (null이면 일반 오브젝트)
        public string prefabGuid;

        // 중첩 프리팹 자식 오브젝트의 오버라이드 (레거시 — 하위 호환용)
        public List<ChildOverrideData> childOverrides = new List<ChildOverrideData>();

        // PrefabUtility.GetPropertyModifications()로 추출한 원시 오버라이드 데이터
        public List<PrefabModData> modifications = new List<PrefabModData>();

        public bool IsPrefabReference => !string.IsNullOrEmpty(prefabGuid);

        public void WriteJson(StringBuilder sb, int indent)
        {
            sb.Append("{\n");
            var ind = indent + 1;

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"name\": {0},\n", PrefabJsonRoot.JsonStringEncode(name));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"active\": {0},\n", active ? "true" : "false");

            // 중첩 프리팹이면 GUID + Transform + 컴포넌트(오버라이드/추가) 기록, 자식은 생략
            if (IsPrefabReference)
            {
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"prefabGuid\": {0},\n", PrefabJsonRoot.JsonStringEncode(prefabGuid));
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.Append("\"transform\": ");
                transform.WriteJson(sb, ind);

                // 컴포넌트가 있으면 출력 (추가 컴포넌트 + 기존 오버라이드)
                if (components != null && components.Count > 0)
                {
                    sb.Append(",\n");
                    PrefabJsonRoot.WriteIndent(sb, ind);
                    sb.Append("\"components\": ");
                    WriteComponentArray(sb, ind);
                }

                // 자식 오브젝트 오버라이드 (레거시)
                if (childOverrides != null && childOverrides.Count > 0)
                {
                    sb.Append(",\n");
                    PrefabJsonRoot.WriteIndent(sb, ind);
                    sb.Append("\"childOverrides\": ");
                    WriteChildOverrideArray(sb, ind);
                }

                // PropertyModification 기반 오버라이드 (신규)
                if (modifications != null && modifications.Count > 0)
                {
                    sb.Append(",\n");
                    PrefabJsonRoot.WriteIndent(sb, ind);
                    sb.Append("\"modifications\": ");
                    WriteModificationArray(sb, ind);
                }

                sb.Append("\n");
                PrefabJsonRoot.WriteIndent(sb, indent);
                sb.Append("}");
                return;
            }

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"tag\": {0},\n", PrefabJsonRoot.JsonStringEncode(tag));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"layer\": {0},\n", layer);

            // Transform
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.Append("\"transform\": ");
            transform.WriteJson(sb, ind);
            sb.Append(",\n");

            // 컴포넌트 배열
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.Append("\"components\": ");
            WriteComponentArray(sb, ind);
            sb.Append(",\n");

            // 자식 노드 배열
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.Append("\"children\": ");
            WriteChildrenArray(sb, ind);
            sb.Append("\n");

            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("}");
        }

        void WriteComponentArray(StringBuilder sb, int indent)
        {
            if (components == null || components.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[\n");
            for (int i = 0; i < components.Count; i++)
            {
                PrefabJsonRoot.WriteIndent(sb, indent + 1);
                components[i].WriteJson(sb, indent + 1);
                if (i < components.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("]");
        }

        void WriteChildrenArray(StringBuilder sb, int indent)
        {
            if (children == null || children.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[\n");
            for (int i = 0; i < children.Count; i++)
            {
                PrefabJsonRoot.WriteIndent(sb, indent + 1);
                children[i].WriteJson(sb, indent + 1);
                if (i < children.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("]");
        }

        void WriteChildOverrideArray(StringBuilder sb, int indent)
        {
            sb.Append("[\n");
            for (int i = 0; i < childOverrides.Count; i++)
            {
                PrefabJsonRoot.WriteIndent(sb, indent + 1);
                childOverrides[i].WriteJson(sb, indent + 1);
                if (i < childOverrides.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("]");
        }

        void WriteModificationArray(StringBuilder sb, int indent)
        {
            sb.Append("[\n");
            for (int i = 0; i < modifications.Count; i++)
            {
                PrefabJsonRoot.WriteIndent(sb, indent + 1);
                modifications[i].WriteJson(sb, indent + 1);
                if (i < modifications.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("]");
        }
    }

    // Transform 데이터 (Transform/RectTransform 통합)
    [Serializable]
    public class TransformData
    {
        public bool isRectTransform;
        public float[] localPosition = new float[3];
        public float[] localRotation = new float[4]; // Quaternion
        public float[] localScale = new float[3];

        // RectTransform 전용
        public float[] anchorMin = new float[2];
        public float[] anchorMax = new float[2];
        public float[] anchoredPosition = new float[2];
        public float[] sizeDelta = new float[2];
        public float[] pivot = new float[2];

        public void WriteJson(StringBuilder sb, int indent)
        {
            sb.Append("{\n");
            var ind = indent + 1;

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"isRectTransform\": {0},\n", isRectTransform ? "true" : "false");
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"localPosition\": [{0}, {1}, {2}],\n",
                FloatStr(localPosition[0]), FloatStr(localPosition[1]), FloatStr(localPosition[2]));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"localRotation\": [{0}, {1}, {2}, {3}],\n",
                FloatStr(localRotation[0]), FloatStr(localRotation[1]),
                FloatStr(localRotation[2]), FloatStr(localRotation[3]));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"localScale\": [{0}, {1}, {2}]",
                FloatStr(localScale[0]), FloatStr(localScale[1]), FloatStr(localScale[2]));

            if (isRectTransform)
            {
                sb.Append(",\n");
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"anchorMin\": [{0}, {1}],\n",
                    FloatStr(anchorMin[0]), FloatStr(anchorMin[1]));
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"anchorMax\": [{0}, {1}],\n",
                    FloatStr(anchorMax[0]), FloatStr(anchorMax[1]));
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"anchoredPosition\": [{0}, {1}],\n",
                    FloatStr(anchoredPosition[0]), FloatStr(anchoredPosition[1]));
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"sizeDelta\": [{0}, {1}],\n",
                    FloatStr(sizeDelta[0]), FloatStr(sizeDelta[1]));
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"pivot\": [{0}, {1}]",
                    FloatStr(pivot[0]), FloatStr(pivot[1]));
            }

            sb.Append("\n");
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("}");
        }

        static string FloatStr(float v)
        {
            // 소수점 이하 불필요한 0 제거, 로케일 독립적 출력
            return v.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // 컴포넌트 데이터
    [Serializable]
    public class ComponentData
    {
        public string typeName; // FullName (예: "UnityEngine.UI.Image")
        public bool enabled;
        public List<PropertyData> properties = new List<PropertyData>();

        public void WriteJson(StringBuilder sb, int indent)
        {
            sb.Append("{\n");
            var ind = indent + 1;

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"typeName\": {0},\n", PrefabJsonRoot.JsonStringEncode(typeName));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"enabled\": {0},\n", enabled ? "true" : "false");

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.Append("\"properties\": ");
            WritePropertyArray(sb, ind);
            sb.Append("\n");

            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("}");
        }

        void WritePropertyArray(StringBuilder sb, int indent)
        {
            if (properties == null || properties.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[\n");
            for (int i = 0; i < properties.Count; i++)
            {
                PrefabJsonRoot.WriteIndent(sb, indent + 1);
                properties[i].WriteJson(sb, indent + 1);
                if (i < properties.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("]");
        }
    }

    // 프로퍼티 데이터
    [Serializable]
    public class PropertyData
    {
        public string name;           // propertyPath
        public string type;           // SerializedPropertyType 이름
        public string value;          // 문자열 직렬화 값
        public string objectRefGuid;  // ObjectReference용 GUID
        public string objectRefType;  // 참조 타입명

        public void WriteJson(StringBuilder sb, int indent)
        {
            sb.Append("{\n");
            var ind = indent + 1;

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"name\": {0},\n", PrefabJsonRoot.JsonStringEncode(name));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"type\": {0},\n", PrefabJsonRoot.JsonStringEncode(type));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"value\": {0}", PrefabJsonRoot.JsonStringEncode(value));

            if (!string.IsNullOrEmpty(objectRefGuid))
            {
                sb.Append(",\n");
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"objectRefGuid\": {0},\n", PrefabJsonRoot.JsonStringEncode(objectRefGuid));
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"objectRefType\": {0}", PrefabJsonRoot.JsonStringEncode(objectRefType));
            }

            sb.Append("\n");
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("}");
        }
    }

    // 중첩 프리팹 자식 오브젝트 오버라이드 데이터
    [Serializable]
    public class ChildOverrideData
    {
        public string childPath;  // 프리팹 루트 기준 상대 경로 (예: "Text", "Icon/Image")
        public bool active;
        public TransformData transform;
        public List<ComponentData> components = new List<ComponentData>();

        public void WriteJson(StringBuilder sb, int indent)
        {
            sb.Append("{\n");
            var ind = indent + 1;

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"childPath\": {0},\n", PrefabJsonRoot.JsonStringEncode(childPath));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"active\": {0},\n", active ? "true" : "false");

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.Append("\"transform\": ");
            transform.WriteJson(sb, ind);
            sb.Append(",\n");

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.Append("\"components\": ");
            if (components == null || components.Count == 0)
            {
                sb.Append("[]");
            }
            else
            {
                sb.Append("[\n");
                for (int i = 0; i < components.Count; i++)
                {
                    PrefabJsonRoot.WriteIndent(sb, ind + 1);
                    components[i].WriteJson(sb, ind + 1);
                    if (i < components.Count - 1) sb.Append(",");
                    sb.Append("\n");
                }
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.Append("]");
            }

            sb.Append("\n");
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("}");
        }
    }

    // PrefabUtility.PropertyModification의 직렬화용 데이터
    [Serializable]
    public class PrefabModData
    {
        public long targetFileId;    // 소스 프리팹 내 대상 오브젝트의 localIdentifierInFile
        public string propertyPath;  // 프로퍼티 경로 (예: "m_Color.r", "m_IsActive")
        public string value;         // 문자열 값
        public string objectRefGuid; // ObjectReference용 에셋 GUID (null이면 값 타입)

        public void WriteJson(StringBuilder sb, int indent)
        {
            sb.Append("{\n");
            var ind = indent + 1;

            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"targetFileId\": {0},\n", targetFileId);
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"propertyPath\": {0},\n", PrefabJsonRoot.JsonStringEncode(propertyPath));
            PrefabJsonRoot.WriteIndent(sb, ind);
            sb.AppendFormat("\"value\": {0}", PrefabJsonRoot.JsonStringEncode(value));

            if (!string.IsNullOrEmpty(objectRefGuid))
            {
                sb.Append(",\n");
                PrefabJsonRoot.WriteIndent(sb, ind);
                sb.AppendFormat("\"objectRefGuid\": {0}", PrefabJsonRoot.JsonStringEncode(objectRefGuid));
            }

            sb.Append("\n");
            PrefabJsonRoot.WriteIndent(sb, indent);
            sb.Append("}");
        }
    }
}
