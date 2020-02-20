using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Transport.Tcp;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Tcp {
	public enum TcpServiceType {
		Internal,
		External
	}

	public enum TcpSecurityType {
		Normal,
		Secure
	}

	public class TcpService : IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.SystemStart>,
		IHandle<SystemMessage.BecomeShuttingDown> {
		private static readonly ILogger Log = Serilog.Log.ForContext<TcpService>();

		private readonly IPublisher _publisher;
		private readonly IPEndPoint _serverEndPoint;
		private readonly TcpServerListener _serverListener;
		private readonly IPublisher _networkSendQueue;
		private readonly TcpServiceType _serviceType;
		private readonly TcpSecurityType _securityType;
		private readonly Func<Guid, IPEndPoint, ITcpDispatcher> _dispatcherFactory;
		private readonly TimeSpan _heartbeatInterval;
		private readonly TimeSpan _heartbeatTimeout;
		private readonly IAuthenticationProvider _authProvider;
		private readonly X509Certificate _certificate;
		private readonly int _connectionPendingSendBytesThreshold;
		private readonly int _connectionQueueSizeThreshold;

		public TcpService(IPublisher publisher,
			IPEndPoint serverEndPoint,
			IPublisher networkSendQueue,
			TcpServiceType serviceType,
			TcpSecurityType securityType,
			ITcpDispatcher dispatcher,
			TimeSpan heartbeatInterval,
			TimeSpan heartbeatTimeout,
			IAuthenticationProvider authProvider,
			X509Certificate certificate,
			int connectionPendingSendBytesThreshold,
			int connectionQueueSizeThreshold)
			: this(publisher, serverEndPoint, networkSendQueue, serviceType, securityType, (_, __) => dispatcher,
				heartbeatInterval, heartbeatTimeout, authProvider, certificate, connectionPendingSendBytesThreshold, connectionQueueSizeThreshold) {
		}

		public TcpService(IPublisher publisher,
			IPEndPoint serverEndPoint,
			IPublisher networkSendQueue,
			TcpServiceType serviceType,
			TcpSecurityType securityType,
			Func<Guid, IPEndPoint, ITcpDispatcher> dispatcherFactory,
			TimeSpan heartbeatInterval,
			TimeSpan heartbeatTimeout,
			IAuthenticationProvider authProvider,
			X509Certificate certificate,
			int connectionPendingSendBytesThreshold,
			int connectionQueueSizeThreshold) {
			Ensure.NotNull(publisher, "publisher");
			Ensure.NotNull(serverEndPoint, "serverEndPoint");
			Ensure.NotNull(networkSendQueue, "networkSendQueue");
			Ensure.NotNull(dispatcherFactory, "dispatcherFactory");
			Ensure.NotNull(authProvider, "authProvider");
			if (securityType == TcpSecurityType.Secure)
				Ensure.NotNull(certificate, "certificate");

			_publisher = publisher;
			_serverEndPoint = serverEndPoint;
			_serverListener = new TcpServerListener(_serverEndPoint);
			_networkSendQueue = networkSendQueue;
			_serviceType = serviceType;
			_securityType = securityType;
			_dispatcherFactory = dispatcherFactory;
			_heartbeatInterval = heartbeatInterval;
			_heartbeatTimeout = heartbeatTimeout;
			_connectionPendingSendBytesThreshold = connectionPendingSendBytesThreshold;
			_connectionQueueSizeThreshold = connectionQueueSizeThreshold;
			_authProvider = authProvider;
			_certificate = certificate;
		}

		public void Handle(SystemMessage.SystemInit message) {
			try {
				_serverListener.StartListening(OnConnectionAccepted, _securityType.ToString());
			} catch (Exception e) {
				Application.Exit(ExitCode.Error, e.Message);
			}
		}

		public void Handle(SystemMessage.SystemStart message) {
		}

		public void Handle(SystemMessage.BecomeShuttingDown message) {
			_serverListener.Stop();
		}

		private void OnConnectionAccepted(IPEndPoint endPoint, Socket socket) {
			var conn = _securityType == TcpSecurityType.Secure
				? TcpConnectionSsl.CreateServerFromSocket(Guid.NewGuid(), endPoint, socket, _certificate, verbose: true)
				: TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), endPoint, socket, verbose: true);
			Log.Information(
				"{serviceType} TCP connection accepted: [{securityType}, {remoteEndPoint}, L{localEndPoint}, {connectionId:B}].",
				_serviceType, _securityType, conn.RemoteEndPoint, conn.LocalEndPoint, conn.ConnectionId);

			var dispatcher = _dispatcherFactory(conn.ConnectionId, _serverEndPoint);
			var manager = new TcpConnectionManager(
				string.Format("{0}-{1}", _serviceType.ToString().ToLower(), _securityType.ToString().ToLower()),
				_serviceType,
				dispatcher,
				_publisher,
				conn,
				_networkSendQueue,
				_authProvider,
				_heartbeatInterval,
				_heartbeatTimeout,
				(m, e) => _publisher.Publish(new TcpMessage.ConnectionClosed(m, e)),
				_connectionPendingSendBytesThreshold,
				_connectionQueueSizeThreshold); // TODO AN: race condition
			_publisher.Publish(new TcpMessage.ConnectionEstablished(manager));
			manager.StartReceiving();
		}
	}
}
