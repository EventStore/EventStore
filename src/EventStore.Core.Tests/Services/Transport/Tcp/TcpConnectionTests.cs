using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Transport.Tcp;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Services.Transport.Tcp {
	[TestFixture]
	public class TcpConnectionTests {
		protected static Socket CreateListeningSocket() {
			var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
			listener.Listen(1);
			return listener;
		}

		private IEnumerable<ArraySegment<byte>> GenerateData() {
			var data = new List<ArraySegment<byte>>();
			data.Add(new ArraySegment<byte>(new byte[100]));
			return data;
		}

		[Test, Timeout(120000)]
		public async Task no_data_should_be_dispatched_after_tcp_connection_closed() {
			for (int i = 0; i < 1000; i++) {
				bool closed = false;
				bool dataReceivedAfterClose = false;
				var listeningSocket = CreateListeningSocket();

				var mre = new ManualResetEventSlim(false);
				var clientTcpConnection = TcpConnection.CreateConnectingTcpConnection(
					Guid.NewGuid(),
					(IPEndPoint)listeningSocket.LocalEndPoint,
					new TcpClientConnector(),
					TimeSpan.FromSeconds(5),
					(conn) => mre.Set(),
					(conn, error) => {
						Assert.Fail($"Connection failed: {error}");
					},
					false);

				var serverSocket = listeningSocket.Accept();
				var serverTcpConnection = TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(),
					(IPEndPoint)serverSocket.RemoteEndPoint, serverSocket, false);

				mre.Wait(TimeSpan.FromSeconds(3));
				try {
					clientTcpConnection.ConnectionClosed += (connection, error) => {
						Volatile.Write(ref closed, true);
					};

					clientTcpConnection.ReceiveAsync((connection, data) => {
						if (Volatile.Read(ref closed)) {
							dataReceivedAfterClose = true;
						}
					});

					using (var b = new Barrier(2)) {
						Task sendData = Task.Factory.StartNew(() => {
							b.SignalAndWait();
							for (int j = 0; j < 1000; j++)
								serverTcpConnection.EnqueueSend(GenerateData());
						}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

						Task closeConnection = Task.Factory.StartNew(() => {
							b.SignalAndWait();
							serverTcpConnection.Close("Intentional close");
						}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

						await Task.WhenAll(sendData, closeConnection);
						Assert.False(dataReceivedAfterClose);
					}
				} finally {
					clientTcpConnection.Close("Shut down");
					serverTcpConnection.Close("Shut down");
					listeningSocket.Dispose();
				}
			}
		}

		[Test, Timeout(120000)]
		public void when_connection_closed_quickly_socket_should_be_properly_disposed() {
			for (int i = 0; i < 1000; i++) {
				var listeningSocket = CreateListeningSocket();
				ITcpConnection clientTcpConnection = null;
				ITcpConnection serverTcpConnection = null;
				Socket serverSocket = null;
				try {
					ManualResetEventSlim mre = new ManualResetEventSlim(false);

					clientTcpConnection = TcpConnection.CreateConnectingTcpConnection(
					Guid.NewGuid(),
					(IPEndPoint)listeningSocket.LocalEndPoint,
					new TcpClientConnector(),
					TimeSpan.FromSeconds(5),
					(conn) => {},
					(conn, error) => {},
					false);

					clientTcpConnection.ConnectionClosed += (conn, error) => {
						mre.Set();
					};

					serverSocket = listeningSocket.Accept();
					clientTcpConnection.Close("Intentional close");
					serverTcpConnection = TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(),
						(IPEndPoint)serverSocket.RemoteEndPoint, serverSocket, false);

					mre.Wait(TimeSpan.FromSeconds(10));
					SpinWait.SpinUntil(() => serverTcpConnection.IsClosed, TimeSpan.FromSeconds(10));

					bool disposed;
					try {
						disposed = serverSocket.Poll(1000, SelectMode.SelectRead) && (serverSocket.Available == 0);
					} catch (ObjectDisposedException) {
						disposed = true;
					} catch (SocketException) {
						disposed = true;
					}

					Assert.AreEqual(true, disposed);
				} finally {
					clientTcpConnection?.Close("Shut down");
					serverTcpConnection?.Close("Shut down");
					listeningSocket.Dispose();
					serverSocket?.Dispose();
				}
			}
		}
	}
}
