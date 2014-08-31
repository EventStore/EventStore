using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.Tests.Services.Replication;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services
{
    [TestFixture]
    public class PersistentSubscription_should
    {
        private PersistentSubscription _subscription;
        private FakeEnvelope _firstClientEnvelope;
        private Guid _firstClientCorrelationId;
        private FakeEnvelope _secondClientEnvelope;
        private Guid _secondClientCorrelationId;
        private int _fetchFrom;
        private int _lastCheckpoint;
        private Guid _firstClientConnectionId;
        private Guid _secondClientConnectionId;
        private FakeCheckpointReader _checkpointReader;

        [SetUp]
        public void Setup()
        {
            _checkpointReader = new FakeCheckpointReader();
            _subscription = new PersistentSubscription(false, "sub1", "stream", "groupName",false,false, new FakeEventLoader(x => _fetchFrom = x),
                _checkpointReader, new FakeCheckpointWriter(x => _lastCheckpoint = x));

            _checkpointReader.Load(null);

            _firstClientEnvelope = new FakeEnvelope();
            _firstClientCorrelationId = Guid.NewGuid();
            _firstClientConnectionId = Guid.NewGuid();
            _subscription.AddClient(_firstClientCorrelationId, _firstClientConnectionId, _firstClientEnvelope, 10, "user", "from");

            _secondClientEnvelope = new FakeEnvelope();
            _secondClientCorrelationId = Guid.NewGuid();
            _secondClientConnectionId = Guid.NewGuid();
            _subscription.AddClient(_secondClientCorrelationId, _secondClientConnectionId, _secondClientEnvelope, 10, "user", "from");
        }

        [Test]
        public void start_in_idle_mode()
        {
            var subscription = new PersistentSubscription(false, "sub1", "stream", "groupName",false,false, new FakeEventLoader(x => _fetchFrom = x), _checkpointReader,
                new FakeCheckpointWriter(x => _lastCheckpoint = x));

            Assert.AreEqual(PersistentSubscriptionState.Idle, subscription.State);
        }
        
        [Test]
        public void transition_to_push_mode_if_there_is_no_checkpoint()
        {
            var subscription = new PersistentSubscription(false, "sub1", "stream", "groupName", false, false,new FakeEventLoader(x => _fetchFrom = x), _checkpointReader,
                new FakeCheckpointWriter(x => _lastCheckpoint = x));

            _checkpointReader.Load(null);

            Assert.AreEqual(PersistentSubscriptionState.Push, subscription.State);
        }
        
        [Test]
        public void transition_to_pull_mode_if_there_is_checkpoint()
        {
            var subscription = new PersistentSubscription(false, "sub1", "stream", "groupName", false, false, new FakeEventLoader(x => _fetchFrom = x), _checkpointReader,
                new FakeCheckpointWriter(x => _lastCheckpoint = x));

            _checkpointReader.Load(156);
            //TODO competing FIX ME!
            Assert.AreNotEqual(_lastCheckpoint, -122020292); //shouldnt fail and should make compiler error shutup
            Assert.AreEqual(PersistentSubscriptionState.Pull, subscription.State);
        }

        [Test]
        public void push_event_to_least_busy_client()
        {
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId,new Guid[0]);

            _subscription.Push(
                new ResolvedEvent(
                    new EventRecord(
                        1, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson,
                        "type", new byte[0], new byte[0])));

            Assert.AreEqual(0, _firstClientEnvelope.Replies.Count);
            Assert.AreEqual(1, _secondClientEnvelope.Replies.Count);

        }

        [Test]
        public void switch_to_pull_mode_without_forwarding_an_event_when_all_clients_are_busy()
        {
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId, new Guid[0]);
            _subscription.Push(
                new ResolvedEvent(
                    new EventRecord(
                        55, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson,
                        "type", new byte[0], new byte[0])));

            Assert.AreEqual(0, _firstClientEnvelope.Replies.Count);
            Assert.AreEqual(0, _secondClientEnvelope.Replies.Count);
            Assert.AreEqual(PersistentSubscriptionState.Pull, _subscription.State);
        }

        [Test]
        public void fetch_some_events_when_in_pull_mode_and_new_client_connects()
        {
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId,new Guid[0]);

            _subscription.Push(
                new ResolvedEvent(
                    new EventRecord(
                        55, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson,
                        "type", new byte[0], new byte[0])));

            _fetchFrom = 0;

            _subscription.AddClient(Guid.NewGuid(),Guid.NewGuid(),new FakeEnvelope(), 10, "user", "from");
            Assert.AreEqual(PersistentSubscriptionState.Pull, _subscription.State);
            Assert.AreEqual(55, _fetchFrom);
        }

        [Test]
        public void try_to_push_some_events_when_fetch_request_is_complete()
        {
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId,new Guid[0]);

            var evnt = new ResolvedEvent(new EventRecord(55, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            _subscription.Push(evnt);
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.HandleReadEvents(new[]{evnt}, 56);

            Assert.AreEqual(PersistentSubscriptionState.Pull, _subscription.State);
            Assert.AreEqual(1, _firstClientEnvelope.Replies.Count);
        }

        [Test]
        public void refrain_from_pushing_events_when_transitioning_from_pull_to_push_mode()
        {
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId, new Guid[0]);

            var evnt1 = new ResolvedEvent(new EventRecord(55, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            var evnt2 = new ResolvedEvent(new EventRecord(56, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            _subscription.Push(evnt1);
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId,new Guid[0]);

            _subscription.Push(evnt2);

            Assert.AreEqual(0, _firstClientEnvelope.Replies.Count);
            Assert.AreEqual(0, _secondClientEnvelope.Replies.Count);
        }

        [Test]
        public void deduplicate_events_pushed_and_fetched_when_transitioning_from_pull_to_push_mode()
        {
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId, new Guid[0]);

            var evnt1 = new ResolvedEvent(new EventRecord(55, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            var evnt2 = new ResolvedEvent(new EventRecord(56, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            var evnt3 = new ResolvedEvent(new EventRecord(57, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            _subscription.Push(evnt1);
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId,new Guid[0]);

            _subscription.Push(evnt2);
            _subscription.HandleReadEvents(new[] { evnt1, evnt2, evnt3 }, 58);

            Assert.AreEqual(3, _firstClientEnvelope.Replies.Count);
            Assert.AreEqual(PersistentSubscriptionState.Push, _subscription.State);
        }

        [Test]
        public void deduplicate_events_pushed_and_fetched_when_transitioning_from_pull_to_push_mode_2()
        {
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId, new Guid[0]);

            var evnt1 = new ResolvedEvent(new EventRecord(55, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            var evnt2 = new ResolvedEvent(new EventRecord(56, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            var evnt3 = new ResolvedEvent(new EventRecord(57, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            _subscription.Push(evnt1);
            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new Guid[0]);
            _subscription.AcknowledgeMessagesProcessed(_secondClientCorrelationId, new Guid[0]);

            _subscription.HandleReadEvents(new[] { evnt1, evnt2, evnt3 }, 58);
            _subscription.Push(evnt2);

            Assert.AreEqual(3, _firstClientEnvelope.Replies.Count);
            Assert.AreEqual(PersistentSubscriptionState.Push, _subscription.State);
        }

        [Test]
        public void reroute_unprocessed_events_from_disconnected_clients()
        {
            var processedEventId = Guid.NewGuid();
            var evnt1 = new ResolvedEvent(new EventRecord(55, 0, Guid.NewGuid(), processedEventId, 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            var evnt2 = new ResolvedEvent(new EventRecord(56, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            var evnt3 = new ResolvedEvent(new EventRecord(57, 0, Guid.NewGuid(), Guid.NewGuid(), 0, 0, "stream", 0, DateTime.MinValue, PrepareFlags.IsJson, "type", new byte[0], new byte[0]));
            _subscription.Push(evnt1);
            _subscription.Push(evnt2);
            _subscription.Push(evnt3);

            _subscription.AcknowledgeMessagesProcessed(_firstClientCorrelationId, new[] { processedEventId });
            _subscription.RemoveClientByConnectionId(_firstClientConnectionId);

            Assert.AreEqual(2, _secondClientEnvelope.Replies.Count);
            Assert.IsTrue(_secondClientEnvelope.Replies.Cast<ClientMessage.PersistentSubscriptionStreamEventAppeared>().Any(x => x.Event.Event.EventNumber == 56));
            Assert.IsTrue(_secondClientEnvelope.Replies.Cast<ClientMessage.PersistentSubscriptionStreamEventAppeared>().Any(x => x.Event.Event.EventNumber == 57));

            Assert.AreEqual(2, _firstClientEnvelope.Replies.Count);
            Assert.IsTrue(_firstClientEnvelope.Replies.Cast<ClientMessage.PersistentSubscriptionStreamEventAppeared>().Any(x => x.Event.Event.EventNumber == 55));
            Assert.IsTrue(_firstClientEnvelope.Replies.Cast<ClientMessage.PersistentSubscriptionStreamEventAppeared>().Any(x => x.Event.Event.EventNumber == 57));
        }

        private class FakeEventLoader : IPersistentSubscriptionEventLoader
        {
            private readonly Action<int> _action;

            public FakeEventLoader(Action<int> action)
            {
                _action = action;
            }

            public void BeginLoadState(PersistentSubscription subscription, int startEventNumber, int freeSlots, Action<ResolvedEvent[], int> onFetchCompleted)
            {
                _action(startEventNumber);
            }
        }

        private class FakeCheckpointReader : IPersistentSubscriptionCheckpointReader
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

        private class FakeCheckpointWriter : IPersistentSubscriptionCheckpointWriter
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
}