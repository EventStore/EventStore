// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EventStore.Core.XUnit.Tests.RateLimiting;

// attempt to implement with channels, this isn't coming out more nicely than using
// a Queue directly as in BoundedAsyncPriorityQueue
public class BoundedAsyncPriorityQueue2<T> : IBoundedAsyncPriorityQueue<T> {
	private readonly Channel<T>[] _queues = new Channel<T>[(int)Priority.Count];
	private readonly Func<T, Priority> _getPriority;

	//qq (overflow)
	public int Count {
		get {
			var count = 0;
			for (var i = 0; i < _queues.Length; i++)
				count += _queues[i].Reader.Count;
			return count;
		}
	}

	public BoundedAsyncPriorityQueue2(int capacityPerPriority, Func<T, Priority> getPriority) {
		_getPriority = getPriority;

		for (var i = 0; i < _queues.Length; i++) {
			_queues[i] = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity: capacityPerPriority) {
				AllowSynchronousContinuations = false,
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = false, //qq
				SingleWriter = false, //qq
			});
		}
	}

	public bool TryEnqueue(T item) {
		var priority = _getPriority(item);
		return _queues[(int)priority].Writer.TryWrite(item);
	}

	public bool TryPeek(out T item) {
		for (var i = 0; i < _queues.Length; i++) {
			if (_queues[i].Reader.TryPeek(out item)) {
				return true;
			}
		}

		item = default;
		return false;
	}

	// Read from the queue with the highest priority.
	public bool TryRead(out T item) {
		for (var i = 0; i < _queues.Length; i++) {
			if (_queues[i].Reader.TryRead(out item)) {
				return true;
			}
		}

		item = default;
		return false;
	}


	//qq this is the part where the channel could conceivably help vs Queue, but it seems not in the end
	//qqqq make sure to consume the valuetasks exactly once
	public async ValueTask WaitToReadAsync(CancellationToken token) {
		//qq use the struct as an array trick
		var wait0 = _queues[0].Reader.WaitToReadAsync(token);
		if (wait0.IsCompleted) {
			await wait0; //qq handle if this is false
			return;
		}

		var wait1 = _queues[1].Reader.WaitToReadAsync(token);
		if (wait1.IsCompleted) {
			//qq need to consume wait0
			await wait1; //qq handle if this is false
			return;
		}


		var wait2 = _queues[2].Reader.WaitToReadAsync(token);
		if (wait2.IsCompleted) {
			//qq need to consume wait0 and wait1
			await wait2;
			return;
		}

		// none of them are ready to read. create a task that completes when any of them are ready to read.
		//qq minimise the allocations here
		var t = await Task.WhenAny(
			wait0.AsTask(),
			wait1.AsTask(),
			wait2.AsTask());
		await t;
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
