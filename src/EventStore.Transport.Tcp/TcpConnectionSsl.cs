using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Common.Utils;
using ILogger = Serilog.ILogger;

namespace EventStore.Transport.Tcp {
	public class TcpConnectionSsl : TcpConnectionBase, ITcpConnection {
		private static readonly ILogger Log = Serilog.Log.ForContext<TcpConnectionSsl>();

		public static ITcpConnection CreateConnectingConnection(Guid connectionId,
			string targetHost,
			IPEndPoint remoteEndPoint,
			Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> serverCertValidator,
			Func<X509CertificateCollection> clientCertificatesSelector,
			TcpClientConnector connector,
			TimeSpan connectionTimeout,
			Action<ITcpConnection> onConnectionEstablished,
			Action<ITcpConnection, SocketError> onConnectionFailed,
			bool verbose) {
			var connection = new TcpConnectionSsl(connectionId, remoteEndPoint, verbose);
			// ReSharper disable ImplicitlyCapturedClosure
			connector.InitConnect(remoteEndPoint,
				(socket) => {
					connection.InitClientSocket(socket);
				},
				(_, socket) => {
					connection.InitSslStream(targetHost, serverCertValidator, clientCertificatesSelector, verbose);
					if (onConnectionEstablished != null)
						onConnectionEstablished(connection);
				},
				(_, socketError) => {
					if (onConnectionFailed != null)
						onConnectionFailed(connection, socketError);
				}, connection, connectionTimeout);
			// ReSharper restore ImplicitlyCapturedClosure
			return connection;
		}

