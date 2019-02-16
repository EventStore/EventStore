using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_created.with_from_all_any_foreach_projection {
	[TestFixture]
	public class when_running_and_events_are_posted : specification_with_standard_projections_runnning {
		protected override bool GivenStandardProjectionsRunning() {
			return false;
		}

		protected override void Given() {
			base.Given();
			PostEvent("stream-1", "type1", "{}");
			PostEvent("stream-1", "type2", "{}");
			PostEvent("stream-2", "type1", "{}");
			PostEvent("stream-2", "type2", "{}");
			WaitIdle();
		}

		protected override void When() {
			base.When();
			PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {a:0}},
    $any: function(s,e) {s.a++;},
    $created: function(s,e){s.a++;},
}).outputState();
");
			WaitIdle();
		}

		[Test, Category("Network")]
		public void receives_created_notification() {
			AssertStreamTail("$projections-test-projection-stream-1-result", "Result:{\"a\":3}");
			AssertStreamTail("$projections-test-projection-stream-2-result", "Result:{\"a\":3}");
		}
	}
}
