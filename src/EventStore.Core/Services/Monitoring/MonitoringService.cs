using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Monitoring.Utils;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Transport.Tcp;

namespace EventStore.Core.Services.Monitoring {
	[Flags]
	public enum StatsStorage {
		None = 0x0, // only for tests
		Stream = 0x1,
		File = 0x2,
		StreamAndFile = Stream | File
	}

	public class MonitoringService : IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<SystemMessage.BecomeShutdown>,
		IHandle<ClientMessage.WriteEventsCompleted>,
		IHandle<MonitoringMessage.GetFreshStats>,
		IHandle<MonitoringMessage.GetFreshTcpConnectionStats> {
		private static readonly ILogger RegularLog = LogManager.GetLogger("REGULAR-STATS-LOGGER");
		private static readonly ILogger Log = LogManager.GetLoggerFor<MonitoringService>();

		private static readonly string StreamMetadata =
			string.Format("{{\"$maxAge\":{0}}}", (int)TimeSpan.FromDays(10).TotalSeconds);

		public static readonly TimeSpan MemoizePeriod = TimeSpan.FromSeconds(1);
		private static readonly IEnvelope NoopEnvelope = new NoopEnvelope();

		private readonly IQueuedHandler _monitoringQueue;
		private readonly IPublisher _statsCollectionBus;
		private readonly IPublisher _mainBus;
		private readonly ICheckpoint _writerCheckpoint;
		private readonly string _dbPath;
		private readonly StatsStorage _statsStorage;
		private readonly long _statsCollectionPeriodMs;
		private SystemStatsHelper _systemStats;

		private string _lastWrittenCsvHeader;
		private DateTime _lastCsvTimestamp = DateTime.UtcNow;
		private DateTime _lastStatsRequestTime = DateTime.UtcNow;
		private StatsContainer _memoizedStats;
		private readonly Timer _timer;
		private readonly string _nodeStatsStream;
		private bool _statsStreamCreated;
		private Guid _streamMetadataWriteCorrId;
		private IMonitoredTcpConnection[] _memoizedTcpConnections;
		private DateTime _lastTcpConnectionsRequestTime;
		private IPEndPoint _tcpEndpoint;
		private IPEndPoint _tcpSecureEndpoint;

		public MonitoringService(IQueuedHandler monitoringQueue,
			IPublisher statsCollectionBus,
			IPublisher mainBus,
			ICheckpoint writerCheckpoint,
			string dbPath,
			TimeSpan statsCollectionPeriod,
			IPEndPoint nodeEndpoint,
			StatsStorage statsStorage,
			IPEndPoint tcpEndpoint,
			IPEndPoint tcpSecureEndpoint) {
			Ensure.NotNull(monitoringQueue, "monitoringQueue");
			Ensure.NotNull(statsCollectionBus, "statsCollectionBus");
			Ensure.NotNull(mainBus, "mainBus");
			Ensure.NotNull(writerCheckpoint, "writerCheckpoint");
			Ensure.NotNullOrEmpty(dbPath, "dbPath");
			Ensure.NotNull(nodeEndpoint, "nodeEndpoint");
			Ensure.NotNull(tcpEndpoint, "tcpEndpoint");

			_monitoringQueue = monitoringQueue;
			_statsCollectionBus = statsCollectionBus;
			_mainBus = mainBus;
			_writerCheckpoint = writerCheckpoint;
			_dbPath = dbPath;
			_statsStorage = statsStorage;
			_statsCollectionPeriodMs = statsCollectionPeriod > TimeSpan.Zero
				? (long)statsCollectionPeriod.TotalMilliseconds
				: Timeout.Infinite;
			_nodeStatsStream = string.Format("{0}-{1}", SystemStreams.StatsStreamPrefix, nodeEndpoint);
			_tcpEndpoint = tcpEndpoint;
			_tcpSecureEndpoint = tcpSecureEndpoint;
			_timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
		}

		public void Handle(SystemMessage.SystemInit message) {
			_systemStats = new SystemStatsHelper(Log, _writerCheckpoint, _dbPath);
			_timer.Change(_statsCollectionPeriodMs, Timeout.Infinite);
		}

		public void OnTimerTick(object state) {
			CollectRegularStats();
			_timer.Change(_statsCollectionPeriodMs, Timeout.Infinite);
		}

		private void CollectRegularStats() {
			try {
				var stats = CollectStats();
				if (stats != null) {
					var rawStats = stats.GetStats(useGrouping: false, useMetadata: false);

					if ((_statsStorage & StatsStorage.File) != 0)
						SaveStatsToFile(LogManager.StructuredLog ? StatsContainer.Group(rawStats) : rawStats);

					if ((_statsStorage & StatsStorage.Stream) != 0) {
						if (_statsStreamCreated)
							SaveStatsToStream(rawStats);
					}
				}
			} catch (Exception ex) {
				Log.ErrorException(ex, "Error on regular stats collection.");
			}
		}

		private StatsContainer CollectStats() {
			var statsContainer = new StatsContainer();
			try {
				statsContainer.Add(_systemStats.GetSystemStats());
				_statsCollectionBus.Publish(
					new MonitoringMessage.InternalStatsRequest(new StatsCollectorEnvelope(statsContainer)));
			} catch (Exception ex) {
				Log.ErrorException(ex, "Error while collecting stats");
				statsContainer = null;
			}

			return statsContainer;
		}

		private void SaveStatsToFile(Dictionary<string, object> rawStats) {
			if (LogManager.StructuredLog) {
				rawStats.Add("timestamp", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
				RegularLog.Info("{@stats}", rawStats);
			} else {
				var writeHeader = false;

				var header = StatsCsvEncoder.GetHeader(rawStats);
				if (header != _lastWrittenCsvHeader) {
					_lastWrittenCsvHeader = header;
					writeHeader = true;
				}

				var line = StatsCsvEncoder.GetLine(rawStats);
				var timestamp = GetTimestamp(line);
				if(timestamp.HasValue){
					if(timestamp.Value.Day != _lastCsvTimestamp.Day){
						writeHeader = true;
					}
					_lastCsvTimestamp = timestamp.Value;
				}

				if(writeHeader){
					RegularLog.Info(Environment.NewLine);
					RegularLog.Info(header);
				}
				RegularLog.Info(line);
			}
		}

        private DateTime? GetTimestamp(string line) {
			var separatorIdx = line.IndexOf(',');
			if(separatorIdx == -1)
				return null;

			try{
				return DateTime.Parse(line.Substring(0, separatorIdx)).ToUniversalTime();
			}
			catch{
				return null;
			}
        }

        private void SaveStatsToStream(Dictionary<string, object> rawStats) {
			var data = rawStats.ToJsonBytes();
			var evnt = new Event(Guid.NewGuid(), SystemEventTypes.StatsCollection, true, data, null);
			var corrId = Guid.NewGuid();
			var msg = new ClientMessage.WriteEvents(corrId, corrId, NoopEnvelope, false, _nodeStatsStream,
				ExpectedVersion.Any, new[] {evnt}, SystemAccount.Principal);
			_mainBus.Publish(msg);
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			if ((_statsStorage & StatsStorage.Stream) == 0)
				return;

			if (_statsStreamCreated)
				return;

			switch (message.State) {
				case VNodeState.CatchingUp:
				case VNodeState.Clone:
				case VNodeState.Slave:
				case VNodeState.Master: {
					SetStatsStreamMetadata();
					break;
				}
			}
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			try {
				_timer.Dispose();
				_systemStats.Dispose();
			} catch (ObjectDisposedException) {
				// ok, no problem if already disposed
			}
		}

		public void Handle(SystemMessage.BecomeShutdown message) {
			_monitoringQueue.RequestStop();
		}

		private void SetStatsStreamMetadata() {
			var metadata = Helper.UTF8NoBom.GetBytes(StreamMetadata);
			_streamMetadataWriteCorrId = Guid.NewGuid();
			_mainBus.Publish(
				new ClientMessage.WriteEvents(
					_streamMetadataWriteCorrId, _streamMetadataWriteCorrId, new PublishEnvelope(_monitoringQueue),
					false, SystemStreams.MetastreamOf(_nodeStatsStream), ExpectedVersion.NoStream,
					new[] {new Event(Guid.NewGuid(), SystemEventTypes.StreamMetadata, true, metadata, null)},
					SystemAccount.Principal));
		}

		public void Handle(ClientMessage.WriteEventsCompleted message) {
			if (message.CorrelationId != _streamMetadataWriteCorrId)
				return;
			switch (message.Result) {
				case OperationResult.Success:
				case OperationResult.WrongExpectedVersion: // already created
				{
					Log.Trace("Created stats stream '{stream}', code = {result}", _nodeStatsStream, message.Result);
					_statsStreamCreated = true;
					break;
				}
				case OperationResult.PrepareTimeout:
				case OperationResult.CommitTimeout:
				case OperationResult.ForwardTimeout: {
					Log.Debug("Failed to create stats stream '{stream}'. Reason : {e}({message}). Retrying...",
						_nodeStatsStream, message.Result, message.Message);
					SetStatsStreamMetadata();
					break;
				}
				case OperationResult.AccessDenied: {
					// can't do anything about that right now
					break;
				}
				case OperationResult.StreamDeleted:
				case OperationResult.InvalidTransaction: // should not happen at all
				{
					Log.Error(
						"Monitoring service got unexpected response code when trying to create stats stream ({e}).",
						message.Result);
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void Handle(MonitoringMessage.GetFreshStats message) {
			try {
				StatsContainer stats;
				if (!TryGetMemoizedStats(out stats)) {
					stats = CollectStats();
					if (stats != null) {
						_memoizedStats = stats;
						_lastStatsRequestTime = DateTime.UtcNow;
					}
				}

				Dictionary<string, object> selectedStats = null;
				if (stats != null) {
					selectedStats = stats.GetStats(message.UseGrouping, message.UseMetadata);
					if (message.UseGrouping)
						selectedStats = message.StatsSelector(selectedStats);
				}

				message.Envelope.ReplyWith(
					new MonitoringMessage.GetFreshStatsCompleted(success: selectedStats != null, stats: selectedStats));
			} catch (Exception ex) {
				Log.ErrorException(ex, "Error on getting fresh stats");
			}
		}

		public void Handle(MonitoringMessage.GetFreshTcpConnectionStats message) {
			try {
				IMonitoredTcpConnection[] connections = null;
				if (!TryGetMemoizedTcpConnections(out connections)) {
					connections = TcpConnectionMonitor.Default.GetTcpConnectionStats();
					if (connections != null) {
						_memoizedTcpConnections = connections;
						_lastTcpConnectionsRequestTime = DateTime.UtcNow;
					}
				}

				List<MonitoringMessage.TcpConnectionStats> connStats = new List<MonitoringMessage.TcpConnectionStats>();
				foreach (var conn in connections) {
					var tcpConn = conn as TcpConnection;
					if (tcpConn != null) {
						var isExternalConnection = _tcpEndpoint.Port == tcpConn.LocalEndPoint.Port;
						connStats.Add(new MonitoringMessage.TcpConnectionStats {
							IsExternalConnection = isExternalConnection,
							RemoteEndPoint = tcpConn.RemoteEndPoint.ToString(),
							LocalEndPoint = tcpConn.LocalEndPoint.ToString(),
							ConnectionId = tcpConn.ConnectionId,
							ClientConnectionName = tcpConn.ClientConnectionName,
							TotalBytesSent = tcpConn.TotalBytesSent,
							TotalBytesReceived = tcpConn.TotalBytesReceived,
							PendingSendBytes = tcpConn.PendingSendBytes,
							PendingReceivedBytes = tcpConn.PendingReceivedBytes,
							IsSslConnection = false
						});
					}

					var tcpConnSsl = conn as TcpConnectionSsl;
					if (tcpConnSsl != null) {
						var isExternalConnection = _tcpSecureEndpoint != null &&
						                           _tcpSecureEndpoint.Port == tcpConnSsl.LocalEndPoint.Port;
						connStats.Add(new MonitoringMessage.TcpConnectionStats {
							IsExternalConnection = isExternalConnection,
							RemoteEndPoint = tcpConnSsl.RemoteEndPoint.ToString(),
							LocalEndPoint = tcpConnSsl.LocalEndPoint.ToString(),
							ConnectionId = tcpConnSsl.ConnectionId,
							ClientConnectionName = tcpConnSsl.ClientConnectionName,
							TotalBytesSent = tcpConnSsl.TotalBytesSent,
							TotalBytesReceived = tcpConnSsl.TotalBytesReceived,
							PendingSendBytes = tcpConnSsl.PendingSendBytes,
							PendingReceivedBytes = tcpConnSsl.PendingReceivedBytes,
							IsSslConnection = true
						});
					}
				}

				message.Envelope.ReplyWith(
					new MonitoringMessage.GetFreshTcpConnectionStatsCompleted(connStats)
				);
			} catch (Exception ex) {
				Log.ErrorException(ex, "Error on getting fresh tcp connection stats");
			}
		}

		private bool TryGetMemoizedStats(out StatsContainer stats) {
			if (_memoizedStats == null || DateTime.UtcNow - _lastStatsRequestTime > MemoizePeriod) {
				stats = null;
				return false;
			}

			stats = _memoizedStats;
			return true;
		}

		private bool TryGetMemoizedTcpConnections(out IMonitoredTcpConnection[] connections) {
			if (_memoizedTcpConnections == null || DateTime.UtcNow - _lastTcpConnectionsRequestTime > MemoizePeriod) {
				connections = null;
				return false;
			}

			connections = _memoizedTcpConnections;
			return true;
		}
	}
}
