using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.Common.Utils;
using System.Collections.Concurrent;
using EventStore.ClientAPI.Common.Utils.Threading;

namespace EventStore.ClientAPI.Transport.Tcp {
	internal class TcpConnection : TcpConnectionBase, ITcpConnection {
		internal const int MaxSendPacketSize = 64 * 1024;

		internal static readonly SocketArgsPool SocketArgsPool = new SocketArgsPool("TcpConnection.SocketArgsPool",
			TcpConfiguration.SendReceivePoolSize,
			() => new SocketAsyncEventArgs());

		internal static ITcpConnection CreateConnectingConnection(ILogger log,
			Guid connectionId,
			IPEndPoint remoteEndPoint,
			TcpClientConnector connector,
			TimeSpan connectionTimeout,
			Action<ITcpConnection> onConnectionEstablished,
			Action<ITcpConnection, SocketError> onConnectionFailed,
			Action<ITcpConnection, SocketError> onConnectionClosed) {
			var connection = new TcpConnection(log, connectionId, remoteEndPoint, onConnectionClosed);
// ReSharper disable ImplicitlyCapturedClosure
			connector.InitConnect(remoteEndPoint,
				(socket) => {
					connection.InitSocket(socket);
				},
				(_, socket) => {
					connection.InitSendReceive();
					if (onConnectionEstablished != null)
						ThreadPool.QueueUserWorkItem(o => onConnectionEstablished(connection));
				},
				(_, socketError) => {
					if (onConnectionFailed != null)
						ThreadPool.QueueUserWorkItem(o => onConnectionFailed(connection, socketError));
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

		private readonly Queue<ArraySegment<byte>> _receiveQueue = new Queue<ArraySegment<byte>>();
		private readonly MemoryStream _memoryStream = new MemoryStream();

		private readonly object _receivingLock = new object();
		private readonly object _sendLock = new object();
		private readonly object _closeLock = new object();

		private bool _isSending;
		private volatile bool _isClosed;

		private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;
		private readonly Action<ITcpConnection, SocketError> _onConnectionClosed;

		private TcpConnection(ILogger log, Guid connectionId, IPEndPoint remoteEndPoint,
			Action<ITcpConnection, SocketError> onConnectionClosed)
			: base(remoteEndPoint) {
			Ensure.NotNull(log, "log");
			Ensure.NotEmptyGuid(connectionId, "connectionId");

			_connectionId = connectionId;
			_log = log;
			_onConnectionClosed = onConnectionClosed;
		}

		private void InitSocket(Socket socket) {
			_socket = socket;
		}

		private void InitSendReceive() {
			InitConnectionBase(_socket);
			//_log.Info("TcpConnection::InitSocket[{0}, L{1}]", RemoteEndPoint, LocalEndPoint);
			lock (_sendLock) {
				try {
					_socket.NoDelay = true;
				} catch (ObjectDisposedException) {
					CloseInternal(SocketError.Shutdown, "Socket disposed.");
					return;
				} catch (SocketException) {
					CloseInternal(SocketError.Shutdown, "Socket is disposed.");
					return;
				}

				var receiveSocketArgs = SocketArgsPool.Get();
				_receiveSocketArgs = receiveSocketArgs;
				_receiveSocketArgs.AcceptSocket = _socket;
				_receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

				var sendSocketArgs = SocketArgsPool.Get();
				_sendSocketArgs = sendSocketArgs;
				_sendSocketArgs.AcceptSocket = _socket;
				_sendSocketArgs.Completed += OnSendAsyncCompleted;
			}

			StartReceive();
			TrySend();
		}

		public void EnqueueSend(IEnumerable<ArraySegment<byte>> data) {
			lock (_sendLock) {
				int bytes = 0;
				foreach (var segment in data) {
					_sendQueue.Enqueue(segment);
					bytes += segment.Count;
				}

				NotifySendScheduled(bytes);
			}

			TrySend();
		}

		private void TrySend() {
			bool continueSendSynchronously = true;
			try {
				do {
					lock (_sendLock) {
						if (_isSending || _sendQueue.IsEmpty || _sendSocketArgs == null) return;
						if (TcpConnectionMonitor.Default.IsSendBlocked()) return;
						_isSending = true;
					}

					_memoryStream.SetLength(0);

					ArraySegment<byte> sendPiece;
					while (_sendQueue.TryDequeue(out sendPiece)) {
						_memoryStream.Write(sendPiece.Array, sendPiece.Offset, sendPiece.Count);
						if (_memoryStream.Length >= MaxSendPacketSize)
							break;
					}

					_sendSocketArgs.SetBuffer(_memoryStream.GetBuffer(), 0, (int)_memoryStream.Length);

					NotifySendStarting(_sendSocketArgs.Count);
					var firedAsync = _sendSocketArgs.AcceptSocket.SendAsync(_sendSocketArgs);
					if (firedAsync) {
						continueSendSynchronously = false;
					} else {
						continueSendSynchronously = ProcessSend(_sendSocketArgs);
					}
				} while (continueSendSynchronously);
			} catch (ObjectDisposedException) {
				ReturnSendingSocketArgs();
			}
		}

		private void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e) {
			if (ProcessSend(e)) {
				TrySend();
			}
		}

		private bool ProcessSend(SocketAsyncEventArgs socketArgs) {
			if (socketArgs.SocketError != SocketError.Success) {
				NotifySendCompleted(0);
				ReturnSendingSocketArgs();
				CloseInternal(socketArgs.SocketError, "Socket send error.");
				return false;
			} else {
				NotifySendCompleted(socketArgs.Count);
				if (_isClosed) {
					ReturnSendingSocketArgs();
					return false;
				} else {
					lock (_sendLock) {
						_isSending = false;
					}

					return true;
				}
			}
		}

		public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback) {
			if (callback == null)
				throw new ArgumentNullException("callback");

			lock (_receivingLock) {
				if (_receiveCallback != null)
					throw new InvalidOperationException(
						"ReceiveAsync called again while previous call wasn't fulfilled");
				_receiveCallback = callback;
			}

			TryDequeueReceivedData();
		}

		private void StartReceive() {

			try {

				bool continueReceiveSynchronously = true;

				do {

					var buffer = new ArraySegment<byte>(new byte[TcpConfiguration.SocketBufferSize]);
					_receiveSocketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

					NotifyReceiveStarting();
					bool firedAsync = _receiveSocketArgs.AcceptSocket.ReceiveAsync(_receiveSocketArgs);
					if (firedAsync) {
						continueReceiveSynchronously = false;
					} else {

						var processReceiveSuccess = ProcessReceive(_receiveSocketArgs);

						if (processReceiveSuccess) {
							TryDequeueReceivedData();
						}

						continueReceiveSynchronously = processReceiveSuccess;
					}

				} while (continueReceiveSynchronously);

			} catch (ObjectDisposedException) {
				ReturnReceivingSocketArgs();
			}
		}

		private void OnReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e) {
			if (ProcessReceive(e)) {
				TryDequeueReceivedData();
				StartReceive();
			}
		}

