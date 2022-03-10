using System;
using System.Collections.Concurrent;
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.RequestManager {
	public class CommitSource :
	IHandle<ReplicationTrackingMessage.IndexedTo>,
	IHandle<ReplicationTrackingMessage.ReplicatedTo> {
		private LogNotificationTracker _indexTracker = new LogNotificationTracker(false);
		private LogNotificationTracker _replicatedTracker = new LogNotificationTracker(false);
		private LogNotificationTracker _delayTracker = new LogNotificationTracker(true);

		public void Handle(ReplicationTrackingMessage.ReplicatedTo @event) {
			_replicatedTracker.TryEnqueLogPostion(@event.LogPosition);
		}
		public void Handle(ReplicationTrackingMessage.IndexedTo @event) {
			_indexTracker.TryEnqueLogPostion(@event.LogPosition);
		}

		public void NotifyOnReplicated(long position, Action target) {
			_replicatedTracker.Register(position, target);
		}
		public void NotifyOnIndexed(long position, Action target) {
			_indexTracker.Register(position, target);
		}
		public void NotifyAfter(TimeSpan delay, Action target) {
			_delayTracker.Register(delay, target);
		}
	}
}
