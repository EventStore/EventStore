using System;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Bus
{
    /// <summary>
    /// Lightweight in-memory queue with a separate thread in which it passes messages
    /// to the consumer. It also tracks statistics about the message processing to help
    /// in identifying bottlenecks
    /// </summary>
    public class QueuedHandlerThreadPool : IQueuedHandler, IMonitoredQueue, IThreadSafePublisher
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<QueuedHandlerThreadPool>();

        public int MessageCount { get { return _queue.Count; } }
        public string Name { get { return _queueStats.Name; } }

        private readonly IHandle<Message> _consumer;

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private readonly Common.Concurrent.ConcurrentQueue<Message> _queue = new Common.Concurrent.ConcurrentQueue<Message>();

        private volatile bool _stop;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
        private readonly TimeSpan _threadStopWaitTimeout;

        // monitoring
        private readonly QueueMonitor _queueMonitor;
        private readonly QueueStatsCollector _queueStats;

        private int _isRunning;

        public QueuedHandlerThreadPool(IHandle<Message> consumer,
                                       string name,
                                       bool watchSlowMsg = true,
                                       TimeSpan? slowMsgThreshold = null,
                                       TimeSpan? threadStopWaitTimeout = null,
                                       string groupName = null)
        {
            Ensure.NotNull(consumer, "consumer");
            Ensure.NotNull(name, "name");

            _consumer = consumer;

            _watchSlowMsg = watchSlowMsg;
            _slowMsgThreshold = slowMsgThreshold ?? InMemoryBus.DefaultSlowMessageThreshold;
            _threadStopWaitTimeout = threadStopWaitTimeout ?? QueuedHandler.DefaultStopWaitTimeout;

            _queueMonitor = QueueMonitor.Default;
            _queueStats = new QueueStatsCollector(name, groupName);
        }

        public void Start()
        {
            _queueStats.Start();
            _queueMonitor.Register(this);
        }

        public void Stop()
        {
            _stop = true;
            if (!_stopped.Wait(_threadStopWaitTimeout))
                throw new TimeoutException(string.Format("Unable to stop thread '{0}'.", Name));
            _queueMonitor.Unregister(this);
        }

        public void RequestStop()
        {
            _stop = true;
            _queueMonitor.Unregister(this);
        }

        private void ReadFromQueue(object o)
        {
            bool proceed = true;
            while (proceed)
            {
                _stopped.Reset();
                _queueStats.EnterBusy();

                Message msg;
                while (!_stop && _queue.TryDequeue(out msg))
                {
#if DEBUG
                    _queueStats.Dequeued(msg);
#endif
                    try
                    {
                        var queueCnt = _queue.Count;
                        _queueStats.ProcessingStarted(msg.GetType(), queueCnt);

                        if (_watchSlowMsg)
                        {
                            var start = DateTime.UtcNow;

                            _consumer.Handle(msg);

                            var elapsed = DateTime.UtcNow - start;
                            if (elapsed > _slowMsgThreshold)
                            {
                                Log.Trace("SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
                                          _queueStats.Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, queueCnt, _queue.Count);
                                if (elapsed > QueuedHandler.VerySlowMsgThreshold && !(msg is SystemMessage.SystemInit))
                                    Log.Error("---!!! VERY SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
                                              _queueStats.Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, queueCnt, _queue.Count);
                            }
                        }
                        else
                        {
                            _consumer.Handle(msg);
                        }

                        _queueStats.ProcessingEnded(1);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException(ex, "Error while processing message {0} in queued handler '{1}'.", msg, _queueStats.Name);
                    }
                }

                _queueStats.EnterIdle();
                _stopped.Set();

                Interlocked.CompareExchange(ref _isRunning, 0, 1);
                // try to reacquire lock if needed
                proceed = !_stop && _queue.Count > 0 && Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0; 
            }
        }

        public void Publish(Message message)
        {
            //Ensure.NotNull(message, "message");
#if DEBUG
            _queueStats.Enqueued();
#endif
            _queue.Enqueue(message);
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(ReadFromQueue);
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        public QueueStats GetStatistics()
        {
            return _queueStats.GetStatistics(_queue.Count);
        }
    }
}

