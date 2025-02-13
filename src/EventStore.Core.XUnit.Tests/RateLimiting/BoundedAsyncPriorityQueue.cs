// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;

namespace EventStore.Core.XUnit.Tests.RateLimiting;

public interface IAsyncSource<T> {
	int Count { get; }
	bool TryPeek(out T item);
	bool TryRead(out T item);
	ValueTask WaitToReadAsync(CancellationToken token);
	ValueTask<T> ReadAsync(CancellationToken token);
}

public interface IBoundedAsyncPriorityQueue<T> : IAsyncSource<T> {
	bool TryEnqueue(T item);
}

// Bounded size priority queue that can be read asynchronously
// Items of the same priority are dealt with in FIFO order.
//qq if the high priority section is full, should we push out one of the lower priority items?
public class BoundedAsyncPriorityQueue<T> : IBoundedAsyncPriorityQueue<T> {
	readonly Queue<T>[] _queues = new Queue<T>[(int)Priority.Count];
	//qq consider manual vs auto
	// - with auto we will release one waiting reader at a time, but it'll be bad if
	//   that reader then doesn't actually read the item
	// - with manual we will release all the readers, there could be a thousand contending for one item.
	// we could use auto but also trigger it on a schedule?
	readonly AsyncAutoResetEvent _signal = new(initialState: false);
	readonly int _capacityPerPriority;
	readonly Func<T, Priority> _getPriority;

	//qq consider locking only the queue that we are using (perhaps less contention, but also perhaps
	// more locking and unlocking). measure
	private object Lock => _queues;

	public BoundedAsyncPriorityQueue(int capacityPerPriority, Func<T, Priority> getPriority) {
		_capacityPerPriority = capacityPerPriority;
		_getPriority = getPriority;

		for (var i = 0; i < _queues.Length; i++) {
			_queues[i] = new(capacityPerPriority);
		}
	}

	//qq (overflow)
	public int Count {
		get {
			lock (Lock) {
				var count = 0;
				for (var i = 0; i < _queues.Length; i++)
					count += _queues[i].Count;
				return count;
			}
		}
	}

	public bool TryEnqueue(T item) {
		var priority = _getPriority(item);
		lock (Lock) {
			var q = _queues[(int)priority];
			if (q.Count >= _capacityPerPriority) {
				return false;
			}

			q.Enqueue(item);
			return true;
		}
	}

	public bool TryPeek(out T item) {
		lock (Lock) {
			for (var i = 0; i < _queues.Length; i++) {
				if (_queues[i].TryPeek(out item))
					return true;
			}
		}

		item = default;
		return false;
	}

	public bool TryRead(out T item) {
		lock (Lock) {
			for (var i = 0; i < _queues.Length; i++) {
				if (_queues[i].TryDequeue(out item))
					return true;
			}
		}

		item = default;
		return false;
	}

	public async ValueTask WaitToReadAsync(CancellationToken token) {
		// try synchronously
		lock (Lock) {
			for (var i = 0; i < _queues.Length; i++) {
				if (_queues[i].Count >= 0)
					return;
			}
		}

		// wait asynchronously
		await _signal.WaitAsync(token); //qq does this pool?
	}

	public async ValueTask<T> ReadAsync(CancellationToken token) {
		while (true) {
			await WaitToReadAsync(token);
			if (TryRead(out var item)) {
				return item;
			}
		}
	}
}
