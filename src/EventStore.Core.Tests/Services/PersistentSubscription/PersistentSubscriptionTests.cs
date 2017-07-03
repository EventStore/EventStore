﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.Tests.ClientAPI.UserManagement;
using EventStore.Core.Tests.Services.Replication;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ExpectedVersion = EventStore.Core.Data.ExpectedVersion;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Core.Tests.Services.PersistentSubscription
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(new FakeCheckpointReader())
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker()));
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
                        .WithEventLoader(new FakeStreamReader(x => { }))
                        .WithCheckpointReader(null)
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                        .WithMessageParker(new FakeMessageParker()));

            });
        }

        [Test]
        public void null_checkpoint_writer_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                        .WithEventLoader(new FakeStreamReader(x => { }))
                        .WithCheckpointReader(new FakeCheckpointReader())
                        .WithCheckpointWriter(null)
                        .WithMessageParker(new FakeMessageParker()));

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
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                        .WithMessageParker(new FakeMessageParker()));

            });
        }

        [Test]
        public void null_stream_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor(null, "groupName")
                        .WithEventLoader(new FakeStreamReader(x => { }))
                        .WithCheckpointReader(new FakeCheckpointReader())
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                        .WithMessageParker(new FakeMessageParker()));

            });
        }

        [Test]
        public void null_groupname_throws_argument_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                    PersistentSubscriptionParamsBuilder.CreateFor("streamName", null)
                        .WithEventLoader(new FakeStreamReader(x => { }))
                        .WithCheckpointReader(new FakeCheckpointReader())
                        .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                        .WithMessageParker(new FakeMessageParker()));

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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
                    .StartFromBeginning());
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            sub.HandleReadCompleted(new[] {Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0)}, 1, false);
            Assert.AreEqual(1, envelope1.Replies.Count);
        }

        [Test]
        public void subscription_with_pull_does_not_crash_if_not_ready_yet()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
                    .StartFromBeginning());
            Assert.DoesNotThrow(() =>
            {
                sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
                sub.HandleReadCompleted(new[] {Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0)}, 1, false);
            });
        }

        [Test]
        public void subscription_with_live_data_does_not_crash_if_not_ready_yet()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1,false);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1, false);
            Assert.AreEqual(2, envelope1.Replies.Count);
        }
    }

    [TestFixture]
    public class DeleteTests
    {
        [Test]
        public void subscription_deletes_checkpoint_when_deleted()
        {
            var reader = new FakeCheckpointReader();
            var deleted = false;
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }, () => { deleted = true; }))
                    .WithMessageParker(new FakeMessageParker())
                    .StartFromCurrent());
            reader.Load(null);
            sub.Delete();
            Assert.IsTrue(deleted);
        }

        [Test]
        public void subscription_deletes_parked_messages_when_deleted()
        {
            var reader = new FakeCheckpointReader();
            var deleted = false;
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { }))
                    .WithMessageParker(new FakeMessageParker(() => { deleted = true; }))
                    .StartFromCurrent());
            reader.Load(null);
            sub.Delete();
            Assert.IsTrue(deleted);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1, false);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1, false);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1, false);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1, false);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .PreferDispatchToSingle()
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1, false);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => cp = i))
                    .WithMessageParker(new FakeMessageParker())
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
            }, 1, false);
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
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
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
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotifyClockTick(DateTime.Now.AddSeconds(1));
            Assert.AreEqual(0, envelope1.Replies.Count);
            Assert.AreEqual(0, parker.ParkedEvents.Count);
        }

        [Test]
        public void messages_get_timed_out_and_resent_to_clients()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
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
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotifyClockTick(DateTime.Now.AddSeconds(3));
            Assert.AreEqual(2, envelope1.Replies.Count);
            var msg1 = (Messages.ClientMessage.PersistentSubscriptionStreamEventAppeared) envelope1.Replies[0];
            var msg2 = (Messages.ClientMessage.PersistentSubscriptionStreamEventAppeared)envelope1.Replies[1];
            Assert.IsTrue(id1 == msg1.Event.Event.EventId || id1 == msg2.Event.Event.EventId);
            Assert.IsTrue(id2 == msg1.Event.Event.EventId || id2 == msg2.Event.Event.EventId);
            Assert.AreEqual(0, parker.ParkedEvents.Count);
        }

        [Test]
        public void message_gets_timed_out_and_parked_after_max_retry_count()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .WithMaxRetriesOf(0)
                    .WithMessageTimeoutOf(TimeSpan.FromSeconds(1)));
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotifyClockTick(DateTime.Now.AddSeconds(3));
            Assert.AreEqual(0, envelope1.Replies.Count);
            Assert.AreEqual(1, parker.ParkedEvents.Count);
            Assert.AreEqual(id1, parker.ParkedEvents[0].OriginalEvent.EventId);
        }

        [Test]
        public void multiple_messages_get_timed_out_and_parked_after_max_retry_count()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .PreferDispatchToSingle()
                    .StartFromBeginning()
                    .WithMaxRetriesOf(0)
                    .WithMessageTimeoutOf(TimeSpan.FromSeconds(1)));
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
                Helper.BuildFakeEvent(id2, "type", "streamName", 1),
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotifyClockTick(DateTime.Now.AddSeconds(3));
            Assert.AreEqual(0, envelope1.Replies.Count);
            Assert.AreEqual(2, parker.ParkedEvents.Count);
            Assert.IsTrue(id1 == parker.ParkedEvents[0].OriginalEvent.EventId ||
                          id1 == parker.ParkedEvents[1].OriginalEvent.EventId);
            Assert.IsTrue(id2 == parker.ParkedEvents[0].OriginalEvent.EventId ||
                          id2 == parker.ParkedEvents[1].OriginalEvent.EventId);
        }

        [Test]
        public void timeout_park_correctly_tracks_the_available_client_slots()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .WithMaxRetriesOf(0)
                    .WithMessageTimeoutOf(TimeSpan.Zero)
                    .StartFromBeginning());
            reader.Load(null);
            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), envelope1, 2, "foo", "bar");

            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1)
            }, 1, false);

            Assert.AreEqual(2, envelope1.Replies.Count);

            // Should expire first 2 and send to park.
            sub.NotifyClockTick(DateTime.Now.AddSeconds(1));
            parker.ParkMessageCompleted(0, OperationResult.Success);
            parker.ParkMessageCompleted(1, OperationResult.Success);
            Assert.AreEqual(2, parker.ParkedEvents.Count);

            // The next 2 should still be sent to client.
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1)
            }, 1, false);

            Assert.AreEqual(4, envelope1.Replies.Count);
        }

    }

    [TestFixture]
    public class NAKTests
    {
        [Test]
        public void explicit_nak_with_park_parks_the_message()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .StartFromBeginning());
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotAcknowledgeMessagesProcessed(corrid, new [] {id1}, NakAction.Park, "a reason from client.");
            Assert.AreEqual(0, envelope1.Replies.Count);
            Assert.AreEqual(1, parker.ParkedEvents.Count);
            Assert.AreEqual(id1, parker.ParkedEvents[0].OriginalEvent.EventId);
        }

        [Test]
        public void explicit_nak_with_skip_skips_the_message()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .StartFromBeginning());
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotAcknowledgeMessagesProcessed(corrid, new[] { id1 }, NakAction.Skip, "a reason from client.");
            Assert.AreEqual(0, envelope1.Replies.Count);
            Assert.AreEqual(0, parker.ParkedEvents.Count);
        }

        [Test]
        public void explicit_nak_with_default_retries_the_message()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .StartFromBeginning());
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotAcknowledgeMessagesProcessed(corrid, new[] { id1 }, NakAction.Unknown, "a reason from client.");
            Assert.AreEqual(1, envelope1.Replies.Count);
            Assert.AreEqual(0, parker.ParkedEvents.Count);
        }

        [Test]
        public void explicit_nak_with_retry_retries_the_message()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .StartFromBeginning());
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
            }, 1, false);
            envelope1.Replies.Clear();
            sub.NotAcknowledgeMessagesProcessed(corrid, new[] { id1 }, NakAction.Retry, "a reason from client.");
            Assert.AreEqual(1, envelope1.Replies.Count);
            Assert.AreEqual(id1, ((ClientMessage.PersistentSubscriptionStreamEventAppeared)envelope1.Replies[0]).Event.Event.EventId);
            Assert.AreEqual(0, parker.ParkedEvents.Count);
        }

        [Test]
        public void explicit_nak_with_retry_correctly_tracks_the_available_client_slots()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMaxRetriesOf(10)
                    .WithMessageParker(parker)
                    .StartFromBeginning());
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 10, "foo", "bar");
            var id1 = Guid.NewGuid();
            var ev = Helper.BuildFakeEvent(id1, "type", "streamName", 0);
            sub.HandleReadCompleted(new[]
            {
                ev,
            }, 1, false);

            for (int i = 1; i < 11; i++)
            {
                sub.NotAcknowledgeMessagesProcessed(corrid, new[] { id1 }, NakAction.Retry, "a reason from client.");
                Assert.AreEqual(i + 1, envelope1.Replies.Count);
            }

            Assert.That(parker.ParkedEvents, Has.No.Member(ev));

            //This time should be parked
            sub.NotAcknowledgeMessagesProcessed(corrid, new[] { id1 }, NakAction.Retry, "a reason from client.");
            Assert.AreEqual(11, envelope1.Replies.Count);
            Assert.That(parker.ParkedEvents, Has.Member(ev));
        }

        [Test]
        public void explicit_nak_with_park_correctly_tracks_the_available_client_slots()
        {
            var envelope1 = new FakeEnvelope();
            var reader = new FakeCheckpointReader();
            var parker = new FakeMessageParker();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(reader)
                    .WithCheckpointWriter(new FakeCheckpointWriter(i => { }))
                    .WithMessageParker(parker)
                    .WithMaxRetriesOf(0)
                    .WithMessageTimeoutOf(TimeSpan.Zero)
                    .StartFromBeginning());
            reader.Load(null);
            var corrid = Guid.NewGuid();
            sub.AddClient(corrid, Guid.NewGuid(), envelope1, 1, "foo", "bar");

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            sub.HandleReadCompleted(new[]
            {
                Helper.BuildFakeEvent(id1, "type", "streamName", 0),
                Helper.BuildFakeEvent(id2, "type", "streamName", 1),
                Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 2)
            }, 1, false);

            Assert.AreEqual(1, envelope1.Replies.Count);

            sub.NotAcknowledgeMessagesProcessed(corrid, new[] { id1 }, NakAction.Park, "a reason from client.");
            Assert.AreEqual(2, envelope1.Replies.Count);
            Assert.That(parker.ParkedEvents, Has.Exactly(1).Matches<ResolvedEvent>(_ => _.Event.EventId == id1));

            sub.NotAcknowledgeMessagesProcessed(corrid, new[] { id2 }, NakAction.Park, "a reason from client.");
            Assert.That(parker.ParkedEvents, Has.Exactly(1).Matches<ResolvedEvent>(_ => _.Event.EventId == id2));
            Assert.AreEqual(3, envelope1.Replies.Count);
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
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(new FakeCheckpointReader())
                    .WithMessageParker(new FakeMessageParker())
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), new FakeEnvelope(), 1, "foo", "bar");
            Assert.IsTrue(sub.HasClients);
            Assert.AreEqual(1, sub.ClientCount);
        }
    }

    [TestFixture]
    public class RemoveClientTests
    {
        [Test]
        public void unsubscribing_a_client_retries_inflight_messages_immediately()
        {
            var client1Envelope = new FakeEnvelope();
            var client2Envelope = new FakeEnvelope();

            var fakeCheckpointReader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(fakeCheckpointReader)
                    .WithMessageParker(new FakeMessageParker())
                    .PreferRoundRobin()
                    .StartFromCurrent()
                    .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            fakeCheckpointReader.Load(null);

            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), client1Envelope, 10, "foo", "bar");
            var client2Id = Guid.NewGuid();
            sub.AddClient(client2Id, Guid.NewGuid(), client2Envelope, 10, "foo", "bar");


            Assert.IsTrue(sub.HasClients);
            Assert.AreEqual(2, sub.ClientCount);

            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0));
            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1));

            Assert.AreEqual(1, client1Envelope.Replies.Count);
            Assert.AreEqual(1, client2Envelope.Replies.Count);

            sub.RemoveClientByCorrelationId(client2Id, false);
            Assert.AreEqual(1, sub.ClientCount);

            // Message 2 should be retried on client 1 as it wasn't acked.
            Assert.AreEqual(2, client1Envelope.Replies.Count);
            Assert.AreEqual(1, client2Envelope.Replies.Count);
        }

        [Test]
        public void disconnecting_a_client_retries_inflight_messages_immediately()
        {
            var client1Envelope = new FakeEnvelope();
            var client2Envelope = new FakeEnvelope();

            var fakeCheckpointReader = new FakeCheckpointReader();
            var sub = new Core.Services.PersistentSubscription.PersistentSubscription(
                PersistentSubscriptionParamsBuilder.CreateFor("streamName", "groupName")
                    .WithEventLoader(new FakeStreamReader(x => { }))
                    .WithCheckpointReader(fakeCheckpointReader)
                    .WithMessageParker(new FakeMessageParker())
                    .PreferRoundRobin()
                    .StartFromCurrent()
            .WithCheckpointWriter(new FakeCheckpointWriter(x => { })));

            fakeCheckpointReader.Load(null);

            sub.AddClient(Guid.NewGuid(), Guid.NewGuid(), client1Envelope, 10, "foo", "bar");
            var connectionId = Guid.NewGuid();
            sub.AddClient(Guid.NewGuid(), connectionId, client2Envelope, 10, "foo", "bar");

            Assert.IsTrue(sub.HasClients);
            Assert.AreEqual(2, sub.ClientCount);

            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 0));
            sub.NotifyLiveSubscriptionMessage(Helper.BuildFakeEvent(Guid.NewGuid(), "type", "streamName", 1));

            Assert.AreEqual(1, client1Envelope.Replies.Count);
            Assert.AreEqual(1, client2Envelope.Replies.Count);

            sub.RemoveClientByConnectionId(connectionId);

            Assert.AreEqual(1, sub.ClientCount);

            // Message 2 should be retried on client 1 as it wasn't acked.
            Assert.AreEqual(2, client1Envelope.Replies.Count);
            Assert.AreEqual(1, client2Envelope.Replies.Count);
        }
    }

    [TestFixture, Ignore("very long test")]
    public class DeadlockTest : TestWithNode
    {
        [Test]
        public void read_whilst_ack_doesnt_deadlock_with_request_response_dispatcher()
        {
            var eventStoreConnection = BuildConnection(_node);
            eventStoreConnection.ConnectAsync().Wait();

            var persistentSubscriptionSettings = PersistentSubscriptionSettings.Create().Build();
            var userCredentials = new UserCredentials("admin", "changeit");
            eventStoreConnection.CreatePersistentSubscriptionAsync("TestStream", "TestGroup", persistentSubscriptionSettings, userCredentials).Wait();

            const int count = 5000;
            eventStoreConnection.AppendToStreamAsync("TestStream", ExpectedVersion.Any, CreateEvent().Take(count)).Wait();


            var received = 0;
            var manualResetEventSlim = new ManualResetEventSlim();
            var sub1 = eventStoreConnection.ConnectToPersistentSubscription("TestStream", "TestGroup", (sub, ev) =>
            {
                received++;
                if (received == count)
                {
                    manualResetEventSlim.Set();
                }
            },
                (sub, reason, ex) => { });
            Assert.IsTrue(manualResetEventSlim.Wait(TimeSpan.FromSeconds(30)), "Failed to receive all events in 2 minutes. Assume event store is deadlocked.");
            sub1.Stop(TimeSpan.FromSeconds(10));
            eventStoreConnection.Close();

        }

        private static IEnumerable<EventData> CreateEvent()
        {
            while (true)
            {
                yield return new EventData(Guid.NewGuid(), "testtype", false, new byte[0], new byte[0]);
            }
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

    class FakeStreamReader : IPersistentSubscriptionStreamReader
    {
        private readonly Action<int> _action;

        public FakeStreamReader(Action<int> action)
        {
            _action = action;
        }

        public void BeginReadEvents(string stream, int startEventNumber, int countToLoad, int batchSize, bool resolveLinkTos,
            Action<ResolvedEvent[], int, bool> onEventsFound)
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

    class FakeMessageParker : IPersistentSubscriptionMessageParker
    {
        private Action<int?> _readEndSequenceCompleted;
        private Action<ResolvedEvent, OperationResult> _parkMessageCompleted;
        public List<ResolvedEvent> ParkedEvents = new List<ResolvedEvent>();
        private readonly Action _deleteAction;

        public FakeMessageParker() { }

        public FakeMessageParker(Action deleteAction)
        {
            _deleteAction = deleteAction;
        }

        public int MarkedAsProcessed { get; private set; }

        public void ReadEndSequenceCompleted(int sequence)
        {
            if (_readEndSequenceCompleted != null) _readEndSequenceCompleted(sequence);
        }

        public void ParkMessageCompleted(int idx, OperationResult result)
        {
            if (_parkMessageCompleted != null) _parkMessageCompleted(ParkedEvents[idx], result);
        }

        public void BeginParkMessage(ResolvedEvent ev, string reason, Action<ResolvedEvent, OperationResult> completed)
        {
            ParkedEvents.Add(ev);
            _parkMessageCompleted = completed;
        }

        public void BeginReadEndSequence(Action<int?> completed)
        {
            _readEndSequenceCompleted = completed;
        }

        public void BeginMarkParkedMessagesReprocessed(int sequence)
        {
            MarkedAsProcessed = sequence;
        }
        public void BeginDelete(Action<IPersistentSubscriptionMessageParker> completed)
        {
            if (_deleteAction != null)
            {
                _deleteAction();
            }
        }
    }


    class FakeCheckpointWriter : IPersistentSubscriptionCheckpointWriter
    {
        private readonly Action<int> _action;
        private readonly Action _deleteAction;

        public FakeCheckpointWriter(Action<int> action, Action deleteAction = null)
        {
            _action = action;
            _deleteAction = deleteAction;
        }

        public void BeginWriteState(int state)
        {
            _action(state);
        }

        public void BeginDelete(Action<IPersistentSubscriptionCheckpointWriter> completed)
        {
            if (_deleteAction != null)
            {
                _deleteAction();
            }
        }
    }
}
