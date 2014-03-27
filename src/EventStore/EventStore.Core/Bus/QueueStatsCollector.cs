using System;
using System.Diagnostics;
using System.Net.Configuration;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Bus
{
    public class QueueStatsCollector
    {
        private static readonly TimeSpan MinRefreshPeriod = TimeSpan.FromMilliseconds(100);

        public readonly string Name;
        public readonly string GroupName;

        public Type InProgressMessage { get { return _inProgressMsgType; } }

#if DEBUG        
        public static int NonIdle
        {
            get { return _nonIdle; }
        }
#endif
        private readonly object _statisticsLock = new object(); // this lock is mostly acquired from a single thread (+ rarely to get statistics), so performance penalty is not too high
        
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

        public QueueStatsCollector(string name, string groupName = null)
        {
            Ensure.NotNull(name, "name");

            Name = name;
            GroupName = groupName;
        }

        public void Start()
        {
            _totalTimeWatch.Start();
#if DEBUG
            if (_notifyLock != null)
            {
                lock (_notifyLock)
                {
                    _nonIdle++;
                }
            }
#endif
            EnterIdle();
        }

        public void Stop()
        {
            EnterIdle();
            _totalTimeWatch.Stop();
        }

        public void ProcessingStarted<T>(int queueLength)
        {
            ProcessingStarted(typeof(T), queueLength);
        }

        public void ProcessingStarted(Type msgType, int queueLength)
        {
            _lifetimeQueueLengthPeak = _lifetimeQueueLengthPeak > queueLength ? _lifetimeQueueLengthPeak : queueLength;
            _currentQueueLengthPeak = _currentQueueLengthPeak > queueLength ? _currentQueueLengthPeak : queueLength;

            _inProgressMsgType = msgType;
        }

        public void ProcessingEnded(int itemsProcessed)
        {
            Interlocked.Add(ref _totalItems, itemsProcessed);
            _lastProcessedMsgType = _inProgressMsgType;
            _inProgressMsgType = null;
        }

        public void EnterIdle()
        {
            if (_wasIdle)
                return;
            _wasIdle = true;
#if DEBUG
            if (_notifyLock != null)
            {
                lock (_notifyLock)
                {
                    _nonIdle = NonIdle - 1;
                    if (NonIdle == 0)
                    {
                        Monitor.Pulse(_notifyLock);
                    }
                }
            }
#endif

            //NOTE: the following locks are primarily acquired in main thread, 
            //      so not too high performance penalty
            lock (_statisticsLock)
            {
                _totalIdleWatch.Start();
                _idleWatch.Restart();

                _totalBusyWatch.Stop();
                _busyWatch.Reset();
            }
        }

        public void EnterBusy()
        {
            if (!_wasIdle)
                return;
            _wasIdle = false;

#if DEBUG
            if (_notifyLock != null)
            {
                lock (_notifyLock)
                {
                    _nonIdle = NonIdle + 1;
                }
            }
#endif

            lock (_statisticsLock)
            {
                _totalIdleWatch.Stop();
                _idleWatch.Reset();

                _totalBusyWatch.Start();
                _busyWatch.Restart();
            }
        }

        public QueueStats GetStatistics(int currentQueueLength)
        {
            lock (_statisticsLock)
            {
                var totalTime = _totalTimeWatch.Elapsed;
                var totalIdleTime = _totalIdleWatch.Elapsed;
                var totalBusyTime = _totalBusyWatch.Elapsed;
                var totalItems = Interlocked.Read(ref _totalItems);

                var lastRunMs = totalTime - _lastTotalTime;
                var lastItems = totalItems - _lastTotalItems;
                var avgItemsPerSecond = lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * lastItems / lastRunMs.Ticks) : 0;
                var avgProcessingTime = lastItems != 0 ? (totalBusyTime - _lastTotalBusyTime).TotalMilliseconds / lastItems : 0;
                var idleTimePercent = Math.Min(100.0, lastRunMs.Ticks != 0 ? 100.0 * (totalIdleTime - _lastTotalIdleTime).Ticks / lastRunMs.Ticks : 100);

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

                if (totalTime - _lastTotalTime >= MinRefreshPeriod)
                {
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
        private static object _notifyLock;
        private static int _nonIdle = 0;
        private static ICheckpoint[] _writerCheckpoint = new ICheckpoint[3];
        private static ICheckpoint[] _chaserCheckpoint = new ICheckpoint[3];
        private static int _length;

        public static void InitializeIdleDetection(bool enable = true)
        {
            if (enable)
            {
                _nonIdle = 0;
                _length = 0;
                _notifyLock = new object();
                _writerCheckpoint = new ICheckpoint[3];
                _chaserCheckpoint = new ICheckpoint[3];
            }
            else
            {
                _notifyLock = null;
            }
        }

#endif

        [Conditional("DEBUG")]
        public static void WaitIdle(bool waitForNonEmptyTf = false)
        {
#if DEBUG
            var counter = 0;
            lock (_notifyLock)
            {
                while (_nonIdle > 0 || _length > 0 || AreCheckpointsDifferent(0) || AreCheckpointsDifferent(1)
                       || AreCheckpointsDifferent(2) || AnyCheckpointsDifferent()
                       || (waitForNonEmptyTf && _writerCheckpoint[0].Read() == 0))
                {
                    if (!Monitor.Wait(_notifyLock, 100))
                    {
                        Console.WriteLine("Waiting for IDLE state...");
                        counter++;
                        if (counter > 50)
                            throw new ApplicationException("Infinite loop?");
                    }
                }
            }
#endif
        }

#if DEBUG
        private static bool AreCheckpointsDifferent(int index)
        {
            return _writerCheckpoint[index] != null && _chaserCheckpoint[index] != null
                   && _writerCheckpoint[index].Read() != _chaserCheckpoint[index].Read();
        }

        private static bool AnyCheckpointsDifferent()
        {
            long c1 = _writerCheckpoint[0] != null ? _writerCheckpoint[0].Read() : -1;
            long c2 = _writerCheckpoint[1] != null ? _writerCheckpoint[1].Read() : -1;
            long c3 = _writerCheckpoint[2] != null ? _writerCheckpoint[2].Read() : -1;

            return (c2 != -1 && c1 != c2) || (c2 != -1 && c3 != -1 && c2 != c3);
        }

        public static void InitializeCheckpoints(int index, ICheckpoint writerCheckpoint, ICheckpoint chaserCheckpoint)
        {
            if (index == -1)
            {
                index = 0;
                _chaserCheckpoint[1] = _chaserCheckpoint[2] = null;
                _writerCheckpoint[1] = _writerCheckpoint[2] = null;
            }
            _chaserCheckpoint[index] = chaserCheckpoint;
            _writerCheckpoint[index] = writerCheckpoint;
        }
#endif

        [Conditional("DEBUG")]
        public void Enqueued()
        {
#if DEBUG
            Interlocked.Increment(ref _length);
#endif
        }

        [Conditional("DEBUG")]
        public void Dequeued()
        {
#if DEBUG            
            Interlocked.Decrement(ref _length);
#endif
        }
    }    
}

