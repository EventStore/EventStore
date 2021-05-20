using System;
using EventStore.Common.Utils;
using ProtoBuf;

namespace EventStore.Core.Messages {
	public static class ReplicationMessageDto {
		[ProtoContract]
		public class ReplicaLogWrite {
			[ProtoMember(1)] public long LogPosition { get; set; }

			[ProtoMember(2)] public byte[] ReplicaId { get; set; }

			public ReplicaLogWrite() {
			}

			public ReplicaLogWrite(long logPosition, byte[] replicaId) {
				LogPosition = logPosition;
				ReplicaId = replicaId;
			}
		}

		[ProtoContract]
		public class ReplicatedTo {
			[ProtoMember(1)] public long LogPosition { get; set; }

			public ReplicatedTo() {
			}

			public ReplicatedTo(long logPosition) {
				LogPosition = logPosition;
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

			[ProtoMember(6)] public byte[] LeaderId { get; set; }

			[ProtoMember(7)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(8)] public bool IsPromotable { get; set; }

			public SubscribeReplica() {
			}

			public SubscribeReplica(long logPosition, byte[] chunkId, Epoch[] lastEpochs, byte[] ip, int port,
				byte[] leaderId, byte[] subscriptionId, bool isPromotable) {
				LogPosition = logPosition;
				ChunkId = chunkId;
				LastEpochs = lastEpochs;

				Ip = ip;
				Port = port;
				LeaderId = leaderId;
				SubscriptionId = subscriptionId;
				IsPromotable = isPromotable;
			}
		}

		[ProtoContract]
		public class ReplicaSubscriptionRetry {
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			public ReplicaSubscriptionRetry() {
			}

			public ReplicaSubscriptionRetry(byte[] leaderId, byte[] subscriptionId) {
				Ensure.NotNull(leaderId, "leaderId");
				Ensure.NotNull(subscriptionId, "subscriptionId");

				LeaderId = leaderId;
				SubscriptionId = subscriptionId;
			}
		}

		[ProtoContract]
		public class ReplicaSubscribed {
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public long SubscriptionPosition { get; set; }

			public ReplicaSubscribed() {
			}

			public ReplicaSubscribed(byte[] leaderId, byte[] subscriptionId, long subscriptionPosition) {
				Ensure.NotNull(leaderId, "leaderId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
				Ensure.Nonnegative(subscriptionPosition, "subscriptionPosition");

				LeaderId = leaderId;
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
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public byte[] ChunkHeaderBytes { get; set; }

			[ProtoMember(4)] public int FileSize { get; set; }

			[ProtoMember(5)] public bool IsCompletedChunk { get; set; }

			public CreateChunk() {
			}

			public CreateChunk(byte[] leaderId, byte[] subscriptionId, byte[] chunkHeaderBytes, int fileSize,
				bool isCompletedChunk) {
				Ensure.NotNull(leaderId, "leaderId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
				Ensure.NotNull(chunkHeaderBytes, "chunkHeaderBytes");

				LeaderId = leaderId;
				SubscriptionId = subscriptionId;
				ChunkHeaderBytes = chunkHeaderBytes;
				FileSize = fileSize;
				IsCompletedChunk = isCompletedChunk;
			}
		}

		[ProtoContract]
		public class RawChunkBulk {
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public int ChunkStartNumber { get; set; }

			[ProtoMember(4)] public int ChunkEndNumber { get; set; }

			[ProtoMember(5)] public int RawPosition { get; set; }

			[ProtoMember(6)] public byte[] RawBytes { get; set; }

			[ProtoMember(7)] public bool CompleteChunk { get; set; }

			public RawChunkBulk() {
			}

			public RawChunkBulk(byte[] leaderId,
				byte[] subscriptionId,
				int chunkStartNumber,
				int chunkEndNumber,
				int rawPosition,
				byte[] rawBytes,
				bool completeChunk) {
				Ensure.NotNull(leaderId, "leaderId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
				Ensure.NotNull(rawBytes, "rawBytes");
				Ensure.Positive(rawBytes.Length, "rawBytes.Length"); // we should never send empty array, NEVER

				LeaderId = leaderId;
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
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			[ProtoMember(3)] public int ChunkStartNumber { get; set; }

			[ProtoMember(4)] public int ChunkEndNumber { get; set; }

			[ProtoMember(5)] public long SubscriptionPosition { get; set; }

			[ProtoMember(6)] public byte[] DataBytes { get; set; }

			[ProtoMember(7)] public bool CompleteChunk { get; set; }

			public DataChunkBulk() {
			}

			public DataChunkBulk(byte[] leaderId,
				byte[] subscriptionId,
				int chunkStartNumber,
				int chunkEndNumber,
				long subscriptionPosition,
				byte[] dataBytes,
				bool completeChunk) {
				Ensure.NotNull(leaderId, "leaderId");
				Ensure.NotNull(subscriptionId, "subscriptionId");
				Ensure.NotNull(dataBytes, "rawBytes");
				Ensure.Nonnegative(dataBytes.Length,
					"dataBytes.Length"); // we CAN send empty dataBytes array here, unlike as with completed chunks

				LeaderId = leaderId;
				SubscriptionId = subscriptionId;
				ChunkStartNumber = chunkStartNumber;
				ChunkEndNumber = chunkEndNumber;
				SubscriptionPosition = subscriptionPosition;
				DataBytes = dataBytes;
				CompleteChunk = completeChunk;
			}
		}

		[ProtoContract]
		public class FollowerAssignment {
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			public FollowerAssignment() {
			}

			public FollowerAssignment(byte[] leaderId, byte[] subscriptionId) {
				LeaderId = leaderId;
				SubscriptionId = subscriptionId;
			}
		}

		[ProtoContract]
		public class CloneAssignment {
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			public CloneAssignment() {
			}

			public CloneAssignment(byte[] leaderId, byte[] subscriptionId) {
				LeaderId = leaderId;
				SubscriptionId = subscriptionId;
			}
		}

		[ProtoContract]
		public class DropSubscription {
			[ProtoMember(1)] public byte[] LeaderId { get; set; }

			[ProtoMember(2)] public byte[] SubscriptionId { get; set; }

			public DropSubscription() {
			}

			public DropSubscription(byte[] leaderId, byte[] subscriptionId) {
				LeaderId = leaderId;
				SubscriptionId = subscriptionId;
			}
		}
	}
}
