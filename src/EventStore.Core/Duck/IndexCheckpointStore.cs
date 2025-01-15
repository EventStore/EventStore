using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Duck.Default;
using Eventuous.Subscriptions.Checkpoints;
using Serilog;

namespace EventStore.Core.Duck;

public class IndexCheckpointStore(DefaultIndexHandler handler) : ICheckpointStore {
	static readonly ILogger Log = Serilog.Log.ForContext<IndexCheckpointStore>();

	public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
		var lastPosition = DefaultIndex.GetLastPosition();
		return ValueTask.FromResult(new Checkpoint(checkpointId, lastPosition));
	}

	public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
		while (true) {
			try {
				handler.Commit();
				Log.Information("Committing checkpoint {Checkpoint}", checkpoint);
				return checkpoint;
			} catch (Exception) {
				Log.Warning("Unable to commit {Checkpoint}, will retry", checkpoint);
				await Task.Delay(100, cancellationToken);
			}
		}
	}
}