		private bool ProcessReceive(SocketAsyncEventArgs socketArgs) {
			// socket closed normally or some error occurred
			if (socketArgs.BytesTransferred == 0 || socketArgs.SocketError != SocketError.Success) {
				NotifyReceiveCompleted(0);
				ReturnReceivingSocketArgs();
				CloseInternal(socketArgs.SocketError,
					socketArgs.SocketError != SocketError.Success ? "Socket receive error" : "Socket closed");
				return false;
			}

			NotifyReceiveCompleted(socketArgs.BytesTransferred);

			var receiveBuffer =
				new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.BytesTransferred);
			lock (_receivingLock) {
				_receiveQueue.Enqueue(receiveBuffer);
			}

			lock (_receiveSocketArgs) {
				if (socketArgs.Buffer == null)
					throw new Exception("Cleaning already null buffer");
				socketArgs.SetBuffer(null, 0, 0);
			}

			return true;
		}

		private void TryDequeueReceivedData() {
			Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback;
			List<ArraySegment<byte>> res;
			lock (_receivingLock) {
				// no awaiting callback or no data to dequeue
				if (_receiveCallback == null || _receiveQueue.Count == 0)
					return;

				res = new List<ArraySegment<byte>>(_receiveQueue.Count);
				while (_receiveQueue.Count > 0) {
					res.Add(_receiveQueue.Dequeue());
				}

				callback = _receiveCallback;
				_receiveCallback = null;
			}

			lock (_closeLock) {
				if(!_isClosed)
					callback(this, res);
			}

			int bytes = 0;
			for (int i = 0, n = res.Count; i < n; ++i) {
				bytes += res[i].Count;
			}

			NotifyReceiveDispatched(bytes);
		}

		public void Close(string reason) {
			CloseInternal(SocketError.Success, reason ?? "Normal socket close."); // normal socket closing
		}

		private void CloseInternal(SocketError socketError, string reason) {
			lock (_closeLock) {
				if (_isClosed) return;
				_isClosed = true;
			}

			NotifyClosed();

			_log.Info("ClientAPI {0} closed [{1:HH:mm:ss.fff}: N{2}, L{3}, {4:B}]:", GetType().Name, DateTime.UtcNow,
				RemoteEndPoint, LocalEndPoint, _connectionId);
			_log.Info("Received bytes: {0}, Sent bytes: {1}", TotalBytesReceived, TotalBytesSent);
			_log.Info("Send calls: {0}, callbacks: {1}", SendCalls, SendCallbacks);
			_log.Info("Receive calls: {0}, callbacks: {1}", ReceiveCalls, ReceiveCallbacks);
			_log.Info("Close reason: [{0}] {1}", socketError, reason);

			if (_socket != null) {
				Helper.EatException(() => _socket.Shutdown(SocketShutdown.Both));
				Helper.EatException(() => _socket.Close());
			}

			lock (_sendLock) {
				if (!_isSending)
					ReturnSendingSocketArgs();
			}

			if (_onConnectionClosed != null)
				_onConnectionClosed(this, socketError);
		}

		private void ReturnSendingSocketArgs() {
			var socketArgs = Interlocked.Exchange(ref _sendSocketArgs, null);
			if (socketArgs != null) {
				socketArgs.Completed -= OnSendAsyncCompleted;
				socketArgs.AcceptSocket = null;
				if (socketArgs.Buffer != null)
					socketArgs.SetBuffer(null, 0, 0);
				SocketArgsPool.Return(socketArgs);
			}
		}

		private void ReturnReceivingSocketArgs() {
			var socketArgs = Interlocked.Exchange(ref _receiveSocketArgs, null);
			if (socketArgs != null) {
				socketArgs.Completed -= OnReceiveAsyncCompleted;
				socketArgs.AcceptSocket = null;
				if (socketArgs.Buffer != null)
					socketArgs.SetBuffer(null, 0, 0);
				SocketArgsPool.Return(socketArgs);
			}
		}

		public override string ToString() {
			return RemoteEndPoint.ToString();
		}
	}
}
