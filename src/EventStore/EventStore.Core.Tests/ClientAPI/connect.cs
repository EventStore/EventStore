using System;
using System.Net;
using System.Threading;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class connect : SpecificationWithDirectoryPerTestFixture
    {
        [Test]
        [Category("Network")]
        public void should_not_throw_exception_when_server_is_down()
        {
            Assert.Inconclusive("Reconnection tests are very unstable.");

            using (var connection = EventStoreConnection.Create())
            {
                Assert.DoesNotThrow(() => connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348)));
            }
        }

        [Test]
        [Category("Network")]
        public void should_throw_exception_when_trying_to_reopen_closed_connection()
        {
            Assert.Inconclusive("Reconnection tests are very unstable.");

            var settings = ConnectionSettings.Create()
                                             .LimitReconnectionsTo(0)
                                             .SetReconnectionDelayTo(TimeSpan.FromMilliseconds(0));

            using (var connection = EventStoreConnection.Create(settings))
            {
                var reconnected = new ManualResetEventSlim();
                connection.Reconnecting += (_, __) => reconnected.Set();

                connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348));

                if (!reconnected.Wait(TimeSpan.FromSeconds(120))) // TCP connection timeout might might be even 60 seconds
                    Assert.Fail("Reconnection took too long.");

                Assert.Throws<InvalidOperationException>(
                    () => connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348)),
                        "EventStoreConnection has been closed");
            }
        }

        [Test]
        [Category("Network")]
        public void should_close_connection_after_configured_amount_of_failed_reconnections()
        {
            Assert.Inconclusive("In progress item might still be present in the processing queue when close occurs.");

            var settings = ConnectionSettings.Create()
                                             .LimitReconnectionsTo(0)
                                             .SetReconnectionDelayTo(TimeSpan.FromMilliseconds(0));

            using (var connection = EventStoreConnection.Create(settings))
            {
                var reconnected = new ManualResetEventSlim();
                connection.Reconnecting += (_, __) => reconnected.Set();

                connection.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12348));

                if (!reconnected.Wait(TimeSpan.FromSeconds(120))) // TCP connection timeout might might be even 60 seconds
                    Assert.Fail("Reconnection took too long.");

                Assert.Throws<InvalidOperationException>(() => connection.CreateStream("stream", Guid.NewGuid(), false, new byte[0]),
                                                         "EventStoreConnection [127.0.0.1:12348] is still active.");
            }
        }
    }
}