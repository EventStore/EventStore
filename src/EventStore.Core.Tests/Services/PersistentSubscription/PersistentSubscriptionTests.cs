using System;
using EventStore.ClientAPI.Messages;
using EventStore.Core.Data;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.Tests.Services.Replication;
using EventStore.Core.Tests.Services.VNode;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.PersistentSubscriptionTests
{
    [TestFixture]
    public class when_creating_persistent_subscription
    {
        private Core.Services.PersistentSubscription.PersistentSubscription _sub;

        [TestFixtureSetUp]
        public void Setup()
        {
            _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(new FakeCheckpointReader())
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

        }

        [Test]
        public void subscription_id_is_set()
        {
            Assert.AreEqual("streamName:groupName", _sub.SubscriptionId);
        }

        [Test]
        public void stream_name_is_set()
        {
            Assert.AreEqual("streamName", _sub.EventStreamId);
        }

        [Test]
        public void group_name_is_set()
        {
            Assert.AreEqual("groupName", _sub.GroupName);
        }

        [Test]
        public void there_are_no_clients()
        {
            Assert.IsFalse(_sub.HasClients);
            Assert.AreEqual(0, _sub.ClientCount);
        }

        [Test]
        public void null_checkpoint_reader_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                        .WithEventLoader(new FakeEventLoader(x => { }))
                        .WithCheckpointReader(null)
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            });
        }

        [Test]
        public void null_checkpoint_writer_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                        .WithEventLoader(new FakeEventLoader(x => { }))
                        .WithCheckpointReader(new FakeCheckpointReader())
                        .WithCheckpointWriter(null));

            });
        }


        [Test]
        public void null_event_reader_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                        .WithEventLoader(null)
                        .WithCheckpointReader(new FakeCheckpointReader())
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            });
        }

        [Test]
        public void null_stream_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor(null, "groupName")
                        .WithEventLoader(new FakeEventLoader(x => { }))
                        .WithCheckpointReader(new FakeCheckpointReader())
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            });
        }

        [Test]
        public void null_groupname_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor("streamName", null)
                        .WithEventLoader(new FakeEventLoader(x => { }))
                        .WithCheckpointReader(new FakeCheckpointReader())
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            });
        }

    }

    [TestFixture]
    public class LiveTests
    {
        [Test]
        public void live_subscription_pushes_events_to_client()
        {
            var envelope = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .StartFromCurrent());
            reader.Load(null);        
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(),envelope, 10, "foo", "bar");
            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0));
            Assert.AreEqual(1,envelope.Replies.Count);
        }

        [Test]
        public void live_subscription_with_round_robin_two_pushes_events_to_both()
        {
            var envelope1 = new FakeEnvelope();
            var envelope2 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .PreferRoundRobin()
                    .StartFromCurrent());
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope2, 10, "foo", "bar");
            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0));
            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1));
            Assert.AreEqual(1, envelope1.Replies.Count);
            Assert.AreEqual(1, envelope2.Replies.Count);
        }

        [Test]
        public void live_subscription_with_prefer_one_and_two_pushes_events_to_both()
        {
            var envelope1 = new FakeEnvelope();
            var envelope2 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .PreferDispatchToSingle()
                    .StartFromCurrent());
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope2, 10, "foo", "bar");
            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0));
            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1));
            Assert.AreEqual(2, envelope1.Replies.Count);
        }


        [Test]
        public void subscription_with_pull_sends_data_to_client()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .StartFromBeginning());
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.HandleReadCompleted(new[] {Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0)}, 1);
            Assert.AreEqual(1, envelope1.Replies.Count);
        }

        [Test]
        public void subscription_with_pull_does_not_crash_if_not_ready_yet()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .StartFromBeginning());
            Assert.DoesNotThrow(() =>
            {
                sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
                sub.HandleReadCompleted(new[] {Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0)}, 1);
            });
        }

        [Test]
        public void subscription_with_live_data_does_not_crash_if_not_ready_yet()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .StartFromBeginning());
            Assert.DoesNotThrow(() =>
            {
                sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
                sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0));
            });
        }


        [Test]
        public void subscription_with_pull_and_round_robin_set_and_two_clients_sends_data_to_client()
        {
            var envelope1 = new FakeEnvelope();
            var envelope2 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .PreferRoundRobin()
                    .StartFromBeginning());
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope2, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
                Helper.BuildFakeEvent(id2, "type", "streamName", 1)
            }, 1);
            Assert.AreEqual(1, envelope1.Replies.Count);
            Assert.AreEqual(1,envelope2.Replies.Count);
        }


        [Test]
        public void subscription_with_pull_and_prefer_one_set_and_two_clients_sends_data_to_one_client()
        {
            var envelope1 = new FakeEnvelope();
            var envelope2 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .PreferDispatchToSingle()
                    .StartFromBeginning());
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope2, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
                Helper.BuildFakeEvent(id2, "type", "streamName", 1)
            }, 1);
            Assert.AreEqual(2, envelope1.Replies.Count);
        }
    }

    [TestFixture]
    public class Checkpointing
    {
        [Test]
        public void subscription_does_not_write_checkpoint_when_max_not_hit()
        {
            int cp = -1;
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .MinimumToCheckPoint(5)
                    .MaximumToCheckPoint(20));
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id, "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1)
            }, 1);
            sub.AcknowledgeMessagesProcessed(corrid, new[] { id });
            Assert.AreEqual(-1, cp);
        }

        [Test]
        public void subscription_does_not_write_checkpoint_when_min_hit()
        {
            int cp = -1;
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .MinimumToCheckPoint(1)
                    .MaximumToCheckPoint(20));
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id, "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1)
            }, 1);
            sub.AcknowledgeMessagesProcessed(corrid, new[] { id });
            Assert.AreEqual(-1, cp);
        }

        [Test]
        public void subscription_does_write_checkpoint_when_max_is_hit()
        {
            int cp = -1;
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .MaximumToCheckPoint(1));
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id, "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1)
            }, 1);
            sub.AcknowledgeMessagesProcessed(corrid, new []{id});
            Assert.AreEqual(1, cp);
        }

        [Test]
        public void subscription_does_not_include_not_acked_messages_when_max_is_hit()
        {
            int cp = -1;
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .MaximumToCheckPoint(1));
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id, "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 2),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 3)
            }, 1);
            sub.AcknowledgeMessagesProcessed(corrid, new[] { id });
            Assert.AreEqual(1, cp);
        }


        [Test]
        public void subscription_does_write_checkpoint_on_time_when_min_is_hit()
        {
            int cp = -1;
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .MinimumToCheckPoint(1)
                    .MaximumToCheckPoint(5));
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id, "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1)
            }, 1);
            sub.AcknowledgeMessagesProcessed(corrid, new[] { id });
            sub.NotifyClockTick(DateTime.Now);
            Assert.AreEqual(1, cp);
        }

        [Test]
        public void subscription_does_not_write_checkpoint_on_time_when_min_is_not_hit()
        {
            int cp = -1;
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .MinimumToCheckPoint(2)
                    .MaximumToCheckPoint(5));
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id, "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1)
            }, 1);
            sub.AcknowledgeMessagesProcessed(corrid, new[] { id });
            sub.NotifyClockTick(DateTime.Now);
            Assert.AreEqual(1, cp);
        }
    }

    [TestFixture]
    public class TimeoutTests
    {
        [Test]
        public void with_no_timeouts_to_happen_no_timeouts_happen()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .WithMessageTimeoutOf(TimeSpan.FromSeconds(3)));
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
                Helper.BuildFakeEvent(id2, "type", "streamName", 1)
            }, 1);
            envelope1.Replies.Clear();
            sub.NotifyClockTick(DateTime.Now.AddSeconds(1));
            Assert.AreEqual(0, envelope1.Replies.Count);
        }
        [Test]
        public void messages_get_timed_out_and_resent_to_clients()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .WithMessageTimeoutOf(TimeSpan.FromSeconds(1)));
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
                Helper.BuildFakeEvent(id2, "type", "streamName", 1)
            }, 1);
            envelope1.Replies.Clear();
            sub.NotifyClockTick(DateTime.Now.AddSeconds(3));
            Assert.AreEqual(2, envelope1.Replies.Count);
            var msg1 = (Messages.ClientMessage.PersistentSubscriptionStreamEventAppeared) envelope1.Replies[0];
            var msg2 = (Messages.ClientMessage.PersistentSubscriptionStreamEventAppeared)envelope1.Replies[1];
            Assert.IsTrue(id1 == msg1.Event.Event.EventId || id1 == msg2.Event.Event.EventId);
            Assert.IsTrue(id2 == msg1.Event.Event.EventId || id2 == msg2.Event.Event.EventId);
        }
    }

    public class Helper
    {
        public static ResolvedEvent BuildFakeEvent(Guid id, string type, string stream, int version)
        {
            return
                new ResolvedEvent(new EventRecord(version, 1234567, Guid.NewGuid(), id, 1234567, 1234, stream, version,
                    DateTime.Now, PrepareFlags.SingleWrite, type, new byte[0], new byte[0]));
        }
    }

    [TestFixture]
    public class AddingClientTests
    {
        [Test]
        public void adding_a_client_adds_the_client()
        {
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeEventLoader(x => { }))
                    .WithCheckpointReader(new FakeCheckpointReader())
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), new FakeEnvelope(), 1, "foo", "bar");
            Assert.IsTrue(sub.HasClients);
            Assert.AreEqual(1, sub.ClientCount);
        }
    }


    class FakeEventLoader : IPersistentSubscriptionEventLoader
    {
        private readonly Action<int> _action;

        public FakeEventLoader(Action<int> action)
        {
            _action = action;
        }

        public void BeginLoadState(Core.Services.PersistentSubscription.PersistentSubscription subscription, int startEventNumber, int countToLoad, Action<ResolvedEvent[], int> onFetchCompleted)
        {
            _action(startEventNumber);
        }
    }

    class FakeCheckpointReader : IPersistentSubscriptionCheckpointReader
    {
        private Action<int?> _onStateLoaded;

        public void BeginLoadState(string subscriptionId, Action<int?> onStateLoaded)
        {
            _onStateLoaded = onStateLoaded;
        }

        public void Load(int? state)
        {
            _onStateLoaded(state);
        }
    }

    class FakeCheckpointWriter : IPersistentSubscriptionCheckpointWriter
    {
        private readonly Action<int> _action;

        public FakeCheckpointWriter(Action<int> action)
        {
            _action = action;
        }

        public void BeginWriteState(int state)
        {
            _action(state);
        }
    }
}
