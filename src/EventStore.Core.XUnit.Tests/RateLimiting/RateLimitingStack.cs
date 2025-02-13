// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace EventStore.Core.XUnit.Tests.RateLimiting;

// combines multiple PriorityConcurrencyLimiters, one for each source.
// this could be written the same as DefaultPartitionedRateLimiter except that it wraps
// PartitionedRateLimiters instead of RateLimiters
public class PriorityBySourceLimiter<TResource> : PartitionedRateLimiter<TResource> {
	private readonly PriorityConcurrencyLimiter[] _sources = new PriorityConcurrencyLimiter[(int)Source.Count];
	private readonly Func<TResource, Source> _getSource;
	private readonly Func<TResource, Priority> _getPriority;

	public PriorityBySourceLimiter(
		int capacityPerPriority,
		int concurrencyLimit,
		Func<TResource, Source> getSource,
		Func<TResource, Priority> getPriority) {

		for (var i = 0; i < _sources.Length; i++) {
			//qq concurrencyLimit wants to be different per source and dynamically changeable
			_sources[i] = new(capacityPerPriority, concurrencyLimit);
		}

		_getSource = getSource;
		_getPriority = getPriority;
	}

	public override RateLimiterStatistics GetStatistics(TResource resource) =>
		GetRateLimiter(resource).GetStatistics(_getPriority(resource));

	protected override ValueTask<RateLimitLease> AcquireAsyncCore(TResource resource, int permitCount, CancellationToken cancellationToken) =>
		GetRateLimiter(resource).AcquireAsync(_getPriority(resource), permitCount, cancellationToken);

	protected override RateLimitLease AttemptAcquireCore(TResource resource, int permitCount) =>
		GetRateLimiter(resource).AttemptAcquire(_getPriority(resource), permitCount);

	private PriorityConcurrencyLimiter GetRateLimiter(TResource resource) {
		var source = _getSource(resource);
		return _sources[(int)source];
	}
}

// Partitions by priority for the queueing, but shares the pool of leases between all the partitions.
// it is important that we respect FIFO order of requests with the same priority
public class PriorityConcurrencyLimiter : PartitionedRateLimiter<Priority> {
	private readonly IBoundedAsyncPriorityQueue<AcquisitionRequest> _queue;
	private int _poolSize;
	private int _permitsInUse;

	//qq we are quite granular about acquiring this lock, perhaps we should acquire it more coarsely
	// perhaps we should consider passing it in to the queue so it can use the same synchronization object
	// or the priority queue maybe needn't be thread safe
	private object Lock => _queue;

	public PriorityConcurrencyLimiter(int capacityPerPriority, int concurrencyLimit) {
		_poolSize = concurrencyLimit;
		_queue = new BoundedAsyncPriorityQueue<AcquisitionRequest>(
			capacityPerPriority: capacityPerPriority,
			x => x.Priority);
	}

	public override RateLimiterStatistics GetStatistics(Priority priority) =>
		throw new NotImplementedException();

	private void Release(int permitCount) {
		lock (Lock) {
			_permitsInUse -= permitCount;
			// we have just freed up some permits, maybe the next request can be served
			PumpQueue();
		}
	}

	protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
		Priority priority,
		int permitsRequested,
		CancellationToken cancellationToken) {

		if (TryLease(permitsRequested)) {
			// the permits were already available
			return Lease.CreateAcquired(this, permitsRequested);
		} else {
			// the permits were not available, perhaps we can queue for them
			//qq if the acquisitionrequest does include allocated a tcs, avoid doing this if the queue is full.
			var acquisitionRequest = new AcquisitionRequest(permitsRequested, priority);
			if (_queue.TryEnqueue(acquisitionRequest)) {
				//qq we have just put a request in the queue, it can be immediately serviceable
				// e.g. if we are a high priority request for 1 permit, and the previous head of the queue
				// was a medium priority request for 3 tokens with only 2 available.
				//qq it might be better to detect that synchronously.
				PumpQueue();
				await acquisitionRequest.Task;
				return Lease.CreateAcquired(this, permitsRequested);
			} else {
				// the queue is full
				return Lease.NotAcquired;
			}
		}
	}

	protected override RateLimitLease AttemptAcquireCore(Priority priority, int permitsRequested) {
		return TryLease(permitsRequested)
			? Lease.CreateAcquired(this, permitsRequested)
			: Lease.NotAcquired;
	}

	private void PumpQueue() {
		lock (Lock) {
			if (_queue.TryPeek(out var acquisitionRequest)) {
				if (TryLeaseNoQueueCheck(acquisitionRequest.PermitCount)) {
					_queue.TryRead(out _);
					acquisitionRequest.Complete();
				}
			} else {
				// no requests in queue, nothing to do
			}
		}
	}

	private bool TryLease(int permitsRequested) {
		lock (Lock) {
			if (_queue.Count is 0) {
				return TryLeaseNoQueueCheck(permitsRequested);
			} else {
				// queue is not empty, there might be permits available but not enough
				// for the head of the queue. we can't take them.
			}
			return false;
		}
	}

	private bool TryLeaseNoQueueCheck(int permitsRequested) {
		lock (Lock) {
			var permitsWouldBeInUse = _permitsInUse + permitsRequested;
			if (permitsWouldBeInUse <= _poolSize) {
				_permitsInUse = permitsWouldBeInUse;
				return true;
			}
			return false;
		}
	}

	//qq can we do it more efficiently than this? IValueTaskSource?
	readonly record struct AcquisitionRequest(int PermitCount, Priority Priority) {
		private readonly TaskCompletionSource _tcs = new();
		public Task Task => _tcs.Task;
		public void Complete() => _tcs.TrySetResult();
	}

	class Lease : RateLimitLease {
		public static Lease NotAcquired = new(false, null, 0);

		private readonly PriorityConcurrencyLimiter _limiter;
		private readonly int _permitCount;
		private bool _disposed;

		public static Lease CreateAcquired(PriorityConcurrencyLimiter limiter, int permitCount) =>
			new(true, limiter, permitCount);

		private Lease(bool isAcquired, PriorityConcurrencyLimiter limiter, int permitCount) {
			IsAcquired = isAcquired;
			_limiter = limiter;
			_permitCount = permitCount;
		}

		public override bool IsAcquired { get; }

		public override IEnumerable<string> MetadataNames => [];

		public override bool TryGetMetadata(string metadataName, out object metadata) {
			metadata = default;
			return false;
		}

		protected override void Dispose(bool disposing) {
			if (_disposed)
				return;

			_disposed = true;

			_limiter?.Release(_permitCount);
		}
	}
}
