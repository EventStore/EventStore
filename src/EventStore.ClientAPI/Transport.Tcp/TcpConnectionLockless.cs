using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.ClientAPI.Common.Utils;
using System.Collections.Concurrent;
using EventStore.ClientAPI.Common.Utils.Threading;

namespace EventStore.ClientAPI.Transport.Tcp {
	internal class TcpConnectionLockless : TcpConnectionBase, ITcpConnection {
		internal static ITcpConnection CreateConnectingConnection(ILogger log,
			Guid connectionId,
			IPEndPoint remoteEndPoint,
			TcpClientConnector connector,
			TimeSpan connectionTimeout,
			Action<ITcpConnection> onConnectionEstablished,
			Action<ITcpConnection, SocketError> onConnectionFailed,
			Action<ITcpConnection, SocketError> onConnectionClosed) {
			var connection = new TcpConnectionLockless(log, connectionId, remoteEndPoint, onConnectionClosed);
// ReSharper disable ImplicitlyCapturedClosure
			connector.InitConnect(remoteEndPoint,
				(_, socket) => {
					if (connection.InitSocket(socket)) {
						if (onConnectionEstablished != null)
							onConnectionEstablished(connection);
						connection.StartReceive();
						connection.TrySend();
					}
				},
				(_, socketError) => {
					if (onConnectionFailed != null)
						onConnectionFailed(connection, socketError);
				}, connection, connectionTimeout);
// ReSharper restore ImplicitlyCapturedClosure
			return connection;
		}

		public Guid ConnectionId {
			get { return _connectionId; }
		}

		public int SendQueueSize {
			get { return _sendQueue.Count; }
		}

		private readonly Guid _connectionId;
		private readonly ILogger _log;

		private Socket _socket;
		private SocketAsyncEventArgs _receiveSocketArgs;
		private SocketAsyncEventArgs _sendSocketArgs;

		private readonly ConcurrentQueueWrapper<ArraySegment<byte>> _sendQueue =
			new ConcurrentQueueWrapper<ArraySegment<byte>>();

		private readonly ConcurrentQueueWrapper<ArraySegment<byte>> _receiveQueue =
			new ConcurrentQueueWrapper<ArraySegment<byte>>();

		private readonly MemoryStream _memoryStream = new MemoryStream();

		private int _sending;
		private int _receiving;
		private int _closed;

		private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;
		private readonly Action<ITcpConnection, SocketError> _onConnectionClosed;

		private TcpConnectionLockless(ILogger log, Guid connectionId, IPEndPoint remoteEndPoint,
			Action<ITcpConnection, SocketError> onConnectionClosed) : base(remoteEndPoint) {
			Ensure.NotNull(log, "log");
			Ensure.NotEmptyGuid(connectionId, "connectionId");

			_connectionId = connectionId;
			_log = log;
			_onConnectionClosed = onConnectionClosed;
		}

		private bool InitSocket(Socket socket) {
			InitConnectionBase(socket);
			try {
				socket.NoDelay = true;
			} catch (ObjectDisposedException) {
				CloseInternal(SocketError.Shutdown, "Socket disposed.");
				return false;
			}

			var receiveSocketArgs = TcpConnection.SocketArgsPool.Get();
			receiveSocketArgs.AcceptSocket = socket;
			receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

			var sendSocketArgs = TcpConnection.SocketArgsPool.Get();
			sendSocketArgs.AcceptSocket = socket;
			sendSocketArgs.Completed += OnSendAsyncCompleted;

			Interlocked.Exchange(ref _socket, socket);
			Interlocked.Exchange(ref _receiveSocketArgs, receiveSocketArgs);
			Interlocked.Exchange(ref _sendSocketArgs, sendSocketArgs);

			if (Interlocked.CompareExchange(ref _closed, 0, 0) != 0) {
				CloseSocket();
				ReturnReceivingSocketArgs();
				if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
					ReturnSendingSocketArgs();
				return false;
			}

			return true;
		}

		public void EnqueueSend(IEnumerable<ArraySegment<byte>> data) {
			using (var memStream = new MemoryStream()) {
				int bytes = 0;
				foreach (var segment in data) {
					memStream.Write(segment.Array, segment.Offset, segment.Count);
					bytes += segment.Count;
				}

				_sendQueue.Enqueue(new ArraySegment<byte>(memStream.GetBuffer(), 0, (int)memStream.Length));
				NotifySendScheduled(bytes);
			}

			TrySend();
		}

		private void TrySend() {
			while (!_sendQueue.IsEmpty && Interlocked.CompareExchange(ref _sending, 1, 0) == 0) {
				if (!_sendQueue.IsEmpty && _sendSocketArgs != null) {
					//if (TcpConnectionMonitor.Default.IsSendBlocked()) return;

					_memoryStream.SetLength(0);

					ArraySegment<byte> sendPiece;
					while (_sendQueue.TryDequeue(out sendPiece)) {
						_memoryStream.Write(sendPiece.Array, sendPiece.Offset, sendPiece.Count);
						if (_memoryStream.Length >= TcpConnection.MaxSendPacketSize)
							break;
					}

					_sendSocketArgs.SetBuffer(_memoryStream.GetBuffer(), 0, (int)_memoryStream.Length);

					try {
						NotifySendStarting(_sendSocketArgs.Count);
						var firedAsync = _sendSocketArgs.AcceptSocket.SendAsync(_sendSocketArgs);
						if (!firedAsync)
							ProcessSend(_sendSocketArgs);
					} catch (ObjectDisposedException) {
						ReturnSendingSocketArgs();
					}

					return;
				}

				Interlocked.Exchange(ref _sending, 0);
				if (Interlocked.CompareExchange(ref _closed, 0, 0) != 0) {
					if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
						ReturnSendingSocketArgs();
					return;
				}
			}
		}

