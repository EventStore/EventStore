namespace EventStore.Core.Tests.Infrastructure {
	public interface IRandTestFinishCondition {
		bool Done { get; }
		bool Success { get; }

		void Process(int iteration, RandTestQueueItem item);
		void Log();
	}
}
