using System;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.AwakeReaderService {
	public partial class AwakeServiceMessage {
		[StatsGroup("awake")]
		public enum MessageType {
			None = 0,
			SubscribeAwake = 1,
			UnsubscribeAwake = 2,
		}


		[StatsMessage(MessageType.SubscribeAwake)]
		public sealed partial class SubscribeAwake : Message {
			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly string StreamId;
			public readonly TFPos From;
			public readonly Message ReplyWithMessage;

			public SubscribeAwake(
				IEnvelope envelope, Guid correlationId, string streamId, TFPos @from, Message replyWithMessage) {
				StreamId = streamId;
				From = @from;
				ReplyWithMessage = replyWithMessage;
				Envelope = envelope;
				CorrelationId = correlationId;
			}
		}

		[StatsMessage(MessageType.UnsubscribeAwake)]
		public sealed partial class UnsubscribeAwake : Message {
			public readonly Guid CorrelationId;

			public UnsubscribeAwake(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
	}
}
