using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace CAT.VFX.Internal
{
    /// <summary>
    /// 효율적인 델리게이트 컨테이너 - 순회 중 안전한 추가/제거 지원
    /// </summary>
    internal class FastActionBase<T>
    {
        private static readonly ObjectPool<LinkedListNode<T>> s_NodePool =
            new ObjectPool<LinkedListNode<T>>(
                () => new LinkedListNode<T>(default),
                null,
                x => x.Value = default);

        private readonly LinkedList<T> _delegates = new LinkedList<T>();

        public void Add(T rhs)
        {
            if (rhs == null) return;
            var node = s_NodePool.Get();
            node.Value = rhs;
            _delegates.AddLast(node);
        }

        public void Remove(T rhs)
        {
            if (rhs == null) return;
            var node = _delegates.Find(rhs);
            if (node != null)
            {
                _delegates.Remove(node);
                s_NodePool.Release(node);
            }
        }

        protected void Invoke(Action<T> callback)
        {
            var node = _delegates.First;
            while (node != null)
            {
                try
                {
                    callback(node.Value);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }

                node = node.Next;
            }
        }

        public void Clear()
        {
            var node = _delegates.First;
            while (node != null)
            {
                var next = node.Next;
                _delegates.Remove(node);
                s_NodePool.Release(node);
                node = next;
            }
        }
    }

    /// <summary>
    /// 파라미터 없는 FastAction
    /// </summary>
    internal class FastAction : FastActionBase<Action>
    {
        public void Invoke()
        {
            Invoke(action => action.Invoke());
        }
    }
}