		public static ITcpConnection CreateServerFromSocket(Guid connectionId,
			IPEndPoint remoteEndPoint,
			Socket socket,
			Func<X509Certificate> serverCertificateSelector,
			Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> clientCertValidator,
			bool verbose) {
			var connection = new TcpConnectionSsl(connectionId, remoteEndPoint, verbose);
			connection.InitServerSocket(socket, serverCertificateSelector, clientCertValidator, verbose);
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
		public string _clientConnectionName;

		private Socket _socket;

		private readonly ConcurrentQueueWrapper<ArraySegment<byte>> _sendQueue =
			new ConcurrentQueueWrapper<ArraySegment<byte>>();

		private readonly ConcurrentQueueWrapper<ReceivedData>
			_receiveQueue = new ConcurrentQueueWrapper<ReceivedData>();

		private readonly MemoryStream _memoryStream = new MemoryStream();

		private readonly object _streamLock = new object();
		private readonly object _closeLock = new object();
		private bool _isSending;
		private int _receiveHandling;
		private volatile bool _isClosed;

		private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _receiveCallback;

		private SslStream _sslStream;
		private bool _isAuthenticated;
		private int _sendingBytes;
		private Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _serverCertValidator;
		private Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> _clientCertValidator;
		private readonly byte[] _receiveBuffer = new byte[TcpConnection.BufferManager.ChunkSize];

		private TcpConnectionSsl(Guid connectionId, IPEndPoint remoteEndPoint, bool verbose) : base(remoteEndPoint) {
			Ensure.NotEmptyGuid(connectionId, "connectionId");

			_connectionId = connectionId;
			_verbose = verbose;
		}

		private void InitServerSocket(Socket socket, Func<X509Certificate> serverCertificateSelector, Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> clientCertValidator, bool verbose) {
			InitConnectionBase(socket);
			if (verbose)
				Console.WriteLine("TcpConnectionSsl::InitClientSocket({0}, L{1})", RemoteEndPoint, LocalEndPoint);

			_clientCertValidator = clientCertValidator;

			lock (_streamLock) {
				try {
					socket.NoDelay = true;
				} catch (ObjectDisposedException) {
					CloseInternal(SocketError.Shutdown, "Socket is disposed.");
					return;
				} catch (SocketException) {
					CloseInternal(SocketError.Shutdown, "Socket is disposed.");
					return;
				}

				try {
					_sslStream = new SslStream(new NetworkStream(socket, true), false, ValidateClientCertificate, null);
				} catch (IOException exc) {
					Log.Debug(exc, "[S{remoteEndPoint}, L{localEndPoint}]: IOException on NetworkStream. The socket has already been disposed.", RemoteEndPoint,
						LocalEndPoint);
					return;
				}

				try {
					var enabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
					var certificate = serverCertificateSelector?.Invoke();
					Ensure.NotNull(certificate, "certificate");
					_sslStream.BeginAuthenticateAsServer(certificate, true, enabledSslProtocols, false,
						OnEndAuthenticateAsServer, _sslStream);
				} catch (AuthenticationException exc) {
					Log.Information(exc,
						"[S{remoteEndPoint}, L{localEndPoint}]: Authentication exception on BeginAuthenticateAsServer.",
						RemoteEndPoint, LocalEndPoint);
					CloseInternal(SocketError.SocketError, exc.Message);
				} catch (ObjectDisposedException) {
					CloseInternal(SocketError.SocketError, "SslStream disposed.");
				} catch (Exception exc) {
					Log.Information(exc,
						"[S{remoteEndPoint}, L{localEndPoint}]: Exception on BeginAuthenticateAsServer.",
						RemoteEndPoint, LocalEndPoint);
					CloseInternal(SocketError.SocketError, exc.Message);
				}
			}
		}

		private void OnEndAuthenticateAsServer(IAsyncResult ar) {
			try {
				lock (_streamLock) {
					var sslStream = (SslStream)ar.AsyncState;
					sslStream.EndAuthenticateAsServer(ar);
					if (_verbose)
						DisplaySslStreamInfo(sslStream);
					_isAuthenticated = true;
				}

				StartReceive();
				TrySend();
			} catch (AuthenticationException exc) {
				Log.Information(exc,
					"[S{remoteEndPoint}, L{localEndPoint}]: Authentication exception on EndAuthenticateAsServer.",
					RemoteEndPoint, LocalEndPoint);
				CloseInternal(SocketError.SocketError, exc.Message);
			} catch (ObjectDisposedException) {
				CloseInternal(SocketError.SocketError, "SslStream disposed.");
			} catch (Exception exc) {
				Log.Information(exc, "[S{remoteEndPoint}, L{localEndPoint}]: Exception on EndAuthenticateAsServer.",
					RemoteEndPoint, LocalEndPoint);
				CloseInternal(SocketError.SocketError, exc.Message);
			}
		}

		private void InitClientSocket(Socket socket) {
			_socket = socket;
		}

		private void InitSslStream(string targetHost, Func<X509Certificate, X509Chain, SslPolicyErrors, ValueTuple<bool, string>> serverCertValidator, Func<X509CertificateCollection> clientCertificatesSelector, bool verbose) {
			Ensure.NotNull(targetHost, "targetHost");
			InitConnectionBase(_socket);
			if (verbose)
				Console.WriteLine("TcpConnectionSsl::InitClientSslStream({0}, L{1})", RemoteEndPoint, LocalEndPoint);

			_serverCertValidator = serverCertValidator;

			lock (_streamLock) {
				try {
					_socket.NoDelay = true;
				} catch (ObjectDisposedException) {
					CloseInternal(SocketError.Shutdown, "Socket is disposed.");
					return;
				} catch (SocketException) {
					CloseInternal(SocketError.Shutdown, "Socket is disposed.");
					return;
				}

				try {
					_sslStream = new SslStream(new NetworkStream(_socket, true), false, ValidateServerCertificate, null);
				} catch (IOException exc) {
					Log.Debug(exc, "[S{remoteEndPoint}, L{localEndPoint}]: IOException on NetworkStream. The socket has already been disposed.", RemoteEndPoint,
						LocalEndPoint);
					return;
				}

				try {
					var enabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
					var clientCertificates = clientCertificatesSelector?.Invoke();
					_sslStream.BeginAuthenticateAsClient(targetHost, clientCertificates, enabledSslProtocols, false, OnEndAuthenticateAsClient, _sslStream);
				} catch (AuthenticationException exc) {
					Log.Information(exc,
						"[S{remoteEndPoint}, L{localEndPoint}]: Authentication exception on BeginAuthenticateAsClient.",
						RemoteEndPoint, LocalEndPoint);
					CloseInternal(SocketError.SocketError, exc.Message);
				} catch (ObjectDisposedException) {
					CloseInternal(SocketError.SocketError, "SslStream disposed.");
				} catch (Exception exc) {
					Log.Information(exc,
						"[S{remoteEndPoint}, {localEndPoint}]: Exception on BeginAuthenticateAsClient.", RemoteEndPoint,
						LocalEndPoint);
					CloseInternal(SocketError.SocketError, exc.Message);
				}
			}
		}

		private void OnEndAuthenticateAsClient(IAsyncResult ar) {
			try {
				lock (_streamLock) {
					var sslStream = (SslStream)ar.AsyncState;
					sslStream.EndAuthenticateAsClient(ar);
					if (_verbose)
						DisplaySslStreamInfo(sslStream);
					_isAuthenticated = true;
				}

				StartReceive();
				TrySend();
			} catch (AuthenticationException exc) {
				Log.Information(exc,
					"[S{remoteEndPoint}, L{localEndPoint}]: Authentication exception on EndAuthenticateAsClient.",
					RemoteEndPoint, LocalEndPoint);
				CloseInternal(SocketError.SocketError, exc.Message);
			} catch (ObjectDisposedException) {
				CloseInternal(SocketError.SocketError, "SslStream disposed.");
			} catch (Exception exc) {
				Log.Information(exc, "[S{remoteEndPoint}, L{localEndPoint}]: Exception on EndAuthenticateAsClient.",
					RemoteEndPoint, LocalEndPoint);
				CloseInternal(SocketError.SocketError, exc.Message);
			}
		}

		// The following method is invoked by the RemoteCertificateValidationDelegate. 
		public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain,
			SslPolicyErrors sslPolicyErrors) {
			var (isValid, error) = _serverCertValidator(certificate, chain, sslPolicyErrors);
			if (!isValid && error != null) {
				Log.Error("Server certificate validation error: {e}", error);
			}
			return isValid;
		}

