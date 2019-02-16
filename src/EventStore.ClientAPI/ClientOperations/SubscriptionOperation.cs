using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;
#if!NET452
using TaskEx = System.Threading.Tasks.Task;

#endif

namespace EventStore.ClientAPI.ClientOperations {
	internal interface IResolvedEvent {
		string OriginalStreamId { get; }
		long OriginalEventNumber { get; }
		RecordedEvent OriginalEvent { get; }
		Position? OriginalPosition { get; }
	}

	internal abstract class SubscriptionOperation<T, TE> : ISubscriptionOperation
		where T : EventStoreSubscription where TE : IResolvedEvent {
		private readonly ILogger _log;
		private readonly TaskCompletionSource<T> _source;
		protected readonly string _streamId;
		protected readonly bool _resolveLinkTos;
		protected readonly UserCredentials _userCredentials;
		protected readonly Func<T, TE, Task> _eventAppeared;
		private readonly Action<T, SubscriptionDropReason, Exception> _subscriptionDropped;
		private readonly bool _verboseLogging;
		protected readonly Func<TcpPackageConnection> _getConnection;
		private readonly int _maxQueueSize = 2000;
		private readonly ConcurrentQueueWrapper<Func<Task>> _actionQueue = new ConcurrentQueueWrapper<Func<Task>>();
		private int _actionExecuting;
		private T _subscription;
		private int _unsubscribed;
		protected Guid _correlationId;

		protected SubscriptionOperation(ILogger log,
			TaskCompletionSource<T> source,
			string streamId,
			bool resolveLinkTos,
			UserCredentials userCredentials,
			Func<T, TE, Task> eventAppeared,
			Action<T, SubscriptionDropReason, Exception> subscriptionDropped,
			bool verboseLogging,
			Func<TcpPackageConnection> getConnection) {
			Ensure.NotNull(log, "log");
			Ensure.NotNull(source, "source");
			Ensure.NotNull(eventAppeared, "eventAppeared");
			Ensure.NotNull(getConnection, "getConnection");

			_log = log;
			_source = source;
			_streamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
			_resolveLinkTos = resolveLinkTos;
			_userCredentials = userCredentials;
			_eventAppeared = eventAppeared;
			_subscriptionDropped = subscriptionDropped ?? ((x, y, z) => { });
			_verboseLogging = verboseLogging;
			_getConnection = getConnection;
		}

		protected void EnqueueSend(TcpPackage package) {
			_getConnection().EnqueueSend(package);
		}

		public bool Subscribe(Guid correlationId, TcpPackageConnection connection) {
			Ensure.NotNull(connection, "connection");

			if (_subscription != null || _unsubscribed != 0)
				return false;

			_correlationId = correlationId;
			connection.EnqueueSend(CreateSubscriptionPackage());
			return true;
		}

		protected abstract TcpPackage CreateSubscriptionPackage();

		public void Unsubscribe() {
			DropSubscription(SubscriptionDropReason.UserInitiated, null, _getConnection());
		}

		private TcpPackage CreateUnsubscriptionPackage() {
			return new TcpPackage(TcpCommand.UnsubscribeFromStream, _correlationId,
				new ClientMessage.UnsubscribeFromStream().Serialize());
		}

		protected abstract bool InspectPackage(TcpPackage package, out InspectionResult result);

		public InspectionResult InspectPackage(TcpPackage package) {
			try {
				InspectionResult result;
				if (InspectPackage(package, out result)) {
					return result;
				}

				switch (package.Command) {
					case TcpCommand.SubscriptionDropped: {
						var dto = package.Data.Deserialize<ClientMessage.SubscriptionDropped>();
						switch (dto.Reason) {
							case ClientMessage.SubscriptionDropped.SubscriptionDropReason.Unsubscribed:
								DropSubscription(SubscriptionDropReason.UserInitiated, null);
								break;
							case ClientMessage.SubscriptionDropped.SubscriptionDropReason.AccessDenied:
								DropSubscription(SubscriptionDropReason.AccessDenied,
									new AccessDeniedException(string.Format(
										"Subscription to '{0}' failed due to access denied.",
										_streamId == string.Empty ? "<all>" : _streamId)));
								break;
							case ClientMessage.SubscriptionDropped.SubscriptionDropReason.NotFound:
								DropSubscription(SubscriptionDropReason.NotFound,
									new ArgumentException(string.Format(
										"Subscription to '{0}' failed due to not found.",
										_streamId == string.Empty ? "<all>" : _streamId)));
								break;
							default:
								if (_verboseLogging)
									_log.Debug("Subscription dropped by server. Reason: {0}.", dto.Reason);
								DropSubscription(SubscriptionDropReason.Unknown,
									new CommandNotExpectedException(string.Format("Unsubscribe reason: '{0}'.",
										dto.Reason)));
								break;
						}

						return new InspectionResult(InspectionDecision.EndOperation,
							string.Format("SubscriptionDropped: {0}", dto.Reason));
					}

					case TcpCommand.NotAuthenticated: {
						string message = Helper.EatException(() =>
							Helper.UTF8NoBom.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
						DropSubscription(SubscriptionDropReason.NotAuthenticated,
							new NotAuthenticatedException(string.IsNullOrEmpty(message)
								? "Authentication error"
								: message));
						return new InspectionResult(InspectionDecision.EndOperation, "NotAuthenticated");
					}

					case TcpCommand.BadRequest: {
						string message = Helper.EatException(() =>
							Helper.UTF8NoBom.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
						DropSubscription(SubscriptionDropReason.ServerError,
							new ServerErrorException(string.IsNullOrEmpty(message) ? "<no message>" : message));
						return new InspectionResult(InspectionDecision.EndOperation,
							string.Format("BadRequest: {0}", message));
					}

					case TcpCommand.NotHandled: {
						if (_subscription != null)
							throw new Exception("NotHandled command appeared while we were already subscribed.");

						var message = package.Data.Deserialize<ClientMessage.NotHandled>();
						switch (message.Reason) {
							case ClientMessage.NotHandled.NotHandledReason.NotReady:
								return new InspectionResult(InspectionDecision.Retry, "NotHandled - NotReady");

							case ClientMessage.NotHandled.NotHandledReason.TooBusy:
								return new InspectionResult(InspectionDecision.Retry, "NotHandled - TooBusy");

							case ClientMessage.NotHandled.NotHandledReason.NotMaster:
								var masterInfo =
									message.AdditionalInfo.Deserialize<ClientMessage.NotHandled.MasterInfo>();
								return new InspectionResult(InspectionDecision.Reconnect, "NotHandled - NotMaster",
									masterInfo.ExternalTcpEndPoint, masterInfo.ExternalSecureTcpEndPoint);

							default:
								_log.Error("Unknown NotHandledReason: {0}.", message.Reason);
								return new InspectionResult(InspectionDecision.Retry, "NotHandled - <unknown>");
						}
					}

					default: {
						DropSubscription(SubscriptionDropReason.ServerError,
							new CommandNotExpectedException(package.Command.ToString()));
						return new InspectionResult(InspectionDecision.EndOperation, package.Command.ToString());
					}
				}
			} catch (Exception e) {
				DropSubscription(SubscriptionDropReason.Unknown, e);
				return new InspectionResult(InspectionDecision.EndOperation,
					string.Format("Exception - {0}", e.Message));
			}
		}

		public void ConnectionClosed() {
			DropSubscription(SubscriptionDropReason.ConnectionClosed,
				new ConnectionClosedException("Connection was closed."));
		}

		internal bool TimeOutSubscription() {
			if (_subscription != null)
				return false;
			DropSubscription(SubscriptionDropReason.SubscribingError, null);
			return true;
		}

		public void DropSubscription(SubscriptionDropReason reason, Exception exc,
			TcpPackageConnection connection = null) {
			if (Interlocked.CompareExchange(ref _unsubscribed, 1, 0) == 0) {
				if (_verboseLogging)
					_log.Debug("Subscription {0:B} to {1}: closing subscription, reason: {2}, exception: {3}...",
						_correlationId, _streamId == string.Empty ? "<all>" : _streamId, reason, exc);

				if (reason != SubscriptionDropReason.UserInitiated) {
					var er = exc ?? new Exception(String.Format("Subscription dropped for {0}", reason));
					_source.TrySetException(er);
				}

				if (reason == SubscriptionDropReason.UserInitiated && _subscription != null && connection != null)
					connection.EnqueueSend(CreateUnsubscriptionPackage());

				if (_subscription != null)
					ExecuteActionAsync(() => {
						_subscriptionDropped(_subscription, reason, exc);
						return TaskEx.CompletedTask;
					});
			}
		}

		protected void ConfirmSubscription(long lastCommitPosition, long? lastEventNumber) {
			if (lastCommitPosition < -1)
				throw new ArgumentOutOfRangeException(nameof(lastCommitPosition),
					string.Format("Invalid lastCommitPosition {0} on subscription confirmation.", lastCommitPosition));
			if (_subscription != null)
				throw new Exception("Double confirmation of subscription.");

			if (_verboseLogging)
				_log.Debug("Subscription {0:B} to {1}: subscribed at CommitPosition: {2}, EventNumber: {3}.",
					_correlationId, _streamId == string.Empty ? "<all>" : _streamId, lastCommitPosition,
					lastEventNumber);
			_subscription = CreateSubscriptionObject(lastCommitPosition, lastEventNumber);
			_source.SetResult(_subscription);
		}

		protected abstract T CreateSubscriptionObject(long lastCommitPosition, long? lastEventNumber);

		protected void EventAppeared(TE e) {
			if (_unsubscribed != 0)
				return;

			if (_subscription == null) throw new Exception("Subscription not confirmed, but event appeared!");

			if (_verboseLogging)
				_log.Debug("Subscription {0:B} to {1}: event appeared ({2}, {3}, {4} @ {5}).",
					_correlationId, _streamId == string.Empty ? "<all>" : _streamId,
					e.OriginalStreamId, e.OriginalEventNumber, e.OriginalEvent.EventType, e.OriginalPosition);

			ExecuteActionAsync(() => _eventAppeared(_subscription, e));
		}

		private void ExecuteActionAsync(Func<Task> action) {
			_actionQueue.Enqueue(action);

			if (_actionQueue.Count > _maxQueueSize)
				DropSubscription(SubscriptionDropReason.ProcessingQueueOverflow,
					new Exception("client buffer too big"));

			if (Interlocked.CompareExchange(ref _actionExecuting, 1, 0) == 0)
				ThreadPool.QueueUserWorkItem(ExecuteActions);
		}

		private async void ExecuteActions(object state) {
			do {
				Func<Task> action;
				while (_actionQueue.TryDequeue(out action)) {
					try {
						await action().ConfigureAwait(false);
					} catch (Exception exc) {
						_log.Error(exc, "Exception during executing user callback: {0}.", exc.Message);
					}
				}

				Interlocked.Exchange(ref _actionExecuting, 0);
			} while (!_actionQueue.IsEmpty && Interlocked.CompareExchange(ref _actionExecuting, 1, 0) == 0);
		}
	}
}
