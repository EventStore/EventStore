using System;
using System.Linq;
using System.Text;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class read_events_forward_with_soft_deleted_stream_should : SpecificationWithMiniNode
    {
        private const string StreamName = "test-stream-1";

        protected override void When()
        {
	    //Write original data
            var events = new EventData[4];
            for (int i = 0; i < 4; i++)
            {
                events[i] = new EventData(Guid.NewGuid(), "eventType", true, GetSomeData(), new byte[0]);
            }
            _conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, events).Wait();

	    //Soft delete stream
            _conn.DeleteStreamAsync(StreamName, ExpectedVersion.Any, false).Wait();

	    //Continue writing from $truncateBefore
            _conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any,
                new EventData(Guid.NewGuid(), "eventType", true, GetSomeData(), new byte[0])).Wait();
        }

        [Test, Category("LongRunning")]
        public void read_from_start_with_batch_size_greater_than_truncate_before()
        {
            var slice = _conn.ReadStreamEventsForwardAsync(StreamName, StreamPosition.Start, 10, false).Result;
	    Assert.AreEqual(1, slice.Events.Count());
        }

        [Test, Category("LongRunning")]
        public void read_from_start_with_batch_size_less_than_truncate_before()
        {
            var slice = _conn.ReadStreamEventsForwardAsync(StreamName, StreamPosition.Start, 1, false).Result;
	    Assert.AreEqual(1, slice.Events.Count());
        }

        [Test, Category("LongRunning")]
	public void read_from_start_with_batch_size_equal_to_truncate_before()
        {
            var slice = _conn.ReadStreamEventsForwardAsync(StreamName, StreamPosition.Start, 4, false).Result;
	    Assert.AreEqual(1, slice.Events.Count());
        }

	private static byte[] GetSomeData()
        {
            return Encoding.UTF8.GetBytes("{\"Key\":\"Value\"}");
        }
    }
}