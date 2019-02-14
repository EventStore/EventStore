using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Transport.Tcp;
using EventStore.Transport.Tcp.Framing;
using EventStore.Core.Settings;

namespace EventStore.Core.Services.Transport.Tcp {
	/// <summary>
	/// Manager for individual TCP connection. It handles connection lifecycle,
	/// heartbeats, message framing and dispatch to the memory bus.
	/// </summary>
	public class TcpConnectionManager : IHandle<TcpMessage.Heartbeat>, IHandle<TcpMessage.HeartbeatTimeout> {
		public const int ConnectionQueueSizeThreshold = 50000;
		public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMilliseconds(1000);

		private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnectionManager>();

		public readonly Guid ConnectionId;
		public readonly string ConnectionName;
		public readonly IPEndPoint RemoteEndPoint;

		public IPEndPoint LocalEndPoint {
			get { return _connection.LocalEndPoint; }
		}

		public bool IsClosed {
			get { return _isClosed != 0; }
		}

		public int SendQueueSize {
			get { return _connection.SendQueueSize; }
		}

		public string ClientConnectionName {
			get { return _clientConnectionName; }
		}

		private readonly ITcpConnection _connection;
		private readonly IEnvelope _tcpEnvelope;
		private readonly IPublisher _publisher;
		private readonly ITcpDispatcher _dispatcher;
		private readonly IMessageFramer _framer;
		private int _messageNumber;
		private int _isClosed;
		private string _clientConnectionName;

		private byte _version;

		private readonly Action<TcpConnectionManager, SocketError> _connectionClosed;
		private readonly Action<TcpConnectionManager> _connectionEstablished;

		private readonly SendToWeakThisEnvelope _weakThisEnvelope;
		private readonly TimeSpan _heartbeatInterval;
		private readonly TimeSpan _heartbeatTimeout;
		private readonly int _connectionPendingSendBytesThreshold;

		private readonly IAuthenticationProvider _authProvider;
		private UserCredentials _defaultUser;
		private TcpServiceType _serviceType;

		public TcpConnectionManager(string connectionName,
			TcpServiceType serviceType,
			ITcpDispatcher dispatcher,
			IPublisher publisher,
			ITcpConnection openedConnection,
			IPublisher networkSendQueue,
			IAuthenticationProvider authProvider,
			TimeSpan heartbeatInterval,
			TimeSpan heartbeatTimeout,
			Action<TcpConnectionManager, SocketError> onConnectionClosed,
			int connectionPendingSendBytesThreshold) {
			Ensure.NotNull(dispatcher, "dispatcher");
			Ensure.NotNull(publisher, "publisher");
			Ensure.NotNull(openedConnection, "openedConnnection");
			Ensure.NotNull(networkSendQueue, "networkSendQueue");
			Ensure.NotNull(authProvider, "authProvider");

			ConnectionId = openedConnection.ConnectionId;
			ConnectionName = connectionName;

			_serviceType = serviceType;
			_tcpEnvelope = new SendOverTcpEnvelope(this, networkSendQueue);
			_publisher = publisher;
			_dispatcher = dispatcher;
			_authProvider = authProvider;

			_framer = new LengthPrefixMessageFramer();
			_framer.RegisterMessageArrivedCallback(OnMessageArrived);

			_weakThisEnvelope = new SendToWeakThisEnvelope(this);
			_heartbeatInterval = heartbeatInterval;
			_heartbeatTimeout = heartbeatTimeout;
			_connectionPendingSendBytesThreshold = connectionPendingSendBytesThreshold;

			_connectionClosed = onConnectionClosed;

			RemoteEndPoint = openedConnection.RemoteEndPoint;
			_connection = openedConnection;
			_connection.ConnectionClosed += OnConnectionClosed;
			if (_connection.IsClosed) {
				OnConnectionClosed(_connection, SocketError.Success);
				return;
			}

			ScheduleHeartbeat(0);
		}

