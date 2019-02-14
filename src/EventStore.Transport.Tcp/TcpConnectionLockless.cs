using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.BufferManagement;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using System.Collections.Concurrent;

namespace EventStore.Transport.Tcp {
	public class TcpConnectionLockless : TcpConnectionBase, ITcpConnection {
		internal const int MaxSendPacketSize = 64 * 1024;

		internal static readonly BufferManager BufferManager =
			new BufferManager(TcpConfiguration.BufferChunksCount, TcpConfiguration.SocketBufferSize);

		private static readonly ILogger Log = LogManager.GetLoggerFor<TcpConnectionLockless>();

		private static readonly SocketArgsPool SocketArgsPool = new SocketArgsPool("TcpConnection.SocketArgsPool",
			TcpConfiguration.SendReceivePoolSize,
			() => new SocketAsyncEventArgs());

		public static ITcpConnection CreateConnectingTcpConnection(Guid connectionId,
			IPEndPoint remoteEndPoint,
			TcpClientConnector connector,
			TimeSpan connectionTimeout,
			Action<ITcpConnection> onConnectionEstablished,
			Action<ITcpConnection, SocketError> onConnectionFailed,
			bool verbose) {
			var connection = new TcpConnectionLockless(connectionId, remoteEndPoint, verbose);
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

		public static ITcpConnection CreateAcceptedTcpConnection(Guid connectionId, IPEndPoint remoteEndPoint,
			Socket socket, bool verbose) {
			var connection = new TcpConnectionLockless(connectionId, remoteEndPoint, verbose);
			if (connection.InitSocket(socket)) {
				connection.StartReceive();
				connection.TrySend();
			}

			return connection;
		}

		public event Action<ITcpConnection, SocketError> ConnectionClosed;

		public Guid ConnectionId {
			get { return _connectionId; }
		}

		public int SendQueueSize {
			get { return _sendQueue.Count; }
		}

		public string ClientConnectionName {
			get { return _clientConnectionName; }
		}

		private readonly Guid _connectionId;
		private readonly bool _verbose;
		private string _clientConnectionName;

		private Socket _socket;
		private SocketAsyncEventArgs _receiveSocketArgs;
		private SocketAsyncEventArgs _sendSocketArgs;

		private readonly ConcurrentQueueWrapper<ArraySegment<byte>> _sendQueue =
			new ConcurrentQueueWrapper<ArraySegment<byte>>();

		private readonly ConcurrentQueueWrapper<ReceivedData>
			_receiveQueue = new ConcurrentQueueWrapper<ReceivedData>();

		private readonly MemoryStream _memoryStream = new MemoryStream();

		private int _sending;
		private int _receiving;
		private int _closed;

		private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;

		private TcpConnectionLockless(Guid connectionId, IPEndPoint remoteEndPoint, bool verbose) :
			base(remoteEndPoint) {
			Ensure.NotEmptyGuid(connectionId, "connectionId");

			_connectionId = connectionId;
			_verbose = verbose;
		}

		private bool InitSocket(Socket socket) {
			InitConnectionBase(socket);
			try {
				socket.NoDelay = true;
			} catch (ObjectDisposedException) {
				CloseInternal(SocketError.Shutdown, "Socket disposed.");
				return false;
			}

			var receiveSocketArgs = SocketArgsPool.Get();
			receiveSocketArgs.AcceptSocket = socket;
			receiveSocketArgs.Completed += OnReceiveAsyncCompleted;

			var sendSocketArgs = SocketArgsPool.Get();
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
						if (_memoryStream.Length >= MaxSendPacketSize)
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
			var buffer = BufferManager.CheckOut();
			if (buffer.Array == null || buffer.Count == 0 || buffer.Array.Length < buffer.Offset + buffer.Count)
				throw new Exception("Invalid buffer allocated");

			try {
				NotifyReceiveStarting();
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

			var buf = new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count);
			_receiveQueue.Enqueue(new ReceivedData(buf, socketArgs.BytesTransferred));
			socketArgs.SetBuffer(null, 0, 0);

			StartReceive();
			TryDequeueReceivedData();
		}

		private void TryDequeueReceivedData() {
			while (!_receiveQueue.IsEmpty && Interlocked.CompareExchange(ref _receiving, 1, 0) == 0) {
				if (!_receiveQueue.IsEmpty && _receiveCallback != null) {
					var callback = Interlocked.Exchange(ref _receiveCallback, null);
					if (callback == null) {
						Log.Fatal("Some threading issue in TryDequeueReceivedData! Callback is null!");
						throw new Exception("Some threading issue in TryDequeueReceivedData! Callback is null!");
					}

					var res = new List<ReceivedData>(_receiveQueue.Count);
					ReceivedData piece;
					while (_receiveQueue.TryDequeue(out piece)) {
						res.Add(piece);
					}

					var data = new ArraySegment<byte>[res.Count];
					int bytes = 0;
					for (int i = 0; i < data.Length; ++i) {
						var d = res[i];
						bytes += d.DataLen;
						data[i] = new ArraySegment<byte>(d.Buf.Array, d.Buf.Offset, d.DataLen);
					}

					callback(this, data);

					for (int i = 0, n = res.Count; i < n; ++i) {
						TcpConnection.BufferManager.CheckIn(res[i].Buf); // dispose buffers
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
			if (_verbose) {
				Log.Info(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}] Received bytes: {totalBytesReceived}, Sent bytes: {totalBytesSent}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					TotalBytesReceived, TotalBytesSent);
				Log.Info(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}] Send calls: {sendCalls}, callbacks: {sendCallbacks}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					SendCalls, SendCallbacks);
				Log.Info(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}] Receive calls: {receiveCalls}, callbacks: {receiveCallbacks}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					ReceiveCalls, ReceiveCallbacks);
				Log.Info(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}] Close reason: [{socketError}] {reason}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					socketError, reason);
			}

			CloseSocket();
			if (Interlocked.CompareExchange(ref _sending, 1, 0) == 0)
				ReturnSendingSocketArgs();
			var handler = ConnectionClosed;
			if (handler != null)
				handler(this, socketError);
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
				SocketArgsPool.Return(socketArgs);
			}
		}

		private void ReturnReceivingSocketArgs() {
			var socketArgs = Interlocked.Exchange(ref _receiveSocketArgs, null);
			if (socketArgs != null) {
				socketArgs.Completed -= OnReceiveAsyncCompleted;
				socketArgs.AcceptSocket = null;
				if (socketArgs.Buffer != null) {
					BufferManager.CheckIn(
						new ArraySegment<byte>(socketArgs.Buffer, socketArgs.Offset, socketArgs.Count));
					socketArgs.SetBuffer(null, 0, 0);
				}

				SocketArgsPool.Return(socketArgs);
			}
		}

		public void SetClientConnectionName(string clientConnectionName) {
			_clientConnectionName = clientConnectionName;
		}

		public override string ToString() {
			return RemoteEndPoint.ToString();
		}

		private struct ReceivedData {
			public readonly ArraySegment<byte> Buf;
			public readonly int DataLen;

			public ReceivedData(ArraySegment<byte> buf, int dataLen) {
				Buf = buf;
				DataLen = dataLen;
			}
		}
	}
}
