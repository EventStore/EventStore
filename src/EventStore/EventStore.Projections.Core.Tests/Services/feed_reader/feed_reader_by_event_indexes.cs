using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.feed_reader
{
    namespace feed_reader_by_event_indexes
    {

        [TestFixture]
        class feed_reader_by_event_indexes : TestFixtureWithFeedReaderService
        {
            private QuerySourcesDefinition _querySourcesDefinition;
            private CheckpointTag _fromPosition;
            private int _maxEvents;

            protected override void Given()
            {
                base.Given();
                var pos1 = ExistingEvent("test-stream", "type1", "{}", "{Data: 1}");
                var pos2 = ExistingEvent("test-stream", "type1", "{}", "{Data: 2}");
                var pos3 = ExistingEvent("test-stream", "type2", "{}", "{Data: 3}");

                ExistingEvent("$et-type1", "$>", TFPosToMetadata(pos1), "0@test-stream");
                NoStream("$et-type2");
                //NOTE: the following events should be late written
                //ExistingEvent("$et-type2", "$>", "", "2@test-stream");
                //ExistingEvent("$et-type1", "$>", "", "1@test-stream");

                _querySourcesDefinition = new QuerySourcesDefinition
                    {
                        AllStreams = true,
                        Events = new[] {"type1", "type2"},
                        Options = new QuerySourcesDefinitionOptions {UseEventIndexes = true}
                    };
                _fromPosition = CheckpointTag.FromStreamPositions(new Dictionary<string, int>{{"$et-type1", -1}, {"$et-type2", -1}});
                _maxEvents = 2;
            }

            private string TFPosToMetadata(TFPos tfPos)
            {
                return string.Format(@"{{""$c"":{0},""$p"":{1}}}", tfPos.CommitPosition, tfPos.PreparePosition);
            }

            protected override IEnumerable<Message> When()
            {
                yield return
                    new FeedReaderMessage.ReadPage(
                        Guid.NewGuid(), new PublishEnvelope(GetInputQueue()), _querySourcesDefinition, _fromPosition,
                        _maxEvents);
            }

            [Test]
            public void publishes_feed_page_message()
            {
                var feedPage = _consumer.HandledMessages.OfType<FeedReaderMessage.FeedPage>().ToArray();
                Assert.AreEqual(1, feedPage.Length);
            }

            [Test]
            public void returns_correct_last_reader_position()
            {
                var feedPage = _consumer.HandledMessages.OfType<FeedReaderMessage.FeedPage>().Single();
                Assert.AreEqual(
                    CheckpointTag.FromStreamPositions(new Dictionary<string, int> {{"$et-type1", 0}, {"$et-type2", -1}}),
                    feedPage.LastReaderPosition);
            }
        }
    }
}