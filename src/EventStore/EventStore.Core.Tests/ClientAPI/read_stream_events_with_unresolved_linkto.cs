using System;
using System.Linq;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using StreamMetadata = EventStore.ClientAPI.StreamMetadata;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class read_stream_events_with_unresolved_linkto : SpecificationWithMiniNode
    {
        private EventData[] _testEvents;

        protected override void When()
        {
            _conn.SetStreamMetadata(
                "$all", -1, StreamMetadata.Build().SetReadRole(SystemRoles.All),
                new UserCredentials(SystemUsers.Admin, SystemUsers.DefaultAdminPassword));

            _testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
            _conn.AppendToStream("stream", ExpectedVersion.EmptyStream, _testEvents);
            _conn.AppendToStream(
                "links", ExpectedVersion.EmptyStream,
                new EventData(
                    Guid.NewGuid(), EventStore.ClientAPI.Common.SystemEventTypes.LinkTo, false,
                    Encoding.UTF8.GetBytes("0@stream"), null));
            _conn.DeleteStream("stream", ExpectedVersion.Any);
        }

        [Test, Category("LongRunning")]
        public void ensure_deleted_stream()
        {
            var res = _conn.ReadStreamEventsForward("stream", 0, 100, false);
            Assert.AreEqual(SliceReadStatus.StreamNotFound, res.Status);
            Assert.AreEqual(0, res.Events.Length);
        }

        [Test, Category("LongRunning")]
        public void returns_unresolved_linkto()
        {
            var read = _conn.ReadStreamEventsForward("links", 0, 1, true);
            Assert.AreEqual(1, read.Events.Length);
            Assert.IsNull(read.Events[0].Event);
            Assert.IsNotNull(read.Events[0].Link);
        }
    }
}
