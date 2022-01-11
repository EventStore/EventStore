﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using EventStore.Client.Messages;
using EventStore.Common.Utils;
using EventStore.Core.Authentication.DelegatedAuthentication;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Util;
using Google.Protobuf;
using Microsoft.Diagnostics.Tracing.Parsers.IIS_Trace;
using OperationResult = EventStore.Core.Messages.OperationResult;

namespace EventStore.Core.Services.Transport.Tcp {
	public enum ClientVersion : byte {
		V1 = 0,
		V2 = 1
	}

	public class ClientWriteTcpDispatcher : TcpDispatcher {
		private readonly TimeSpan _writeTimeout;

		protected ClientWriteTcpDispatcher(TimeSpan writeTimeout) {
			_writeTimeout = writeTimeout;
			AddUnwrapper(TcpCommand.WriteEvents, UnwrapWriteEvents, ClientVersion.V2);
			AddWrapper<ClientMessage.WriteEvents>(WrapWriteEvents, ClientVersion.V2);
			AddUnwrapper(TcpCommand.WriteEventsCompleted, UnwrapWriteEventsCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.WriteEventsCompleted>(WrapWriteEventsCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.TransactionStart, UnwrapTransactionStart, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionStart>(WrapTransactionStart, ClientVersion.V2);
			AddUnwrapper(TcpCommand.TransactionStartCompleted, UnwrapTransactionStartCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionStartCompleted>(WrapTransactionStartCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.TransactionWrite, UnwrapTransactionWrite, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionWrite>(WrapTransactionWrite, ClientVersion.V2);
			AddUnwrapper(TcpCommand.TransactionWriteCompleted, UnwrapTransactionWriteCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionWriteCompleted>(WrapTransactionWriteCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.TransactionCommit, UnwrapTransactionCommit, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionCommit>(WrapTransactionCommit, ClientVersion.V2);
			AddUnwrapper(TcpCommand.TransactionCommitCompleted, UnwrapTransactionCommitCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.TransactionCommitCompleted>(WrapTransactionCommitCompleted, ClientVersion.V2);

			AddUnwrapper(TcpCommand.DeleteStream, UnwrapDeleteStream, ClientVersion.V2);
			AddWrapper<ClientMessage.DeleteStream>(WrapDeleteStream, ClientVersion.V2);
			AddUnwrapper(TcpCommand.DeleteStreamCompleted, UnwrapDeleteStreamCompleted, ClientVersion.V2);
			AddWrapper<ClientMessage.DeleteStreamCompleted>(WrapDeleteStreamCompleted, ClientVersion.V2);
		}
		private ClientMessage.WriteEvents UnwrapWriteEvents(TcpPackage package, IEnvelope envelope,
			ClaimsPrincipal user) {
			var dto = package.Data.Deserialize<WriteEvents>();
			if (dto == null) return null;

			var events = new Event[dto.Events.Count];
			for (int i = 0; i < events.Length; ++i) {
				// ReSharper disable PossibleNullReferenceException
				var e = dto.Events[i];
				// ReSharper restore PossibleNullReferenceException
				events[i] = new Event(new Guid(e.EventId.ToByteArray()), e.EventType, e.DataContentType == 1, e.Data.ToByteArray(), e.Metadata.ToByteArray());
			}

			var cts = new CancellationTokenSource();
			var envelopeWrapper = new CallbackEnvelope(OnMessage);
			cts.CancelAfter(_writeTimeout);

			return new ClientMessage.WriteEvents(Guid.NewGuid(), package.CorrelationId, envelopeWrapper, dto.RequireLeader,
				dto.EventStreamId, dto.ExpectedVersion, events, user, package.Tokens, cts.Token);

			void OnMessage(Message m) {
				cts.Dispose();
				envelope.ReplyWith(m);
			}
		}

		private static TcpPackage WrapWriteEvents(ClientMessage.WriteEvents msg) {
			var events = new NewEvent[msg.Events.Length];
			for (int i = 0; i < events.Length; ++i) {
				var e = msg.Events[i];
				events[i] = new NewEvent(e.EventId.ToByteArray(),
					e.EventType,
					e.IsJson ? 1 : 0,
					0, e.Data,
					e.Metadata);
			}

			var dto = new WriteEvents(msg.EventStreamId, msg.ExpectedVersion, events,
				msg.RequireLeader);
			return CreateWriteRequestPackage(TcpCommand.WriteEvents, msg, dto);
		}

		private static TcpPackage CreateWriteRequestPackage<T>(TcpCommand command, ClientMessage.WriteRequestMessage msg,
			T dto) where T: IMessage<T>{
			// we forwarding with InternalCorrId, not client's CorrelationId!!!
			if (msg.User == UserManagement.SystemAccounts.System) {
				return new TcpPackage(command, TcpFlags.TrustedWrite, msg.InternalCorrId, null, null, dto.Serialize());
			}

			foreach (var identity in msg.User.Identities) {
				if (!(identity is DelegatedClaimsIdentity dci)) {
					continue;
				}

				var jwtClaim = dci.FindFirst("jwt");
				if (jwtClaim != null) {
					return new TcpPackage(command, TcpFlags.Authenticated, msg.InternalCorrId, jwtClaim.Value,
						dto.Serialize());
				}

				var uidClaim = dci.FindFirst("uid");
				var pwdClaim = dci.FindFirst("pwd");

				if (uidClaim != null && pwdClaim != null) {
					return new TcpPackage(command, TcpFlags.Authenticated, msg.InternalCorrId, uidClaim.Value,
						pwdClaim.Value, dto.Serialize());
				}
			}

			return msg.Login != null && msg.Password != null
				? new TcpPackage(command, TcpFlags.Authenticated, msg.InternalCorrId, msg.Login, msg.Password,
					dto.Serialize())
				: new TcpPackage(command, TcpFlags.None, msg.InternalCorrId, null, null, dto.Serialize());
		}

		private static ClientMessage.WriteEventsCompleted UnwrapWriteEventsCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<WriteEventsCompleted>();
			if (dto == null) return null;
			if (dto.Result == Client.Messages.OperationResult.Success)
				return new ClientMessage.WriteEventsCompleted(package.CorrelationId,
					dto.FirstEventNumber,
					dto.LastEventNumber,
					dto.PreparePosition,
					dto.CommitPosition);
			return new ClientMessage.WriteEventsCompleted(package.CorrelationId,
				(OperationResult)dto.Result,
				dto.Message,
				dto.CurrentVersion);
		}

		private static TcpPackage WrapWriteEventsCompleted(ClientMessage.WriteEventsCompleted msg) {
			var dto = new WriteEventsCompleted((Client.Messages.OperationResult)msg.Result,
				msg.Message,
				msg.FirstEventNumber,
				msg.LastEventNumber,
				msg.PreparePosition,
				msg.CommitPosition,
				msg.CurrentVersion);
			return new TcpPackage(TcpCommand.WriteEventsCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.TransactionStart UnwrapTransactionStart(TcpPackage package, IEnvelope envelope,
			ClaimsPrincipal user) {
			var dto = package.Data.Deserialize<TransactionStart>();
			if (dto == null) return null;
			return new ClientMessage.TransactionStart(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.RequireLeader,
				dto.EventStreamId, dto.ExpectedVersion, user, package.Tokens);
		}

		private static TcpPackage WrapTransactionStart(ClientMessage.TransactionStart msg) {
			var dto = new TransactionStart(msg.EventStreamId, msg.ExpectedVersion,
				msg.RequireLeader);
			return CreateWriteRequestPackage(TcpCommand.TransactionStart, msg, dto);
		}

		private static ClientMessage.TransactionStartCompleted UnwrapTransactionStartCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TransactionStartCompleted>();
			if (dto == null) return null;
			return new ClientMessage.TransactionStartCompleted(package.CorrelationId, dto.TransactionId,
				(OperationResult)dto.Result, dto.Message);
		}

		private static TcpPackage WrapTransactionStartCompleted(ClientMessage.TransactionStartCompleted msg) {
			var dto = new TransactionStartCompleted(msg.TransactionId,
				(Client.Messages.OperationResult)msg.Result, msg.Message);
			return new TcpPackage(TcpCommand.TransactionStartCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.TransactionWrite UnwrapTransactionWrite(TcpPackage package, IEnvelope envelope,
			ClaimsPrincipal user) {
			var dto = package.Data.Deserialize<TransactionWrite>();
			if (dto == null) return null;

			var events = new Event[dto.Events.Count];
			for (int i = 0; i < events.Length; ++i) {
				// ReSharper disable PossibleNullReferenceException
				var e = dto.Events[i];
				// ReSharper restore PossibleNullReferenceException
				events[i] = new Event(new Guid(e.EventId.ToByteArray()), e.EventType, e.DataContentType == 1, e.Data.ToByteArray(), e.Metadata.ToByteArray());
			}

			return new ClientMessage.TransactionWrite(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.RequireLeader,
				dto.TransactionId, events, user, package.Tokens);
		}

		private static TcpPackage WrapTransactionWrite(ClientMessage.TransactionWrite msg) {
			var events = new NewEvent[msg.Events.Length];
			for (int i = 0; i < events.Length; ++i) {
				var e = msg.Events[i];
				events[i] = new NewEvent(e.EventId.ToByteArray(), e.EventType, e.IsJson ? 1 : 0, 0,
					e.Data, e.Metadata);
			}

			var dto = new TransactionWrite(msg.TransactionId, events, msg.RequireLeader);
			return CreateWriteRequestPackage(TcpCommand.TransactionWrite, msg, dto);
		}

		private static ClientMessage.TransactionWriteCompleted UnwrapTransactionWriteCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TransactionWriteCompleted>();
			if (dto == null) return null;
			return new ClientMessage.TransactionWriteCompleted(package.CorrelationId, dto.TransactionId,
				(OperationResult)dto.Result, dto.Message);
		}

		private static TcpPackage WrapTransactionWriteCompleted(ClientMessage.TransactionWriteCompleted msg) {
			var dto = new TransactionWriteCompleted(msg.TransactionId,
				(Client.Messages.OperationResult)msg.Result, msg.Message);
			return new TcpPackage(TcpCommand.TransactionWriteCompleted, msg.CorrelationId, dto.Serialize());
		}

		private static ClientMessage.TransactionCommit UnwrapTransactionCommit(TcpPackage package, IEnvelope envelope,
			ClaimsPrincipal user) {
			var dto = package.Data.Deserialize<TransactionCommit>();
			if (dto == null) return null;
			return new ClientMessage.TransactionCommit(Guid.NewGuid(), package.CorrelationId, envelope,
				dto.RequireLeader, dto.TransactionId, user, package.Tokens);
		}

		private static TcpPackage WrapTransactionCommit(ClientMessage.TransactionCommit msg) {
			var dto = new TransactionCommit(msg.TransactionId, msg.RequireLeader);
			return CreateWriteRequestPackage(TcpCommand.TransactionCommit, msg, dto);
		}

		private static ClientMessage.TransactionCommitCompleted UnwrapTransactionCommitCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<TransactionCommitCompleted>();
			if (dto == null) return null;
			if (dto.Result == Client.Messages.OperationResult.Success)
				return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId,
					dto.FirstEventNumber, dto.LastEventNumber, dto.PreparePosition, dto.CommitPosition);
			return new ClientMessage.TransactionCommitCompleted(package.CorrelationId, dto.TransactionId,
				(OperationResult)dto.Result, dto.Message);
		}

		private static TcpPackage WrapTransactionCommitCompleted(ClientMessage.TransactionCommitCompleted msg) {
			var dto = new TransactionCommitCompleted(msg.TransactionId,
				(Client.Messages.OperationResult)msg.Result,
				msg.Message, msg.FirstEventNumber, msg.LastEventNumber, msg.PreparePosition, msg.CommitPosition);
			return new TcpPackage(TcpCommand.TransactionCommitCompleted, msg.CorrelationId, dto.Serialize());
		}

		private ClientMessage.DeleteStream UnwrapDeleteStream(TcpPackage package, IEnvelope envelope,
			ClaimsPrincipal user) {
			var dto = package.Data.Deserialize<DeleteStream>();
			if (dto == null) return null;

			var cts = new CancellationTokenSource();
			var envelopeWrapper = new CallbackEnvelope(OnMessage);
			cts.CancelAfter(_writeTimeout);

			return new ClientMessage.DeleteStream(Guid.NewGuid(), package.CorrelationId, envelopeWrapper, dto.RequireLeader,
				dto.EventStreamId, dto.ExpectedVersion, dto.HardDelete, user, package.Tokens, cts.Token);

			void OnMessage(Message m) {
				cts.Dispose();
				envelope.ReplyWith(m);
			}
		}

		private static TcpPackage WrapDeleteStream(ClientMessage.DeleteStream msg) {
			var dto = new DeleteStream(msg.EventStreamId, msg.ExpectedVersion, msg.RequireLeader,
				msg.HardDelete);
			return CreateWriteRequestPackage(TcpCommand.DeleteStream, msg, dto);
		}

		private static ClientMessage.DeleteStreamCompleted UnwrapDeleteStreamCompleted(TcpPackage package,
			IEnvelope envelope) {
			var dto = package.Data.Deserialize<DeleteStreamCompleted>();
			if (dto == null) return null;
			return new ClientMessage.DeleteStreamCompleted(package.CorrelationId, (OperationResult)dto.Result,
				dto.Message,
				dto.PreparePosition,
				dto.CommitPosition);
		}

		private static TcpPackage WrapDeleteStreamCompleted(ClientMessage.DeleteStreamCompleted msg) {
			var dto = new DeleteStreamCompleted((Client.Messages.OperationResult)msg.Result,
				msg.Message,
				msg.PreparePosition,
				msg.CommitPosition);
			return new TcpPackage(TcpCommand.DeleteStreamCompleted, msg.CorrelationId, dto.Serialize());
		}
	}
}
