using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages {
	public static class ReplicationMessage {
		public class SubscribeReplica : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly long LogPosition;
			public readonly Guid ChunkId;
			public readonly EpochRecord[] LastEpochs;
			public readonly IPEndPoint ReplicaEndPoint;
			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;
			public readonly bool IsPromotable;

			public SubscribeReplica(long logPosition, Guid chunkId, EpochRecord[] lastEpochs,
				IPEndPoint replicaEndPoint,
				Guid masterId, Guid subscriptionId, bool isPromotable) {
				Ensure.Nonnegative(logPosition, "logPosition");
				Ensure.NotNull(lastEpochs, "lastEpochs");
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				Ensure.NotNull(replicaEndPoint, "replicaEndPoint");

				LogPosition = logPosition;
				ChunkId = chunkId;
				LastEpochs = lastEpochs;
				ReplicaEndPoint = replicaEndPoint;
				MasterId = masterId;
				SubscriptionId = subscriptionId;
				IsPromotable = isPromotable;
			}
		}

		public class AckLogPosition : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid SubscriptionId;
			public readonly long ReplicationLogPosition;

			public AckLogPosition(Guid subscriptionId, long replicationLogPosition) {
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				SubscriptionId = subscriptionId;
				ReplicationLogPosition = replicationLogPosition;
			}
		}

		public class ReplicaLogPositionAck : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid SubscriptionId;
			public readonly long ReplicationLogPosition;

			public ReplicaLogPositionAck(Guid subscriptionId, long replicationLogPosition) {
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");

				SubscriptionId = subscriptionId;
				ReplicationLogPosition = replicationLogPosition;
			}
		}

		public class ReplicaSubscriptionRequest : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly TcpConnectionManager Connection;

			public readonly long LogPosition;
			public readonly Guid ChunkId;
			public readonly Epoch[] LastEpochs;
			public readonly IPEndPoint ReplicaEndPoint;
			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;
			public readonly bool IsPromotable;

			public ReplicaSubscriptionRequest(Guid correlationId,
				IEnvelope envelope,
				TcpConnectionManager connection,
				long logPosition,
				Guid chunkId,
				Epoch[] lastEpochs,
				IPEndPoint replicaEndPoint,
				Guid masterId,
				Guid subscriptionId,
				bool isPromotable) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.NotNull(envelope, "envelope");
				Ensure.NotNull(connection, "connection");
				Ensure.Nonnegative(logPosition, "logPosition");
				Ensure.NotNull(lastEpochs, "lastEpochs");
				Ensure.NotNull(replicaEndPoint, "ReplicaEndPoint");
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");

				CorrelationId = correlationId;
				Envelope = envelope;
				Connection = connection;
				LogPosition = logPosition;
				ChunkId = chunkId;
				LastEpochs = lastEpochs;
				ReplicaEndPoint = replicaEndPoint;
				MasterId = masterId;
				SubscriptionId = subscriptionId;
				IsPromotable = isPromotable;
			}
		}

		public class ReconnectToMaster : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly VNodeInfo Master;
			public readonly Guid StateCorrelationId;

			public ReconnectToMaster(Guid stateCorrelationId, VNodeInfo master) {
				Ensure.NotEmptyGuid(stateCorrelationId, "stateCorrelationId");
				Ensure.NotNull(master, "master");
				StateCorrelationId = stateCorrelationId;
				Master = master;
			}
		}

		public interface IReplicationMessage {
			Guid MasterId { get; }
			Guid SubscriptionId { get; }
		}

		public class SubscribeToMaster : Message, IReplicationMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid StateCorrelationId;
			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;

			public SubscribeToMaster(Guid stateCorrelationId, Guid masterId, Guid subscriptionId) {
				Ensure.NotEmptyGuid(stateCorrelationId, "stateCorrelationId");
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");

				StateCorrelationId = stateCorrelationId;
				MasterId = masterId;
				SubscriptionId = subscriptionId;
			}
		}

		public class ReplicaSubscriptionRetry : Message, IReplicationMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;

			public ReplicaSubscriptionRetry(Guid masterId, Guid subscriptionId) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				MasterId = masterId;
				SubscriptionId = subscriptionId;
			}
		}

		public class ReplicaSubscribed : Message, IReplicationMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;
			public readonly long SubscriptionPosition;

			public readonly IPEndPoint MasterEndPoint;

			public ReplicaSubscribed(Guid masterId, Guid subscriptionId, long subscriptionPosition) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				Ensure.Nonnegative(subscriptionPosition, "subscriptionPosition");

				MasterId = masterId;
				SubscriptionId = subscriptionId;
				SubscriptionPosition = subscriptionPosition;
			}

			public ReplicaSubscribed(Guid masterId, Guid subscriptionId, long subscriptionPosition,
				IPEndPoint masterEndPoint) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				Ensure.Nonnegative(subscriptionPosition, "subscriptionPosition");
				Ensure.NotNull(masterEndPoint, "masterEndPoint");

				MasterId = masterId;
				SubscriptionId = subscriptionId;
				SubscriptionPosition = subscriptionPosition;
				MasterEndPoint = masterEndPoint;
			}
		}

		public class SlaveAssignment : Message, IReplicationMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;

			public SlaveAssignment(Guid masterId, Guid subscriptionId) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				MasterId = masterId;
				SubscriptionId = subscriptionId;
			}
		}

		public class CloneAssignment : Message, IReplicationMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;

			public CloneAssignment(Guid masterId, Guid subscriptionId) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				MasterId = masterId;
				SubscriptionId = subscriptionId;
			}
		}

		public class CreateChunk : Message, IReplicationMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;

			public readonly ChunkHeader ChunkHeader;
			public readonly int FileSize;
			public bool IsCompletedChunk;

			public CreateChunk(Guid masterId, Guid subscriptionId, ChunkHeader chunkHeader, int fileSize,
				bool isCompletedChunk) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				Ensure.NotNull(chunkHeader, "chunkHeader");

				MasterId = masterId;
				SubscriptionId = subscriptionId;
				ChunkHeader = chunkHeader;
				FileSize = fileSize;
				IsCompletedChunk = isCompletedChunk;
			}

			public override string ToString() {
				return string.Format(
					"CreateChunk message: MasterId: {0}, SubscriptionId: {1}, ChunkHeader: {2}, FileSize: {3}, IsCompletedChunk: {4}",
					MasterId, SubscriptionId, ChunkHeader, FileSize, IsCompletedChunk);
			}
		}

		public class RawChunkBulk : Message, IReplicationMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;

			public readonly int ChunkStartNumber;
			public readonly int ChunkEndNumber;
			public readonly int RawPosition;
			public readonly byte[] RawBytes;
			public readonly bool CompleteChunk;

			public RawChunkBulk(Guid masterId,
				Guid subscriptionId,
				int chunkStartNumber, int chunkEndNumber,
				int rawPosition, byte[] rawBytes,
				bool completeChunk) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				Ensure.NotNull(rawBytes, "rawBytes");
				Ensure.Positive(rawBytes.Length, "rawBytes.Length"); // we should never send empty array, NEVER

				MasterId = masterId;
				SubscriptionId = subscriptionId;
				ChunkStartNumber = chunkStartNumber;
				ChunkEndNumber = chunkEndNumber;
				RawPosition = rawPosition;
				RawBytes = rawBytes;
				CompleteChunk = completeChunk;
			}

			public override string ToString() {
				return string.Format(
					"RawChunkBulk message: MasterId: {0}, SubscriptionId: {1}, ChunkStartNumber: {2}, ChunkEndNumber: {3}, RawPosition: {4}, RawBytes length: {5}, CompleteChunk: {6}",
					MasterId, SubscriptionId,
					ChunkStartNumber, ChunkEndNumber, RawPosition, RawBytes.Length, CompleteChunk);
			}
		}

		public class DataChunkBulk : Message, IReplicationMessage, StorageMessage.IFlushableMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			Guid IReplicationMessage.MasterId {
				get { return MasterId; }
			}

			Guid IReplicationMessage.SubscriptionId {
				get { return SubscriptionId; }
			}

			public readonly Guid MasterId;
			public readonly Guid SubscriptionId;

			public readonly int ChunkStartNumber;
			public readonly int ChunkEndNumber;
			public readonly long SubscriptionPosition;
			public readonly byte[] DataBytes;
			public readonly bool CompleteChunk;

			public DataChunkBulk(Guid masterId,
				Guid subscriptionId,
				int chunkStartNumber,
				int chunkEndNumber,
				long subscriptionPosition,
				byte[] dataBytes,
				bool completeChunk) {
				Ensure.NotEmptyGuid(masterId, "masterId");
				Ensure.NotEmptyGuid(subscriptionId, "subscriptionId");
				Ensure.NotNull(dataBytes, "rawBytes");
				Ensure.Nonnegative(dataBytes.Length,
					"dataBytes.Length"); // we CAN send empty dataBytes array here, unlike as with completed chunks

				MasterId = masterId;
				SubscriptionId = subscriptionId;
				ChunkStartNumber = chunkStartNumber;
				ChunkEndNumber = chunkEndNumber;
				SubscriptionPosition = subscriptionPosition;
				DataBytes = dataBytes;
				CompleteChunk = completeChunk;
			}

			public override string ToString() {
				return string.Format(
					"DataChunkBulk message: MasterId: {0}, SubscriptionId: {1}, ChunkStartNumber: {2}, ChunkEndNumber: {3}, SubscriptionPosition: {4}, DataBytes length: {5}, CompleteChunk: {6}",
					MasterId, SubscriptionId, ChunkStartNumber, ChunkEndNumber,
					SubscriptionPosition, DataBytes.Length, CompleteChunk);
			}
		}

		public class GetReplicationStats : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public IEnvelope Envelope;

			public GetReplicationStats(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		public class GetReplicationStatsCompleted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public List<ReplicationMessage.ReplicationStats> ReplicationStats;

			public GetReplicationStatsCompleted(List<ReplicationMessage.ReplicationStats> replicationStats) {
				ReplicationStats = replicationStats;
			}
		}

		public class ReplicationStats {
			public Guid SubscriptionId { get; private set; }
			public Guid ConnectionId { get; private set; }
			public string SubscriptionEndpoint { get; private set; }
			public long TotalBytesSent { get; private set; }
			public long TotalBytesReceived { get; private set; }
			public int PendingSendBytes { get; private set; }
			public int PendingReceivedBytes { get; private set; }
			public int SendQueueSize { get; private set; }

			public ReplicationStats(Guid subscriptionId, Guid connectionId, string subscriptionEndpoint,
				int sendQueueSize,
				long totalBytesSent, long totalBytesReceived, int pendingSendBytes, int pendingReceivedBytes) {
				SubscriptionId = subscriptionId;
				ConnectionId = connectionId;
				SubscriptionEndpoint = subscriptionEndpoint;
				SendQueueSize = sendQueueSize;
				TotalBytesSent = totalBytesSent;
				TotalBytesReceived = totalBytesReceived;
				PendingSendBytes = pendingSendBytes;
				PendingReceivedBytes = pendingReceivedBytes;
			}
		}
	}
}
