﻿using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_category_foreach_projection {
	[TestFixture]
	public class when_running_and_events_are_indexed : specification_with_standard_projections_runnning {
		protected override bool GivenStandardProjectionsRunning() {
			return false;
		}

		protected override async Task Given() {
			await base.Given();
			await PostEvent("stream-1", "type1", "{}");
			await PostEvent("stream-1", "type2", "{}");
			await PostEvent("stream-2", "type1", "{}");
			await PostEvent("stream-2", "type2", "{}");
			await HardDeleteStream("stream-1");
			WaitIdle();
			await EnableStandardProjections();
		}

		protected override async Task When() {
			await base.When();
			await PostProjection(@"
fromCategory('stream').foreachStream().when({
    $init: function(){return {}},
    type1: function(s,e){s.a=(s.a||0) + 1},
    type2: function(s,e){s.a=(s.a||0) + 1},
    $deleted: function(s,e){s.deleted=1},
}).outputState();
");
			WaitIdle();
		}

		[Test, Category("Network")]
		public async Task receives_deleted_notification() {
			await AssertStreamTail("$projections-test-projection-stream-1-result", "Result:{\"deleted\":1}");
			await AssertStreamTail("$projections-test-projection-stream-2-result", "Result:{\"a\":2}");
		}
	}
}
