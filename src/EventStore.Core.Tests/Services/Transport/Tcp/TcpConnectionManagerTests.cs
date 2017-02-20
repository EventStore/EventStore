using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Tcp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Transport.Tcp;
using System.Net.Sockets;
using EventStore.Core.Authentication;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Authentication;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using System.Threading;

namespace EventStore.Core.Tests.Services.Transport.Tcp
{
    [TestFixture]
    public class TcpConnectionManagerTests
    {
        public void when_handling_trusted_write_on_external_service()
        {
            var package = new TcpPackage(TcpCommand.WriteEvents, TcpFlags.TrustedWrite, Guid.NewGuid(), null, null, new byte[] { });

            var dummyConnection = new DummyTcpConnection();

            var tcpConnectionManager = new TcpConnectionManager(
                Guid.NewGuid().ToString(), TcpServiceType.External, new ClientTcpDispatcher(), new V1ClientTcpDispatcher(),
                InMemoryBus.CreateTest(), dummyConnection, InMemoryBus.CreateTest(), new InternalAuthenticationProvider(new Core.Helpers.IODispatcher(InMemoryBus.CreateTest(), new NoopEnvelope()), new StubPasswordHashAlgorithm(), 1),
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), (man, err) => { });

            tcpConnectionManager.ProcessPackage(package);

            var data = dummyConnection.ReceivedData.Last();
            var receivedPackage = TcpPackage.FromArraySegment(data);
           
            Assert.AreEqual(receivedPackage.Command, TcpCommand.BadRequest, "Expected Bad Request but got {0}", receivedPackage.Command);
        }

        public void when_handling_trusted_write_on_internal_service()
        {
            ManualResetEvent waiter = new ManualResetEvent(false);
            ClientMessage.WriteEvents publishedWrite = null;
            var evnt = new Event(Guid.NewGuid(), "TestEventType", true, new byte[] { }, new byte[] { });
            var write = new TcpClientMessageDto.WriteEvents(
                Guid.NewGuid().ToString(),
                ExpectedVersion.Any,
                new[] { new TcpClientMessageDto.NewEvent(evnt.EventId.ToByteArray(), evnt.EventType, evnt.IsJson ? 1 : 0, 0, evnt.Data, evnt.Metadata) },
                false);

            var package = new TcpPackage(TcpCommand.WriteEvents, Guid.NewGuid(), write.Serialize());
            var dummyConnection = new DummyTcpConnection();
            var publisher = InMemoryBus.CreateTest();

            publisher.Subscribe(new AdHocHandler<ClientMessage.WriteEvents>(x => {
                publishedWrite = x;
                waiter.Set();
            }));

            var tcpConnectionManager = new TcpConnectionManager(
                Guid.NewGuid().ToString(), TcpServiceType.Internal, new ClientTcpDispatcher(), new V1ClientTcpDispatcher(),
                publisher, dummyConnection, publisher, new InternalAuthenticationProvider(new Core.Helpers.IODispatcher(publisher, new NoopEnvelope()), new StubPasswordHashAlgorithm(), 1),
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), (man, err) => { });

            tcpConnectionManager.ProcessPackage(package);

            if (!waiter.WaitOne(TimeSpan.FromSeconds(5)))
            {
                throw new Exception("Timed out waiting for events.");
            }
            Assert.AreEqual(evnt.EventId, publishedWrite.Events.First().EventId, "Expected the published write to be the event that was sent through the tcp connection manager to be the event {0} but got {1}", evnt.EventId, publishedWrite.Events.First().EventId);
        }
    }

    internal class DummyTcpConnection : ITcpConnection
    {
        public Guid ConnectionId
        {
            get
            {
                return Guid.NewGuid();
            }
        }

        public bool IsClosed
        {
            get
            {
                return false;
            }
        }

        public IPEndPoint LocalEndPoint
        {
            get
            {
                return new IPEndPoint(IPAddress.Loopback, 2); ;
            }
        }

        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return new IPEndPoint(IPAddress.Loopback, 1);
            }
        }

        public int SendQueueSize
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public event Action<ITcpConnection, SocketError> ConnectionClosed;

        public void Close(string reason)
        {
            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, SocketError.Shutdown);
        }

        public IEnumerable<ArraySegment<byte>> ReceivedData;
        public void EnqueueSend(IEnumerable<ArraySegment<byte>> data)
        {
            ReceivedData = data;
        }

        public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback)
        {
            throw new NotImplementedException();
        }
    }
}
