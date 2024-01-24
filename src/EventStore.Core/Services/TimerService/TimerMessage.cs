using System;
using EventStore.Core.Messaging;
using JetBrains.Annotations;

namespace EventStore.Core.Services.TimerService {
	public static partial class TimerMessage {
		[DerivedMessage(CoreMessage.Timer)]
		public partial class Schedule : Message<Schedule> {
			public readonly TimeSpan TriggerAfter;
			public readonly IEnvelope Envelope;
			public readonly Message ReplyMessage;
			private readonly Action _replyAction;

			public static Schedule Create<T>(TimeSpan triggerAfter, IEnvelope envelope, T replyMessage) where T : class, Message => 
				new(triggerAfter, envelope, replyMessage, () => envelope.ReplyWith(replyMessage));

			private Schedule(TimeSpan triggerAfter, [NotNull] IEnvelope envelope, [NotNull] Message replyMessage, [NotNull] Action replyAction) {
				TriggerAfter = triggerAfter;
				Envelope     = envelope ?? throw new ArgumentNullException(nameof(envelope));
				ReplyMessage = replyMessage ?? throw new ArgumentNullException(nameof(replyMessage));
				_replyAction = replyAction ?? throw new ArgumentNullException(nameof(replyAction));
			}

			public void Reply() => _replyAction();
		}
	}
}
