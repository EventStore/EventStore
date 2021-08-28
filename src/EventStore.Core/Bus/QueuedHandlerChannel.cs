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
		private readonly ChannelOptions _config;
		private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
		private readonly TimeSpan _stopWaitTimeout;

		private long _queueDepth;
		private long _previousQueueDepth;

		private readonly QueueMonitor _queueMonitor;
		private readonly QueueStatsCollector _queueStats;
		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();
		private CancellationTokenSource _tokenSource;


		//todo-jgeall: this is currently mapping into existing patterns for queue monitoring that should be reviewed and updated making this far simpler
		public QueuedHandlerChannel(
			IHandle<Message> consumer,
			string name,
			QueueStatsManager queueStatsManager,
			bool watchSlowMsg = true,
			TimeSpan? slowMsgThreshold = null,
			TimeSpan? stopWaitTimeout = null,
			string groupName = null,
			bool continueOnContext = true,
			ChannelOptions config = null) {
			Ensure.NotNull(consumer, "consumer");
			Ensure.NotNull(name, "name");

			if (config == null) {
				config = new UnboundedChannelOptions();
			}
			_config = config;
			_consumer = consumer;
			_watchSlowMsg = watchSlowMsg;
			_slowMsgThreshold = slowMsgThreshold ?? InMemoryBus.DefaultSlowMessageThreshold;
			_stopWaitTimeout = stopWaitTimeout ?? QueuedHandler.DefaultStopWaitTimeout;
			_queueMonitor = QueueMonitor.Default;
			_queueStats = queueStatsManager.CreateQueueStatsCollector(name, groupName);
			_channel = BuildChannel();
			_tokenSource = new CancellationTokenSource();
		}

		private Channel<Message> BuildChannel() {
			return Channel.CreateUnbounded<Message>((UnboundedChannelOptions)_config);
		}

		public Task Start() {
			if (!_stopped.IsSet)
				throw new InvalidOperationException("Channel already running.");
			if (_consumer == null) { new InvalidOperationException("Consumer not set, unable to start Queue."); }

			_queueMonitor.Register(this);

			_stopped.Reset();
			Task.Run(() => ReadFromQueue(_tokenSource.Token));
			return _tcs.Task;
		}

		public void Stop() {
			_tokenSource.Cancel();
			if (!_stopped.Wait(_stopWaitTimeout))
				throw new TimeoutException(string.Format("Unable to stop channel '{0}'.", Name));
		}

		public void RequestStop() {
			_tokenSource.Cancel();
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Code", "CAC002:ConfigureAwaitChecker", Justification = "Dedicated thread processing when true.")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Code", "CAC001:ConfigureAwaitChecker", Justification = "Dedicated thread processing when true.")]
		private async void ReadFromQueue(CancellationToken cancelToken) {

			Action<Message> send = _consumer.Handle;
			if (_watchSlowMsg) { send = MonitoredSend; }
			try {
				_queueStats.Start();
				while (await _channel.Reader.WaitToReadAsync(cancelToken).ConfigureAwait(false)) {
					Message msg = null;
					try {
						while (_channel.Reader.TryRead(out msg)) {
							Interlocked.Decrement(ref _queueDepth);
							_queueStats.EnterBusy();
#if DEBUG
							_queueStats.Dequeued(msg);
#endif
							_previousQueueDepth = Interlocked.Read(ref _queueDepth);
							_queueStats.ProcessingStarted(msg.GetType(), (int)_previousQueueDepth);
							send(msg);
							_queueStats.ProcessingEnded(1);
						}
						_queueStats.EnterIdle();
						if (cancelToken.IsCancellationRequested) { break; }
					
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

		private void MonitoredSend(Message msg) {
			var start = DateTime.UtcNow;

			_consumer.Handle(msg);

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
			//Ensure.NotNull(message, "message");
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
