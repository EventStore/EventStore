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

using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class subscribe_to_all_should: SpecificationWithDirectory
    {
        private const int Timeout = 10000;
        
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

        [Test, Category("LongRunning")]
        public void allow_multiple_subscriptions()
        {
            const string stream = "subscribe_to_all_should_allow_multiple_subscriptions";
            using (var store = TestConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(2);

                using (store.SubscribeToAll(false, (s, x) => appeared.Signal(), (s, r, e) => dropped.Signal()).Result)
                using (store.SubscribeToAll(false, (s, x) => appeared.Signal(), (s, r, e) => dropped.Signal()).Result)
                {
                    var create = store.AppendToStreamAsync(stream, ExpectedVersion.EmptyStream, TestEvent.NewTestEvent());
                    Assert.IsTrue(create.Wait(Timeout), "StreamCreateAsync timed out.");

                    Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event timed out.");
                }
            }
        }

        [Test, Category("LongRunning")]
        public void catch_deleted_events_as_well()
        {
            const string stream = "subscribe_to_all_should_catch_created_and_deleted_events_as_well";
            using (var store = TestConnection.Create())
            {
                store.Connect(_node.TcpEndPoint);
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                using (store.SubscribeToAll(false, (s, x) => appeared.Signal(), (s, r, e) => dropped.Signal()).Result)
                {
                    var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                    Assert.IsTrue(delete.Wait(Timeout), "DeleteStreamAsync timed out.");

                    Assert.IsTrue(appeared.Wait(Timeout), "Appeared countdown event didn't fire in time.");
                }
            }
        }
    }
}
