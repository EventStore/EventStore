using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.RequestManager {
	public class CommitSource :
	IHandle<ReplicationTrackingMessage.IndexedTo>,
	IHandle<ReplicationTrackingMessage.ReplicatedTo> {
		private LogNotificationTracker _indexTracker = new LogNotificationTracker("IndexedTracker");
		private LogNotificationTracker _replicatedTracker = new LogNotificationTracker("ReplicatedTracker");

		public void Handle(ReplicationTrackingMessage.ReplicatedTo @event) {
			_replicatedTracker.UpdateLogPosition(@event.LogPosition);
		}
		public void Handle(ReplicationTrackingMessage.IndexedTo @event) {
			_indexTracker.UpdateLogPosition(@event.LogPosition);
		}
		public Task WaitForReplication(long position, CancellationToken token) {
			return new Task(async () => {
				await _replicatedTracker.Waitfor(position).ConfigureAwait(false);
			}, token);
		}
		public Task WaitForIndexing(long position, CancellationToken token) {
			return new Task(async () => {
				await _indexTracker.Waitfor(position).ConfigureAwait(false);
			}, token);
		}

	}
}
