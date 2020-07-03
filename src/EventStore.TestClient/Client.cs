using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using EventStore.BufferManagement;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.TestClient.Commands;
using EventStore.TestClient.Commands.DvuBasic;
using EventStore.Transport.Tcp;
using EventStore.Transport.Tcp.Formatting;
using EventStore.Transport.Tcp.Framing;
using Connection = EventStore.Transport.Tcp.TcpTypedConnection<byte[]>;
using ILogger = Serilog.ILogger;

namespace EventStore.TestClient {
	public class Client {
		private static readonly ILogger Log = Serilog.Log.ForContext<Client>();

		public readonly bool InteractiveMode;

		public readonly ClientOptions Options;
		public readonly EndPoint TcpEndpoint;
		public readonly EndPoint HttpEndpoint;
		public readonly bool UseSsl;
		public readonly bool ValidateServer;

		private readonly BufferManager _bufferManager =
			new BufferManager(TcpConfiguration.BufferChunksCount, TcpConfiguration.SocketBufferSize);

		private readonly TcpClientConnector _connector = new TcpClientConnector();

		private readonly CommandsProcessor _commands = new CommandsProcessor(Log);

		public Client(ClientOptions options) {
			Options = options;

			TcpEndpoint = new DnsEndPoint(options.Host, options.TcpPort);
			HttpEndpoint = new DnsEndPoint(options.Host, options.HttpPort);

			UseSsl = options.UseTls;
			ValidateServer = options.TlsValidateServer;

			InteractiveMode = options.Command.IsEmpty();

			RegisterProcessors();

		}

		private void RegisterProcessors() {
			_commands.Register(new UsageProcessor(_commands), usageProcessor: true);
			_commands.Register(new ExitProcessor());

			_commands.Register(new PingProcessor());
			_commands.Register(new PingFloodProcessor());
			_commands.Register(new PingFloodWaitingProcessor());

			_commands.Register(new WriteProcessor());
			_commands.Register(new WriteJsonProcessor());
			_commands.Register(new WriteFloodProcessor());
			_commands.Register(new WriteFloodClientApiProcessor());
			_commands.Register(new WriteFloodWaitingProcessor());

			_commands.Register(new MultiWriteProcessor());
			_commands.Register(new MultiWriteFloodWaitingProcessor());

			_commands.Register(new TransactionWriteProcessor());

			_commands.Register(new DeleteProcessor());

			_commands.Register(new ReadAllProcessor());
			_commands.Register(new ReadProcessor());
			_commands.Register(new ReadFloodProcessor());

			_commands.Register(new WriteLongTermProcessor());

			_commands.Register(new DvuBasicProcessor());
			_commands.Register(new RunTestScenariosProcessor());

			_commands.Register(new SubscribeToStreamProcessor());

			_commands.Register(new ScavengeProcessor());

			_commands.Register(new TcpSanitazationCheckProcessor());

			_commands.Register(new SubscriptionStressTestProcessor());
		}

		public int Run() {
			if (!InteractiveMode) {
				var args = ParseCommandLine(Options.Command[0]);
				return Execute(args);
			}

			new Thread(() => {
				Thread.Sleep(100);
				Console.Write(">>> ");

				string line;
				while ((line = Console.ReadLine()) != null) {
					try {
						if (string.IsNullOrWhiteSpace(line))
							continue;

						try {
							var args = ParseCommandLine(line);
							Execute(args);
						} catch (Exception exc) {
							Log.Error(exc, "Error during executing command.");
						}
					} finally {
						Thread.Sleep(100);
						Console.Write(">>> ");
					}
				}
			}) {IsBackground = true, Name = "Client Main Loop Thread"}.Start();
			return 0;
		}

		private static string[] ParseCommandLine(string line) {
			return line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
		}

		private int Execute(string[] args) {
			Log.Information("Processing command: {command}.", string.Join(" ", args));

			var context = new CommandProcessorContext(this, Log, new ManualResetEventSlim(true));

			int exitCode;
			if (_commands.TryProcess(context, args, out exitCode)) {
				Log.Information("Command exited with code {exitCode}.", exitCode);
				return exitCode;
			}

			return exitCode;
		}

