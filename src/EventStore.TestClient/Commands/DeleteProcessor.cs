using System;
using System.Diagnostics;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class DeleteProcessor : ICmdProcessor {
		public string Usage {
			get { return "DEL [<stream-id> [<expected-version>]]"; }
		}

		public string Keyword {
			get { return "DEL"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			var eventStreamId = "test-stream";
			var expectedVersion = ExpectedVersion.Any;

			if (args.Length > 0) {
				if (args.Length > 2)
					return false;
				eventStreamId = args[0];
				if (args.Length == 2)
					expectedVersion = args[1].Trim().ToUpper() == "ANY" ? ExpectedVersion.Any : int.Parse(args[1]);
			}

			context.IsAsync();
			var sw = new Stopwatch();
			context.Client.CreateTcpConnection(
				context,
				connectionEstablished: conn => {
					context.Log.Info(
						"[{remoteEndPoint}, L{localEndPoint}]: Trying to delete event stream '{stream}'...",
						conn.RemoteEndPoint, conn.LocalEndPoint, eventStreamId);
					var corrid = Guid.NewGuid();
					var deleteDto = new TcpClientMessageDto.DeleteStream(eventStreamId, expectedVersion, false, true);
					var package = new TcpPackage(TcpCommand.DeleteStream, corrid, deleteDto.Serialize()).AsByteArray();
					sw.Start();
					conn.EnqueueSend(package);
				},
				handlePackage: (conn, pkg) => {
					sw.Stop();
					context.Log.Info("Delete request took: {elapsed}.", sw.Elapsed);

					if (pkg.Command != TcpCommand.DeleteStreamCompleted) {
						context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
						return;
					}

					var dto = pkg.Data.Deserialize<TcpClientMessageDto.DeleteStreamCompleted>();
					if (dto.Result == TcpClientMessageDto.OperationResult.Success) {
						context.Log.Info("DELETED event stream {stream}.", eventStreamId);
						PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword),
							(int)Math.Round(sw.Elapsed.TotalMilliseconds));
						context.Success();
					} else {
						context.Log.Info("DELETION FAILED for event stream {stream}: {message} ({e}).", eventStreamId,
							dto.Message, dto.Result);
						context.Fail();
					}

					conn.Close();
				},
				connectionClosed: (connection, error) => context.Fail(reason: "Connection was closed prematurely."));

			context.WaitForCompletion();
			return true;
		}
	}
}
