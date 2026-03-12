using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CAT.Utility
{
    /// <summary>
    /// SerializedObject에서 모든 직렬화 프로퍼티를 추출하는 유틸리티.
    /// </summary>
    public static class SerializedPropertyExtractor
    {
        // 스킵할 프로퍼티 이름
        static readonly HashSet<string> SkipProperties = new HashSet<string>
        {
            "m_Script",
            "m_ObjectHideFlags"
        };

        // 최대 프로퍼티 깊이 (무한 재귀 방지)
        const int MaxDepth = 10;

        /// <summary>
        /// 컴포넌트의 모든 직렬화 프로퍼티를 추출한다.
        /// </summary>
        public static List<PropertyData> ExtractProperties(Component component)
        {
            var result = new List<PropertyData>();
            var so = new SerializedObject(component);
            var iterator = so.GetIterator();

            // Generic(구조체/컨테이너)일 때만 자식 진입, 나머지는 형제로 이동
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false; // 기본값: 자식 진입하지 않음

                // 스킵 대상 확인
                if (SkipProperties.Contains(iterator.name))
                    continue;

                // 깊이 제한
                if (iterator.depth > MaxDepth)
                    continue;

                // Generic(RectOffset, 구조체 등)은 컨테이너이므로 자식으로 진입
                if (iterator.propertyType == SerializedPropertyType.Generic)
                {
                    enterChildren = true;
                    continue;
                }

                var propData = ConvertProperty(iterator);
                if (propData != null)
                    result.Add(propData);
            }

            return result;
        }

        /// <summary>
        /// SerializedProperty를 PropertyData로 변환한다.
        /// </summary>
        static PropertyData ConvertProperty(SerializedProperty prop)
        {
            var data = new PropertyData
            {
                name = prop.propertyPath,
                type = prop.propertyType.ToString()
            };

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    data.value = prop.intValue.ToString();
                    break;

                case SerializedPropertyType.Boolean:
                    data.value = prop.boolValue ? "true" : "false";
                    break;

                case SerializedPropertyType.Float:
                    data.value = prop.floatValue.ToString("G9",
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;

                case SerializedPropertyType.String:
                    data.value = prop.stringValue ?? "";
                    break;

                case SerializedPropertyType.Color:
                {
                    var c = prop.colorValue;
                    data.value = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3}", c.r, c.g, c.b, c.a);
                    break;
                }

                case SerializedPropertyType.Vector2:
                {
                    var v = prop.vector2Value;
                    data.value = FormatFloats(v.x, v.y);
                    break;
                }

                case SerializedPropertyType.Vector3:
                {
                    var v = prop.vector3Value;
                    data.value = FormatFloats(v.x, v.y, v.z);
                    break;
                }

                case SerializedPropertyType.Vector4:
                {
                    var v = prop.vector4Value;
                    data.value = FormatFloats(v.x, v.y, v.z, v.w);
                    break;
                }

                case SerializedPropertyType.Vector2Int:
                {
                    var v = prop.vector2IntValue;
                    data.value = string.Format("{0},{1}", v.x, v.y);
                    break;
                }

                case SerializedPropertyType.Vector3Int:
                {
                    var v = prop.vector3IntValue;
                    data.value = string.Format("{0},{1},{2}", v.x, v.y, v.z);
                    break;
                }

                case SerializedPropertyType.Quaternion:
                {
                    var q = prop.quaternionValue;
                    data.value = FormatFloats(q.x, q.y, q.z, q.w);
                    break;
                }

                case SerializedPropertyType.Rect:
                {
                    var r = prop.rectValue;
                    data.value = FormatFloats(r.x, r.y, r.width, r.height);
                    break;
                }

                case SerializedPropertyType.RectInt:
                {
                    var r = prop.rectIntValue;
                    data.value = string.Format("{0},{1},{2},{3}", r.x, r.y, r.width, r.height);
                    break;
                }

                case SerializedPropertyType.Bounds:
                {
                    var b = prop.boundsValue;
                    data.value = FormatFloats(
                        b.center.x, b.center.y, b.center.z,
                        b.size.x, b.size.y, b.size.z);
                    break;
                }

                case SerializedPropertyType.Enum:
                    data.value = prop.intValue.ToString();
                    break;

                case SerializedPropertyType.ObjectReference:
                {
                    var obj = prop.objectReferenceValue;
                    if (obj != null)
                    {
                        string guid;
                        long localId;
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId))
                        {
                            data.objectRefGuid = guid;
                            data.objectRefType = obj.GetType().FullName;
                            data.value = obj.name;
                        }
                        else
                        {
                            // 씬 내 오브젝트 등 GUID 없는 경우
                            data.value = obj.name;
                            data.objectRefType = obj.GetType().FullName;
                        }
                    }
                    else
                    {
                        data.value = "null";
                    }
                    break;
                }

                case SerializedPropertyType.LayerMask:
                    data.value = prop.intValue.ToString();
                    break;

                case SerializedPropertyType.ArraySize:
                    data.value = prop.intValue.ToString();
                    break;

                case SerializedPropertyType.AnimationCurve:
                {
                    // 애니메이션 커브는 키프레임 정보를 직렬화
                    var curve = prop.animationCurveValue;
                    if (curve != null && curve.keys.Length > 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        for (int i = 0; i < curve.keys.Length; i++)
                        {
                            var key = curve.keys[i];
                            if (i > 0) sb.Append("|");
                            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "{0},{1},{2},{3},{4},{5}",
                                key.time, key.value,
                                key.inTangent, key.outTangent,
                                key.inWeight, key.outWeight);
                        }
                        data.value = sb.ToString();
                    }
                    else
                    {
                        data.value = "";
                    }
                    break;
                }

                case SerializedPropertyType.Gradient:
                    // Gradient는 SerializedProperty에서 직접 접근이 제한적 — 스킵
                    data.value = "(gradient)";
                    break;

                case SerializedPropertyType.Generic:
                    // 구조체/클래스 컨테이너 — 자식 프로퍼티로 분해되므로 자체는 스킵
                    return null;

                default:
                    data.value = "(unsupported)";
                    break;
            }

            return data;
        }

        /// <summary>
        /// Transform 데이터를 추출한다.
        /// </summary>
        public static TransformData ExtractTransform(Transform transform)
        {
            var data = new TransformData();
            var rectTransform = transform as RectTransform;
            data.isRectTransform = rectTransform != null;

            var pos = transform.localPosition;
            data.localPosition[0] = pos.x;
            data.localPosition[1] = pos.y;
            data.localPosition[2] = pos.z;

            var rot = transform.localRotation;
            data.localRotation[0] = rot.x;
            data.localRotation[1] = rot.y;
            data.localRotation[2] = rot.z;
            data.localRotation[3] = rot.w;

            var scale = transform.localScale;
            data.localScale[0] = scale.x;
            data.localScale[1] = scale.y;
            data.localScale[2] = scale.z;

            if (rectTransform != null)
            {
                var ancMin = rectTransform.anchorMin;
                data.anchorMin[0] = ancMin.x;
                data.anchorMin[1] = ancMin.y;

                var ancMax = rectTransform.anchorMax;
                data.anchorMax[0] = ancMax.x;
                data.anchorMax[1] = ancMax.y;

                var ancPos = rectTransform.anchoredPosition;
                data.anchoredPosition[0] = ancPos.x;
                data.anchoredPosition[1] = ancPos.y;

                var sd = rectTransform.sizeDelta;
                data.sizeDelta[0] = sd.x;
                data.sizeDelta[1] = sd.y;

                var piv = rectTransform.pivot;
                data.pivot[0] = piv.x;
                data.pivot[1] = piv.y;
            }

            return data;
        }

        /// <summary>
        /// GameObject를 재귀적으로 GameObjectNode로 변환한다.
        /// </summary>
        /// <param name="go">대상 GameObject</param>
        /// <param name="isRoot">최상위 프리팹이면 true (중첩 프리팹 감지 제외)</param>
        public static GameObjectNode ExtractGameObject(GameObject go, bool isRoot = true)
        {
            // 중첩 프리팹 감지: 최상위가 아니고, 프리팹 인스턴스 루트인 경우
            if (!isRoot)
            {
                var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabSource != null &&
                    PrefabUtility.GetNearestPrefabInstanceRoot(go) == go)
                {
                    string guid;
                    long localId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabSource, out guid, out localId))
                    {
                        // 프리팹 참조 + 컴포넌트(추가/오버라이드 포함) 저장, 자식은 생략
                        var prefabNode = new GameObjectNode
                        {
                            name = go.name,
                            active = go.activeSelf,
                            prefabGuid = guid,
                            transform = ExtractTransform(go.transform)
                        };

                        // 모든 컴포넌트 추출 (추가 컴포넌트 + 기존 컴포넌트의 오버라이드 값)
                        var comps = go.GetComponents<Component>();
                        for (int i = 0; i < comps.Length; i++)
                        {
                            var comp = comps[i];
                            if (comp == null) continue;
                            if (comp is Transform) continue;

                            prefabNode.components.Add(new ComponentData
                            {
                                typeName = comp.GetType().FullName,
                                enabled = IsComponentEnabled(comp),
                                properties = ExtractProperties(comp)
                            });
                        }

                        return prefabNode;
                    }
                }
            }

            var node = new GameObjectNode
            {
                name = go.name,
                active = go.activeSelf,
                tag = go.tag,
                layer = go.layer,
                transform = ExtractTransform(go.transform)
            };

            // Transform을 제외한 모든 컴포넌트 추출
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null) continue; // Missing 컴포넌트 스킵
                if (comp is Transform) continue; // Transform은 별도 처리

                var compData = new ComponentData
                {
                    typeName = comp.GetType().FullName,
                    enabled = IsComponentEnabled(comp),
                    properties = ExtractProperties(comp)
                };
                node.components.Add(compData);
            }

            // 자식 오브젝트 재귀 추출
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i).gameObject;
                node.children.Add(ExtractGameObject(child, false));
            }

            return node;
        }

        /// <summary>
        /// 컴포넌트의 enabled 상태를 가져온다.
        /// </summary>
        static bool IsComponentEnabled(Component component)
        {
            // Behaviour 기반 컴포넌트는 enabled 프로퍼티가 있다
            if (component is Behaviour behaviour)
                return behaviour.enabled;

            // Renderer 기반
            if (component is Renderer renderer)
                return renderer.enabled;

            // Collider 기반
            if (component is Collider collider)
                return collider.enabled;

            // 기본값: 활성화
            return true;
        }

        static string FormatFloats(params float[] values)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(values[i].ToString("G9",
                    System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
