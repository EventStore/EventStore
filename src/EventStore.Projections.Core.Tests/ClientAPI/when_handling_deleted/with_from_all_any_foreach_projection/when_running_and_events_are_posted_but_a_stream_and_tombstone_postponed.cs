using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_all_any_foreach_projection {
	[TestFixture]
	public class when_running_and_events_are_posted_but_a_stream_and_tombstone_postponed :
		specification_with_standard_projections_runnning {
		protected override bool GivenStandardProjectionsRunning() {
			return false;
		}

		protected override void Given() {
			base.Given();
			PostEvent("stream-2", "type1", "{}");
			PostEvent("stream-2", "type2", "{}");
			WaitIdle();

			PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {a:0}},
    $any: function(s,e){s.a++},
    $deleted: function(s,e){s.deleted=1;},
}).outputState();
");
			WaitIdle();
		}

		protected override void When() {
			base.When();
			PostEvent("stream-1", "type1", "{}");
			PostEvent("stream-1", "type2", "{}");
			WaitIdle();
			HardDeleteStream("stream-1");
			WaitIdle();
		}

		[Test, Category("Network")]
		public void receives_deleted_notification() {
			AssertStreamTail("$projections-test-projection-stream-1-result", "Result:{\"a\":2,\"deleted\":1}");
		}
	}
}
