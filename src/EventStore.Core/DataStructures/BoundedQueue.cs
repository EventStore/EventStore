using System.Collections.Generic;

namespace EventStore.Core.DataStructures
{
    public class BoundedQueue<T> 
    {
        private readonly int _capacity;
        private readonly Queue<T> _queue;

        public BoundedQueue(int capacity)
        {
            _queue = new Queue<T>(capacity);
            _capacity = capacity;
        }

        public void Enqueue(T obj)
        {
            if (_queue.Count >= _capacity) Dequeue();
            _queue.Enqueue(obj);
        }

        private T Dequeue()
        {
            return _queue.Dequeue();
        }
    }
}