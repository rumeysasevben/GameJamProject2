using System;
using System.Collections.Generic;
using UnityEngine;

namespace TenCandles
{
    // Minimal component pool. WebGL GC spikes hurt most with 25 enemies plus projectiles on screen.
    public class ObjectPool<T> where T : Component
    {
        readonly Func<T> create;
        readonly Stack<T> free = new Stack<T>();

        public ObjectPool(Func<T> create, int prewarm = 0)
        {
            this.create = create;
            for (int i = 0; i < prewarm; i++) Release(create());
        }

        public T Get()
        {
            T item = free.Count > 0 ? free.Pop() : create();
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            free.Push(item);
        }
    }
}
