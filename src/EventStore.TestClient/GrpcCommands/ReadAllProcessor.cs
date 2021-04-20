using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.TestClient.Commands;

namespace EventStore.TestClient.GrpcCommands {
	internal class ReadAllProcessor : ICmdProcessor {
		private static readonly UTF8Encoding UTF8NoBom = new UTF8Encoding(false);

		public string Usage {
			//                         0          1            2
			get { return "RDALLGRPC [[F|B] [<commit pos> <prepare pos>]]"; }
		}

		public string Keyword {
			get { return "RDALLGRPC"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			Direction direction = Direction.Forwards;
			Position startPosition = Position.Start;

			if (args.Length > 0) {
				if (args.Length != 1 && args.Length != 3)
					return false;

				switch (args[0].ToUpper())
				{
					case "F":
						direction = Direction.Forwards;
						break;
					case "B":
						direction = Direction.Backwards;
						break;
					default:
						return false;
				}

				if (args.Length >= 3) {
					if (ulong.TryParse(args[1], out var commitPos) && ulong.TryParse(args[2], out var preparePos))
						startPosition = new Position(commitPos, preparePos);
					else return false;
				}
			}

			var monitor = new RequestMonitor();
			try {
				ReadAll(context, direction, startPosition, monitor).Wait();
				context.Success();
			} catch (Exception ex) {
				context.Fail(ex);
			}

			return true;
		}

		private async Task ReadAll(CommandProcessorContext context, Direction direction, Position startPosition, RequestMonitor monitor) {
			context.IsAsync();

			int total = 0;
			var sw = new Stopwatch();

			context.Log.Information("Reading $all {direction} from {position}",
				direction.ToString(), startPosition);

			var client = context._grpcTestClient.CreateGrpcClient();

			sw.Start();
			var result = client.ReadAllAsync(direction, startPosition, configureOperationOptions: opts => opts.TimeoutAfter = null);
			await foreach (var res in result) {
				total++;
				if (total % 10_000 == 0) {
					context.Log.Information("Read {total} events. Position: {position}", total,
						res.OriginalPosition);
				}
			}

			sw.Stop();
			context.Log.Information("=== Reading ALL {readDirection} completed in {elapsed}. Total read: {total}",
				direction, sw.Elapsed, total);
		}
	}
}
