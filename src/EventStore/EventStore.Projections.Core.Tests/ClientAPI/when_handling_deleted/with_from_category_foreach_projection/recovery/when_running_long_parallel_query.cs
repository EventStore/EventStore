using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_deleted.with_from_category_foreach_projection.recovery
{
    [TestFixture, Category("LongRunning")]
    public class when_running_long_parallel_query : specification_with_standard_projections_runnning
    {
        protected override int GivenWorkerThreadCount()
        {
            return 2;
        }

        protected override void Given()
        {
            base.Given();
            for (var i = 0; i <= 40; i++)
            {
                for (var j = 0; j < 10; j++)
                {
                    PostEvent("stream-" + i, "type" + (j % 2 + 1), "{}");
                }
            }
            WaitIdle();
        }

        protected override void When()
        {
            base.When();
            PostQuery(@"
fromCategory('stream').foreachStream().when({
    $init: function(){return {a:0}},
    type1: function(s,e){s.a++},
    type2: function(s,e){s.a++},
});
");
            WaitIdle(multiplier: 10);
        }

        [Test, Category("Network"), Category("LongRunning")]
        public void produces_correct_result()
        {
            AssertStreamTail("$projections-query-stream-1-result", "Result:{\"a\":10}");
            AssertStreamTail("$projections-query-stream-2-result", "Result:{\"a\":10}");
            AssertStreamTail("$projections-query-stream-35-result", "Result:{\"a\":10}");
        }
    }
}