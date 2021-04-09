using System;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class PingProcessor : ICmdProcessor {
		public string Usage {
			get { return Keyword; }
		}

		public string Keyword {
			get { return "PING"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			context.IsAsync();

			context._tcpTestClient.CreateTcpConnection(
				context,
				connectionEstablished: conn => {
					var package = new TcpPackage(TcpCommand.Ping, Guid.NewGuid(), null);
					context.Log.Information("[{ip}:{tcpPort}]: PING...", context._tcpTestClient.Options.Host,
						context._tcpTestClient.Options.TcpPort);
					conn.EnqueueSend(package.AsByteArray());
				},
				handlePackage: (conn, pkg) => {
					if (pkg.Command != TcpCommand.Pong) {
						context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
						return;
					}

					context.Log.Information("[{ip}:{tcpPort}]: PONG!", context._tcpTestClient.Options.Host,
						context._tcpTestClient.Options.TcpPort);
					context.Success();
					conn.Close();
				},
				connectionClosed: (typedConnection, error) =>
					context.Fail(reason: "Connection was closed prematurely."));
			context.WaitForCompletion();
			return true;
		}
	}
}
