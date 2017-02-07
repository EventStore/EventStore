using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.query_result.with_long_from_all_query
{
    [TestFixture]
    public class when_getting_result : specification_with_standard_projections_runnning
    {
        protected override void Given()
        {
            base.Given();

            for (var i = 1; i <= 10000; i++)
            {
                PostEvent("stream-" + i, "type1", "{}");
            }

            WaitIdle();
        }

        protected override void When()
        {
            base.When();

            PostQuery(@"
fromAll().when({
    $init: function(){return {count:0}},
    type1: function(s,e){s.count++},
});
");
        }

        [Test, Category("Network")]
        public void waits_for_results()
        {
            var result = _queryManager.GetResultAsync("query", _admin).Result;
            Assert.AreEqual("{\"count\":10000}", result);
        }
    }
}