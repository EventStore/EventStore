﻿using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Utils;
using EventStore.Core.Services;
using NUnit.Framework;
using System;
using System.Threading;
using ResolvedEvent = EventStore.ClientAPI.ResolvedEvent;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit
{
    [TestFixture]
    [Category("ClientAPI"), Category("LongRunning")]
    public class subscribe_to_stream_with_link_to_event_with_event_number_greater_than_int_maxvalue : MiniNodeWithExistingRecords
    {
        private const string StreamName = "subscribe_to_stream_with_link_to_event_with_event_number_greater_than_int_maxvalue";
        private const long intMaxValue = (long)int.MaxValue;

        private string _linkedStreamName = "linked-" + StreamName;
        private Guid _event1Id;

        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private ResolvedEvent _receivedEvent;

        public override void WriteTestScenario()
        {
            var event1 = WriteSingleEvent(StreamName, intMaxValue + 1, new string('.', 3000));
            WriteSingleEvent(StreamName, intMaxValue + 2, new string('.', 3000));
            _event1Id = event1.EventId;
        }

        public override void Given()
        {
            _store = BuildConnection(Node);
            _store.ConnectAsync().Wait();

            _store.SubscribeToStreamAsync(_linkedStreamName, true, HandleEvent).Wait();
            _store.AppendToStreamAsync(_linkedStreamName, ExpectedVersion.NoStream,
                    new EventData(Guid.NewGuid(),
                        SystemEventTypes.LinkTo, false, Helper.UTF8NoBom.GetBytes(
                            string.Format("{0}@{1}", intMaxValue + 1, StreamName)
                        ), null)).Wait();
        }

        private void HandleEvent(EventStoreSubscription sub, ResolvedEvent resolvedEvent)
        {
            _receivedEvent = resolvedEvent;
            _resetEvent.Set();
        }

        [Test]
        public void should_receive_and_resolve_the_linked_event()
        {
            Assert.IsTrue(_resetEvent.WaitOne(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(intMaxValue + 1, _receivedEvent.Event.EventNumber);
            Assert.AreEqual(_event1Id, _receivedEvent.Event.EventId);
            Assert.AreEqual(intMaxValue + 1, _receivedEvent.Event.EventNumber);
        }
    }
}