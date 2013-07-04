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
using System.IO;
using System.Text;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Services.v8;
using EventStore.Projections.Core.v8;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.v8
{
    [TestFixture]
    public class v8_internals
    {
        private ProjectionStateHandlerFactory _stateHandlerFactory;

        [SetUp]
        public void Setup()
        {
            _stateHandlerFactory = new ProjectionStateHandlerFactory();
        }

        private static readonly string _jsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prelude");
        private Action<int, Action> _cancelCallbackFactory;
        private Js1.CommandHandlerRegisteredDelegate _commandHandlerRegisteredCallback;
        private Js1.ReverseCommandHandlerDelegate _reverseCommandHandlerDelegate;

        [Test, Explicit, Category("v8"), Category("Manual"), ExpectedException(typeof(Js1Exception))]
        public void long_execution_of_non_v8_code_does_not_crash()
        {
            _cancelCallbackFactory = (timeout, action) => ThreadPool.QueueUserWorkItem(state =>
                {
                    Console.WriteLine("Calling a callback in " + timeout + "ms");
                    Thread.Sleep(timeout);
                    action();
                });
            Action<string> logger = Console.WriteLine;


            Func<string, Tuple<string, string>> getModuleSource = name =>
                {
                    var fullScriptFileName = Path.GetFullPath(Path.Combine(_jsPath, name + ".js"));
                    var scriptSource = File.ReadAllText(fullScriptFileName, Helper.UTF8NoBom);
                    return Tuple.Create(scriptSource, fullScriptFileName);
                };


            var preludeSource = getModuleSource("1Prelude");
            var prelude = new PreludeScript(preludeSource.Item1, preludeSource.Item2, getModuleSource, _cancelCallbackFactory, logger);
            try
            {
                //var cancelToken = 123;
                prelude.ScheduleTerminateExecution();
                Thread.Sleep(500);
                _commandHandlerRegisteredCallback = (name, handle) => { };
                _reverseCommandHandlerDelegate = (name, body) => { };
                Js1.CompileQuery(
                    prelude.GetHandle(), "log(1);", "fn", _commandHandlerRegisteredCallback,
                    _reverseCommandHandlerDelegate);

                prelude.CancelTerminateExecution();
            }
            catch
            {
                prelude.Dispose(); // clean up unmanaged resources if failed to create
                throw;
            }

        }

    }
}