		public TcpConnectionManager(string connectionName,
			Guid connectionId,
			ITcpDispatcher dispatcher,
			IPublisher publisher,
			IPEndPoint remoteEndPoint,
			TcpClientConnector connector,
			bool useSsl,
			string sslTargetHost,
			bool sslValidateServer,
			IPublisher networkSendQueue,
			IAuthenticationProvider authProvider,
			TimeSpan heartbeatInterval,
			TimeSpan heartbeatTimeout,
			Action<TcpConnectionManager> onConnectionEstablished,
			Action<TcpConnectionManager, SocketError> onConnectionClosed) {
			Ensure.NotEmptyGuid(connectionId, "connectionId");
			Ensure.NotNull(dispatcher, "dispatcher");
			Ensure.NotNull(publisher, "publisher");
			Ensure.NotNull(authProvider, "authProvider");
			Ensure.NotNull(remoteEndPoint, "remoteEndPoint");
			Ensure.NotNull(connector, "connector");
			if (useSsl) Ensure.NotNull(sslTargetHost, "sslTargetHost");

			ConnectionId = connectionId;
			ConnectionName = connectionName;

			_tcpEnvelope = new SendOverTcpEnvelope(this, networkSendQueue);
			_publisher = publisher;
			_dispatcher = dispatcher;
			_authProvider = authProvider;

			_framer = new LengthPrefixMessageFramer();
			_framer.RegisterMessageArrivedCallback(OnMessageArrived);

			_weakThisEnvelope = new SendToWeakThisEnvelope(this);
			_heartbeatInterval = heartbeatInterval;
			_heartbeatTimeout = heartbeatTimeout;
			_connectionPendingSendBytesThreshold = ESConsts.UnrestrictedPendingSendBytes;

			_connectionEstablished = onConnectionEstablished;
			_connectionClosed = onConnectionClosed;

			RemoteEndPoint = remoteEndPoint;
			_connection = useSsl
				? connector.ConnectSslTo(ConnectionId, remoteEndPoint, ConnectionTimeout,
					sslTargetHost, sslValidateServer, OnConnectionEstablished, OnConnectionFailed)
				: connector.ConnectTo(ConnectionId, remoteEndPoint, ConnectionTimeout, OnConnectionEstablished,
					OnConnectionFailed);
			_connection.ConnectionClosed += OnConnectionClosed;
			if (_connection.IsClosed)
				OnConnectionClosed(_connection, SocketError.Success);
		}

		private void OnConnectionEstablished(ITcpConnection connection) {
			Log.Info("Connection '{connectionName}' ({connectionId:B}) to [{remoteEndPoint}] established.",
				ConnectionName, ConnectionId, connection.RemoteEndPoint);

			ScheduleHeartbeat(0);

			var handler = _connectionEstablished;
			if (handler != null)
				handler(this);
		}

		private void OnConnectionFailed(ITcpConnection connection, SocketError socketError) {
			if (Interlocked.CompareExchange(ref _isClosed, 1, 0) != 0) return;
			Log.Info("Connection '{connectionName}' ({connectionId:B}) to [{remoteEndPoint}] failed: {e}.",
				ConnectionName, ConnectionId, connection.RemoteEndPoint, socketError);
			if (_connectionClosed != null)
				_connectionClosed(this, socketError);
		}

		private void OnConnectionClosed(ITcpConnection connection, SocketError socketError) {
			if (Interlocked.CompareExchange(ref _isClosed, 1, 0) != 0) return;
			Log.Info(
				"Connection '{connectionName}{clientConnectionName}' [{remoteEndPoint}, {connectionId:B}] closed: {e}.",
				ConnectionName, ClientConnectionName.IsEmptyString() ? string.Empty : ":" + ClientConnectionName,
				connection.RemoteEndPoint, ConnectionId, socketError);
			if (_connectionClosed != null)
				_connectionClosed(this, socketError);
		}

		public void StartReceiving() {
			_connection.ReceiveAsync(OnRawDataReceived);
		}

