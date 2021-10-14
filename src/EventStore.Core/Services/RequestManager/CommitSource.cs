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

		public IDisposable NotifyOnReplicated(long position, Action target) {
			return _replicatedTracker.Register(position, target);
		}
		public IDisposable NotifyOnIndexed(long position, Action target) {
			return _indexTracker.Register(position, target);
		}
		public IDisposable NotifyAfter(TimeSpan delay, Action target) {
			return _delayTracker.Register(delay, target);
		}
	}
	public class LogNotificationTracker {
		//todo: replace with custom sorted list for performance as time alows
		private readonly SortedList<long, List<Notification>> _registeredActions = new SortedList<long, List<Notification>>();
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
						foreach (Notification target in targets) {
							//n.b. targets should enque messages only
							try {
								target.Notify();
							} catch {
								//ignore									
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
		public IDisposable Register(TimeSpan delay, Action target) {
			if (!_isTimeLog) { throw new InvalidOperationException("Timespan Delays can only be registered on TimeLogs"); }

			var now = _mainStopwatch.ElapsedMilliseconds;
			var position = now + (long)delay.TotalMilliseconds;
			return Register(position, target);
		}
		public IDisposable Register(long position, Action target) {
			lock (_registerLock) {
				if (_logPosition >= position) {
					target();
					return new Disposer(() => { });
				};
				var notification = new Notification(target);
				var disposer = new Disposer(() => { notification.Cancel(); });
				if (!_registeredActions.TryGetValue(position, out var actionList)) {					
					actionList = new List<Notification> { notification };
					_registeredActions.TryAdd(position, actionList);
					if (_notifying == 1) { return disposer; } //we're reentering the same lock let Notify reschedule when done
					if (_isTimeLog && !_registeredActions.Keys.IsEmpty() && position == _registeredActions.Keys[0]) {
						_cancelNotifyAt.Set();
						_cancelNotifyAt = new ManualResetEventSlim();
						Task.Run(() => NotifyAt(_registeredActions.Keys[0], _cancelNotifyAt));
					}
				} else {
					actionList.Add(notification);
				}
				return disposer;
			}
		}
		public struct Notification {
			private Action _target;

			public Notification(Action target) {
				_target = target;
			}
			public void Notify() {
				_target?.Invoke();
				_target = null;
			}
			public void Cancel() {
				_target = null;
			}

		}
		public class Disposer : IDisposable {
			private Action _disposeAction;

			public Disposer(Action disposeAction) {
				_disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
			}

			private bool _disposed;
			public void Dispose() {
				if (_disposed)
					return;
				try {
					_disposeAction();
					_disposeAction = null;
				} catch {
					//ignore
				}
				_disposed = true;
			}
		}
	}
}