		public Connection CreateTcpConnection(CommandProcessorContext context,
			Action<Connection, TcpPackage> handlePackage,
			Action<Connection> connectionEstablished = null,
			Action<Connection, SocketError> connectionClosed = null,
			bool failContextOnError = true,
			IPEndPoint tcpEndPoint = null) {
			var connectionCreatedEvent = new ManualResetEventSlim(false);
			Connection typedConnection = null;

			Action<ITcpConnection> onConnectionEstablished = conn => {
				// we execute callback on ThreadPool because on FreeBSD it can be called synchronously
				// causing deadlock
				ThreadPool.QueueUserWorkItem(_ => {
					if (!InteractiveMode)
						Log.Information(
							"TcpTypedConnection: connected to [{remoteEndPoint}, L{localEndPoint}, {connectionId:B}].",
							conn.RemoteEndPoint, conn.LocalEndPoint, conn.ConnectionId);
					if (connectionEstablished != null) {
						if (!connectionCreatedEvent.Wait(10000))
							throw new Exception("TcpTypedConnection: creation took too long!");
						connectionEstablished(typedConnection);
					}
				});
			};
			Action<ITcpConnection, SocketError> onConnectionFailed = (conn, error) => {
				Log.Error(
					"TcpTypedConnection: connection to [{remoteEndPoint}, L{localEndPoint}, {connectionId:B}] failed. Error: {e}.",
					conn.RemoteEndPoint, conn.LocalEndPoint, conn.ConnectionId, error);

				if (connectionClosed != null)
					connectionClosed(null, error);

				if (failContextOnError)
					context.Fail(reason: string.Format("Socket connection failed with error {0}.", error));
			};

			var endpoint = tcpEndPoint ?? TcpEndpoint;
			ITcpConnection connection;
			if (UseSsl) {
				connection = _connector.ConnectSslTo(
					Guid.NewGuid(),
					endpoint.GetHost(),
					endpoint.ResolveDnsToIPAddress(),
					TcpConnectionManager.ConnectionTimeout,
					(cert,chain,err) => (err == SslPolicyErrors.None || !ValidateServer, err.ToString()),
					() => null,
					onConnectionEstablished,
					onConnectionFailed,
					verbose: !InteractiveMode);
			} else {
				connection = _connector.ConnectTo(
					Guid.NewGuid(),
					endpoint.ResolveDnsToIPAddress(),
					TcpConnectionManager.ConnectionTimeout,
					onConnectionEstablished,
					onConnectionFailed,
					verbose: !InteractiveMode);
			}

			typedConnection = new Connection(connection, new RawMessageFormatter(_bufferManager),
				new LengthPrefixMessageFramer());
			typedConnection.ConnectionClosed +=
				(conn, error) => {
					if (!InteractiveMode || error != SocketError.Success) {
						Log.Information(
							"TcpTypedConnection: connection [{remoteEndPoint}, L{localEndPoint}] was closed {status}",
							conn.RemoteEndPoint, conn.LocalEndPoint,
							error == SocketError.Success ? "cleanly." : "with error: " + error + ".");
					}

					if (connectionClosed != null)
						connectionClosed(conn, error);
					else
						Log.Information("connectionClosed callback was null");
				};
			connectionCreatedEvent.Set();

			typedConnection.ReceiveAsync(
				(conn, pkg) => {
					var package = new TcpPackage();
					bool validPackage = false;
					try {
						package = TcpPackage.FromArraySegment(new ArraySegment<byte>(pkg));
						validPackage = true;

						if (package.Command == TcpCommand.HeartbeatRequestCommand) {
							var resp = new TcpPackage(TcpCommand.HeartbeatResponseCommand, Guid.NewGuid(), null);
							conn.EnqueueSend(resp.AsByteArray());
							return;
						}

						handlePackage(conn, package);
					} catch (Exception ex) {
						Log.Information(ex,
							"TcpTypedConnection: [{remoteEndPoint}, L{localEndPoint}] ERROR for {package}. Connection will be closed.",
							conn.RemoteEndPoint, conn.LocalEndPoint,
							validPackage ? package.Command as object : "<invalid package>");
						conn.Close(ex.Message);

						if (failContextOnError)
							context.Fail(ex);
					}
				});

			return typedConnection;
		}
	}
}
