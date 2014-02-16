using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.ClientAPI.when_handling_delete
{
    [TestFixture]
    public class with_from_all_foreach_projection_running_and_events_are_indexed : specification_with_standard_projections_runnning
    {
        protected override bool GivenStandardProjectionsRunning()
        {
            return false;
        }

        protected override void Given()
        {
            base.Given();
            PostEvent("stream1", "type1", "{}");
            PostEvent("stream1", "type2", "{}");
            PostEvent("stream2", "type1", "{}");
            PostEvent("stream2", "type2", "{}");
            WaitIdle();
            EnableStandardProjections();
            WaitIdle();
            HardDeleteStream("stream1");
            WaitIdle();
            DisableStandardProjections();
            WaitIdle();

            // required to flush index checkpoint
            {
                EnableStandardProjections();
                WaitIdle();
                DisableStandardProjections();
                WaitIdle();
            }

        }

        protected override void When()
        {
            base.When();
            PostProjection(@"
fromAll().foreachStream().when({
    $init: function(){return {}},
    type1: function(s,e){s.a=(s.a||0) + 1},
    type2: function(s,e){s.a=(s.a||0) + 1},
    $deleted: function(s,e){s.deleted=1},
}).outputState();
");
            WaitIdle();
        }

        [Test, Category("Network")]
        public void receives_deleted_notification()
        {
            AssertStreamTail(
                "$projections-test-projection-stream1-result", "Result:{\"deleted\":1}");
            AssertStreamTail(
                "$projections-test-projection-stream2-result", "Result:{\"a\":2}");
        }
    }
}