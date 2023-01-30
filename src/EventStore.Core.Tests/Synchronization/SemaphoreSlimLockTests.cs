using EventStore.Core.Synchronization;
using NUnit.Framework;

namespace EventStore.Core.Tests.Synchronization;

[TestFixture]
public class SemaphoreSlimLockTests {
	private SemaphoreSlimLock _semaphoreSlimLock;

	[SetUp]
	public void SetUp() => _semaphoreSlimLock = new();

	[TearDown]
	public void TearDown() => _semaphoreSlimLock?.Dispose();

	[Test]
	public void can_lock() {
		Assert.True(_semaphoreSlimLock.TryAcquire());
	}

	[Test]
	public void can_unlock() {
		Assert.True(_semaphoreSlimLock.TryAcquire());
		Assert.True(_semaphoreSlimLock.TryRelease());
	}

	[Test]
	public void cannot_lock_when_locked() {
		Assert.True(_semaphoreSlimLock.TryAcquire());
		Assert.False(_semaphoreSlimLock.TryAcquire());
	}

	[Test]
	public void cannot_unlock_when_unlocked() {
		Assert.False(_semaphoreSlimLock.TryRelease());
	}
}
