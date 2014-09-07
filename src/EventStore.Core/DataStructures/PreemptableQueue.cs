using System.Collections.Generic;

namespace EventStore.Core.DataStructures
{
    public class PreemptableQueue<T>
    {
        private readonly Queue<T> _preempt = new Queue<T>();
        private readonly Queue<T> _regular = new Queue<T>();
        public int Count { get { return _preempt.Count + _regular.Count; } }

        public void Preempt(T item)
        {
            _preempt.Enqueue(item);
        }

        public void Enqueue(T item)
        {
            _regular.Enqueue(item);
        }

        public T Dequeue()
        {
            return _preempt.Count > 0 ? _preempt.Dequeue() : _regular.Dequeue();
        }
    }
}