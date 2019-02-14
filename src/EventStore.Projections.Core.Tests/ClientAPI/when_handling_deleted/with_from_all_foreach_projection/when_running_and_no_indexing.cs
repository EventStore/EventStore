using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_all_foreach_projection {
	[TestFixture]
	public class when_running_and_no_indexing : specification_with_standard_projections_runnning {
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
			PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {}},
    type1: function(s,e){s.a=1},
    type2: function(s,e){s.a=1},
    $deleted: function(s,e){s.deleted=1;},
}).outputState();
");
		}

		protected override void When() {
			base.When();
			this.HardDeleteStream("stream-1");
			WaitIdle();
		}

		[Test, Category("Network")]
		public void receives_deleted_notification() {
			AssertStreamTail(
				"$projections-test-projection-stream-1-result", "Result:{\"a\":1}", "Result:{\"a\":1,\"deleted\":1}");
		}
	}
}
