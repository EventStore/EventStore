using System;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;
using System.Threading.Channels;

namespace EventStore.Core.Bus {
	/// <summary>
	/// Channels based queue which passes messages
	/// to the consumer. It also tracks statistics about the message processing to help
	/// in identifying bottlenecks
	/// </summary>
	public class QueuedHandlerChannel : IQueuedHandler, IHandle<Message>, IPublisher, IMonitoredQueue,
		IThreadSafePublisher {
		private static readonly ILogger Log = Serilog.Log.ForContext<QueuedHandlerChannel>();
		private Channel<Message> _channel;
		public int MessageCount {
			get { return _channel.Reader.Count; }
		}

		public string Name {
			get { return _queueStats.Name; }
		}

		public IHandle<Message> Consumer {
			set {
				if (_consumer != null) { throw new InvalidOperationException("Cannot change consumer once set."); }
				_consumer = value;
			}
		}

		private IHandle<Message> _consumer;
		private readonly bool _watchSlowMsg;
		private readonly TimeSpan _slowMsgThreshold;
		private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
		private readonly TimeSpan _stopWaitTimeout;

		private long _queueDepth;
		private long _previousQueueDepth;

		private readonly QueueMonitor _queueMonitor;
		private readonly QueueStatsCollector _queueStats;
		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();
		private CancellationTokenSource _tokenSource;


		public QueuedHandlerChannel(
			IHandle<Message> consumer,
			string name,
			QueueStatsManager queueStatsManager,
			bool watchSlowMsg = true,
			TimeSpan? slowMsgThreshold = null,
			TimeSpan? stopWaitTimeout = null,
			string groupName = null) {
			Ensure.NotNull(consumer, "consumer");
			Ensure.NotNull(name, "name");
				
			_consumer = consumer;
			_watchSlowMsg = watchSlowMsg;
			_slowMsgThreshold = slowMsgThreshold ?? InMemoryBus.DefaultSlowMessageThreshold;
			_stopWaitTimeout = stopWaitTimeout ?? QueuedHandler.DefaultStopWaitTimeout;
			_channel = Channel.CreateUnbounded<Message>();
			_queueMonitor = QueueMonitor.Default;
			_queueStats = queueStatsManager.CreateQueueStatsCollector(name, groupName);		
		}

		

		public Task Start() {
			if (!_stopped.IsSet ) { throw new InvalidOperationException("Channel already running."); };
			if (_consumer == null) {throw  new InvalidOperationException("Consumer not set, unable to start Queue."); }
			_queueMonitor.Register(this);
			
			_stopped.Reset();
			_tokenSource = new CancellationTokenSource();
			Task.Run(() => ReadFromQueue(_tokenSource.Token));
			return _tcs.Task;
		}

		public void Stop() {
			_tokenSource?.Cancel();
			if (!_stopped.Wait(_stopWaitTimeout))
				throw new TimeoutException(string.Format("Unable to stop channel '{0}'.", Name));
		}

		public void RequestStop() {
			_tokenSource.Cancel();
		}

		private async void ReadFromQueue(CancellationToken cancelToken) {			
			try {
				_queueStats.Start();
				var messages = _channel.Reader.ReadAllAsync(cancelToken);
				await foreach (Message msg in messages.WithCancellation(cancelToken).ConfigureAwait(false)) {
					try {
						_previousQueueDepth = Interlocked.Decrement(ref _queueDepth);						
						_queueStats.EnterBusy();
#if DEBUG
						_queueStats.Dequeued(msg);
#endif
						_queueStats.ProcessingStarted(msg.GetType(), (int)_previousQueueDepth);
						var start = DateTime.UtcNow;

						_consumer.Handle(msg);						
						
						Report(msg, start);
						_queueStats.ProcessingEnded(1);
						if (_queueDepth < 1) { _queueStats.EnterIdle(); }

					} catch (Exception ex) {
						if (cancelToken.IsCancellationRequested) { break; }
						Log.Error(ex, "Error while processing message {message} in queued handler '{queue}'.",
							msg, Name);
#if DEBUG
						throw;
#endif
					}
				}

			} catch (Exception ex) {
				if (!cancelToken.IsCancellationRequested) {
					_tcs.TrySetException(ex);
					throw;
				}
			} finally {
				_channel.Writer.TryComplete();
				_stopped.Set();
				_queueStats.Stop();
				_queueMonitor.Unregister(this);
			}
		}

		private void Report(Message msg, DateTime start) {
			if(!_watchSlowMsg) return;
			var elapsed = DateTime.UtcNow - start;
			if (elapsed > _slowMsgThreshold) {
				Log.Debug(
					"SLOW QUEUE MSG [{queue}]: {message} - {elapsed}ms. Q: {prevQueueCount}/{curQueueCount}.",
					Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, _previousQueueDepth, Interlocked.Read(ref _queueDepth));
				if (elapsed > QueuedHandler.VerySlowMsgThreshold &&
					!(msg is SystemMessage.SystemInit))
					Log.Error(
						"---!!! VERY SLOW QUEUE MSG [{queue}]: {message} - {elapsed}ms. Q:{prevQueueCount}/{curQueueCount}.",
						Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, _previousQueueDepth, Interlocked.Read(ref _queueDepth));
			}
		}
		public void Publish(Message message) {			
#if DEBUG
			_queueStats.Enqueued();
#endif
			//sync & blocking to match current behavior			
			while (!_channel.Writer.TryWrite(message)) { }
			Interlocked.Increment(ref _queueDepth);
		}

		public void Handle(Message message) {
			Publish(message);
		}

		public QueueStats GetStatistics() {
			return _queueStats.GetStatistics((int)Interlocked.Read(ref _queueDepth));
		}
	}
}