		public bool ValidateClientCertificate(object sender, X509Certificate certificate, X509Chain chain,
			SslPolicyErrors sslPolicyErrors) {
			var (isValid, error) = _clientCertValidator(certificate, chain, sslPolicyErrors);
			if (!isValid && error != null) {
				Log.Error("Client certificate validation error: {e}", error);
			}
			return isValid;
		}

		private void DisplaySslStreamInfo(SslStream stream) {
			Log.Information("[S{remoteEndPoint}, L{localEndPoint}]", RemoteEndPoint, LocalEndPoint);
			Log.Information("Cipher: {cipherAlgorithm}", stream.CipherAlgorithm);
			try {
				Log.Information("Cipher strength: {cipherStrength}", stream.CipherStrength);
			} catch (NotImplementedException) {
			}

			Log.Information("Hash: {hashAlgorithm}", stream.HashAlgorithm);
			try {
				Log.Information("Hash strength: {hashStrength}", stream.HashStrength);
			} catch (NotImplementedException) {
			}

			Log.Information("Key exchange: {keyExchangeAlgorithm}", stream.KeyExchangeAlgorithm);
			try {
				Log.Information("Key exchange strength: {keyExchangeStrength}", stream.KeyExchangeStrength);
			} catch (NotImplementedException) {
			}

			Log.Information("Protocol: {sslProtocol}", stream.SslProtocol);
			Log.Information("Is authenticated: {isAuthenticated} as server? {isServer}", stream.IsAuthenticated,
				stream.IsServer);
			Log.Information("IsSigned: {isSigned}", stream.IsSigned);
			Log.Information("Is Encrypted: {isEncrypted}", stream.IsEncrypted);
			Log.Information("Can read: {canRead}, write {canWrite}", stream.CanRead, stream.CanWrite);
			Log.Information("Can timeout: {canTimeout}", stream.CanTimeout);
			try {
				Log.Information("Certificate revocation list checked: {checkCertRevocationStatus}",
					stream.CheckCertRevocationStatus);
			} catch (NotImplementedException) {
			}

			X509Certificate localCert = stream.LocalCertificate;
			if (localCert != null)
				Log.Information(
					"Local certificate was issued to {subject} and is valid from {effectiveDate} until {expirationDate}.",
					localCert.Subject, localCert.GetEffectiveDateString(), localCert.GetExpirationDateString());
			else
				Log.Information("Local certificate is null.");

			// Display the properties of the client's certificate.
			X509Certificate remoteCert = stream.RemoteCertificate;
			if (remoteCert != null)
				Log.Information(
					"Remote certificate was issued to {subject} and is valid from {remoteCertEffectiveDate} until {remoteCertExpirationDate}.",
					remoteCert.Subject, remoteCert.GetEffectiveDateString(), remoteCert.GetExpirationDateString());
			else
				Log.Information("Remote certificate is null.");
		}

