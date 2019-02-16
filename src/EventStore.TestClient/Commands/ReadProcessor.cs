using System;
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class ReadProcessor : ICmdProcessor {
		public string Usage {
			get { return "RD [<stream-id> [<from-number> [<only-if-master>]]]"; }
		}

		public string Keyword {
			get { return "RD"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			var eventStreamId = "test-stream";
			var fromNumber = 0;
			const bool resolveLinkTos = false;
			var requireMaster = false;

			if (args.Length > 0) {
				if (args.Length > 3)
					return false;
				eventStreamId = args[0];
				if (args.Length >= 2)
					fromNumber = int.Parse(args[1]);
				if (args.Length >= 3)
					requireMaster = bool.Parse(args[2]);
			}

			context.IsAsync();

			var sw = new Stopwatch();
			context.Client.CreateTcpConnection(
				context,
				connectionEstablished: conn => {
					context.Log.Info("[{remoteEndPoint}, L{localEndPoint}]: Reading...", conn.RemoteEndPoint,
						conn.LocalEndPoint);
					var readDto =
						new TcpClientMessageDto.ReadEvent(eventStreamId, fromNumber, resolveLinkTos, requireMaster);
					var package =
						new TcpPackage(TcpCommand.ReadEvent, Guid.NewGuid(), readDto.Serialize()).AsByteArray();
					sw.Start();
					conn.EnqueueSend(package);
				},
				handlePackage: (conn, pkg) => {
					sw.Stop();
					context.Log.Info("Read request took: {elapsed}.", sw.Elapsed);

					if (pkg.Command != TcpCommand.ReadEventCompleted) {
						context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
						return;
					}

					var dto = pkg.Data.Deserialize<TcpClientMessageDto.ReadEventCompleted>();
					context.Log.Info("READ events from <{stream}>:\n\n"
					                 + "\tEventStreamId: {stream}\n"
					                 + "\tEventNumber:   {eventNumber}\n"
					                 + "\tReadResult:    {readResult}\n"
					                 + "\tEventType:     {eventType}\n"
					                 + "\tData:          {data}\n"
					                 + "\tMetadata:      {metadata}\n",
						eventStreamId,
						eventStreamId,
						dto.Event.Event.EventNumber,
						(ReadEventResult)dto.Result,
						dto.Event.Event.EventType,
						Helper.UTF8NoBom.GetString(dto.Event.Event.Data ?? new byte[0]),
						Helper.UTF8NoBom.GetString(dto.Event.Event.Metadata ?? new byte[0]));


					if (dto.Result == TcpClientMessageDto.ReadEventCompleted.ReadEventResult.Success) {
						PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword),
							(int)Math.Round(sw.Elapsed.TotalMilliseconds));
						context.Success();
					} else
						context.Fail();

					conn.Close();
				},
				connectionClosed: (connection, error) => context.Fail(reason: "Connection was closed prematurely."));

			return true;
		}
	}
}
