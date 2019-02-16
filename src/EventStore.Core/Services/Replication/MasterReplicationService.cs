using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Tcp;

namespace EventStore.Core.Services.Replication {
	public class MasterReplicationService : IMonitoredQueue,
		IHandle<SystemMessage.SystemStart>,
		IHandle<SystemMessage.StateChangeMessage>,
		IHandle<ReplicationMessage.ReplicaSubscriptionRequest>,
		IHandle<ReplicationMessage.ReplicaLogPositionAck>,
		IHandle<ReplicationMessage.GetReplicationStats> {
		public const int MaxQueueSize = 100;
		public const int CloneThreshold = 1024;
		public const int SlaveLagThreshold = 256 * 1024;
		public const int LagOccurencesThreshold = 2;
		public const int BulkSize = 8192;
		public const int ReplicaSendWindow = 16 * 1024 * 1024;
		public const int ReplicaAckWindow = 512 * 1024;
		public static readonly TimeSpan RoleAssignmentsInterval = TimeSpan.FromMilliseconds(1000);
		public static readonly TimeSpan NoQuorumTimeout = TimeSpan.FromMilliseconds(3000);

		private static readonly ILogger Log = LogManager.GetLoggerFor<MasterReplicationService>();

		public string Name {
			get { return _queueStats.Name; }
		}

		private readonly IPublisher _publisher;
		private readonly Guid _instanceId;
		private readonly TFChunkDb _db;
		private readonly IPublisher _tcpSendPublisher;
		private readonly IEpochManager _epochManager;
		private readonly int _clusterSize;

		private readonly Thread _mainLoopThread;
		private volatile bool _stop;
		private readonly QueueStatsCollector _queueStats = new QueueStatsCollector("Master Replication Service");

		private readonly ConcurrentDictionary<Guid, ReplicaSubscription> _subscriptions =
			new ConcurrentDictionary<Guid, ReplicaSubscription>();

		private volatile VNodeState _state = VNodeState.Initializing;

		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private TimeSpan _lastRolesAssignmentTimestamp;
		private volatile bool _newSubscriptions;
		private TimeSpan _noQuorumTimestamp = TimeSpan.Zero;
		private bool _noQuorumNotified;
		private ManualResetEventSlim _flushSignal = new ManualResetEventSlim(false, 1);
		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

		public Task Task {
			get { return _tcs.Task; }
		}

		public MasterReplicationService(IPublisher publisher,
			Guid instanceId,
			TFChunkDb db,
			IPublisher tcpSendPublisher,
			IEpochManager epochManager,
			int clusterSize) {
			Ensure.NotNull(publisher, "publisher");
			Ensure.NotEmptyGuid(instanceId, "instanceId");
			Ensure.NotNull(db, "db");
			Ensure.NotNull(tcpSendPublisher, "tcpSendPublisher");
			Ensure.NotNull(epochManager, "epochManager");
			Ensure.Positive(clusterSize, "clusterSize");

			_publisher = publisher;
			_instanceId = instanceId;
			_db = db;
			_tcpSendPublisher = tcpSendPublisher;
			_epochManager = epochManager;
			_clusterSize = clusterSize;

			_lastRolesAssignmentTimestamp = _stopwatch.Elapsed;
			_mainLoopThread = new Thread(MainLoop) {Name = _queueStats.Name, IsBackground = true};
		}

		public void Handle(SystemMessage.SystemStart message) {
			_mainLoopThread.Start();
		}

		public void Handle(SystemMessage.StateChangeMessage message) {
			_state = message.State;

			if (message.State == VNodeState.ShuttingDown)
				_stop = true;
		}

		public void Handle(ReplicationMessage.ReplicaSubscriptionRequest message) {
			_publisher.Publish(new SystemMessage.VNodeConnectionEstablished(message.ReplicaEndPoint,
				message.Connection.ConnectionId));

			if (_state != VNodeState.Master || message.MasterId != _instanceId) {
				message.Envelope.ReplyWith(
					new ReplicationMessage.ReplicaSubscriptionRetry(_instanceId, message.SubscriptionId));
				return;
			}

			var subscription = new ReplicaSubscription(_tcpSendPublisher,
				message.Connection,
				message.SubscriptionId,
				message.ReplicaEndPoint,
				message.IsPromotable);

			foreach (var subscr in _subscriptions.Values) {
				if (subscr != subscription && subscr.ConnectionId == subscription.ConnectionId)
					subscr.ShouldDispose = true;
			}

			if (SubscribeReplica(subscription, message.LastEpochs, message.CorrelationId, message.LogPosition,
				message.ChunkId)) {
				_newSubscriptions = true;
				if (!_subscriptions.TryAdd(subscription.SubscriptionId, subscription)) {
					ReplicaSubscription existingSubscr;
					_subscriptions.TryGetValue(subscription.SubscriptionId, out existingSubscr);
					Log.Error(
						"There is already a subscription with SubscriptionID {subscriptionId:B}: {existingSubscription}.",
						subscription.SubscriptionId, existingSubscr);
					Log.Error("Subscription we tried to add: {existingSubscription}.", existingSubscr);
					subscription.SendBadRequestAndClose(message.CorrelationId, string.Format(
						"There is already a subscription with SubscriptionID {0:B}: {1}.\nSubscription we tried to add: {2}",
						subscription.SubscriptionId, existingSubscr, subscription));
					subscription.Dispose();
				}
			}
		}

		public void Handle(ReplicationMessage.ReplicaLogPositionAck message) {
			ReplicaSubscription subscription;
			if (_subscriptions.TryGetValue(message.SubscriptionId, out subscription))
				Interlocked.Exchange(ref subscription.AckedLogPosition, message.ReplicationLogPosition);
		}

		public void Handle(ReplicationMessage.GetReplicationStats message) {
			var connections = TcpConnectionMonitor.Default.GetTcpConnectionStats();
			var replicaStats = new List<ReplicationMessage.ReplicationStats>();
			foreach (var conn in connections) {
				var tcpConn = conn as TcpConnection;
				if (tcpConn != null) {
					var subscription = _subscriptions.FirstOrDefault(x => x.Value.ConnectionId == tcpConn.ConnectionId);
					if (subscription.Value != null) {
						var stats = new ReplicationMessage.ReplicationStats(subscription.Key, tcpConn.ConnectionId,
							subscription.Value.ReplicaEndPoint.ToString(), tcpConn.SendQueueSize,
							conn.TotalBytesSent, conn.TotalBytesReceived, conn.PendingSendBytes,
							conn.PendingReceivedBytes);
						replicaStats.Add(stats);
					}
				}
			}

			message.Envelope.ReplyWith(new ReplicationMessage.GetReplicationStatsCompleted(replicaStats));
		}

		private bool SubscribeReplica(ReplicaSubscription replica, Epoch[] lastEpochs, Guid correlationId,
			long logPosition, Guid chunkId) {
			try {
				var epochs = lastEpochs ?? new Epoch[0];
				Log.Info(
					"SUBSCRIBE REQUEST from [{replicaEndPoint},C:{connectionId:B},S:{subscriptionId:B},{logPosition}(0x{logPosition:X}),{epochs}]...",
					replica.ReplicaEndPoint, replica.ConnectionId, replica.SubscriptionId, logPosition, logPosition,
					string.Join(", ", epochs.Select(x => EpochRecordExtensions.AsString((Epoch)x))));

				var epochCorrectedLogPos =
					GetValidLogPosition(logPosition, epochs, replica.ReplicaEndPoint, replica.SubscriptionId);
				var subscriptionPos = SetSubscriptionPosition(replica, epochCorrectedLogPos, chunkId,
					replicationStart: true, verbose: true, trial: 0);
				Interlocked.Exchange(ref replica.AckedLogPosition, subscriptionPos);
				return true;
			} catch (Exception exc) {
				Log.ErrorException(exc, "Exception while subscribing replica. Connection will be dropped.");
				replica.SendBadRequestAndClose(correlationId,
					string.Format("Exception while subscribing replica. Connection will be dropped. Error: {0}",
						exc.Message));
				return false;
			}
		}

		private long GetValidLogPosition(long logPosition, Epoch[] epochs, IPEndPoint replicaEndPoint,
			Guid subscriptionId) {
			if (epochs.Length == 0) {
				if (logPosition > 0) {
					// slave has some data, but doesn't have any epoch
					// for now we'll just report error and close connection
					var msg = string.Format(
						"Replica [{0},S:{1},{2}] has positive LogPosition {3} (0x{3:X}), but does not have epochs.",
						replicaEndPoint, subscriptionId,
						string.Join(", ", epochs.Select(x => x.AsString())), logPosition);
					Log.Info(
						"Replica [{replicaEndPoint},S:{subscriptionId},{epochs}] has positive LogPosition {logPosition} (0x{logPosition:X}), but does not have epochs.",
						replicaEndPoint, subscriptionId,
						string.Join(", ", epochs.Select(x => x.AsString())), logPosition, logPosition);
					throw new Exception(msg);
				}

				return 0;
			}

			var masterCheckpoint = _db.Config.WriterCheckpoint.Read();
			Epoch afterCommonEpoch = null;
			Epoch commonEpoch = null;
			for (int i = 0; i < epochs.Length; ++i) {
				var epoch = epochs[i];
				if (_epochManager.IsCorrectEpochAt(epoch.EpochPosition, epoch.EpochNumber, epoch.EpochId)) {
					commonEpoch = epoch;
					afterCommonEpoch = i > 0 ? epochs[i - 1] : null;
					break;
				}
			}

			if (commonEpoch == null) {
				Log.Error(
					"No common epoch found for replica [{replicaEndPoint},S{subscriptionId},{logPosition}(0x{logPosition:X}),{epochs}]. Subscribing at 0. Master LogPosition: {masterCheckpoint} (0x{masterCheckpoint:X}), known epochs: {knownEpochs}.",
					replicaEndPoint, subscriptionId,
					logPosition, logPosition,
					string.Join(", ", epochs.Select(x => x.AsString())),
					masterCheckpoint, masterCheckpoint,
					string.Join(", ", _epochManager.GetLastEpochs(int.MaxValue).Select(x => x.AsString())));
				return 0;
			}

			// if afterCommonEpoch is present, logPosition > afterCommonEpoch.EpochPosition,
			// so safe position is definitely the start of afterCommonEpoch
			var replicaPosition = afterCommonEpoch == null ? logPosition : afterCommonEpoch.EpochPosition;

			if (commonEpoch.EpochNumber == _epochManager.LastEpochNumber)
				return Math.Min(replicaPosition, masterCheckpoint);

			var nextEpoch = _epochManager.GetEpoch(commonEpoch.EpochNumber + 1, throwIfNotFound: false);
			if (nextEpoch == null) {
				nextEpoch = _epochManager.GetEpochWithAllEpochs(commonEpoch.EpochNumber + 1, throwIfNotFound: false);
			}

			if (nextEpoch == null) {
				var msg = string.Format(
					"Replica [{0},S:{1},{2}(0x{3:X}),epochs:\n{4}]\n provided epochs which are not in "
					+ "EpochManager (possibly too old, known epochs:\n{5}).\nMaster LogPosition: {6} (0x{7:X}). "
					+ "We do not support this case as of now.\n"
					+ "CommonEpoch: {8}, AfterCommonEpoch: {9}",
					replicaEndPoint, subscriptionId, logPosition, logPosition,
					string.Join("\n", epochs.Select(x => x.AsString())),
					string.Join("\n", _epochManager.GetLastEpochs(int.MaxValue).Select(x => x.AsString())),
					masterCheckpoint, masterCheckpoint,
					commonEpoch.AsString(), afterCommonEpoch == null ? "<none>" : afterCommonEpoch.AsString());
				Log.Error(
					"Replica [{replicaEndPoint},S:{subscriptionId},{logPosition}(0x{logPosition:X}),epochs:\n{epochs}]\n provided epochs which are not in "
					+ "EpochManager (possibly too old, known epochs:\n{lastEpochs}).\nMaster LogPosition: {masterCheckpoint} (0x{masterCheckpoint:X}). "
					+ "We do not support this case as of now.\n"
					+ "CommonEpoch: {commonEpoch}, AfterCommonEpoch: {afterCommonEpoch}",
					replicaEndPoint,
					subscriptionId,
					logPosition,
					logPosition,
					string.Join("\n", epochs.Select(x => x.AsString())),
					string.Join("\n", _epochManager.GetLastEpochs(int.MaxValue).Select(x => x.AsString())),
					masterCheckpoint,
					masterCheckpoint,
					commonEpoch.AsString(),
					afterCommonEpoch == null ? "<none>" : afterCommonEpoch.AsString()
				);
				throw new Exception(msg);
			}

			return Math.Min(replicaPosition, nextEpoch.EpochPosition);
		}

		private long SetSubscriptionPosition(ReplicaSubscription sub,
			long logPosition,
			Guid chunkId,
			bool replicationStart,
			bool verbose,
			int trial) {
			if (trial >= 10)
				throw new Exception("Too many retrials to acquire reader for subscriber.");

			try {
				var chunk = _db.Manager.GetChunkFor(logPosition);
				Debug.Assert(chunk != null, string.Format(
					"Chunk for LogPosition {0} (0x{0:X}) is null in MasterReplicationService! Replica: [{1},C:{2},S:{3}]",
					logPosition, sub.ReplicaEndPoint, sub.ConnectionId, sub.SubscriptionId));
				var bulkReader = chunk.AcquireReader();
				if (chunk.ChunkHeader.IsScavenged && (chunkId == Guid.Empty || chunkId != chunk.ChunkHeader.ChunkId)) {
					var chunkStartPos = chunk.ChunkHeader.ChunkStartPosition;
					if (verbose) {
						Log.Info(
							"Subscribed replica [{replicaEndPoint}, S:{subscriptionId}] for raw send at {chunkStartPosition} (0x{chunkStartPosition:X}) (requested {logPosition} (0x{logPosition:X})).",
							sub.ReplicaEndPoint, sub.SubscriptionId, chunkStartPos, chunkStartPos, logPosition,
							logPosition);
						if (chunkStartPos != logPosition) {
							Log.Info(
								"Forcing replica [{replicaEndPoint}, S:{subscriptionId}] to recreate chunk from position {chunkStartPosition} (0x{chunkStartPosition:X})...",
								sub.ReplicaEndPoint, sub.SubscriptionId, chunkStartPos, chunkStartPos);
						}
					}

					sub.LogPosition = chunkStartPos;
					sub.RawSend = true;
					bulkReader.SetRawPosition(ChunkHeader.Size);
					if (replicationStart)
						sub.SendMessage(new ReplicationMessage.ReplicaSubscribed(_instanceId, sub.SubscriptionId,
							sub.LogPosition));
					sub.SendMessage(new ReplicationMessage.CreateChunk(_instanceId,
						sub.SubscriptionId,
						chunk.ChunkHeader,
						chunk.FileSize,
						isCompletedChunk: true));
				} else {
					if (verbose)
						Log.Info(
							"Subscribed replica [{replicaEndPoint},S:{subscriptionId}] for data send at {logPosition} (0x{logPosition:X}).",
							sub.ReplicaEndPoint, sub.SubscriptionId, logPosition, logPosition);

					sub.LogPosition = logPosition;
					sub.RawSend = false;
					bulkReader.SetDataPosition(chunk.ChunkHeader.GetLocalLogPosition(logPosition));
					if (replicationStart)
						sub.SendMessage(new ReplicationMessage.ReplicaSubscribed(_instanceId, sub.SubscriptionId,
							sub.LogPosition));
				}

				sub.EOFSent = false;
				var oldBulkReader = Interlocked.Exchange(ref sub.BulkReader, bulkReader);
				if (oldBulkReader != null)
					oldBulkReader.Release();
				return sub.LogPosition;
			} catch (FileBeingDeletedException) {
				return SetSubscriptionPosition(sub, logPosition, chunkId, replicationStart, verbose, trial + 1);
			}
		}

		private void MainLoop() {
			try {
				_queueStats.Start();
				QueueMonitor.Default.Register(this);

				_db.Config.WriterCheckpoint.Flushed += OnWriterFlushed;

				while (!_stop) {
					try {
						_queueStats.EnterBusy();

						_queueStats.ProcessingStarted(typeof(SendReplicationData), _subscriptions.Count);

						_flushSignal
							.Reset(); // Reset the flush signal as we're about to read anyway. This could be closer to the actual read but no harm from too many checks.

						var dataFound = ManageSubscriptions();
						ManageNoQuorumDetection();
						var newSubscriptions = _newSubscriptions;
						_newSubscriptions = false;
						ManageRoleAssignments(force: newSubscriptions);

						_queueStats.ProcessingEnded(_subscriptions.Count);

						if (!dataFound) {
							_queueStats.EnterIdle();

							_flushSignal.Wait(TimeSpan.FromMilliseconds(500));
						}
					} catch (Exception exc) {
						Log.InfoException(exc, "Error during master replication iteration.");
#if DEBUG
						throw;
#endif
					}
				}

				foreach (var subscription in _subscriptions.Values) {
					subscription.Dispose();
				}

				_db.Config.WriterCheckpoint.Flushed -= OnWriterFlushed;

				_publisher.Publish(new SystemMessage.ServiceShutdown(Name));
			} catch (Exception ex) {
				_tcs.TrySetException(ex);
				throw;
			} finally {
				_queueStats.Stop();
				QueueMonitor.Default.Unregister(this);
			}
		}

		private void OnWriterFlushed(long obj) {
			_flushSignal.Set();
		}

		private bool ManageSubscriptions() {
			var dataFound = false;
			foreach (var subscription in _subscriptions.Values) {
				if (subscription.IsConnectionClosed) {
					_publisher.Publish(new SystemMessage.VNodeConnectionLost(subscription.ReplicaEndPoint,
						subscription.ConnectionId));
					subscription.ShouldDispose = true;
				}

				if (subscription.ShouldDispose) {
					ReplicaSubscription tmp;
					_subscriptions.TryRemove(subscription.SubscriptionId, out tmp);
					subscription.Dispose();
					continue;
				}

				if (subscription.SendQueueSize >= MaxQueueSize
				    || subscription.LogPosition - Interlocked.Read(ref subscription.AckedLogPosition) >=
				    ReplicaSendWindow)
					continue;

				if (subscription.BulkReader == null) throw new Exception("BulkReader is null for subscription.");

				try {
					var masterCheckpoint = _db.Config.WriterCheckpoint.Read();

					if (TrySendLogBulk(subscription, masterCheckpoint))
						dataFound = true;

					if (subscription.State == ReplicaState.CatchingUp &&
					    masterCheckpoint - subscription.LogPosition <= CloneThreshold) {
						subscription.State = ReplicaState.Clone;
						subscription.SendMessage(
							new ReplicationMessage.CloneAssignment(_instanceId, subscription.SubscriptionId));
					}
				} catch (Exception exc) {
					Log.InfoException(exc, "Error during replication send to replica: {subscription}.", subscription);
				}
			}

			return dataFound;
		}

		private bool TrySendLogBulk(ReplicaSubscription subscription, long masterCheckpoint) {
			/*
			if (subscription == null) throw new Exception("subscription == null");
			if (subscription.BulkReader == null) throw new Exception("subscription.BulkReader == null");
			if (subscription.BulkReader.Chunk == null) throw new Exception("subscription.BulkReader.Chunk == null");
			if (subscription.DataBuffer == null) throw new Exception("subscription.DataBuffer == null");
			*/

			var bulkReader = subscription.BulkReader;
			var chunkHeader = bulkReader.Chunk.ChunkHeader;

			BulkReadResult bulkResult;
			if (subscription.RawSend) {
				bulkResult = bulkReader.ReadNextRawBytes(subscription.DataBuffer.Length, subscription.DataBuffer);
			} else {
				var bytesToRead = (int)Math.Min(subscription.DataBuffer.Length,
					masterCheckpoint - subscription.LogPosition);
				bulkResult = bulkReader.ReadNextDataBytes(bytesToRead, subscription.DataBuffer);
			}

			bool dataFound = false;
			// for logical send we can get 0 at the end multiple time, but we need to get EOF exactly once
			if (bulkResult.BytesRead > 0 || (bulkResult.IsEOF && !subscription.RawSend && !subscription.EOFSent)) {
				var data = new byte[bulkResult.BytesRead];
				Buffer.BlockCopy(subscription.DataBuffer, 0, data, 0, bulkResult.BytesRead);

				dataFound = true;
				subscription.EOFSent = bulkResult.IsEOF;

				if (subscription.RawSend) {
					var msg = new ReplicationMessage.RawChunkBulk(
						_instanceId, subscription.SubscriptionId, chunkHeader.ChunkStartNumber,
						chunkHeader.ChunkEndNumber,
						bulkResult.OldPosition, data, bulkResult.IsEOF);
					subscription.SendMessage(msg);
				} else {
					if (chunkHeader.GetLocalLogPosition(subscription.LogPosition) != bulkResult.OldPosition) {
						throw new Exception(string.Format(
							"Replication invariant failure. SubscriptionPosition {0}, bulkResult.OldPosition {1}",
							subscription.LogPosition, bulkResult.OldPosition));
					}

					var msg = new ReplicationMessage.DataChunkBulk(
						_instanceId, subscription.SubscriptionId, chunkHeader.ChunkStartNumber,
						chunkHeader.ChunkEndNumber,
						subscription.LogPosition, data, bulkResult.IsEOF);
					subscription.LogPosition += bulkResult.BytesRead;
					subscription.SendMessage(msg);
				}
			}

			if (bulkResult.IsEOF) {
				var newLogPosition = chunkHeader.ChunkEndPosition;
				if (newLogPosition < masterCheckpoint) {
					dataFound = true;
					SetSubscriptionPosition(subscription, newLogPosition, Guid.Empty, replicationStart: false,
						verbose: true, trial: 0);
				}
			}

			return dataFound;
		}

		private void ManageNoQuorumDetection() {
			if (_state == VNodeState.Master) {
				var now = _stopwatch.Elapsed;
				if (_subscriptions.Count >= _clusterSize / 2) // everything is ok
					_noQuorumTimestamp = TimeSpan.Zero;
				else {
					if (_noQuorumTimestamp == TimeSpan.Zero) {
						_noQuorumTimestamp = now;
						_noQuorumNotified = false;
					}

					if (!_noQuorumNotified && now - _noQuorumTimestamp > NoQuorumTimeout) {
						_publisher.Publish(new SystemMessage.NoQuorumMessage());
						_noQuorumNotified = true;
					}
				}
			}
		}

		private void ManageRoleAssignments(bool force = false) {
			if (force || _stopwatch.Elapsed - _lastRolesAssignmentTimestamp >= RoleAssignmentsInterval) {
				ManageRoleAssignments(_subscriptions.Values);
				_lastRolesAssignmentTimestamp = _stopwatch.Elapsed;
			}
		}

		private void ManageRoleAssignments(IEnumerable<ReplicaSubscription> subscribers) {
			var candidates = subscribers.Where(x => x.IsPromotable && x.State != ReplicaState.CatchingUp)
				.OrderByDescending(x => x.LogPosition)
				.ToArray();
			var masterCheckpoint = _db.Config.WriterCheckpoint.Read();

			int slavesCnt = 0;
			int laggedSlaves = 0;
			var desiredSlaveCount = _clusterSize - 1;

			for (int i = 0; i < candidates.Length; ++i) {
				var candidate = candidates[i];
				if (candidate.State == ReplicaState.Slave) {
					slavesCnt++;
					candidate.LagOccurences = i < desiredSlaveCount ? 0 : candidate.LagOccurences + 1;

					if (candidate.LagOccurences >= LagOccurencesThreshold
					    && masterCheckpoint - candidate.LogPosition >= SlaveLagThreshold) {
						++laggedSlaves;
					}
				}
			}

			int cloneIndex = 0;
			int slaveIndex = candidates.Length - 1;
			for (int k = slavesCnt; k < desiredSlaveCount; ++k) {
				// find next best clone
				while (cloneIndex < candidates.Length && candidates[cloneIndex].State != ReplicaState.Clone)
					cloneIndex++;

				// out of suitable clones - get out of here
				if (cloneIndex >= candidates.Length)
					break;

				// we need more slaves, even if there is lagging slaves
				var newSlave = candidates[cloneIndex];
				newSlave.State = ReplicaState.Slave;
				newSlave.LagOccurences = 0;
				newSlave.SendMessage(new ReplicationMessage.SlaveAssignment(_instanceId, newSlave.SubscriptionId));
				cloneIndex++;
			}

			for (int k = 0; k < laggedSlaves; ++k) {
				// find next best clone
				while (cloneIndex < candidates.Length && candidates[cloneIndex].State != ReplicaState.Clone)
					cloneIndex++;

				// find next worst slave
				while (slaveIndex >= 0 && candidates[slaveIndex].State != ReplicaState.Slave)
					slaveIndex--;

				// no more suitable clones - get out of here
				if (cloneIndex > slaveIndex)
					break;

				// we have enough slaves, but some of them are probably lagging behind
				Debug.Assert(slaveIndex >= 0);

				var oldSlave = candidates[slaveIndex];
				oldSlave.State = ReplicaState.Clone;
				oldSlave.LagOccurences = 0;
				oldSlave.SendMessage(new ReplicationMessage.CloneAssignment(_instanceId, oldSlave.SubscriptionId));
				slaveIndex--;

				var newSlave = candidates[cloneIndex];
				newSlave.State = ReplicaState.Slave;
				newSlave.LagOccurences = 0;
				newSlave.SendMessage(new ReplicationMessage.SlaveAssignment(_instanceId, newSlave.SubscriptionId));
				cloneIndex++;
			}
		}

		public QueueStats GetStatistics() {
			return _queueStats.GetStatistics(_subscriptions.Count);
		}

		private enum ReplicaState {
			CatchingUp,
			Clone,
			Slave
		}

		private class SendReplicationData {
		}

		private class ReplicaSubscription : IDisposable {
			public readonly byte[] DataBuffer = new byte[BulkSize];

			public Guid ConnectionId {
				get { return _connection.ConnectionId; }
			}

			public int SendQueueSize {
				get { return _connection.SendQueueSize; }
			}

			public bool IsConnectionClosed {
				get { return _connection.IsClosed; }
			}

			public readonly bool IsPromotable;
			public readonly IPEndPoint ReplicaEndPoint;
			public readonly Guid SubscriptionId;

			public TFChunkBulkReader BulkReader;
			public bool RawSend;

			public bool EOFSent;
			public long LogPosition;
			public long AckedLogPosition;

			public bool ShouldDispose;
			public ReplicaState State = ReplicaState.CatchingUp;
			public int LagOccurences;

			private readonly IPublisher _tcpSendPublisher;
			private readonly TcpConnectionManager _connection;

			public ReplicaSubscription(IPublisher tcpSendPublisher, TcpConnectionManager connection,
				Guid subscriptionId, IPEndPoint replicaEndPoint, bool isPromotable) {
				_tcpSendPublisher = tcpSendPublisher;
				_connection = connection;
				SubscriptionId = subscriptionId;
				ReplicaEndPoint = replicaEndPoint;
				IsPromotable = isPromotable;
			}

			public void SendMessage(Message msg) {
				_tcpSendPublisher.Publish(new TcpMessage.TcpSend(_connection, msg));
			}

			public void SendBadRequestAndClose(Guid correlationId, string message) {
				_connection.SendBadRequestAndClose(correlationId, message);
			}

			public override string ToString() {
				return string.Format(
					"Connection: {0:B}, ReplicaEndPoint: {1}, IsPromotable: {2}, RawSend: {3}, EOFSent: {4}, "
					+ "LogPosition: {5} (0x{5:X}), AckedLogPosition: {6} (0x{6:X}), State: {7}, LagOccurences: {8}, "
					+ "SubscriptionId: {9}, ShouldDispose: {10}",
					_connection.ConnectionId,
					ReplicaEndPoint,
					IsPromotable,
					RawSend,
					EOFSent,
					LogPosition,
					AckedLogPosition,
					State,
					LagOccurences,
					SubscriptionId,
					ShouldDispose);
			}

			public void Dispose() {
				_connection?.Stop("Closing replication subscription connection.");
				var bulkReader = Interlocked.Exchange(ref BulkReader, null);
				if (bulkReader != null)
					bulkReader.Release();
			}
		}
	}
}
