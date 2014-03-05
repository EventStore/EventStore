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
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.v8;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.v8
{
    [TestFixture]
    public class when_creating_v8_projection
    {
        private ProjectionStateHandlerFactory _stateHandlerFactory;

        [SetUp]
        public void Setup()
        {
            _stateHandlerFactory = new ProjectionStateHandlerFactory();
        }

        [Test, Category("v8")]
        public void api_can_be_used() 
        {
            var ver = Js1.ApiVersion();
            Console.WriteLine(ver);
        }

        [Test, Category("v8")]
        public void api_can_be_used2()
        {
            var ver = Js1.ApiVersion();
            Console.WriteLine(ver);
        }

        [Test, Category("v8")]
        public void it_can_be_created()
        {
            using (_stateHandlerFactory.Create("JS", @""))
            {
            }
        }

        [Test, Category("v8")]
        public void it_can_log_messages()
        {
            string m = null;
            using (_stateHandlerFactory.Create("JS", @"log(""Message1"");", logger: s => m = s))
            {
            }
            Assert.AreEqual("Message1", m);
        }

        [Test, Category("v8"), ExpectedException(typeof(Js1Exception), ExpectedMessage = "SyntaxError:", MatchType = MessageMatch.StartsWith)]
        public void js_syntax_errors_are_reported()
        {
            string m = null;
            using (_stateHandlerFactory.Create("JS", @"log(1;", logger: s => m = s))
            {
            }
        }

        [Test, Category("v8"), ExpectedException(typeof(Js1Exception), ExpectedMessage = "123")]
        public void js_exceptions_errors_are_reported()
        {
            string m = null;
            using (_stateHandlerFactory.Create("JS", @"throw 123;", logger: s => m = s))
            {
            }
        }

        [Test, Category("v8"), ExpectedException(typeof(Js1Exception), ExpectedMessage = "terminated", MatchType = MessageMatch.Contains)]
        public void long_compilation_times_out()
        {
            string m = null;
            using (_stateHandlerFactory.Create("JS",
                @"
                            var i = 0;
                            while (true) i++;
                ",
                logger: s => m = s,
                cancelCallbackFactory: (timeout, action) => ThreadPool.QueueUserWorkItem(state =>
                    {
                        Console.WriteLine("Calling a callback in " + timeout + "ms");
                        Thread.Sleep(timeout);
                        action();
                    })))
            {
            }
        }

        [Test, Category("v8"), ExpectedException(typeof(Js1Exception), ExpectedMessage = "terminated", MatchType = MessageMatch.Contains)]
        public void long_execution_times_out()
        {
            //string m = null;
            using (var h = _stateHandlerFactory.Create("JS",
                @"
                    fromAll().when({
                        $any: function (s, e) {
                            log('1');
                            var i = 0;
                            while (true) i++;
                        }
                    });
                ",
                logger: Console.WriteLine,
                cancelCallbackFactory: (timeout, action) => ThreadPool.QueueUserWorkItem(state =>
                {
                    Console.WriteLine("Calling a callback in " + timeout + "ms");
                    Thread.Sleep(timeout);
                    action();
                })))
            {
                h.Initialize();
                string newState;
                EmittedEventEnvelope[] emittedevents;
                h.ProcessEvent(
                    "partition", CheckpointTag.FromPosition(0, 100, 50), "stream", "event", "", Guid.NewGuid(), 1, "", "{}",
                    out newState, out emittedevents);
            }
        }

        [Test, Category("v8"), ExpectedException(typeof(Js1Exception), ExpectedMessage = "terminated", MatchType = MessageMatch.Contains)]
        public void long_post_processing_times_out()
        {
            //string m = null;
            using (var h = _stateHandlerFactory.Create("JS",
                @"
                    fromAll().when({
                        $any: function (s, e) {
                            return {};
                        }
                    })
                    .transformBy(function(s){
                            log('1');
                            var i = 0;
                            while (true) i++;
                    });
                ",
                logger: Console.WriteLine,
                cancelCallbackFactory: (timeout, action) => ThreadPool.QueueUserWorkItem(state =>
                {
                    Console.WriteLine("Calling a callback in " + timeout + "ms");
                    Thread.Sleep(timeout);
                    action();
                })))
            {
                h.Initialize();
                string newState;
                EmittedEventEnvelope[] emittedevents;
                h.ProcessEvent(
                    "partition", CheckpointTag.FromPosition(0, 100, 50), "stream", "event", "", Guid.NewGuid(), 1, "", "{}",
                    out newState, out emittedevents);
                h.TransformStateToResult();
            }
        }

        [Test, Explicit, Category("v8"), Category("Manual")]
        public void long_execution_times_out_many()
        {
            //string m = null;
            for (var i = 0; i < 10; i++)
            {
                Console.WriteLine(i);
                try
                {
                    using (var h = _stateHandlerFactory.Create(
                        "JS", @"
                    fromAll().when({
                        $any: function (s, e) {
                            log('1');
                            var i = 0;
                            while (true) i++;
                        }
                    });
                ", logger: Console.WriteLine,
                        cancelCallbackFactory: (timeout, action) => ThreadPool.QueueUserWorkItem(
                            state =>
                                {
                                    Console.WriteLine("Calling a callback in " + timeout + "ms");
                                    Thread.Sleep(timeout);
                                    action();
                                })))
                    {
                        h.Initialize();
                        string newState;
                        EmittedEventEnvelope[] emittedevents;
                        h.ProcessEvent(
                            "partition", CheckpointTag.FromPosition(0, 100, 50), "stream", "event", "", Guid.NewGuid(), 1,
                            "", "{}", out newState, out emittedevents);
                    }
                }
                catch (Js1Exception)
                {
                }
            }
            Assert.Pass();
        }


    }
}
