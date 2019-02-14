using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations {
	internal class VolatileSubscriptionOperation : SubscriptionOperation<EventStoreSubscription, ResolvedEvent> {
		public VolatileSubscriptionOperation(ILogger log, TaskCompletionSource<EventStoreSubscription> source,
			string streamId, bool resolveLinkTos, UserCredentials userCredentials,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, bool verboseLogging,
			Func<TcpPackageConnection> getConnection)
			: base(log, source, streamId, resolveLinkTos, userCredentials, eventAppeared, subscriptionDropped,
				verboseLogging, getConnection) {
		}

		protected override TcpPackage CreateSubscriptionPackage() {
			var dto = new ClientMessage.SubscribeToStream(_streamId, _resolveLinkTos);
			return new TcpPackage(
				TcpCommand.SubscribeToStream, _userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
				_correlationId, _userCredentials != null ? _userCredentials.Username : null,
				_userCredentials != null ? _userCredentials.Password : null, dto.Serialize());
		}

		protected override bool InspectPackage(TcpPackage package, out InspectionResult result) {
			if (package.Command == TcpCommand.SubscriptionConfirmation) {
				var dto = package.Data.Deserialize<ClientMessage.SubscriptionConfirmation>();
				ConfirmSubscription(dto.LastCommitPosition, dto.LastEventNumber);
				result = new InspectionResult(InspectionDecision.Subscribed, "SubscriptionConfirmation");
				return true;
			}

			if (package.Command == TcpCommand.StreamEventAppeared) {
				var dto = package.Data.Deserialize<ClientMessage.StreamEventAppeared>();
				EventAppeared(new ResolvedEvent(dto.Event));
				result = new InspectionResult(InspectionDecision.DoNothing, "StreamEventAppeared");
				return true;
			}

			result = null;
			return false;
		}

		protected override EventStoreSubscription CreateSubscriptionObject(long lastCommitPosition,
			long? lastEventNumber) {
			return new VolatileEventStoreSubscription(this, _streamId, lastCommitPosition, lastEventNumber);
		}
	}
}
