using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MorseL.Common
{
    /**
     * Based off a modified (mostly c/p) version of
     * https://stackoverflow.com/a/3719378/110762
     */
    public class LruCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<LruCacheItem<TKey, TValue>>> _cacheMap = new Dictionary<TKey, LinkedListNode<LruCacheItem<TKey, TValue>>>();
        private readonly LinkedList<LruCacheItem<TKey, TValue>> _lruList = new LinkedList<LruCacheItem<TKey, TValue>>();

        public LruCache(int capacity)
        {
            this._capacity = capacity;
        }

        public TValue Get(TKey key)
        {
            lock (this)
            {
                LinkedListNode<LruCacheItem<TKey, TValue>> node;
                if (_cacheMap.TryGetValue(key, out node))
                {
                    TValue value = node.Value.Value;
                    _lruList.Remove(node);
                    _lruList.AddLast(node);
                    return value;
                }
                return default(TValue);
            }
        }

        public void Add(TKey key, TValue val)
        {
            lock (this)
            {
                if (_cacheMap.Count >= _capacity)
                {
                    RemoveFirst();
                }

                var cacheItem = new LruCacheItem<TKey, TValue>(key, val);
                var node = new LinkedListNode<LruCacheItem<TKey, TValue>>(cacheItem);
                _lruList.AddLast(node);
                _cacheMap.Add(key, node);
            }
        }

        public bool ContainsKey(TKey key)
        {
            lock (this)
            {
                return _cacheMap.ContainsKey(key);
            }
        }

        private void RemoveFirst()
        {
            // Remove from LRUPriority
            var node = _lruList.First;
            _lruList.RemoveFirst();

            // Remove from cache
            _cacheMap.Remove(node.Value.Key);
        }
    }

    class LruCacheItem<TKey, TValue>
    {
        public LruCacheItem(TKey k, TValue v)
        {
            Key = k;
            Value = v;
        }
        public TKey Key;
        public TValue Value;
    }
}