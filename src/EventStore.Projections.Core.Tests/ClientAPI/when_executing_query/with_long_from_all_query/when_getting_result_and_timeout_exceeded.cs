using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_executing_query.with_long_from_all_query
{
    [TestFixture, Category("LongRunning")]
    public class when_getting_result_and_timeout_exceeded : specification_with_standard_projections_runnning
    {
        protected override void Given()
        {
            base.Given();

            for (var i = 1; i <= 75000; i++)
            {
                PostEvent("stream-" + i, "type1", "{}");
            }

            WaitIdle();
        }

        [Test, Category("Network")]
        public void throws_exception()
        {
            const string query = @"
fromAll().when({
    $init: function(){return {count:0}},
    type1: function(s,e){s.count++},
});
";
            Assert.ThrowsAsync<OperationTimedOutException>(() => _queryManager.ExecuteAsync("query", query, _admin));
        }
    }
}
