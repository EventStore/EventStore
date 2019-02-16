using EventStore.Common.Utils;
using ProtoBuf;

namespace EventStore.Core.Messages {
	public static class ReplicationMessageDto {
		[ProtoContract]
		public class PrepareAck {
			[ProtoMember(1)] public long LogPosition { get; set; }

			[ProtoMember(2)] public byte Flags { get; set; }

			public PrepareAck() {
			}

			public PrepareAck(long logPosition, byte flags) {
				LogPosition = logPosition;
				Flags = flags;
			}
		}

		[ProtoContract]
		public class CommitAck {
			[ProtoMember(1)] public long LogPosition { get; set; }

			[ProtoMember(2)] public long TransactionPosition { get; set; }

			[ProtoMember(3)] public long FirstEventNumber { get; set; }

			[ProtoMember(4)] public long LastEventNumber { get; set; }

			public CommitAck() {
			}

			public CommitAck(long logPosition, long transactionPosition, long firstEventNumber, long lastEventNumber) {
				LogPosition = logPosition;
				TransactionPosition = transactionPosition;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
			}
		}

		[ProtoContract]
		public class Epoch {
			[ProtoMember(1)] public long EpochPosition { get; set; }

			[ProtoMember(2)] public int EpochNumber { get; set; }

			[ProtoMember(3)] public byte[] EpochId { get; set; }

			public Epoch() {
			}

			public Epoch(long epochPosition, int epochNumber, byte[] epochId) {
				EpochPosition = epochPosition;
				EpochNumber = epochNumber;
				EpochId = epochId;
			}
		}

		[ProtoContract]
		public class SubscribeReplica {
			[ProtoMember(1)] public long LogPosition { get; set; }

			[ProtoMember(2)] public byte[] ChunkId { get; set; }

			[ProtoMember(3)] public Epoch[] LastEpochs { get; set; }

			[ProtoMember(4)] public byte[] Ip { get; set; }

			[ProtoMember(5)] public int Port { get; set; }

			[ProtoMember(6)] public byte[] MasterId { get; set; }

			[ProtoMember(7)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(8)] public bool IsPromotable { get; set; }

			public SubscribeReplica() {
			}

			public SubscribeReplica(long logPosition, byte[] chunkId, Epoch[] lastEpochs, byte[] ip, int port,
				byte[] masterId, byte[] subscriptionId, bool isPromotable) {
				LogPosition = logPosition;
				ChunkId = chunkId;
				LastEpochs = lastEpochs;

				Ip = ip;
				Port = port;
				MasterId = masterId;
				SubscriptionId = subscriptionId;
				IsPromotable = isPromotable;
			}
		}

		[ProtoContract]
		public class ReplicaSubscriptionRetry {
			[ProtoMember(1)] public byte[] MasterId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			public ReplicaSubscriptionRetry() {
			}

			public ReplicaSubscriptionRetry(byte[] masterId, byte[] subscriptionId) {
				Ensure.NotNull(masterId, "masterId");
				Ensure.NotNull(subscriptionId, "subscriptionId");

				MasterId = masterId;
				SubscriptionId = subscriptionId;
			}
		}

		[ProtoContract]
		public class ReplicaSubscribed {
			[ProtoMember(1)] public byte[] MasterId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public long SubscriptionPosition { get; set; }

			public ReplicaSubscribed() {
			}

			public ReplicaSubscribed(byte[] masterId, byte[] subscriptionId, long subscriptionPosition) {
				Ensure.NotNull(masterId, "masterId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
				Ensure.Nonnegative(subscriptionPosition, "subscriptionPosition");

				MasterId = masterId;
				SubscriptionId = subscriptionId;
				SubscriptionPosition = subscriptionPosition;
			}
		}

		[ProtoContract]
		public class ReplicaLogPositionAck {
			[ProtoMember(1)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(2)] public long ReplicationLogPosition { get; set; }

			public ReplicaLogPositionAck() {
			}

			public ReplicaLogPositionAck(byte[] subscriptionId, long replicationLogPosition) {
				SubscriptionId = subscriptionId;
				ReplicationLogPosition = replicationLogPosition;
			}
		}

		[ProtoContract]
		public class CreateChunk {
			[ProtoMember(1)] public byte[] MasterId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public byte[] ChunkHeaderBytes { get; set; }

			[ProtoMember(4)] public int FileSize { get; set; }

			[ProtoMember(5)] public bool IsCompletedChunk { get; set; }

			public CreateChunk() {
			}

			public CreateChunk(byte[] masterId, byte[] subscriptionId, byte[] chunkHeaderBytes, int fileSize,
				bool isCompletedChunk) {
				Ensure.NotNull(masterId, "masterId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
				Ensure.NotNull(chunkHeaderBytes, "chunkHeaderBytes");

				MasterId = masterId;
				SubscriptionId = subscriptionId;
				ChunkHeaderBytes = chunkHeaderBytes;
				FileSize = fileSize;
				IsCompletedChunk = isCompletedChunk;
			}
		}

		[ProtoContract]
		public class RawChunkBulk {
			[ProtoMember(1)] public byte[] MasterId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public int ChunkStartNumber { get; set; }

			[ProtoMember(4)] public int ChunkEndNumber { get; set; }

			[ProtoMember(5)] public int RawPosition { get; set; }

			[ProtoMember(6)] public byte[] RawBytes { get; set; }

			[ProtoMember(7)] public bool CompleteChunk { get; set; }

			public RawChunkBulk() {
			}

			public RawChunkBulk(byte[] masterId,
				byte[] subscriptionId,
				int chunkStartNumber,
				int chunkEndNumber,
				int rawPosition,
				byte[] rawBytes,
				bool completeChunk) {
				Ensure.NotNull(masterId, "masterId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
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
		}

		[ProtoContract]
		public class DataChunkBulk {
			[ProtoMember(1)] public byte[] MasterId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public int ChunkStartNumber { get; set; }

			[ProtoMember(4)] public int ChunkEndNumber { get; set; }

			[ProtoMember(5)] public long SubscriptionPosition { get; set; }

			[ProtoMember(6)] public byte[] DataBytes { get; set; }

			[ProtoMember(7)] public bool CompleteChunk { get; set; }

			public DataChunkBulk() {
			}

			public DataChunkBulk(byte[] masterId,
				byte[] subscriptionId,
				int chunkStartNumber,
				int chunkEndNumber,
				long subscriptionPosition,
				byte[] dataBytes,
				bool completeChunk) {
				Ensure.NotNull(masterId, "masterId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
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
		}

		[ProtoContract]
		public class SlaveAssignment {
			[ProtoMember(1)] public byte[] MasterId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			public SlaveAssignment() {
			}

			public SlaveAssignment(byte[] masterId, byte[] subscriptionId) {
				MasterId = masterId;
				SubscriptionId = subscriptionId;
			}
		}

		[ProtoContract]
		public class CloneAssignment {
			[ProtoMember(1)] public byte[] MasterId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			public CloneAssignment() {
			}

			public CloneAssignment(byte[] masterId, byte[] subscriptionId) {
				MasterId = masterId;
				SubscriptionId = subscriptionId;
			}
		}
	}
}
