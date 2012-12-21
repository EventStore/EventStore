using System.Linq;
using System.Text;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.partition_state_update_manager
{
    [TestFixture]
    public class when_two_states_were_updated
    {
        private PartitionStateUpdateManager _updateManager;
        private CheckpointTag _zero = CheckpointTag.FromPosition(100, 50);
        private CheckpointTag _one = CheckpointTag.FromPosition(200, 150);
        private CheckpointTag _two = CheckpointTag.FromPosition(300, 250);
        private CheckpointTag _three = CheckpointTag.FromPosition(400, 350);

        [SetUp]
        public void setup()
        {
            _updateManager = new PartitionStateUpdateManager(new ProjectionNamesBuilder("projection"));
            _updateManager.StateUpdated("partition1", "state1", _zero, _one);
            _updateManager.StateUpdated("partition2", "state2", _zero, _two);
        }

        [Test]
        public void handles_state_updated_for_the_same_partition()
        {
            _updateManager.StateUpdated("partition1", "state", _two, _three);
        }

        [Test]
        public void handles_state_updated_for_another_partition()
        {
            _updateManager.StateUpdated("partition3", "state", _two, _three);
        }

        [Test]
        public void emit_events_writes_both_state_updated_event()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            Assert.AreEqual(2, (eventWriter.Writes.SelectMany(write => write)).Count());
        }

        [Test]
        public void emit_events_writes_to_correct_streams()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            var events = eventWriter.Writes.SelectMany(write => write).ToArray();
            Assert.IsTrue(events.Any((v => "$projections-projection-partition1-state" == v.StreamId)));
            Assert.IsTrue(events.Any((v => "$projections-projection-partition2-state" == v.StreamId)));
        }

        [Test]
        public void emit_events_writes_correct_state_data()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            var events = eventWriter.Writes.SelectMany(write => write).ToArray();
            var event1 = events.Single(v => "$projections-projection-partition1-state" == v.StreamId);
            var event2 = events.Single(v => "$projections-projection-partition2-state" == v.StreamId);

            Assert.AreEqual("state1", Encoding.UTF8.GetString(event1.Data));
            Assert.AreEqual("state2", Encoding.UTF8.GetString(event2.Data));
        }

        [Test]
        public void emit_events_writes_event_with_correct_caused_by_tag()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            var events = eventWriter.Writes.SelectMany(write => write).ToArray();
            var event1 = events.Single(v => "$projections-projection-partition1-state" == v.StreamId);
            var event2 = events.Single(v => "$projections-projection-partition2-state" == v.StreamId);
            Assert.AreEqual(_one, event1.CausedByTag);
            Assert.AreEqual(_two, event2.CausedByTag);
        }

        [Test]
        public void emit_events_writes_event_with_correct_expected_tag()
        {
            var eventWriter = new FakeEventWriter();
            _updateManager.EmitEvents(eventWriter);
            var events = eventWriter.Writes.SelectMany(write => write).ToArray();
            var event1 = events.Single(v => "$projections-projection-partition1-state" == v.StreamId);
            var event2 = events.Single(v => "$projections-projection-partition2-state" == v.StreamId);
            Assert.AreEqual(_zero, event1.ExpectedTag);
            Assert.AreEqual(_zero, event2.ExpectedTag);
        }
    }
}