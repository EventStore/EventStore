﻿using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_all_foreach_projection {
	[TestFixture]
	public class when_running_and_events_are_indexed_but_a_stream_and_tombstone :
		specification_with_standard_projections_runnning {
		protected override bool GivenStandardProjectionsRunning() {
			return false;
		}

		protected override async Task Given() {
			await base.Given();
			await PostEvent("stream-2", "type1", "{}");
			await PostEvent("stream-2", "type2", "{}");
			WaitIdle();
			await EnableStandardProjections();
			WaitIdle();
			await DisableStandardProjections();
			WaitIdle();

			// required to flush index checkpoint
			{
				await EnableStandardProjections();
				WaitIdle();
				await DisableStandardProjections();
				WaitIdle();
			}

			await PostEvent("stream-1", "type1", "{}");
			await PostEvent("stream-1", "type2", "{}");
			await HardDeleteStream("stream-1");
			WaitIdle();
		}

		protected override async Task When() {
			await base.When();
			await PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {a:0}},
    type1: function(s,e){s.a++},
    type2: function(s,e){s.a++},
    $deleted: function(s,e){s.deleted=1},
}).outputState();
");
			WaitIdle();
		}

		[Test, Category("Network")]
		public async Task receives_deleted_notification() {
			await AssertStreamTail("$projections-test-projection-stream-1-result", "Result:{\"a\":2,\"deleted\":1}");
		}
	}
}
