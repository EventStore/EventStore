using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
	public class LogNotificationTracker {
		//todo: replace with custom sorted list for performance as time alows
		private readonly SortedList<long, List<Action>> _registeredActions = new SortedList<long, List<Action>>();
		private readonly bool _isTimeLog;
		private long _logPosition;
		private long _notifying;
		private Channel<long> _positionQueue;
		private object _registerLock = new object();
		ManualResetEventSlim _cancelNotifyAt = new ManualResetEventSlim();
		Stopwatch _mainStopwatch = Stopwatch.StartNew();

		public LogNotificationTracker(bool isTimeLog) {
			var discardingQueueOptions = new BoundedChannelOptions(1);
			discardingQueueOptions.AllowSynchronousContinuations = false;
			discardingQueueOptions.FullMode = BoundedChannelFullMode.DropOldest;
			discardingQueueOptions.SingleReader = true;
			_positionQueue = Channel.CreateBounded<long>(discardingQueueOptions);
			
			_isTimeLog = isTimeLog;
			if (!isTimeLog) {
				Task.Run(() => Notify());
			}
		}
		public bool TryEnqueLogPostion(long logPosition) => _positionQueue.Writer.TryWrite(logPosition);
		private async void Notify() {
			while (true) {
				//We have a dedicated thread, it's faster to keep using it, (n.b. it's just a request to the scheduler) 
#pragma warning disable CAC001 // ConfigureAwaitChecker
				Notify(await _positionQueue.Reader.ReadAsync());
#pragma warning restore CAC001 // ConfigureAwaitChecker
			}
		}
		private void Notify(long logPosition) {
			lock (_registerLock) {
				 _notifying = 1;
				_logPosition = logPosition;
				while (!_registeredActions.Keys.IsEmpty() && _registeredActions.Keys[0] <= logPosition) {
					if (_registeredActions.Remove(_registeredActions.Keys[0], out var targets)) {
						foreach (Action target in targets) {
							if (target != null) {
								//n.b. targets should enque messages only
								try {
									target();
								} catch {
									//ignore									
								}
							}
						}
					}
				}
				if (_isTimeLog && !_registeredActions.Keys.IsEmpty()) {
					_cancelNotifyAt.Set();
					_cancelNotifyAt = new ManualResetEventSlim();
					Task.Run(() => NotifyAt(_registeredActions.Keys[0], _cancelNotifyAt));
				}
				 _notifying = 0;
			}
		}
		private void NotifyAt(long timePosition, ManualResetEventSlim cancel) {
			if (!_isTimeLog) { return; }			
			var now = _mainStopwatch.ElapsedMilliseconds;
			var delay = timePosition - now;
			if (delay > 0) {
				cancel.Wait((int)delay);
				if (cancel.IsSet) { return; }
			}
			Notify(timePosition);
		}
		public void Register(TimeSpan delay, Action target) {
			if (!_isTimeLog) { throw new InvalidOperationException("Timespan Delays can only be registered on TimeLogs"); }

			var now = _mainStopwatch.ElapsedMilliseconds;
			var position = now + (long)delay.TotalMilliseconds;
			Register(position, target);
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
					if (_notifying == 1) { return; } //we're reentering the same lock let Notify reschedule when done
					if (_isTimeLog && !_registeredActions.Keys.IsEmpty() && position == _registeredActions.Keys[0]) {
						_cancelNotifyAt.Set();
						_cancelNotifyAt = new ManualResetEventSlim();
						Task.Run(() => NotifyAt(_registeredActions.Keys[0], _cancelNotifyAt));
					}
				} else {
					actionList.Add(target);
				}
			}
		}

	}
}
