using System;
using System.Threading;
using EventStore.Core.Index;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class IndexScavenger : IIndexScavenger {
		private readonly ITableIndex _tableIndex;

		public IndexScavenger(ITableIndex tableIndex) {
			_tableIndex = tableIndex;
		}

		public void ScavengeIndex(
			long scavengePoint,
			Action<PTable> checkSuitability,
			Func<IndexEntry, bool> shouldKeep,
			IIndexScavengerLog log,
			CancellationToken cancellationToken) {

			_tableIndex.Scavenge(checkSuitability, shouldKeep, log, cancellationToken);
		}
	}
}
