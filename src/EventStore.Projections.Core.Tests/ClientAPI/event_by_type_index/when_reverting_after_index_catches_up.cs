using System.Threading;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.event_by_type_index {
	[TestFixture]
	public class when_reverting_after_index_catches_up : specification_with_standard_projections_runnning {
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
    $init: function(){return {a:0}},
    type1: function(s,e){s.a++},
    type2: function(s,e){s.a++},
    $deleted: function(s,e){s.deleted=1;},
}).outputState();
");
			_manager.AbortAsync("test-projection", _admin).Wait();
			WaitIdle();

			EnableStandardProjections();
			WaitIdle();
			DisableStandardProjections();
			WaitIdle();
			EnableStandardProjections();
			WaitIdle();
		}

		protected override void When() {
			base.When();
			_manager.EnableAsync("test-projection", _admin).Wait();
			WaitIdle();
		}

		[Test, Category("Network")]
		public void receives_deleted_notification() {
			AssertStreamTail("$projections-test-projection-stream-1-result", "Result:{\"a\":1}", "Result:{\"a\":2}");
			AssertStreamTail("$projections-test-projection-stream-2-result", "Result:{\"a\":1}", "Result:{\"a\":2}");
		}
	}
}
