using System;
using System.IO;
using System.Linq;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Core.Services.Transport.Tcp {
	public class InternalTcpDispatcher : ClientWriteTcpDispatcher {
		public InternalTcpDispatcher(TimeSpan writeTimeout) : base(writeTimeout) {
			AddUnwrapper(TcpCommand.LeaderReplicatedTo, UnwrapReplicatedTo, ClientVersion.V2);
			AddWrapper<ReplicationTrackingMessage.ReplicatedTo>(WrapReplicatedTo, ClientVersion.V2);

			AddUnwrapper(TcpCommand.SubscribeReplica, UnwrapReplicaSubscriptionRequest, ClientVersion.V2);
			AddWrapper<ReplicationMessage.SubscribeReplica>(WrapSubscribeReplica, ClientVersion.V2);
			AddUnwrapper(TcpCommand.ReplicaLogPositionAck, UnwrapReplicaLogPositionAck, ClientVersion.V2);
			AddWrapper<ReplicationMessage.AckLogPosition>(WrapAckLogPosition, ClientVersion.V2);
			AddUnwrapper(TcpCommand.CreateChunk, UnwrapCreateChunk, ClientVersion.V2);
			AddWrapper<ReplicationMessage.CreateChunk>(WrapCreateChunk, ClientVersion.V2);
			AddUnwrapper(TcpCommand.RawChunkBulk, UnwrapRawChunkBulk, ClientVersion.V2);
			AddWrapper<ReplicationMessage.RawChunkBulk>(WrapRawChunkBulk, ClientVersion.V2);
			AddUnwrapper(TcpCommand.DataChunkBulk, UnwrapDataChunkBulk, ClientVersion.V2);
			AddWrapper<ReplicationMessage.DataChunkBulk>(WrapDataChunkBulk, ClientVersion.V2);
			AddUnwrapper(TcpCommand.ReplicaSubscriptionRetry, UnwrapReplicaSubscriptionRetry, ClientVersion.V2);
			AddWrapper<ReplicationMessage.ReplicaSubscriptionRetry>(WrapReplicaSubscriptionRetry, ClientVersion.V2);
			AddUnwrapper(TcpCommand.ReplicaSubscribed, UnwrapReplicaSubscribed, ClientVersion.V2);
			AddWrapper<ReplicationMessage.ReplicaSubscribed>(WrapReplicaSubscribed, ClientVersion.V2);

			AddUnwrapper(TcpCommand.FollowerAssignment, UnwrapFollowerAssignment, ClientVersion.V2);
			AddWrapper<ReplicationMessage.FollowerAssignment>(WrapFollowerAssignment, ClientVersion.V2);
			AddUnwrapper(TcpCommand.CloneAssignment, UnwrapCloneAssignment, ClientVersion.V2);
			AddWrapper<ReplicationMessage.CloneAssignment>(WrapCloneAssignment, ClientVersion.V2);
			AddUnwrapper(TcpCommand.DropSubscription, UnwrapDropSubscription, ClientVersion.V2);
			AddWrapper<ReplicationMessage.DropSubscription>(WrapDropSubscription, ClientVersion.V2);
		}
		

		private TcpPackage WrapReplicatedTo(ReplicationTrackingMessage.ReplicatedTo msg) {
			var dto = new ReplicationMessageDto.ReplicatedTo(msg.LogPosition);
			return new TcpPackage(TcpCommand.LeaderReplicatedTo, Guid.NewGuid(), dto.Serialize());
		}

		private static ReplicationTrackingMessage.LeaderReplicatedTo UnwrapReplicatedTo(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.ReplicatedTo>();
			return new ReplicationTrackingMessage.LeaderReplicatedTo(dto.LogPosition);
		}

		private ReplicationMessage.ReplicaSubscriptionRequest UnwrapReplicaSubscriptionRequest(TcpPackage package,
			IEnvelope envelope, TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.SubscribeReplica>();
			var vnodeTcpEndPoint = new DnsEndPoint(Helper.UTF8NoBom.GetString(dto.Ip), dto.Port);
			var lastEpochs = dto.LastEpochs.Safe()
				.Select(x => new Epoch(x.EpochPosition, x.EpochNumber, new Guid(x.EpochId))).ToArray();
			return new ReplicationMessage.ReplicaSubscriptionRequest(package.CorrelationId,
				envelope,
				connection,
				dto.LogPosition,
				new Guid(dto.ChunkId),
				lastEpochs,
				vnodeTcpEndPoint,
				new Guid(dto.LeaderId),
				new Guid(dto.SubscriptionId),
				dto.IsPromotable);
		}

		private TcpPackage WrapSubscribeReplica(ReplicationMessage.SubscribeReplica msg) {
			var epochs = msg.LastEpochs.Select(x =>
				new ReplicationMessageDto.Epoch(x.EpochPosition, x.EpochNumber, x.EpochId.ToByteArray())).ToArray();
			var dto = new ReplicationMessageDto.SubscribeReplica(msg.LogPosition,
				msg.ChunkId.ToByteArray(),
				epochs,
				Helper.UTF8NoBom.GetBytes(msg.ReplicaEndPoint.GetHost()),
				msg.ReplicaEndPoint.GetPort(),
				msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray(),
				msg.IsPromotable);
			return new TcpPackage(TcpCommand.SubscribeReplica, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.ReplicaLogPositionAck UnwrapReplicaLogPositionAck(TcpPackage package,
			IEnvelope envelope, TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.ReplicaLogPositionAck>();
			return new ReplicationMessage.ReplicaLogPositionAck(new Guid(dto.SubscriptionId),
				dto.ReplicationLogPosition);
		}

		private TcpPackage WrapAckLogPosition(ReplicationMessage.AckLogPosition msg) {
			var dto = new ReplicationMessageDto.ReplicaLogPositionAck(msg.SubscriptionId.ToByteArray(),
				msg.ReplicationLogPosition);
			return new TcpPackage(TcpCommand.ReplicaLogPositionAck, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.CreateChunk UnwrapCreateChunk(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.CreateChunk>();
			ChunkHeader chunkHeader;
			using (var memStream = new MemoryStream(dto.ChunkHeaderBytes)) {
				chunkHeader = ChunkHeader.FromStream(memStream);
			}

			return new ReplicationMessage.CreateChunk(new Guid(dto.LeaderId), new Guid(dto.SubscriptionId), chunkHeader,
				dto.FileSize, dto.IsCompletedChunk);
		}

		private TcpPackage WrapCreateChunk(ReplicationMessage.CreateChunk msg) {
			var dto = new ReplicationMessageDto.CreateChunk(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray(),
				msg.ChunkHeader.AsByteArray(),
				msg.FileSize,
				msg.IsCompletedChunk);
			return new TcpPackage(TcpCommand.CreateChunk, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.RawChunkBulk UnwrapRawChunkBulk(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.RawChunkBulk>();
			return new ReplicationMessage.RawChunkBulk(new Guid(dto.LeaderId),
				new Guid(dto.SubscriptionId),
				dto.ChunkStartNumber,
				dto.ChunkEndNumber,
				dto.RawPosition,
				dto.RawBytes,
				dto.CompleteChunk);
		}

		private TcpPackage WrapRawChunkBulk(ReplicationMessage.RawChunkBulk msg) {
			var dto = new ReplicationMessageDto.RawChunkBulk(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray(),
				msg.ChunkStartNumber,
				msg.ChunkEndNumber,
				msg.RawPosition,
				msg.RawBytes,
				msg.CompleteChunk);
			return new TcpPackage(TcpCommand.RawChunkBulk, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.DataChunkBulk UnwrapDataChunkBulk(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.DataChunkBulk>();
			return new ReplicationMessage.DataChunkBulk(new Guid(dto.LeaderId),
				new Guid(dto.SubscriptionId),
				dto.ChunkStartNumber,
				dto.ChunkEndNumber,
				dto.SubscriptionPosition,
				dto.DataBytes,
				dto.CompleteChunk);
		}

		private TcpPackage WrapDataChunkBulk(ReplicationMessage.DataChunkBulk msg) {
			var dto = new ReplicationMessageDto.DataChunkBulk(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray(),
				msg.ChunkStartNumber,
				msg.ChunkEndNumber,
				msg.SubscriptionPosition,
				msg.DataBytes,
				msg.CompleteChunk);
			return new TcpPackage(TcpCommand.DataChunkBulk, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.ReplicaSubscriptionRetry UnwrapReplicaSubscriptionRetry(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.ReplicaSubscriptionRetry>();
			return new ReplicationMessage.ReplicaSubscriptionRetry(new Guid(dto.LeaderId),
				new Guid(dto.SubscriptionId));
		}

		private TcpPackage WrapReplicaSubscriptionRetry(ReplicationMessage.ReplicaSubscriptionRetry msg) {
			var dto = new ReplicationMessageDto.ReplicaSubscriptionRetry(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray());
			return new TcpPackage(TcpCommand.ReplicaSubscriptionRetry, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.ReplicaSubscribed UnwrapReplicaSubscribed(TcpPackage package, IEnvelope envelope,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.ReplicaSubscribed>();
			return new ReplicationMessage.ReplicaSubscribed(new Guid(dto.LeaderId),
				new Guid(dto.SubscriptionId),
				dto.SubscriptionPosition,
				connection.RemoteEndPoint);
		}

		private TcpPackage WrapReplicaSubscribed(ReplicationMessage.ReplicaSubscribed msg) {
			var dto = new ReplicationMessageDto.ReplicaSubscribed(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray(),
				msg.SubscriptionPosition);
			return new TcpPackage(TcpCommand.ReplicaSubscribed, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.FollowerAssignment UnwrapFollowerAssignment(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.FollowerAssignment>();
			return new ReplicationMessage.FollowerAssignment(new Guid(dto.LeaderId), new Guid(dto.SubscriptionId));
		}

		private TcpPackage WrapFollowerAssignment(ReplicationMessage.FollowerAssignment msg) {
			var dto = new ReplicationMessageDto.FollowerAssignment(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray());
			return new TcpPackage(TcpCommand.FollowerAssignment, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.CloneAssignment UnwrapCloneAssignment(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.CloneAssignment>();
			return new ReplicationMessage.CloneAssignment(new Guid(dto.LeaderId), new Guid(dto.SubscriptionId));
		}

		private TcpPackage WrapCloneAssignment(ReplicationMessage.CloneAssignment msg) {
			var dto = new ReplicationMessageDto.CloneAssignment(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray());
			return new TcpPackage(TcpCommand.CloneAssignment, Guid.NewGuid(), dto.Serialize());
		}

		private ReplicationMessage.DropSubscription UnwrapDropSubscription(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<ReplicationMessageDto.CloneAssignment>();
			return new ReplicationMessage.DropSubscription(new Guid(dto.LeaderId), new Guid(dto.SubscriptionId));
		}

		private TcpPackage WrapDropSubscription(ReplicationMessage.DropSubscription msg) {
			var dto = new ReplicationMessageDto.DropSubscription(msg.LeaderId.ToByteArray(),
				msg.SubscriptionId.ToByteArray());
			return new TcpPackage(TcpCommand.DropSubscription, Guid.NewGuid(), dto.Serialize());
		}
	}
}
