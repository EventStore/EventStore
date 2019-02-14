using System;
using System.Diagnostics;
using System.Net.Configuration;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Bus {
	public class QueueStatsCollector {
		private static readonly TimeSpan MinRefreshPeriod = TimeSpan.FromMilliseconds(100);

		public readonly string Name;
		public readonly string GroupName;

		public Type InProgressMessage {
			get { return _inProgressMsgType; }
		}

		private readonly object
			_statisticsLock =
				new object(); // this lock is mostly acquired from a single thread (+ rarely to get statistics), so performance penalty is not too high

		private readonly Stopwatch _busyWatch = new Stopwatch();
		private readonly Stopwatch _idleWatch = new Stopwatch();
		private readonly Stopwatch _totalIdleWatch = new Stopwatch();
		private readonly Stopwatch _totalBusyWatch = new Stopwatch();
		private readonly Stopwatch _totalTimeWatch = new Stopwatch();
		private TimeSpan _lastTotalIdleTime;
		private TimeSpan _lastTotalBusyTime;
		private TimeSpan _lastTotalTime;

		private long _totalItems;
		private long _lastTotalItems;
		private int _lifetimeQueueLengthPeak;
		private int _currentQueueLengthPeak;
		private Type _lastProcessedMsgType;
		private Type _inProgressMsgType;

		private bool _wasIdle;

#if DEBUG
		private bool _started = false; //whether the queue has been started or not
		private int _length = 0; //current number of items on the queue
		private bool
			_idleDetection =
				QueueStatsCollector
					._idleDetectionEnabled; //whether idle detection was enabled when this instance was created
#endif
		public QueueStatsCollector(string name, string groupName = null) {
			Ensure.NotNull(name, "name");

			Name = name;
			GroupName = groupName;
		}

		public void Start() {
			_totalTimeWatch.Start();
#if DEBUG
			Debug.Assert(!_started,
				string.Format("QueueStatsCollector [{0}] was already started when Start() entered", Name));
			lock (_itemsUpdateLock) {
				_started = true;
				_totalLength += _length;
			}

			if (_idleDetection) {
				lock (_notifyIdleLock) {
					_nonIdle++;
				}
			}

			if (_idleDetection) {
				lock (_notifyStopLock) {
					_totalStarted++;
				}
			}
#endif
			EnterIdle();
		}

		public void Stop() {
#if DEBUG
			Debug.Assert(_started,
				string.Format("QueueStatsCollector [{0}] was not started when Stop() entered", Name));
#endif
			EnterIdle();
			_totalTimeWatch.Stop();
#if DEBUG
			lock (_itemsUpdateLock) {
				_started = false;
				_totalLength -= _length;
				Debug.Assert(_totalLength >= 0,
					string.Format("QueueStatsCollector [{0}] _totalLength = {1} < 0", Name, _totalLength));
			}

			if (_idleDetection) {
				lock (_notifyStopLock) {
					_totalStarted--;
					Debug.Assert(_totalStarted >= 0,
						string.Format("QueueStatsCollector [{0}] _totalStarted = {1} < 0", Name, _totalStarted));
					if (_totalStarted == 0) {
						Monitor.Pulse(_notifyStopLock);
					}
				}
			}
#endif
		}

		public void ProcessingStarted<T>(int queueLength) {
			ProcessingStarted(typeof(T), queueLength);
		}

		public void ProcessingStarted(Type msgType, int queueLength) {
			_lifetimeQueueLengthPeak = _lifetimeQueueLengthPeak > queueLength ? _lifetimeQueueLengthPeak : queueLength;
			_currentQueueLengthPeak = _currentQueueLengthPeak > queueLength ? _currentQueueLengthPeak : queueLength;

			_inProgressMsgType = msgType;
		}

		public void ProcessingEnded(int itemsProcessed) {
			Interlocked.Add(ref _totalItems, itemsProcessed);
			_lastProcessedMsgType = _inProgressMsgType;
			_inProgressMsgType = null;
		}

		public void EnterIdle() {
#if DEBUG
			Debug.Assert(_started,
				string.Format("QueueStatsCollector [{0}] was not started when EnterIdle() entered", Name));
#endif
			if (_wasIdle)
				return;
			_wasIdle = true;
#if DEBUG
			if (_idleDetection) {
				lock (_notifyIdleLock) {
					_nonIdle--;
					Debug.Assert(_nonIdle >= 0,
						string.Format("QueueStatsCollector [{0}] _nonIdle = {1} < 0", Name, _nonIdle));
					if (_nonIdle == 0) {
						Monitor.Pulse(_notifyIdleLock);
					}
				}
			}
#endif

			//NOTE: the following locks are primarily acquired in main thread,
			//      so not too high performance penalty
			lock (_statisticsLock) {
				_totalIdleWatch.Start();
				_idleWatch.Restart();

				_totalBusyWatch.Stop();
				_busyWatch.Reset();
			}
		}

		public void EnterBusy() {
#if DEBUG
			Debug.Assert(_started,
				string.Format("QueueStatsCollector [{0}] was not started when EnterBusy() entered", Name));
#endif
			if (!_wasIdle)
				return;
			_wasIdle = false;

#if DEBUG
			if (_idleDetection) {
				lock (_notifyIdleLock) {
					_nonIdle++;
				}
			}
#endif

			lock (_statisticsLock) {
				_totalIdleWatch.Stop();
				_idleWatch.Reset();

				_totalBusyWatch.Start();
				_busyWatch.Restart();
			}
		}

		public QueueStats GetStatistics(int currentQueueLength) {
			lock (_statisticsLock) {
				var totalTime = _totalTimeWatch.Elapsed;
				var totalIdleTime = _totalIdleWatch.Elapsed;
				var totalBusyTime = _totalBusyWatch.Elapsed;
				var totalItems = Interlocked.Read(ref _totalItems);

				var lastRunMs = totalTime - _lastTotalTime;
				var lastItems = totalItems - _lastTotalItems;
				var avgItemsPerSecond = lastRunMs.Ticks != 0
					? (int)(TimeSpan.TicksPerSecond * lastItems / lastRunMs.Ticks)
					: 0;
				var avgProcessingTime =
					lastItems != 0 ? (totalBusyTime - _lastTotalBusyTime).TotalMilliseconds / lastItems : 0;
				var idleTimePercent = Math.Min(100.0,
					lastRunMs.Ticks != 0 ? 100.0 * (totalIdleTime - _lastTotalIdleTime).Ticks / lastRunMs.Ticks : 100);

				var stats = new QueueStats(
					Name,
					GroupName,
					currentQueueLength,
					avgItemsPerSecond,
					avgProcessingTime,
					idleTimePercent,
					_busyWatch.IsRunning ? _busyWatch.Elapsed : (TimeSpan?)null,
					_idleWatch.IsRunning ? _idleWatch.Elapsed : (TimeSpan?)null,
					totalItems,
					_currentQueueLengthPeak,
					_lifetimeQueueLengthPeak,
					_lastProcessedMsgType,
					_inProgressMsgType);

				if (totalTime - _lastTotalTime >= MinRefreshPeriod) {
					_lastTotalTime = totalTime;
					_lastTotalIdleTime = totalIdleTime;
					_lastTotalBusyTime = totalBusyTime;
					_lastTotalItems = totalItems;

					_currentQueueLengthPeak = 0;
				}

				return stats;
			}
		}

#if DEBUG
		private static object _notifyIdleLock = new object();
		private static object _notifyStopLock = new object();
		private static object _itemsUpdateLock = new object();
		private static volatile bool _idleDetectionEnabled = false;
		private static int _nonIdle = 0;
		private static ICheckpoint[] _writerCheckpoint = new ICheckpoint[3];
		private static ICheckpoint[] _chaserCheckpoint = new ICheckpoint[3];
		private static int _totalLength = 0; //sum of lengths of all active (started) queues
		private static int _totalStarted = 0; //number of active (started) queues
		public static bool DumpMessages;

		public static void InitializeIdleDetection() {
			lock (_notifyIdleLock) {
				_nonIdle = 0;
			}

			lock (_itemsUpdateLock) {
				_totalLength = 0;
			}

			lock (_notifyStopLock) {
				_totalStarted = 0;
			}

			_writerCheckpoint = new ICheckpoint[3];
			_chaserCheckpoint = new ICheckpoint[3];
			_idleDetectionEnabled = true;
		}

		public static void DisableIdleDetection() {
			WaitIdle(waitForCheckpoints: false, waitForNonEmptyTf: false);
			WaitStop();
			_idleDetectionEnabled = false;
		}

		private static void WaitStop(int multiplier = 1) {
			Debug.Assert(_idleDetectionEnabled, "_idleDetectionEnabled was false when WaitStop() entered");
			lock (_notifyStopLock) {
				var counter = 0;
				while (_totalStarted > 0) {
					if (!Monitor.Wait(_notifyStopLock, 100)) {
						Console.WriteLine("Waiting for STOP state...");
						counter++;
						if (counter > 150 * multiplier)
							throw new ApplicationException("Infinite WaitStop() loop?");
					}
				}
			}
		}
#endif

		[Conditional("DEBUG")]
		public static void WaitIdle(bool waitForCheckpoints = true, bool waitForNonEmptyTf = false,
			int multiplier = 1) {
#if DEBUG
			Debug.Assert(_idleDetectionEnabled, "_idleDetectionEnabled was false when WaitIdle() entered");
			var counter = 0;
			lock (_notifyIdleLock) {
				var successes = 0;
				while (successes < 2) {
					while (_nonIdle > 0 || _totalLength > 0 ||
					       (waitForCheckpoints && (AreCheckpointsDifferent(0) || AreCheckpointsDifferent(1)
					                                                          || AreCheckpointsDifferent(2) ||
					                                                          AnyCheckpointsDifferent()))
					       || (waitForNonEmptyTf && _writerCheckpoint[0].Read() == 0)) {
						if (!Monitor.Wait(_notifyIdleLock, 100)) {
							Console.WriteLine("Waiting for IDLE state...");
							counter++;
							if (counter > 150 * multiplier)
								throw new ApplicationException("Infinite WaitIdle() loop?");
						}
					}

					Thread.Sleep(10);
					successes++;
				}
			}
#endif
		}

#if DEBUG
		private static bool AreCheckpointsDifferent(int index) {
			return _writerCheckpoint[index] != null && _chaserCheckpoint[index] != null
			                                        && _writerCheckpoint[index].ReadNonFlushed() !=
			                                        _chaserCheckpoint[index].Read();
		}

		private static bool AnyCheckpointsDifferent() {
			long c1 = _writerCheckpoint[0] != null ? _writerCheckpoint[0].ReadNonFlushed() : -1;
			long c2 = _writerCheckpoint[1] != null ? _writerCheckpoint[1].ReadNonFlushed() : -1;
			long c3 = _writerCheckpoint[2] != null ? _writerCheckpoint[2].ReadNonFlushed() : -1;

			return (c2 != -1 && c1 != c2) || (c2 != -1 && c3 != -1 && c2 != c3);
		}

		public static void InitializeCheckpoints(int index, ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint) {
			if (index == -1) {
				index = 0;
				_chaserCheckpoint[1] = _chaserCheckpoint[2] = null;
				_writerCheckpoint[1] = _writerCheckpoint[2] = null;
			}

			_chaserCheckpoint[index] = chaserCheckpoint;
			_writerCheckpoint[index] = writerCheckpoint;
		}
#endif

		[Conditional("DEBUG")]
		public void Enqueued() {
#if DEBUG
			lock (_itemsUpdateLock) {
				_length++;
				if (_started) {
					//if the queue is stopped, do not increment _totalLength
					//This is particularly important for idle detection in WaitIdle() since items published on a stopped queue may never be dequeued and WaitIdle() will wait indefinitely.
					//If ever the queue is started again, _length will be added back to _totalLength.
					_totalLength++;
				}
			}
#endif
		}

		[Conditional("DEBUG")]
		public void Dequeued(Message msg) {
#if DEBUG
			Debug.Assert(_started,
				string.Format("QueueStatsCollector [{0}] was not started when Dequeued() entered", Name));
			lock (_itemsUpdateLock) {
				if (_started) {
					_length--;
					_totalLength--;
					Debug.Assert(_length >= 0,
						string.Format("QueueStatsCollector [{0}] _length = {1} < 0", Name, _length));
					Debug.Assert(_totalLength >= 0,
						string.Format("QueueStatsCollector [{0}] _totalLength = {1} < 0", Name, _totalLength));
				}
			}

			if (DumpMessages) {
				Console.WriteLine(msg.GetType().Namespace + "." + msg.GetType().Name);
			}
#endif
		}
	}
}