		private void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e) {
			// No other code should go here. All handling is the same for sync/async completion.
			ProcessSend(e);
		}

		private void ProcessSend(SocketAsyncEventArgs socketArgs) {
			if (socketArgs.SocketError != SocketError.Success) {
				NotifySendCompleted(0);
				ReturnSendingSocketArgs();
				CloseInternal(socketArgs.SocketError, "Socket send error.");
			} else {
				NotifySendCompleted(socketArgs.Count);
				Interlocked.Exchange(ref _sending, 0);
				if (Interlocked.CompareExchange(ref _closed, 0, 0) != 0) {
					if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
						ReturnSendingSocketArgs();
					return;
				}

				TrySend();
			}
		}

		public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback) {
			if (callback == null)
				throw new ArgumentNullException("callback");

			if (Interlocked.Exchange(ref _receiveCallback, callback) != null)
				throw new InvalidOperationException("ReceiveAsync called again while previous call was not fulfilled");
			TryDequeueReceivedData();
		}

		private void StartReceive() {
			try {
				NotifyReceiveStarting();

				var buffer = new ArraySegment<byte>(new byte[TcpConfiguration.SocketBufferSize]);
				_receiveSocketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
				var firedAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
				if (!firedAsync)
					ProcessReceive(_receiveSocketArgs);
			} catch (ObjectDisposedException) {
				ReturnReceivingSocketArgs();
			}
		}

		private void OnReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e) {
			// No other code should go here.  All handling is the same on async and sync completion.
			ProcessReceive(e);
		}

		private void ProcessReceive(SocketAsyncEventArgs socketArgs) {
			// socket closed normally or some error occurred
			if (socketArgs.BytesTransferred == 0 || socketArgs.SocketError != SocketError.Success) {
				NotifyReceiveCompleted(0);
				ReturnReceivingSocketArgs();
				CloseInternal(socketArgs.SocketError,
					socketArgs.SocketError != SocketError.Success ? "Socket receive error" : "Socket closed");
				return;
			}

			NotifyReceiveCompleted(socketArgs.BytesTransferred);

			var data = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.BytesTransferred);
			_receiveQueue.Enqueue(data);
			socketArgs.SetBuffer(null, 0, 0);

			StartReceive();
			TryDequeueReceivedData();
		}

		private void TryDequeueReceivedData() {
			while (!_receiveQueue.IsEmpty && Interlocked.CompareExchange(ref _receiving, 1, 0) == 0) {
				if (!_receiveQueue.IsEmpty && _receiveCallback != null) {
					var callback = Interlocked.Exchange(ref _receiveCallback, null);
					if (callback == null) {
						_log.Error("Threading issue in TryDequeueReceivedData. Callback is null.");
						throw new Exception("Threading issue in TryDequeueReceivedData. Callback is null.");
					}

					var res = new List<ArraySegment<byte>>(_receiveQueue.Count);
					ArraySegment<byte> piece;
					while (_receiveQueue.TryDequeue(out piece)) {
						res.Add(piece);
					}

					callback(this, res);

					int bytes = 0;
					for (int i = 0, n = res.Count; i < n; ++i) {
						bytes += res[i].Count;
					}

					NotifyReceiveDispatched(bytes);
				}

				Interlocked.Exchange(ref _receiving, 0);
			}
		}

		public void Close(string reason) {
			CloseInternal(SocketError.Success, reason ?? "Normal socket close."); // normal socket closing
		}

		private void CloseInternal(SocketError socketError, string reason) {
			if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0)
				return;
			NotifyClosed();
			_log.Info("ClientAPI {0} closed [{1:HH:mm:ss.fff}: N{2}, L{3}, {4:B}]:", GetType().Name, DateTime.UtcNow,
				RemoteEndPoint, LocalEndPoint, _connectionId);
			_log.Info("Received bytes: {0}, Sent bytes: {1}", TotalBytesReceived, TotalBytesSent);
			_log.Info("Send calls: {0}, callbacks: {1}", SendCalls, SendCallbacks);
			_log.Info("Receive calls: {0}, callbacks: {1}", ReceiveCalls, ReceiveCallbacks);
			_log.Info("Close reason: [{0}] {1}", socketError, reason);
			CloseSocket();
			if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
				ReturnSendingSocketArgs();
			if (_onConnectionClosed != null)
				_onConnectionClosed(this, socketError);
		}

		private void CloseSocket() {
			var socket = Interlocked.Exchange(ref _socket, null);
			if (socket != null) {
				Helper.EatException(() => socket.Shutdown(SocketShutdown.Both));
				Helper.EatException(() => socket.Close(TcpConfiguration.SocketCloseTimeoutMs));
			}
		}

		private void ReturnSendingSocketArgs() {
			var socketArgs = Interlocked.Exchange(ref _sendSocketArgs, null);
			if (socketArgs != null) {
				socketArgs.Completed -= OnSendAsyncCompleted;
				socketArgs.AcceptSocket = null;
				if (socketArgs.Buffer != null)
					socketArgs.SetBuffer(null, 0, 0);
				TcpConnection.SocketArgsPool.Return(socketArgs);
			}
		}

		private void ReturnReceivingSocketArgs() {
			var socketArgs = Interlocked.Exchange(ref _receiveSocketArgs, null);
			if (socketArgs != null) {
				socketArgs.Completed -= OnReceiveAsyncCompleted;
				socketArgs.AcceptSocket = null;
				if (socketArgs.Buffer != null)
					socketArgs.SetBuffer(null, 0, 0);
				TcpConnection.SocketArgsPool.Return(socketArgs);
			}
		}

		public override string ToString() {
			return RemoteEndPoint.ToString();
		}
	}
}
