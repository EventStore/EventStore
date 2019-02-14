using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class MultiWriteProcessor : ICmdProcessor {
		public string Usage {
			get { return "MWR [<write-count=10> [<stream=test-stream> [<expected-version=ANY>]]"; }
		}

		public string Keyword {
			get { return "MWR"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			const string data = "test-data";
			var eventStreamId = "test-stream";
			var writeCount = 10;
			var expectedVersion = ExpectedVersion.Any;

			if (args.Length > 0) {
				if (args.Length > 3)
					return false;
				writeCount = int.Parse(args[0]);
				if (args.Length >= 2)
					eventStreamId = args[1];
				if (args.Length >= 3)
					expectedVersion = args[2].Trim().ToUpper() == "ANY"
						? ExpectedVersion.Any
						: int.Parse(args[2].Trim());
			}

			context.IsAsync();
			var sw = new Stopwatch();
			context.Client.CreateTcpConnection(
				context,
				connectionEstablished: conn => {
					context.Log.Info("[{remoteEndPoint}, L{localEndPoint}]: Writing...", conn.RemoteEndPoint,
						conn.LocalEndPoint);
					var writeDto = new TcpClientMessageDto.WriteEvents(
						eventStreamId,
						expectedVersion,
						Enumerable.Range(0, writeCount).Select(x => new TcpClientMessageDto.NewEvent(
							Guid.NewGuid().ToByteArray(),
							"type",
							0, 0,
							Helper.UTF8NoBom.GetBytes(data),
							new byte[0])).ToArray(),
						false);
					var package = new TcpPackage(TcpCommand.WriteEvents, Guid.NewGuid(), writeDto.Serialize())
						.AsByteArray();
					sw.Start();
					conn.EnqueueSend(package);
				},
				handlePackage: (conn, pkg) => {
					sw.Stop();
					context.Log.Info("Write request took: {elapsed}.", sw.Elapsed);

					if (pkg.Command != TcpCommand.WriteEventsCompleted) {
						context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
						return;
					}

					var dto = pkg.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
					if (dto.Result == TcpClientMessageDto.OperationResult.Success) {
						context.Log.Info("Successfully written {writeCount} events.", writeCount);
						PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword),
							(int)Math.Round(sw.Elapsed.TotalMilliseconds));
						context.Success();
					} else {
						context.Log.Info("Error while writing: {e}.", dto.Result);
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
