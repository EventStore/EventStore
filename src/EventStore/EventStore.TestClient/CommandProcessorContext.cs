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
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.TestClient
{
    /// <summary>
    /// This context is passed to the instances of <see cref="ICmdProcessor"/>
    /// when they are executed. It can also be used for async syncrhonization
    /// </summary>
    public class CommandProcessorContext
    {
        public int ExitCode;
        public Exception Error;
        public string Reason;

        /// <summary>
        /// Current logger of the test client
        /// </summary>
        public readonly ILogger Log;
        public readonly Client Client;

        private readonly ManualResetEventSlim _doneEvent;
        private int _completed;

        public CommandProcessorContext(Client client, ILogger log, ManualResetEventSlim doneEvent)
        {
            Client = client;
            Log = log;
            _doneEvent = doneEvent;
        }

        public void Completed(int exitCode = (int)Common.Utils.ExitCode.Success, Exception error = null, string reason = null)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                ExitCode = exitCode;

                Error = error;
                Reason = reason;

                _doneEvent.Set();
            }
        }

        public void Fail(Exception exc = null, string reason = null)
        {
            Completed((int)Common.Utils.ExitCode.Error, exc, reason);
        }

        public void Success()
        {
            Completed();
        }

        public void IsAsync()
        {
            _doneEvent.Reset();
        }

        public void WaitForCompletion()
        {
            if (Client.Options.Timeout < 0)
                _doneEvent.Wait();
            else
            {
                if (!_doneEvent.Wait(Client.Options.Timeout*1000))
                    throw new TimeoutException("Command didn't finished within timeout.");
            }
        }

        public TimeSpan Time(Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.Elapsed;
        }
    }
}