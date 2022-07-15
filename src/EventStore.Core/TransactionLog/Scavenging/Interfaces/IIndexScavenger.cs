using System;
using System.Threading;
using EventStore.Core.Index;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IIndexScavenger {
		void ScavengeIndex(
			long scavengePoint,
			Func<IndexEntry, bool> shouldKeep,
			IIndexScavengerLog log,
			CancellationToken cancellationToken);
	}
}
