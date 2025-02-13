// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using DotNext.Runtime.CompilerServices;

namespace EventStore.Core.XUnit.Tests.RateLimiting;

// combines different sources
//qq this could be 'partition' rather than 'source' and have Source be a type parameter
public class ConcurrentConsumerBySource<T> {
	private readonly BoundedAsyncPriorityQueue<T>[] _queues = new BoundedAsyncPriorityQueue<T>[(int)Source.Count];
	private readonly ConcurrentConsumer<T>[] _consumers = new ConcurrentConsumer<T>[(int)Source.Count];
	private readonly Func<T, Source> _getSource;

	public ConcurrentConsumerBySource(
		int concurrencyLimit,
		Func<T, Source> getSource,
		Func<T, Priority> getPriority,
		Func<T, CancellationToken, ValueTask> process) {

		_getSource = getSource;

		for (var i = 0; i < _consumers.Length; i++) {
			_queues[i] = new BoundedAsyncPriorityQueue<T>(capacityPerPriority: 50, getPriority);
			_consumers[i] = new(_queues[i], concurrencyLimit, process); //qq wants to be different concurrentylimit per source
		}
	}

	public void Run(CancellationToken token) {
		foreach (var source in _consumers)
			source.Run(token);
	}

	public bool TryEnqueue(T item) {
		return _queues[(int)_getSource(item)].TryEnqueue(item);
	}
}

// Consumes items from an IAsyncSource concurrently according to the concurrentLimit
// the concurrentLimit is respected by spawning the right amount of readers, each reading from the source
//qq make the limit dynamically adjustable
public class ConcurrentConsumer<T> {
	private readonly Reader[] _readers;

	public ConcurrentConsumer(
		IAsyncSource<T> itemSource,
		int concurrencyLimit,
		Func<T, CancellationToken, ValueTask> process) {

		_readers = new Reader[concurrencyLimit];
		for (var i = 0; i < _readers.Length; i++)
			_readers[i] = new Reader(itemSource, process);
	}

	public Task Run(CancellationToken token) {
		foreach (var reader in _readers)
			_ = reader.Run(token);
		//qq better to return a task that completes when we've stopped running
		// bear in mind dynamically adjusting the reader count
		return Task.CompletedTask;
	}

	class Reader(
		IAsyncSource<T> itemSource,
		Func<T, CancellationToken, ValueTask> process) {

		public async Task Run(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				var item = await itemSource.ReadAsync(token);
				await process(item, token);
			}
		}
	}
}

// this implementation has one logical process that pumps the IAsyncSource (rather than many readers)
// and controls concurrency by acquiring a lease from a regular ConcurrencyLimiter with QueueLimit 1
//   note that here we are after prioritisation.
// a downside is that by the time we acquire the lease the single item we have may not be the highest
// priority any more.
//qq make the limit dynamically adjustable (awkward, we need to recreate the ConcurrencyLimiter i think)
public class ConcurrentConsumer2<T> {
	private readonly IAsyncSource<T> _itemSource;
	private readonly Func<T, CancellationToken, ValueTask> _process;
	private readonly RateLimiter _limiter;

	public ConcurrentConsumer2(
		IAsyncSource<T> itemSource,
		int concurrencyLimit,
		Func<T, CancellationToken, ValueTask> process) {

		_limiter = new ConcurrencyLimiter(new() {
			PermitLimit = concurrencyLimit,
			QueueLimit = 1,
		});
		_itemSource = itemSource;
		_process = process;
	}

	public async Task Run(CancellationToken token) {
		while (!token.IsCancellationRequested) {
			var item = await _itemSource.ReadAsync(token);
			var lease = await _limiter.AcquireAsync(permitCount: 1, token);
			_ = Process(item, lease, token);
		}
	}

	//qq can this builder be used with ValueTask
	//qq can we use a ivaluetasksource to avoid the allocations
	// we would need some way to consume the valuetasks, bit of a faff
	[AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
	async Task Process(T item, RateLimitLease lease, CancellationToken token) {
		using var _ = lease;
		await _process(item, token);
	}
}
