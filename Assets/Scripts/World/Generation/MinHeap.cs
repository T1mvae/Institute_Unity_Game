using System;
using System.Collections.Generic;

namespace Institute.World
{
    /// <summary>
    /// Minimal binary min-heap. Unity's netstandard2.1 profile has no System PriorityQueue,
    /// so the weighted region-growth (Dijkstra-like) step uses this.
    /// </summary>
    public class MinHeap<T>
    {
        readonly List<T> _items = new List<T>();
        readonly List<float> _priorities = new List<float>();

        public int Count => _items.Count;

        public void Push(T item, float priority)
        {
            _items.Add(item);
            _priorities.Add(priority);
            int i = _items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_priorities[parent] <= _priorities[i]) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public T Pop()
        {
            if (_items.Count == 0) throw new InvalidOperationException("Heap empty");
            T top = _items[0];
            int last = _items.Count - 1;
            _items[0] = _items[last];
            _priorities[0] = _priorities[last];
            _items.RemoveAt(last);
            _priorities.RemoveAt(last);

            int i = 0;
            int n = _items.Count;
            while (true)
            {
                int l = 2 * i + 1, rr = 2 * i + 2, smallest = i;
                if (l < n && _priorities[l] < _priorities[smallest]) smallest = l;
                if (rr < n && _priorities[rr] < _priorities[smallest]) smallest = rr;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
            return top;
        }

        void Swap(int a, int b)
        {
            (_items[a], _items[b]) = (_items[b], _items[a]);
            (_priorities[a], _priorities[b]) = (_priorities[b], _priorities[a]);
        }
    }
}
