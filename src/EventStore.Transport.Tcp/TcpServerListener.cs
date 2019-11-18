using System;
using System.Net;
using System.Net.Sockets;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Transport.Tcp {
	public class TcpServerListener {
		private static readonly ILogger Log = LogManager.GetLoggerFor<TcpServerListener>();

		private readonly IPEndPoint _serverEndPoint;
		private readonly Socket _listeningSocket;
		private readonly SocketArgsPool _acceptSocketArgsPool;
		private Action<IPEndPoint, Socket> _onSocketAccepted;

		public TcpServerListener(IPEndPoint serverEndPoint) {
			Ensure.NotNull(serverEndPoint, "serverEndPoint");

			_serverEndPoint = serverEndPoint;

			_listeningSocket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			_acceptSocketArgsPool = new SocketArgsPool("TcpServerListener.AcceptSocketArgsPool",
				TcpConfiguration.ConcurrentAccepts * 2,
				CreateAcceptSocketArgs);
		}

		private SocketAsyncEventArgs CreateAcceptSocketArgs() {
			var socketArgs = new SocketAsyncEventArgs();
			socketArgs.Completed += AcceptCompleted;
			return socketArgs;
		}

		public void StartListening(Action<IPEndPoint, Socket> callback, string securityType) {
			Ensure.NotNull(callback, "callback");

			_onSocketAccepted = callback;

			Log.Info("Starting {securityType} TCP listening on TCP endpoint: {serverEndPoint}.", securityType,
				_serverEndPoint);
			try {
				_listeningSocket.Bind(_serverEndPoint);
				_listeningSocket.Listen(TcpConfiguration.AcceptBacklogCount);
			} catch (Exception) {
				Log.Info("Failed to listen on TCP endpoint: {serverEndPoint}.", _serverEndPoint);
				Helper.EatException(() => _listeningSocket.Close(TcpConfiguration.SocketCloseTimeoutMs));
				throw;
			}

			for (int i = 0; i < TcpConfiguration.ConcurrentAccepts; ++i) {
				StartAccepting();
			}
		}

		private void StartAccepting() {
			var socketArgs = _acceptSocketArgsPool.Get();

			try {
				var firedAsync = _listeningSocket.AcceptAsync(socketArgs);
				if (!firedAsync)
					ProcessAccept(socketArgs);
			} catch (ObjectDisposedException) {
				HandleBadAccept(socketArgs);
			}
		}

		private void AcceptCompleted(object sender, SocketAsyncEventArgs e) {
			ProcessAccept(e);
		}

		private void ProcessAccept(SocketAsyncEventArgs e) {
			if (e.SocketError != SocketError.Success) {
				HandleBadAccept(e);
			} else {
				var acceptSocket = e.AcceptSocket;
				e.AcceptSocket = null;
				_acceptSocketArgsPool.Return(e);

				OnSocketAccepted(acceptSocket);
			}

			StartAccepting();
		}

		private void HandleBadAccept(SocketAsyncEventArgs socketArgs) {
			Helper.EatException(
				() => {
					if (socketArgs.AcceptSocket != null) // avoid annoying exceptions
						socketArgs.AcceptSocket.Close(TcpConfiguration.SocketCloseTimeoutMs);
				});
			socketArgs.AcceptSocket = null;
			_acceptSocketArgsPool.Return(socketArgs);
		}

		private void OnSocketAccepted(Socket socket) {
			IPEndPoint socketEndPoint;
			try {
				socketEndPoint = (IPEndPoint)socket.RemoteEndPoint;
			} catch (Exception) {
				return;
			}

			_onSocketAccepted(socketEndPoint, socket);
		}

		public void Stop() {
			Helper.EatException(() => _listeningSocket.Close(TcpConfiguration.SocketCloseTimeoutMs));
		}
	}
}
