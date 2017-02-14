﻿using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("ClientAPI"), Category("LongRunning")]
    public class read_all_events_backward_should : SpecificationWithMiniNode
    {
        private EventData[] _testEvents;

        protected override void When()
        {
            _conn.SetStreamMetadataAsync("$all", -1,
                                    StreamMetadata.Build().SetReadRole(SystemRoles.All),
                                    DefaultData.AdminCredentials)
            .Wait();

            _testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
            _conn.AppendToStreamAsync("stream", ExpectedVersion.EmptyStream, _testEvents).Wait();
        }

        [Test, Category("LongRunning")]
        public void return_empty_slice_if_asked_to_read_from_start()
        {
            var read = _conn.ReadAllEventsBackwardAsync(Position.Start, 1, false).Result;
            Assert.That(read.IsEndOfStream, Is.True);
            Assert.That(read.Events.Length, Is.EqualTo(0));
        }

        [Test, Category("LongRunning")]
        public void return_partial_slice_if_not_enough_events()
        {
            var read = _conn.ReadAllEventsBackwardAsync(Position.End, 30, false).Result;
            Assert.That(read.Events.Length, Is.LessThan(30));
            Assert.That(EventDataComparer.Equal(_testEvents.Reverse().ToArray(),
                                                read.Events.Take(_testEvents.Length).Select(x => x.Event).ToArray()));
        }

        [Test, Category("LongRunning")]
        public void return_events_in_reversed_order_compared_to_written()
        {
            var read = _conn.ReadAllEventsBackwardAsync(Position.End, _testEvents.Length, false).Result;
            Assert.That(EventDataComparer.Equal(_testEvents.Reverse().ToArray(), 
                                                read.Events.Select(x => x.Event).ToArray()));
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_all_one_by_one_until_end_of_stream()
        {
            var all = new List<RecordedEvent>();
            var position = Position.End;
            AllEventsSlice slice;

            while (!(slice = _conn.ReadAllEventsBackwardAsync(position, 1, false).Result).IsEndOfStream)
            {
                all.Add(slice.Events.Single().Event);
                position = slice.NextPosition;
            }

            Assert.That(EventDataComparer.Equal(_testEvents.Reverse().ToArray(), all.Take(_testEvents.Length).ToArray()));
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_events_slice_at_time()
        {
            var all = new List<RecordedEvent>();
            var position = Position.End;
            AllEventsSlice slice;

            while (!(slice = _conn.ReadAllEventsBackwardAsync(position, 5, false).Result).IsEndOfStream)
            {
                all.AddRange(slice.Events.Select(x => x.Event));
                position = slice.NextPosition;
            }

            Assert.That(EventDataComparer.Equal(_testEvents.Reverse().ToArray(), all.Take(_testEvents.Length).ToArray()));
        }

        [Test]
        [Category("Network")]
        public void throw_when_got_int_max_value_as_maxcount()
        {

            Assert.Throws<ArgumentException>(
                () => _conn.ReadAllEventsBackwardAsync(Position.Start, int.MaxValue, resolveLinkTos: false));
        }
    }
}
