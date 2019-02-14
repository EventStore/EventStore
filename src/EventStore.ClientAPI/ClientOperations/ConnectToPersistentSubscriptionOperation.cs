using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations {
	internal class ConnectToPersistentSubscriptionOperation :
		SubscriptionOperation<PersistentEventStoreSubscription, PersistentSubscriptionResolvedEvent>,
		IConnectToPersistentSubscriptions {
		private readonly string _groupName;
		private readonly int _bufferSize;
		private string _subscriptionId;

		public ConnectToPersistentSubscriptionOperation(ILogger log,
			TaskCompletionSource<PersistentEventStoreSubscription> source, string groupName, int bufferSize,
			string streamId, UserCredentials userCredentials,
			Func<PersistentEventStoreSubscription, PersistentSubscriptionResolvedEvent, Task> eventAppeared,
			Action<PersistentEventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
			bool verboseLogging, Func<TcpPackageConnection> getConnection)
			: base(log, source, streamId, false, userCredentials, eventAppeared, subscriptionDropped, verboseLogging,
				getConnection) {
			_groupName = groupName;
			_bufferSize = bufferSize;
		}

		protected override TcpPackage CreateSubscriptionPackage() {
			var dto = new ClientMessage.ConnectToPersistentSubscription(_groupName, _streamId, _bufferSize);
			return new TcpPackage(TcpCommand.ConnectToPersistentSubscription,
				_userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
				_correlationId,
				_userCredentials != null ? _userCredentials.Username : null,
				_userCredentials != null ? _userCredentials.Password : null,
				dto.Serialize());
		}

		protected override bool InspectPackage(TcpPackage package, out InspectionResult result) {
			if (package.Command == TcpCommand.PersistentSubscriptionConfirmation) {
				var dto = package.Data.Deserialize<ClientMessage.PersistentSubscriptionConfirmation>();
				ConfirmSubscription(dto.LastCommitPosition, dto.LastEventNumber);
				result = new InspectionResult(InspectionDecision.Subscribed, "SubscriptionConfirmation");
				_subscriptionId = dto.SubscriptionId;
				return true;
			}

			if (package.Command == TcpCommand.PersistentSubscriptionStreamEventAppeared) {
				var dto = package.Data.Deserialize<ClientMessage.PersistentSubscriptionStreamEventAppeared>();
				EventAppeared(new PersistentSubscriptionResolvedEvent(new ResolvedEvent(dto.Event), dto.RetryCount));
				result = new InspectionResult(InspectionDecision.DoNothing, "StreamEventAppeared");
				return true;
			}

			if (package.Command == TcpCommand.SubscriptionDropped) {
				var dto = package.Data.Deserialize<ClientMessage.SubscriptionDropped>();
				if (dto.Reason == ClientMessage.SubscriptionDropped.SubscriptionDropReason.AccessDenied) {
					DropSubscription(SubscriptionDropReason.AccessDenied,
						new AccessDeniedException("You do not have access to the stream."));
					result = new InspectionResult(InspectionDecision.EndOperation, "SubscriptionDropped");
					return true;
				}

				if (dto.Reason == ClientMessage.SubscriptionDropped.SubscriptionDropReason.NotFound) {
					DropSubscription(SubscriptionDropReason.NotFound, new ArgumentException("Subscription not found"));
					result = new InspectionResult(InspectionDecision.EndOperation, "SubscriptionDropped");
					return true;
				}

				if (dto.Reason == ClientMessage.SubscriptionDropped.SubscriptionDropReason
					    .PersistentSubscriptionDeleted) {
					DropSubscription(SubscriptionDropReason.PersistentSubscriptionDeleted,
						new PersistentSubscriptionDeletedException());
					result = new InspectionResult(InspectionDecision.EndOperation, "SubscriptionDropped");
					return true;
				}

				if (dto.Reason == ClientMessage.SubscriptionDropped.SubscriptionDropReason.SubscriberMaxCountReached) {
					DropSubscription(SubscriptionDropReason.MaxSubscribersReached,
						new MaximumSubscribersReachedException());
					result = new InspectionResult(InspectionDecision.EndOperation, "SubscriptionDropped");
					return true;
				}

				DropSubscription((SubscriptionDropReason)dto.Reason, null, _getConnection());
				result = new InspectionResult(InspectionDecision.EndOperation, "SubscriptionDropped");
				return true;
			}

			result = null;
			return false;
		}

		protected override PersistentEventStoreSubscription CreateSubscriptionObject(long lastCommitPosition,
			long? lastEventNumber) {
			return new PersistentEventStoreSubscription(this, _streamId, lastCommitPosition, lastEventNumber);
		}

		public void NotifyEventsProcessed(Guid[] processedEvents) {
			Ensure.NotNull(processedEvents, "processedEvents");
			var dto = new ClientMessage.PersistentSubscriptionAckEvents(
				_subscriptionId,
				processedEvents.Select(x => x.ToByteArray()).ToArray());

			var package = new TcpPackage(TcpCommand.PersistentSubscriptionAckEvents,
				_userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
				_correlationId,
				_userCredentials != null ? _userCredentials.Username : null,
				_userCredentials != null ? _userCredentials.Password : null,
				dto.Serialize());
			EnqueueSend(package);
		}

		public void NotifyEventsFailed(Guid[] processedEvents, PersistentSubscriptionNakEventAction action,
			string reason) {
			Ensure.NotNull(processedEvents, "processedEvents");
			Ensure.NotNull(reason, "reason");
			var dto = new ClientMessage.PersistentSubscriptionNakEvents(
				_subscriptionId,
				processedEvents.Select(x => x.ToByteArray()).ToArray(),
				reason,
				(ClientMessage.PersistentSubscriptionNakEvents.NakAction)action);

			var package = new TcpPackage(TcpCommand.PersistentSubscriptionNakEvents,
				_userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
				_correlationId,
				_userCredentials != null ? _userCredentials.Username : null,
				_userCredentials != null ? _userCredentials.Password : null,
				dto.Serialize());
			EnqueueSend(package);
		}
	}

	/// <summary>
	/// Thrown when max subscribers is set on subscription and it has been reached
	/// </summary>
	public class MaximumSubscribersReachedException : Exception {
		/// <summary>
		/// Constructs a <see cref="MaximumSubscribersReachedException"></see>
		/// </summary>
		public MaximumSubscribersReachedException()
			: base("Maximum subscriptions reached.") {
		}
	}

	/// <summary>
	/// Thrown when the persistent subscription has been deleted to subscribers connected to it
	/// </summary>
	public class PersistentSubscriptionDeletedException : Exception {
		/// <summary>
		/// Constructs a <see cref="PersistentSubscriptionDeletedException"></see>
		/// </summary>
		public PersistentSubscriptionDeletedException() : base("The subscription has been deleted.") {
		}
	}
}
