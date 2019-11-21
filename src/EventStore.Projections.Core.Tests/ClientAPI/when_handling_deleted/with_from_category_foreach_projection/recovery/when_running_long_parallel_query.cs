﻿using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_category_foreach_projection.
	recovery {
	[TestFixture, Category("LongRunning")]
	public class when_running_long_parallel_query : specification_with_standard_projections_runnning {
		protected override int GivenWorkerThreadCount() {
			return 2;
		}

		protected override async Task Given() {
			await base.Given();
			for (var i = 0; i <= 900; i++) {
				for (var j = 0; j < 10; j++) {
					await PostEvent("stream-" + i, "type" + (j % 2 + 1), "{}");
				}
			}

			WaitIdle();
		}

		protected override async Task When() {
			await base.When();
			await PostQuery(@"
fromCategory('stream').foreachStream().when({
    $init: function(){return {a:0}},
    type1: function(s,e){s.a++},
    type2: function(s,e){s.a++},
});
");
			WaitIdle(multiplier: 10);
		}

		[Test, Category("Network"), Category("LongRunning")]
		public async Task produces_correct_result() {
			await AssertStreamTail("$projections-query-stream-1-result", "Result:{\"a\":10}");
			await AssertStreamTail("$projections-query-stream-2-result", "Result:{\"a\":10}");
			await AssertStreamTail("$projections-query-stream-3-result", "Result:{\"a\":10}");
			await DumpStreamsAsync();
		}

		private async Task DumpStreamsAsync() {
#if DEBUG
			var result = await _conn.ReadAllEventsForwardAsync(Position.Start, 4096, false, _admin);
			var top = result.Events.GroupBy(v => v.OriginalStreamId)
				.Select(v => new { v.Key, Count = v.Count() })
				.OrderByDescending(v => v.Count);
			foreach (var s in top.Take(50)) {
				Trace.WriteLine(s.Count.ToString("0000") + " - " + s.Key);
			}

			Trace.WriteLine("==============");

			var topE = result.Events.GroupBy(v => v.Event.EventType)
				.Select(v => new { v.Key, Count = v.Count(), Events = v })
				.OrderByDescending(v => v.Count);
			foreach (
				var e in
				topE)
				Trace.WriteLine(e.Count.ToString("0000") + " - " + e.Key);

			Trace.WriteLine("==============");

			foreach (var s in topE.Take(2)) {
				Dump(">>> " + s.Key, s.Key, s.Events.Take(75).ToArray());
			}
#endif
		}
	}
}
