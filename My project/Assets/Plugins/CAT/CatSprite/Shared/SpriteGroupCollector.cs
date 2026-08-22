using System.Collections.Generic;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// 그룹 단위로 자식 렌더러를 다루는 컴포넌트들이 공유하는 수집 로직.
    /// SpriteGroupTint(정점 색)와 SpriteGroupEffect(머티리얼)는 건드리는 채널이 달라 서로 독립이지만,
    /// "자식을 모으고 중첩 그룹에 양보하고 이전 원본 값을 승계한다"는 절차는 동일하다.
    ///
    /// 양보 판정은 TOwner 타입 단위다. 서로 다른 채널을 쓰는 그룹끼리는 양보하지 않는다.
    /// (같은 오브젝트에 Tint와 Effect가 함께 있어도 각자 전체 자식을 관리한다)
    /// </summary>
    internal static class SpriteGroupCollector
    {
        // MapPreviousIndices용 재사용 버퍼. Clear는 용량을 유지하므로 워밍업 이후 힙 할당이 없다.
        private static readonly Dictionary<Object, int> indexScratch = new Dictionary<Object, int>();

        /// <summary>
        /// owner 하위의 TRenderer를 모아 results에 채운다.
        /// 파괴된 항목과, owner 사이에 다른 TOwner가 끼어 있는 항목(= 그쪽 그룹 소유)은 제외한다.
        /// </summary>
        public static void Collect<TOwner, TRenderer>(TOwner owner, bool includeInactive, List<TRenderer> results)
            where TOwner : Component
            where TRenderer : Component
        {
            results.Clear();

            if (owner == null)
                return;

            owner.GetComponentsInChildren(includeInactive, results);

            Transform self = owner.transform;

            for (int i = results.Count - 1; i >= 0; i--)
            {
                TRenderer r = results[i];

                // 제네릭 T로는 UnityEngine.Object의 == 오버로드가 잡히지 않아 파괴된 오브젝트를
                // null로 인식하지 못한다. Component로 캐스팅해서 비교해야 한다.
                if ((Component)r == null || HasNestedOwner<TOwner>(self, r.transform))
                    results.RemoveAt(i);
            }
        }

        /// <summary>owner와 target 사이에 다른 TOwner가 있으면 true. (target 자신도 검사 대상)</summary>
        public static bool HasNestedOwner<TOwner>(Transform owner, Transform target)
            where TOwner : Component
        {
            for (Transform t = target; t != null && t != owner; t = t.parent)
            {
                if ((Component)t.GetComponent<TOwner>() != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// current의 각 항목이 previous에서 몇 번 인덱스였는지 indices에 채운다. 없으면 -1.
        /// 재수집 때 이미 알고 있던 원본 값(색/머티리얼)을 승계하기 위한 것이다.
        /// 루프 안에서 List.IndexOf를 부르면 O(n²)이 되므로 조회 맵을 한 번만 만든다.
        /// </summary>
        public static void MapPreviousIndices<T>(List<T> previous, List<T> current, List<int> indices)
            where T : Object
        {
            indices.Clear();
            indexScratch.Clear();

            for (int i = 0; i < previous.Count; i++)
            {
                // 타입 파라미터로는 UnityEngine.Object의 == 오버로드가 잡히지 않는다.
                // Object로 받아야 파괴된 오브젝트가 null로 판정된다.
                // (파괴된 오브젝트끼리는 Equals가 서로 true라 Dictionary 키로 넣으면 안 된다)
                Object item = previous[i];
                if (item == null)
                    continue;

                // 같은 렌더러가 중복 등록된 비정상 데이터에서도 예외 없이 첫 항목을 쓴다.
                if (!indexScratch.ContainsKey(item))
                    indexScratch.Add(item, i);
            }

            for (int i = 0; i < current.Count; i++)
            {
                Object item = current[i];
                indices.Add(item != null && indexScratch.TryGetValue(item, out int found) ? found : -1);
            }

            indexScratch.Clear();
        }

        /// <summary>두 목록이 같은 항목을 같은 순서로 담고 있으면 true.</summary>
        public static bool AreSame<T>(List<T> a, List<T> b) where T : Object
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                // 여기서도 Object로 받아야 파괴된 항목이 null로 비교된다.
                Object left = a[i];
                Object right = b[i];

                if (left != right)
                    return false;
            }

            return true;
        }
    }
}
