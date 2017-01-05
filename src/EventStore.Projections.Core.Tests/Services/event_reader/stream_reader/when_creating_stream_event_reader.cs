﻿using System;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.stream_reader
{
    [TestFixture]
    public class when_creating_stream_event_reader : TestFixtureWithExistingEvents
    {
        [Test]
        public void it_can_be_created()
        {
            new StreamEventReader(_bus, Guid.NewGuid(), null, "stream", 0, new RealTimeProvider(), false,
                produceStreamDeletes: false);
        }

        [Test]
        public void null_publisher_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => {
            new StreamEventReader(null, Guid.NewGuid(), null, "stream", 0, new RealTimeProvider(), false,
                produceStreamDeletes: false);
            });
        }

        [Test]
        public void empty_event_reader_id_throws_argument_exception()
        {
            Assert.Throws<ArgumentException>(() => {
            new StreamEventReader(_bus, Guid.Empty, null, "stream", 0, new RealTimeProvider(), false,
                produceStreamDeletes: false);
            });
        }

        [Test]
        public void null_stream_name_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => {
            new StreamEventReader(_bus, Guid.NewGuid(), null, null, 0, new RealTimeProvider(), false,
                produceStreamDeletes: false);
            });
        }

        [Test]
        public void empty_stream_name_throws_argument_exception()
        {
            Assert.Throws<ArgumentException>(() => {
            new StreamEventReader(_bus, Guid.NewGuid(), null, "", 0, new RealTimeProvider(), false,
                produceStreamDeletes: false);
            });
        }

        [Test]
        public void negative_event_sequence_number_throws_argument_exception()
        {
            Assert.Throws<ArgumentException>(() => {
            new StreamEventReader(_bus, Guid.NewGuid(), null, "", -1, new RealTimeProvider(), false,
                produceStreamDeletes: false);
            });
        }
    }
}
