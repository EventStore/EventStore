using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Shared;

namespace EventStore.Client.PersistentSubscriptions {
	partial class EventStorePersistentSubscriptionsClient {
		public PersistentSubscription Subscribe(string streamName, string groupName,
			Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
			Action<PersistentSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = null, int bufferSize = 10, bool autoAck = true) {
			if (streamName == null) {
				throw new ArgumentNullException(nameof(streamName));
			}

			if (groupName == null) {
				throw new ArgumentNullException(nameof(groupName));
			}

			if (eventAppeared == null) {
				throw new ArgumentNullException(nameof(eventAppeared));
			}

			if (streamName == string.Empty) {
				throw new ArgumentException($"{nameof(streamName)} may not be empty.", nameof(streamName));
			}

			if (groupName == string.Empty) {
				throw new ArgumentException($"{nameof(groupName)} may not be empty.", nameof(groupName));
			}

			if (bufferSize <= 0) {
				throw new ArgumentOutOfRangeException(nameof(bufferSize));
			}

			var options = new ReadReq.Types.Options {
				BufferSize = bufferSize,
				GroupName = groupName,
				StreamName = streamName,
				UuidOption = new ReadReq.Types.Options.Types.UUIDOption {Structured = new Empty()}
			};


			return new PersistentSubscription(_client, options, autoAck, eventAppeared,
				subscriptionDropped ?? delegate { }, userCredentials);
		}
	}
}
