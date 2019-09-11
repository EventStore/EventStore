using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_category_foreach_projection.
	recovery {
	[TestFixture]
	public class when_running_parallel_query : specification_with_standard_projections_runnning {
		protected override int GivenWorkerThreadCount() {
			return 2;
		}

		protected override async Task Given() {
			await base.Given();
			await PostEvent("stream-1", "type1", "{}");
			await PostEvent("stream-1", "type2", "{}");
			await PostEvent("stream-2", "type1", "{}");
			await PostEvent("stream-2", "type2", "{}");
			await PostEvent("stream-2", "type1", "{}");
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
			WaitIdle();
		}

		[Test, Category("Network")]
		public async Task produces_correct_result() {
			await AssertStreamTail("$projections-query-stream-1-result", "Result:{\"a\":2}");
			await AssertStreamTail("$projections-query-stream-2-result", "Result:{\"a\":3}");
		}
	}
}
