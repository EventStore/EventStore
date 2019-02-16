using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_all_any_foreach_projection {
	[TestFixture]
	public class
		when_running_and_then_other_events_tombstone_ant_other_events :
			specification_with_standard_projections_runnning {
		protected override bool GivenStandardProjectionsRunning() {
			return false;
		}

		protected override void Given() {
			base.Given();
			PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {a:0}},
    $any: function(s,e){s.a++},
    $deleted: function(s,e){s.deleted=1;},
}).outputState();
");
		}

		protected override void When() {
			base.When();
			PostEvent("stream-1", "type1", "{}");
			PostEvent("stream-1", "type2", "{}");
			PostEvent("stream-2", "type1", "{}");
			PostEvent("stream-2", "type2", "{}");
			WaitIdle();
			HardDeleteStream("stream-1");
			WaitIdle();
			PostEvent("stream-2", "type1", "{}");
			PostEvent("stream-2", "type2", "{}");
			PostEvent("stream-3", "type1", "{}");
			WaitIdle();
		}

		[Test, Category("Network")]
		public void receives_deleted_notification() {
			AssertStreamTail(
				"$projections-test-projection-stream-1-result", "Result:{\"a\":2}", "Result:{\"a\":2,\"deleted\":1}");
			AssertStreamTail("$projections-test-projection-stream-2-result", "Result:{\"a\":4}");
			AssertStreamTail("$projections-test-projection-stream-3-result", "Result:{\"a\":1}");
		}
	}
}
