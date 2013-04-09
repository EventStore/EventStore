// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

using System;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_truncating_database: SpecificationWithDirectoryPerTestFixture
    {
        [Test]
        public void everything_should_go_fine()
        {
            var miniNode = new MiniNode(PathName);
            miniNode.Start();

            var tcpPort = miniNode.TcpEndPoint.Port;
            var httpPort = miniNode.HttpEndPoint.Port;
            const int cnt = 50;
            var countdown = new CountdownEvent(cnt);

            // --- first part of events
            WriteEvents(cnt, miniNode, countdown);
            Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Too long writing first part of events.");
            countdown.Reset();

            // -- set up truncation
            var truncatePosition = miniNode.Db.Config.WriterCheckpoint.ReadNonFlushed();
            miniNode.Db.Config.TruncateCheckpoint.Write(truncatePosition);
            miniNode.Db.Config.TruncateCheckpoint.Flush();

            // --- second part of events
            WriteEvents(cnt, miniNode, countdown);
            Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Too long writing second part of events.");
            countdown.Reset();

            miniNode.Shutdown(keepDb: true, keepPorts: true);

            // --- first restart and truncation
            miniNode = new MiniNode(PathName, tcpPort, httpPort);
            
            miniNode.Start();
            Assert.AreEqual(-1, miniNode.Db.Config.TruncateCheckpoint.Read());
            Assert.That(miniNode.Db.Config.WriterCheckpoint.Read(), Is.GreaterThanOrEqualTo(truncatePosition));

            // -- third part of events
            WriteEvents(cnt, miniNode, countdown);
            Assert.IsTrue(countdown.Wait(TimeSpan.FromSeconds(10)), "Too long writing third part of events.");
            countdown.Reset();

            miniNode.Shutdown(keepDb: true, keepPorts: true);

            // -- second restart
            miniNode = new MiniNode(PathName, tcpPort, httpPort);
            Assert.AreEqual(-1, miniNode.Db.Config.TruncateCheckpoint.Read());
            miniNode.Start();

            // -- if we get here -- then everything is ok
            miniNode.Shutdown();
        }

        private static void WriteEvents(int cnt, MiniNode miniNode, CountdownEvent countdown)
        {
            for (int i = 0; i < cnt; ++i)
            {
                miniNode.Node.MainQueue.Publish(
                    new ClientMessage.WriteEvents(Guid.NewGuid(),
                                                  new CallbackEnvelope(m =>
                                                  {
                                                      Assert.IsInstanceOf<ClientMessage.WriteEventsCompleted>(m);
                                                      countdown.Signal();
                                                  }),
                                                  false,
                                                  "test-stream",
                                                  ExpectedVersion.Any,
                                                  new[]
                                                  {
                                                      new Event(Guid.NewGuid(), "test-event-type", false, new byte[4000], null)
                                                  }));
            }
        }
    }
}
