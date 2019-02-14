using System;
using System.Diagnostics;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class WriteProcessor : ICmdProcessor {
		public string Usage {
			get { return "WR [<stream-id> <expected-version> <data> [<metadata> [<is-json> [<login> <pass>]]]"; }
		}

		public string Keyword {
			get { return "WR"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			var eventStreamId = "test-stream";
			var expectedVersion = ExpectedVersion.Any;
			var data = "test-data";
			string metadata = null;
			bool isJson = false;
			string login = null;
			string pass = null;

			if (args.Length > 0) {
				if (args.Length < 3 || args.Length > 7 || args.Length == 6)
					return false;
				eventStreamId = args[0];
				expectedVersion = args[1].ToUpper() == "ANY" ? ExpectedVersion.Any : int.Parse(args[1]);
				data = args[2];
				if (args.Length >= 4)
					metadata = args[3];
				if (args.Length >= 5)
					isJson = bool.Parse(args[4]);
				if (args.Length >= 7) {
					login = args[5];
					pass = args[6];
				}
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
						new[] {
							new TcpClientMessageDto.NewEvent(Guid.NewGuid().ToByteArray(),
								"TakeSomeSpaceEvent",
								isJson ? 1 : 0, 0,
								Helper.UTF8NoBom.GetBytes(data),
								Helper.UTF8NoBom.GetBytes(metadata ?? string.Empty))
						},
						false);
					var package = new TcpPackage(TcpCommand.WriteEvents,
						login == null ? TcpFlags.None : TcpFlags.Authenticated,
						Guid.NewGuid(),
						login,
						pass,
						writeDto.Serialize()).AsByteArray();
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
						context.Log.Info("Successfully written.");
						PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword),
							(int)Math.Round(sw.Elapsed.TotalMilliseconds));
						context.Success();
					} else {
						context.Log.Info("Error while writing: {message} ({e}).", dto.Message, dto.Result);
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
