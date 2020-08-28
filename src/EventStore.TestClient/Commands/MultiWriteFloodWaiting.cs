using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Data;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class MultiWriteFloodWaitingProcessor : ICmdProcessor {
		public string Usage {
			get { return "MWRFLW [<events-count> [<clients> <requests>]]"; }
		}

		public string Keyword {
			get { return "MWRFLW"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			int clientsCnt = 1;
			long requestsCnt = 5000;
			var writeCount = 10;

			if (args.Length > 0) {
				if (args.Length != 1 && args.Length != 3)
					return false;
				try {
					writeCount = int.Parse(args[0]);
					if (args.Length > 1) {
						clientsCnt = int.Parse(args[1]);
						requestsCnt = long.Parse(args[2]);
					}
				} catch {
					return false;
				}
			}

			WriteFlood(context, writeCount, clientsCnt, requestsCnt);
			return true;
		}

		private void WriteFlood(CommandProcessorContext context, int writeCnt, int clientsCnt, long requestsCnt) {
			const string data = "test-data";

			context.IsAsync();

			var clients = new List<TcpTypedConnection<byte[]>>();
			var threads = new List<Thread>();
			var doneEvent = new ManualResetEventSlim(false);
			long succ = 0;
			long fail = 0;
			long all = 0;

			for (int i = 0; i < clientsCnt; i++) {
				var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
				var localDoneEvent = new AutoResetEvent(false);
				var eventStreamId = "es" + Guid.NewGuid();
				var client = context.Client.CreateTcpConnection(
					context,
					(conn, pkg) => {
						if (pkg.Command != TcpCommand.WriteEventsCompleted) {
							context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
							return;
						}

						var dto = pkg.Data.Deserialize<TcpClientMessageDto.WriteEventsCompleted>();
						if (dto.Result == TcpClientMessageDto.OperationResult.Success) {
							if (Interlocked.Increment(ref succ) % 1000 == 0) Console.Write(".");
						} else {
							if (Interlocked.Increment(ref fail) % 1000 == 0) Console.Write("#");
						}

						if (Interlocked.Increment(ref all) == requestsCnt) {
							context.Success();
							doneEvent.Set();
						}

						localDoneEvent.Set();
					},
					connectionClosed: (conn, err) => context.Fail(reason: "Connection was closed prematurely."));
				clients.Add(client);

				threads.Add(new Thread(() => {
					for (int j = 0; j < count; ++j) {
						var writeDto = new TcpClientMessageDto.WriteEvents(
							eventStreamId,
							ExpectedVersion.Any,
							Enumerable.Range(0, writeCnt).Select(x =>
								new TcpClientMessageDto.NewEvent(Guid.NewGuid().ToByteArray(),
									"type",
									0, 0,
									Common.Utils.Helper.UTF8NoBom.GetBytes(data),
									new byte[0])).ToArray(),
							false);
						var package = new TcpPackage(TcpCommand.WriteEvents, Guid.NewGuid(), writeDto.Serialize());
						client.EnqueueSend(package.AsByteArray());
						localDoneEvent.WaitOne();
					}
				}) {IsBackground = true});
			}

			var sw = Stopwatch.StartNew();
			threads.ForEach(thread => thread.Start());
			doneEvent.Wait();
			sw.Stop();
			clients.ForEach(client => client.Close());

			var reqPerSec = (all + 0.0) / sw.ElapsedMilliseconds * 1000;
			context.Log.Information("Completed. Successes: {success}, failures: {failures}", succ, fail);
			context.Log.Information("{requests} requests completed in {elapsed}ms ({rate:0.00} reqs per sec).", all,
				sw.ElapsedMilliseconds, reqPerSec);

			PerfUtils.LogData(Keyword,
				PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
					PerfUtils.Col("requestsCnt", requestsCnt),
					PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
				PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));
			PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
				(int)reqPerSec);
			PerfUtils.LogTeamCityGraphData(
				string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt),
				(int)(100.0 * fail / (fail + succ)));
			PerfUtils.LogTeamCityGraphData(string.Format("{0}-latency-ms", Keyword),
				(int)Math.Round(sw.Elapsed.TotalMilliseconds / requestsCnt));

			if (succ != requestsCnt)
				context.Fail(reason: "There were errors or not all requests completed.");
			else
				context.Success();
		}
	}
}