		private void OnRawDataReceived(ITcpConnection connection, IEnumerable<ArraySegment<byte>> data) {
			Interlocked.Increment(ref _messageNumber);

			try {
				_framer.UnFrameData(data);
			} catch (PackageFramingException exc) {
				SendBadRequestAndClose(Guid.Empty,
					string.Format("Invalid TCP frame received. Error: {0}.", exc.Message));
				return;
			}

			_connection.ReceiveAsync(OnRawDataReceived);
		}

		private void OnMessageArrived(ArraySegment<byte> data) {
			TcpPackage package;
			try {
				package = TcpPackage.FromArraySegment(data);
			} catch (Exception e) {
				SendBadRequestAndClose(Guid.Empty, string.Format("Received bad network package. Error: {0}", e));
				return;
			}

			OnPackageReceived(package);
		}

		private void OnPackageReceived(TcpPackage package) {
			try {
				ProcessPackage(package);
			} catch (Exception e) {
				SendBadRequestAndClose(package.CorrelationId,
					string.Format("Error while processing package. Error: {0}", e));
			}
		}

		public void ProcessPackage(TcpPackage package) {
			if (_serviceType == TcpServiceType.External && (package.Flags & TcpFlags.TrustedWrite) != 0) {
				SendBadRequestAndClose(package.CorrelationId,
					"Trusted writes aren't accepted over the external TCP interface");
				return;
			}

			switch (package.Command) {
				case TcpCommand.HeartbeatResponseCommand:
					break;
				case TcpCommand.HeartbeatRequestCommand:
					SendPackage(new TcpPackage(TcpCommand.HeartbeatResponseCommand, package.CorrelationId, null));
					break;
				case TcpCommand.IdentifyClient: {
					try {
						var message = (ClientMessage.IdentifyClient)_dispatcher.UnwrapPackage(package, _tcpEnvelope,
							null, null, null, this, _version);
						Log.Info(
							"Connection '{connectionName}' ({connectionId:B}) identified by client. Client connection name: '{clientConnectionName}', Client version: {clientVersion}.",
							ConnectionName, ConnectionId, message.ConnectionName, (ClientVersion)message.Version);
						_version = (byte)message.Version;
						_clientConnectionName = message.ConnectionName;
						_connection.SetClientConnectionName(_clientConnectionName);
						SendPackage(new TcpPackage(TcpCommand.ClientIdentified, package.CorrelationId, null));
					} catch (Exception ex) {
						Log.Error("Error identifying client: {e}", ex);
					}

					break;
				}
				case TcpCommand.BadRequest: {
					var reason = string.Empty;
					Helper.EatException(() =>
						reason = Helper.UTF8NoBom.GetString(package.Data.Array, package.Data.Offset,
							package.Data.Count));
					Log.Error(
						"Bad request received from '{connectionName}{clientConnectionName}' [{remoteEndPoint}, L{localEndPoint}, {connectionId:B}], will stop server. CorrelationId: {correlationId:B}, Error: {e}.",
						ConnectionName,
						ClientConnectionName.IsEmptyString() ? string.Empty : ":" + ClientConnectionName,
						RemoteEndPoint,
						LocalEndPoint, ConnectionId, package.CorrelationId,
						reason.IsEmptyString() ? "<reason missing>" : reason);
					break;
				}
				case TcpCommand.Authenticate: {
					if ((package.Flags & TcpFlags.Authenticated) == 0)
						ReplyNotAuthenticated(package.CorrelationId, "No user credentials provided.");
					else {
						var defaultUser = new UserCredentials(package.Login, package.Password, null);
						Interlocked.Exchange(ref _defaultUser, defaultUser);
						_authProvider.Authenticate(new TcpDefaultAuthRequest(this, package.CorrelationId, defaultUser));
					}

					break;
				}
				default: {
					var defaultUser = _defaultUser;
					if ((package.Flags & TcpFlags.TrustedWrite) != 0) {
						UnwrapAndPublishPackage(package, UserManagement.SystemAccount.Principal, null, null);
					} else if ((package.Flags & TcpFlags.Authenticated) != 0) {
						_authProvider.Authenticate(new TcpAuthRequest(this, package, package.Login, package.Password));
					} else if (defaultUser != null) {
						if (defaultUser.User != null)
							UnwrapAndPublishPackage(package, defaultUser.User, defaultUser.Login, defaultUser.Password);
						else
							_authProvider.Authenticate(new TcpAuthRequest(this, package, defaultUser.Login,
								defaultUser.Password));
					} else {
						UnwrapAndPublishPackage(package, null, null, null);
					}

					break;
				}
			}
		}

