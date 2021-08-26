using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.RequestManager {
	public class CommitSource :
	IHandle<ReplicationTrackingMessage.IndexedTo>,
	IHandle<ReplicationTrackingMessage.ReplicatedTo> {
		private LogNotificationTracker _indexTracker = new LogNotificationTracker();
		private LogNotificationTracker _replicatedTracker = new LogNotificationTracker();

		public void Handle(ReplicationTrackingMessage.ReplicatedTo @event) {
			_replicatedTracker.TryEnqueLogPostion(@event.LogPosition);
		}
		public void Handle(ReplicationTrackingMessage.IndexedTo @event) {
			_indexTracker.TryEnqueLogPostion(@event.LogPosition);
		}
		public void RegisterReplicated(long position, Action target) {
			_replicatedTracker.Register(position, target);
		}
		public void RegisterIndexed(long position, Action target) {
			_indexTracker.Register(position, target);
		}
	}
	public class LogNotificationTracker {
		private readonly Dictionary<long, List<Action>> _registeredActions = new Dictionary<long, List<Action>>();
		private long _logPosition;
		private Channel<long> _positionQueue;
		private object _registerLock = new object();

		public LogNotificationTracker() {
			var discardingQueueOptions = new BoundedChannelOptions(1);
			discardingQueueOptions.AllowSynchronousContinuations = false;
			discardingQueueOptions.FullMode = BoundedChannelFullMode.DropOldest;
			discardingQueueOptions.SingleReader = true;
			_positionQueue = Channel.CreateBounded<long>(discardingQueueOptions);
			Task.Run(() => Notify());
		}
		public bool TryEnqueLogPostion(long logPosition) => _positionQueue.Writer.TryWrite(logPosition);
		private async void Notify() {
			while (true) {
				//We have a dedicated thread, it's faster to keep using it
#pragma warning disable CAC001 // ConfigureAwaitChecker
				Notify(await _positionQueue.Reader.ReadAsync());
#pragma warning restore CAC001 // ConfigureAwaitChecker
			}
		}
		private void Notify(long logPosition) {
			lock (_registerLock) {
				_logPosition = logPosition;
				if (_registeredActions.Keys.IsEmpty()) { return; }
				foreach (long key in _registeredActions.Keys.Where(pos => pos <= logPosition)) {
					if (_registeredActions.Remove(key, out var targets)) {
						foreach (Action target in targets) {
							target();
						}
					}
				}
			}
		}

		public void Register(long position, Action target) {
			lock (_registerLock) {
				if (_logPosition >= position) {
					target();
					return;
				};
				if (!_registeredActions.TryGetValue(position, out var actionList)) {
					actionList = new List<Action> { target };
					_registeredActions.TryAdd(position, actionList);
				} else {
					actionList.Add(target);
				}
			}
		}

	}
}
