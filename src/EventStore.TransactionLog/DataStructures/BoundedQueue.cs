﻿using System.Collections.Generic;

namespace EventStore.Core.TransactionLog.DataStructures {
	public class BoundedQueue<T> {
		private readonly int _maxCapacity;
		private readonly Queue<T> _queue;

		public int MaxCapacity {
			get { return _maxCapacity; }
		}

		public int Count {
			get { return _queue.Count; }
		}

		public BoundedQueue(int maxCapacity) {
			_queue = new Queue<T>(maxCapacity);
			_maxCapacity = maxCapacity;
		}

		public void Enqueue(T obj) {
			if (_queue.Count >= _maxCapacity) Dequeue();
			_queue.Enqueue(obj);
		}

		public T Dequeue() {
			return _queue.Dequeue();
		}

		public T Peek() {
			return _queue.Peek();
		}

		public bool CanAccept() {
			return _queue.Count < _maxCapacity;
		}
	}
}
