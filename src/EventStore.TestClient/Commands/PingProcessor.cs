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

			context.Client.CreateTcpConnection(
				context,
				connectionEstablished: conn => {
					var package = new TcpPackage(TcpCommand.Ping, Guid.NewGuid(), null);
					context.Log.Info("[{ip}:{tcpPort}]: PING...", context.Client.Options.Ip,
						context.Client.Options.TcpPort);
					conn.EnqueueSend(package.AsByteArray());
				},
				handlePackage: (conn, pkg) => {
					if (pkg.Command != TcpCommand.Pong) {
						context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
						return;
					}

					context.Log.Info("[{ip}:{tcpPort}]: PONG!", context.Client.Options.Ip,
						context.Client.Options.TcpPort);
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