		private void UnwrapAndPublishPackage(TcpPackage package, IPrincipal user, string login, string password) {
			Message message = null;
			string error = "";
			try {
				message = _dispatcher.UnwrapPackage(package, _tcpEnvelope, user, login, password, this, _version);
			} catch (Exception ex) {
				error = ex.Message;
			}

			if (message != null)
				_publisher.Publish(message);
			else
				SendBadRequest(package.CorrelationId,
					string.Format("Could not unwrap network package for command {0}.\n{1}", package.Command, error));
		}

		private void ReplyNotAuthenticated(Guid correlationId, string description) {
			_tcpEnvelope.ReplyWith(new TcpMessage.NotAuthenticated(correlationId, description));
		}

		private void ReplyNotReady(Guid correlationId, string description) {
			_tcpEnvelope.ReplyWith(new ClientMessage.NotHandled(correlationId,
				TcpClientMessageDto.NotHandled.NotHandledReason.NotReady, description));
		}

		private void ReplyAuthenticated(Guid correlationId, UserCredentials userCredentials, IPrincipal user) {
			var authCredentials = new UserCredentials(userCredentials.Login, userCredentials.Password, user);
			Interlocked.CompareExchange(ref _defaultUser, authCredentials, userCredentials);
			_tcpEnvelope.ReplyWith(new TcpMessage.Authenticated(correlationId));
		}

		public void SendBadRequestAndClose(Guid correlationId, string message) {
			Ensure.NotNull(message, "message");

			SendPackage(new TcpPackage(TcpCommand.BadRequest, correlationId, Helper.UTF8NoBom.GetBytes(message)),
				checkQueueSize: false);
			Log.Error(
				"Closing connection '{connectionName}{clientConnectionName}' [{remoteEndPoint}, L{localEndPoint}, {connectionId:B}] due to error. Reason: {e}",
				ConnectionName, ClientConnectionName.IsEmptyString() ? string.Empty : ":" + ClientConnectionName,
				RemoteEndPoint, LocalEndPoint, ConnectionId, message);
			_connection.Close(message);
		}

		public void SendBadRequest(Guid correlationId, string message) {
			Ensure.NotNull(message, "message");

			SendPackage(new TcpPackage(TcpCommand.BadRequest, correlationId, Helper.UTF8NoBom.GetBytes(message)),
				checkQueueSize: false);
		}

		public void Stop(string reason = null) {
			Log.Trace(
				"Closing connection '{connectionName}{clientConnectionName}' [{remoteEndPoint}, L{localEndPoint}, {connectionId:B}] cleanly.{reason}",
				ConnectionName, ClientConnectionName.IsEmptyString() ? string.Empty : ":" + ClientConnectionName,
				RemoteEndPoint, LocalEndPoint, ConnectionId,
				reason.IsEmpty() ? string.Empty : " Reason: " + reason);
			_connection.Close(reason);
		}

		public void SendMessage(Message message) {
			var package = _dispatcher.WrapMessage(message, _version);
			if (package != null)
				SendPackage(package.Value);
		}

		private void SendPackage(TcpPackage package, bool checkQueueSize = true) {
			if (IsClosed)
				return;

			int queueSize;
			int queueSendBytes;
			if (checkQueueSize) {
				if ((queueSize = _connection.SendQueueSize) > ConnectionQueueSizeThreshold) {
					SendBadRequestAndClose(Guid.Empty,
						string.Format("Connection queue size is too large: {0}.", queueSize));
					return;
				}

				if (_connectionPendingSendBytesThreshold > ESConsts.UnrestrictedPendingSendBytes &&
				    (queueSendBytes = _connection.PendingSendBytes) > _connectionPendingSendBytesThreshold) {
					SendBadRequestAndClose(Guid.Empty,
						string.Format("Connection pending send bytes is too large: {0}.", queueSendBytes));
					return;
				}
			}

			var data = package.AsArraySegment();
			var framed = _framer.FrameData(data);
			_connection.EnqueueSend(framed);
		}

