namespace EventStore.Core.Synchronization;

public interface IExclusiveLock {
	bool TryAcquire();
	bool TryRelease();
}
