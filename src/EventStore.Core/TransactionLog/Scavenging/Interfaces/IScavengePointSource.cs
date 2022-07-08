using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IScavengePointSource {
		// returns null when no scavenge point
		Task<ScavengePoint> GetLatestScavengePointOrDefaultAsync(CancellationToken cancellationToken);

		Task<ScavengePoint> AddScavengePointAsync(
			long expectedVersion,
			int threshold,
			CancellationToken cancellationToken);
	}
}