		public void Handle(TcpMessage.Heartbeat message) {
			if (IsClosed) return;

			var msgNum = _messageNumber;
			if (message.MessageNumber != msgNum)
				ScheduleHeartbeat(msgNum);
			else {
				SendPackage(new TcpPackage(TcpCommand.HeartbeatRequestCommand, Guid.NewGuid(), null));

				_publisher.Publish(TimerMessage.Schedule.Create(_heartbeatTimeout, _weakThisEnvelope,
					new TcpMessage.HeartbeatTimeout(msgNum)));
			}
		}

		public void Handle(TcpMessage.HeartbeatTimeout message) {
			if (IsClosed) return;

			var msgNum = _messageNumber;
			if (message.MessageNumber != msgNum)
				ScheduleHeartbeat(msgNum);
			else
				Stop(string.Format("HEARTBEAT TIMEOUT at msgNum {0}", msgNum));
		}

		private void ScheduleHeartbeat(int msgNum) {
			_publisher.Publish(TimerMessage.Schedule.Create(_heartbeatInterval, _weakThisEnvelope,
				new TcpMessage.Heartbeat(msgNum)));
		}

		private class SendToWeakThisEnvelope : IEnvelope {
			private readonly WeakReference _receiver;

			public SendToWeakThisEnvelope(object receiver) {
				_receiver = new WeakReference(receiver);
			}

			public void ReplyWith<T>(T message) where T : Message {
				var x = _receiver.Target as IHandle<T>;
				if (x != null)
					x.Handle(message);
			}
		}

		private class TcpAuthRequest : AuthenticationRequest {
			private readonly TcpConnectionManager _manager;
			private readonly TcpPackage _package;

			public TcpAuthRequest(TcpConnectionManager manager, TcpPackage package, string login, string password)
				: base(login, password) {
				_manager = manager;
				_package = package;
			}

			public override void Unauthorized() {
				_manager.ReplyNotAuthenticated(_package.CorrelationId, "Not Authenticated");
			}

			public override void Authenticated(IPrincipal principal) {
				_manager.UnwrapAndPublishPackage(_package, principal, Name, SuppliedPassword);
			}

			public override void Error() {
				_manager.ReplyNotAuthenticated(_package.CorrelationId, "Internal Server Error");
			}

			public override void NotReady() {
				_manager.ReplyNotReady(_package.CorrelationId, "Server not ready");
			}
		}


		private class TcpDefaultAuthRequest : AuthenticationRequest {
			private readonly TcpConnectionManager _manager;
			private readonly Guid _correlationId;
			private readonly UserCredentials _userCredentials;

			public TcpDefaultAuthRequest(TcpConnectionManager manager, Guid correlationId,
				UserCredentials userCredentials)
				: base(userCredentials.Login, userCredentials.Password) {
				_manager = manager;
				_correlationId = correlationId;
				_userCredentials = userCredentials;
			}

			public override void Unauthorized() {
				_manager.ReplyNotAuthenticated(_correlationId, "Unauthorized");
			}

			public override void Authenticated(IPrincipal principal) {
				_manager.ReplyAuthenticated(_correlationId, _userCredentials, principal);
			}

			public override void Error() {
				_manager.ReplyNotAuthenticated(_correlationId, "Internal Server Error");
			}

			public override void NotReady() {
				_manager.ReplyNotAuthenticated(_correlationId, "Server not yet ready");
			}
		}

		private class UserCredentials {
			public readonly string Login;
			public readonly string Password;
			public readonly IPrincipal User;

			public UserCredentials(string login, string password, IPrincipal user) {
				Login = login;
				Password = password;
				User = user;
			}
		}
	}
}
