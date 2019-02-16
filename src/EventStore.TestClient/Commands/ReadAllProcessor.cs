using System;
using System.Diagnostics;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class ReadAllProcessor : ICmdProcessor {
		public string Usage {
			get { return "RDALL [[F|B] [<commit pos> <prepare pos> [<only-if-master>]]]"; }
		}

		public string Keyword {
			get { return "RDALL"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			bool forward = true;
			long commitPos = 0;
			long preparePos = 0;
			bool posOverriden = false;
			bool resolveLinkTos = false;
			bool requireMaster = false;

			if (args.Length > 0) {
				if (args.Length != 1 && args.Length != 3 && args.Length != 4)
					return false;

				if (args[0].ToUpper() == "F")
					forward = true;
				else if (args[0].ToUpper() == "B")
					forward = false;
				else
					return false;

				if (args.Length >= 3) {
					posOverriden = true;
					if (!long.TryParse(args[1], out commitPos) || !long.TryParse(args[2], out preparePos))
						return false;
				}

				if (args.Length >= 4)
					requireMaster = bool.Parse(args[3]);
			}

			if (!posOverriden) {
				commitPos = forward ? 0 : -1;
				preparePos = forward ? 0 : -1;
			}

			context.IsAsync();

			int total = 0;
			var sw = new Stopwatch();
			var tcpCommand = forward ? TcpCommand.ReadAllEventsForward : TcpCommand.ReadAllEventsBackward;

			context.Client.CreateTcpConnection(
				context,
				connectionEstablished: conn => {
					context.Log.Info("[{remoteEndPoint}, L{localEndPoint}]: Reading all {readDirection}...",
						conn.RemoteEndPoint, conn.LocalEndPoint, forward ? "FORWARD" : "BACKWARD");

					var readDto =
						new TcpClientMessageDto.ReadAllEvents(commitPos, preparePos, 10, resolveLinkTos, requireMaster);
					var package = new TcpPackage(tcpCommand, Guid.NewGuid(), readDto.Serialize()).AsByteArray();
					sw.Start();
					conn.EnqueueSend(package);
				},
				handlePackage: (conn, pkg) => {
					if (pkg.Command != tcpCommand) {
						context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
						return;
					}

					var dto = pkg.Data.Deserialize<TcpClientMessageDto.ReadAllEventsCompleted>();
					if (dto.Events.IsEmpty()) {
						sw.Stop();
						context.Log.Info("=== Reading ALL {readDirection} completed in {elapsed}. Total read: {total}",
							forward ? "FORWARD" : "BACKWARD", sw.Elapsed, total);
						context.Success();
						conn.Close();
						return;
					}

					var sb = new StringBuilder();
					for (int i = 0; i < dto.Events.Length; ++i) {
						var evnt = dto.Events[i].Event;
						sb.AppendFormat(
							"\n{0}:\tStreamId: {1},\n\tEventNumber: {2},\n\tData:\n{3},\n\tEventType: {4}\n",
							total,
							evnt.EventStreamId,
							evnt.EventNumber,
							Helper.UTF8NoBom.GetString(evnt.Data),
							evnt.EventType);
						total += 1;
					}

					context.Log.Info("Next {count} events read:\n{events}", dto.Events.Length, sb.ToString());

					var readDto = new TcpClientMessageDto.ReadAllEvents(dto.NextCommitPosition, dto.NextPreparePosition,
						10, resolveLinkTos, requireMaster);
					var package = new TcpPackage(tcpCommand, Guid.NewGuid(), readDto.Serialize()).AsByteArray();
					conn.EnqueueSend(package);
				},
				connectionClosed: (connection, error) => context.Fail(reason: "Connection was closed prematurely."));

			context.WaitForCompletion();
			return true;
		}
	}
}
