using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Settings;

using FilteredReadAllResult = EventStore.Core.Data.FilteredReadAllResult;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Messages {
	public enum OperationResult {
		Success = 0,
		PrepareTimeout = 1,
		CommitTimeout = 2,
		ForwardTimeout = 3,
		WrongExpectedVersion = 4,
		StreamDeleted = 5,
		InvalidTransaction = 6,
		AccessDenied = 7
	}

	public static partial class ClientMessage {
		[StatsGroup("client-messages")]
		public enum MessageType {
			None = 0,
			RequestShutdown = 1,
			ReloadConfig = 2,
			TcpForwardMessage = 3,
			NotHandled = 4,
			WriteEvents = 5,
			WriteEventsCompleted = 6,
			TransactionStart = 7,
			TransactionStartCompleted = 8,
			TransactionWrite = 9,
			TransactionWriteCompleted = 10,
			TransactionCommit = 11,
			TransactionCommitCompleted = 12,
			DeleteStream = 13,
			DeleteStreamCompleted = 14,
			ReadEvent = 15,
			ReadEventCompleted = 16,
			ReadStreamEventsForward = 17,
			ReadStreamEventsForwardCompleted = 18,
			ReadStreamEventsBackward = 19,
			ReadStreamEventsBackwardCompleted = 20,
			ReadAllEventsForward = 21,
			ReadAllEventsForwardCompleted = 22,
			ReadAllEventsBackward = 23,
			ReadAllEventsBackwardCompleted = 24,
			FilteredReadAllEventsForward = 25,
			FilteredReadAllEventsForwardCompleted = 26,
			FilteredReadAllEventsBackward = 27,
			FilteredReadAllEventsBackwardCompleted = 28,
			ConnectToPersistentSubscriptionToStream = 29,
			ConnectToPersistentSubscriptionToAll = 30,
			CreatePersistentSubscriptionToStream = 31,
			CreatePersistentSubscriptionToStreamCompleted = 32,
			CreatePersistentSubscriptionToAll = 33,
			CreatePersistentSubscriptionToAllCompleted = 34,
			UpdatePersistentSubscriptionToStream = 35,
			UpdatePersistentSubscriptionToStreamCompleted = 36,
			UpdatePersistentSubscriptionToAll = 37,
			UpdatePersistentSubscriptionToAllCompleted = 38,
			ReadNextNPersistentMessages = 39,
			ReadNextNPersistentMessagesCompleted = 40,
			DeletePersistentSubscriptionToStream = 41,
			DeletePersistentSubscriptionToStreamCompleted = 42,
			DeletePersistentSubscriptionToAll = 43,
			DeletePersistentSubscriptionToAllCompleted = 44,
			PersistentSubscriptionAckEvents = 45,
			PersistentSubscriptionNackEvents = 46,
			PersistentSubscriptionNakEvents = 47,
			PersistentSubscriptionConfirmation = 48,
			ReplayParkedMessages = 49,
			ReplayParkedMessage = 50,
			ReplayMessagesReceived = 51,
			SubscribeToStream = 52,
			FilteredSubscribeToStream = 53,
			CheckpointReached = 54,
			UnsubscribeFromStream = 55,
			SubscriptionConfirmation = 56,
			StreamEventAppeared = 57,
			PersistentSubscriptionStreamEventAppeared = 58,
			SubscriptionDropped = 59,
			MergeIndexes = 60,
			MergeIndexesResponse = 61,
			SetNodePriority = 62,
			ResignNode = 63,
			ScavengeDatabase = 64,
			StopDatabaseScavenge = 65,
			ScavengeDatabaseResponse = 66,
			IdentifyClient = 67,
			ClientIdentified = 68,
		}

		[StatsMessage(MessageType.RequestShutdown)]
		public partial class RequestShutdown : Message {
			public readonly bool ExitProcess;

			public readonly bool ShutdownHttp;

			public RequestShutdown(bool exitProcess, bool shutdownHttp) {
				ExitProcess = exitProcess;
				ShutdownHttp = shutdownHttp;
			}
		}

		[StatsMessage(MessageType.ReloadConfig)]
		public partial class ReloadConfig : Message {
		}

		[StatsMessage]
		public abstract partial class WriteRequestMessage : Message {
			public readonly Guid InternalCorrId;
			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly bool RequireLeader;

			public readonly ClaimsPrincipal User;
			public string Login => Tokens?.GetValueOrDefault("uid");
			public string Password => Tokens?.GetValueOrDefault("pwd");
			public readonly IReadOnlyDictionary<string, string> Tokens;

			protected WriteRequestMessage(Guid internalCorrId,
				Guid correlationId, IEnvelope envelope, bool requireLeader,
				ClaimsPrincipal user, IReadOnlyDictionary<string, string> tokens) {
				Ensure.NotEmptyGuid(internalCorrId, "internalCorrId");
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.NotNull(envelope, "envelope");

				InternalCorrId = internalCorrId;
				CorrelationId = correlationId;
				Envelope = envelope;
				RequireLeader = requireLeader;

				User = user;
				Tokens = tokens;
			}
		}

		[StatsMessage]
		public abstract partial class ReadRequestMessage : Message {
			public readonly Guid InternalCorrId;
			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;

			public readonly ClaimsPrincipal User;

			public readonly DateTime Expires;

			protected ReadRequestMessage(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				ClaimsPrincipal user, DateTime? expires) {
				Ensure.NotEmptyGuid(internalCorrId, "internalCorrId");
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				Ensure.NotNull(envelope, "envelope");

				InternalCorrId = internalCorrId;
				CorrelationId = correlationId;
				Envelope = envelope;

				User = user;
				Expires = expires ?? DateTime.UtcNow.AddMilliseconds(ESConsts.ReadRequestTimeout);
			}
		}

		[StatsMessage]
		public abstract partial class ReadResponseMessage : Message {
		}

		[StatsMessage(MessageType.TcpForwardMessage)]
		public partial class TcpForwardMessage : Message {
			public readonly Message Message;

			public TcpForwardMessage(Message message) {
				Ensure.NotNull(message, "message");

				Message = message;
			}
		}

		[StatsMessage(MessageType.NotHandled)]
		public partial class NotHandled : Message {
			public readonly Guid CorrelationId;
			public readonly Types.NotHandledReason Reason;
			public readonly Types.LeaderInfo LeaderInfo;
			public readonly string Description;
			public NotHandled(Guid correlationId,
				Types.NotHandledReason reason,
				Types.LeaderInfo leaderInfo) {
				CorrelationId = correlationId;
				Reason = reason;
				LeaderInfo = leaderInfo;
			}

			public NotHandled(Guid correlationId,
				Types.NotHandledReason reason,
				string description) {
				CorrelationId = correlationId;
				Reason = reason;
				Description = description;
			}


			public static class Types {

				public enum NotHandledReason {
					NotReady,
					TooBusy,
					NotLeader,
					IsReadOnly
				}
				public class LeaderInfo {
					public LeaderInfo(EndPoint externalTcp, bool isSecure, EndPoint http) {
						ExternalTcp = externalTcp;
						IsSecure = isSecure;
						Http = http;
					}

					public bool IsSecure { get; }
					public EndPoint ExternalTcp { get; }
					public EndPoint Http { get; }
				}
			}
		}

		[StatsMessage(MessageType.WriteEvents)]
		public partial class WriteEvents : WriteRequestMessage {
			public readonly string EventStreamId;
			public readonly long ExpectedVersion;
			public readonly Event[] Events;
			public readonly CancellationToken CancellationToken;

			public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, Event[] events, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null, CancellationToken cancellationToken = default)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (expectedVersion < Data.ExpectedVersion.StreamExists ||
				    expectedVersion == Data.ExpectedVersion.Invalid)
					throw new ArgumentOutOfRangeException(nameof(expectedVersion));
				Ensure.NotNull(events, "events");

				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
				Events = events;
				CancellationToken = cancellationToken;
			}

			public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, Event @event, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null)
				: this(internalCorrId, correlationId, envelope, requireLeader, eventStreamId, expectedVersion,
					@event == null ? null : new[] {@event}, user, tokens) {
			}

			public override string ToString() {
				return String.Format(
					"WRITE: InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, ExpectedVersion: {3}, Events: {4}",
					InternalCorrId, CorrelationId, EventStreamId, ExpectedVersion, Events.Length);
			}
		}

		[StatsMessage(MessageType.WriteEventsCompleted)]
		public partial class WriteEventsCompleted : Message {
			public readonly Guid CorrelationId;
			public readonly OperationResult Result;
			public readonly string Message;
			public readonly long FirstEventNumber;
			public readonly long LastEventNumber;
			public readonly long PreparePosition;
			public readonly long CommitPosition;
			public readonly long CurrentVersion;

			public WriteEventsCompleted(Guid correlationId, long firstEventNumber, long lastEventNumber,
				long preparePosition, long commitPosition) {
				if (firstEventNumber < -1)
					throw new ArgumentOutOfRangeException(nameof(firstEventNumber),
						$"FirstEventNumber: {firstEventNumber}");
				if (lastEventNumber - firstEventNumber + 1 < 0)
					throw new ArgumentOutOfRangeException(nameof(lastEventNumber),
						$"LastEventNumber {lastEventNumber}, FirstEventNumber {firstEventNumber}.");

				CorrelationId = correlationId;
				Result = OperationResult.Success;
				Message = null;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
			}

			public WriteEventsCompleted(Guid correlationId, OperationResult result, string message,
				long currentVersion = -1) {
				if (result == OperationResult.Success)
					throw new ArgumentException("Invalid constructor used for successful write.", nameof(result));

				CorrelationId = correlationId;
				Result = result;
				Message = message;
				FirstEventNumber = EventNumber.Invalid;
				LastEventNumber = EventNumber.Invalid;
				PreparePosition = EventNumber.Invalid;
				CurrentVersion = currentVersion;
			}

			private WriteEventsCompleted(Guid correlationId, OperationResult result, string message,
				long firstEventNumber, long lastEventNumber, long preparePosition, long commitPosition,
				long currentVersion) {
				CorrelationId = correlationId;
				Result = result;
				Message = message;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
				CurrentVersion = currentVersion;
			}

			public WriteEventsCompleted WithCorrelationId(Guid newCorrId) {
				return new WriteEventsCompleted(newCorrId, Result, Message, FirstEventNumber, LastEventNumber,
					PreparePosition, CommitPosition, CurrentVersion);
			}

			public override string ToString() {
				return
					$"WRITE COMPLETED: CorrelationId: {CorrelationId}, Result: {Result}, Message: {Message}, FirstEventNumber: {FirstEventNumber}, LastEventNumber: {LastEventNumber}, CurrentVersion: {CurrentVersion}";
			}
		}

		[StatsMessage(MessageType.TransactionStart)]
		public partial class TransactionStart : WriteRequestMessage {
			public readonly string EventStreamId;
			public readonly long ExpectedVersion;

			public TransactionStart(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (expectedVersion < Data.ExpectedVersion.Any)
					throw new ArgumentOutOfRangeException(nameof(expectedVersion));

				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion;
			}
		}

		[StatsMessage(MessageType.TransactionStartCompleted)]
		public partial class TransactionStartCompleted : Message {
			public readonly Guid CorrelationId;
			public readonly long TransactionId;
			public readonly OperationResult Result;
			public readonly string Message;

			public TransactionStartCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message) {
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
			}

			public TransactionStartCompleted WithCorrelationId(Guid newCorrId) {
				return new TransactionStartCompleted(newCorrId, TransactionId, Result, Message);
			}
		}

		[StatsMessage(MessageType.TransactionWrite)]
		public partial class TransactionWrite : WriteRequestMessage {
			public readonly long TransactionId;
			public readonly Event[] Events;

			public TransactionWrite(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				long transactionId, Event[] events, ClaimsPrincipal user, IReadOnlyDictionary<string, string> tokens = null)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.Nonnegative(transactionId, "transactionId");
				Ensure.NotNull(events, "events");

				TransactionId = transactionId;
				Events = events;
			}
		}

		[StatsMessage(MessageType.TransactionWriteCompleted)]
		public partial class TransactionWriteCompleted : Message {
			public readonly Guid CorrelationId;
			public readonly long TransactionId;
			public readonly OperationResult Result;
			public readonly string Message;

			public TransactionWriteCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message) {
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
			}

			public TransactionWriteCompleted WithCorrelationId(Guid newCorrId) {
				return new TransactionWriteCompleted(newCorrId, TransactionId, Result, Message);
			}
		}

		[StatsMessage(MessageType.TransactionCommit)]
		public partial class TransactionCommit : WriteRequestMessage {
			public readonly long TransactionId;

			public TransactionCommit(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				long transactionId, ClaimsPrincipal user, IReadOnlyDictionary<string, string> tokens = null)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.Nonnegative(transactionId, "transactionId");
				TransactionId = transactionId;
			}
		}

		[StatsMessage(MessageType.TransactionCommitCompleted)]
		public partial class TransactionCommitCompleted : Message {
			public readonly Guid CorrelationId;
			public readonly long TransactionId;
			public readonly OperationResult Result;
			public readonly string Message;
			public readonly long FirstEventNumber;
			public readonly long LastEventNumber;
			public readonly long PreparePosition;
			public readonly long CommitPosition;

			public TransactionCommitCompleted(Guid correlationId, long transactionId, long firstEventNumber,
				long lastEventNumber, long preparePosition, long commitPosition) {
				if (firstEventNumber < -1)
					throw new ArgumentOutOfRangeException("firstEventNumber",
						String.Format("FirstEventNumber: {0}", firstEventNumber));
				if (lastEventNumber - firstEventNumber + 1 < 0)
					throw new ArgumentOutOfRangeException("lastEventNumber",
						String.Format("LastEventNumber {0}, FirstEventNumber {1}.", lastEventNumber, firstEventNumber));
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = OperationResult.Success;
				Message = String.Empty;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
			}

			public TransactionCommitCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message) {
				if (result == OperationResult.Success)
					throw new ArgumentException("Invalid constructor used for successful write.", "result");

				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
				FirstEventNumber = EventNumber.Invalid;
				LastEventNumber = EventNumber.Invalid;
			}

			private TransactionCommitCompleted(Guid correlationId, long transactionId, OperationResult result,
				string message,
				long firstEventNumber, long lastEventNumber) {
				CorrelationId = correlationId;
				TransactionId = transactionId;
				Result = result;
				Message = message;
				FirstEventNumber = firstEventNumber;
				LastEventNumber = lastEventNumber;
			}

			public TransactionCommitCompleted WithCorrelationId(Guid newCorrId) {
				return new TransactionCommitCompleted(newCorrId, TransactionId, Result, Message, FirstEventNumber,
					LastEventNumber);
			}
		}

		[StatsMessage(MessageType.DeleteStream)]
		public partial class DeleteStream : WriteRequestMessage {
			public readonly string EventStreamId;
			public readonly long ExpectedVersion;
			public readonly bool HardDelete;
			public readonly CancellationToken CancellationToken;

			public DeleteStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireLeader,
				string eventStreamId, long expectedVersion, bool hardDelete, ClaimsPrincipal user,
				IReadOnlyDictionary<string, string> tokens = null, CancellationToken cancellationToken = default)
				: base(internalCorrId, correlationId, envelope, requireLeader, user, tokens) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				EventStreamId = eventStreamId;
				ExpectedVersion = expectedVersion switch {
					Data.ExpectedVersion.Invalid => throw new ArgumentOutOfRangeException(nameof(expectedVersion)),
					< Data.ExpectedVersion.StreamExists => throw new ArgumentOutOfRangeException(nameof(expectedVersion)),
					_ => expectedVersion
				};
				HardDelete = hardDelete;
				CancellationToken = cancellationToken;
			}
		}

		[StatsMessage(MessageType.DeleteStreamCompleted)]
		public partial class DeleteStreamCompleted : Message {
			public readonly Guid CorrelationId;
			public readonly OperationResult Result;
			public readonly string Message;
			public readonly long PreparePosition;
			public readonly long CommitPosition;
			public readonly long CurrentVersion;

			public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message,
				long currentVersion, long preparePosition, long commitPosition) {
				CorrelationId = correlationId;
				Result = result;
				Message = message;
				CurrentVersion = currentVersion;
				PreparePosition = preparePosition;
				CommitPosition = commitPosition;
			}

			public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message,
				long currentVersion = -1L) : this(correlationId, result, message, currentVersion, -1, -1) {
			}

			public DeleteStreamCompleted WithCorrelationId(Guid newCorrId) {
				return new DeleteStreamCompleted(newCorrId, Result, Message, CurrentVersion, PreparePosition,
					CommitPosition);
			}
		}

		[StatsMessage(MessageType.ReadEvent)]
		public partial class ReadEvent : ReadRequestMessage {
			public readonly string EventStreamId;
			public readonly long EventNumber;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public ReadEvent(Guid internalCorrId, Guid correlationId, IEnvelope envelope, string eventStreamId,
				long eventNumber,
				bool resolveLinkTos, bool requireLeader, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (eventNumber < -1) throw new ArgumentOutOfRangeException(nameof(eventNumber));

				EventStreamId = eventStreamId;
				EventNumber = eventNumber;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
			}
		}

		[StatsMessage(MessageType.ReadEventCompleted)]
		public partial class ReadEventCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string EventStreamId;
			public readonly ReadEventResult Result;
			public readonly ResolvedEvent Record;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly string Error;

			public ReadEventCompleted(Guid correlationId, string eventStreamId, ReadEventResult result,
				ResolvedEvent record, StreamMetadata streamMetadata, bool isCachePublic, string error) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				//if (result == ReadEventResult.Success)
				//    Ensure.NotNull(record.Event, "record.Event");

				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				Result = result;
				Record = record;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				Error = error;
			}
		}


		[StatsMessage(MessageType.ReadStreamEventsForward)]
		public partial class ReadStreamEventsForward : ReadRequestMessage {
			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationStreamVersion;
			public readonly TimeSpan? LongPollTimeout;
			public readonly bool ReplyOnExpired;

			public ReadStreamEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, long fromEventNumber, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationStreamVersion, ClaimsPrincipal user,
				bool replyOnExpired,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (fromEventNumber < -1) throw new ArgumentOutOfRangeException(nameof(fromEventNumber));

				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationStreamVersion = validationStreamVersion;
				LongPollTimeout = longPollTimeout;
				ReplyOnExpired = replyOnExpired;
			}

			public override string ToString() {
				return String.Format(GetType().Name + " InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, "
				                                    + "FromEventNumber: {3}, MaxCount: {4}, ResolveLinkTos: {5}, RequireLeader: {6}, ValidationStreamVersion: {7}",
					InternalCorrId, CorrelationId, EventStreamId,
					FromEventNumber, MaxCount, ResolveLinkTos, RequireLeader, ValidationStreamVersion);
			}
		}

		[StatsMessage(MessageType.ReadStreamEventsForwardCompleted)]
		public partial class ReadStreamEventsForwardCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;

			public readonly ReadStreamResult Result;
			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly string Error;
			public readonly long NextEventNumber;
			public readonly long LastEventNumber;
			public readonly bool IsEndOfStream;
			public readonly long TfLastCommitPosition;

			public ReadStreamEventsForwardCompleted(Guid correlationId, string eventStreamId, long fromEventNumber,
				int maxCount,
				ReadStreamResult result, ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic,
				string error, long nextEventNumber, long lastEventNumber, bool isEndOfStream,
				long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				if (result != ReadStreamResult.Success && result != ReadStreamResult.Expired) {
					Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
					Ensure.Equal(isEndOfStream, true, "isEndOfStream");
				}

				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;

				Result = result;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				Error = error;
				NextEventNumber = nextEventNumber;
				LastEventNumber = lastEventNumber;
				IsEndOfStream = isEndOfStream;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		[StatsMessage(MessageType.ReadStreamEventsBackward)]
		public partial class ReadStreamEventsBackward : ReadRequestMessage {
			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationStreamVersion;

			public ReadStreamEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, long fromEventNumber, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationStreamVersion, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
				if (fromEventNumber < -1) throw new ArgumentOutOfRangeException(nameof(fromEventNumber));

				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationStreamVersion = validationStreamVersion;
			}

			public override string ToString() {
				return String.Format(GetType().Name + " InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, "
				                                    + "FromEventNumber: {3}, MaxCount: {4}, ResolveLinkTos: {5}, RequireLeader: {6}, ValidationStreamVersion: {7}",
					InternalCorrId, CorrelationId, EventStreamId, FromEventNumber, MaxCount,
					ResolveLinkTos, RequireLeader, ValidationStreamVersion);
			}
		}

		[StatsMessage(MessageType.ReadStreamEventsBackwardCompleted)]
		public partial class ReadStreamEventsBackwardCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string EventStreamId;
			public readonly long FromEventNumber;
			public readonly int MaxCount;

			public readonly ReadStreamResult Result;
			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly string Error;
			public readonly long NextEventNumber;
			public readonly long LastEventNumber;
			public readonly bool IsEndOfStream;
			public readonly long TfLastCommitPosition;

			public ReadStreamEventsBackwardCompleted(Guid correlationId,
				string eventStreamId,
				long fromEventNumber,
				int maxCount,
				ReadStreamResult result,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata,
				bool isCachePublic,
				string error,
				long nextEventNumber,
				long lastEventNumber,
				bool isEndOfStream,
				long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				if (result != ReadStreamResult.Success) {
					Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
					Ensure.Equal(isEndOfStream, true, "isEndOfStream");
				}

				CorrelationId = correlationId;
				EventStreamId = eventStreamId;
				FromEventNumber = fromEventNumber;
				MaxCount = maxCount;

				Result = result;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				Error = error;
				NextEventNumber = nextEventNumber;
				LastEventNumber = lastEventNumber;
				IsEndOfStream = isEndOfStream;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		[StatsMessage(MessageType.ReadAllEventsForward)]
		public partial class ReadAllEventsForward : ReadRequestMessage {
			public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationTfLastCommitPosition;
			public readonly TimeSpan? LongPollTimeout;
			public readonly bool ReplyOnExpired;

			public ReadAllEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationTfLastCommitPosition, ClaimsPrincipal user,
				bool replyOnExpired,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
				LongPollTimeout = longPollTimeout;
				ReplyOnExpired = replyOnExpired;
			}
		}

		[StatsMessage(MessageType.ReadAllEventsForwardCompleted)]
		public partial class ReadAllEventsForwardCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;

			public readonly ReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;

			public bool IsEndOfStream {
				get { return Events == null || Events.Length < MaxCount; }
			}

			public ReadAllEventsForwardCompleted(Guid correlationId, ReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		[StatsMessage(MessageType.ReadAllEventsBackward)]
		public partial class ReadAllEventsBackward : ReadRequestMessage {
			public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;

			public readonly long? ValidationTfLastCommitPosition;

			public ReadAllEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos,
				bool requireLeader, long? validationTfLastCommitPosition, ClaimsPrincipal user,
				DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
			}
		}

		[StatsMessage(MessageType.ReadAllEventsBackwardCompleted)]
		public partial class ReadAllEventsBackwardCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;

			public readonly ReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;

			public bool IsEndOfStream {
				get { return Events == null || Events.Length < MaxCount; }
			}

			public ReadAllEventsBackwardCompleted(Guid correlationId, ReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
			}
		}

		[StatsMessage(MessageType.FilteredReadAllEventsForward)]
		public partial class FilteredReadAllEventsForward : ReadRequestMessage {
		public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;
			public readonly int MaxSearchWindow;
			public readonly IEventFilter EventFilter;
			public readonly bool ReplyOnExpired;

			public readonly long? ValidationTfLastCommitPosition;
			public readonly TimeSpan? LongPollTimeout;

			public FilteredReadAllEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos, bool requireLeader,
				int maxSearchWindow, long? validationTfLastCommitPosition, IEventFilter eventFilter, ClaimsPrincipal user,
				bool replyOnExpired,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
				LongPollTimeout = longPollTimeout;
				MaxSearchWindow = maxSearchWindow;
				EventFilter = eventFilter;
				ReplyOnExpired = replyOnExpired;
			}
		}

		[StatsMessage(MessageType.FilteredReadAllEventsForwardCompleted)]
		public partial class FilteredReadAllEventsForwardCompleted : ReadResponseMessage {
		public readonly Guid CorrelationId;

			public readonly FilteredReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;
			public readonly bool IsEndOfStream;
			public readonly long ConsideredEventsCount;

			public FilteredReadAllEventsForwardCompleted(Guid correlationId, FilteredReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition,
				bool isEndOfStream, long consideredEventsCount) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
				IsEndOfStream = isEndOfStream;
				ConsideredEventsCount = consideredEventsCount;
			}
		}

		[StatsMessage(MessageType.FilteredReadAllEventsBackward)]
		public partial class FilteredReadAllEventsBackward : ReadRequestMessage {
			public readonly long CommitPosition;
			public readonly long PreparePosition;
			public readonly int MaxCount;
			public readonly bool ResolveLinkTos;
			public readonly bool RequireLeader;
			public readonly int MaxSearchWindow;
			public readonly IEventFilter EventFilter;

			public readonly long? ValidationTfLastCommitPosition;
			public readonly TimeSpan? LongPollTimeout;

			public FilteredReadAllEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos, bool requireLeader,
				int maxSearchWindow, long? validationTfLastCommitPosition, IEventFilter eventFilter, ClaimsPrincipal user,
				TimeSpan? longPollTimeout = null, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				CommitPosition = commitPosition;
				PreparePosition = preparePosition;
				MaxCount = maxCount;
				ResolveLinkTos = resolveLinkTos;
				RequireLeader = requireLeader;
				ValidationTfLastCommitPosition = validationTfLastCommitPosition;
				LongPollTimeout = longPollTimeout;
				MaxSearchWindow = maxSearchWindow;
				EventFilter = eventFilter;
			}
		}

		[StatsMessage(MessageType.FilteredReadAllEventsBackwardCompleted)]
		public partial class FilteredReadAllEventsBackwardCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;

			public readonly FilteredReadAllResult Result;
			public readonly string Error;

			public readonly ResolvedEvent[] Events;
			public readonly StreamMetadata StreamMetadata;
			public readonly bool IsCachePublic;
			public readonly int MaxCount;
			public readonly TFPos CurrentPos;
			public readonly TFPos NextPos;
			public readonly TFPos PrevPos;
			public readonly long TfLastCommitPosition;
			public readonly bool IsEndOfStream;

			public FilteredReadAllEventsBackwardCompleted(Guid correlationId, FilteredReadAllResult result, string error,
				ResolvedEvent[] events,
				StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
				TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition,
				bool isEndOfStream) {
				Ensure.NotNull(events, "events");

				CorrelationId = correlationId;
				Result = result;
				Error = error;
				Events = events;
				StreamMetadata = streamMetadata;
				IsCachePublic = isCachePublic;
				MaxCount = maxCount;
				CurrentPos = currentPos;
				NextPos = nextPos;
				PrevPos = prevPos;
				TfLastCommitPosition = tfLastCommitPosition;
				IsEndOfStream = isEndOfStream;
			}
		}

		//Persistent subscriptions
		[StatsMessage(MessageType.ConnectToPersistentSubscriptionToStream)]
		public partial class ConnectToPersistentSubscriptionToStream : ReadRequestMessage {
			public readonly Guid ConnectionId;
			public readonly string ConnectionName;
			public readonly string GroupName;
			public readonly string EventStreamId;
			public readonly int AllowedInFlightMessages;
			public readonly string From;

			public ConnectToPersistentSubscriptionToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				Guid connectionId, string connectionName, string groupName, string eventStreamId,
				int allowedInFlightMessages, string from, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotEmptyGuid(connectionId, "connectionId");
				Ensure.NotNullOrEmpty(groupName, "subscriptionId");
				Ensure.Nonnegative(allowedInFlightMessages, "AllowedInFlightMessages");
				GroupName = groupName;
				ConnectionId = connectionId;
				ConnectionName = connectionName;
				AllowedInFlightMessages = allowedInFlightMessages;
				EventStreamId = eventStreamId;
				From = from;
			}
		}

		[StatsMessage(MessageType.ConnectToPersistentSubscriptionToAll)]
		public partial class ConnectToPersistentSubscriptionToAll : ReadRequestMessage {
			public readonly Guid ConnectionId;
			public readonly string ConnectionName;
			public readonly string GroupName;
			public readonly int AllowedInFlightMessages;
			public readonly string From;

			public ConnectToPersistentSubscriptionToAll(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				Guid connectionId, string connectionName, string groupName,
				int allowedInFlightMessages, string from, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotEmptyGuid(connectionId, "connectionId");
				Ensure.NotNullOrEmpty(groupName, "groupName");
				Ensure.Nonnegative(allowedInFlightMessages, "AllowedInFlightMessages");
				GroupName = groupName;
				ConnectionId = connectionId;
				ConnectionName = connectionName;
				AllowedInFlightMessages = allowedInFlightMessages;
				From = from;
			}
		}

		[StatsMessage(MessageType.CreatePersistentSubscriptionToStream)]
		public partial class CreatePersistentSubscriptionToStream : ReadRequestMessage {
			public readonly long StartFrom;
			public readonly int MessageTimeoutMilliseconds;
			public readonly bool RecordStatistics;

			public readonly bool ResolveLinkTos;
			public readonly int MaxRetryCount;
			public readonly int BufferSize;
			public readonly int LiveBufferSize;
			public readonly int ReadBatchSize;

			public readonly string GroupName;
			public readonly string EventStreamId;
			public readonly int MaxSubscriberCount;
			public readonly string NamedConsumerStrategy;
			public readonly int MaxCheckPointCount;
			public readonly int MinCheckPointCount;
			public readonly int CheckPointAfterMilliseconds;

			public CreatePersistentSubscriptionToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, bool resolveLinkTos, long startFrom,
				int messageTimeoutMilliseconds, bool recordStatistics, int maxRetryCount, int bufferSize,
				int liveBufferSize, int readbatchSize,
				int checkPointAfterMilliseconds, int minCheckPointCount, int maxCheckPointCount,
				int maxSubscriberCount, string namedConsumerStrategy, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				ResolveLinkTos = resolveLinkTos;
				EventStreamId = eventStreamId;
				GroupName = groupName;
				StartFrom = startFrom;
				MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
				RecordStatistics = recordStatistics;
				MaxRetryCount = maxRetryCount;
				BufferSize = bufferSize;
				LiveBufferSize = liveBufferSize;
				ReadBatchSize = readbatchSize;
				MaxCheckPointCount = maxCheckPointCount;
				MinCheckPointCount = minCheckPointCount;
				CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
				MaxSubscriberCount = maxSubscriberCount;
				NamedConsumerStrategy = namedConsumerStrategy;
			}
		}

		[StatsMessage(MessageType.CreatePersistentSubscriptionToStreamCompleted)]
		public partial class CreatePersistentSubscriptionToStreamCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly CreatePersistentSubscriptionToStreamResult Result;

			public CreatePersistentSubscriptionToStreamCompleted(Guid correlationId, CreatePersistentSubscriptionToStreamResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum CreatePersistentSubscriptionToStreamResult {
				Success = 0,
				AlreadyExists = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		[StatsMessage(MessageType.CreatePersistentSubscriptionToAll)]
		public partial class CreatePersistentSubscriptionToAll : ReadRequestMessage {
			public readonly IEventFilter EventFilter;

			public readonly TFPos StartFrom;
			public readonly int MessageTimeoutMilliseconds;
			public readonly bool RecordStatistics;

			public readonly bool ResolveLinkTos;
			public readonly int MaxRetryCount;
			public readonly int BufferSize;
			public readonly int LiveBufferSize;
			public readonly int ReadBatchSize;

			public readonly string GroupName;
			public readonly int MaxSubscriberCount;
			public readonly string NamedConsumerStrategy;
			public readonly int MaxCheckPointCount;
			public readonly int MinCheckPointCount;
			public readonly int CheckPointAfterMilliseconds;

			public CreatePersistentSubscriptionToAll(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string groupName, IEventFilter eventFilter, bool resolveLinkTos, TFPos startFrom,
				int messageTimeoutMilliseconds, bool recordStatistics, int maxRetryCount, int bufferSize,
				int liveBufferSize, int readbatchSize,
				int checkPointAfterMilliseconds, int minCheckPointCount, int maxCheckPointCount,
				int maxSubscriberCount, string namedConsumerStrategy, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				ResolveLinkTos = resolveLinkTos;
				GroupName = groupName;
				EventFilter = eventFilter;
				StartFrom = startFrom;
				MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
				RecordStatistics = recordStatistics;
				MaxRetryCount = maxRetryCount;
				BufferSize = bufferSize;
				LiveBufferSize = liveBufferSize;
				ReadBatchSize = readbatchSize;
				MaxCheckPointCount = maxCheckPointCount;
				MinCheckPointCount = minCheckPointCount;
				CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
				MaxSubscriberCount = maxSubscriberCount;
				NamedConsumerStrategy = namedConsumerStrategy;
			}
		}

		[StatsMessage(MessageType.CreatePersistentSubscriptionToAllCompleted)]
		public partial class CreatePersistentSubscriptionToAllCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly CreatePersistentSubscriptionToAllResult Result;

			public CreatePersistentSubscriptionToAllCompleted(Guid correlationId, CreatePersistentSubscriptionToAllResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum CreatePersistentSubscriptionToAllResult {
				Success = 0,
				AlreadyExists = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		[StatsMessage(MessageType.UpdatePersistentSubscriptionToStream)]
		public partial class UpdatePersistentSubscriptionToStream : ReadRequestMessage {
			public readonly long StartFrom;
			public readonly int MessageTimeoutMilliseconds;
			public readonly bool RecordStatistics;

			public readonly bool ResolveLinkTos;
			public readonly int MaxRetryCount;
			public readonly int BufferSize;
			public readonly int LiveBufferSize;
			public readonly int ReadBatchSize;

			public readonly string GroupName;
			public readonly string EventStreamId;
			public readonly int MaxSubscriberCount;

			public readonly int MaxCheckPointCount;
			public readonly int MinCheckPointCount;
			public readonly int CheckPointAfterMilliseconds;
			public readonly string NamedConsumerStrategy;

			public UpdatePersistentSubscriptionToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, bool resolveLinkTos, long startFrom,
				int messageTimeoutMilliseconds, bool recordStatistics, int maxRetryCount, int bufferSize,
				int liveBufferSize, int readbatchSize, int checkPointAfterMilliseconds, int minCheckPointCount,
				int maxCheckPointCount, int maxSubscriberCount, string namedConsumerStrategy, ClaimsPrincipal user,
				DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				ResolveLinkTos = resolveLinkTos;
				EventStreamId = eventStreamId;
				GroupName = groupName;
				StartFrom = startFrom;
				MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
				RecordStatistics = recordStatistics;
				MaxRetryCount = maxRetryCount;
				BufferSize = bufferSize;
				LiveBufferSize = liveBufferSize;
				ReadBatchSize = readbatchSize;
				MaxCheckPointCount = maxCheckPointCount;
				MinCheckPointCount = minCheckPointCount;
				CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
				MaxSubscriberCount = maxSubscriberCount;
				NamedConsumerStrategy = namedConsumerStrategy;
			}
		}

		[StatsMessage(MessageType.UpdatePersistentSubscriptionToStreamCompleted)]
		public partial class UpdatePersistentSubscriptionToStreamCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly UpdatePersistentSubscriptionToStreamResult Result;

			public UpdatePersistentSubscriptionToStreamCompleted(Guid correlationId, UpdatePersistentSubscriptionToStreamResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum UpdatePersistentSubscriptionToStreamResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		[StatsMessage(MessageType.UpdatePersistentSubscriptionToAll)]
		public partial class UpdatePersistentSubscriptionToAll : ReadRequestMessage {
			public readonly TFPos StartFrom;
			public readonly int MessageTimeoutMilliseconds;
			public readonly bool RecordStatistics;

			public readonly bool ResolveLinkTos;
			public readonly int MaxRetryCount;
			public readonly int BufferSize;
			public readonly int LiveBufferSize;
			public readonly int ReadBatchSize;

			public readonly string GroupName;
			public readonly int MaxSubscriberCount;

			public readonly int MaxCheckPointCount;
			public readonly int MinCheckPointCount;
			public readonly int CheckPointAfterMilliseconds;
			public readonly string NamedConsumerStrategy;

			public UpdatePersistentSubscriptionToAll(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string groupName, bool resolveLinkTos, TFPos startFrom,
				int messageTimeoutMilliseconds, bool recordStatistics, int maxRetryCount, int bufferSize,
				int liveBufferSize, int readbatchSize, int checkPointAfterMilliseconds, int minCheckPointCount,
				int maxCheckPointCount, int maxSubscriberCount, string namedConsumerStrategy, ClaimsPrincipal user,
				DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				ResolveLinkTos = resolveLinkTos;
				GroupName = groupName;
				StartFrom = startFrom;
				MessageTimeoutMilliseconds = messageTimeoutMilliseconds;
				RecordStatistics = recordStatistics;
				MaxRetryCount = maxRetryCount;
				BufferSize = bufferSize;
				LiveBufferSize = liveBufferSize;
				ReadBatchSize = readbatchSize;
				MaxCheckPointCount = maxCheckPointCount;
				MinCheckPointCount = minCheckPointCount;
				CheckPointAfterMilliseconds = checkPointAfterMilliseconds;
				MaxSubscriberCount = maxSubscriberCount;
				NamedConsumerStrategy = namedConsumerStrategy;
			}
		}

		[StatsMessage(MessageType.UpdatePersistentSubscriptionToAllCompleted)]
		public partial class UpdatePersistentSubscriptionToAllCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly UpdatePersistentSubscriptionToAllResult Result;

			public UpdatePersistentSubscriptionToAllCompleted(Guid correlationId, UpdatePersistentSubscriptionToAllResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum UpdatePersistentSubscriptionToAllResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		[StatsMessage(MessageType.ReadNextNPersistentMessages)]
		public partial class ReadNextNPersistentMessages : ReadRequestMessage {
			public readonly string GroupName;
			public readonly string EventStreamId;
			public readonly int Count;

			public ReadNextNPersistentMessages(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, int count, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				GroupName = groupName;
				EventStreamId = eventStreamId;
				Count = count;
			}
		}

		[StatsMessage(MessageType.ReadNextNPersistentMessagesCompleted)]
		public partial class ReadNextNPersistentMessagesCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly ReadNextNPersistentMessagesResult Result;
			public readonly (ResolvedEvent ResolvedEvent, int RetryCount)[] Events;

			public ReadNextNPersistentMessagesCompleted(Guid correlationId, ReadNextNPersistentMessagesResult result,
				string reason, (ResolvedEvent ResolvedEvent, int RetryCount)[] events) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
				Events = events;
			}

			public enum ReadNextNPersistentMessagesResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		[StatsMessage(MessageType.DeletePersistentSubscriptionToStream)]
		public partial class DeletePersistentSubscriptionToStream : ReadRequestMessage {
			public readonly string GroupName;
			public readonly string EventStreamId;

			public DeletePersistentSubscriptionToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				GroupName = groupName;
				EventStreamId = eventStreamId;
			}
		}

		[StatsMessage(MessageType.DeletePersistentSubscriptionToStreamCompleted)]
		public partial class DeletePersistentSubscriptionToStreamCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly DeletePersistentSubscriptionToStreamResult Result;

			public DeletePersistentSubscriptionToStreamCompleted(Guid correlationId, DeletePersistentSubscriptionToStreamResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum DeletePersistentSubscriptionToStreamResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		[StatsMessage(MessageType.DeletePersistentSubscriptionToAll)]
		public partial class DeletePersistentSubscriptionToAll : ReadRequestMessage {
			public readonly string GroupName;

			public DeletePersistentSubscriptionToAll(Guid internalCorrId, Guid correlationId, IEnvelope envelope
				, string groupName, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				GroupName = groupName;
			}
		}

		[StatsMessage(MessageType.DeletePersistentSubscriptionToAllCompleted)]
		public partial class DeletePersistentSubscriptionToAllCompleted : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly DeletePersistentSubscriptionToAllResult Result;

			public DeletePersistentSubscriptionToAllCompleted(Guid correlationId, DeletePersistentSubscriptionToAllResult result,
				string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum DeletePersistentSubscriptionToAllResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		[StatsMessage(MessageType.PersistentSubscriptionAckEvents)]
		public partial class PersistentSubscriptionAckEvents : ReadRequestMessage {
			public readonly string SubscriptionId;
			public readonly Guid[] ProcessedEventIds;

			public PersistentSubscriptionAckEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string subscriptionId, Guid[] processedEventIds, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(subscriptionId, "subscriptionId");
				Ensure.NotNull(processedEventIds, "processedEventIds");

				SubscriptionId = subscriptionId;
				ProcessedEventIds = processedEventIds;
			}
		}

		[StatsMessage(MessageType.PersistentSubscriptionNackEvents)]
		public partial class PersistentSubscriptionNackEvents : ReadRequestMessage {
			public readonly string SubscriptionId;
			public readonly Guid[] ProcessedEventIds;
			public readonly string Message;
			public readonly NakAction Action;

			public PersistentSubscriptionNackEvents(Guid internalCorrId,
				Guid correlationId,
				IEnvelope envelope,
				string subscriptionId,
				string message,
				NakAction action,
				Guid[] processedEventIds,
				ClaimsPrincipal user,
				DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				SubscriptionId = subscriptionId;
				ProcessedEventIds = processedEventIds;
				Message = message;
				Action = action;
			}

			public enum NakAction {
				Unknown = 0,
				Park = 1,
				Retry = 2,
				Skip = 3,
				Stop = 4
			}
		}

		[StatsMessage(MessageType.PersistentSubscriptionNakEvents)]
		public partial class PersistentSubscriptionNakEvents : ReadRequestMessage {
			public readonly string SubscriptionId;
			public readonly Guid[] ProcessedEventIds;

			public PersistentSubscriptionNakEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string subscriptionId, Guid[] processedEventIds, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotNullOrEmpty(subscriptionId, "subscriptionId");
				Ensure.NotNull(processedEventIds, "processedEventIds");

				SubscriptionId = subscriptionId;
				ProcessedEventIds = processedEventIds;
			}
		}

		[StatsMessage(MessageType.PersistentSubscriptionConfirmation)]
		public partial class PersistentSubscriptionConfirmation : Message {
			public readonly Guid CorrelationId;
			public readonly long LastIndexedPosition;
			public readonly long? LastEventNumber;
			public readonly string SubscriptionId;

			public PersistentSubscriptionConfirmation(string subscriptionId, Guid correlationId,
				long lastIndexedPosition, long? lastEventNumber) {
				CorrelationId = correlationId;
				LastIndexedPosition = lastIndexedPosition;
				LastEventNumber = lastEventNumber;
				SubscriptionId = subscriptionId;
			}
		}

		[StatsMessage(MessageType.ReplayParkedMessages)]
		public partial class ReplayParkedMessages : ReadRequestMessage {
			public readonly string EventStreamId;
			public readonly string GroupName;
			public readonly long? StopAt;

			public ReplayParkedMessages(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				string eventStreamId, string groupName, long? stopAt, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				EventStreamId = eventStreamId;
				GroupName = groupName;
				StopAt = stopAt;
			}
		}

		[StatsMessage(MessageType.ReplayParkedMessage)]
		public partial class ReplayParkedMessage : ReadRequestMessage {
			public readonly string EventStreamId;
			public readonly string GroupName;
			public readonly ResolvedEvent Event;

			public ReplayParkedMessage(Guid internalCorrId, Guid correlationId, IEnvelope envelope, string streamId,
				string groupName, ResolvedEvent @event, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				EventStreamId = streamId;
				GroupName = groupName;
				Event = @event;
			}
		}

		[StatsMessage(MessageType.ReplayMessagesReceived)]
		public partial class ReplayMessagesReceived : ReadResponseMessage {
			public readonly Guid CorrelationId;
			public readonly string Reason;
			public readonly ReplayMessagesReceivedResult Result;

			public ReplayMessagesReceived(Guid correlationId, ReplayMessagesReceivedResult result, string reason) {
				Ensure.NotEmptyGuid(correlationId, "correlationId");
				CorrelationId = correlationId;
				Result = result;
				Reason = reason;
			}

			public enum ReplayMessagesReceivedResult {
				Success = 0,
				DoesNotExist = 1,
				Fail = 2,
				AccessDenied = 3
			}
		}

		//End of persistence subscriptions


		[StatsMessage(MessageType.SubscribeToStream)]
		public partial class SubscribeToStream : ReadRequestMessage {
			public readonly Guid ConnectionId;
			public readonly string EventStreamId; // should be empty to subscribe to all
			public readonly bool ResolveLinkTos;

			public SubscribeToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope, Guid connectionId,
				string eventStreamId, bool resolveLinkTos, ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotEmptyGuid(connectionId, "connectionId");
				ConnectionId = connectionId;
				EventStreamId = eventStreamId;
				ResolveLinkTos = resolveLinkTos;
			}
		}

		[StatsMessage(MessageType.FilteredSubscribeToStream)]
		public partial class FilteredSubscribeToStream : ReadRequestMessage {
			public readonly Guid ConnectionId;
			public readonly string EventStreamId; // should be empty to subscribe to all
			public readonly bool ResolveLinkTos;
			public readonly IEventFilter EventFilter;
			public readonly int CheckpointInterval;

			public FilteredSubscribeToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				Guid connectionId, string eventStreamId, bool resolveLinkTos, ClaimsPrincipal user,
				IEventFilter eventFilter, int checkpointInterval, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
				Ensure.NotEmptyGuid(connectionId, "connectionId");
				ConnectionId = connectionId;
				EventStreamId = eventStreamId;
				ResolveLinkTos = resolveLinkTos;
				EventFilter = eventFilter;
				CheckpointInterval = checkpointInterval;
			}
		}

		[StatsMessage(MessageType.CheckpointReached)]
		public partial class CheckpointReached : Message {
			public readonly Guid CorrelationId;
			public readonly TFPos? Position;

			public CheckpointReached(Guid correlationId, TFPos? position) {
				CorrelationId = correlationId;
				Position = position;
			}
		}

		[StatsMessage(MessageType.UnsubscribeFromStream)]
		public partial class UnsubscribeFromStream : ReadRequestMessage {
			public UnsubscribeFromStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
				ClaimsPrincipal user, DateTime? expires = null)
				: base(internalCorrId, correlationId, envelope, user, expires) {
			}
		}

		[StatsMessage(MessageType.SubscriptionConfirmation)]
		public partial class SubscriptionConfirmation : Message {
			public readonly Guid CorrelationId;
			public readonly long LastIndexedPosition;
			public readonly long? LastEventNumber;

			public SubscriptionConfirmation(Guid correlationId, long lastIndexedPosition, long? lastEventNumber) {
				CorrelationId = correlationId;
				LastIndexedPosition = lastIndexedPosition;
				LastEventNumber = lastEventNumber;
			}
		}

		[StatsMessage(MessageType.StreamEventAppeared)]
		public partial class StreamEventAppeared : Message {
			public readonly Guid CorrelationId;
			public readonly ResolvedEvent Event;

			public StreamEventAppeared(Guid correlationId, ResolvedEvent @event) {
				CorrelationId = correlationId;
				Event = @event;
			}
		}

		[StatsMessage(MessageType.PersistentSubscriptionStreamEventAppeared)]
		public partial class PersistentSubscriptionStreamEventAppeared : Message {
			public readonly Guid CorrelationId;
			public readonly ResolvedEvent Event;
			public readonly int RetryCount;

			public PersistentSubscriptionStreamEventAppeared(Guid correlationId, ResolvedEvent @event, int retryCount) {
				CorrelationId = correlationId;
				Event = @event;
				RetryCount = retryCount;
			}
		}

		[StatsMessage(MessageType.SubscriptionDropped)]
		public partial class SubscriptionDropped : Message {
			public readonly Guid CorrelationId;
			public readonly SubscriptionDropReason Reason;

			public SubscriptionDropped(Guid correlationId, SubscriptionDropReason reason) {
				CorrelationId = correlationId;
				Reason = reason;
			}
		}

		[StatsMessage(MessageType.MergeIndexes)]
		public partial class MergeIndexes : Message {
			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly ClaimsPrincipal User;

			public MergeIndexes(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user) {
				Ensure.NotNull(envelope, "envelope");
				Envelope = envelope;
				CorrelationId = correlationId;
				User = user;
			}
		}

		[StatsMessage(MessageType.MergeIndexesResponse)]
		public partial class MergeIndexesResponse : Message {
			public readonly Guid CorrelationId;
			public readonly MergeIndexesResult Result;

			public MergeIndexesResponse(Guid correlationId, MergeIndexesResult result) {
				CorrelationId = correlationId;
				Result = result;
			}

			public override string ToString() {
				return String.Format("Result: {0}", Result);
			}

			public enum MergeIndexesResult {
				Started
			}
		}

		[StatsMessage(MessageType.SetNodePriority)]
		public partial class SetNodePriority : Message	{
			public readonly int NodePriority;

			public SetNodePriority(int nodePriority) {
				NodePriority = nodePriority;
			}
		}

		[StatsMessage(MessageType.ResignNode)]
		public partial class ResignNode : Message {
		}

		[StatsMessage(MessageType.ScavengeDatabase)]
		public partial class ScavengeDatabase : Message {
			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly ClaimsPrincipal User;
			public readonly int StartFromChunk;
			public readonly int Threads;
			public readonly int? Threshold;
			public readonly int? ThrottlePercent;
			public readonly bool SyncOnly;

			public ScavengeDatabase(
				IEnvelope envelope,
				Guid correlationId,
				ClaimsPrincipal user,
				int startFromChunk,
				int threads,
				int? threshold,
				int? throttlePercent,
				bool syncOnly) {

				Ensure.NotNull(envelope, "envelope");
				Envelope = envelope;
				CorrelationId = correlationId;
				User = user;
				StartFromChunk = startFromChunk;
				Threads = threads;
				Threshold = threshold;
				ThrottlePercent = throttlePercent;
				SyncOnly = syncOnly;
			}
		}

		[StatsMessage(MessageType.StopDatabaseScavenge)]
		public partial class StopDatabaseScavenge : Message {
			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly ClaimsPrincipal User;
			public readonly string ScavengeId;

			public StopDatabaseScavenge(IEnvelope envelope, Guid correlationId, ClaimsPrincipal user, string scavengeId) {
				Ensure.NotNull(envelope, "envelope");
				Envelope = envelope;
				CorrelationId = correlationId;
				User = user;
				ScavengeId = scavengeId;
			}
		}

		[StatsMessage(MessageType.ScavengeDatabaseResponse)]
		public partial class ScavengeDatabaseResponse : Message {
			public readonly Guid CorrelationId;
			public readonly ScavengeResult Result;
			public readonly string ScavengeId;

			public ScavengeDatabaseResponse(Guid correlationId,
				ScavengeResult result, string scavengeId) {
				CorrelationId = correlationId;
				Result = result;
				ScavengeId = scavengeId;
			}

			public override string ToString() {
				return String.Format("Result: {0}, ScavengeId: {1}", Result, ScavengeId);
			}

			public enum ScavengeResult {
				Started,
				Unauthorized,
				InProgress,
				Stopped,
				InvalidScavengeId
			}
		}

		[StatsMessage(MessageType.IdentifyClient)]
		public partial class IdentifyClient : Message {
			public readonly Guid CorrelationId;
			public readonly int Version;
			public readonly string ConnectionName;

			public IdentifyClient(Guid correlationId,
				int version,
				string connectionName) {
				CorrelationId = correlationId;
				Version = version;
				ConnectionName = connectionName;
			}

			public override string ToString() {
				return String.Format("Version: {0}, Connection Name: {1}", Version, ConnectionName);
			}
		}

		[StatsMessage(MessageType.ClientIdentified)]
		public partial class ClientIdentified : Message {
			public readonly Guid CorrelationId;

			public ClientIdentified(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
	}
}
