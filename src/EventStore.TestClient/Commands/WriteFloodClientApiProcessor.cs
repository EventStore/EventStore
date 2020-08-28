using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.Common.Utils;
using ExpectedVersion = EventStore.Core.TransactionLog.Data.ExpectedVersion;

namespace EventStore.TestClient.Commands {
	internal class WriteFloodClientApiProcessor : ICmdProcessor {
		public string Usage {
			get { return "WRFLCA [<clients> <requests> [<streams-cnt> [<size>]]]"; }
		}

		public string Keyword {
			get { return "WRFLCA"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			int clientsCnt = 1;
			long requestsCnt = 5000;
			int streamsCnt = 1000;
			int size = 256;
			if (args.Length > 0) {
				if (args.Length < 2 || args.Length > 4)
					return false;

				try {
					clientsCnt = int.Parse(args[0]);
					requestsCnt = long.Parse(args[1]);
					if (args.Length >= 3)
						streamsCnt = int.Parse(args[2]);
					if (args.Length >= 4)
						size = int.Parse(args[3]);
				} catch {
					return false;
				}
			}

			WriteFlood(context, clientsCnt, requestsCnt, streamsCnt, size);
			return true;
		}

		private void WriteFlood(CommandProcessorContext context, int clientsCnt, long requestsCnt, int streamsCnt,
			int size) {
			context.IsAsync();

			var doneEvent = new ManualResetEventSlim(false);
			var clients = new List<IEventStoreConnection>();
			var threads = new List<Thread>();

			long succ = 0;

			var streams = Enumerable.Range(0, streamsCnt).Select(x => Guid.NewGuid().ToString()).ToArray();
			var sw2 = new Stopwatch();
			for (int i = 0; i < clientsCnt; i++) {
				var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
				var rnd = new Random();

				var settings = ConnectionSettings.Create()
					.UseConsoleLogger()
					.PerformOnAnyNode()
					.LimitReconnectionsTo(10)
					.LimitRetriesForOperationTo(10)
					.LimitOperationsQueueTo(10000)
					.LimitConcurrentOperationsTo(context.Client.Options.WriteWindow / clientsCnt)
					.FailOnNoServerResponse();

				var client = EventStoreConnection.Create(settings,
					new Uri($"tcp://{context.Client.TcpEndpoint.GetHost()}:{context.Client.TcpEndpoint.GetPort()}"));
				clients.Add(client);

				threads.Add(new Thread(_ => {
					client.ErrorOccurred += (s, e) => context.Fail(e.Exception, "Error on connection");
					client.ConnectAsync().Wait();

					for (int j = 0; j < count; ++j) {
						var task = client.AppendToStreamAsync(streams[rnd.Next(streamsCnt)],
							ExpectedVersion.Any,
							new EventData(Guid.NewGuid(),
								"TakeSomeSpaceEvent",
								false,
								Common.Utils.Helper.UTF8NoBom.GetBytes("DATA" + new string('*', size)),
								Common.Utils.Helper.UTF8NoBom.GetBytes("METADATA" + new string('$', 100))));
						task.ContinueWith(x => {
							if (x.IsFaulted) {
								context.Fail(x.Exception.InnerException, "Error on writing operation.");
								return;
							}

							var localAll = Interlocked.Increment(ref succ);
							if (localAll % 1000 == 0) Console.Write('.');
							if (localAll % 100000 == 0) {
								var elapsed = sw2.Elapsed;
								sw2.Restart();
								context.Log.Debug("\nDONE TOTAL {writes} WRITES IN {elapsed} ({rate:0.0}/s).",
									localAll,
									elapsed,
									1000.0 * 100000 / elapsed.TotalMilliseconds);
							}

							if (localAll == requestsCnt) {
								context.Success();
								doneEvent.Set();
							}
						});
					}
				}) {IsBackground = true});
			}

			var sw = Stopwatch.StartNew();
			sw2.Start();
			threads.ForEach(thread => thread.Start());
			doneEvent.Wait();
			clients.ForEach(x => x.Close());
			sw.Stop();

			context.Log.Information("Completed. Successes: {success}.", succ);

			var reqPerSec = (succ + 0.0) / sw.ElapsedMilliseconds * 1000;
			context.Log.Information("{requests} requests completed in {elapsed}ms ({rate:0.00} reqs per sec).", succ,
				sw.ElapsedMilliseconds, reqPerSec);

			var fail = requestsCnt - succ;
			PerfUtils.LogData(
				Keyword,
				PerfUtils.Row(PerfUtils.Col("clientsCnt", clientsCnt),
					PerfUtils.Col("requestsCnt", requestsCnt),
					PerfUtils.Col("ElapsedMilliseconds", sw.ElapsedMilliseconds)),
				PerfUtils.Row(PerfUtils.Col("successes", succ), PerfUtils.Col("failures", fail)));
			var failuresRate = (int)(100 * fail / (fail + succ));
			PerfUtils.LogTeamCityGraphData(string.Format("{0}-{1}-{2}-reqPerSec", Keyword, clientsCnt, requestsCnt),
				(int)reqPerSec);
			PerfUtils.LogTeamCityGraphData(
				string.Format("{0}-{1}-{2}-failureSuccessRate", Keyword, clientsCnt, requestsCnt), failuresRate);
			PerfUtils.LogTeamCityGraphData(
				string.Format("{0}-c{1}-r{2}-st{3}-s{4}-reqPerSec", Keyword, clientsCnt, requestsCnt, streamsCnt, size),
				(int)reqPerSec);
			PerfUtils.LogTeamCityGraphData(
				string.Format("{0}-c{1}-r{2}-st{3}-s{4}-failureSuccessRate", Keyword, clientsCnt, requestsCnt,
					streamsCnt, size), failuresRate);

			if (Interlocked.Read(ref succ) != requestsCnt)
				context.Fail(reason: "There were errors or not all requests completed.");
			else
				context.Success();
		}
	}
}
