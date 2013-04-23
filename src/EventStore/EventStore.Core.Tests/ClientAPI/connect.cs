using System;
using System.Net;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helper;
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
            var ip = IPAddress.Loopback;
            int port = TcpPortsHelper.GetAvailablePort(ip);
            try
            {
                using (var connection = TestConnection.Create(new IPEndPoint(ip, port)))
                {
                    Assert.DoesNotThrow(() => connection.Connect());
                }
            }
            finally
            {
                TcpPortsHelper.ReturnPort(port);
            }
        }

        [Test]
        [Category("Network")]
        public void should_throw_exception_when_trying_to_reopen_closed_connection()
        {
            var closed = new ManualResetEventSlim();
            var settings = ConnectionSettings.Create()
                                             .EnableVerboseLogging()
                                             .UseCustomLogger(ClientApiLoggerBridge.Default)
                                             .LimitReconnectionsTo(0)
                                             .SetReconnectionDelayTo(TimeSpan.FromMilliseconds(0))
                                             .OnClosed((x, r) => closed.Set())
                                             .FailOnNoServerResponse();

            var ip = IPAddress.Loopback;
            int port = TcpPortsHelper.GetAvailablePort(ip);
            try
            {
                using (var connection = EventStoreConnection.Create(settings, new IPEndPoint(ip, port)))
                {
                    connection.Connect();

                    if (!closed.Wait(TimeSpan.FromSeconds(120))) // TCP connection timeout might be even 60 seconds
                        Assert.Fail("Connection timeout took too long.");

                    Assert.That(() => connection.Connect(),
                                Throws.Exception.InstanceOf<AggregateException>()
                                      .With.InnerException.InstanceOf<InvalidOperationException>());
                }
            }
            finally
            {
                TcpPortsHelper.ReturnPort(port);
            }
        }

        [Test]
        [Category("Network")]
        public void should_close_connection_after_configured_amount_of_failed_reconnections()
        {
            var closed = new ManualResetEventSlim();
            var settings = ConnectionSettings.Create()
                                             .EnableVerboseLogging()
                                             .UseCustomLogger(ClientApiLoggerBridge.Default)
                                             .LimitReconnectionsTo(1)
                                             .SetReconnectionDelayTo(TimeSpan.FromMilliseconds(0))
                                             .OnClosed((x, r) => closed.Set())
                                             .OnConnected(x => Console.WriteLine("Connected..."))
                                             .OnReconnecting(x => Console.WriteLine("Reconnecting..."))
                                             .OnDisconnected(x => Console.WriteLine("Disconnected..."))
                                             .OnErrorOccurred((x, exc) => Console.WriteLine("Error: {0}", exc))
                                             .FailOnNoServerResponse();

            var ip = IPAddress.Loopback;
            int port = TcpPortsHelper.GetAvailablePort(ip);
            try
            {
                using (var connection = EventStoreConnection.Create(settings, new IPEndPoint(ip, port)))
                {
                    connection.Connect();

                    if (!closed.Wait(TimeSpan.FromSeconds(120))) // TCP connection timeout might be even 60 seconds
                        Assert.Fail("Connection timeout took too long.");

                    Assert.That(() => connection.AppendToStream("stream", ExpectedVersion.EmptyStream, TestEvent.NewTestEvent()),
                                Throws.Exception.InstanceOf<AggregateException>()
                                .With.InnerException.InstanceOf<InvalidOperationException>());
                }
            }
            finally
            {
                TcpPortsHelper.ReturnPort(port);
            }
        }
    }
}