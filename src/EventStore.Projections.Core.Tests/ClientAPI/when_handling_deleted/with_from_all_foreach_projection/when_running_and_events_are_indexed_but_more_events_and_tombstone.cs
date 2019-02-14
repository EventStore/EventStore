using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_all_foreach_projection {
	[TestFixture]
	public class when_running_and_events_are_indexed_but_more_events_and_tombstone :
		specification_with_standard_projections_runnning {
		protected override bool GivenStandardProjectionsRunning() {
			return false;
		}

		protected override void Given() {
			base.Given();
			PostEvent("stream-1", "type1", "{}");
			PostEvent("stream-2", "type1", "{}");
			PostEvent("stream-2", "type2", "{}");
			WaitIdle();
			EnableStandardProjections();
			WaitIdle();
			DisableStandardProjections();
			;
			WaitIdle();

			// required to flush index checkpoint
			{
				EnableStandardProjections();
				WaitIdle();
				DisableStandardProjections();
				WaitIdle();
			}

			PostEvent("stream-1", "type2", "{}");
			HardDeleteStream("stream-1");
			WaitIdle();
		}

		protected override void When() {
			base.When();
			PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {a:0}},
    type1: function(s,e){s.a++},
    type2: function(s,e){},
    $deleted: function(s,e){s.deleted=1},
}).outputState();
");
			WaitIdle();
		}

		[Test, Category("Network")]
		public void receives_deleted_notification() {
			AssertStreamTail("$projections-test-projection-stream-1-result", "Result:{\"a\":0,\"deleted\":1}");
		}
	}
}