		public void EnqueueSend(IEnumerable<ArraySegment<byte>> data) {
			lock (_streamLock) {
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
			lock (_streamLock) {
				if (_isSending || _sendQueue.IsEmpty || _sslStream == null || !_isAuthenticated) return;
				if (TcpConnectionMonitor.Default.IsSendBlocked()) return;
				_isSending = true;
			}

			_memoryStream.SetLength(0);

			ArraySegment<byte> sendPiece;
			while (_sendQueue.TryDequeue(out sendPiece)) {
				_memoryStream.Write(sendPiece.Array, sendPiece.Offset, sendPiece.Count);
				if (_memoryStream.Length >= TcpConnection.MaxSendPacketSize)
					break;
			}

			_sendingBytes = (int)_memoryStream.Length;

			try {
				NotifySendStarting(_sendingBytes);
				_sslStream.BeginWrite(_memoryStream.GetBuffer(), 0, _sendingBytes, OnEndWrite, null);
			} catch (SocketException exc) {
				Log.Debug(exc, "SocketException '{e}' during BeginWrite.", exc.SocketErrorCode);
				CloseInternal(exc.SocketErrorCode, "SocketException during BeginWrite.");
			} catch (ObjectDisposedException) {
				CloseInternal(SocketError.SocketError, "SslStream disposed.");
			} catch (Exception exc) {
				Log.Debug(exc, "Exception during BeginWrite.");
				CloseInternal(SocketError.SocketError, "Exception during BeginWrite");
			}
		}

		private void OnEndWrite(IAsyncResult ar) {
			try {
				_sslStream.EndWrite(ar);
				NotifySendCompleted(_sendingBytes);

				lock (_streamLock) {
					_isSending = false;
				}

				TrySend();
			} catch (SocketException exc) {
				Log.Debug(exc, "SocketException '{e}' during EndWrite.", exc.SocketErrorCode);
				NotifySendCompleted(0);
				CloseInternal(exc.SocketErrorCode, "SocketException during EndWrite.");
			} catch (ObjectDisposedException) {
				NotifySendCompleted(0);
				CloseInternal(SocketError.SocketError, "SslStream disposed.");
			} catch (Exception exc) {
				Log.Debug(exc, "Exception during EndWrite.");
				NotifySendCompleted(0);
				CloseInternal(SocketError.SocketError, "Exception during EndWrite.");
			}
		}

		public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback) {
			Ensure.NotNull(callback, "callback");

			if (Interlocked.Exchange(ref _receiveCallback, callback) != null) {
				Log.Fatal("ReceiveAsync called again while previous call wasn't fulfilled");
				throw new InvalidOperationException("ReceiveAsync called again while previous call wasn't fulfilled");
			}

			TryDequeueReceivedData();
		}

