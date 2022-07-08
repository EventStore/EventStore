using System.Threading;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface ICleaner {
		void Clean(
			ScavengePoint scavengePoint,
			IScavengeStateForCleaner state,
			CancellationToken cancellationToken);

		void Clean(
			ScavengeCheckpoint.Cleaning checkpoint,
			IScavengeStateForCleaner state,
			CancellationToken cancellationToken);
	}
}
