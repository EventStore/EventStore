using System;
using System.Net;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class connect : SpecificationWithDirectoryPerTestFixture
    {
        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            base.TestFixtureTearDown();
        }

        [Test]
        [Category("Network")]
        public void should_not_throw_exception_when_server_is_down()
        {
            using (var connection = EventStoreConnection.Create())
            {
                connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348));
            }
        }

        [Test]
        [Category("Network")]
        public void should_throw_exception_when_trying_to_reopen_closed_connection()
        {
            var settings = ConnectionSettings.Create()
                .LimitReconnectionsTo(0)
                .SetReconnectionDelayTo(TimeSpan.Zero);

            using (var connection = EventStoreConnection.Create(settings))
            {
                connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348));

                Thread.Sleep(4000); //Ensure reconnection attempt

                Assert.Throws<InvalidOperationException>(
                    () => connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348)),
                        "EventStoreConnection has been closed");
            }
        }

        [Test]
        [Category("Network")]
        public void should_close_connection_after_configured_amount_of_failed_reconnections()
        {
            var settings = ConnectionSettings.Create()
                .LimitReconnectionsTo(0)
                .SetReconnectionDelayTo(TimeSpan.Zero);

            using (var connection = EventStoreConnection.Create(settings))
            {
                connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348));

                Thread.Sleep(4000); //Ensure reconnection attempt

                Assert.Throws<InvalidOperationException>(
                    () => connection.CreateStream("stream", Guid.NewGuid(), false, new byte[0]),
                        "EventStoreConnection [127.0.0.1:12348] is not active.");
            }
        }
    }
}