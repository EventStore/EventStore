﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.ClientAPI.Helpers;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture]
    public class isjson_flag_on_event: SpecificationWithDirectory
    {
        private MiniNode _node;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _node = new MiniNode(PathName);
            _node.Start();
        }

        [TearDown]
        public override void TearDown()
        {
            _node.Shutdown();
            base.TearDown();
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void should_be_preserved_with_all_possible_write_and_read_methods()
        {
            const string stream = "should_be_preserved_with_all_possible_write_methods";
            using (var connection = TestConnection.To(_node, TcpType.Normal))
            {
                connection.Connect();

                connection.AppendToStream(
                    stream,
                    ExpectedVersion.Any,
                    new EventData(Guid.NewGuid(), "some-type", true, Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}"), null),
                    new EventData(Guid.NewGuid(), "some-type", true, null, Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}")),
                    new EventData(Guid.NewGuid(), "some-type", true, Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}"), Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}")));

                using (var transaction = connection.StartTransaction(stream, ExpectedVersion.Any))
                {
                    transaction.Write(
                        new EventData(Guid.NewGuid(), "some-type", true, Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}"), null),
                        new EventData(Guid.NewGuid(), "some-type", true, null, Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}")),
                        new EventData(Guid.NewGuid(), "some-type", true, Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}"), Helper.UTF8NoBom.GetBytes("{\"some\":\"json\"}")));
                    transaction.Commit();
                }

                var done = new ManualResetEventSlim();
                _node.Node.MainQueue.Publish(new ClientMessage.ReadStreamEventsForward(
                    Guid.NewGuid(), Guid.NewGuid(), new CallbackEnvelope(message =>
                    {
                        Assert.IsInstanceOf<ClientMessage.ReadStreamEventsForwardCompleted>(message);
                        var msg = (ClientMessage.ReadStreamEventsForwardCompleted) message;
                        Assert.AreEqual(Data.ReadStreamResult.Success, msg.Result);
                        Assert.AreEqual(6, msg.Events.Length);
                        Assert.IsTrue(msg.Events.All(x => (x.OriginalEvent.Flags & PrepareFlags.IsJson) != 0));

                        done.Set();
                    }), stream, 0, 100, false, false, null, null));
                Assert.IsTrue(done.Wait(10000), "Read wasn't completed in time.");
            }
        }
    }
}
