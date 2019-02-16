using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Tcp;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI.Internal {
	internal class EventStoreConnectionLogicHandler : IEventStoreConnectionLogicHandler {
		private static readonly TimerTickMessage TimerTickMessage = new TimerTickMessage();

		public int TotalOperationCount {
			get { return _operations.TotalOperationCount; }
		}

		private readonly IEventStoreConnection _esConnection;
		private readonly ConnectionSettings _settings;
		private readonly byte ClientVersion = 1;

		private readonly SimpleQueuedHandler _queue;
		private readonly Timer _timer;
		private IEndPointDiscoverer _endPointDiscoverer;

		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private ReconnectionInfo _reconnInfo;
		private HeartbeatInfo _heartbeatInfo;
		private AuthInfo _authInfo;
		private IdentifyInfo _identifyInfo;
		private TimeSpan _lastTimeoutsTimeStamp;
		private readonly OperationsManager _operations;
		private readonly SubscriptionsManager _subscriptions;

		private ConnectionState _state = ConnectionState.Init;
		private ConnectingPhase _connectingPhase = ConnectingPhase.Invalid;
		private int _wasConnected;

		private int _packageNumber;
		private TcpPackageConnection _connection;

		public EventStoreConnectionLogicHandler(IEventStoreConnection esConnection, ConnectionSettings settings) {
			Ensure.NotNull(esConnection, "esConnection");
			Ensure.NotNull(settings, "settings");

			_esConnection = esConnection;
			_settings = settings;

			_operations = new OperationsManager(_esConnection.ConnectionName, settings);
			_subscriptions = new SubscriptionsManager(_esConnection.ConnectionName, settings);
			_queue = new SimpleQueuedHandler(_settings.Log);
			_queue.RegisterHandler<StartConnectionMessage>(msg => StartConnection(msg.Task, msg.EndPointDiscoverer));
			_queue.RegisterHandler<CloseConnectionMessage>(msg => CloseConnection(msg.Reason, msg.Exception));

			_queue.RegisterHandler<StartOperationMessage>(msg =>
				StartOperation(msg.Operation, msg.MaxRetries, msg.Timeout));
			_queue.RegisterHandler<StartSubscriptionMessage>(StartSubscription);
			_queue.RegisterHandler<StartPersistentSubscriptionMessage>(StartSubscription);

			_queue.RegisterHandler<EstablishTcpConnectionMessage>(msg => EstablishTcpConnection(msg.EndPoints));
			_queue.RegisterHandler<TcpConnectionEstablishedMessage>(msg => TcpConnectionEstablished(msg.Connection));
			_queue.RegisterHandler<TcpConnectionErrorMessage>(msg => TcpConnectionError(msg.Connection, msg.Exception));
			_queue.RegisterHandler<TcpConnectionClosedMessage>(msg => TcpConnectionClosed(msg.Connection));
			_queue.RegisterHandler<HandleTcpPackageMessage>(msg => HandleTcpPackage(msg.Connection, msg.Package));

			_queue.RegisterHandler<TimerTickMessage>(msg => TimerTick());

			_timer = new Timer(_ => EnqueueMessage(TimerTickMessage), null, Consts.TimerPeriod, Consts.TimerPeriod);
		}

		public void EnqueueMessage(Message message) {
			if (_settings.VerboseLogging && message != TimerTickMessage) LogDebug("enqueueing message {0}.", message);
			_queue.EnqueueMessage(message);
		}

		private void StartConnection(TaskCompletionSource<object> task, IEndPointDiscoverer endPointDiscoverer) {
			Ensure.NotNull(task, "task");
			Ensure.NotNull(endPointDiscoverer, "endPointDiscoverer");

			LogDebug("StartConnection");

			switch (_state) {
				case ConnectionState.Init: {
					_endPointDiscoverer = endPointDiscoverer;
					_state = ConnectionState.Connecting;
					_connectingPhase = ConnectingPhase.Reconnecting;
					DiscoverEndPoint(task);
					break;
				}
				case ConnectionState.Connecting:
				case ConnectionState.Connected: {
					task.SetException(new InvalidOperationException(
						string.Format("EventStoreConnection '{0}' is already active.", _esConnection.ConnectionName)));
					break;
				}
				case ConnectionState.Closed:
					task.SetException(new ObjectDisposedException(_esConnection.ConnectionName));
					break;
				default: throw new Exception(string.Format("Unknown state: {0}", _state));
			}
		}

		private void DiscoverEndPoint(TaskCompletionSource<object> completionTask) {
			LogDebug("DiscoverEndPoint");

			if (_state != ConnectionState.Connecting) return;
			if (_connectingPhase != ConnectingPhase.Reconnecting) return;

			_connectingPhase = ConnectingPhase.EndPointDiscovery;

			_endPointDiscoverer.DiscoverAsync(_connection != null ? _connection.RemoteEndPoint : null).ContinueWith(
				t => {
					if (t.IsFaulted) {
						EnqueueMessage(
							new CloseConnectionMessage("Failed to resolve TCP end point to which to connect.",
								t.Exception));
						completionTask?.SetException(
							new CannotEstablishConnectionException("Cannot resolve target end point.", t.Exception));
					} else {
						EnqueueMessage(new EstablishTcpConnectionMessage(t.Result));
						completionTask?.SetResult(null);
					}
				});
		}

		private void EstablishTcpConnection(NodeEndPoints endPoints) {
			var endPoint = _settings.UseSslConnection
				? endPoints.SecureTcpEndPoint ?? endPoints.TcpEndPoint
				: endPoints.TcpEndPoint;
			if (endPoint == null) {
				CloseConnection("No end point to node specified.");
				return;
			}

			LogDebug("EstablishTcpConnection to [{0}]", endPoint);

			if (_state != ConnectionState.Connecting) return;
			if (_connectingPhase != ConnectingPhase.EndPointDiscovery) return;

			_connectingPhase = ConnectingPhase.ConnectionEstablishing;
			_connection = new TcpPackageConnection(
				_settings.Log,
				endPoint,
				Guid.NewGuid(),
				_settings.UseSslConnection,
				_settings.TargetHost,
				_settings.ValidateServer,
				_settings.ClientConnectionTimeout,
				(connection, package) => EnqueueMessage(new HandleTcpPackageMessage(connection, package)),
				(connection, exc) => EnqueueMessage(new TcpConnectionErrorMessage(connection, exc)),
				connection => EnqueueMessage(new TcpConnectionEstablishedMessage(connection)),
				(connection, error) => EnqueueMessage(new TcpConnectionClosedMessage(connection, error)));
			_connection.StartReceiving();
		}

		private void TcpConnectionError(TcpPackageConnection connection, Exception exception) {
			if (_connection != connection) return;
			if (_state == ConnectionState.Closed) return;

			LogDebug("TcpConnectionError connId {0:B}, exc {1}.", connection.ConnectionId, exception);
			CloseConnection("TCP connection error occurred.", exception);
		}

		private void CloseConnection(string reason, Exception exception = null) {
			if (_state == ConnectionState.Closed) {
				LogDebug("CloseConnection IGNORED because is ESConnection is CLOSED, reason {0}, exception {1}.",
					reason, exception);
				return;
			}

			LogDebug("CloseConnection, reason {0}, exception {1}.", reason, exception);

			_state = ConnectionState.Closed;

			_timer.Dispose();
			_operations.CleanUp();
			_subscriptions.CleanUp();
			CloseTcpConnection(reason);

			LogInfo("Closed. Reason: {0}.", reason);

			if (exception != null)
				RaiseErrorOccurred(exception);

			RaiseClosed(reason);
		}

		private void CloseTcpConnection(string reason) {
			if (_connection == null) {
				LogDebug("CloseTcpConnection IGNORED because _connection == null");
				return;
			}

			LogDebug("CloseTcpConnection");
			_connection.Close(reason);
			TcpConnectionClosed(_connection);
			_connection = null;
		}

		private void TcpConnectionClosed(TcpPackageConnection connection) {
			if (_state == ConnectionState.Init) throw new Exception();
			if (_state == ConnectionState.Closed || _connection != connection) {
				LogDebug(
					"IGNORED (_state: {0}, _conn.ID: {1:B}, conn.ID: {2:B}): TCP connection to [{3}, L{4}] closed.",
					_state, _connection == null ? Guid.Empty : _connection.ConnectionId, connection.ConnectionId,
					connection.RemoteEndPoint, connection.LocalEndPoint);
				return;
			}

			_state = ConnectionState.Connecting;
			_connectingPhase = ConnectingPhase.Reconnecting;

			LogDebug("TCP connection to [{0}, L{1}, {2:B}] closed.", connection.RemoteEndPoint,
				connection.LocalEndPoint, connection.ConnectionId);

			_subscriptions.PurgeSubscribedAndDroppedSubscriptions(_connection.ConnectionId);
			_reconnInfo = new ReconnectionInfo(_reconnInfo.ReconnectionAttempt, _stopwatch.Elapsed);

			if (Interlocked.CompareExchange(ref _wasConnected, 0, 1) == 1) {
				RaiseDisconnected(connection.RemoteEndPoint);
			}
		}

		private void TcpConnectionEstablished(TcpPackageConnection connection) {
			if (_state != ConnectionState.Connecting || _connection != connection || connection.IsClosed) {
				LogDebug(
					"IGNORED (_state {0}, _conn.Id {1:B}, conn.Id {2:B}, conn.closed {3}): TCP connection to [{4}, L{5}] established.",
					_state, _connection == null ? Guid.Empty : _connection.ConnectionId, connection.ConnectionId,
					connection.IsClosed, connection.RemoteEndPoint, connection.LocalEndPoint);
				return;
			}

			LogDebug("TCP connection to [{0}, L{1}, {2:B}] established.", connection.RemoteEndPoint,
				connection.LocalEndPoint, connection.ConnectionId);
			_heartbeatInfo = new HeartbeatInfo(_packageNumber, true, _stopwatch.Elapsed);

			if (_settings.DefaultUserCredentials != null) {
				_connectingPhase = ConnectingPhase.Authentication;

				_authInfo = new AuthInfo(Guid.NewGuid(), _stopwatch.Elapsed);
				_connection.EnqueueSend(new TcpPackage(TcpCommand.Authenticate,
					TcpFlags.Authenticated,
					_authInfo.CorrelationId,
					_settings.DefaultUserCredentials.Username,
					_settings.DefaultUserCredentials.Password,
					null));
			} else {
				GoToIdentifyState();
			}
		}

		private void GoToIdentifyState() {
			Ensure.NotNull(_connection, "_connection");
			_connectingPhase = ConnectingPhase.Identification;

			_identifyInfo = new IdentifyInfo(Guid.NewGuid(), _stopwatch.Elapsed);
			var dto = new ClientMessage.IdentifyClient(ClientVersion, _esConnection.ConnectionName);
			_connection.EnqueueSend(new TcpPackage(TcpCommand.IdentifyClient, _identifyInfo.CorrelationId,
				dto.Serialize()));
		}

		private void GoToConnectedState() {
			Ensure.NotNull(_connection, "_connection");

			_state = ConnectionState.Connected;
			_connectingPhase = ConnectingPhase.Connected;

			Interlocked.CompareExchange(ref _wasConnected, 1, 0);

			RaiseConnectedEvent(_connection.RemoteEndPoint);

			if (_stopwatch.Elapsed - _lastTimeoutsTimeStamp >= _settings.OperationTimeoutCheckPeriod) {
				_operations.CheckTimeoutsAndRetry(_connection);
				_subscriptions.CheckTimeoutsAndRetry(_connection);
				_lastTimeoutsTimeStamp = _stopwatch.Elapsed;
			}
		}

		private void TimerTick() {
			switch (_state) {
				case ConnectionState.Init: break;
				case ConnectionState.Connecting: {
					if (_connectingPhase == ConnectingPhase.Reconnecting &&
					    _stopwatch.Elapsed - _reconnInfo.TimeStamp >= _settings.ReconnectionDelay) {
						LogDebug("TimerTick checking reconnection...");

						_reconnInfo = new ReconnectionInfo(_reconnInfo.ReconnectionAttempt + 1, _stopwatch.Elapsed);
						if (_settings.MaxReconnections >= 0 &&
						    _reconnInfo.ReconnectionAttempt > _settings.MaxReconnections)
							CloseConnection("Reconnection limit reached.");
						else {
							RaiseReconnecting();
							_operations.CheckTimeoutsAndRetry(_connection);
							DiscoverEndPoint(null);
						}
					}

					if (_connectingPhase == ConnectingPhase.Authentication &&
					    _stopwatch.Elapsed - _authInfo.TimeStamp >= _settings.OperationTimeout) {
						RaiseAuthenticationFailed("Authentication timed out.");
						GoToIdentifyState();
					}

					if (_connectingPhase == ConnectingPhase.Identification &&
					    _stopwatch.Elapsed - _identifyInfo.TimeStamp >= _settings.OperationTimeout) {
						const string msg = "Timed out waiting for client to be identified";
						LogDebug(msg);
						CloseTcpConnection(msg);
					}

					if (_connectingPhase > ConnectingPhase.ConnectionEstablishing)
						ManageHeartbeats();
					break;
				}
				case ConnectionState.Connected: {
					// operations timeouts are checked only if connection is established and check period time passed
					if (_stopwatch.Elapsed - _lastTimeoutsTimeStamp >= _settings.OperationTimeoutCheckPeriod) {
						// On mono even impossible connection first says that it is established
						// so clearing of reconnection count on ConnectionEstablished event causes infinite reconnections.
						// So we reset reconnection count to zero on each timeout check period when connection is established
						_reconnInfo = new ReconnectionInfo(0, _stopwatch.Elapsed);
						_operations.CheckTimeoutsAndRetry(_connection);
						_subscriptions.CheckTimeoutsAndRetry(_connection);
						_lastTimeoutsTimeStamp = _stopwatch.Elapsed;
					}

					ManageHeartbeats();
					break;
				}
				case ConnectionState.Closed: break;
				default: throw new Exception(string.Format("Unknown state: {0}.", _state));
			}
		}

		private void ManageHeartbeats() {
			if (_connection == null) throw new Exception();

			var timeout = _heartbeatInfo.IsIntervalStage ? _settings.HeartbeatInterval : _settings.HeartbeatTimeout;
			if (_stopwatch.Elapsed - _heartbeatInfo.TimeStamp < timeout)
				return;

			var packageNumber = _packageNumber;
			if (_heartbeatInfo.LastPackageNumber != packageNumber) {
				_heartbeatInfo = new HeartbeatInfo(packageNumber, true, _stopwatch.Elapsed);
				return;
			}

			if (_heartbeatInfo.IsIntervalStage) {
				// TcpMessage.Heartbeat analog
				_connection.EnqueueSend(new TcpPackage(TcpCommand.HeartbeatRequestCommand, Guid.NewGuid(), null));
				_heartbeatInfo = new HeartbeatInfo(_heartbeatInfo.LastPackageNumber, false, _stopwatch.Elapsed);
			} else {
				// TcpMessage.HeartbeatTimeout analog
				var msg = string.Format(
					"EventStoreConnection '{0}': closing TCP connection [{1}, {2}, {3}] due to HEARTBEAT TIMEOUT at pkgNum {4}.",
					_esConnection.ConnectionName, _connection.RemoteEndPoint, _connection.LocalEndPoint,
					_connection.ConnectionId, packageNumber);
				_settings.Log.Info(msg);
				CloseTcpConnection(msg);
			}
		}

		private void StartOperation(IClientOperation operation, int maxRetries, TimeSpan timeout) {
			switch (_state) {
				case ConnectionState.Init:
					operation.Fail(new InvalidOperationException(
						string.Format("EventStoreConnection '{0}' is not active.", _esConnection.ConnectionName)));
					break;
				case ConnectionState.Connecting:
					LogDebug("StartOperation enqueue {0}, {1}, {2}, {3}.", operation.GetType().Name, operation,
						maxRetries, timeout);
					_operations.EnqueueOperation(new OperationItem(operation, maxRetries, timeout));
					break;
				case ConnectionState.Connected:
					LogDebug("StartOperation schedule {0}, {1}, {2}, {3}.", operation.GetType().Name, operation,
						maxRetries, timeout);
					_operations.ScheduleOperation(new OperationItem(operation, maxRetries, timeout), _connection);
					break;
				case ConnectionState.Closed:
					operation.Fail(new ObjectDisposedException(_esConnection.ConnectionName));
					break;
				default: throw new Exception(string.Format("Unknown state: {0}.", _state));
			}
		}

		private void StartSubscription(StartSubscriptionMessage msg) {
			switch (_state) {
				case ConnectionState.Init:
					msg.Source.SetException(new InvalidOperationException(
						string.Format("EventStoreConnection '{0}' is not active.", _esConnection.ConnectionName)));
					break;
				case ConnectionState.Connecting:
				case ConnectionState.Connected:
					var operation = new VolatileSubscriptionOperation(_settings.Log, msg.Source, msg.StreamId,
						msg.ResolveLinkTos,
						msg.UserCredentials, msg.EventAppeared, msg.SubscriptionDropped,
						_settings.VerboseLogging, () => _connection);
					LogDebug("StartSubscription {4} {0}, {1}, {2}, {3}.", operation.GetType().Name, operation,
						msg.MaxRetries, msg.Timeout, _state == ConnectionState.Connected ? "fire" : "enqueue");
					var subscription = new SubscriptionItem(operation, msg.MaxRetries, msg.Timeout);
					if (_state == ConnectionState.Connecting)
						_subscriptions.EnqueueSubscription(subscription);
					else
						_subscriptions.StartSubscription(subscription, _connection);
					break;
				case ConnectionState.Closed:
					msg.Source.SetException(new ObjectDisposedException(_esConnection.ConnectionName));
					break;
				default: throw new Exception(string.Format("Unknown state: {0}.", _state));
			}
		}

		private void StartSubscription(StartPersistentSubscriptionMessage msg) {
			switch (_state) {
				case ConnectionState.Init:
					msg.Source.SetException(new InvalidOperationException(
						string.Format("EventStoreConnection '{0}' is not active.", _esConnection.ConnectionName)));
					break;
				case ConnectionState.Connecting:
				case ConnectionState.Connected:
					var operation = new ConnectToPersistentSubscriptionOperation(_settings.Log, msg.Source,
						msg.SubscriptionId, msg.BufferSize, msg.StreamId,
						msg.UserCredentials, msg.EventAppeared, msg.SubscriptionDropped,
						_settings.VerboseLogging, () => _connection);
					LogDebug("StartSubscription {4} {0}, {1}, {2}, {3}.", operation.GetType().Name, operation,
						msg.MaxRetries, msg.Timeout, _state == ConnectionState.Connected ? "fire" : "enqueue");
					var subscription = new SubscriptionItem(operation, msg.MaxRetries, msg.Timeout);
					if (_state == ConnectionState.Connecting)
						_subscriptions.EnqueueSubscription(subscription);
					else
						_subscriptions.StartSubscription(subscription, _connection);
					break;
				case ConnectionState.Closed:
					msg.Source.SetException(new ObjectDisposedException(_esConnection.ConnectionName));
					break;
				default: throw new Exception(string.Format("Unknown state: {0}.", _state));
			}
		}

		private void HandleTcpPackage(TcpPackageConnection connection, TcpPackage package) {
			if (_connection != connection || _state == ConnectionState.Closed || _state == ConnectionState.Init) {
				LogDebug("IGNORED: HandleTcpPackage connId {0}, package {1}, {2}.", connection.ConnectionId,
					package.Command, package.CorrelationId);
				return;
			}

			LogDebug("HandleTcpPackage connId {0}, package {1}, {2}.", _connection.ConnectionId, package.Command,
				package.CorrelationId);
			_packageNumber += 1;

			if (package.Command == TcpCommand.HeartbeatResponseCommand)
				return;
			if (package.Command == TcpCommand.HeartbeatRequestCommand) {
				_connection.EnqueueSend(
					new TcpPackage(TcpCommand.HeartbeatResponseCommand, package.CorrelationId, null));
				return;
			}

			if (package.Command == TcpCommand.Authenticated || package.Command == TcpCommand.NotAuthenticated) {
				if (_state == ConnectionState.Connecting
				    && _connectingPhase == ConnectingPhase.Authentication
				    && _authInfo.CorrelationId == package.CorrelationId) {
					if (package.Command == TcpCommand.NotAuthenticated)
						RaiseAuthenticationFailed("Not authenticated");

					GoToIdentifyState();
					return;
				}
			}

			if (package.Command == TcpCommand.ClientIdentified) {
				if (_state == ConnectionState.Connecting
				    && _identifyInfo.CorrelationId == package.CorrelationId) {
					GoToConnectedState();
					return;
				}
			}

			if (package.Command == TcpCommand.BadRequest && package.CorrelationId == Guid.Empty) {
				string message = Helper.EatException(() =>
					Helper.UTF8NoBom.GetString(package.Data.Array, package.Data.Offset, package.Data.Count));
				var exc = new EventStoreConnectionException(
					string.Format("Bad request received from server. Error: {0}",
						string.IsNullOrEmpty(message) ? "<no message>" : message));
				CloseConnection("Connection-wide BadRequest received. Too dangerous to continue.", exc);
				return;
			}

			OperationItem operation;
			SubscriptionItem subscription;
			if (_operations.TryGetActiveOperation(package.CorrelationId, out operation)) {
				var result = operation.Operation.InspectPackage(package);
				LogDebug("HandleTcpPackage OPERATION DECISION {0} ({1}), {2}", result.Decision, result.Description,
					operation);
				switch (result.Decision) {
					case InspectionDecision.DoNothing: break;
					case InspectionDecision.EndOperation:
						_operations.RemoveOperation(operation);
						break;
					case InspectionDecision.Retry:
						_operations.ScheduleOperationRetry(operation);
						break;
					case InspectionDecision.Reconnect:
						ReconnectTo(new NodeEndPoints(result.TcpEndPoint, result.SecureTcpEndPoint));
						_operations.ScheduleOperationRetry(operation);
						break;
					default: throw new Exception(string.Format("Unknown InspectionDecision: {0}", result.Decision));
				}

				if (_state == ConnectionState.Connected)
					_operations.TryScheduleWaitingOperations(connection);
			} else if (_subscriptions.TryGetActiveSubscription(package.CorrelationId, out subscription)) {
				var result = subscription.Operation.InspectPackage(package);
				LogDebug("HandleTcpPackage SUBSCRIPTION DECISION {0} ({1}), {2}", result.Decision, result.Description,
					subscription);
				switch (result.Decision) {
					case InspectionDecision.DoNothing: break;
					case InspectionDecision.EndOperation:
						_subscriptions.RemoveSubscription(subscription);
						break;
					case InspectionDecision.Retry:
						_subscriptions.ScheduleSubscriptionRetry(subscription);
						break;
					case InspectionDecision.Reconnect:
						ReconnectTo(new NodeEndPoints(result.TcpEndPoint, result.SecureTcpEndPoint));
						_subscriptions.ScheduleSubscriptionRetry(subscription);
						break;
					case InspectionDecision.Subscribed:
						subscription.IsSubscribed = true;
						break;
					default: throw new Exception(string.Format("Unknown InspectionDecision: {0}", result.Decision));
				}
			} else {
				LogDebug("HandleTcpPackage UNMAPPED PACKAGE with CorrelationId {0:B}, Command: {1}",
					package.CorrelationId, package.Command);
			}
		}

		private void ReconnectTo(NodeEndPoints endPoints) {
			IPEndPoint endPoint = _settings.UseSslConnection
				? endPoints.SecureTcpEndPoint ?? endPoints.TcpEndPoint
				: endPoints.TcpEndPoint;
			if (endPoint == null) {
				CloseConnection("No end point is specified while trying to reconnect.");
				return;
			}

			if (_state != ConnectionState.Connected || _connection.RemoteEndPoint.Equals(endPoint))
				return;

			var msg = string.Format(
				"EventStoreConnection '{0}': going to reconnect to [{1}]. Current endpoint: [{2}, L{3}].",
				_esConnection.ConnectionName, endPoint, _connection.RemoteEndPoint, _connection.LocalEndPoint);
			if (_settings.VerboseLogging) _settings.Log.Info(msg);
			CloseTcpConnection(msg);

			_state = ConnectionState.Connecting;
			_connectingPhase = ConnectingPhase.EndPointDiscovery;
			EstablishTcpConnection(endPoints);
		}

		private void LogDebug(string message, params object[] parameters) {
			if (_settings.VerboseLogging)
				_settings.Log.Debug("EventStoreConnection '{0}': {1}.", _esConnection.ConnectionName,
					parameters.Length == 0 ? message : string.Format(message, parameters));
		}

		private void LogInfo(string message, params object[] parameters) {
			if (_settings.VerboseLogging)
				_settings.Log.Info("EventStoreConnection '{0}': {1}.", _esConnection.ConnectionName,
					parameters.Length == 0 ? message : string.Format(message, parameters));
		}

		private void RaiseConnectedEvent(IPEndPoint remoteEndPoint) {
			Connected(_esConnection, new ClientConnectionEventArgs(_esConnection, remoteEndPoint));
		}

		private void RaiseDisconnected(IPEndPoint remoteEndPoint) {
			Disconnected(_esConnection, new ClientConnectionEventArgs(_esConnection, remoteEndPoint));
		}

		private void RaiseClosed(string reason) {
			Closed(_esConnection, new ClientClosedEventArgs(_esConnection, reason));
		}

		private void RaiseErrorOccurred(Exception exception) {
			ErrorOccurred(_esConnection, new ClientErrorEventArgs(_esConnection, exception));
		}

		private void RaiseReconnecting() {
			Reconnecting(_esConnection, new ClientReconnectingEventArgs(_esConnection));
		}

		private void RaiseAuthenticationFailed(string reason) {
			AuthenticationFailed(_esConnection, new ClientAuthenticationFailedEventArgs(_esConnection, reason));
		}

		public event EventHandler<ClientConnectionEventArgs> Connected = delegate { };
		public event EventHandler<ClientConnectionEventArgs> Disconnected = delegate { };
		public event EventHandler<ClientReconnectingEventArgs> Reconnecting = delegate { };
		public event EventHandler<ClientClosedEventArgs> Closed = delegate { };
		public event EventHandler<ClientErrorEventArgs> ErrorOccurred = delegate { };
		public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed = delegate { };

		private struct HeartbeatInfo {
			public readonly int LastPackageNumber;
			public readonly bool IsIntervalStage;
			public readonly TimeSpan TimeStamp;

			public HeartbeatInfo(int lastPackageNumber, bool isIntervalStage, TimeSpan timeStamp) {
				LastPackageNumber = lastPackageNumber;
				IsIntervalStage = isIntervalStage;
				TimeStamp = timeStamp;
			}
		}

		private struct ReconnectionInfo {
			public readonly int ReconnectionAttempt;
			public readonly TimeSpan TimeStamp;

			public ReconnectionInfo(int reconnectionAttempt, TimeSpan timeStamp) {
				ReconnectionAttempt = reconnectionAttempt;
				TimeStamp = timeStamp;
			}
		}

		private struct AuthInfo {
			public readonly Guid CorrelationId;
			public readonly TimeSpan TimeStamp;

			public AuthInfo(Guid correlationId, TimeSpan timeStamp) {
				CorrelationId = correlationId;
				TimeStamp = timeStamp;
			}
		}

		private struct IdentifyInfo {
			public readonly Guid CorrelationId;
			public readonly TimeSpan TimeStamp;

			public IdentifyInfo(Guid correlationId, TimeSpan timeStamp) {
				CorrelationId = correlationId;
				TimeStamp = timeStamp;
			}
		}

		private enum ConnectionState {
			Init,
			Connecting,
			Connected,
			Closed
		}

		private enum ConnectingPhase {
			Invalid,
			Reconnecting,
			EndPointDiscovery,
			ConnectionEstablishing,
			Authentication,
			Identification,
			Connected
		}
	}
}
