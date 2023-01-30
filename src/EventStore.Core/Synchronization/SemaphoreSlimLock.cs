using System;
using System.Threading;

namespace EventStore.Core.Synchronization;

public class SemaphoreSlimLock : IExclusiveLock, IDisposable {
	private readonly SemaphoreSlim _semaphoreSlim;

	public SemaphoreSlimLock() {
		_semaphoreSlim = new SemaphoreSlim(1, 1);
	}

	public bool TryAcquire() => _semaphoreSlim.Wait(TimeSpan.Zero);

	public bool TryRelease() {
		try {
			_semaphoreSlim.Release();
			return true;
		} catch (SemaphoreFullException) {
			return false;
		}
	}

	public void Dispose() {
		GC.SuppressFinalize(this);
		_semaphoreSlim?.Dispose();
	}
}
