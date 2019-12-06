using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;

namespace EventStore.ClientAPI.ClientOperations {
	internal class VolatileFilteredSubscriptionOperation : VolatileSubscriptionOperation {
		private readonly Filter _filter;
		private readonly Func<EventStoreSubscription, Position, Task> _checkpointReached;
		private readonly int _checkpointInterval;
		private readonly bool _verboseLogging;
		private readonly ILogger _log;

		public VolatileFilteredSubscriptionOperation(ILogger log, TaskCompletionSource<EventStoreSubscription> source,
			string streamId, bool resolveLinkTos, int checkpointInterval, Filter filter,
			UserCredentials userCredentials,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Func<EventStoreSubscription, Position, Task> checkpointReached,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, bool verboseLogging,
			Func<TcpPackageConnection> getConnection) :
			base(log, source, streamId, resolveLinkTos, userCredentials, eventAppeared, subscriptionDropped,
				verboseLogging, getConnection) {
			_log = log;
			_filter = filter;
			_checkpointReached = checkpointReached;
			_checkpointInterval = checkpointInterval;
			_verboseLogging = verboseLogging;
		}

		protected override TcpPackage CreateSubscriptionPackage() {
			var dto = new ClientMessage.FilteredSubscribeToStream(_streamId, _resolveLinkTos,
				_filter.Value, _checkpointInterval);

			return new TcpPackage(
				TcpCommand.SubscribeToStreamFiltered, _userCredentials != null ? TcpFlags.Authenticated : TcpFlags.None,
				_correlationId, _userCredentials != null ? _userCredentials.Username : null,
				_userCredentials != null ? _userCredentials.Password : null, dto.Serialize());
		}

		protected override bool InspectPackage(TcpPackage package, out InspectionResult result) {
			if (package.Command == TcpCommand.CheckpointReached) {
				var dto = package.Data.Deserialize<ClientMessage.CheckpointReached>();
				CheckpointReached(new Position(dto.CommitPosition, dto.PreparePosition));
				result = new InspectionResult(InspectionDecision.DoNothing, "CheckpointReached");
				return true;
			}

			return base.InspectPackage(package, out result);
		}

		private void CheckpointReached(Position position) {
			if (_unsubscribed != 0)
				return;

			if (_subscription == null) throw new Exception("Subscription not confirmed, but checkpoint reached!");

			if (_verboseLogging)
				_log.Debug("Subscription {0:B} to {1}: checkpoint read @{2}.",
					_correlationId, _streamId == string.Empty ? "<all>" : _streamId, position);

			if (_checkpointReached != null) {
				ExecuteActionAsync(() => _checkpointReached(_subscription, position));
			}
		}
	}
}
