// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Plugins.Authentication;
using EventStore.Transport.Tcp;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Transport.Tcp;

public enum TcpServiceType {
	Internal,
	External
}

public enum TcpSecurityType {
	Normal,
	Secure
}

public class TcpService(
	IPublisher publisher,
	IPEndPoint serverEndPoint,
	IPublisher networkSendQueue,
	TcpServiceType serviceType,
	TcpSecurityType securityType,
	Func<Guid, IPEndPoint, ITcpDispatcher> dispatcherFactory,
	TimeSpan heartbeatInterval,
	TimeSpan heartbeatTimeout,
	IAuthenticationProvider authProvider,
	AuthorizationGateway authorizationGateway,
	Func<X509Certificate2> certificateSelector,
	Func<X509Certificate2Collection> intermediatesSelector,
	CertificateDelegates.ClientCertificateValidator sslClientCertValidator,
	int connectionPendingSendBytesThreshold,
	int connectionQueueSizeThreshold)
	: IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.SystemStart>,
		IHandle<SystemMessage.BecomeShuttingDown> {
	private static readonly ILogger Log = Serilog.Log.ForContext<TcpService>();

	private readonly IPublisher _publisher = Ensure.NotNull(publisher);
	private readonly IPEndPoint _serverEndPoint = Ensure.NotNull(serverEndPoint);
	private readonly TcpServerListener _serverListener = new(serverEndPoint);
	private readonly IPublisher _networkSendQueue = Ensure.NotNull(networkSendQueue);
	private readonly Func<Guid, IPEndPoint, ITcpDispatcher> _dispatcherFactory = Ensure.NotNull(dispatcherFactory);
	private readonly IAuthenticationProvider _authProvider = Ensure.NotNull(authProvider);
	private readonly AuthorizationGateway _authorizationGateway = Ensure.NotNull(authorizationGateway);

	public TcpService(IPublisher publisher,
		IPEndPoint serverEndPoint,
		IPublisher networkSendQueue,
		TcpServiceType serviceType,
		TcpSecurityType securityType,
		ITcpDispatcher dispatcher,
		TimeSpan heartbeatInterval,
		TimeSpan heartbeatTimeout,
		IAuthenticationProvider authProvider,
		AuthorizationGateway authorizationGateway,
		Func<X509Certificate2> certificateSelector,
		Func<X509Certificate2Collection> intermediatesSelector,
		CertificateDelegates.ClientCertificateValidator sslClientCertValidator,
		int connectionPendingSendBytesThreshold,
		int connectionQueueSizeThreshold)
		: this(publisher, serverEndPoint, networkSendQueue, serviceType, securityType, (_, _) => dispatcher,
			heartbeatInterval, heartbeatTimeout, authProvider, authorizationGateway, certificateSelector, intermediatesSelector, sslClientCertValidator, connectionPendingSendBytesThreshold,
			connectionQueueSizeThreshold) {
	}

	public void Handle(SystemMessage.SystemInit message) {
		try {
			_serverListener.StartListening(OnConnectionAccepted, securityType.ToString());
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
		var conn = securityType == TcpSecurityType.Secure
			? TcpConnectionSsl.CreateServerFromSocket(Guid.NewGuid(), endPoint, socket, certificateSelector, intermediatesSelector, sslClientCertValidator, verbose: true)
			: TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), endPoint, socket, verbose: true);
		Log.Information(
			"{serviceType} TCP connection accepted: [{securityType}, {remoteEndPoint}, L{localEndPoint}, {connectionId:B}].",
			serviceType, securityType, conn.RemoteEndPoint, conn.LocalEndPoint, conn.ConnectionId);

		var dispatcher = _dispatcherFactory(conn.ConnectionId, _serverEndPoint);
		var manager = new TcpConnectionManager(
			$"{serviceType.ToString().ToLower()}-{securityType.ToString().ToLower()}",
			serviceType,
			dispatcher,
			_publisher,
			conn,
			_networkSendQueue,
			_authProvider,
			_authorizationGateway,
			heartbeatInterval,
			heartbeatTimeout,
			(m, e) => _publisher.Publish(new TcpMessage.ConnectionClosed(m, e)),
			connectionPendingSendBytesThreshold,
			connectionQueueSizeThreshold); // TODO AN: race condition
		_publisher.Publish(new TcpMessage.ConnectionEstablished(manager));
		manager.StartReceiving();
	}
}
