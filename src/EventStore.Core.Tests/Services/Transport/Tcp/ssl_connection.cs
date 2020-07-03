using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.Tests.Helpers;
using EventStore.Transport.Tcp;
using NUnit.Framework;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Tests.Services.Transport.Tcp {
	[TestFixture]
	public class ssl_connections {
		private static readonly ILogger Log = Serilog.Log.ForContext<ssl_connections>();
		private IPAddress _ip;
		private int _port;

		[SetUp]
		public void SetUp() {
			_ip = IPAddress.Loopback;
			_port = PortsHelper.GetAvailablePort(_ip);
		}
		
		[Test]
		public void should_connect_to_each_other_and_send_data() {
			var serverEndPoint = new IPEndPoint(_ip, _port);
			X509Certificate cert = GetServerCertificate();

			var sent = new byte[1000];
			new Random().NextBytes(sent);

			var received = new MemoryStream();

			var done = new ManualResetEventSlim();

			var listener = new TcpServerListener(serverEndPoint);
			listener.StartListening((endPoint, socket) => {
				var ssl = TcpConnectionSsl.CreateServerFromSocket(Guid.NewGuid(), endPoint, socket, () => cert,delegate { return (true, null); },
					verbose: true);
				ssl.ConnectionClosed += (x, y) => done.Set();
				if (ssl.IsClosed)
					done.Set();

				Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
				callback = (x, y) => {
					foreach (var arraySegment in y) {
						received.Write(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
						Log.Information("Received: {0} bytes, total: {1}.", arraySegment.Count, received.Length);
					}

					if (received.Length >= sent.Length) {
						Log.Information("Done receiving...");
						done.Set();
					} else {
						Log.Information("Receiving...");
						ssl.ReceiveAsync(callback);
					}
				};
				Log.Information("Receiving...");
				ssl.ReceiveAsync(callback);
			}, "Secure");

			var clientSsl = TcpConnectionSsl.CreateConnectingConnection(
				Guid.NewGuid(),
				serverEndPoint.GetHost(),
				serverEndPoint,
				delegate { return (true, null); },
				null,
				new TcpClientConnector(),
				TcpConnectionManager.ConnectionTimeout,
				conn => {
					Log.Information("Sending bytes...");
					conn.EnqueueSend(new[] { new ArraySegment<byte>(sent) });
				},
				(conn, err) => {
					Log.Error("Connecting failed: {0}.", err);
					done.Set();
				},
				verbose: true);

			Assert.IsTrue(done.Wait(20000), "Took too long to receive completion.");

			Log.Information("Stopping listener...");
			listener.Stop();
			Log.Information("Closing client TLS connection...");
			clientSsl.Close("Normal close.");
			Log.Information("Checking received data...");
			Assert.AreEqual(sent, received.ToArray());
		}

		public static X509Certificate2 GetRootCertificate() {
			using var stream = Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("EventStore.Core.Tests.Services.Transport.Tcp.test_certificates.ca.ca.pem");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			return new X509Certificate2(mem.ToArray());
		}

		public static X509Certificate2 GetServerCertificate() {
			using var stream = Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("EventStore.Core.Tests.Services.Transport.Tcp.test_certificates.node1.node1.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			return new X509Certificate2(mem.ToArray(), "password");
		}

		public static X509Certificate2 GetOtherServerCertificate() {
			using var stream = Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("EventStore.Core.Tests.Services.Transport.Tcp.test_certificates.node2.node2.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			return new X509Certificate2(mem.ToArray(), "password");
		}

		public static X509Certificate2 GetUntrustedCertificate() {
			using var stream = Assembly.GetExecutingAssembly()
				.GetManifestResourceStream("EventStore.Core.Tests.Services.Transport.Tcp.test_certificates.untrusted.untrusted.p12");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);
			return new X509Certificate2(mem.ToArray(), "password");
		}
	}
}
