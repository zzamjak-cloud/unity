using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CAT.VFX.Internal
{
    /// <summary>
    /// 제네릭 레퍼런스 카운팅 오브젝트 캐시
    /// Hash128 기반 키로 동일 오브젝트 공유, 참조 카운트 0이면 자동 파괴
    /// </summary>
    internal class ObjectRepository<T> where T : Object
    {
        private readonly Dictionary<Hash128, Entry> _cache = new Dictionary<Hash128, Entry>(8);
        private readonly Dictionary<int, Hash128> _objectKey = new Dictionary<int, Hash128>(8);
        private readonly Action<T> _onRelease;
        private readonly Stack<Entry> _pool = new Stack<Entry>(8);

        public ObjectRepository(Action<T> onRelease = null)
        {
            if (onRelease == null)
            {
                _onRelease = x =>
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        Object.DestroyImmediate(x, false);
                    }
                    else
#endif
                    {
                        Object.Destroy(x);
                    }
                };
            }
            else
            {
                _onRelease = onRelease;
            }

            for (var i = 0; i < 8; i++)
            {
                _pool.Push(new Entry());
            }
        }

        public int count => _cache.Count;

        public void Clear()
        {
            foreach (var kv in _cache)
            {
                var entry = kv.Value;
                if (entry == null) continue;

                entry.Release(_onRelease);
                _pool.Push(entry);
            }

            _cache.Clear();
            _objectKey.Clear();
        }

        public bool Valid(Hash128 hash, T obj)
        {
            return _cache.TryGetValue(hash, out var entry) && entry.storedObject == obj;
        }

        public void Get(Hash128 hash, ref T obj, Func<T> onCreate)
        {
            if (GetFromCache(hash, ref obj)) return;
            Add(hash, ref obj, onCreate());
        }

        public void Get<TS>(Hash128 hash, ref T obj, Func<TS, T> onCreate, TS source)
        {
            if (GetFromCache(hash, ref obj)) return;
            Add(hash, ref obj, onCreate(source));
        }

        private bool GetFromCache(Hash128 hash, ref T obj)
        {
            if (_cache.TryGetValue(hash, out var entry))
            {
                if (!entry.storedObject)
                {
                    Release(ref entry.storedObject);
                    return false;
                }

                if (entry.storedObject != obj)
                {
                    Release(ref obj);
                    ++entry.reference;
                    obj = entry.storedObject;
                }

                return true;
            }

            return false;
        }

        private void Add(Hash128 hash, ref T obj, T newObject)
        {
            if (!newObject)
            {
                Release(ref obj);
                obj = newObject;
                return;
            }

            var newEntry = 0 < _pool.Count ? _pool.Pop() : new Entry();
            newEntry.storedObject = newObject;
            newEntry.hash = hash;
            newEntry.reference = 1;
            _cache[hash] = newEntry;
            _objectKey[newObject.GetInstanceID()] = hash;
            Release(ref obj);
            obj = newObject;
        }

        public void Release(ref T obj)
        {
            if (ReferenceEquals(obj, null)) return;

            var id = obj.GetInstanceID();
            if (_objectKey.TryGetValue(id, out var hash)
                && _cache.TryGetValue(hash, out var entry))
            {
                entry.reference--;
                if (entry.reference <= 0 || !entry.storedObject)
                {
                    Remove(entry);
                }
            }

            obj = null;
        }

        private void Remove(Entry entry)
        {
            if (ReferenceEquals(entry, null)) return;

            _cache.Remove(entry.hash);
            _objectKey.Remove(entry.storedObject.GetInstanceID());
            entry.Release(_onRelease);
            _pool.Push(entry);
        }

        private class Entry
        {
            public Hash128 hash;
            public int reference;
            public T storedObject;

            public void Release(Action<T> onRelease)
            {
                reference = 0;
                if (storedObject)
                {
                    onRelease?.Invoke(storedObject);
                }

                storedObject = null;
            }
        }
    }
}