		private void StartReceive() {
			try {
				NotifyReceiveStarting();
				_sslStream.BeginRead(_receiveBuffer, 0, _receiveBuffer.Length, OnEndRead, null);
			} catch (SocketException exc) {
				Log.Debug(exc, "SocketException '{e}' during BeginRead.", exc.SocketErrorCode);
				CloseInternal(exc.SocketErrorCode, "SocketException during BeginRead.");
			} catch (ObjectDisposedException) {
				CloseInternal(SocketError.SocketError, "SslStream disposed.");
			} catch (Exception exc) {
				Log.Debug(exc, "Exception during BeginRead.");
				CloseInternal(SocketError.SocketError, "Exception during BeginRead.");
			}
		}

		private void OnEndRead(IAsyncResult ar) {
			int bytesRead;
			try {
				bytesRead = _sslStream.EndRead(ar);
			} catch (SocketException exc) {
				Log.Debug(exc, "SocketException '{e}' during EndRead.", exc.SocketErrorCode);
				NotifyReceiveCompleted(0);
				CloseInternal(exc.SocketErrorCode, "SocketException during EndRead.");
				return;
			} catch (ObjectDisposedException) {
				NotifyReceiveCompleted(0);
				CloseInternal(SocketError.SocketError, "SslStream disposed.");
				return;
			} catch (Exception exc) {
				Log.Debug(exc, "Exception during EndRead.");
				NotifyReceiveCompleted(0);
				CloseInternal(SocketError.SocketError, "Exception during EndRead.");
				return;
			}

			if (bytesRead <= 0) // socket closed normally
			{
				NotifyReceiveCompleted(0);
				CloseInternal(SocketError.Success, "Socket closed.");
				return;
			}

			NotifyReceiveCompleted(bytesRead);

			var buffer = TcpConnection.BufferManager.CheckOut();
			if (buffer.Array == null || buffer.Count == 0 || buffer.Array.Length < buffer.Offset + buffer.Count)
				throw new Exception("Invalid buffer allocated.");
			Buffer.BlockCopy(_receiveBuffer, 0, buffer.Array, buffer.Offset, bytesRead);
			var buf = new ArraySegment<byte>(buffer.Array, buffer.Offset, buffer.Count);
			_receiveQueue.Enqueue(new ReceivedData(buf, bytesRead));

			StartReceive();
			TryDequeueReceivedData();
		}

		private void TryDequeueReceivedData() {
			if (Interlocked.CompareExchange(ref _receiveHandling, 1, 0) != 0)
				return;
			do {
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

					lock (_closeLock) {
						if(!_isClosed)
							callback(this, data);
					}

					for (int i = 0, n = res.Count; i < n; ++i) {
						TcpConnection.BufferManager.CheckIn(res[i].Buf); // dispose buffers
					}

					NotifyReceiveDispatched(bytes);
				}

				Interlocked.Exchange(ref _receiveHandling, 0);
			} while (!_receiveQueue.IsEmpty
			         && _receiveCallback != null
			         && Interlocked.CompareExchange(ref _receiveHandling, 1, 0) == 0);
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

			if (_verbose) {
				Log.Information(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}]:Received bytes: {totalBytesReceived}, Sent bytes: {totalBytesSent}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					TotalBytesReceived, TotalBytesSent);
				Log.Information(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}]:Send calls: {sendCalls}, callbacks: {sendCallbacks}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					SendCalls, SendCallbacks);
				Log.Information(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}]:Receive calls: {receiveCalls}, callbacks: {receiveCallbacks}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					ReceiveCalls, ReceiveCallbacks);
				Log.Information(
					"ES {connectionType} closed [{dateTime:HH:mm:ss.fff}: N{remoteEndPoint}, L{localEndPoint}, {connectionId:B}]:Close reason: [{e}] {reason}",
					GetType().Name, DateTime.UtcNow, RemoteEndPoint, LocalEndPoint, _connectionId,
					socketError, reason);
			}

			if (_sslStream != null)
				Helper.EatException(() => _sslStream.Close());

			if (_socket != null)
				Helper.EatException(() => _socket.Dispose());

			var handler = ConnectionClosed;
			if (handler != null)
				handler(this, socketError);
		}

		public void SetClientConnectionName(string clientConnectionName) {
			_clientConnectionName = clientConnectionName;
		}

		public override string ToString() {
			return "S" + RemoteEndPoint;
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
