using System;
using System.Security.Claims;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages.EventReaders.Feeds {
	public static partial class FeedReaderMessage {
		//qq hmm name
		[StatsGroup("projections-event-readers-feed-reader")]
		public enum MessageType {
			None = 0,
			ReadPage = 1,
			FeedPage = 2,
		}

		[StatsMessage]
		public abstract partial class FeedReaderMessageBase : Message {
		}

		[StatsMessage(MessageType.ReadPage)]
		public sealed partial class ReadPage : FeedReaderMessageBase {
			public readonly Guid CorrelationId;
			public readonly IEnvelope Envelope;
			public readonly ClaimsPrincipal User;

			public readonly QuerySourcesDefinition QuerySource;
			public readonly CheckpointTag FromPosition;
			public readonly int MaxEvents;

			public ReadPage(
				Guid correlationId, IEnvelope envelope, ClaimsPrincipal user, QuerySourcesDefinition querySource,
				CheckpointTag fromPosition,
				int maxEvents) {
				User = user;
				CorrelationId = correlationId;
				Envelope = envelope;
				QuerySource = querySource;
				FromPosition = fromPosition;
				MaxEvents = maxEvents;
			}
		}

		[StatsMessage(MessageType.FeedPage)]
		public sealed partial class FeedPage : FeedReaderMessageBase {
			public enum ErrorStatus {
				Success,
				NotAuthorized
			}

			public readonly Guid CorrelationId;
			public readonly ErrorStatus Error;
			public readonly TaggedResolvedEvent[] Events;
			public readonly CheckpointTag LastReaderPosition;

			public FeedPage(
				Guid correlationId, ErrorStatus error, TaggedResolvedEvent[] events, CheckpointTag lastReaderPosition) {
				CorrelationId = correlationId;
				Error = error;
				Events = events;
				LastReaderPosition = lastReaderPosition;
			}
		}
	}
}
