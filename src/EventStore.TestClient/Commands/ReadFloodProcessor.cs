using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Transport.Tcp;

namespace EventStore.TestClient.Commands {
	internal class ReadFloodProcessor : ICmdProcessor {
		public string Usage {
			get { return "RDFL [<clients> <requests> [<event-stream>]"; }
		}

		public string Keyword {
			get { return "RDFL"; }
		}

		private RequestMonitor _monitor = new RequestMonitor();

		public bool Execute(CommandProcessorContext context, string[] args) {
			int clientsCnt = 1;
			long requestsCnt = 5000;
			var eventStreamId = "test-stream";
			bool resolveLinkTos = false;
			bool requireMaster = false;
			if (args.Length > 0) {
				if (args.Length != 2 && args.Length != 3 && args.Length != 4)
					return false;

				try {
					clientsCnt = int.Parse(args[0]);
					requestsCnt = long.Parse(args[1]);
					if (args.Length >= 3)
						eventStreamId = args[2];
					if (args.Length >= 4)
						requireMaster = bool.Parse(args[3]);
				} catch {
					return false;
				}
			}

			ReadFlood(context, eventStreamId, clientsCnt, requestsCnt, resolveLinkTos, requireMaster);
			return true;
		}

		private void ReadFlood(CommandProcessorContext context, string eventStreamId, int clientsCnt, long requestsCnt,
			bool resolveLinkTos, bool requireMaster) {
			context.IsAsync();

			var clients = new List<TcpTypedConnection<byte[]>>();
			var threads = new List<Thread>();
			var doneEvent = new ManualResetEventSlim(false);
			var sw2 = new Stopwatch();
			long succ = 0;
			long fail = 0;
			long all = 0;
			for (int i = 0; i < clientsCnt; i++) {
				var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
				long received = 0;
				long sent = 0;
				var client = context.Client.CreateTcpConnection(
					context,
					(conn, pkg) => {
						if (pkg.Command != TcpCommand.ReadEventCompleted) {
							context.Fail(reason: string.Format("Unexpected TCP package: {0}.", pkg.Command));
							return;
						}

						var dto = pkg.Data.Deserialize<TcpClientMessageDto.ReadEventCompleted>();
						_monitor.EndOperation(pkg.CorrelationId);
						if (dto.Result == TcpClientMessageDto.ReadEventCompleted.ReadEventResult.Success) {
							if (Interlocked.Increment(ref succ) % 1000 == 0) Console.Write(".");
						} else {
							if (Interlocked.Increment(ref fail) % 1000 == 0) Console.Write("#");
						}

						Interlocked.Increment(ref received);
						var localAll = Interlocked.Increment(ref all);
						if (localAll % 100000 == 0) {
							var elapsed = sw2.Elapsed;
							sw2.Restart();
							context.Log.Trace("\nDONE TOTAL {reads} READS IN {elapsed} ({rate:0.0}/s).", localAll,
								elapsed, 1000.0 * 100000 / elapsed.TotalMilliseconds);
						}

						if (localAll == requestsCnt) {
							context.Success();
							doneEvent.Set();
						}
					},
					connectionClosed: (conn, err) => context.Fail(reason: "Connection was closed prematurely."));
				clients.Add(client);

				threads.Add(new Thread(() => {
					for (int j = 0; j < count; ++j) {
						var corrId = Guid.NewGuid();
						var read = new TcpClientMessageDto.ReadEvent(eventStreamId, 0, resolveLinkTos, requireMaster);
						var package = new TcpPackage(TcpCommand.ReadEvent, corrId, read.Serialize());
						_monitor.StartOperation(corrId);
						client.EnqueueSend(package.AsByteArray());

						var localSent = Interlocked.Increment(ref sent);
						while (localSent - Interlocked.Read(ref received) >
						       context.Client.Options.ReadWindow / clientsCnt) {
							Thread.Sleep(1);
						}
					}
				}) {IsBackground = true});
			}

			var sw = Stopwatch.StartNew();
			sw2.Start();
			threads.ForEach(thread => thread.Start());
			doneEvent.Wait();
			sw.Stop();
			clients.ForEach(client => client.Close());

			var reqPerSec = (all + 0.0) / sw.ElapsedMilliseconds * 1000;
			context.Log.Info("Completed. READS succ: {success}, fail: {failures}.", Interlocked.Read(ref succ),
				Interlocked.Read(ref fail));
			context.Log.Info("{requests} requests completed in {elapsed}ms ({rate:0.00} reqs per sec).", all,
				sw.ElapsedMilliseconds, reqPerSec);
			_monitor.GetMeasurementDetails();
			PerfUtils.LogData(Keyword,
				PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
					PerfUtils.Col("requestsCnt", requestsCnt),
					PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
				PerfUtils.Row(PerfUtils.Col("readsCnt", all)));
			PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
				(int)reqPerSec);

			if (succ != requestsCnt)
				context.Fail(reason: "There were errors or not all requests completed.");
			else
				context.Success();
		}
	}
}
