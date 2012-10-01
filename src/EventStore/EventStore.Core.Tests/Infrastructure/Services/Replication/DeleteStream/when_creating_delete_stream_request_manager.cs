using System;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Replication.DeleteStream
{
    [TestFixture]
    public class when_creating_create_stream_request_manager
    {
        [Test]
        public void null_publisher_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => new DeleteStreamTwoPhaseRequestManager(null, 3, 3));
        }

        [Test]
        public void zero_prepare_ack_count_throws_argument_out_range()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DeleteStreamTwoPhaseRequestManager(new FakePublisher(), 0, 3));
        }

        [Test]
        public void zero_commit_ack_count_throws_argument_out_range()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DeleteStreamTwoPhaseRequestManager(new FakePublisher(), 3, 0));
        }


        [Test]
        public void negative_commit_ack_count_throws_argument_out_range()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DeleteStreamTwoPhaseRequestManager(new FakePublisher(), 3, -1));
        }


        [Test]
        public void negative_prepare_ack_count_throws_argument_out_range()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DeleteStreamTwoPhaseRequestManager(new FakePublisher(), -1, 3));
        }
    }
}
