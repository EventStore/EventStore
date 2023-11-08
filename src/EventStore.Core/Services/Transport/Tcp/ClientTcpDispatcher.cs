using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using EventStore.Client.Messages;
using EventStore.Common.Utils;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Util;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Core.Services.Transport.Tcp {
	public class ClientTcpDispatcher : ClientWriteTcpDispatcher {
		public ClientTcpDispatcher(int writeTimeoutMs)
			: this(TimeSpan.FromMilliseconds(writeTimeoutMs)) {
		}

		public ClientTcpDispatcher(TimeSpan writeTimeout) : base(writeTimeout) {
			AddUnwrapper(TcpCommand.Ping, UnwrapPing, ClientVersion.V2);
			AddWrapper<TcpMessage.PongMessage>(WrapPong, ClientVersion.V2);

			AddUnwrapper(TcpCommand.IdentifyClient, UnwrapIdentifyClient, ClientVersion.V2);

			AddUnwrapper(TcpCommand.SubscribeToStream, UnwrapSubscribeToStream, ClientVersion.V2);
			AddUnwrapper(TcpCommand.UnsubscribeFromStream, UnwrapUnsubscribeFromStream, ClientVersion.V2);

			AddWrapper<ClientMessage.CheckpointReached>(WrapCheckpointReached, ClientVersion.V2);

			AddWrapper<ClientMessage.SubscriptionConfirmation>(WrapSubscribedToStream, ClientVersion.V2);
			AddWrapper<ClientMessage.StreamEventAppeared>(WrapStreamEventAppeared, ClientVersion.V2);
			AddWrapper<ClientMessage.SubscriptionDropped>(WrapSubscriptionDropped, ClientVersion.V2);

			AddUnwrapper(TcpCommand.ScavengeDatabase, UnwrapScavengeDatabase, ClientVersion.V2);
			AddWrapper<ClientMessage.ScavengeDatabaseInProgressResponse>(WrapScavengeDatabaseResponse, ClientVersion.V2);
			AddWrapper<ClientMessage.ScavengeDatabaseStartedResponse>(WrapScavengeDatabaseResponse, ClientVersion.V2);
			AddWrapper<ClientMessage.ScavengeDatabaseUnauthorizedResponse>(WrapScavengeDatabaseResponse, ClientVersion.V2);

			AddWrapper<ClientMessage.NotHandled>(WrapNotHandled, ClientVersion.V2);
			AddUnwrapper(TcpCommand.NotHandled, UnwrapNotHandled, ClientVersion.V2);

			AddWrapper<TcpMessage.NotAuthenticated>(WrapNotAuthenticated, ClientVersion.V2);
			AddWrapper<TcpMessage.Authenticated>(WrapAuthenticated, ClientVersion.V2);
		}


		private TcpPackage WrapCheckpointReached(ClientMessage.CheckpointReached msg) {
			var dto = new CheckpointReached(msg.Position.Value.CommitPosition,
				msg.Position.Value.PreparePosition);
			return new TcpPackage(TcpCommand.CheckpointReached, msg.CorrelationId, dto.Serialize());
		}

		private static Message UnwrapPing(TcpPackage package, IEnvelope envelope) {
			var data = new byte[package.Data.Count];
			Buffer.BlockCopy(package.Data.Array, package.Data.Offset, data, 0, package.Data.Count);
			var pongMessage = new TcpMessage.PongMessage(package.CorrelationId, data);
			envelope.ReplyWith(pongMessage);
			return pongMessage;
		}

		private static Message UnwrapIdentifyClient(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<IdentifyClient>();
			if (dto == null) return null;

			return new ClientMessage.IdentifyClient(package.CorrelationId, dto.Version, dto.ConnectionName);
		}

		private static TcpPackage WrapPong(TcpMessage.PongMessage message) {
			return new TcpPackage(TcpCommand.Pong, message.CorrelationId, message.Payload);
		}

		private static Client.Messages.ResolvedEvent[] ConvertToResolvedEvents(ResolvedEvent[] events) {
			var result = new Client.Messages.ResolvedEvent[events.Length];
			for (int i = 0; i < events.Length; ++i) {
				result[i] = new Client.Messages.ResolvedEvent(events[i]);
			}

			return result;
		}

		private ClientMessage.SubscribeToStream UnwrapSubscribeToStream(TcpPackage package,
			IEnvelope envelope,
			ClaimsPrincipal user,
			TcpConnectionManager connection) {
			var dto = package.Data.Deserialize<SubscribeToStream>();
			if (dto == null) return null;
			return new ClientMessage.SubscribeToStream(Guid.NewGuid(), package.CorrelationId, envelope,
				connection.ConnectionId, dto.EventStreamId, dto.ResolveLinkTos, user);
		}

		private ClientMessage.UnsubscribeFromStream UnwrapUnsubscribeFromStream(TcpPackage package, IEnvelope envelope,
			ClaimsPrincipal user) {
			var dto = package.Data.Deserialize<UnsubscribeFromStream>();
			if (dto == null) return null;
			return new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), package.CorrelationId, envelope, user);
		}

		private TcpPackage WrapSubscribedToStream(ClientMessage.SubscriptionConfirmation msg) {
			var dto = new SubscriptionConfirmation(msg.LastIndexedPosition, msg.LastEventNumber ?? 0);
			return new TcpPackage(TcpCommand.SubscriptionConfirmation, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapStreamEventAppeared(ClientMessage.StreamEventAppeared msg) {
			var dto = new StreamEventAppeared(new Client.Messages.ResolvedEvent(msg.Event));
			return new TcpPackage(TcpCommand.StreamEventAppeared, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapSubscriptionDropped(ClientMessage.SubscriptionDropped msg) {
			var dto = new SubscriptionDropped(
				(SubscriptionDropped.Types.SubscriptionDropReason)msg.Reason);
			return new TcpPackage(TcpCommand.SubscriptionDropped, msg.CorrelationId, dto.Serialize());
		}

		private ClientMessage.ScavengeDatabase UnwrapScavengeDatabase(TcpPackage package, IEnvelope envelope,
			ClaimsPrincipal user) {
			return new ClientMessage.ScavengeDatabase(envelope, package.CorrelationId, user, 0, 1, null, null, false);
		}
		
		private TcpPackage WrapScavengeDatabaseResponse(Message msg) {
			ScavengeDatabaseResponse.Types.ScavengeResult result;
			string scavengeId;
			Guid correlationId;
			
			switch (msg) {
				case ClientMessage.ScavengeDatabaseStartedResponse startedResponse:
					result = ScavengeDatabaseResponse.Types.ScavengeResult.Started;
					scavengeId = startedResponse.ScavengeId;
					correlationId = startedResponse.CorrelationId;
					break;
				case ClientMessage.ScavengeDatabaseInProgressResponse inProgressResponse:
					result = ScavengeDatabaseResponse.Types.ScavengeResult.InProgress;
					scavengeId = inProgressResponse.ScavengeId;
					correlationId = inProgressResponse.CorrelationId;
					break;
				case ClientMessage.ScavengeDatabaseUnauthorizedResponse unauthorizedResponse:
					result = ScavengeDatabaseResponse.Types.ScavengeResult.Unauthorized;
					scavengeId = unauthorizedResponse.ScavengeId;
					correlationId = unauthorizedResponse.CorrelationId;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			var dto = new ScavengeDatabaseResponse(result, scavengeId);
			return new TcpPackage(TcpCommand.ScavengeDatabaseResponse, correlationId, dto.Serialize());
		}

		private ClientMessage.NotHandled UnwrapNotHandled(TcpPackage package, IEnvelope envelope) {
			var dto = package.Data.Deserialize<NotHandled>();
			if (dto == null) return null;
			var reason = dto.Reason switch {
				NotHandled.Types.NotHandledReason.NotReady => ClientMessage.NotHandled.Types.NotHandledReason.NotReady,
				NotHandled.Types.NotHandledReason.TooBusy => ClientMessage.NotHandled.Types.NotHandledReason.TooBusy,
				NotHandled.Types.NotHandledReason.NotLeader => ClientMessage.NotHandled.Types.NotHandledReason.NotLeader,
				NotHandled.Types.NotHandledReason.IsReadOnly => ClientMessage.NotHandled.Types.NotHandledReason.IsReadOnly,
				_ => throw new ArgumentOutOfRangeException()
			};
			var leaderInfoDto = dto.AdditionalInfo switch {
				{} ai => ai.ToByteArray().Deserialize<NotHandled.Types.LeaderInfo>(),
				_ => null
			};

			var leaderInfo = leaderInfoDto switch {
				{ ExternalTcpAddress: { } } => new ClientMessage.NotHandled.Types.LeaderInfo(
					new DnsEndPoint(leaderInfoDto.HttpAddress, leaderInfoDto.HttpPort)),
				{ ExternalSecureTcpAddress: { } } => new ClientMessage.NotHandled.Types.LeaderInfo(
					new DnsEndPoint(leaderInfoDto.HttpAddress, leaderInfoDto.HttpPort)),
				_ => null
			};
			return new ClientMessage.NotHandled(package.CorrelationId, reason, leaderInfo);
		}

		private TcpPackage WrapNotHandled(ClientMessage.NotHandled msg) {
			var dto = new Client.Messages.NotHandled(msg);
			return new TcpPackage(TcpCommand.NotHandled, msg.CorrelationId, dto.Serialize());
		}

		private TcpPackage WrapNotAuthenticated(TcpMessage.NotAuthenticated msg) {
			return new TcpPackage(TcpCommand.NotAuthenticated, msg.CorrelationId,
				Helper.UTF8NoBom.GetBytes(msg.Reason ?? string.Empty));
		}

		private TcpPackage WrapAuthenticated(TcpMessage.Authenticated msg) {
			return new TcpPackage(TcpCommand.Authenticated, msg.CorrelationId, Empty.ByteArray);
		}
	}
}
