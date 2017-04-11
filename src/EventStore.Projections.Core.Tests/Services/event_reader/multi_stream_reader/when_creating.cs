﻿using System;
using System.Collections.Generic;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.multi_stream_reader
{
    [TestFixture]
    public class when_creating : TestFixtureWithExistingEvents
    {
        private string[] _abStreams;
        private Dictionary<string, long> _ab12Tag;
        private new RealTimeProvider _timeProvider;

        [SetUp]
        public void setup()
        {
            _timeProvider = new RealTimeProvider();
            _ab12Tag = new Dictionary<string, long> { { "a", 1 }, { "b", 2 } };
            _abStreams = new[] {"a", "b"};
        }

        [Test]
        public void it_can_be_created()
        {
            new MultiStreamEventReader(
                _ioDispatcher, _bus, Guid.NewGuid(), null, 0, _abStreams, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            new MultiStreamEventReader(
                _ioDispatcher, null, Guid.NewGuid(), null, 0, _abStreams, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_event_reader_id_throws_argument_exception()
        {
            new MultiStreamEventReader(
                _ioDispatcher, _bus, Guid.Empty, null, 0, _abStreams, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_streams_throws_argument_null_exception()
        {
            new MultiStreamEventReader(
                _ioDispatcher, _bus, Guid.NewGuid(), null, 0, null, _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void null_time_provider_throws_argument_null_exception()
        {
            new MultiStreamEventReader(_ioDispatcher, _bus, Guid.NewGuid(), null, 0, _abStreams, _ab12Tag, false, null);
        }

        [Test, ExpectedException(typeof(ArgumentException))]
        public void empty_streams_throws_argument_exception()
        {
            new MultiStreamEventReader(
                _ioDispatcher, _bus, Guid.NewGuid(), null, 0, new string[0], _ab12Tag, false, _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void invalid_from_tag_throws_argument_exception()
        {
            new MultiStreamEventReader(
                _ioDispatcher, _bus, Guid.NewGuid(), null, 0, _abStreams,
                new Dictionary<string, long> {{"a", 1}, {"c", 2}}, false, _timeProvider);
        }
    }
}
